using System.Threading.Tasks;

namespace AdityaLMS.Core.Interfaces
{
    public interface IScormService
    {
        /// <summary>
        /// Extracts a SCORM zip archive, parses imsmanifest.xml, and determines the launch HTML file.
        /// </summary>
        /// <param name="zipFilePath">Path to the SCORM package .zip on disk.</param>
        /// <param name="scormId">Unique identifier or slug for this package.</param>
        /// <param name="destinationFolder">Base directory where packages are unpacked.</param>
        /// <returns>Launch relative path (e.g. "scormcontent/index.html") and error string if any.</returns>
        Task<(string LaunchHref, string Error)> ProcessScormPackageAsync(string zipFilePath, string scormId, string destinationFolder);

        /// <summary>
        /// Robustly resolves a requested asset path inside an extracted SCORM package folder.
        /// Handles case-insensitivity, query string stripping, and nested subpath fallbacks.
        /// </summary>
        string ResolveScormAsset(string scormPackageDir, string requestedPath);
    }
}

