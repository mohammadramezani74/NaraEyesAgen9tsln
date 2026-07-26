

namespace NaraEyesAgent.Core.Models.Basic
{
    public class PollResponse
    {
        public DateTime ServerTime { get; set; }
        public List<OutBoxDeviceMessage> Commands { get; set; } = new List<OutBoxDeviceMessage>();
    }
}
