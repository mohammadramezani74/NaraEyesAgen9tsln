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
            // مقادیر رسمی از xfsapi.h — WM_USER = 0x0400
            const int WFS_EXECUTE_EVENT = 0x0400 + 20; // 0x0414
            const int WFS_SERVICE_EVENT = 0x0400 + 21; // 0x0415
            const int WFS_USER_EVENT = 0x0400 + 22; // 0x0416
            const int WFS_SYSTEM_EVENT = 0x0400 + 23; // 0x0417

            // System Event IDs — طبق xfsapi.h
            const uint WFS_SYSE_UNDELIVERABLE_MSG = 1;
            const uint WFS_SYSE_HARDWARE_ERROR = 2;
            const uint WFS_SYSE_VERSION_ERROR = 3;
            const uint WFS_SYSE_DEVICE_STATUS = 4;
            const uint WFS_SYSE_APP_DISCONNECT = 5;
            const uint WFS_SYSE_SOFTWARE_ERROR = 6;
            const uint WFS_SYSE_USER_ERROR = 7;
            const uint WFS_SYSE_LOCK_REQUESTED = 8;

            bool isXfsMsg =
                (m.Msg == WFS_EXECUTE_EVENT) ||
                (m.Msg == WFS_SERVICE_EVENT) ||
                (m.Msg == WFS_USER_EVENT) ||
                (m.Msg == WFS_SYSTEM_EVENT);

            if (!isXfsMsg || m.LParam == IntPtr.Zero)
            {
                base.WndProc(ref m);
                return;
            }

            IntPtr pRes = m.LParam;

            try
            {
                WFSRESULT res = (WFSRESULT)Marshal.PtrToStructure(pRes, typeof(WFSRESULT));
                uint eventId = res.dwCommandCodeOrEventID;

                if (m.Msg == WFS_SYSTEM_EVENT)
                {
                    // اگر صاحب قفل هستیم و کس دیگری قفل خواسته، فوراً آزاد کن
                    if (eventId == WFS_SYSE_LOCK_REQUESTED)
                    {
                        SafeLog("[XFS] LOCK_REQUESTED — releasing lock, hService=" + res.hService);
                        try { XfsApi.WFSUnlock(res.hService); } catch { }
                    }
                    else if (eventId == WFS_SYSE_DEVICE_STATUS && res.lpBuffer != IntPtr.Zero)
                    {
                        WFSDEVSTATUS dev =
                            (WFSDEVSTATUS)Marshal.PtrToStructure(res.lpBuffer, typeof(WFSDEVSTATUS));

                        string t, msg, sug, sev;
                        PersianText.MapDeviceState(dev.dwState, out t, out msg, out sug, out sev);

                        ReportToServer(new XfsUiEvent
                        {
                            KindFa = "وضعیت دستگاه",
                            TitleFa = t,
                            MessageFa = msg,
                            SuggestionFa = sug,
                            Severity = sev,
                            DeviceState = dev.dwState,
                            EventId = eventId
                        });
                    }
                    else
                    {
                        string kind, title, msg, sug, sev;
                        PersianText.MapSystemEvent(eventId, out kind, out title, out msg, out sug, out sev);

                        // برای خطاهای HW/SW/User مقدار dwAction را هم بخوان
                        uint? action = null;
                        if ((eventId == WFS_SYSE_HARDWARE_ERROR ||
                             eventId == WFS_SYSE_SOFTWARE_ERROR ||
                             eventId == WFS_SYSE_USER_ERROR) && res.lpBuffer != IntPtr.Zero)
                        {
                            try
                            {
                                WFSHWERROR err =
                                    (WFSHWERROR)Marshal.PtrToStructure(res.lpBuffer, typeof(WFSHWERROR));
                                action = err.dwAction;
                                msg += "  [dwAction=" + err.dwAction + "]";
                            }
                            catch { }
                        }

                        ReportToServer(new XfsUiEvent
                        {
                            KindFa = kind,
                            TitleFa = title,
                            MessageFa = msg,
                            SuggestionFa = sug,
                            Severity = sev,
                            DeviceState = action,
                            EventId = eventId
                        });
                    }
                }
                else
                {
                    ReportToServer(new XfsUiEvent
                    {
                        KindFa = "رویداد سرویس/کاربر/اجرا",
                        TitleFa = "رویداد جدید",
                        MessageFa = "Msg=0x" + m.Msg.ToString("X4") + " EventId=" + eventId,
                        SuggestionFa = "—",
                        Severity = "Info",
                        EventId = eventId
                    });
                }
            }
            catch (Exception ex)
            {
                SafeLog("WndProc error: " + ex.Message);
            }
            finally
            {
                // اجباری طبق اسپک — وگرنه نشتی حافظه در XFS Manager
                try { XfsApi.WFSFreeResult(pRes); }
                catch (Exception ex) { SafeLog("WFSFreeResult error: " + ex.Message); }
            }

            base.WndProc(ref m);
        }

        public void Dispose()
        {
            try { DestroyHandle(); } catch { /* ignore */ }
        }
    }

    #endregion


}

