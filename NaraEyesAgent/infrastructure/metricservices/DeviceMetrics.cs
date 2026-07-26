

namespace NaraEyesAgent.Infrastructure.MetricServices
{
    using NaraEyesAgent.Core.Models.Metrics;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;                 // برای LINQ در .NET 3.5
    using System.Management;          // برای WMI (Cpu Model)
    using System.Net.NetworkInformation;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;

    public static class DeviceMetrics
    {
        // --- کش مقادیر ثابت سخت‌افزاری (thread-safe با double-checked locking) ---
        private static volatile bool _totalRamCached;
        private static double? _totalRamGb;
        private static readonly object _ramLock = new object();

        private static volatile bool _cpuModelCached;
        private static string _cpuModel;
        private static readonly object _cpuLock = new object();

        // --- بافر ثابت برای پینگ (کاهش فشار GC) ---
        private static readonly byte[] PingBuffer = new byte[16];

        // ===== API بدون پارامترهای پیش‌فرض (C# 3.0) =====

        public static DeviceMetricsDto Capture(Guid deviceId, string agentVersion)
        {
            return Capture(deviceId, agentVersion, null, 1000, false);
        }

        public static DeviceMetricsDto Capture(Guid deviceId, string agentVersion, string pingTarget)
        {
            return Capture(deviceId, agentVersion, pingTarget, 1000, false);
        }

        public static DeviceMetricsDto Capture(Guid deviceId, string agentVersion, string pingTarget, int pingTimeoutMs)
        {
            return Capture(deviceId, agentVersion, pingTarget, pingTimeoutMs, false);
        }

        public static DeviceMetricsDto Capture(Guid deviceId, string agentVersion, string pingTarget, int pingTimeoutMs, bool readCpuTemp)
        {
            // همیشه UTC
            DateTime nowUtc = DateTime.UtcNow;

            double? cpu = TryGetCpuUsagePercent();
            double? ram = TryGetRamUsagePercent();
            double? disk = TryGetDiskUsagePercent();
            double? totalRamGb = GetTotalRamGbCached();
            string cpuModel = GetCpuModelCached();

            double? temp = null;
            if (readCpuTemp) // اختیاری چون WMI/WMIC ممکن است کند یا ناپایدار باشد
                temp = TryGetCpuTemperatureCelsius();

            int? latency = null;
            bool pingOk = false;
            if (!IsNullOrWhiteSpace(pingTarget))
                TryPing(pingTarget, pingTimeoutMs, out latency, out pingOk);

            DeviceMetricsDto dto = new DeviceMetricsDto();
            dto.CapturedAtUtc = nowUtc;  // ✅ UTC
            dto.CpuUsage = NormalizePercent(cpu);
            dto.RamUsage = NormalizePercent(ram);
            dto.DiskUsage = NormalizePercent(disk);
            dto.CpuTemp = NormalizeTemp(temp);
            dto.NetworkLatencyMs = NormalizeLatency(latency);
            dto.PingOk = pingOk;
            dto.AgentAlive = true;
            dto.AgentVersion = NormalizeVersion(agentVersion);
            dto.TotalRamGb = totalRamGb;
            dto.CpuModel = cpuModel;
            return dto;
        }

        // ===== RAM Total (cached) =====
        private static double? GetTotalRamGbCached()
        {
            if (_totalRamCached) return _totalRamGb;
            lock (_ramLock)
            {
                if (!_totalRamCached)
                {
                    _totalRamGb = ComputeTotalRamGb();
                    _totalRamCached = true;
                }
            }
            return _totalRamGb;
        }

        private static double? ComputeTotalRamGb()
        {
            try
            {
                MEMORYSTATUSEX mem = new MEMORYSTATUSEX();
                mem.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                if (!GlobalMemoryStatusEx(ref mem)) return null;
                const double G = 1024.0 * 1024.0 * 1024.0;
                return Math.Round(mem.ullTotalPhys / G, 1);
            }
            catch { return null; }
        }

        // ===== CPU Model (cached) =====
        private static string GetCpuModelCached()
        {
            if (_cpuModelCached) return _cpuModel;
            lock (_cpuLock)
            {
                if (!_cpuModelCached)
                {
                    _cpuModel = ComputeCpuModel();
                    if (IsNullOrWhiteSpace(_cpuModel)) _cpuModel = "Unknown CPU";
                    _cpuModelCached = true;
                }
            }
            return _cpuModel;
        }

        private static string ComputeCpuModel()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor"))
                using (ManagementObjectCollection results = searcher.Get())
                {
                    foreach (ManagementObject mo in results)
                    {
                        object nameObj = mo["Name"];
                        if (nameObj != null)
                        {
                            string name = Convert.ToString(nameObj);
                            if (!string.IsNullOrEmpty(name))
                            {
                                if (name.Length > 64) name = name.Substring(0, 64);
                                return name.Trim();
                            }
                        }
                    }
                }
            }
            catch { }
            return "Unknown CPU";
        }

        // ===== CPU Usage via GetSystemTimes (سبک و دقیق) =====
        private static double? TryGetCpuUsagePercent()
        {
            try
            {
                FILETIME idle1, kernel1, user1;
                if (!GetSystemTimes(out idle1, out kernel1, out user1)) return null;

                Thread.Sleep(250);

                FILETIME idle2, kernel2, user2;
                if (!GetSystemTimes(out idle2, out kernel2, out user2)) return null;

                ulong i1 = ToUInt64(idle1), k1 = ToUInt64(kernel1), u1 = ToUInt64(user1);
                ulong i2 = ToUInt64(idle2), k2 = ToUInt64(kernel2), u2 = ToUInt64(user2);

                ulong idle = i2 - i1;
                ulong kernel = k2 - k1;
                ulong user = u2 - u1;

                ulong total = kernel + user; // kernel شامل idle است
                if (total == 0) return null;

                ulong busy = total - idle;
                double percent = (busy * 100.0) / (double)total;
                return Math.Round(percent, 2);
            }
            catch { return null; }
        }

        // ===== RAM Usage (%) =====
        private static double? TryGetRamUsagePercent()
        {
            try
            {
                MEMORYSTATUSEX mem = new MEMORYSTATUSEX();
                mem.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                if (!GlobalMemoryStatusEx(ref mem)) return null;

                double total = mem.ullTotalPhys;
                double avail = mem.ullAvailPhys;
                if (total <= 0) return null;

                double used = total - avail;
                return Math.Round((used / total) * 100.0, 2);
            }
            catch { return null; }
        }

        // ===== Disk Usage (% Used) =====
        private static double? TryGetDiskUsagePercent()
        {
            try
            {
                // DriveInfo و LINQ در 3.5 موجودند
                var drives = DriveInfo.GetDrives()
                                      .Where(delegate (DriveInfo d) { return d.DriveType == DriveType.Fixed && d.IsReady; });
                long total = 0, free = 0;
                foreach (DriveInfo d in drives) { total += d.TotalSize; free += d.AvailableFreeSpace; }
                if (total <= 0) return null;
                long used = total - free;
                return Math.Round((used * 100.0) / (double)total, 2);
            }
            catch { return null; }
        }

        // ===== CPU Temperature (WMI via WMIC — اختیاری) =====
        private static double? TryGetCpuTemperatureCelsius()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "cmd.exe";
                psi.Arguments = "/c wmic /namespace:\\\\root\\wmi PATH MSAcpi_ThermalZoneTemperature get CurrentTemperature";
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;

                using (Process p = new Process())
                {
                    p.StartInfo = psi;
                    p.Start();

                    string stdout = p.StandardOutput.ReadToEnd();
                    string _ = p.StandardError.ReadToEnd(); // در صورت نیاز لاگ کنید
                    p.WaitForExit();

                    // CurrentTemperature\n 3000\n 2985 ...
                    string[] parts = stdout.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    List<double> temps = new List<double>();

                    for (int i = 0; i < parts.Length; i++)
                    {
                        string s = parts[i].Trim();
                        if (s.Length == 0) continue;
                        if (string.Compare(s, "CurrentTemperature", StringComparison.OrdinalIgnoreCase) == 0) continue;

                        long raw;
                        if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out raw))
                        {
                            double c = (raw / 10.0) - 273.15;
                            if (c > -50 && c < 120) temps.Add(c);
                        }
                    }

                    if (temps.Count == 0) return null;

                    // میانگین
                    double sum = 0;
                    for (int i = 0; i < temps.Count; i++) sum += temps[i];
                    return sum / temps.Count;
                }
            }
            catch
            {
                return null;
            }
        }

        // ===== Ping (ICMP) =====
        private static void TryPing(string host, int timeoutMs, out int? latencyMs, out bool ok)
        {
            latencyMs = null; ok = false;
            try
            {
                using (Ping ping = new Ping())
                {
                    PingOptions options = new PingOptions(64, true);
                    PingReply reply = ping.Send(host, timeoutMs, PingBuffer, options);
                    if (reply.Status == IPStatus.Success)
                    {
                        long rtt = reply.RoundtripTime;
                        if (rtt < 0) rtt = 0;
                        if (rtt > int.MaxValue) rtt = int.MaxValue;
                        latencyMs = (int)rtt;
                        ok = true;
                    }
                }
            }
            catch { /* silent */ }
        }

        // ===== Normalizers =====
        private static double? NormalizePercent(double? value)
        {
            if (!value.HasValue) return null;
            double v = value.Value;
            if (v < 0) return 0;
            if (v > 100) return 100;
            return Math.Round(v, 2);
        }

        private static double? NormalizeTemp(double? temp)
        {
            if (!temp.HasValue) return null;
            double t = temp.Value;
            if (t < -20) return -20;
            if (t > 120) return 120;
            return Math.Round(t, 1);
        }

        private static int? NormalizeLatency(int? latency)
        {
            if (!latency.HasValue) return null;
            int l = latency.Value;
            if (l < 0) return null;
            return l;
        }

        private static string NormalizeVersion(string version)
        {
            if (string.IsNullOrEmpty(version)) return "unknown";
            return (version.Length <= 50) ? version : version.Substring(0, 50);
        }

        private static bool IsNullOrWhiteSpace(string s)
        {
            if (s == null) return true;
            for (int i = 0; i < s.Length; i++)
            {
                if (!char.IsWhiteSpace(s[i])) return false;
            }
            return true;
        }

        // ===== P/Invoke =====
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out FILETIME idleTime, out FILETIME kernelTime, out FILETIME userTime);

        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
        }

        private static ulong ToUInt64(FILETIME ft)
        {
            return (((ulong)ft.dwHighDateTime) << 32) | (ulong)ft.dwLowDateTime;
        }

        [DllImport("kernel32.dll")]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }
    }
}
