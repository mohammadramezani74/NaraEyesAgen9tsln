using Ionic.Zip;
using Ionic.Zlib;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ZipFile = Ionic.Zip.ZipFile;


namespace NaraEyesAgent.Infrastructure.TakeJournal
{
    public sealed class GetJournals
    {
        private readonly string[] _roots;

        private static readonly Regex EjName = new Regex(
            @"^ej_(\d+)_([0-9]{8})(?:\.[Tt][Xx][Tt])?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public GetJournals(params string[] roots)
        {
            //if (roots != null && roots.Length > 0)
            //    _roots = roots;
            //else
                _roots = new[] { @"D:\ejournal" , @"C:\Program Files\Armaghan\Log" };
        }
        private static readonly Regex ArmaghanName = new Regex(
    @"^all logs-([0-9]{8})([0-9]{6})$",
    RegexOptions.Compiled | RegexOptions.CultureInvariant);
        /// <summary>
        /// فایل‌های ژورنال بین startYmd و endYmd (YYYYMMDD، هر دو شامل) را پیدا می‌کند؛
        /// اگر بیش از یک فایل باشد، Zip می‌کند.
        /// خروجی: bytes, contentType, fileName.
        /// </summary>
        public byte[] Collect(string startYmd, string endYmd, out string contentType, out string fileName)
        {
            // نرمال‌سازی ترتیب تاریخ‌ها بدون tuple-assignment
            if (StringCompareOrdinal(endYmd, startYmd) < 0)
            {
                string tmp = startYmd;
                startYmd = endYmd;
                endYmd = tmp;
            }

            // اعتبارسنجی مختصر فرمت YYYYMMDD
            if (!IsYmd(startYmd) || !IsYmd(endYmd))
            {
                contentType = "text/plain";
                fileName = "bad-request.txt";
                return new byte[0];
            }

            IEnumerable<FileInfo> files = FindJournalFiles(_roots, startYmd, endYmd);
            return PackageFiles(files, out contentType, out fileName, startYmd, endYmd);
        }

        private static IEnumerable<FileInfo> FindJournalFiles(string[] roots, string startYmd, string endYmd)
        {
   
            for (int i = 0; i < roots.Length; i++)
            {
                string root = roots[i];
                if (string.IsNullOrEmpty(root)) continue;
                if (!Directory.Exists(root)) continue;

                FileInfo[] files;
                try
                {
                    files = new DirectoryInfo(root).GetFiles("*", SearchOption.TopDirectoryOnly);
                }
                catch
                {
                    continue;
                }

                for (int j = 0; j < files.Length; j++)
                {
                    FileInfo f = files[j];

                    string nameNoExt = Path.GetFileNameWithoutExtension(f.Name);
                    Match m = EjName.Match(nameNoExt);
                    string ymd = null;
                    if (m.Success)
                    {
                        ymd = m.Groups[2].Value; // YYYYMMDD
                    }
                    else
                    {
                        // 2) حالت all logs-YYYYMMDDhhmmss
                        Match m2 = ArmaghanName.Match(nameNoExt);
                        if (m2.Success)
                        {
                            ymd = m2.Groups[1].Value; // YYYYMMDD
                        }
                    }

                    if (ymd == null) continue;

                    if (StringCompareOrdinal(ymd, startYmd) < 0) continue;
                    if (StringCompareOrdinal(ymd, endYmd) > 0) continue;

                    yield return f;
                }
            }
        }
        public byte[] CollectYesterdayAsZipNullable(out string contentType, out string fileName, DateTime? now = null)
        {
            // تاریخ مبنا: امروز ساعت 00:00؛ هدف: روز قبل
            DateTime baseTime = (now ?? DateTime.Now).Date;
            DateTime target = baseTime.AddDays(-1);
            string ymd = target.ToString("yyyyMMdd");

     
            IEnumerable<FileInfo> filesEnum = FindJournalFiles(_roots, ymd, ymd);
            List<FileInfo> files = new List<FileInfo>();
            foreach (var f in filesEnum) files.Add(f);


            if (files.Count == 0)
            {
                contentType = null;
                fileName = null;
                return null;
            }

            // خروجی: حتماً ZIP (حتی اگر فقط یک فایل باشد)
            contentType = "application/zip";
            fileName = $"journal_{ymd}.zip";

            using (var ms = new MemoryStream())
            using (var zip = new Ionic.Zip.ZipFile())
            {
                // پشتیبانی از نام‌های UTF-8
                zip.AlternateEncoding = Encoding.UTF8;
                zip.AlternateEncodingUsage = ZipOption.AsNecessary;

                // سرعت مناسب؛ در صورت نیاز می‌توان BestCompression گذاشت
                zip.CompressionLevel = Ionic.Zlib.CompressionLevel.BestSpeed;

                // افزودن فایل‌ها در ریشه ZIP
                for (int i = 0; i < files.Count; i++)
                {
                    try
                    {
                        zip.AddFile(files[i].FullName, "");
                    }
                    catch
                    {
                        // اگر فایلی مشکل داشت، ردش کن و ادامه بده
                    }
                }

                // اگر عملاً هیچ ورودی سالمی اضافه نشد، نال برگردان
                if (zip.Entries.Count == 0)
                {
                    contentType = null;
                    fileName = null;
                    return null;
                }

                try
                {
                    zip.Save(ms);
                    return ms.ToArray();
                }
                catch
                {
                    // در صورت خطا در ساخت ZIP، همگی را نال برگردان
                    contentType = null;
                    fileName = null;
                    return null;
                }
            }
        }

        private static byte[] PackageFiles(IEnumerable<FileInfo> files, out string contentType, out string fileName, string startYmd, string endYmd)
        {
            // دستی به لیست تبدیل می‌کنیم تا فقط یک بار پیمایش شود
            List<FileInfo> list = new List<FileInfo>();
            foreach (FileInfo fi in files) list.Add(fi);

            if (list.Count == 0)
            {
                contentType = "text/plain";
                fileName = "no-journal.txt";
                return new byte[0];
            }

            if (list.Count == 1)
            {
                FileInfo fi = list[0];
                contentType = "text/plain"; // درصورت نیاز می‌تونی بر اساس محتوا تنظیم کنی
                fileName = fi.Name;

                try
                {
                    return File.ReadAllBytes(fi.FullName);
                }
                catch
                {
                    contentType = "text/plain";
                    fileName = "read-error.txt";
                    return new byte[0];
                }
            }

            // چند فایل → Zip در حافظه
            contentType = "application/zip";
            fileName = string.Format("journal_{0}-{1}.zip", startYmd, endYmd);

            using (MemoryStream ms = new MemoryStream())
            using (ZipFile zip = new ZipFile())
            {
                // برای سازگاری با نام‌فایل‌های UTF-8
                zip.AlternateEncoding = Encoding.UTF8;
                zip.AlternateEncodingUsage = ZipOption.AsNecessary;

                // سرعت بهتر؛ درصورت نیاز BestCompression بگذار
                zip.CompressionLevel = Ionic.Zlib.CompressionLevel.BestSpeed;

                // Zip64 را فعال نکن تا سازگاری با قدیمی‌ها بهتر شود (پیش‌فرض خاموش است)
                // zip.UseZip64WhenSaving = Zip64Option.Never;

                for (int i = 0; i < list.Count; i++)
                {
                    // فایل‌ها را بدون زیرپوشه در ریشهٔ Zip قرار بده
                    zip.AddFile(list[i].FullName, "");
                }

                try
                {
                    zip.Save(ms);
                    return ms.ToArray();
                }
                catch
                {
                    // اگر خطای فشرده‌سازی رخ دهد، خروجی خالی بده
                    contentType = "text/plain";
                    fileName = "zip-error.txt";
                    return new byte[0];
                }
            }
        }

        // ---- Helpers ----

        private static int StringCompareOrdinal(string a, string b)
        {
            return string.Compare(a, b, StringComparison.Ordinal);
        }

        private static bool IsYmd(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length != 8) return false;
            // همه کاراکترها رقم باشند
            for (int i = 0; i < 8; i++)
            {
                char c = s[i];
                if (c < '0' || c > '9') return false;
            }
            return true;
        }
    }
}
