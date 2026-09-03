
using NaraEyesAgent.Common.IpHelper;
using NaraEyesAgent.Common.IpHelper;
using NaraEyesAgent.Core.XFSPatterns.package;
using NaraEyesAgent.Core.XFSServices;
using NaraEyesAgent.Core.XFSServices;
using NLog;
using NLog.Config;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NaraEyesAgent
{
    public class Program
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private static AgentConsole _agent;
        private static XfsEventWindow _evtWindow;
        static async Task Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try
                {
                    var ex = e.ExceptionObject as Exception;
                    File.AppendAllText(@"C:\naraeyes\crash.log",
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} FATAL{Environment.NewLine}{ex}{Environment.NewLine}{new string('-', 60)}{Environment.NewLine}");
                }
                catch { }
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                try
                {
                    File.AppendAllText(@"C:\naraeyes\crash.log",
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} UNOBSERVED{Environment.NewLine}{e.Exception}{Environment.NewLine}");
                    e.SetObserved();
                }
                catch { }
            };
            void Assert(string name, int actual, int expected)
            {
                string mark = actual == expected ? "OK " : "BAD";
                Console.WriteLine($"[SIZEOF] {mark} {name,-20} = {actual,4} (expected {expected})");
            }

            Assert("WFSRESULT", Marshal.SizeOf(typeof(WFSRESULT)), 34);
            Assert("WFSVERSION", Marshal.SizeOf(typeof(WFSVERSION)), 520);
            Assert("WFSCDMSTATUS", Marshal.SizeOf(typeof(CDM.WFSCDMSTATUS)), 16);
            Assert("WFSCDMCASHUNIT", Marshal.SizeOf(typeof(CDM.WFSCDMCASHUNIT)), 52);
            Assert("WFSCDMPHCU", Marshal.SizeOf(typeof(CDM.WFSCDMPHCU)), 31);
            Assert("WFSCAMSTATUS", Marshal.SizeOf(typeof(CAM.WFSCAMSTATUS)), 54);
            Assert("WFSSIUSTATUS", Marshal.SizeOf(typeof(WFSSIUSTATUS)), 198);
            Assert("WFSPTRSTATUS", Marshal.SizeOf(typeof(PTR.WFSPTRSTATUS)), 52);
            Assert("WFSIDCSTATUS", Marshal.SizeOf(typeof(IDC.WFSIDCSTATUS)), 16);
            Assert("WFSPINSTATUS", Marshal.SizeOf(typeof(PIN.WFSPINSTATUS)), 8);
 
            PreloadXfsManager();
            string exePath = Environment.ProcessPath ?? "";
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                           @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", writable: true))
                {
                    if (key != null)
                    {
                        // پاکسازی ورودی‌های خراب نسخه‌های قبلی
                        key.DeleteValue("NaraEyesAgent", throwOnMissingValue: false);
                        key.DeleteValue("NaraEyesAgent.exe", throwOnMissingValue: false);

                        if (!string.IsNullOrEmpty(exePath) &&
                            exePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            key.SetValue("NaraEyesAgent", "\"" + exePath + "\"");
                            Console.WriteLine("[BOOT] Autostart -> " + exePath);
                        }
                        else
                        {
                            Console.WriteLine("[WARN] مسیر اجرایی معتبر نیست — ثبت خودکار انجام نشد.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("[WARN] Run key not accessible — skipping autostart registration.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[WARN] Autostart registration failed: " + ex.Message);
            }
            Console.WriteLine("[BOOT] Agent console starting...");
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var nlogPath = Path.Combine(baseDir, "NLog.config");
            try
            {
                LogManager.Configuration = new XmlLoggingConfiguration(nlogPath);
            }
            catch (Exception cfgEx)
            {

                Console.WriteLine("NLog config load failed: " + cfgEx.Message);
            }
            MappedDiagnosticsContext.Set("DeviceIp", GetIpHelper.GetLocalIPv4());
            Log.Info("agent starting");
            _agent = new AgentConsole(Log);
       await     _agent.Start();




            Console.WriteLine("[RUN] Press Ctrl+C to stop.");
            Console.CancelKeyPress += async (s, e) =>
            {
                e.Cancel = true;
              await  _agent.Stop();
            };

            // در انتظار توقف
          await  _agent.WaitForStop();

            OpenModuleService.CloseAllModules();
            Console.WriteLine("[EXIT] Agent console stopped.");
        }
        private static void CopyMsxfsDll()
        {
            try
            {
                string source = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        "System32",
        "msxfs.dll");
                Console.WriteLine(
           Environment.GetFolderPath(
               Environment.SpecialFolder.System));
                Console.WriteLine(File.Exists(source));

                string destination =
                    Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "msxfs.dll");

                if (!File.Exists(source))
                {
                    Console.WriteLine(
                        "[WARN] msxfs.dll not found in System32");

                    return;
                }

                if (!File.Exists(destination))
                {
                    File.Copy(
                        source,
                        destination);

                    Console.WriteLine(
                        "[OK] msxfs.dll copied.");
                }
                else
                {
                    Console.WriteLine(
                        "[INFO] msxfs.dll already exists.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "[ERR] Copy msxfs.dll failed: " +
                    ex.Message);
            }
        }
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibraryW(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetDllDirectoryW(string lpPathName);

        private static bool PreloadXfsManager()
        {
            // مسیرهای محتمل، به ترتیب اولویت
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var candidates = new[]
            {
            Path.Combine(win, "SysWOW64", "msxfs.dll"),   // XFS سی‌ودو بیتی روی ویندوز ۶۴ بیتی
            Path.Combine(win, "System32", "msxfs.dll"),
            Path.Combine(win, "Sysnative", "msxfs.dll"),  // دور زدن redirector
        };

            foreach (var p in candidates)
            {
                Console.WriteLine($"[XFS] probe {p} exists={File.Exists(p)}");
                if (!File.Exists(p)) continue;

                // مسیر همسایه‌ها را به search path اضافه کن
                SetDllDirectoryW(Path.GetDirectoryName(p));

                IntPtr h = LoadLibraryW(p);
                if (h != IntPtr.Zero)
                {
                    Console.WriteLine($"[XFS] loaded {p}");
                    return true;
                }

                int err = Marshal.GetLastWin32Error();
                Console.WriteLine($"[XFS] LoadLibrary failed err={err} " +
                    (err == 126 ? "(ERROR_MOD_NOT_FOUND — یک dependency گم است، نه خود msxfs)" :
                     err == 193 ? "(ERROR_BAD_EXE_FORMAT — ناهماهنگی ۳۲/۶۴ بیت!)" : ""));
            }

            Console.WriteLine("[XFS] msxfs.dll در هیچ مسیری لود نشد.");
            return false;
        }

    }
}
