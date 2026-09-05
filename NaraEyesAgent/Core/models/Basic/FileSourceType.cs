namespace NaraEyesAgent.Core.Models.Basic
{
    /// <summary>
    /// منبع فایلی که سرور از ایجنت می‌خواهد.
    ///
    /// عمداً یک enum مشترک برای هر سه منبع ساخته شده تا افزودن منبع
    /// چهارم فقط یک ردیف اینجا + یک مسیر در Config.txt باشد، نه یک
    /// مسیر کد جدید.
    ///
    /// ⚠️ مقادیر باید دقیقاً با NaraEyes.Domain.Enumerations.FileSourceType
    /// در سرور یکی باشند. اگر یکی را عوض کردی، آن یکی را هم عوض کن.
    /// </summary>
    public enum FileSourceType
    {
        /// <summary>
        /// رفتار قدیمی: الگوی ej_*_YYYYMMDD در D:\ejournal به‌علاوه‌ی
        /// آرشیوهای ارمغان. برای سازگاری با فرمان‌هایی که Payload منبع
        /// ندارند (سرور قدیمی) نگه داشته شده — مقدار پیش‌فرض صفر است.
        /// </summary>
        LegacyEjournal = 0,

        /// <summary>ژورنال ارمغان — آرشیوهای «all logs-…» + فایل زنده‌ی امروز</summary>
        ArmaghanJournal = 1,

        /// <summary>لاگ سپنتا — فقط آرشیوهای «backed up-…»</summary>
        SepantaLog = 2,

        /// <summary>
        /// تصاویر ارمغان — پوشه‌های روزانه با نام تاریخ **شمسی** (مثل 14020502).
        /// تنها منبعی که تقویمش شمسی است.
        /// </summary>
        ArmaghanImages = 3,
    }
}