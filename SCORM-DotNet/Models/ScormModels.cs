using System.Collections.Generic;

namespace SCORM_Implementation.Models
{
    public record ScormManifestMetadata(
        string Identifier,
        string Title,
        string Schema,
        string SchemaVersion,
        string? LaunchHref,
        bool IsRise360,
        List<string> Resources
    );

    public record ScormProcessResult(
        bool Success,
        string? LaunchHref,
        string? Error,
        ScormManifestMetadata? Metadata
    );

    public class ScormPackageViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Schema { get; set; } = "SCORM";
        public string SchemaVersion { get; set; } = "1.2";
        public string LaunchHref { get; set; } = "index.html";
        public string LaunchUrl { get; set; } = string.Empty;
        public bool IsRise360 { get; set; }
    }

    public class ScormPlayerViewModel
    {
        public string PackageId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string LaunchHref { get; set; } = "index.html";
        public string LaunchUrl { get; set; } = string.Empty;
        public string SchemaVersion { get; set; } = "1.2";
        public bool IsRise360 { get; set; }
        public string InitialStateJson { get; set; } = "{}";
    }

    public class ScormTelemetryDto
    {
        public string PackageId { get; set; } = string.Empty;
        public string Version { get; set; } = "1.2";
        public string Status { get; set; } = "completed"; // "completed", "passed", "failed", "incomplete"
        public string? Score { get; set; }
        public string? Location { get; set; }
        public string? SuspendData { get; set; }
        public Dictionary<string, object>? Cmi { get; set; }
    }
}

