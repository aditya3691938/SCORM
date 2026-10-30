using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using AdityaLMS.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AdityaLMS.Infrastructure.Services
{
    public class ScormService : IScormService
    {
        private readonly ILogger<ScormService> _logger;

        public ScormService(ILogger<ScormService> logger)
        {
            _logger = logger;
        }

        public async Task<(string LaunchHref, string Error)> ProcessScormPackageAsync(string zipFilePath, string scormId, string destinationFolder)
        {
            if (!File.Exists(zipFilePath))
            {
                return (null, "SCORM package file missing.");
            }

            var packageDir = Path.Combine(destinationFolder, "scorm", scormId);
            Directory.CreateDirectory(packageDir);

            // 1. Extract ZIP
            try
            {
                await Task.Run(() =>
                {
                    ZipFile.ExtractToDirectory(zipFilePath, packageDir, overwriteFiles: true);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract SCORM package {ScormId}", scormId);
                return (null, $"Failed to extract SCORM zip package: {ex.Message}");
            }

            // 2. Search for imsmanifest.xml
            string manifestPath = Path.Combine(packageDir, "imsmanifest.xml");
            if (!File.Exists(manifestPath))
            {
                manifestPath = Directory.GetFiles(packageDir, "imsmanifest.xml", SearchOption.AllDirectories).FirstOrDefault();
            }

            string launchHref = null;

            if (!string.IsNullOrEmpty(manifestPath) && File.Exists(manifestPath))
            {
                try
                {
                    var xdoc = XDocument.Load(manifestPath);
                    // Locate first <resource> element regardless of XML namespace
                    var resource = xdoc.Descendants().FirstOrDefault(e => e.Name.LocalName == "resource");
                    if (resource != null)
                    {
                        var hrefAttr = resource.Attribute("href")?.Value;
                        if (!string.IsNullOrEmpty(hrefAttr))
                        {
                            var relBase = Path.GetRelativePath(packageDir, Path.GetDirectoryName(manifestPath) ?? packageDir);
                            if (!string.IsNullOrEmpty(relBase) && relBase != ".")
                            {
                                hrefAttr = Path.Combine(relBase, hrefAttr);
                            }

                            // Articulate Rise 360 Direct Player Check
                            var riseDirect = Path.Combine(packageDir, "scormcontent", "index.html");
                            if (hrefAttr.ToLower().Contains("scormdriver") && File.Exists(riseDirect))
                            {
                                return ("scormcontent/index.html", null);
                            }

                            launchHref = hrefAttr.Replace('\\', '/');
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing imsmanifest.xml for SCORM package {ScormId}", scormId);
                }
            }

            // Direct check for Rise 360 web player
            var risePath = Path.Combine(packageDir, "scormcontent", "index.html");
            if (File.Exists(risePath))
            {
                return ("scormcontent/index.html", null);
            }

            // Fallback candidates
            if (string.IsNullOrEmpty(launchHref))
            {
                string[] candidates = { "index.html", "story.html", "index_lms.html", "indexapi.html", "launch.html" };
                var allFiles = Directory.GetFiles(packageDir, "*.*", SearchOption.AllDirectories);

                foreach (var candidate in candidates)
                {
                    var found = allFiles.FirstOrDefault(f => Path.GetFileName(f).Equals(candidate, StringComparison.OrdinalIgnoreCase));
                    if (found != null)
                    {
                        var rel = Path.GetRelativePath(packageDir, found);
                        return (rel.Replace('\\', '/'), null);
                    }
                }
            }

            if (string.IsNullOrEmpty(launchHref))
            {
                return (null, "Could not identify launch HTML file in SCORM manifest or package directory.");
            }

            return (launchHref, null);
        }

        public string ResolveScormAsset(string scormPackageDir, string requestedPath)
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
                if (string.Equals(rel, normalizedTarget, StringComparison.OrdinalIgnoreCase))
                {
                    return rel;
                }
            }

            // 3. Fallback: Search by file name only
            var fileName = Path.GetFileName(clean);
            if (!string.IsNullOrEmpty(fileName))
            {
                var match = allFiles.FirstOrDefault(f => string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    return Path.GetRelativePath(scormPackageDir, match).Replace('\\', '/');
                }
            }

            return null;
        }
    }
}

