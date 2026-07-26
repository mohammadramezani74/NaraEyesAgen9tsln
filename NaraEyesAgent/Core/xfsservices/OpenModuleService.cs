using NaraEyesAgent.Core.XFSPatterns.package;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace NaraEyesAgent.Core.XFSServices
{

    public static class OpenModuleService
    {
        public const int XFS_VER_303 = 0x00030003;
        public const int WFS_SUCCESS = 0;

        // کلاس‌های ایونت برای Register (bitmask)
        private const int SERVICE_EVENTS = 0x00000001;
        private const int USER_EVENTS = 0x00000002;
        private const int SYSTEM_EVENTS = 0x00000004;
        private const int EXECUTE_EVENTS = 0x00000008;
        private const int ALL_EVENTS = SERVICE_EVENTS | USER_EVENTS | SYSTEM_EVENTS | EXECUTE_EVENTS;

        // هندل سرویس‌ها (همونطور که خودت گذاشتی)
        public static ushort hCam = 0;
        public static ushort hPin = 0;
        public static ushort hCdm = 0;
        public static ushort hIdc = 0;
        public static ushort hPtr = 0;
        public static ushort hSiu = 0;

        // یک پنجره‌ی مخفی برای گرفتن پیام‌ها
        private static XfsEventWindow _evtWindow;
        private static readonly object _sync = new object();
        private static volatile bool _opened = false;
        private static DateTime _lastCdmOpenAttempt = DateTime.MinValue;
        private static DateTime _lastPtrOpenAttempt = DateTime.MinValue;
        private static DateTime _lastIdcOpenAttempt = DateTime.MinValue;
        private static DateTime _lastcameraOpenAttempt = DateTime.MinValue;
        private static DateTime _lastsiuOpenAttempt = DateTime.MinValue;
        private static DateTime _lastpinOpenAttempt = DateTime.MinValue;
        private static TimeSpan _openRetryInterval = TimeSpan.FromSeconds(30);

        public static void openAllModulesOnce()
        {
            if (_opened) return;
            lock (_sync)
            {
                if (_opened) return;

         
                openAllModules();

                _opened = true;
            }
        }
        public static void openAllModules()
        {
            
            //SafeLog("XFSStartUp...");
            var verApi = new WFSVERSION();
            XfsApi.WFSStartUp(XFS_VER_303, ref verApi);

            // ساخت پنجره‌ی پیام‌ها فقط یک‌بار
            if (_evtWindow == null)
                _evtWindow = new XfsEventWindow();

            // ——— Camera ———
            var verSvcCam = new WFSVERSION();
            var verSpiCam = new WFSVERSION();
            XfsApi.WFSOpen("Camera", IntPtr.Zero, "XfsConsole", 0, 6000, XFS_VER_303,
                           ref verSvcCam, ref verSpiCam, ref hCam);
            if (hCam != 0)
            {
                Console.WriteLine("Camera is Open ed");
            }
            TryRegister("Camera", hCam);

            // ——— PinPad ———
            var verSvcPin = new WFSVERSION();
            var verSpiPin = new WFSVERSION();
            XfsApi.WFSOpen("Encryptor", IntPtr.Zero, "XfsConsole", 0, 6000, XFS_VER_303,
                           ref verSvcPin, ref verSpiPin, ref hPin);
            TryRegister("Encryptor", hPin);
            if (hPin != 0)
            {
                Console.WriteLine("Pin is Open ed");
            }

            // ——— CDM ———
            var verSvcCdm = new WFSVERSION();
            var verSpiCdm = new WFSVERSION();
            XfsApi.WFSOpen("CashDispenser", IntPtr.Zero, "XfsConsole", 0, 1000, XFS_VER_303,
                           ref verSvcCdm, ref verSpiCdm, ref hCdm);
            Console.WriteLine("openSuccess");
            TryRegister("CashDispenser", hCdm);
            if (hCdm != 0)
            {
                Console.WriteLine("Cdm is Open ed");
            }

            // ——— IDC ———
            var verSvcIdc = new WFSVERSION();
            var verSpiIdc = new WFSVERSION();
            XfsApi.WFSOpen("CardReader", IntPtr.Zero, "XfsConsole", 0, 6000, XFS_VER_303,
                           ref verSvcIdc, ref verSpiIdc, ref hIdc);
            TryRegister("CardReader", hIdc);
            if (hIdc != 0)
            {
                Console.WriteLine($"idc: {hIdc}");
                Console.WriteLine("IDC is Open ed");
            }
            // ——— PTR ———
            var verSvcPtr = new WFSVERSION();
            var verSpiPtr = new WFSVERSION();
            XfsApi.WFSOpen("ReceiptPrinter", IntPtr.Zero, "XfsConsole", 0, 6000, XFS_VER_303,
                           ref verSvcPtr, ref verSpiPtr, ref hPtr);
            TryRegister("ReceiptPrinter", hPtr);
            if (hPtr != 0)
            {
                Console.WriteLine("printer is opened");
            }
            // ——— SIU ———
            var verSvcSiu = new WFSVERSION();
            var verSpiSiu = new WFSVERSION();
            XfsApi.WFSOpen("Sensors", IntPtr.Zero, "XfsConsole", 0, 6000, XFS_VER_303,
                           ref verSvcSiu, ref verSpiSiu, ref hSiu);
            TryRegister("Sensors", hSiu);
            if (hSiu != 0)
            {
                Console.WriteLine("Sensors is opened");
            }

       

            SafeLog("Installation is Done.");
        }

        private static void TryRegister(string logicalName, ushort hService)
        {
            if (hService == 0)
            {
                SafeLog($"[{logicalName}] WFSOpen returned 0 handle. Skip register.");
                return;
            }

            // گاهی Vendorها بلافاصله بعد از Open هنوز آماده ارسال پیام نیستند؛ کمی تاخیر کمک می‌کند
            Thread.Sleep(50);

            int hr = XfsApi.WFSRegister(hService, ALL_EVENTS, _evtWindow.Handle);
            if (hr != WFS_SUCCESS)
            {

            }
            else
            {
                SafeLog($"[{logicalName}] WFSRegister OK.");
            }
        }


        private static void SafeLog(string s)
        {
            Console.WriteLine(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "  " + s);
            try { }
            catch { /* هرگز کرش نکن */ }
        }
        public static void CloseAllModules()
        {
            lock (_sync)
            {
                try
                {
                    // 1) امن: WFSDeregister (اگر امضایش را داری؛ در غیر این صورت می‌توان صرف‌نظر کرد)
                    SafeDeregister("Camera", hCam);
                    SafeDeregister("Encryptor", hPin);
                    SafeDeregister("CashDispenser", hCdm);
                    SafeDeregister("CardReader", hIdc);
                    SafeDeregister("ReceiptPrinter", hPtr);
                    SafeDeregister("Sensors", hSiu);

                    // 2) Close همه سرویس‌ها
                    CloseService(ref hCam);
                    CloseService(ref hPin);
                    CloseService(ref hCdm);
                    CloseService(ref hIdc);
                    CloseService(ref hPtr);
                    CloseService(ref hSiu);
                }
                catch (Exception ex) { }
                finally
                {
                    // 3) CleanUp
                    try { XfsApi.WFSCleanUp(); } catch { }

                    // 4) پنجره پیام را ببند
                    try { if (_evtWindow != null) { _evtWindow.Dispose(); _evtWindow = null; } } catch { }

                    _opened = false;
                }
            }
        }

        private static void SafeDeregister(string logical, ushort hService)
        {
            try
            {
                if (hService != 0 && _evtWindow != null)
                {
                    //// اگر امضای WFSDeregister را نداری، این تابع را حذف کن.
                    XfsApi.WFSDeregister(hService, ALL_EVENTS, _evtWindow.Handle);
                }
            }
            catch { /* ignore */ }
        }

        private static void CloseService(ref ushort serviceHandle)
        {
            if (serviceHandle != 0)
            {
                try { XfsApi.WFSClose(serviceHandle); } catch { }
                serviceHandle = 0;
            }
        }


        ////////////////Ensure Open Service ///////////////////////
        public static bool EnsureCdmOpen()
        {
            lock (_sync)
            {
                if (hCdm != 0)
                    return true;

                var now = DateTime.Now;
                if (now - _lastCdmOpenAttempt < _openRetryInterval)
                    return false;

                _lastCdmOpenAttempt = now;

                var verSvc = new WFSVERSION();
                var verSpi = new WFSVERSION();
                ushort h =0;

                int hr = XfsApi.WFSOpen(
                    "CashDispenser",
                    IntPtr.Zero,
                    "XfsConsole",
                    0,
                    6000,
                    XFS_VER_303,
                    ref verSvc,
                    ref verSpi,
                    ref h);

                if (hr == 0 && h != 0)
                {
                    hCdm = h;
                    TryRegister("CashDispenser", hCdm);
                    Console.WriteLine("CDM re-opened successfully.");
                    return true;
                }

                Console.WriteLine($"Failed to open CDM, hr=0x{hr:X}");
                hCdm = 0;
                return false;
            }
        }

        public static bool EnsurePtrOpen()
        {
            lock (_sync)
            {
                if (hPtr !=0)
                    return true;

                var now = DateTime.Now;
                if (now - _lastPtrOpenAttempt < _openRetryInterval)
                    return false;

                _lastPtrOpenAttempt = now;

                var verSvc = new WFSVERSION();
                var verSpi = new WFSVERSION();
                ushort h = 0;

                int hr = XfsApi.WFSOpen(
                    "ReceiptPrinter",
                    IntPtr.Zero,
                    "XfsConsole",
                    0,
                    6000,
                    XFS_VER_303,
                    ref verSvc,
                    ref verSpi,
                    ref h);

                if (hr == 0 && h != 0)
                {
                    hPtr = h;
                    TryRegister("ReceiptPrinter", hPtr);
                    Console.WriteLine("PTR re-opened successfully.");
                    return true;
                }

                Console.WriteLine($"Failed to open PTR, hr=0x{hr:X}");
                hPtr = 0;
                return false;
            }
        }

        public static void InvalidatePtr()
        {
            lock (_sync)
            {
                if (hPtr != 0)
                {
                    try { XfsApi.WFSClose(hPtr); }
                    catch { /* ignore */ }
                    hPtr =0;
                }
            }
        }

        public static void InvalidateCdm()
        {
            lock (_sync)
            {
                if (hCdm != 0)
                {
                    try { XfsApi.WFSClose(hCdm); }
                    catch { /* ignore */ }
                    hCdm =0;
                }
            }
        }

        public static bool EnsureIdcOpen()
        {
            lock (_sync)
            {
                if (hIdc != 0)
                    return true;

                var now = DateTime.Now;
                if (now - _lastIdcOpenAttempt < _openRetryInterval)
                    return false;

                _lastIdcOpenAttempt = now;

 
                ushort h = 0;
                var verSvcIdc = new WFSVERSION();
                var verSpiIdc = new WFSVERSION();
                int hr = XfsApi.WFSOpen("CardReader", IntPtr.Zero, "XfsConsole", 0, 6000, XFS_VER_303,
                               ref verSvcIdc, ref verSpiIdc, ref h);
        

                if (hr == 0 && h != 0)
                {
                    hIdc = h;
                    TryRegister("CardReader", hIdc);
                    Console.WriteLine("Idc re-opened successfully.");
                    return true;
                }

                Console.WriteLine($"Failed to open Idc, hr=0x{hr:X}");
                hIdc = 0;
                return false;
            }
        }

        public static void InvalidateIdc()
        {
            lock (_sync)
            {
                if (hIdc != 0)
                {
                    try { XfsApi.WFSClose(hIdc); }
                    catch { }
                    hIdc = 0;
                }
            }
        }

        public static bool EnsureCameraOpen()
        {
            lock (_sync)
            {
                if (hCam != 0)
                    return true;

                var now = DateTime.Now;
                if (now - _lastcameraOpenAttempt < _openRetryInterval)
                    return false;

                _lastcameraOpenAttempt = now;


                ushort h = 0;
                var verSvcCam = new WFSVERSION();
                var verSpiCam = new WFSVERSION();
                int hr = XfsApi.WFSOpen("Camera", IntPtr.Zero, "XfsConsole", 0, 6000, XFS_VER_303,
                           ref verSvcCam, ref verSpiCam, ref h);


                if (hr == 0 && h != 0)
                {
                    hCam = h;
                    TryRegister("Camera", hCam);
                    Console.WriteLine("camera re-opened successfully.");
                    return true;
                }

                Console.WriteLine($"Failed to open Camera, hr=0x{hr:X}");
                hCam = 0;
                return false;
            }
        }

        public static void InvalidateCamera()
        {
            lock (_sync)
            {
                if (hCam != 0)
                {
                    try { XfsApi.WFSClose(hCam); }
                    catch { }
                    hCam = 0;
                }
            }
        }

        public static bool EnsureSensorOpen()
        {
            lock (_sync)
            {
                if (hSiu != 0)
                    return true;

                var now = DateTime.Now;
                if (now - _lastsiuOpenAttempt < _openRetryInterval)
                    return false;

                _lastsiuOpenAttempt = now;


                ushort h = 0;
                var verSvcSiu = new WFSVERSION();
                var verSpiSiu = new WFSVERSION();
             var hr=   XfsApi.WFSOpen("Sensors", IntPtr.Zero, "XfsConsole", 0, 6000, XFS_VER_303,
                               ref verSvcSiu, ref verSpiSiu, ref h);



                if (hr == 0 && h != 0)
                {
                    hSiu = h;
                    TryRegister("Sensors", hSiu);
                    Console.WriteLine("Sensors re-opened successfully.");
                    return true;
                }

                Console.WriteLine($"Failed to open Sensors, hr=0x{hr:X}");
                hSiu = 0;
                return false;
            }
        }

        public static void InvalidateSensors()
        {
            lock (_sync)
            {
                if (hSiu != 0)
                {
                    try { XfsApi.WFSClose(hSiu); }
                    catch { }
                    hSiu = 0;
                }
            }
        }

        public static bool EnsurePinOpen()
        {
            lock (_sync)
            {
                if (hPin != 0)
                    return true;

                var now = DateTime.Now;
                if (now - _lastpinOpenAttempt < _openRetryInterval)
                    return false;

                _lastpinOpenAttempt = now;


                ushort h = 0;
                var verSvcPin = new WFSVERSION();
                var verSpiPin = new WFSVERSION();
              var hr=  XfsApi.WFSOpen("Encryptor", IntPtr.Zero, "XfsConsole", 0, 6000, XFS_VER_303,
                               ref verSvcPin, ref verSpiPin, ref h);



                if (hr == 0 && h != 0)
                {
                    hPin = h;
                    TryRegister("Encryptor", hPin);
                    Console.WriteLine("Encryptor re-opened successfully.");
                    return true;
                }

                Console.WriteLine($"Failed to open Encryptor, hr=0x{hr:X}");
                hPin = 0;
                return false;
            }
        }

        public static void InvalidatePin()
        {
            lock (_sync)
            {
                if (hPin != 0)
                {
                    try { XfsApi.WFSClose(hPin); }
                    catch { }
                    hPin = 0;
                }
            }
        }
        public static bool IsFatalServiceError(int hResult)
        {
            switch (hResult)
            {
                case XfsErrors.WFS_ERR_INVALID_HSERVICE:
                case XfsErrors.WFS_ERR_CONNECTION_LOST:
                case XfsErrors.WFS_ERR_HARDWARE_ERROR:
                case XfsErrors.WFS_ERR_INTERNAL_ERROR:
                    return true;

                default:
                    return false;
            }
        }























    }
}
