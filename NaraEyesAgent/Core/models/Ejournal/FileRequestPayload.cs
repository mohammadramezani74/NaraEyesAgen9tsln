namespace NaraEyesAgent.Core.Models.Ejournal
{
    /// <summary>
    /// محتوای Payload فرمان EJournal.
    ///
    /// قبلاً سرور فقط { CommandId } می‌فرستاد و ایجنت اصلاً Payload را
    /// باز نمی‌کرد؛ فقط StartDate و EndDate را می‌خواند. حالا منبع هم
    /// اضافه شده.
    ///
    /// اگر Payload نال یا بدون Source باشد، مقدار صفر یعنی
    /// FileSourceType.LegacyEjournal — یعنی دقیقاً رفتار قبلی. بنابراین
    /// ایجنت جدید با سرور قدیمی هم کار می‌کند و لازم نیست هر ۳۰۰ دستگاه
    /// هم‌زمان با سرور آپدیت شوند.
    /// </summary>
    public sealed class FileRequestPayload
    {
        public Guid CommandId { get; set; }

        /// <summary>مقدار عددی FileSourceType</summary>
        public int Source { get; set; }
    }
}