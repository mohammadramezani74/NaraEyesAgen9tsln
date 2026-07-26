
using NaraEyesAgent.Common.IpHelper;
using NaraEyesAgent.Core.XFSServices;
using NaraEyesAgent.Common.IpHelper;
using NaraEyesAgent.Core.XFSServices;
using NLog;
using NLog.Config;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            string exePath = System.Reflection.Assembly
             .GetExecutingAssembly().Location;

            Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry
                .CurrentUser.OpenSubKey(
                "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

            key.SetValue("NaraEyesAgent", "\"" + exePath + "\"");
            key.Close();
            CopyMsxfsDll();
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

      
    }
}
