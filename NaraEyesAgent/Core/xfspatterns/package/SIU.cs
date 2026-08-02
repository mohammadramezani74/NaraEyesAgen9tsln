using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace NaraEyesAgent.Core.XFSPatterns.package
{
    public static class SIU
    {
        public const int WFS_SERVICE_CLASS_SIU = 8;
        public const int SIU_SERVICE_OFFSET = (WFS_SERVICE_CLASS_SIU * 100);

        // Info
        public const int WFS_INF_SIU_STATUS = (SIU_SERVICE_OFFSET + 1);
        public const int WFS_INF_SIU_CAPABILITIES = (SIU_SERVICE_OFFSET + 2);

        // Commands
        public const int WFS_CMD_SIU_ENABLE_EVENTS = (SIU_SERVICE_OFFSET + 1);
        public const int WFS_CMD_SIU_SET_PORTS = (SIU_SERVICE_OFFSET + 2);
        public const int WFS_CMD_SIU_SET_DOOR = (SIU_SERVICE_OFFSET + 3);
        public const int WFS_CMD_SIU_SET_INDICATOR = (SIU_SERVICE_OFFSET + 4);
        public const int WFS_CMD_SIU_SET_AUXILIARY = (SIU_SERVICE_OFFSET + 5);
        public const int WFS_CMD_SIU_SET_GUIDLIGHT = (SIU_SERVICE_OFFSET + 6);
        public const int WFS_CMD_SIU_RESET = (SIU_SERVICE_OFFSET + 7);

        // Array sizes
        public const int WFS_SIU_SENSORS_SIZE = 32;
        public const int WFS_SIU_DOORS_SIZE = 16;
        public const int WFS_SIU_INDICATORS_SIZE = 16;
        public const int WFS_SIU_AUXILIARIES_SIZE = 16;
        public const int WFS_SIU_GUIDLIGHTS_SIZE = 16;

        // Indices (نمونه‌های متداول)
        public const int WFS_SIU_OPERATORSWITCH = 0; // Sensors
        public const int WFS_SIU_TAMPER = 1;
        public const int WFS_SIU_CARDUNIT = 0;       // Guidelights index set (برای SetGuidLight)
        public const int WFS_SIU_PINPAD = 1;
        public const int WFS_SIU_NOTESDISPENSER = 2;
        public const int WFS_SIU_RECEIPTPRINTER = 4;

        // Enable/Disable event flags
        public const ushort WFS_SIU_NO_CHANGE = 0x0000;
        public const ushort WFS_SIU_ENABLE_EVENT = 0x0001;
        public const ushort WFS_SIU_DISABLE_EVENT = 0x0002;

        // OperatorSwitch status bits
        public const ushort WFS_SIU_RUN = 0x0001;
        public const ushort WFS_SIU_MAINTENANCE = 0x0002;
        public const ushort WFS_SIU_SUPERVISOR = 0x0004;

        // Guidelight command/status (on/off/flash/continuous)
        public const ushort WFS_SIU_OFF = 0x0001;
        public const ushort WFS_SIU_ON = 0x0002;
        public const ushort WFS_SIU_SLOW_FLASH = 0x0004;
        public const ushort WFS_SIU_MEDIUM_FLASH = 0x0008;
        public const ushort WFS_SIU_QUICK_FLASH = 0x0010;
        public const ushort WFS_SIU_CONTINUOUS = 0x0080;
    }
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    struct WFSSIUSTATUS
    {
        public ushort fwDevice;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SIU.WFS_SIU_SENSORS_SIZE)]
        public ushort[] fwSensors;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SIU.WFS_SIU_DOORS_SIZE)]
        public ushort[] fwDoors;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SIU.WFS_SIU_INDICATORS_SIZE)]
        public ushort[] fwIndicators;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SIU.WFS_SIU_AUXILIARIES_SIZE)]
        public ushort[] fwAuxiliaries;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SIU.WFS_SIU_GUIDLIGHTS_SIZE)]
        public ushort[] fwGuidLights;
        public IntPtr lpszExtra; // MULTI-STRING (key=value)
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct WFSSIUCAPS
    {
        public ushort wClass;
        public ushort fwType;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SIU.WFS_SIU_SENSORS_SIZE)]
        public ushort[] fwSensors;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SIU.WFS_SIU_DOORS_SIZE)]
        public ushort[] fwDoors;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SIU.WFS_SIU_INDICATORS_SIZE)]
        public ushort[] fwIndicators;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SIU.WFS_SIU_AUXILIARIES_SIZE)]
        public ushort[] fwAuxiliaries;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SIU.WFS_SIU_GUIDLIGHTS_SIZE)]
        public ushort[] fwGuidLights;
        public IntPtr lpszExtra;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct WFSSIUENABLE
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SIU.WFS_SIU_SENSORS_SIZE)]
        public ushort[] fwSensors;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SIU.WFS_SIU_DOORS_SIZE)]
        public ushort[] fwDoors;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SIU.WFS_SIU_INDICATORS_SIZE)]
        public ushort[] fwIndicators;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SIU.WFS_SIU_AUXILIARIES_SIZE)]
        public ushort[] fwAuxiliaries;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SIU.WFS_SIU_GUIDLIGHTS_SIZE)]
        public ushort[] fwGuidLights;
        public IntPtr lpszExtra;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct WFSSIUSETGUIDLIGHT
    {
        public ushort wGuidLight;   // e.g. WFS_SIU_CARDUNIT
        public ushort fwCommand;    // e.g. WFS_SIU_ON / OFF / *_FLASH / CONTINUOUS
    }

    enum OperatorSwitchMode { Unknown = 0, Run, Maintenance, Supervisor }
}
