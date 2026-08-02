using System;
using System.Runtime.InteropServices;

namespace NaraEyesAgent.Core.XFSPatterns.package
{
    public static class CDM
    {
        // --- CDM constants (xfscdm.h) ---
        public const int WFS_SERVICE_CLASS_CDM = 3;
        private const int CDM_BASE = WFS_SERVICE_CLASS_CDM * 100;

        // GetInfo categories
        public const int WFS_INF_CDM_STATUS = CDM_BASE + 1;
        public const int WFS_INF_CDM_CASH_UNIT_INFO = CDM_BASE + 3;

        // Execute commands (standard)
        public const int WFS_CMD_CDM_RESET = CDM_BASE + 21;

        // Execute commands (calibration – vendor dependent)
        // NOTE: شمارهٔ دقیق ممکن است در SDK سازنده متفاوت باشد. اگر مستند شما عدد خاصی دارد همان را جایگزین کنید.
        public const int WFS_CMD_CDM_CALIBRATE_CASH_UNIT = CDM_BASE + 101;

        // Optional/typical calibration actions (در برخی SPها استفاده می‌شود)
        public const uint WFS_CDM_CALIBRATE_START = 0x00000001;
        public const uint WFS_CDM_CALIBRATE_ABORT = 0x00000002;
        public const uint WFS_CDM_CALIBRATE_RESET = 0x00000004;

        // --- CDM structs (Pack و CharSet از XFSConstants) ---

        [StructLayout(LayoutKind.Sequential, Pack = XFSConstants.STRUCTPACKSIZE, CharSet = XFSConstants.CHARSET)]
        public struct WFSCDMSTATUS
        {
            public ushort fwDevice;
            public ushort fwSafeDoor;
            public ushort fwDispenser;
            public ushort fwIntermediateStacker;
            public IntPtr lppPositions;   // LPWFSCDMOUTPOS*
            public IntPtr lpszExtra;      // LPSTR
            //[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            //public uint[] dwGuidLights;
            //public ushort wDevicePosition;
            //public ushort usPowerSaveRecoveryTime;
            //public ushort wAntiFraudModule;
        }

        [StructLayout(LayoutKind.Sequential, Pack = XFSConstants.STRUCTPACKSIZE, CharSet = XFSConstants.CHARSET)]
        public struct WFSCDMPHCU
        {
            // NOTE: در برخی هدرها نام فیلد lpszPhysicalPositionName است؛ در خیلی از پیاده‌سازی‌ها lpPhysicalPositionName.
            // در این پروژه از lpPhysicalPositionName استفاده می‌کنیم و در کد اصلی با PtrToAnsi می‌خوانیم.
            public IntPtr lpPhysicalPositionName; // LPSTR

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
            public byte[] cUnitID;

            public uint ulInitialCount;
            public uint ulCount;
            public uint ulRejectCount;
            public uint ulMaximum;

            public ushort usPStatus;

            [MarshalAs(UnmanagedType.Bool)]
            public bool bHardwareSensor;

            //public uint ulDispensedCount;
            //public uint ulPresentedCount;
            //public uint ulRetractedCount;
        }

        [StructLayout(LayoutKind.Sequential, Pack = XFSConstants.STRUCTPACKSIZE, CharSet = XFSConstants.CHARSET)]
        public struct WFSCDMCASHUNIT
        {
            public ushort usNumber;                 // شمارهٔ Logical CU
            public ushort usType;
            public IntPtr lpszCashUnitName;         // LPSTR

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
            public byte[] cUnitID;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public byte[] cCurrencyID;

            public uint ulValues;
            public uint ulInitialCount;
            public uint ulCount;
            public uint ulRejectCount;
            public uint ulMinimum;
            public uint ulMaximum;

            [MarshalAs(UnmanagedType.Bool)]
            public bool bAppLock;

            public ushort usStatus;
            public ushort usNumPhysicalCUs;
            public IntPtr lppPhysical;              // WFSCDMPHCU**

            //public uint ulDispensedCount;
            //public uint ulPresentedCount;
            //public uint ulRetractedCount;
        }

        [StructLayout(LayoutKind.Sequential, Pack = XFSConstants.STRUCTPACKSIZE, CharSet = XFSConstants.CHARSET)]
        public struct WFSCDMCUINFO
        {
            public ushort usTellerID;
            public ushort usCount;
            public IntPtr lppList;                  // WFSCDMCASHUNIT**
        }

        // --- Calibration input (vendor-dependent) ---
        // این ساختار «حداقلی» است که در اکثر SPهای Vendor-based جواب می‌دهد:
        //  - usNumber: شماره Logical Cash Unit برای کالیبراسیون
        //  - dwAction: نوع عملیات (START/ABORT/RESET). اگر SP لازم ندارد 0 بدهید.
        //  - lpszExtra: فیلد توسعه‌پذیر (MULTI-STRING)
        [StructLayout(LayoutKind.Sequential, Pack = XFSConstants.STRUCTPACKSIZE, CharSet = XFSConstants.CHARSET)]
        public struct WFSCDMCALIBRATE
        {
            public ushort usNumber;       // Logical CU number
            public uint dwAction;         // یکی از WFS_CDM_CALIBRATE_* (در صورت نیاز؛ در غیر اینصورت 0)
            public IntPtr lpszExtra;      // LPSTR (MULTI-STRING or null)
        }
    }
}
