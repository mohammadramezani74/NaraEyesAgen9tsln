using System;
using System.Text;

namespace NaraEyesAgent.Core.XFSServices
{
    internal static class PersianText
    {
        // نگاشت وضعیت عمومی دستگاه به عنوان/توضیح/پیشنهاد
        public static void MapDeviceState(uint state,
            out string title, out string msg, out string suggestion, out string severity)
        {
            switch (state)
            {
                case 0: // WFS_STAT_DEVONLINE
                    title = "دستگاه آنلاین شد";
                    msg = "ارتباط و وضعیت کلی دستگاه عادی است.";
                    suggestion = "—";
                    severity = "Info";
                    return;
                case 1: // OFFLINE
                    title = "دستگاه آفلاین شد";
                    msg = "دستگاه از دسترس خارج است یا ارتباط قطع شده.";
                    suggestion = "اتصال شبکه/سرور را بررسی کنید.";
                    severity = "Warning";
                    return;
                case 2: // POWEROFF
                    title = "برق دستگاه قطع است";
                    msg = "ماژول/دستگاه خاموش گزارش شده است.";
                    suggestion = "برق و کابل‌ها را بررسی کنید.";
                    severity = "Error";
                    return;
                case 3: // NODEVICE
                    title = "ماژول در دسترس نیست";
                    msg = "ماژول گزارش شده شناسایی نشده یا نصب نیست.";
                    suggestion = "نصب/اتصالات ماژول را بررسی کنید.";
                    severity = "Error";
                    return;
                case 4: // HWERROR
                    title = "خطای سخت‌افزاری";
                    msg = "خرابی یا گیر مکانیکی گزارش شده.";
                    suggestion = "گیر اسکناس/مکانیزم را بررسی و در صورت لزوم ری‌استارت کنید.";
                    severity = "Error";
                    return;
                case 5: // USERERROR
                    title = "خطای کاربری/عملیاتی";
                    msg = "رفتار کاربر یا عملیات باعث خطا شده است.";
                    suggestion = "راهنمای کاربری را نمایش دهید/دوباره تلاش شود.";
                    severity = "Warning";
                    return;
                case 6: // BUSY
                    title = "دستگاه مشغول است";
                    msg = "در حال پردازش درخواست دیگر.";
                    suggestion = "چند لحظه بعد دوباره تلاش کنید.";
                    severity = "Info";
                    return;
                case 7: // FRAUD
                    title = "تلاش برای تقلب";
                    msg = "سیستم رفتار مشکوک را گزارش کرده است.";
                    suggestion = "به تیم ناظر اطلاع دهید و لاگ‌ها را بررسی کنید.";
                    severity = "Error";
                    return;
                case 8: // POTENTIALFRAUD
                    title = "احتمال تقلب";
                    msg = "رفتار غیرمعمول مشاهده شد.";
                    suggestion = "پایش دقیق‌تر و اطلاع‌رسانی به ناظر.";
                    severity = "Warning";
                    return;
            }
            title = "وضعیت نامشخص";
            msg = "کد وضعیت: " + state;
            suggestion = "—";
            severity = "Info";
        }

        public static void MapSystemEvent(uint eventId,
            out string kind, out string title, out string msg, out string suggestion, out string severity)
        {
            // مقداردهی اولیه
            kind = "ایونت سیستمی";
            title = "ایونت سیستمی";
            msg = "کد ایونت: " + eventId;
            suggestion = "—";
            severity = "Info";

            switch (eventId)
            {
                case 0x04: // XFS_SYSE_DEVICE_STATUS (placeholder)
                    kind = "وضعیت دستگاه";
                    // عنوان/متن را بعداً با MapDeviceState پر می‌کنیم (در WndProc)
                    return;

                case 0x01: // XFS_SYSE_HARDWARE_ERROR
                    kind = "خطای سخت‌افزاری";
                    title = "خطای سخت‌افزاری گزارش شد";
                    msg = "خرابی یا گیر مکانیکی توسط XFS گزارش شده است.";
                    suggestion = "ماژول را بررسی و در صورت نیاز ری‌استارت یا سرویس کنید.";
                    severity = "Error";
                    return;

                case 0x02: // XFS_SYSE_SOFTWARE_ERROR
                    kind = "خطای نرم‌افزاری";
                    title = "خطای نرم‌افزاری گزارش شد";
                    msg = "سرویس‌دهنده/درایور گزارش خطایی نرم‌افزاری داده است.";
                    suggestion = "لاگ‌ها را بررسی و سرویس را ری‌استارت کنید.";
                    severity = "Error";
                    return;

                case 0x03: // XFS_SYSE_USER_ERROR
                    kind = "خطای کاربری";
                    title = "خطای کاربری/عملیاتی";
                    msg = "تعامل ناصحیح کاربر/اپلیکیشن باعث خطا شده.";
                    suggestion = "راهنمای کاربری/UX را نمایش دهید.";
                    severity = "Warning";
                    return;

                case 0x06: // XFS_SYSE_UNDELIVERABLE_MSG
                    kind = "پیام تحویل‌ناشدنی";
                    title = "پیام تحویل‌ناشدنی";
                    msg = "یک پیام به مقصد مناسب تحویل نشده.";
                    suggestion = "مسیر پیام/ثبت‌نام ایونت‌ها را بررسی کنید.";
                    severity = "Warning";
                    return;

                case 0x05: // XFS_SYSE_APP_DISCONNECT
                    kind = "قطع ارتباط اپلیکیشن";
                    title = "قطع ارتباط اپلیکیشن";
                    msg = "XFS گزارش قطع ارتباط اپلیکیشن را داده است.";
                    suggestion = "سلامت اپلیکیشن/شبکه را بررسی کنید.";
                    severity = "Warning";
                    return;
            }
        }
        public static string BuildJson(XfsUiEvent e)
        {

            Func<string, string> esc = s => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append("\"kind\":\"").Append(esc(e.KindFa)).Append("\",");
            sb.Append("\"title\":\"").Append(esc(e.TitleFa)).Append("\",");
            sb.Append("\"message\":\"").Append(esc(e.MessageFa)).Append("\",");
            sb.Append("\"suggestion\":\"").Append(esc(e.SuggestionFa)).Append("\",");
            sb.Append("\"severity\":\"").Append(esc(e.Severity)).Append("\"");
            if (e.DeviceState.HasValue) { sb.Append(",\"deviceState\":").Append(e.DeviceState.Value); }
            if (e.EventId.HasValue) { sb.Append(",\"eventId\":").Append(e.EventId.Value); }
            sb.Append("}");
            return sb.ToString();
        }

    }

}
