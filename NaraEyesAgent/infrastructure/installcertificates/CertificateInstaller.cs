using System;
using System.Collections.Generic;
using System.IO;

using System.Security.Cryptography.X509Certificates;


namespace NaraEyesAgent.infrastructure.InstallCertificates
{
    public class CertificateInstaller
    {
        public static void InstallCertificatesFromFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                Console.WriteLine("Folder not found.");
                return;
            }

            var certFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
                                     .Where(f => f.EndsWith(".cer", StringComparison.OrdinalIgnoreCase) ||
                                                 f.EndsWith(".crt", StringComparison.OrdinalIgnoreCase)||
                                                    f.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase))
                                     .ToList();

            foreach (var file in certFiles)
            {
                try
                {
                    InstallIfNotExists(file);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing {file}: {ex.Message}");
                }
            }
        }

        private static void InstallIfNotExists(string filePath)
        {
            X509Certificate2 cert;

            if (filePath.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase))
            {
                // اگر پسورد داری اینجا بده
                cert = new X509Certificate2(filePath, "123!@#qaz",
                    X509KeyStorageFlags.MachineKeySet |
                    X509KeyStorageFlags.PersistKeySet |
                    X509KeyStorageFlags.Exportable);
            }
            else
            {
                cert = new X509Certificate2(filePath);
            }

            string thumbprint = cert.Thumbprint;

            if (IsCertificateInstalled(thumbprint))
            {
                Console.WriteLine($"Already installed: {filePath}");
                return;
            }

            InstallCertificate(cert);
            Console.WriteLine($"Installed: {filePath}");
        }

        private static bool IsCertificateInstalled(string thumbprint)
        {
             var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);

            var found = store.Certificates
                .Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false);

            return found.Count > 0;
        }

        private static void InstallCertificate(X509Certificate2 cert)
        {
             var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);
            store.Add(cert);
        }
    }
}
