using System;
using System.Runtime.InteropServices;

namespace NaraEyesAgent.Core.XFSPatterns.package
{
    public static class PTR
    {
        public const int WFS_SERVICE_CLASS_PTR = 1;
        public const int PTR_SERVICE_OFFSET = WFS_SERVICE_CLASS_PTR * 100; // 100
        public const int WFS_INF_PTR_STATUS = PTR_SERVICE_OFFSET + 1;      // 101
        public const int WFS_CMD_PTR_RESET = PTR_SERVICE_OFFSET + 8;      // 108

        // ----- WFSPTRSTATUS -----
        // Ref: WFSPTRSTATUS fields (fwDevice, fwMedia, fwPaper[16], fwToner, fwInk, fwLamp, lppRetractBins, usMediaOnStacker, lpszExtra)
        // CWA 14050-3 (Printer) spec
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct WFSPTRSTATUS
        {
            public ushort fwDevice;
            public ushort fwMedia;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = WFS_PTR_SUPPLYSIZE)]
            public ushort[] fwPaper;        // size 16

            public ushort fwToner;
            public ushort fwInk;
            public ushort fwLamp;

            public IntPtr lppRetractBins;   // LPWFSPTRRETRACTBINS* (optional, not parsed here)
            public ushort usMediaOnStacker;
            public IntPtr lpszExtra;        // LPSTR
        }

        // ----- WFSPTRRESET (input to WFS_CMD_PTR_RESET) -----
        // Ref: WFSPTRRESET has dwMediaControl + usRetractBinNumber
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct WFSPTRRESET
        {
            public uint dwMediaControl;     // bitmask of WFS_PTR_CTRL*
            public ushort usRetractBinNumber;
        }

        // ----- fwDevice (reuse generic WFS_STAT_* via mapping in Program) -----

        // ----- fwMedia values -----
        public const ushort WFS_PTR_MEDIAPRESENT = 0;
        public const ushort WFS_PTR_MEDIANOTPRESENT = 1;
        public const ushort WFS_PTR_MEDIAJAMMED = 2;
        public const ushort WFS_PTR_MEDIANOTSUPP = 3;
        public const ushort WFS_PTR_MEDIAUNKNOWN = 4;
        public const ushort WFS_PTR_MEDIAENTERING = 5;
        public const ushort WFS_PTR_MEDIARETRACTED = 6;

        // ----- fwPaper array size & indices -----
        public const int WFS_PTR_SUPPLYSIZE = 16;
        public const int WFS_PTR_SUPPLYUPPER = 0;
        public const int WFS_PTR_SUPPLYLOWER = 1;
        public const int WFS_PTR_SUPPLYEXTERNAL = 2;
        public const int WFS_PTR_SUPPLYAUX = 3;
        public const int WFS_PTR_SUPPLYAUX2 = 4;
        public const int WFS_PTR_SUPPLYPARK = 5;

        // ----- fwPaper values -----
        public const ushort WFS_PTR_PAPERFULL = 0;
        public const ushort WFS_PTR_PAPERLOW = 1;
        public const ushort WFS_PTR_PAPEROUT = 2;
        public const ushort WFS_PTR_PAPERNOTSUPP = 3;
        public const ushort WFS_PTR_PAPERUNKNOWN = 4;
        public const ushort WFS_PTR_PAPERJAMMED = 5;

        // ----- fwToner values -----
        public const ushort WFS_PTR_TONERFULL = 0;
        public const ushort WFS_PTR_TONERLOW = 1;
        public const ushort WFS_PTR_TONEROUT = 2;
        public const ushort WFS_PTR_TONERNOTSUPP = 3;
        public const ushort WFS_PTR_TONERUNKNOWN = 4;

        // ----- fwInk values -----
        public const ushort WFS_PTR_INKFULL = 0;
        public const ushort WFS_PTR_INKLOW = 1;
        public const ushort WFS_PTR_INKOUT = 2;
        public const ushort WFS_PTR_INKNOTSUPP = 3;
        public const ushort WFS_PTR_INKUNKNOWN = 4;

        // ----- fwLamp values -----
        public const ushort WFS_PTR_LAMPOK = 0;
        public const ushort WFS_PTR_LAMPFADING = 1;
        public const ushort WFS_PTR_LAMPINOP = 2;
        public const ushort WFS_PTR_LAMPNOTSUPP = 3;
        public const ushort WFS_PTR_LAMPUNKNOWN = 4;

        // ----- dwMediaControl flags (for RESET / CONTROL_MEDIA) -----
        public const uint WFS_PTR_CTRLEJECT = 0x0001;
        public const uint WFS_PTR_CTRLPERFORATE = 0x0002;
        public const uint WFS_PTR_CTRLCUT = 0x0004;
        public const uint WFS_PTR_CTRLSKIP = 0x0008;
        public const uint WFS_PTR_CTRLFLUSH = 0x0010;
        public const uint WFS_PTR_CTRLRETRACT = 0x0020;
        public const uint WFS_PTR_CTRLSTACK = 0x0040;
        public const uint WFS_PTR_CTRLPARTIALCUT = 0x0080;
        public const uint WFS_PTR_CTRLALARM = 0x0100;
        public const uint WFS_PTR_CTRLATPFORWARD = 0x0200;
        public const uint WFS_PTR_CTRLATPBACKWARD = 0x0400;
        public const uint WFS_PTR_CTRLTURNMEDIA = 0x0800;
        public const uint WFS_PTR_CTRLSTAMP = 0x1000;
        public const uint WFS_PTR_CTRLPARK = 0x2000;
    }
}
