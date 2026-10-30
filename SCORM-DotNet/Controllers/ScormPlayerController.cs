using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using SCORM_Implementation.Models;
using SCORM_Implementation.Services;

namespace SCORM_Implementation.Controllers
{
    public class ScormPlayerController : Controller
    {
        private readonly IScormService _scormService;
        private readonly string _packagesFolder;
        private readonly FileExtensionContentTypeProvider _contentTypeProvider;

        public ScormPlayerController(IScormService scormService, IWebHostEnvironment env)
        {
            _scormService = scormService;
            _packagesFolder = Path.Combine(env.ContentRootPath, "packages");
            Directory.CreateDirectory(_packagesFolder);

            _contentTypeProvider = new FileExtensionContentTypeProvider();
            _contentTypeProvider.Mappings[".wasm"] = "application/wasm";
            _contentTypeProvider.Mappings[".json"] = "application/json";
            _contentTypeProvider.Mappings[".woff2"] = "font/woff2";
            _contentTypeProvider.Mappings[".woff"] = "font/woff";
            _contentTypeProvider.Mappings[".mp4"] = "video/mp4";
            _contentTypeProvider.Mappings[".webm"] = "video/webm";
        }

        [HttpGet("player/{packageId}")]
        public IActionResult Player(string packageId)
        {
            var packageDir = Path.Combine(_packagesFolder, packageId);
            if (!Directory.Exists(packageDir))
            {
                return NotFound($"Package '{packageId}' not found.");
            }

            var manifestJson = Path.Combine(packageDir, "res_manifest.json");
            var manifestXml = Path.Combine(packageDir, "imsmanifest.xml");

            string title = packageId.Replace('_', ' ');
            string schemaVer = "1.2";
            string launchHref = "index.html";
            bool isRise360 = false;

            if (System.IO.File.Exists(manifestJson))
            {
                try
                {
                    var json = System.IO.File.ReadAllText(manifestJson);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("title", out var t)) title = t.GetString() ?? title;
                    if (root.TryGetProperty("schema_version", out var sv)) schemaVer = sv.GetString() ?? schemaVer;
                    if (root.TryGetProperty("launch_href", out var lh)) launchHref = lh.GetString() ?? launchHref;
                    if (root.TryGetProperty("is_rise_360", out var ir)) isRise360 = ir.GetBoolean();
                }
                catch { }
            }
            else if (System.IO.File.Exists(manifestXml))
            {
                var meta = _scormService.ParseManifest(manifestXml);
                title = meta.Title;
                schemaVer = meta.SchemaVersion;
                launchHref = meta.LaunchHref ?? launchHref;
                isRise360 = meta.IsRise360;
            }

            var resolved = _scormService.ResolveScormAsset(packageDir, launchHref);
            if (!string.IsNullOrEmpty(resolved)) launchHref = resolved;

            var statePath = Path.Combine(packageDir, "cmi_state.json");
            string initialStateJson = "{}";
            if (System.IO.File.Exists(statePath))
            {
                try
                {
                    var stateObj = System.IO.File.ReadAllText(statePath);
                    using var sdoc = JsonDocument.Parse(stateObj);
                    if (sdoc.RootElement.TryGetProperty("cmi", out var cmiElem))
                    {
                        initialStateJson = cmiElem.GetRawText();
                    }
                }
                catch { }
            }

            var vm = new ScormPlayerViewModel
            {
                PackageId = packageId,
                Title = title,
                SchemaVersion = schemaVer,
                LaunchHref = launchHref,
                LaunchUrl = $"/content/{packageId}/{launchHref}",
                IsRise360 = isRise360,
                InitialStateJson = initialStateJson
            };

            return View(vm);
        }

        [HttpGet("content/{packageId}/{*filename}")]
        public IActionResult ServeContent(string packageId, string? filename)
        {
            var packageDir = Path.Combine(_packagesFolder, packageId);
            if (!Directory.Exists(packageDir)) return NotFound("SCORM package not found.");

            var resolvedRel = _scormService.ResolveScormAsset(packageDir, filename ?? "");
            if (string.IsNullOrEmpty(resolvedRel))
            {
                return NotFound($"File '{filename}' not found in SCORM package.");
            }

            var fullPath = Path.Combine(packageDir, resolvedRel);
            if (!System.IO.File.Exists(fullPath)) return NotFound("File not found.");

            if (!_contentTypeProvider.TryGetContentType(fullPath, out var contentType))
            {
                contentType = "application/octet-stream";
            }

            return PhysicalFile(fullPath, contentType, enableRangeProcessing: true);
        }

        [HttpPost("api/telemetry/{packageId}")]
        public async Task<IActionResult> SaveTelemetry(string packageId, [FromBody] ScormTelemetryDto dto)
        {
            var packageDir = Path.Combine(_packagesFolder, packageId);
            if (!Directory.Exists(packageDir)) return NotFound(new { error = "Package not found" });

            try
            {
                var statePath = Path.Combine(packageDir, "cmi_state.json");
                var stateJson = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
                await System.IO.File.WriteAllTextAsync(statePath, stateJson);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }

            return Ok(new { status = "success", packageId, recorded = true });
        }

        [HttpGet("api/state/{packageId}")]
        public async Task<IActionResult> GetState(string packageId)
        {
            var packageDir = Path.Combine(_packagesFolder, packageId);
            var statePath = Path.Combine(packageDir, "cmi_state.json");

            if (System.IO.File.Exists(statePath))
            {
                var json = await System.IO.File.ReadAllTextAsync(statePath);
                return Content(json, "application/json");
            }

            return Ok(new { cmi = new Dictionary<string, object>() });
        }

        [HttpPost("api/reset/{packageId}")]
        public IActionResult ResetState(string packageId)
        {
            var packageDir = Path.Combine(_packagesFolder, packageId);
            var statePath = Path.Combine(packageDir, "cmi_state.json");

            if (System.IO.File.Exists(statePath))
            {
                try { System.IO.File.Delete(statePath); } catch { }
            }

            return Ok(new { status = "success", message = "State reset." });
        }
    }
}

