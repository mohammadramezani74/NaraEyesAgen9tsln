using NaraEyesAgent.Core.Models.ScreenShot;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NaraEyesAgent.infrastructure.SaveFile
{
    public static class SaveFiles
    {
        private const string BaseDir = @"C:\AgentFiles";

        private static readonly HttpClient _http = new HttpClient(
            new SocketsHttpHandler
            {
                UseProxy = false,
                SslOptions =
                {
                    EnabledSslProtocols =
                        System.Security.Authentication.SslProtocols.Tls12 |
                        System.Security.Authentication.SslProtocols.Tls13,
                    RemoteCertificateValidationCallback = (_, _, _, _) => true
                }
            })
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        public static string SaveBase64Files(CommandBaseUpload upload)
        {
            try
            {
                if (upload == null ||
                    string.IsNullOrWhiteSpace(upload.Name) ||
                    string.IsNullOrWhiteSpace(upload.FileData))
                {
                    Console.WriteLine("[SaveFiles] Invalid upload payload");
                    return string.Empty;
                }

                EnsureDir(BaseDir);

                // ✅ جلوگیری از Path Traversal
                string safeName = Path.GetFileName(upload.Name);
                string safeExt = Path.GetExtension(upload.Extension ?? "");
                string filePath = Path.Combine(BaseDir, safeName + safeExt);

                // مطمئن شو فایل داخل BaseDir میمونه
                if (!filePath.StartsWith(BaseDir, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("[SaveFiles] Path traversal attempt blocked");
                    return string.Empty;
                }

                byte[] fileBytes = Convert.FromBase64String(upload.FileData);
                File.WriteAllBytes(filePath, fileBytes);

                Console.WriteLine($"[SaveFiles] Saved: {filePath}");
                return filePath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SaveFiles] SaveBase64Files error: {ex.Message}");
                return string.Empty;
            }
        }

        public static async Task<bool> SaveFilesFromUrlAsync(
            string url,
            CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url))
                    return false;

                EnsureDir(BaseDir);

                // ✅ نام فایل از URL - safe
                string fileName = Path.GetFileName(new Uri(url).LocalPath);
                if (string.IsNullOrWhiteSpace(fileName))
                    fileName = $"file_{DateTime.Now:yyyyMMddHHmmss}";

                string localPath = Path.Combine(BaseDir, fileName);

                // ✅ HttpClient به جای WebClient
                var bytes = await _http.GetByteArrayAsync(url, ct);
                await File.WriteAllBytesAsync(localPath, bytes, ct);

                Console.WriteLine($"[SaveFiles] Downloaded: {localPath}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SaveFiles] SaveFilesFromUrl error: {ex.Message}");
                return false;
            }
        }

        private static void EnsureDir(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }
    }
}