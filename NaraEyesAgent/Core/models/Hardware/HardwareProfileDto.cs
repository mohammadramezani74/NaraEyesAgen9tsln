namespace NaraEyesAgent.Core.Models.Hardware
{
    /// <summary>
    /// عکس لحظه‌ای سخت‌افزار کامپیوتر داخل ATM.
    ///
    /// ⚠️ این کلاس باید با NaraEyes.Application.Contracts.Models.Hardware
    /// .HardwareProfilePayload در سرور **دقیقاً** یکی بماند. نام پراپرتی‌ها
    /// روی سیم منتقل می‌شوند؛ تغییر یک نام یعنی آن فیلد در سرور null
    /// می‌شود — بدون خطا، فقط بی‌سروصدا.
    /// </summary>
    public sealed class HardwareProfileDto
    {
        // ---------- حافظه ----------
        public int RamTotalMb { get; set; }
        public List<RamModuleDto> RamModules { get; set; } = new();

        // ---------- پردازنده ----------
        public string? CpuName { get; set; }
        public int CpuCores { get; set; }
        public int CpuLogicalProcessors { get; set; }
        public int CpuMaxClockMhz { get; set; }
        public string? CpuId { get; set; }

        // ---------- دیسک ----------
        public string? DiskModel { get; set; }
        public long DiskSizeBytes { get; set; }
        public string? DiskSerial { get; set; }
        public string? DiskInterface { get; set; }

        // ---------- مادربرد ----------
        public string? BoardManufacturer { get; set; }
        public string? BoardProduct { get; set; }
        public string? BoardSerial { get; set; }
        public string? BiosVersion { get; set; }

        // ---------- سیستم ----------
        public string? ComputerName { get; set; }
        public string? OsVersion { get; set; }

        public DateTime CollectedAt { get; set; }

        /// <summary>
        /// اگر false باشد، پروفایل **ارسال نمی‌شود**.
        ///
        /// روی بردهای صنعتی گاهی WMI ناقص برمی‌گردد. اگر داده‌ی ناقص را
        /// بفرستیم، سرور آن را «قطعه برداشته شد» تفسیر می‌کند و یک اختلال
        /// موقت WMI می‌تواند همزمان روی ده‌ها دستگاه آلارم بحرانی بزند.
        /// نبود داده هرگز نباید نتیجه‌ی منفی بدهد.
        /// </summary>
        public bool IsComplete { get; set; }
    }

    public sealed class RamModuleDto
    {
        public int CapacityMb { get; set; }
        public string? Manufacturer { get; set; }
        public string? PartNumber { get; set; }
        public string? SerialNumber { get; set; }
        public string? DeviceLocator { get; set; }
        public int SpeedMhz { get; set; }
    }
}