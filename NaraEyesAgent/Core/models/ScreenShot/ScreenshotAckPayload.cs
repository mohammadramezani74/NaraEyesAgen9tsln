


namespace NaraEyesAgent.Core.Models.ScreenShot
{
    public sealed class ScreenshotAckPayload
    {
        public Guid CommandId { get; set; }                
        public string ContentType { get; set; } = "image/png";
        public string DataBase64 { get; set; } = string.Empty;
        public int? Width { get; set; }
        public int? Height { get; set; }
    }
}
