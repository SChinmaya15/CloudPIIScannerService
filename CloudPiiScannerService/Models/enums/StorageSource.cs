using System.Text.Json.Serialization;

namespace CloudPiiScannerService.Models.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum StorageSource
    {
        LOCAL=0,
        AWS_S3=1,
        DROPBOX=2,
        ONEDRIVE=3,
        GOOGLE_DRIVE=4
    }
}
