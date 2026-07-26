using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;

namespace NaraEyesAgent.infrastructure.lic
{
    public interface ILicenseService
    {
        bool IsValid();

        LicenseModel GetInfo();
    }
    public class LicenseService : ILicenseService
    {
        private readonly string _licensePath;
        private readonly string _publicKey;

        public LicenseService(
            string licensePath,
            string publicKey)
        {
            _licensePath = licensePath;
            _publicKey = publicKey;
        }

        public bool IsValid()
        {
            var license = GetInfo();

            if (license == null)
                return false;

            if (license.ExpireDate.Date < DateTime.UtcNow.Date)
                return false;

            //if (license.MachineId != MachineHelper.GetMachineId())
            //    return false;

            return VerifySignature(license);
        }

        public LicenseModel GetInfo()
        {
            if (!File.Exists(_licensePath))
                return null;

            var json =
                File.ReadAllText(_licensePath);

            return JsonConvert.DeserializeObject<LicenseModel>(json);
        }

        private bool VerifySignature(
            LicenseModel license)
        {
            var raw =
                $"{license.CustomerName}|{license.MachineId}|{license.ExpireDate:yyyyMMdd}";

            var rsa =
                new RSACryptoServiceProvider();

            rsa.FromXmlString(_publicKey);

            return rsa.VerifyData(
                Encoding.UTF8.GetBytes(raw),
                CryptoConfig.MapNameToOID("SHA256"),
                Convert.FromBase64String(license.Signature));
        }
    }
        public class LicenseModel
    {
        public string CustomerName { get; set; }

        public string MachineId { get; set; }

        public DateTime ExpireDate { get; set; }

        public string Signature { get; set; }
    }
   
}
