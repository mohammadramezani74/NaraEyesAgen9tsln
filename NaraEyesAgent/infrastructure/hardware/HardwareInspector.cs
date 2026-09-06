using NaraEyesAgent.Core.Models.Hardware;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management;

namespace NaraEyesAgent.Infrastructure.Hardware
{
    /// <summary>
    /// خواندن مشخصات سخت‌افزار از WMI.
    ///
    /// چهار قطعه‌ای که بانک خواسته: رم، پردازنده، هارد، مادربرد.
    ///
    /// نکته‌ی مهم درباره‌ی حافظه: **Win32_PhysicalMemory** استفاده می‌شود،
    /// نه TotalPhysicalMemory یا TotalVisibleMemorySize.
    ///
    /// دلیلش را در عکس‌های دستگاه‌ها می‌شود دید: دو ATM با رم یکسان، یکی
    /// «4.00 GB (2.97 GB usable)» و دیگری «4.00 GB (3.22 GB usable)»
    /// نشان می‌دهد. ویندوز ۳۲ بیتی فقط بخشی از رم را آدرس‌دهی می‌کند و
    /// مقدار usable به کارت گرافیک و بایوس وابسته است. اگر آن عدد را
    /// مبنا بگیریم، یک آپدیت بایوس می‌تواند بدون هیچ تعویضی آلارم
    /// «کاهش حافظه» بزند.
    ///
    /// Win32_PhysicalMemory ظرفیت **نصب‌شده** را از SMBIOS می‌خواند —
    /// همان چیزی که DxDiag هم نشان می‌دهد (4096MB RAM).
    /// </summary>
    public static class HardwareInspector
    {
        private const int WmiTimeoutSeconds = 20;

        public static HardwareProfileDto Collect()
        {
            var p = new HardwareProfileDto
            {
                CollectedAt = DateTime.Now,
                ComputerName = SafeEnv(() => Environment.MachineName),
                OsVersion = SafeEnv(() => Environment.OSVersion.VersionString),
            };

            CollectRam(p);
            CollectCpu(p);
            CollectDisk(p);
            CollectBoard(p);

            // ناقص یعنی نفرست. بهتر است یک چرخه از دست برود تا اینکه یک
            // آلارم کاذب روی ۳۰۰ دستگاه بخورد.
            p.IsComplete =
                p.RamTotalMb > 0
                && p.CpuCores > 0
                && !string.IsNullOrWhiteSpace(p.CpuName)
                && p.DiskSizeBytes > 0
                && !string.IsNullOrWhiteSpace(p.BoardProduct);

            return p;
        }

        // =============================================================

        private static void CollectRam(HardwareProfileDto p)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Capacity, Manufacturer, PartNumber, SerialNumber, " +
                    "DeviceLocator, Speed FROM Win32_PhysicalMemory");

                long totalBytes = 0;

                foreach (ManagementObject mo in searcher.Get())
                {
                    using (mo)
                    {
                        long cap = ToLong(mo["Capacity"]);
                        totalBytes += cap;

                        p.RamModules.Add(new RamModuleDto
                        {
                            CapacityMb = (int)(cap / 1024 / 1024),
                            Manufacturer = CleanSerial(mo["Manufacturer"]),
                            PartNumber = CleanSerial(mo["PartNumber"]),
                            SerialNumber = CleanSerial(mo["SerialNumber"]),
                            DeviceLocator = Clean(mo["DeviceLocator"]),
                            SpeedMhz = (int)ToLong(mo["Speed"]),
                        });
                    }
                }

                p.RamTotalMb = (int)(totalBytes / 1024 / 1024);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[HW] خواندن حافظه ناموفق: " + ex.Message);
            }
        }

        private static void CollectCpu(HardwareProfileDto p)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Name, NumberOfCores, NumberOfLogicalProcessors, " +
                    "MaxClockSpeed, ProcessorId FROM Win32_Processor");

                foreach (ManagementObject mo in searcher.Get())
                {
                    using (mo)
                    {
                        p.CpuName = Clean(mo["Name"]);
                        p.CpuCores = (int)ToLong(mo["NumberOfCores"]);
                        p.CpuLogicalProcessors = (int)ToLong(mo["NumberOfLogicalProcessors"]);
                        p.CpuMaxClockMhz = (int)ToLong(mo["MaxClockSpeed"]);
                        p.CpuId = Clean(mo["ProcessorId"]);
                        break;   // ATMها تک‌سوکت‌اند
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[HW] خواندن پردازنده ناموفق: " + ex.Message);
            }
        }

        private static void CollectDisk(HardwareProfileDto p)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Model, Size, SerialNumber, InterfaceType, MediaType, Index " +
                    "FROM Win32_DiskDrive");

                ManagementObject? best = null;
                long bestIndex = long.MaxValue;

                foreach (ManagementObject mo in searcher.Get())
                {
                    string media = Clean(mo["MediaType"]) ?? "";

                    // USB و کارت‌خوان‌ها را کنار بگذار — کارشناس که فلش
                    // وصل کند نباید «تعویض هارد» ثبت شود.
                    if (media.IndexOf("Fixed", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        mo.Dispose();
                        continue;
                    }

                    long idx = ToLong(mo["Index"]);
                    if (idx < bestIndex)
                    {
                        best?.Dispose();
                        best = mo;
                        bestIndex = idx;
                    }
                    else
                    {
                        mo.Dispose();
                    }
                }

                if (best is not null)
                {
                    using (best)
                    {
                        p.DiskModel = Clean(best["Model"]);
                        p.DiskSizeBytes = ToLong(best["Size"]);
                        p.DiskSerial = CleanSerial(best["SerialNumber"]);
                        p.DiskInterface = Clean(best["InterfaceType"]);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[HW] خواندن دیسک ناموفق: " + ex.Message);
            }
        }

        private static void CollectBoard(HardwareProfileDto p)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT Manufacturer, Product, SerialNumber FROM Win32_BaseBoard"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        using (mo)
                        {
                            p.BoardManufacturer = Clean(mo["Manufacturer"]);
                            p.BoardProduct = Clean(mo["Product"]);
                            p.BoardSerial = CleanSerial(mo["SerialNumber"]);
                            break;
                        }
                    }
                }

                using (var bios = new ManagementObjectSearcher(
                    "SELECT SMBIOSBIOSVersion FROM Win32_BIOS"))
                {
                    foreach (ManagementObject mo in bios.Get())
                    {
                        using (mo)
                        {
                            p.BiosVersion = Clean(mo["SMBIOSBIOSVersion"]);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[HW] خواندن مادربرد ناموفق: " + ex.Message);
            }
        }

        // =============================================================

        private static long ToLong(object? o)
        {
            if (o is null) return 0;

            try
            {
                return Convert.ToInt64(o, CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0;
            }
        }

        private static string? Clean(object? o)
        {
            var s = o?.ToString()?.Trim();
            return string.IsNullOrWhiteSpace(s) ? null : s;
        }

        /// <summary>
        /// سریال‌های زباله را به null تبدیل می‌کند.
        ///
        /// بردهای صنعتی اغلب رشته‌هایی مثل "00000000"، "Default string" یا
        /// "To Be Filled By O.E.M." برمی‌گردانند. اگر این‌ها را سریال
        /// واقعی فرض کنیم، دو دستگاه متفاوت «یکسان» دیده می‌شوند و
        /// تعویض قطعه تشخیص داده نمی‌شود.
        /// </summary>
        private static string? CleanSerial(object? o)
        {
            var s = Clean(o);
            if (s is null) return null;

            string norm = s.Replace(" ", "").Replace("-", "").ToUpperInvariant();

            if (norm.Length == 0) return null;
            if (norm.All(c => c == '0')) return null;
            if (norm.All(c => c == 'F')) return null;

            if (norm.Contains("DEFAULTSTRING")) return null;
            if (norm.Contains("TOBEFILLED")) return null;
            if (norm.Contains("NOTSPECIFIED")) return null;
            if (norm.Contains("SYSTEMSERIAL")) return null;
            if (norm == "NONE" || norm == "N/A" || norm == "NA") return null;

            // بایوس AMI روی بردهای iEi جدول SPD را نمی‌خواند و به‌جای
            // داده‌ی واقعی، نام فیلد را با شماره‌ی اسلات می‌نویسد:
            // "Manufacturer3"، "SerNum3"، "PartNum3"، "AssetTagNum3".
            //
            // این‌ها روی هر ۳۰۰ دستگاه یکسان‌اند و با تعویض رم هم عوض
            // نمی‌شوند، چون به اسلات وابسته‌اند نه به خود ماژول.
            // پذیرفتنشان یعنی امضایی بسازیم که هرگز تغییر نمی‌کند و
            // به‌ظاهر کار می‌کند — بدترین حالت ممکن.
            if (norm.StartsWith("SERNUM")) return null;
            if (norm.StartsWith("PARTNUM")) return null;
            if (norm.StartsWith("MANUFACTURER")) return null;
            if (norm.StartsWith("ASSETTAG")) return null;
            if (norm.StartsWith("MODULE")) return null;

            return s;
        }

        private static string? SafeEnv(Func<string> f)
        {
            try { return f(); } catch { return null; }
        }
    }
}