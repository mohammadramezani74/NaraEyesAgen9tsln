using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace NaraEyesAgent.Infrastructure.ArmaghanState
{
    public enum ArmaghanState
    {
        InService = 1,
        OutOfService = 2,
    }

    /// <summary>
    /// آخرین وضعیت اعلام‌شده توسط خود ارمغان را از فایل ژورنال زنده می‌خواند.
    ///
    /// چرا: کلید اپراتور از طریق SIU خوانده می‌شود، و اگر ماژول SIU باز
    /// نشود دستگاه برای همیشه «خارج از سرویس» گزارش می‌شود حتی وقتی عالی
    /// کار می‌کند. ژورنال ارمغان دیدگاه خود نرم‌افزار ATM را می‌دهد که به
    /// «آیا مشتری می‌توانست تراکنش بزند» نزدیک‌تر است.
    ///
    /// اگر هر چیزی مبهم بود null برمی‌گرداند تا فراخوان به منطق قبلی
    /// برگردد. این کلاس عمداً هیچ‌وقت حدس نمی‌زند.
    /// </summary>
    public static class AtmStateReader
    {
        private const string LiveFileName = "journal backup.log";

        /// <summary>فقط انتهای فایل خوانده می‌شود؛ تا آخر روز بزرگ می‌شود</summary>
        private const int TailBytes = 96 * 1024;

        /// <summary>
        /// null یعنی «نمی‌دانم، تو تصمیم بگیر».
        /// </summary>
        /// <param name="logRoot">مسیر پوشه‌ی لاگ ارمغان</param>
        /// <param name="maxAgeMinutes">اگر آخرین نوشته از این قدیمی‌تر بود، اعتماد نکن</param>
        /// <param name="note">توضیح برای لاگ ایجنت — چرا این نتیجه</param>
        public static ArmaghanState? TryGetState(
            string logRoot, int maxAgeMinutes, out string note)
        {
            note = "";

            try
            {
                if (string.IsNullOrWhiteSpace(logRoot))
                {
                    note = "مسیر لاگ ارمغان تنظیم نشده";
                    return null;
                }

                string path = Path.Combine(logRoot, LiveFileName);
                if (!File.Exists(path))
                {
                    note = "فایل ژورنال زنده وجود ندارد";
                    return null;
                }

                string text = ReadTail(path);
                if (string.IsNullOrEmpty(text))
                {
                    note = "فایل ژورنال خالی یا خوانده نشد";
                    return null;
                }

                string[] lines = text.Replace("\r\n", "\n").Split('\n');

                // ---- تازگی ----
                //
                // اگر ارمغان کرش کند، نوشتن متوقف می‌شود ولی آخرین خط
                // همچنان In-service است. بدون این بررسی، دستگاه مرده را
                // «آماده‌به‌کار» گزارش می‌کنیم — دقیقاً همان چیزی که این
                // سامانه برای تشخیصش ساخته شده.
                DateTime? lastWrite = LastTimestamp(lines);
                if (lastWrite is null)
                {
                    note = "هیچ مهر زمانی معتبری در انتهای فایل نبود";
                    return null;
                }

                double ageMin = (DateTime.Now - lastWrite.Value).TotalMinutes;
                if (ageMin > maxAgeMinutes)
                {
                    note = $"آخرین نوشته {(int)ageMin} دقیقه پیش — کهنه‌تر از آستانه";
                    return null;
                }

                // ---- آخرین تغییر وضعیت ----
                for (int i = lines.Length - 1; i >= 0; i--)
                {
                    if (lines[i].IndexOf("ATM STATE CHANGING",
                            StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    string? raw = FindStateValue(lines, i);
                    if (raw is null) continue;   // بلوک ناقص — عقب‌تر برو

                    string norm = Normalize(raw);

                    // SUSPEND هر شب هنگام بستن بچ رخ می‌دهد و دو ثانیه
                    // طول می‌کشد. اگر چرخه‌ی متریک دقیقاً در آن پنجره
                    // بیفتد، یک بازه‌ی خارج از سرویس ساختگی ثبت می‌شود.
                    if (norm.Contains("SUSPEND"))
                    {
                        // برای اینکه بعداً بدانیم چه کدهایی وجود دارند
                        Console.WriteLine($"[ARMAGHAN] SUSPEND نادیده گرفته شد: {raw.Trim()}");
                        continue;
                    }

                    if (norm == "INSERVICE")
                    {
                        note = $"ژورنال: In-service ({(int)ageMin} دقیقه پیش)";
                        return ArmaghanState.InService;
                    }

                    // هر مقدار ناشناخته امن رفتار می‌کند: خارج از سرویس،
                    // نه در سرویس.
                    note = $"ژورنال: {raw.Trim()}";
                    return ArmaghanState.OutOfService;
                }

                note = "بلوک تغییر وضعیت در انتهای فایل پیدا نشد";
                return null;
            }
            catch (Exception ex)
            {
                note = "خطا در خواندن ژورنال: " + ex.Message;
                return null;
            }
        }

        // -----------------------------------------------------------------

        /// <summary>
        /// ارمغان فایل را باز نگه می‌دارد، پس FileShare.ReadWrite اجباری
        /// است — با File.ReadAllText قطعاً IOException می‌گیریم.
        /// </summary>
        private static string ReadTail(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                                           FileShare.ReadWrite | FileShare.Delete))
            {
                long len = fs.Length;
                long start = len > TailBytes ? len - TailBytes : 0;

                fs.Seek(start, SeekOrigin.Begin);

                var buffer = new byte[len - start];
                int read = fs.Read(buffer, 0, buffer.Length);
                if (read <= 0) return "";

                // انکودینگ فایل قطعی نیست، ولی هر چیزی که ما دنبالش هستیم
                // ASCII است. اگر بخش فارسی خراب دیکد شود اهمیتی ندارد.
                string text = Encoding.UTF8.GetString(buffer, 0, read);

                // خط اول ممکن است از وسط بریده شده باشد
                if (start > 0)
                {
                    int nl = text.IndexOf('\n');
                    if (nl >= 0) text = text.Substring(nl + 1);
                }

                return text;
            }
        }

        /// <summary>آخرین مهر زمانی از الگوی [yyyy/MM/dd HH:mm:ss…]</summary>
        private static DateTime? LastTimestamp(string[] lines)
        {
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string s = lines[i];
                if (s.Length < 21) continue;
                if (s[0] != '[') continue;

                // [2026/08/03 21:06:45,687.426]
                string candidate = s.Substring(1, 19);

                DateTime dt;
                if (DateTime.TryParseExact(candidate, "yyyy/MM/dd HH:mm:ss",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                    return dt;
            }

            return null;
        }

        /// <summary>خط STATE: را در چند خط بعد از سرصفحه پیدا می‌کند</summary>
        private static string? FindStateValue(string[] lines, int headerIndex)
        {
            int limit = Math.Min(lines.Length - 1, headerIndex + 6);

            for (int j = headerIndex + 1; j <= limit; j++)
            {
                string s = lines[j].Trim();

                if (s.StartsWith("STATE:", StringComparison.OrdinalIgnoreCase))
                    return s.Substring("STATE:".Length);
            }

            return null;
        }

        private static string Normalize(string raw)
            => raw.Replace("-", "").Replace("_", "").Replace(" ", "")
                  .Trim().ToUpperInvariant();
    }
}