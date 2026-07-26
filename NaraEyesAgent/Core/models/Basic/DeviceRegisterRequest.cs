namespace NaraEyesAgent.Core.Models.Basic
{
    public class DeviceRegisterRequest
    {
        public int TerminalCode { get; set; }
        public string Ip { get; set; }
        public string Model { get; set; }
        public string AgentVersion { get; set; }
    }
}
