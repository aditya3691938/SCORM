using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using SCORM_Implementation.Models;

namespace SCORM_Implementation.Services
{
    public class ScormService : IScormService
    {
        private readonly ILogger<ScormService> _logger;

        public ScormService(ILogger<ScormService> logger)
        {
            _logger = logger;
        }

        public async Task<ScormProcessResult> ProcessScormPackageAsync(string zipFilePath, string scormId, string destinationFolder)
        {
            if (!File.Exists(zipFilePath))
            {
                return new ScormProcessResult(false, null, "SCORM package file missing.", null);
            }

            var packageDir = Path.Combine(destinationFolder, scormId);
            Directory.CreateDirectory(packageDir);

            try
            {
                await Task.Run(() =>
                {
                    ZipFile.ExtractToDirectory(zipFilePath, packageDir, overwriteFiles: true);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract SCORM zip: {Path}", zipFilePath);
                return new ScormProcessResult(false, null, $"Extraction failed: {ex.Message}", null);
            }

            return FinalizeScormPackage(packageDir, scormId);
        }

        public async Task<ScormProcessResult> ProcessScormPackageStreamAsync(Stream zipStream, string scormId, string destinationFolder)
        {
            var packageDir = Path.Combine(destinationFolder, scormId);
            Directory.CreateDirectory(packageDir);

            var tempZip = Path.Combine(packageDir, "package.zip");
            try
            {
                using (var fs = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    if (zipStream.CanSeek) zipStream.Seek(0, SeekOrigin.Begin);
                    await zipStream.CopyToAsync(fs);
                }

                await Task.Run(() =>
                {
                    ZipFile.ExtractToDirectory(tempZip, packageDir, overwriteFiles: true);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract SCORM stream for {Id}", scormId);
                return new ScormProcessResult(false, null, $"Extraction failed: {ex.Message}", null);
            }

            return FinalizeScormPackage(packageDir, scormId);
        }

        private ScormProcessResult FinalizeScormPackage(string packageDir, string scormId)
        {
            // Search for imsmanifest.xml
            string? manifestPath = Path.Combine(packageDir, "imsmanifest.xml");
            if (!File.Exists(manifestPath))
            {
                manifestPath = Directory.GetFiles(packageDir, "imsmanifest.xml", SearchOption.AllDirectories).FirstOrDefault();
            }

            ScormManifestMetadata metadata;
            string? launchHref = null;

            if (!string.IsNullOrEmpty(manifestPath) && File.Exists(manifestPath))
            {
                metadata = ParseManifest(manifestPath);
                launchHref = metadata.LaunchHref;

                if (!string.IsNullOrEmpty(launchHref))
                {
                    var relBase = Path.GetRelativePath(packageDir, Path.GetDirectoryName(manifestPath) ?? packageDir);
                    if (!string.IsNullOrEmpty(relBase) && relBase != ".")
                    {
                        launchHref = Path.Combine(relBase, launchHref).Replace('\\', '/');
                    }

                    // Articulate Rise 360 Direct Route Check
                    var riseDirect = Path.Combine(packageDir, "scormcontent", "index.html");
                    if (launchHref.ToLower().Contains("scormdriver") && File.Exists(riseDirect))
                    {
                        launchHref = "scormcontent/index.html";
                        metadata = metadata with { LaunchHref = launchHref, IsRise360 = true };
                    }
                }
            }
            else
            {
                metadata = new ScormManifestMetadata(scormId, scormId, "SCORM", "1.2", null, false, new List<string>());
            }

            // Direct check for Rise 360
            if (string.IsNullOrEmpty(launchHref))
            {
                var risePath = Path.Combine(packageDir, "scormcontent", "index.html");
                if (File.Exists(risePath))
                {
                    launchHref = "scormcontent/index.html";
                    metadata = metadata with { LaunchHref = launchHref, IsRise360 = true };
                }
            }

            // Fallback candidates
            if (string.IsNullOrEmpty(launchHref))
            {
                string[] candidates = { "index.html", "story.html", "index_lms.html", "indexapi.html", "launch.html", "default.html" };
                var allFiles = Directory.GetFiles(packageDir, "*.*", SearchOption.AllDirectories);

                foreach (var candidate in candidates)
                {
                    var found = allFiles.FirstOrDefault(f => Path.GetFileName(f).Equals(candidate, StringComparison.OrdinalIgnoreCase));
                    if (found != null)
                    {
                        launchHref = Path.GetRelativePath(packageDir, found).Replace('\\', '/');
                        metadata = metadata with { LaunchHref = launchHref };
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(launchHref))
            {
                return new ScormProcessResult(false, null, "Could not identify launch HTML file in SCORM manifest or package directory.", metadata);
            }

            // Write metadata cache
            try
            {
                var cachePath = Path.Combine(packageDir, "res_manifest.json");
                var cacheJson = JsonSerializer.Serialize(new
                {
                    identifier = metadata.Identifier,
                    title = metadata.Title,
                    schema = metadata.Schema,
                    schema_version = metadata.SchemaVersion,
                    launch_href = launchHref,
                    is_rise_360 = metadata.IsRise360,
                    resources = metadata.Resources
                }, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(cachePath, cacheJson);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write res_manifest.json cache for {ScormId}", scormId);
            }

            return new ScormProcessResult(true, launchHref, null, metadata);
        }

        public ScormManifestMetadata ParseManifest(string manifestPath)
        {
            if (!File.Exists(manifestPath))
            {
                return new ScormManifestMetadata("", "Untitled SCORM Course", "SCORM", "1.2", null, false, new List<string>());
            }

            try
            {
                var xdoc = XDocument.Load(manifestPath);

                string identifier = xdoc.Root?.Attribute("identifier")?.Value ?? "";
                string schema = "SCORM";
                string schemaVersion = "1.2";
                string title = "Untitled SCORM Course";
                string? launchHref = null;
                var resources = new List<string>();

                var schemaElem = xdoc.Descendants().FirstOrDefault(e => e.Name.LocalName == "schema");
                if (schemaElem != null && !string.IsNullOrWhiteSpace(schemaElem.Value))
                {
                    schema = schemaElem.Value.Trim();
                }

                var schemaVerElem = xdoc.Descendants().FirstOrDefault(e => e.Name.LocalName == "schemaversion");
                if (schemaVerElem != null && !string.IsNullOrWhiteSpace(schemaVerElem.Value))
                {
                    schemaVersion = schemaVerElem.Value.Trim();
                }

                var orgTitleElem = xdoc.Descendants().FirstOrDefault(e => e.Name.LocalName == "organization")?
                                       .Descendants().FirstOrDefault(e => e.Name.LocalName == "title");
                if (orgTitleElem != null && !string.IsNullOrWhiteSpace(orgTitleElem.Value))
                {
                    title = orgTitleElem.Value.Trim();
                }

                var resourceElems = xdoc.Descendants().Where(e => e.Name.LocalName == "resource").ToList();
                foreach (var res in resourceElems)
                {
                    var href = res.Attribute("href")?.Value;
                    if (!string.IsNullOrEmpty(href))
                    {
                        var cleanHref = href.Replace('\\', '/');
                        resources.Add(cleanHref);
                        if (string.IsNullOrEmpty(launchHref))
                        {
                            launchHref = cleanHref;
                        }
                    }
                }

                return new ScormManifestMetadata(identifier, title, schema, schemaVersion, launchHref, false, resources);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse imsmanifest.xml at {ManifestPath}", manifestPath);
                return new ScormManifestMetadata("", "Untitled SCORM Course", "SCORM", "1.2", null, false, new List<string>());
            }
        }

        public string? ResolveScormAsset(string scormPackageDir, string requestedPath)
        {
            if (!Directory.Exists(scormPackageDir)) return null;

            var clean = requestedPath.Split('?')[0].Split('#')[0].TrimStart('/', '\\');
            var targetPath = Path.Combine(scormPackageDir, clean);

            // 1. Direct match
            if (File.Exists(targetPath))
            {
                return Path.GetRelativePath(scormPackageDir, targetPath).Replace('\\', '/');
            }

            // 2. Case-insensitive exact relative match
            var allFiles = Directory.GetFiles(scormPackageDir, "*.*", SearchOption.AllDirectories);
            var normalizedTarget = clean.Replace('\\', '/');

            foreach (var file in allFiles)
            {
                var rel = Path.GetRelativePath(scormPackageDir, file).Replace('\\', '/');
                if (string.Equals(rel, normalizedTarget, StringComparison.OrdinalIgnoreCase) ||
                    rel.EndsWith("/" + normalizedTarget, StringComparison.OrdinalIgnoreCase))
                {
                    return rel;
                }
            }

            // 3. Recursive fallback by filename
            var fileName = Path.GetFileName(clean);
            if (!string.IsNullOrEmpty(fileName))
            {
                var match = allFiles.FirstOrDefault(f => string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    return Path.GetRelativePath(scormPackageDir, match).Replace('\\', '/');
                }
            }

            // 4. Default launch files if request is directory or empty
            if (string.IsNullOrEmpty(clean) || clean.EndsWith('/'))
            {
                string[] candidates = { "scormcontent/index.html", "index.html", "story.html", "index_lms.html", "launch.html" };
                foreach (var cand in candidates)
                {
                    var candPath = Path.Combine(scormPackageDir, cand);
                    if (File.Exists(candPath))
                    {
                        return cand;
                    }
                }
            }

            return null;
        }

        public List<ScormPackageViewModel> GetAvailablePackages(string packagesDirectory)
        {
            var packages = new List<ScormPackageViewModel>();
            if (!Directory.Exists(packagesDirectory)) return packages;

            foreach (var dir in Directory.GetDirectories(packagesDirectory))
            {
                var id = Path.GetFileName(dir);
                var manifestJson = Path.Combine(dir, "res_manifest.json");
                var manifestXml = Path.Combine(dir, "imsmanifest.xml");

                string title = id.Replace('_', ' ');
                string schema = "SCORM";
                string schemaVer = "1.2";
                string launchHref = "index.html";
                bool isRise360 = false;

                if (File.Exists(manifestJson))
                {
                    try
                    {
                        var json = File.ReadAllText(manifestJson);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("title", out var t)) title = t.GetString() ?? title;
                        if (root.TryGetProperty("schema", out var s)) schema = s.GetString() ?? schema;
                        if (root.TryGetProperty("schema_version", out var sv)) schemaVer = sv.GetString() ?? schemaVer;
                        if (root.TryGetProperty("launch_href", out var lh)) launchHref = lh.GetString() ?? launchHref;
                        if (root.TryGetProperty("is_rise_360", out var ir)) isRise360 = ir.GetBoolean();
                    }
                    catch { }
                }
                else if (File.Exists(manifestXml))
                {
                    var meta = ParseManifest(manifestXml);
                    title = meta.Title;
                    schema = meta.Schema;
                    schemaVer = meta.SchemaVersion;
                    launchHref = meta.LaunchHref ?? launchHref;
                    isRise360 = meta.IsRise360;
                }

                var resolved = ResolveScormAsset(dir, launchHref);
                if (!string.IsNullOrEmpty(resolved)) launchHref = resolved;

                packages.Add(new ScormPackageViewModel
                {
                    Id = id,
                    Title = title,
                    Schema = schema,
                    SchemaVersion = schemaVer,
                    LaunchHref = launchHref,
                    LaunchUrl = $"/content/{id}/{launchHref}",
                    IsRise360 = isRise360
                });
            }

            return packages.OrderBy(p => p.Title).ToList();
        }
    }
}

