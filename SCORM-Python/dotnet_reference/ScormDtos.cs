using System.Collections.Generic;

namespace AdityaLMS.Api.DTOs
{
    public class ScormTelemetryDto
    {
        public int LessonId { get; set; }
        public string Status { get; set; } = "completed"; // "completed", "passed", "failed"
        public double? Score { get; set; }
        public string Location { get; set; }
        public string SuspendData { get; set; }
        public Dictionary<string, object> Cmi { get; set; }
    }

    public class ScormManifestDto
    {
        public string Identifier { get; set; }
        public string Title { get; set; }
        public string Schema { get; set; } = "SCORM";
        public string SchemaVersion { get; set; } = "1.2";
        public string LaunchHref { get; set; }
        public bool IsRise360 { get; set; }
        public List<string> Resources { get; set; } = new List<string>();
    }
}

