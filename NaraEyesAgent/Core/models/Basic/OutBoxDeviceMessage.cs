using System;

namespace NaraEyesAgent.Core.Models.Basic
{
    public class OutBoxDeviceMessage
    {
        public Guid Id { get; set; }
        public string DeviceIp { get; set; }
        public string Payload { get; set; }
        public CommandsType CommandType { get; set; }
        public string StartDate { get; set; }
        public string EndDate { get; set; }
    }
}
