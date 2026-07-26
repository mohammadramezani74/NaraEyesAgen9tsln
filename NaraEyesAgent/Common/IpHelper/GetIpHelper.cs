
using System.Net;
using System.Net.Sockets;
using System.Reflection;


namespace NaraEyesAgent.Common.IpHelper
{
    internal class GetIpHelper
    {
        public static string GetLocalIPv4()
        {
            return Dns.GetHostAddresses(Dns.GetHostName())
                .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)?
                .ToString() ?? "127.0.0.1";
        }
        public static string GetAgentVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        }
    }
}
