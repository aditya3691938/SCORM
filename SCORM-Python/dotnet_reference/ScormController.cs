using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using AdityaLMS.Core.Interfaces;
using AdityaLMS.Api.DTOs;

namespace AdityaLMS.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ScormController : ControllerBase
    {
        private readonly IScormService _scormService;
        private readonly string _uploadBasePath;
        private readonly FileExtensionContentTypeProvider _contentTypeProvider;

        public ScormController(IScormService scormService)
        {
            _scormService = scormService;
            _uploadBasePath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
            _contentTypeProvider = new FileExtensionContentTypeProvider();
            
            // Add custom MIME types
            _contentTypeProvider.Mappings[".wasm"] = "application/wasm";
            _contentTypeProvider.Mappings[".json"] = "application/json";
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadPackage([FromForm] Microsoft.AspNetCore.Http.IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            var ext = Path.GetExtension(file.FileName).ToLower();
            if (ext != ".zip")
                return BadRequest("Only .zip SCORM packages are supported.");

            var scormId = $"scorm_{Guid.NewGuid().ToString("N")[..8]}";
            var tempZip = Path.GetTempFileName();

            using (var stream = new FileStream(tempZip, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var (launchHref, error) = await _scormService.ProcessScormPackageAsync(tempZip, scormId, _uploadBasePath);
            System.IO.File.Delete(tempZip);

            if (!string.IsNullOrEmpty(error))
            {
                return BadRequest(new { error });
            }

            return Ok(new
            {
                scormId,
                launchHref,
                launchUrl = $"/api/scorm/content/{scormId}/{launchHref}"
            });
        }

        [HttpGet("content/{scormId}/{*filename}")]
        public IActionResult ServeContent(string scormId, string filename)
        {
            var packageDir = Path.Combine(_uploadBasePath, "scorm", scormId);
            if (!Directory.Exists(packageDir))
                return NotFound("SCORM package not found.");

            var resolvedRel = _scormService.ResolveScormAsset(packageDir, filename ?? "");
            if (string.IsNullOrEmpty(resolvedRel))
                return NotFound("File not found in SCORM package.");

            var fullPath = Path.Combine(packageDir, resolvedRel);
            if (!System.IO.File.Exists(fullPath))
                return NotFound("File not found.");

            if (!_contentTypeProvider.TryGetContentType(fullPath, out var contentType))
            {
                contentType = "application/octet-stream";
            }

            return PhysicalFile(fullPath, contentType, enableRangeProcessing: true);
        }

        [HttpPost("telemetry/{scormId}")]
        public IActionResult RecordTelemetry(string scormId, [FromBody] ScormTelemetryDto dto)
        {
            // Here you can log to AssessmentAttempt or LessonReview in AdityaLMS DbContext
            return Ok(new
            {
                status = "success",
                scormId,
                recordedStatus = dto.Status,
                score = dto.Score
            });
        }
    }
}

