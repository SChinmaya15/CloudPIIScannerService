namespace CloudPiiScannerService.Models
{
    public class HeartbeatResponse
    {
        public ScanConfig? ScanConfig { get; set; }
        public string AgentId { get; set; } = default!;
        public int PollingIntervalMinutes { get; set; }
    }
}
