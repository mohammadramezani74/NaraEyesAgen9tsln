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
