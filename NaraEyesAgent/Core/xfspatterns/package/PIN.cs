using System.Runtime.InteropServices;
using System;
using NaraEyesAgent.Core.XFSPatterns.package;


public static class PIN
{
    public const int WFS_SERVICE_CLASS_PIN = 4;
    public const int PIN_SERVICE_OFFSET = WFS_SERVICE_CLASS_PIN * 100;

    // Info
    public const int WFS_INF_PIN_STATUS = PIN_SERVICE_OFFSET + 1; // 401

    // ساختار صحیح طبق سند (Pack و CharSet مطابق XFS)
    [StructLayout(LayoutKind.Sequential, Pack = XFSConstants.STRUCTPACKSIZE, CharSet = XFSConstants.CHARSET)]
    public struct WFSPINSTATUS
    {
        public ushort fwDevice;     // وضعیت دستگاه
        public ushort fwEncStat;    // وضعیت ماژول رمزنگاری (ENC state)
        public IntPtr lpszExtra;    // MULTI-STRING key=value
    }
}