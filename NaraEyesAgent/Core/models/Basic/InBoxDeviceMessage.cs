
namespace NaraEyesAgent.Core.Models.Basic
{
    public class InBoxDeviceMessage
    {
        public string DeviceIp { get; set; } = string.Empty;
        public bool Processed { get; set; } = false;
        public DateTime? ProcessedAt { get; set; }
        public MessageType MessageType { get; set; }
        public string Payload { get; set; } = string.Empty;
    }
    public enum MessageType
    {
        Heartbeat = 1,       // سیگنال زنده بودن دستگاه (هر ۳۰ ثانیه)
        Metrics = 2,         // اطلاعات CPU, RAM, Disk, Temp
        DeviceEvent = 3,     // رویداد XFS (خطا یا تغییر وضعیت ماژول)
        ScreenshotAck = 4,   // تأیید اجرای دستور اسکرین‌شات
        CommandAck = 5,      // تأیید اجرای سایر دستورات
        ErrorReport = 6,     // گزارش خطای ایجنت یا سیستم
        LogUpload = 7,
        EJournal = 8,
        FileUpload = 9,
              Group =10
    }
}
