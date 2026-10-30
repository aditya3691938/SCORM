using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SCORM_Implementation.Models;
using SCORM_Implementation.Services;

namespace SCORM_Implementation.Controllers
{
    public class HomeController : Controller
    {
        private readonly IScormService _scormService;
        private readonly string _packagesFolder;

        public HomeController(IScormService scormService, IWebHostEnvironment env)
        {
            _scormService = scormService;
            _packagesFolder = Path.Combine(env.ContentRootPath, "packages");
            Directory.CreateDirectory(_packagesFolder);
        }

        [HttpGet]
        public IActionResult Index()
        {
            var packages = _scormService.GetAvailablePackages(_packagesFolder);
            return View(packages);
        }

        [HttpPost("upload")]
        [RequestSizeLimit(1024 * 1024 * 500)] // 500 MB
        public async Task<IActionResult> Upload(IFormFile scorm_file, [FromForm] string? package_id)
        {
            if (scorm_file == null || scorm_file.Length == 0)
            {
                TempData["Error"] = "No file selected.";
                return RedirectToAction(nameof(Index));
            }

            if (!scorm_file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Please upload a valid SCORM (.zip) archive.";
                return RedirectToAction(nameof(Index));
            }

            var cleanName = Path.GetFileNameWithoutExtension(scorm_file.FileName);
            var id = !string.IsNullOrWhiteSpace(package_id) ? package_id.Trim() : cleanName;
            id = string.Concat(id.Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_'));

            using var stream = scorm_file.OpenReadStream();
            var result = await _scormService.ProcessScormPackageStreamAsync(stream, id, _packagesFolder);

            if (!result.Success)
            {
                TempData["Error"] = $"SCORM extraction error: {result.Error}";
                return RedirectToAction(nameof(Index));
            }

            TempData["Success"] = $"Package '{id}' extracted successfully. Ready to launch!";
            return RedirectToAction("Player", "ScormPlayer", new { packageId = id });
        }

        [HttpGet("manifest/{packageId}")]
        public IActionResult Manifest(string packageId)
        {
            var packageDir = Path.Combine(_packagesFolder, packageId);
            if (!Directory.Exists(packageDir)) return NotFound(new { error = "Package not found" });

            var manifestJson = Path.Combine(packageDir, "res_manifest.json");
            if (System.IO.File.Exists(manifestJson))
            {
                return Content(System.IO.File.ReadAllText(manifestJson), "application/json");
            }

            var manifestXml = Path.Combine(packageDir, "imsmanifest.xml");
            if (System.IO.File.Exists(manifestXml))
            {
                return Ok(_scormService.ParseManifest(manifestXml));
            }

            return NotFound(new { error = "Manifest not found in package directory" });
        }
    }
}

