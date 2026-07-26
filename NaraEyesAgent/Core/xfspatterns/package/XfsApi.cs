using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace NaraEyesAgent.Core.XFSPatterns.package
{
    public static class XfsApi
    {
        // --- Add: define the blocking hook delegate (XFSBLOCKINGHOOK) ---
        // LONG CALLBACK Hook(DWORD dwTime, HWND hWnd, DWORD dwMsg, DWORD dwParam, LPVOID lpContext)
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        public delegate int XFSBLOCKINGHOOK(uint dwTime, IntPtr hWnd, uint dwMsg, uint dwParam, IntPtr lpContext);

        // -------------------- Core wfsapi.dll P/Invokes --------------------

        [DllImport(XFSConstants.LIBNAME, CharSet = XFSConstants.CHARSET, CallingConvention = XFSConstants.CALLINGCONVENTION)]
        public static extern int WFSCancelAsyncRequest(ushort hService, uint RequestID); // uint (REQUESTID)

        [DllImport(XFSConstants.LIBNAME, CharSet = XFSConstants.CHARSET, CallingConvention = XFSConstants.CALLINGCONVENTION)]
        public static extern int WFSCancelBlockingCall(int dwThreadID);

        [DllImport(XFSConstants.LIBNAME, CharSet = XFSConstants.CHARSET, CallingConvention = XFSConstants.CALLINGCONVENTION)]
        public static extern int WFSCleanUp();

        [DllImport(XFSConstants.LIBNAME, CharSet = XFSConstants.CHARSET, CallingConvention = XFSConstants.CALLINGCONVENTION)]
        public static extern int WFSClose(ushort hService);

        [DllImport(XFSConstants.LIBNAME, CharSet = XFSConstants.CHARSET, CallingConvention = XFSConstants.CALLINGCONVENTION)]
        public static extern int WFSAsyncClose(ushort hService, IntPtr hWnd, ref uint lpRequestID); // uint

        [DllImport(XFSConstants.LIBNAME, CharSet = XFSConstants.CHARSET, CallingConvention = XFSConstants.CALLINGCONVENTION)]
        public static extern int WFSCreateAppHandle(ref IntPtr lphApp);

        [DllImport(XFSConstants.LIBNAME, CharSet = XFSConstants.CHARSET, CallingConvention = XFSConstants.CALLINGCONVENTION)]
        public static extern int WFSDeregister(ushort hService, int dwEventClass, IntPtr hWndReg);

        [DllImport(XFSConstants.LIBNAME, CharSet = XFSConstants.CHARSET, CallingConvention = XFSConstants.CALLINGCONVENTION)]
        public static extern int WFSAsyncDeregister(ushort hService, int dwEventClass, IntPtr hWndReg, IntPtr hWnd, ref uint lpRequestID); // uint

        [DllImport(XFSConstants.LIBNAME, CharSet = XFSConstants.CHARSET, CallingConvention = XFSConstants.CALLINGCONVENTION)]
        public static extern int WFSDestroyAppHandle(IntPtr hApp);

        [DllImport(XFSConstants.LIBNAME, CharSet = XFSConstants.CHARSET, CallingConvention = XFSConstants.CALLINGCONVENTION)]
        public static extern int WFSExecute(ushort hService, int dwCommand, IntPtr lpCmdData, int dwTimeOut, ref IntPtr lppResult);

        [DllImport(XFSConstants.LIBNAME, CharSet = XFSConstants.CHARSET, CallingConvention = XFSConstants.CALLINGCONVENTION)]
        public static extern int WFSAsyncExecute(ushort hService, int dwCommand, IntPtr lpCmdData, int dwTimeOut, IntPtr hWnd,
            ref uint lpRequestID); // uint

        // بهتره FreeResult با IntPtr باشه نه ref WFSRESULT
        [DllImport(XFSConstants.LIBNAME, CharSet = XFSConstants.CHARSET, CallingConvention = XFSConstants.CALLINGCONVENTION)]
        public static extern int WFSFreeResult(IntPtr lpResult);

        [DllImport(XFSConstants.LIBNAME, CharSet = XFSConstants.CHARSET, CallingConvention = XFSConstants.CALLINGCONVENTION)]
        public static extern int WFSGetInfo(ushort hService, int dwCategory, IntPtr lpQueryDetails, int dwTimeOut, ref IntPtr lppResult);

        [DllImport(XFSConstants.LIBNAME, CharSet = XFSConstants.CHARSET, CallingConvention = XFSConstants.CALLINGCONVENTION)]
        public static extern int WFSAsyncGetInfo(ushort hService, int dwCategory, IntPtr lpQueryDetails, int dwTimeOut, IntPtr hWnd,
            ref uint lpRequestID); // uint

        [DllImport(XFSConstants.LIBNAME, CharSet = XFSConstants.CHARSET, CallingConvention = XFSConstants.CALLINGCONVENTION)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WFSIsBlocking();

        [DllImport(XFSConstants.LIBNAME, CharSet = XFSConstants.CHARSET, CallingConvention = XFSConstants.CALLINGCONVENTION)]
        public static extern int WFSLock(ushort hService, int dwTimeOut, ref IntPtr lppResult);

        [DllImport(XFSConstants.LIBNAME, CharSet = XFSConstants.CHARSET, CallingConvention = XFSConstants.CALLINGCONVENTION)]
        public static extern int WFSAsyncLock(ushort hService, int dwTimeOut, IntPtr hWnd, ref uint lpRequestID); // uint

        [DllImport(XFSConstants.LIBNAME, CharSet = XFSConstants.CHARSET, CallingConvention = XFSConstants.CALLINGCONVENTION)]
        public static extern int WFSOpen(string lpszLogicalName, IntPtr hApp, string lpszAppID, int dwTraceLevel, int dwTimeOut,
            int dwSrvcVersionsRequired,ref WFSVERSION lpSrvcVersion,ref WFSVERSION lpSPIVersion, ref ushort lphService);

        [DllImport(XFSConstants.LIBNAME, CharSet = XFSConstants.CHARSET, CallingConvention = XFSConstants.CALLINGCONVENTION)]
        public static extern int WFSAsyncOpen(string lpszLogicalName, IntPtr hApp, string lpszAppID, int dwTraceLevel, int dwTimeOut,
            ref ushort lphService, IntPtr hWnd, int dwSrvcVersionsRequired, ref WFSVERSION lpSrvcVersion,ref WFSVERSION lpSPIVersion,
            ref uint lpRequestID); // uint

        [DllImport(XFSConstants.LIBNAME, CharSet = XFSConstants.CHARSET, CallingConvention = XFSConstants.CALLINGCONVENTION)]
        public static extern int WFSRegister(ushort hService, int dwEventClass, IntPtr hWndReg);

        [DllImport(XFSConstants.LIBNAME, CharSet = XFSConstants.CHARSET, CallingConvention = XFSConstants.CALLINGCONVENTION)]
        public static extern int WFSAsyncRegister(ushort hService, int dwEventClass, IntPtr hWndReg, IntPtr hWnd, ref uint lpRequestID); // uint

        [DllImport(XFSConstants.LIBNAME, CharSet = XFSConstants.CHARSET, CallingConvention = XFSConstants.CALLINGCONVENTION)]
        public static extern int WFSSetBlockingHook(XFSBLOCKINGHOOK lpBlockFunc, out IntPtr lppPrevFunc);

        [DllImport(XFSConstants.LIBNAME, CharSet = XFSConstants.CHARSET, CallingConvention = XFSConstants.CALLINGCONVENTION)]
        public static extern int WFSStartUp(int dwVersionsRequired,ref WFSVERSION lpWFSVersion);

        [DllImport(XFSConstants.LIBNAME, CharSet = XFSConstants.CHARSET, CallingConvention = XFSConstants.CALLINGCONVENTION)]
        public static extern int WFSUnhookBlockingHook();

        [DllImport(XFSConstants.LIBNAME, CharSet = XFSConstants.CHARSET, CallingConvention = XFSConstants.CALLINGCONVENTION)]
        public static extern int WFSUnlock(ushort hService);

        [DllImport(XFSConstants.LIBNAME, CharSet = XFSConstants.CHARSET, CallingConvention = XFSConstants.CALLINGCONVENTION)]
        public static extern int WFSAsyncUnlock(ushort hService, IntPtr hWnd, ref uint lpRequestID); // uint

        [DllImport(XFSConstants.LIBNAME, CharSet = XFSConstants.CHARSET, CallingConvention = XFSConstants.CALLINGCONVENTION)]
        public static extern int WFMSetTraceLevel(ushort hService, int dwTraceLevel);
    }
}
