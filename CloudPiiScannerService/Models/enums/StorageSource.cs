using System.Text.Json.Serialization;

namespace CloudPiiScannerService.Models.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum StorageSource
    {
        LOCAL,
        AWS_S3,
        DROPBOX,
        ONEDRIVE,
        GOOGLE_DRIVE
    }
}
