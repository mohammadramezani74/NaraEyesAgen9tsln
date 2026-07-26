

namespace NaraEyesAgent.Core.Models.Metrics
{
    public sealed class DeviceMetricsDto
    {
        public string DeviceIp { get; set; }
        public DateTime CapturedAtUtc { get; set; }

        // منابع سیستمی
        public double? CpuUsage { get; set; }
        public double? RamUsage { get; set; }
        public double? DiskUsage { get; set; }
        public double? CpuTemp { get; set; }
        public string OsFeatures { get; set; }
        public DateTime AgentTime { get; set; }

        // شبکه
        public int? NetworkLatencyMs { get; set; }
        public bool PingOk { get; set; }

        // وضعیت کلی
        public bool AgentAlive { get; set; }
        public string AgentVersion { get; set; } = "unknown";
        public double? TotalRamGb { get; set; }   // مثال: 2.0، 3.9
        public string CpuModel { get; set; }

    }
}
