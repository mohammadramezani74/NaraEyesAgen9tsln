using System.Net.Sockets;


namespace NaraEyesAgent.Common.IpHelper
{
    public class PortChecker
    {
        public static bool CanConnect(string ip, int port, int timeoutMs = 3000)
        {
            using (var client = new TcpClient())
            {
                try
                {
                    var result = client.BeginConnect(ip, port, null, null);


                    bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(timeoutMs));

                    if (!success)
                    {
                        // تایم‌اوت شد
                        return false;
                    }


                    client.EndConnect(result);
                    return true; // وصل شد
                }
                catch
                {
                    return false; // خطا یعنی نشد وصل بشه
                }
            }
        }
    }
}
