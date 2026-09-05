namespace NaraEyesAgent.Configuration
{
    public sealed class AppConfig
    {
        // --- سرور ---
        public string ApiBase = "";
        public string PingTarget = "";
        public string TerminalCode = "";

        // --- مسیرها ---
        public string JournalPath = @"D:\ejournal";
        public string EJournalRoot = @"D:\ejournal";
        public string EJournalFallback = @"D:\ejournal";
        public string LogDir = @"C:\ProgramData\NaraEyes\logs";

        // --- مسیرهای درخواست ۴ و ۵ ---
        // این‌ها در Config.txt قابل تغییرند تا اگر روی دستگاهی مسیر فرق
        // داشت، بدون کامپایل مجدد قابل اصلاح باشد.

        /// <summary>آرشیو لاگ ارمغان — «all logs-YYYYMMDDhhmmss.zip» و «journal backup.log»</summary>
        public string ArmaghanLogPath = @"C:\Program Files\Armaghan\log";

        /// <summary>آرشیو لاگ سپنتا — «backed up-YYYYMMDDhhmmss.zip»</summary>
        public string SepantaLogPath = @"C:\Program Files\Sepanta Agent\log";

        /// <summary>پوشه‌های روزانه‌ی تصاویر با نام تاریخ شمسی (مثل 14020502)</summary>
        public string ImageArchivePath = @"C:\Program Files\Armaghan\ImageArchive";

        /// <summary>
        /// سقف حجم پاسخ به مگابایت.
        ///
        /// فایل به‌صورت Base64 داخل JSON پاسخ long-poll برمی‌گردد، یعنی
        /// حدود ۱٫۳۳ برابر حجم می‌شود و هم روی ایجنت و هم روی سرور در
        /// حافظه نگه داشته می‌شود. ۴۰ مگابایت یعنی حدود ۵۳ مگابایت رشته
        /// Base64 — سقف محافظه‌کارانه‌ای است. بالاتر بردنش ریسک دارد.
        /// </summary>
        public int FileTransferMaxMb = 40;

        /// <summary>
        /// آیا اولین آرشیو بعد از پایان بازه هم فرستاده شود؟
        ///
        /// مهر زمانی نام زیپ، لحظه‌ی **ساخته شدن** آرشیو است نه پایان
        /// محتوای روز. بنابراین دنباله‌ی آخرین روزِ بازه در زیپ بعدی است.
        /// true یعنی «کمی داده‌ی اضافه بهتر از داده‌ی گمشده».
        /// </summary>
        public bool IncludeBoundaryArchive = true;

        // --- لاگ ---
        public string LogLevel = "Info";
        public int MaxLogSizeKb = 512;

        // --- پول/لانگ‌پول ---
        public int PollWaitSeconds = 45;
        public int PollJitterSeconds = 15;

        // --- متریک‌ها ---
        public int MetricsIntervalSec = 180;
        public int MetricsJitterSec = 20;
        public int MetricsMinSec = 30;

        // --- اسکرین‌شات ---
        public int ScreenshotQuality = 85;
        public int ScreenshotMaxWidth = 1600;
        public int ScreenshotMaxHeight = 900;

        // --- XFS logical names ---
        public string CdmLogical = "CashDispenser";
        public string IdcLogical = "CardReader";
        public string PtrLogical = "ReceiptPrinter";
        public string SiuLogical = "Sensors";
        public string CameraLogical = "Cameras";
        public string PinLogical = "Encryptor";

        public string Mode = "Silent";
        public bool Tray = true;
        public bool AllowExit = false;

        public bool ProxyEnabled = false;
        public string ProxyUrl = "";
        public string ProxyUser = "";
        public string ProxyPass = "";

        public bool mTLS = false;
        public string EnrollmentToken = "";
    }
}