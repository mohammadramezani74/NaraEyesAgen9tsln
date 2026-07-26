

namespace NaraEyesAgent.Core.Models.ScreenShot
{
    public sealed class CommandBasePayload
    {
        public Guid CommandId { get; set; }
        public int? DelaySeconds { get; set; }
        public string Reason { get; set; }
    }
    public sealed class CommandBaseUpload
    {
        public Guid CommandId { get; set; }
        public string FileData { get; set; }
        public string Extension { get; set; }
        public string Name { get; set; }
    }
    public sealed  class SendGroupInstructionModel
    {
     
            public Guid MessageBoxId { get; set; }
            public Guid CampaignId { get; set; }
        public string Ip { get; set; }
        public int Type { get; set; }
            public string url { get; set; }
        
    }
}
