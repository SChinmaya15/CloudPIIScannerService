using CloudPiiScannerService.Models.enums;

namespace CloudPiiScannerService.Models
{
    public class ScanResults
    {
        public string ScanId { get; set; }
        public Guid Id { get; set; }
        public string MachineName { get; set; } = default!;
        public StorageSource Source { get; set; }
        public string FilePath { get; set; } = default!;
        public string Entity { get; set; } = default!;
        public bool IsDetected { get; set; }
        public string Details { get; set; } = default!;
    }
}
