using Ionic.Zip;
using NaraEyesAgent.Configuration;
using NaraEyesAgent.Core.Models.Basic;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CompressionLevel = Ionic.Zlib.CompressionLevel;
using ZipFile = Ionic.Zip.ZipFile;

namespace NaraEyesAgent.Infrastructure.TakeJournal
{
    /// <summary>خروجی یک درخواست جمع‌آوری فایل</summary>
    public sealed class FileCollectResult
    {
        public byte[] Data { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = "text/plain";
        public string FileName { get; set; } = "empty.txt";

        /// <summary>پیام برای نمایش به کاربر وقتی داده‌ای برنمی‌گردد</summary>
        public string? Message { get; set; }

        public bool HasData => Data != null && Data.Length > 0;
    }

    /// <summary>
    /// موتور مشترک جمع‌آوری فایل برای هر سه منبع (ژورنال ارمغان، لاگ سپنتا،
    /// تصاویر ارمغان).
    ///
    /// جایگزین GetJournals نمی‌شود — GetJournals برای ارسال خودکار روزانه
    /// دست‌نخورده می‌ماند. این کلاس فقط مسیر «درخواست دستی با بازه‌ی
    /// تاریخ» را پوشش می‌دهد.
    ///
    /// سه نکته‌ی طراحی که ارزش دانستن دارند:
    ///
    /// ۱) تاریخ داخل نام فایل زیپ، **زمان ساخته شدن آرشیو** است نه تاریخ
    ///    محتوای آن. زیپی که ساعت ۲۲:۰۲ روز چهارم ساخته شده، لاگ‌های
    ///    ۲۲:۰۲ تا نیمه‌شب همان روز را ندارد؛ آن‌ها در زیپ بعدی هستند.
    ///    برای همین «اولین زیپ بعد از پایان بازه» هم برداشته می‌شود.
    ///    اگر این رفتار را نمی‌خواهی، IncludeBoundaryArchive را false کن.
    ///
    /// ۲) تقویم منابع یکی نیست. زیپ‌ها میلادی‌اند و پوشه‌های تصویر شمسی.
    ///    بازه‌ی درخواستی **یک بار** به شمسی تبدیل می‌شود و بعد مقایسه
    ///    رشته‌ای انجام می‌گیرد؛ چون فرمت ثابت و صفرپرشده است، مقایسه‌ی
    ///    رشته‌ای دقیقاً معادل مقایسه‌ی تاریخ است.
    ///
    /// ۳) ورودی‌هایی که خودشان فشرده‌اند (zip/jpg/png) دوباره فشرده
    ///    نمی‌شوند. CPU خودپرداز همان CPUیی است که سپنتا ارمغان رویش
    ///    کار می‌کند.
    /// </summary>
    public sealed class FileCollector
    {
        // ---------- مسیرها ----------
        private readonly string _armaghanLogPath;
        private readonly string _sepantaLogPath;
        private readonly string _imageArchivePath;
        private readonly string _legacyJournalPath;

        // ---------- محدودیت‌ها ----------
        private readonly long _maxBytes;
        private readonly bool _includeBoundary;
   

        // ---------- الگوهای نام ----------

        // ^all logs-20260804220225.zip   ← آن ^ کاراکتر واقعی نام فایل است
        private static readonly Regex ArmaghanArchive = new Regex(
            @"^\^?all logs-([0-9]{8})([0-9]{6})$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        // ^backed up-20260826031009.zip
        private static readonly Regex SepantaArchive = new Regex(
            @"^\^?backed up-([0-9]{8})([0-9]{6})$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        // ^14020502  (پوشه یا فایل)
        private static readonly Regex ImageFolder = new Regex(
            @"^\^?([0-9]{8})$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // ej_1234_20260804.txt  (رفتار قدیمی)
        private static readonly Regex LegacyEjName = new Regex(
            @"^ej_(\d+)_([0-9]{8})(?:\.[Tt][Xx][Tt])?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>فایل زنده‌ی امروزِ ارمغان — همیشه قفل است چون در حال نوشتن است</summary>
        private const string ArmaghanLiveFile = "journal backup.log";

        public FileCollector(AppConfig cfg)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _armaghanLogPath = FirstNonEmpty(cfg.ArmaghanLogPath, @"C:\Program Files\Armaghan\log");
            _sepantaLogPath = FirstNonEmpty(cfg.SepantaLogPath, @"C:\Program Files\Sepanta Agent\log");
            _imageArchivePath = FirstNonEmpty(cfg.ImageArchivePath, @"C:\Program Files\Armaghan\ImageArchive");
            _legacyJournalPath = FirstNonEmpty(cfg.JournalPath, @"D:\ejournal");

            int mb = cfg.FileTransferMaxMb > 0 ? cfg.FileTransferMaxMb : 40;
            _maxBytes = (long)mb * 1024L * 1024L;

            _includeBoundary = cfg.IncludeBoundaryArchive;
        }

        // =================================================================
        //  ورودی اصلی
        // =================================================================

        /// <summary>
        /// startYmd و endYmd همیشه **میلادی** yyyyMMdd هستند — سرور آن‌ها را
        /// همین‌طور می‌فرستد. تبدیل به شمسی فقط داخل مسیر تصاویر انجام می‌شود.
        /// </summary>
        public FileCollectResult Collect(FileSourceType source, string startYmd, string endYmd)
        {
            if (CompareOrdinal(endYmd, startYmd) < 0)
            {
                string tmp = startYmd;
                startYmd = endYmd;
                endYmd = tmp;
            }

            if (!IsYmd(startYmd) || !IsYmd(endYmd))
                return Fail("bad-request.txt", "فرمت تاریخ نامعتبر است (انتظار yyyyMMdd).");

            try
            {
                if (source == FileSourceType.ArmaghanImages)
                    return CollectImages(startYmd, endYmd);

                if (source == FileSourceType.SepantaLog)
                    return CollectArchives(
                        _sepantaLogPath, SepantaArchive, "sepanta",
                        startYmd, endYmd, includeLiveFile: false);

                if (source == FileSourceType.ArmaghanJournal)
                    return CollectArchives(
                        _armaghanLogPath, ArmaghanArchive, "armaghan",
                        startYmd, endYmd, includeLiveFile: true);

                return CollectLegacy(startYmd, endYmd);
            }
            catch (Exception ex)
            {
                return Fail("collect-error.txt", "خطا هنگام جمع‌آوری فایل: " + ex.Message);
            }
        }

        // =================================================================
        //  آرشیوهای زیپ (ارمغان و سپنتا)
        // =================================================================

        private FileCollectResult CollectArchives(
            string root, Regex pattern, string tag,
            string startYmd, string endYmd, bool includeLiveFile)
        {
            if (!Directory.Exists(root))
                return Fail("no-path.txt", "مسیر روی دستگاه وجود ندارد: " + root);

            FileInfo[] all;
            try
            {
                all = new DirectoryInfo(root).GetFiles("*", SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex)
            {
                return Fail("no-access.txt", "دسترسی به مسیر ممکن نشد: " + ex.Message);
            }

            // نام → مهر زمانی ۱۴ رقمی
            var stamped = new List<KeyValuePair<string, FileInfo>>();
            foreach (FileInfo f in all)
            {
                string nameNoExt = Path.GetFileNameWithoutExtension(f.Name);
                Match m = pattern.Match(nameNoExt);
                if (!m.Success) continue;

                stamped.Add(new KeyValuePair<string, FileInfo>(
                    m.Groups[1].Value + m.Groups[2].Value, f));
            }

            stamped.Sort((a, b) => CompareOrdinal(a.Key, b.Key));

            var picked = new List<FileInfo>();

            foreach (var kv in stamped)
            {
                string ymd = kv.Key.Substring(0, 8);
                if (CompareOrdinal(ymd, startYmd) < 0) continue;
                if (CompareOrdinal(ymd, endYmd) > 0) continue;
                picked.Add(kv.Value);
            }

            // آرشیو مرزی: اولین زیپی که بعد از پایان بازه ساخته شده، دنباله‌ی
            // آخرین روزِ بازه را در خود دارد. بدون این، لاگ‌های چند ساعت آخر
            // بی‌سروصدا از دست می‌روند و کاربر متوجه نمی‌شود.
            if (_includeBoundary)
            {
                foreach (var kv in stamped)
                {
                    if (CompareOrdinal(kv.Key.Substring(0, 8), endYmd) > 0)
                    {
                        picked.Add(kv.Value);
                        break;
                    }
                }
            }

            // فایل زنده‌ی امروز — فقط اگر بازه شامل امروز باشد
            byte[]? liveBytes = null;
            if (includeLiveFile)
            {
                string todayYmd = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                if (CompareOrdinal(endYmd, todayYmd) >= 0)
                {
                    string livePath = Path.Combine(root, ArmaghanLiveFile);
                    if (File.Exists(livePath))
                        liveBytes = TryReadShared(livePath);
                }
            }

            if (picked.Count == 0 && liveBytes == null)
                return Fail("no-file.txt", "در این بازه فایلی روی دستگاه یافت نشد.");

            // ---- سقف حجم ----
            long total = liveBytes == null ? 0L : liveBytes.LongLength;
            foreach (FileInfo f in picked) total += f.Length;

            if (total > _maxBytes)
                return TooBig(picked, total);

            // ---- تک‌فایل: بدون بسته‌بندی مجدد ----
            if (picked.Count == 1 && liveBytes == null)
            {
                FileInfo only = picked[0];
                byte[]? raw = TryReadShared(only.FullName);
                if (raw == null)
                    return Fail("read-error.txt", "فایل خوانده نشد: " + only.Name);

                return new FileCollectResult
                {
                    Data = raw,
                    ContentType = IsAlreadyCompressed(only.Extension)
                                  ? "application/zip" : "text/plain",
                    FileName = only.Name,
                };
            }

            // ---- چند فایل: زیپ ----
            return BuildZip(
                $"{tag}_{startYmd}-{endYmd}.zip",
                zip =>
                {
                    foreach (FileInfo f in picked)
                    {
                        try { zip.AddFile(f.FullName, ""); }
                        catch { /* فایل قفل یا حذف‌شده — رد شو */ }
                    }

                    if (liveBytes != null)
                        zip.AddEntry(ArmaghanLiveFile, liveBytes);
                });
        }

        // =================================================================
        //  تصاویر — پوشه‌های روزانه با نام شمسی
        // =================================================================

        private FileCollectResult CollectImages(string startYmd, string endYmd)
        {
            if (!Directory.Exists(_imageArchivePath))
                return Fail("no-path.txt", "مسیر تصاویر روی دستگاه وجود ندارد: " + _imageArchivePath);

            // بازه‌ی میلادی → شمسی، فقط یک بار
            string? startJ = ToJalaliYmd(startYmd);
            string? endJ = ToJalaliYmd(endYmd);
            if (startJ == null || endJ == null)
                return Fail("bad-request.txt", "تبدیل تاریخ به شمسی ناموفق بود.");

            DirectoryInfo[] dirs;
            try
            {
                dirs = new DirectoryInfo(_imageArchivePath)
                       .GetDirectories("*", SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex)
            {
                return Fail("no-access.txt", "دسترسی به مسیر تصاویر ممکن نشد: " + ex.Message);
            }

            var picked = new List<DirectoryInfo>();
            foreach (DirectoryInfo d in dirs)
            {
                Match match = ImageFolder.Match(d.Name);

                if (!match.Success)
                    continue;

                string folderJmd = match.Groups[1].Value;

                if (CompareOrdinal(folderJmd, startJ) < 0)
                    continue;

                if (CompareOrdinal(folderJmd, endJ) > 0)
                    continue;

                picked.Add(d);
            }

            picked.Sort((a, b) => CompareOrdinal(a.Name, b.Name));

            if (picked.Count == 0)
                return Fail("no-file.txt",
                    $"تصویری در بازه‌ی {startJ} تا {endJ} (شمسی) یافت نشد.");

            // ---- سقف حجم ----
            var sizes = new List<KeyValuePair<string, long>>();
            long total = 0;
            foreach (DirectoryInfo d in picked)
            {
                long s = DirectorySize(d);
                sizes.Add(new KeyValuePair<string, long>(d.Name, s));
                total += s;
            }

            if (total > _maxBytes)
                return TooBigFolders(sizes, total);

            return BuildZip(
                $"images_{startJ}-{endJ}.zip",
                zip =>
                {
                    foreach (DirectoryInfo d in picked)
                    {
                        try { zip.AddDirectory(d.FullName, d.Name); }
                        catch { /* پوشه‌ی مشکل‌دار — رد شو */ }
                    }
                });
        }

        // =================================================================
        //  رفتار قدیمی — برای فرمان‌های بدون منبع
        // =================================================================

        private FileCollectResult CollectLegacy(string startYmd, string endYmd)
        {
            var roots = new[] { _legacyJournalPath, _armaghanLogPath };
            var picked = new List<FileInfo>();

            foreach (string root in roots)
            {
                if (string.IsNullOrEmpty(root)) continue;
                if (!Directory.Exists(root)) continue;

                FileInfo[] files;
                try { files = new DirectoryInfo(root).GetFiles("*", SearchOption.TopDirectoryOnly); }
                catch { continue; }

                foreach (FileInfo f in files)
                {
                    string nameNoExt = Path.GetFileNameWithoutExtension(f.Name);

                    string? ymd = null;
                    Match m = LegacyEjName.Match(nameNoExt);
                    if (m.Success)
                    {
                        ymd = m.Groups[2].Value;
                    }
                    else
                    {
                        Match m2 = ArmaghanArchive.Match(nameNoExt);
                        if (m2.Success) ymd = m2.Groups[1].Value;
                    }

                    if (ymd == null) continue;
                    if (CompareOrdinal(ymd, startYmd) < 0) continue;
                    if (CompareOrdinal(ymd, endYmd) > 0) continue;

                    picked.Add(f);
                }
            }

            if (picked.Count == 0)
                return Fail("no-journal.txt", "ژورنالی در این بازه یافت نشد.");

            long total = 0;
            foreach (FileInfo f in picked) total += f.Length;
            if (total > _maxBytes) return TooBig(picked, total);

            if (picked.Count == 1)
            {
                byte[]? raw = TryReadShared(picked[0].FullName);
                if (raw == null) return Fail("read-error.txt", "فایل خوانده نشد.");

                return new FileCollectResult
                {
                    Data = raw,
                    ContentType = IsAlreadyCompressed(picked[0].Extension)
                                  ? "application/zip" : "text/plain",
                    FileName = picked[0].Name,
                };
            }

            return BuildZip(
                $"journal_{startYmd}-{endYmd}.zip",
                zip =>
                {
                    foreach (FileInfo f in picked)
                    {
                        try { zip.AddFile(f.FullName, ""); }
                        catch { }
                    }
                });
        }

        // =================================================================
        //  ساخت زیپ
        // =================================================================

        private FileCollectResult BuildZip(string outName, Action<ZipFile> fill)
        {
            using (var ms = new MemoryStream())
            using (var zip = new ZipFile())
            {
                zip.AlternateEncoding = Encoding.UTF8;
                zip.AlternateEncodingUsage = ZipOption.AsNecessary;

                // اگر فایلی وسط کار قفل شد، کل عملیات را نینداز
                zip.ZipErrorAction = ZipErrorAction.Skip;

                fill(zip);

                if (zip.Entries.Count == 0)
                    return Fail("no-file.txt", "هیچ فایل قابل خواندنی پیدا نشد.");

                // ورودی‌های از قبل فشرده را دوباره فشرده نکن — فقط CPU مصرف
                // می‌کند و تقریباً چیزی صرفه‌جویی نمی‌شود.
                foreach (ZipEntry e in zip.Entries)
                {
                    string ext = Path.GetExtension(e.FileName);
                    e.CompressionLevel = IsAlreadyCompressed(ext)
                        ? CompressionLevel.None
                        : CompressionLevel.BestSpeed;
                }

                try
                {
                    zip.Save(ms);
                }
                catch (Exception ex)
                {
                    return Fail("zip-error.txt", "ساخت فایل زیپ ناموفق بود: " + ex.Message);
                }

                return new FileCollectResult
                {
                    Data = ms.ToArray(),
                    ContentType = "application/zip",
                    FileName = outName,
                };
            }
        }

        // =================================================================
        //  کمکی
        // =================================================================

        /// <summary>
        /// فایل زنده‌ی ژورنال همیشه توسط ارمغان باز است. با File.ReadAllBytes
        /// قطعاً IOException می‌گیریم؛ باید صریحاً FileShare.ReadWrite بدهیم.
        /// </summary>
        private static byte[]? TryReadShared(string path)
        {
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                                               FileShare.ReadWrite | FileShare.Delete))
                using (var ms = new MemoryStream())
                {
                    fs.CopyTo(ms);
                    return ms.ToArray();
                }
            }
            catch
            {
                return null;
            }
        }

        private static long DirectorySize(DirectoryInfo d)
        {
            long total = 0;
            try
            {
                foreach (FileInfo f in d.GetFiles("*", SearchOption.AllDirectories))
                    total += f.Length;
            }
            catch { }
            return total;
        }

        /// <summary>میلادی yyyyMMdd → شمسی yyyyMMdd</summary>
        private static string? ToJalaliYmd(string gregorianYmd)
        {
            try
            {
                int y = int.Parse(gregorianYmd.Substring(0, 4), CultureInfo.InvariantCulture);
                int m = int.Parse(gregorianYmd.Substring(4, 2), CultureInfo.InvariantCulture);
                int d = int.Parse(gregorianYmd.Substring(6, 2), CultureInfo.InvariantCulture);

                var dt = new DateTime(y, m, d, 0, 0, 0, DateTimeKind.Unspecified);
                var pc = new PersianCalendar();

                return pc.GetYear(dt).ToString("0000", CultureInfo.InvariantCulture)
                     + pc.GetMonth(dt).ToString("00", CultureInfo.InvariantCulture)
                     + pc.GetDayOfMonth(dt).ToString("00", CultureInfo.InvariantCulture);
            }
            catch
            {
                return null;
            }
        }

        private static bool IsAlreadyCompressed(string? ext)
        {
            if (string.IsNullOrEmpty(ext)) return false;
            string e = ext.ToLowerInvariant();
            return e == ".zip" || e == ".jpg" || e == ".jpeg" || e == ".png"
                || e == ".gif" || e == ".rar" || e == ".7z" || e == ".gz"
                || e == ".mp4" || e == ".avi";
        }

        private static FileCollectResult TooBig(List<FileInfo> files, long total)
        {
            var sb = new StringBuilder();
            sb.AppendLine("حجم فایل‌های این بازه بیش از حد مجاز است.");
            sb.AppendLine($"مجموع: {Mb(total)} مگابایت");
            sb.AppendLine();
            sb.AppendLine("فایل‌های موجود در این بازه:");
            foreach (FileInfo f in files)
                sb.AppendLine($"  {f.Name}  —  {Mb(f.Length)} مگابایت");
            sb.AppendLine();
            sb.AppendLine("لطفاً بازه‌ی کوتاه‌تری انتخاب کنید.");

            return Fail("too-large.txt", sb.ToString());
        }

        private static FileCollectResult TooBigFolders(
            List<KeyValuePair<string, long>> sizes, long total)
        {
            var sb = new StringBuilder();
            sb.AppendLine("حجم تصاویر این بازه بیش از حد مجاز است.");
            sb.AppendLine($"مجموع: {Mb(total)} مگابایت");
            sb.AppendLine();
            sb.AppendLine("حجم هر روز (تاریخ شمسی):");
            foreach (var kv in sizes)
                sb.AppendLine($"  {kv.Key}  —  {Mb(kv.Value)} مگابایت");
            sb.AppendLine();
            sb.AppendLine("لطفاً بازه‌ی کوتاه‌تری انتخاب کنید.");

            return Fail("too-large.txt", sb.ToString());
        }

        private static string Mb(long bytes)
            => (bytes / 1024.0 / 1024.0).ToString("0.0", CultureInfo.InvariantCulture);

        /// <summary>
        /// خطا هم به‌صورت فایل متنی برمی‌گردد تا کاربر در مرورگر دلیل را
        /// ببیند، نه یک دانلود خالی بی‌توضیح.
        /// </summary>
        private static FileCollectResult Fail(string fileName, string message)
            => new FileCollectResult
            {
                Data = Encoding.UTF8.GetBytes(message),
                ContentType = "text/plain",
                FileName = fileName,
                Message = message,
            };

        private static int CompareOrdinal(string a, string b)
            => string.Compare(a, b, StringComparison.Ordinal);

        private static bool IsYmd(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length != 8) return false;
            for (int i = 0; i < 8; i++)
            {
                char c = s[i];
                if (c < '0' || c > '9') return false;
            }
            return true;
        }

        private static string FirstNonEmpty(string? a, string fallback)
            => string.IsNullOrWhiteSpace(a) ? fallback : a!.Trim();
    }
}