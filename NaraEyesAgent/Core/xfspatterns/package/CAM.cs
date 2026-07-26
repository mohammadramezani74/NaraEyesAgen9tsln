using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace NaraEyesAgent.Core.XFSPatterns.package
{
  
        public static class CAM
        {
            // ===== Service & category/command IDs (CEN/XFS CAM) =====
            public const int WFS_SERVICE_CLASS_CAM = 10;
            public const int CAM_SERVICE_OFFSET = WFS_SERVICE_CLASS_CAM * 100;

            // Info
            public const int WFS_INF_CAM_STATUS = CAM_SERVICE_OFFSET + 1;
            public const int WFS_INF_CAM_CAPABILITIES = CAM_SERVICE_OFFSET + 2;

            // Execute
            public const int WFS_CMD_CAM_TAKE_PICTURE = CAM_SERVICE_OFFSET + 1;
            public const int WFS_CMD_CAM_RESET = CAM_SERVICE_OFFSET + 2;
            public const int WFS_CMD_CAM_TAKE_PICTURE_EX = CAM_SERVICE_OFFSET + 3;
            public const int WFS_CMD_CAM_SYNCHRONIZE_COMMAND = CAM_SERVICE_OFFSET + 4;

            // ===== Indices/Enums =====
            public const int WFS_CAM_CAMERAS_SIZE = 8;
            public const int WFS_CAM_CAMERAS_MAX = WFS_CAM_CAMERAS_SIZE - 1;

            // Camera indexes
            public const ushort WFS_CAM_ROOM = 0;
            public const ushort WFS_CAM_PERSON = 1;
            public const ushort WFS_CAM_EXITSLOT = 2;

            // Media states
            public const ushort WFS_CAM_MEDIAOK = 0;
            public const ushort WFS_CAM_MEDIAHIGH = 1;
            public const ushort WFS_CAM_MEDIAFULL = 2;
            public const ushort WFS_CAM_MEDIAUNKNOWN = 3;
            public const ushort WFS_CAM_MEDIANOTSUPP = 4;

            // Camera states
            public const ushort WFS_CAM_CAMNOTSUPP = 0;
            public const ushort WFS_CAM_CAMOK = 1;
            public const ushort WFS_CAM_CAMINOP = 2;
            public const ushort WFS_CAM_CAMUNKNOWN = 3;

            // Caps: type/camData/char support
            public const ushort WFS_CAM_TYPE_CAM = 1;
            public const ushort WFS_CAM_NOTADD = 0;
            public const ushort WFS_CAM_AUTOADD = 1;
            public const ushort WFS_CAM_MANADD = 2;
            public const ushort WFS_CAM_ASCII = 0x0001;
            public const ushort WFS_CAM_UNICODE = 0x0002;

            // Anti-Fraud Module (AFM)
            public const ushort WFS_CAM_AFMNOTSUPP = 0;
            public const ushort WFS_CAM_AFMOK = 1;
            public const ushort WFS_CAM_AFMINOP = 2;
            public const ushort WFS_CAM_AFMDEVICEDETECTED = 3;
            public const ushort WFS_CAM_AFMUNKNOWN = 4;

            // ===== Structures (Pack=1) =====
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct WFSCAMSTATUS
            {
                public ushort fwDevice;

                [MarshalAs(UnmanagedType.ByValArray, SizeConst = WFS_CAM_CAMERAS_SIZE)]
                public ushort[] fwMedia;

                [MarshalAs(UnmanagedType.ByValArray, SizeConst = WFS_CAM_CAMERAS_SIZE)]
                public ushort[] fwCameras;

                [MarshalAs(UnmanagedType.ByValArray, SizeConst = WFS_CAM_CAMERAS_SIZE)]
                public ushort[] usPictures;

                public IntPtr lpszExtra;
                public ushort wAntiFraudModule;
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct WFSCAMCAPS
            {
                public ushort wClass;
                public ushort fwType;

                [MarshalAs(UnmanagedType.ByValArray, SizeConst = WFS_CAM_CAMERAS_SIZE)]
                public ushort[] fwCameras;

                public ushort usMaxPictures;
                public ushort fwCamData;          // NOTADD/AUTOADD/MANADD
                public ushort usCamDataLength;
                public ushort fwCharSupport;      // bitmask (ASCII/UNICODE)
                public IntPtr lpszExtra;          // MULTI-STRING
                public int bPictureFile;          // BOOL (use int for marshalling)
                public int bAntiFraudModule;      // BOOL
                public IntPtr lpdwSynchronizableCommands; // LPDWORD (optional)
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct WFSCAMTAKEPICT
            {
                public ushort wCamera;            // ROOM/PERSON/EXITSLOT
                public IntPtr lpszCamData;        // ANSI (optional)
                public IntPtr lpszUNICODECamData; // Unicode (optional, mutually exclusive)
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct WFSCAMTAKEPICTEX
            {
                public ushort wCamera;
                public IntPtr lpszCamData;        // ANSI (optional)
                public IntPtr lpszUNICODECamData; // Unicode (optional)
                public IntPtr lpszPictureFile;    // ANSI full path (optional)
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct WFSCAMSYNCHRONIZECOMMAND
            {
                public uint dwCommand;
                public IntPtr lpCmdData;
            }

        }
    }

