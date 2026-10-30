using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SCORM_Implementation.Models;

namespace SCORM_Implementation.Services
{
    public interface IScormService
    {
        Task<ScormProcessResult> ProcessScormPackageAsync(string zipFilePath, string scormId, string destinationFolder);
        Task<ScormProcessResult> ProcessScormPackageStreamAsync(Stream zipStream, string scormId, string destinationFolder);
        string? ResolveScormAsset(string scormPackageDir, string requestedPath);
        ScormManifestMetadata ParseManifest(string manifestPath);
        List<ScormPackageViewModel> GetAvailablePackages(string packagesDirectory);
    }
}

