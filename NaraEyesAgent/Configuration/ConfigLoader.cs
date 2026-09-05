

using System.Text;

namespace NaraEyesAgent.Configuration
{
    public static class ConfigLoader
    {

        public static string[] ProbePaths()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return new string[]
            {
           Path.Combine(baseDir, "Config.txt"),                    // کنار exe
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),     // Program Files\NaraEyesAgent\Resources
                         "NaraEyesAgentApplication/Config.txt"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), // %ProgramData%\NaraEyesAgent\Config.txt
                         "NaraEyesAgentApplication/Config.txt")
            };
        }
        public static AppConfig Load(out string loadedFromPath)
        {
            loadedFromPath = null;
            var cfg = new AppConfig();
            string[] candidates = ProbePaths();

            foreach (var p in candidates)
            {
                try
                {
                    if (File.Exists(p))
                    {
                        ApplyFile(cfg, p);
                        loadedFromPath = p;
                        break;
                    }
                }
                catch { /* ignore and try next */ }
            }

            return cfg;
        }

        private static void ApplyFile(AppConfig cfg, string path)
        {
            // UTF-8 with/without BOM
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            foreach (string raw in lines)
            {
                if (string.IsNullOrEmpty(raw)) continue;
                string line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("#") || line.StartsWith(";")) continue;

                // پشتیبانی از ; کامنت انتهای خط
                int semi = line.IndexOf(';');
                if (semi >= 0) line = line.Substring(0, semi).Trim();

                int eq = line.IndexOf('=');
                if (eq <= 0) continue;

                string key = line.Substring(0, eq).Trim();
                string val = line.Substring(eq + 1).Trim();

                SetValue(cfg, key, val);
            }
        }

        private static bool ToBool(string v)
        {
            if (string.IsNullOrEmpty(v)) return false;
            v = v.Trim().ToLowerInvariant();
            return (v == "1" || v == "true" || v == "yes" || v == "on");
        }

        private static int ToInt(string v, int def)
        {
            int n; return int.TryParse(v, out n) ? n : def;
        }

        private static void SetValue(AppConfig c, string k, string v)
        {
            // کلیدها را بدون حساسیت به بزرگی/کوچکی در نظر بگیر
            string key = k.Trim();
            switch (key.ToLowerInvariant())
            {
                // --- Server ---
                case "apibase": c.ApiBase = v; break;
                case "pingtarget": c.PingTarget = v; break;
                case "terminalcode": c.TerminalCode = v; break;

                // --- Paths ---
                case "journalpath": c.JournalPath = v; break;
                case "ejournalroot": c.EJournalRoot = v; break;
                case "ejournalfallback": c.EJournalFallback = v; break;
                case "logdir": c.LogDir = v; break;

                // --- Log ---
                case "loglevel": c.LogLevel = v; break;
                case "maxlogsizekb": c.MaxLogSizeKb = ToInt(v, c.MaxLogSizeKb); break;

                // --- Poll ---
                case "pollwaitseconds": c.PollWaitSeconds = ToInt(v, c.PollWaitSeconds); break;
                case "polljitterseconds": c.PollJitterSeconds = ToInt(v, c.PollJitterSeconds); break;

                // --- Metrics ---
                case "metricsintervalsec": c.MetricsIntervalSec = ToInt(v, c.MetricsIntervalSec); break;
                case "metricsjittersec": c.MetricsJitterSec = ToInt(v, c.MetricsJitterSec); break;
                case "metricsminsec": c.MetricsMinSec = ToInt(v, c.MetricsMinSec); break;

                // --- Screenshot ---
                case "screenshotquality": c.ScreenshotQuality = ToInt(v, c.ScreenshotQuality); break;
                case "screenshotmaxwidth": c.ScreenshotMaxWidth = ToInt(v, c.ScreenshotMaxWidth); break;
                case "screenshotmaxheight": c.ScreenshotMaxHeight = ToInt(v, c.ScreenshotMaxHeight); break;

                // --- XFS logicals ---
                case "cdmlogical": c.CdmLogical = v; break;
                case "idclogical": c.IdcLogical = v; break;
                case "ptrlogical": c.PtrLogical = v; break;
                case "siulogical": c.SiuLogical = v; break;
                case "cameralogical": c.CameraLogical = v; break;
                case "pinlogical": c.PinLogical = v; break;

                // --- Agent ---
                case "mode": c.Mode = v; break;
                case "tray": c.Tray = ToBool(v); break;
                case "allowexit": c.AllowExit = ToBool(v); break;

                // --- Proxy ---
                case "proxyenabled": c.ProxyEnabled = ToBool(v); break;
                case "proxyurl": c.ProxyUrl = v; break;
                case "proxyuser": c.ProxyUser = v; break;
                case "proxypass": c.ProxyPass = v; break;

                case "armaghanlogpath": c.ArmaghanLogPath = v; break;
                case "sepantalogpath": c.SepantaLogPath = v; break;
                case "imagearchivepath": c.ImageArchivePath = v; break;
                case "filetransfermaxmb": c.FileTransferMaxMb = ToInt(v, c.FileTransferMaxMb); break;
                case "includeboundaryarchive":
                    c.IncludeBoundaryArchive = !string.Equals(v.Trim(), "false",
                        StringComparison.OrdinalIgnoreCase);
                    break;

                // --- Security ---
                case "mtls": c.mTLS = ToBool(v); break;
                case "enrollmenttoken": c.EnrollmentToken = v; break;

                default: break; // ناشناخته‌ها را نادیده بگیر
            }
        }

    }
}
