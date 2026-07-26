using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NaraEyesAgent.Core.XFSServices
{
    public sealed class XfsUiEvent
    {
        public string KindFa;        // نوع رخداد فارسی (مثلاً "وضعیت دستگاه" / "خطای سخت‌افزاری")
        public string TitleFa;       // عنوان کوتاه فارسی (مثلاً "دستگاه آفلاین شد")
        public string MessageFa;     // توضیح انسانی برای کاربر
        public string SuggestionFa;  // پیشنهاد اقدام (ری‌استارت، تماس، بررسی درب سیف، ...)
        public string Severity;      // Info/Warning/Error
        public uint? DeviceState;    // اگر داشتیم
        public uint? EventId;        // کد ایونت خام
    }
}
