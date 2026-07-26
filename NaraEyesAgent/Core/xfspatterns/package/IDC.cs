using NaraEyesAgent.Core.XFSServices;
using System;
using System.Runtime.InteropServices;
using static NaraEyesAgent.Core.XFSPatterns.package.CDM;

namespace NaraEyesAgent.Core.XFSPatterns.package
{
    /// <summary>
    /// XFS IDC (Card Reader) constants & helpers
    /// </summary>
    public static class IDC
    {
        // ---- Service class & offsets (per xfsidc.h) ----
        public const int WFS_SERVICE_CLASS_IDC = 2;                // IDC
        public const int IDC_SERVICE_OFFSET = WFS_SERVICE_CLASS_IDC * 100;

        // ---- GetInfo categories ----
        public const int WFS_INF_IDC_STATUS = IDC_SERVICE_OFFSET + 1;  // 201
        public const int WFS_INF_IDC_CAPABILITIES = IDC_SERVICE_OFFSET + 2;  // 202
        // (در صورت نیاز: فرم‌ها/مدیا/… را هم می‌توان اضافه کرد)

        // ---- Execute commands ----
        public const int WFS_CMD_IDC_EJECT_CARD = IDC_SERVICE_OFFSET + 3;  // 203
        public const int WFS_CMD_IDC_RETAIN_CARD = IDC_SERVICE_OFFSET + 4;  // 204
        public const int WFS_CMD_IDC_RESET_COUNT = IDC_SERVICE_OFFSET + 5;  // 205
        public const int WFS_CMD_IDC_SETKEY = IDC_SERVICE_OFFSET + 6;  // 206
        public const int WFS_CMD_IDC_READ_RAW_DATA = IDC_SERVICE_OFFSET + 7;  // 207
        public const int WFS_CMD_IDC_RESET = IDC_SERVICE_OFFSET + 10; // 210

        // ---- RESET action values (LPWORD) ----
        public const ushort WFS_IDC_NOACTION = 1; // فقط ری‌اینیت داخلی؛ حرکت کارت صورت نمی‌گیرد
        public const ushort WFS_IDC_EJECT = 2; // اگر کارتی هست، درب خروج
        public const ushort WFS_IDC_RETAIN = 3; // اگر کارتی هست، ورود به retain bin
        public const ushort WFS_IDC_EJECTTHENRETAIN = 4; // تلاش برای eject؛ اگر نشد retain
        public const ushort WFS_IDC_READPOSITION = 5; // حرکت کارت به موقعیت خواندن (اگر معنی‌دار باشد)

        // ---- Status structure (WFSIDCSTATUS) ----
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct WFSIDCSTATUS
        {
            public ushort fwDevice;       // WFS_STAT_DEV*
            public ushort fwMedia;        // WFS_IDC_MEDIA*
            public ushort fwRetainBin;    // WFS_IDC_RETAINBIN*
            public ushort fwSecurity;     // WFS_IDC_SEC*
            public ushort usCards;        // تعداد کارت‌های retained از آخرین reset_count
            public ushort fwChipPower;    // WFS_IDC_CHIPPOW* (اگر دستگاه chip دارد)
            public IntPtr lpszExtra;      // LPSTR (key=value list)
        }

        /// <summary>
        /// Synchronous RESET helper with lock/unlock & proper LPWORD marshalling.
        /// </summary>
        /// <param name="hService">IDC service handle from WFSOpen</param>
        /// <param name="action">One of WFS_IDC_* action constants</param>
        /// <param name="timeoutMs">execute timeout (default 60s)</param>
        public static void Reset(ushort hService, ushort action, int timeoutMs = 60000)
        {
            // 1) Lock (recommended for state-changing ops)
            IntPtr pLock = IntPtr.Zero;
            int hrLock = XfsApi.WFSLock(hService, 15000, ref pLock);
            if (hrLock == XFSDefinition.WFS_SUCCESS && pLock != IntPtr.Zero)
                XfsApi.WFSFreeResult(pLock);

            IntPtr pIn = IntPtr.Zero;
            IntPtr pRes = IntPtr.Zero;
            try
            {
                // 2) Prepare LPWORD input
                pIn = Marshal.AllocHGlobal(sizeof(ushort));
                Marshal.WriteInt16(pIn, unchecked((short)action));

                // 3) Execute RESET
                int hr = XfsApi.WFSExecute(hService, WFS_CMD_IDC_RESET, pIn, timeoutMs, ref pRes);
                if (hr != XFSDefinition.WFS_SUCCESS)
                    throw new Exception($"WFSExecute(IDC_RESET) call failed hr=0x{hr:X}");

                // 4) Check result
                WFSRESULT res = (WFSRESULT)Marshal.PtrToStructure(pRes, typeof(WFSRESULT));
                if (res.hResult != XFSDefinition.WFS_SUCCESS)
                    throw new Exception($"IDC_RESET failed: 0x{res.hResult:X}");
            }
            finally
            {
                if (pRes != IntPtr.Zero) XfsApi.WFSFreeResult(pRes);
                if (pIn != IntPtr.Zero) Marshal.FreeHGlobal(pIn);
                // 5) Unlock
                XfsApi.WFSUnlock(hService);
            }
        }

        /// <summary>
        /// Simple status fetcher (optional utility).
        /// </summary>
        public static bool TryGetStatus(ushort hService, out WFSIDCSTATUS status, int timeoutMs = 10000)
        {
            IntPtr pRes = IntPtr.Zero;  try
            {
            int hr = XfsApi.WFSGetInfo(hService, WFS_INF_IDC_STATUS, IntPtr.Zero, timeoutMs, ref pRes);
            if (hr != XFSDefinition.WFS_SUCCESS)
            {
             
                    status = default;
                return false;
            }

          
                WFSRESULT res = (WFSRESULT)Marshal.PtrToStructure(pRes, typeof(WFSRESULT));
                if (res.hResult != XFSDefinition.WFS_SUCCESS)
                {
                    if (OpenModuleService.IsFatalServiceError(res.hResult))
                    {
                       
                        OpenModuleService.InvalidateIdc();
                    }
                    status = default;
                    return false;
                }
                status = (WFSIDCSTATUS)Marshal.PtrToStructure(res.lpBuffer, typeof(WFSIDCSTATUS));
                return true;
            }
            finally
            {
                if (pRes != IntPtr.Zero) XfsApi.WFSFreeResult(pRes);
            }
        }
    }
}
