

namespace NaraEyesAgent.Core.Models.ScreenShot
{
    public sealed class CommandAckPayload
    {
        public Guid CommandId { get; set; }
        public bool Accepted { get; set; }
        public string Message { get; set; }
    }
}
