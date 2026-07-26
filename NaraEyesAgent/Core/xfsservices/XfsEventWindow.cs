using NaraEyesAgent.Core.XFSPatterns.package;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace NaraEyesAgent.Core.XFSServices
{

    #region Event Window (XFS → Persian messages)

    internal sealed class XfsEventWindow : NativeWindow, IDisposable
    {
        public XfsEventWindow()
        {
            CreateHandle(new CreateParams());
        }

        // شما می‌تونید این رو به وب‌سرویس‌تون وصل کنید
        private static void ReportToServer(XfsUiEvent e)
        {

            try
            {
                string json = PersianText.BuildJson(e);
            
                SafeLog("[SEND] " + json);
            }
            catch { }
        }

        private static void SafeLog(string text)
        {
            try { Console.WriteLine(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "  " + text); }
            catch { /* ignore */ }
        }


        protected override void WndProc(ref Message m)
        {
            // ⚠ مقادیر واقعی را از SDK خودت جایگزین کن
            const int WFS_EXECUTE_EVENT = 0x0402;
            const int WFS_SERVICE_EVENT = 0x0403;
            const int WFS_USER_EVENT = 0x0404;
            const int WFS_SYSTEM_EVENT = 0x0405;

            const int XFS_SYSE_HARDWARE_ERROR = 0x01;
            const int XFS_SYSE_SOFTWARE_ERROR = 0x02;
            const int XFS_SYSE_USER_ERROR = 0x03;
            const int XFS_SYSE_DEVICE_STATUS = 0x04;
            const int XFS_SYSE_APP_DISCONNECT = 0x05;
            const int XFS_SYSE_UNDELIVERABLE_MSG = 0x06;

            bool isXfsMsg =
                (m.Msg == WFS_EXECUTE_EVENT) ||
                (m.Msg == WFS_SERVICE_EVENT) ||
                (m.Msg == WFS_USER_EVENT) ||
                (m.Msg == WFS_SYSTEM_EVENT);

            if (m.LParam == IntPtr.Zero || !isXfsMsg)
            {
                base.WndProc(ref m);
                return;
            }

            IntPtr pRes = m.LParam;

            //try
            //{
            //    // .NET 3.5 → غیرجنریک
            //    WFSRESULT res = (WFSRESULT)Marshal.PtrToStructure(pRes, typeof(WFSRESULT));

            //    if (m.Msg == WFS_SYSTEM_EVENT)
            //    {
            //        // ترجمه‌ی تیتر/متن/شدت رویداد سیستمی
            //        string kind, title, msg, suggestion, sev;
            //        PersianText.MapSystemEvent(res.u_dwEventID, out kind, out title, out msg, out suggestion, out sev);

            //        // اگر DEVICE_STATUS بود، از lpBuffer وضعیت را هم بخوانیم و فارسی کنیم
            //        if (res.u_dwEventID == XFS_SYSE_DEVICE_STATUS)
            //        {
            //            if (res.lpBuffer != IntPtr.Zero)
            //            {
            //                WFSDEVSTATUS dev = (WFSDEVSTATUS)Marshal.PtrToStructure(res.lpBuffer, typeof(WFSDEVSTATUS));
            //                string t2, m2, s2, sev2;
            //                PersianText.MapDeviceState(dev.dwState, out t2, out m2, out s2, out sev2);

            //                // جایگزینی عنوان/متن با نسخه‌ی وضعیتی
            //                title = t2;
            //                msg = m2;
            //                suggestion = s2;
            //                sev = sev2;

            //                ReportToServer(new XfsUiEvent
            //                {
            //                    KindFa = "وضعیت دستگاه",
            //                    TitleFa = title,
            //                    MessageFa = msg,
            //                    SuggestionFa = suggestion,
            //                    Severity = sev,
            //                    DeviceState = dev.dwState,
            //                    EventId = null
            //                });
            //            }
            //            else
            //            {
            //                ReportToServer(new XfsUiEvent
            //                {
            //                    KindFa = "وضعیت دستگاه",
            //                    TitleFa = "وضعیت نامشخص",
            //                    MessageFa = "اطلاعات وضعیت موجود نیست (lpBuffer تهی).",
            //                    SuggestionFa = "—",
            //                    Severity = "Info",
            //                    EventId = null
            //                });
            //            }
            //        }
            //        else
            //        {
            //            // سایر ایونت‌های سیستمی (HW/SW/User/…)
            //            ReportToServer(new XfsUiEvent
            //            {
            //                KindFa = kind,
            //                TitleFa = title,
            //                MessageFa = msg,
            //                SuggestionFa = suggestion,
            //                Severity = sev,
            //                EventId =null
            //            });
            //        }
            //    }
            //    else
            //    {
             
            //        ReportToServer(new XfsUiEvent
            //        {
            //            KindFa = "ایونت سرویس/کاربر/اجرا",
            //            TitleFa = "رویداد جدید",
            //            MessageFa = "Msg=" + m.Msg + " (برای فارسی‌سازی دقیق‌تر، ثابت‌های کلاس CDM/IDC را اضافه کن)",
            //            SuggestionFa = "—",
            //            Severity = "Info",
            //            EventId = res.u_dwEventID
            //        });
            //    }
            //}
            //catch (Exception ex)
            //{
            //    SafeLog("WndProc error: " + ex.Message);
            //}
            //finally
            //{
            //    try { XfsApi.WFSFreeResult(pRes); } catch (Exception ex) { SafeLog("WFSFreeResult error: " + ex.Message); }
            //}

            base.WndProc(ref m);
        }

        public void Dispose()
        {
            try { DestroyHandle(); } catch { /* ignore */ }
        }
    }

    #endregion

    #region XFS interop structs (حداقلی و سازگار با .NET 3.5)

    [StructLayout(LayoutKind.Sequential, Pack = XFSConstants.STRUCTPACKSIZE, CharSet = XFSConstants.CHARSET)]
    public struct SYSTEMTIME
    {
        public ushort wYear;
        public ushort wMonth;
        public ushort wDayOfWeek;
        public ushort wDay;
        public ushort wHour;
        public ushort wMinute;
        public ushort wSecond;
        public ushort wMilliseconds;
    }

    [StructLayout(LayoutKind.Sequential, Pack = XFSConstants.STRUCTPACKSIZE, CharSet = XFSConstants.CHARSET)]
    public struct WFSRESULT
    {
        public uint RequestID;
        public ushort hService;
        public SYSTEMTIME tsTimestamp;
        public int hResult;
        public uint dwCommandCodeOrEventID;
        public IntPtr lpBuffer;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct WFSDEVSTATUS
    {
        public IntPtr lpszPhysicalName;     // اختیاری
        public IntPtr lpszWorkstationName;  // اختیاری
        public uint dwState;              // یکی از WFS_STAT_* (0..8)
    }

 



    #endregion

}

