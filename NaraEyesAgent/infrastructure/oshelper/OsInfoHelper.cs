

namespace NaraEyesAgent.infrastructure.OsHelper
{
    using System;
    using System.Management;

    public sealed class OsInfo
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string Architecture { get; set; }
    }

    public static class OsInfoHelper
    {
        public static OsInfo GetOsInfo()
        {
            var result = new OsInfo
            {
                Name = "Windows",
                Version = string.Empty,
                Architecture = IntPtr.Size == 4 ? "x86" : "x64" // روی XP هم جواب می‌دهد
            };

            try
            {
                using (var searcher = new ManagementObjectSearcher(
                           "SELECT Caption, Version, CSDVersion, OSArchitecture FROM Win32_OperatingSystem"))
                using (var objects = searcher.Get())
                {
                    foreach (ManagementObject os in objects)
                    {
                        var caption = (os["Caption"] ?? "").ToString().Trim();      // مثل: Microsoft Windows XP Professional
                        var version = (os["Version"] ?? "").ToString().Trim();      // مثل: 5.1.2600
                        var csd = (os["CSDVersion"] ?? "").ToString().Trim();   // مثل: Service Pack 3

                        result.Name = string.IsNullOrEmpty(caption) ? "Windows" : caption;
                        if (!string.IsNullOrEmpty(csd))
                            result.Name += " " + csd;

                        result.Version = version;

                        // روی XP ممکنه OSArchitecture وجود نداشته باشه، با try/catch خنثی می‌گیریم
                        try
                        {
                            var archProp = os.Properties["OSArchitecture"];
                            if (archProp != null && archProp.Value != null)
                                result.Architecture = archProp.Value.ToString().Trim();
                        }
                        catch
                        {
                            // همان مقدار پیش‌فرض IntPtr.Size باقی می‌ماند
                        }

                        break; // فقط اولین رکورد کافی است
                    }
                }
            }
            catch
            {
                // fallback امن، روی همه ویندوزها جواب می‌دهد حتی XP
                var os = Environment.OSVersion;
                result.Name = os.VersionString;
                result.Version = os.Version.ToString();
            }

            return result;
        }
    }

}
