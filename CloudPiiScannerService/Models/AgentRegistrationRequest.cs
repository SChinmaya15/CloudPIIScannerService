namespace CloudPiiScannerService.Models
{
 public class AgentRegistrationRequest
 {
 public string MachineName { get; set; } = default!;
 public string CurrentUser { get; set; } = default!;
 public string MacAddress { get; set; } = default!;
 public string OperatingSystem { get; set; } = default!;
 public string OsVersion { get; set; } = default!;
 public string AgentVersion { get; set; } = default!;
 }
}
