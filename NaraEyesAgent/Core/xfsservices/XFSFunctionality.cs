using NaraEyesAgent.Common.IpHelper;
using NaraEyesAgent.Core.models;
using NaraEyesAgent.Core.models.Module;
using NaraEyesAgent.Core.XFSPatterns.package;
using NaraEyesAgent.infrastructure.Denomination;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Windows.Forms.Design;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace NaraEyesAgent.Core.XFSServices
{
    public static class XFSFunctionality
    {
        const int WFS_STAT_DEVONLINE = 0;
        const int WFS_STAT_DEVOFFLINE = 1;
        const int WFS_STAT_DEVPOWEROFF = 2;
        const int WFS_STAT_DEVNODEVICE = 3;
        const int WFS_STAT_DEVHWERROR = 4;
        const int WFS_STAT_DEVUSERERROR = 5;
        const int WFS_STAT_DEVBUSY = 6;
        const int WFS_STAT_DEVFRAUDATTEMPT = 7;
        const int WFS_STAT_DEVPOTENTIALFRAUD = 8;
        public const int WFS_SUCCESS = 0;
        // TODO: انتقال به Config.txt
        private const string HostCheckIp = "10.119.254.69";
        private const int HostCheckPort = 8001;
        private const long MoneyWarningThreshold = 20_000_000;
        public static void ResteCdm()
        {
            var cdm = OpenModuleService.hCdm;

            if (cdm == 0)
            {
                Console.WriteLine("[CDM] هندل باز نیست — ریست لغو شد.");
                return;
            }

            IntPtr pLock = IntPtr.Zero;
            int hrLock = XfsApi.WFSLock(cdm, 15000, ref pLock);

            if (pLock != IntPtr.Zero)
            {
                try { XfsApi.WFSFreeResult(pLock); } catch { }
                pLock = IntPtr.Zero;
            }

            if (hrLock != WFS_SUCCESS)
            {
                Console.WriteLine($"[CDM] قفل گرفته نشد hr={hrLock}" +
                    (hrLock == -32 ? " (در اختیار اپلیکیشن دیگری) — ریست لغو شد." : " — ریست لغو شد."));
                return;
            }

            IntPtr pRes = IntPtr.Zero;
            try
            {
                int hr = XfsApi.WFSExecute(cdm, CDM.WFS_CMD_CDM_RESET, IntPtr.Zero, 60000, ref pRes);
                Console.WriteLine(hr == WFS_SUCCESS ? "[CDM] RESET موفق." : $"[CDM] RESET ناموفق hr={hr}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CDM] RESET استثنا: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                if (pRes != IntPtr.Zero) { try { XfsApi.WFSFreeResult(pRes); } catch { } }
                try { XfsApi.WFSUnlock(cdm); } catch { }
            }
        }
        public static void ReseteIDC()
        {
            try
            {
                ushort action = IDC.WFS_IDC_NOACTION;
                if (ushort.TryParse("2", out var a) && a >= 1 && a <= 5)
                    action = a;
                Console.WriteLine(OpenModuleService.hIdc);
                IDC.Reset(OpenModuleService.hIdc, action);
                Console.WriteLine("IDC RESET: WFS_SUCCESS");
            }
            catch (Exception)
            {

                Console.WriteLine(OpenModuleService.hIdc);
                Console.WriteLine("IDC RESET Failed");
            }
        }
        public static void Resetptr()
        {
            uint ctrl = 0;
            ushort bin = 0;
            ctrl = PTR.WFS_PTR_CTRLEJECT;
            var hptr = OpenModuleService.hPtr;

            IntPtr pLock = IntPtr.Zero;
            int hrLock = XfsApi.WFSLock(hptr, 15000, ref pLock);
            if (pLock != IntPtr.Zero) { try { XfsApi.WFSFreeResult(pLock); } catch { } }

            if (hrLock != WFS_SUCCESS)
            {
                Console.WriteLine($"[PTR] قفل گرفته نشد hr={hrLock} — ریست لغو شد.");
                return;
            }

            // Build WFSPTRRESET
            var reset = new PTR.WFSPTRRESET { dwMediaControl = ctrl, usRetractBinNumber = bin };
            IntPtr pReset = IntPtr.Zero;
            IntPtr pRes = IntPtr.Zero;

            try
            {
                int sz = Marshal.SizeOf(reset);
                pReset = Marshal.AllocHGlobal(sz);
                Marshal.StructureToPtr(reset, pReset, false);

                int hr = XfsApi.WFSExecute(hptr, PTR.WFS_CMD_PTR_RESET, pReset, 60000, ref pRes);
                if (hr != WFS_SUCCESS) throw new Exception($"WFSExecute(PTR_RESET) failed hr=0x{hr:X}");

                //var res = Marshal.PtrToStructure<WFSRESULT>(pRes);
                //Console.WriteLine(res.hResult == WFS_SUCCESS ? "PTR RESET: WFS_SUCCESS" : $"PTR RESET failed: 0x{res.hResult:X}");
            }
            catch(Exception e) 
            {
                Console.WriteLine("reset Failed");
            }
            finally
            {
                if (pRes != IntPtr.Zero) XfsApi.WFSFreeResult(pRes);
                if (pReset != IntPtr.Zero) Marshal.FreeHGlobal(pReset);
                XfsApi.WFSUnlock(hptr);
            }
        }
        public static DeviceMuduleStatusCommand GetCassetInfo()
        {
            bool HaveError = false;
            bool PinEror = false;
            bool InService = false;
            bool online = false;
            bool offline = false;

            var command = new DeviceMuduleStatusCommand();

            // پیش‌فرض‌های امن — تا هیچ‌جای متد NullReference نگیریم
            command.CdmStatus = new CdmStatusDto { Device = 0, Dispenser = 0, IntermediateStacker = 0, SafeDoor = 0 };
            command.IdcStatus = new models.Module.IdcStatusDto { Device = 0, ChipPower = 0, Media = 0, RetainBin = 0, usCards = 0 };
            command.ptrStatus = new PtrStatusDto { Device = 0, Media = 0, Ink = 0, Toner = 0, Paper = PaperStatus.Unknown };
            command.PinStatus = new PinStatusDto { Device = 0 };
            command.SiuStatus = new SiuStatusModel { Device = 0, Doors = new ushort[0], Auxiliaries = new ushort[0], GuidLights = new ushort[0], Indicators = new ushort[0] };
            command.Cashunit = new List<CashUnitInfo>();

            int hr;

            // ==================== CDM STATUS ====================
            if (OpenModuleService.EnsureCdmOpen())
            {
                CDM.WFSCDMSTATUS st;
                if (TryGetInfo(OpenModuleService.hCdm, CDM.WFS_INF_CDM_STATUS, 10000, "CDM", out st, out hr))
                {
                    command.CdmStatus = new CdmStatusDto
                    {
                        Device = st.fwDevice,
                        Dispenser = st.fwDispenser,
                        IntermediateStacker = st.fwIntermediateStacker,
                        SafeDoor = st.fwSafeDoor,
                    };
                    if (st.fwDevice != 0 && st.fwDevice != 6) HaveError = true;
                }
                else if (OpenModuleService.IsFatalServiceError(hr))
                {
                    OpenModuleService.InvalidateCdm();
                }
            }

            // ==================== CDM CASH UNITS ====================
            if (OpenModuleService.EnsureCdmOpen())
            {
                IntPtr pRes = IntPtr.Zero;
                try
                {
                    hr = XfsApi.WFSGetInfo(OpenModuleService.hCdm, CDM.WFS_INF_CDM_CASH_UNIT_INFO,
                                           IntPtr.Zero, 20000, ref pRes);

                    if (hr != XfsErrors.WFS_SUCCESS || pRes == IntPtr.Zero)
                    {
                        Console.WriteLine($"[CDM_CU] WFSGetInfo failed hr={hr}");
                        if (OpenModuleService.IsFatalServiceError(hr))
                            OpenModuleService.InvalidateCdm();
                    }
                    else
                    {
                        var res = (WFSRESULT)Marshal.PtrToStructure(pRes, typeof(WFSRESULT));

                        if (res.hResult != XfsErrors.WFS_SUCCESS || res.lpBuffer == IntPtr.Zero)
                        {
                            Console.WriteLine($"[CDM_CU] hResult={res.hResult}");
                            if (OpenModuleService.IsFatalServiceError(res.hResult))
                                OpenModuleService.InvalidateCdm();
                        }
                        else
                        {
                            var cuInfo = (CDM.WFSCDMCUINFO)Marshal.PtrToStructure(res.lpBuffer, typeof(CDM.WFSCDMCUINFO));

                            for (int i = 0; i < cuInfo.usCount; i++)
                            {
                                IntPtr pCu = Marshal.ReadIntPtr(cuInfo.lppList, i * IntPtr.Size);
                                if (pCu == IntPtr.Zero) continue;

                                var cu = (CDM.WFSCDMCASHUNIT)Marshal.PtrToStructure(pCu, typeof(CDM.WFSCDMCASHUNIT));

                                string unitId = BytesToAscii(cu.cUnitID);
                                string cur = BytesToAscii(cu.cCurrencyID);

                                // ⚠ یک رکورد به ازای هر کاست *منطقی* — نه فیزیکی.
                                // نسخه‌ی قبلی داخل حلقه‌ی physical اضافه می‌کرد و
                                // موجودی کاست‌های چندفیزیکی را دوبار می‌شمرد،
                                // و کاست‌های بدون physical را کلاً می‌انداخت.
                                command.Cashunit.Add(new CashUnitInfo
                                {
                                    Init = cu.ulInitialCount,
                                    currency = cur,
                                    Count = cu.ulCount,
                                    Presented = 0,
                                    UnitId = GetUniqueLogicalUnitId(unitId, command.Cashunit),
                                    Denomination = (int)cu.ulValues
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CDM_CU] EXCEPTION {ex.GetType().Name}: {ex.Message}");
                }
                finally
                {
                    if (pRes != IntPtr.Zero)
                    {
                        try { XfsApi.WFSFreeResult(pRes); } catch { }
                        pRes = IntPtr.Zero;
                    }
                }
            }

            // ==================== IDC ====================
            if (OpenModuleService.EnsureIdcOpen())
            {
                if (IDC.TryGetStatus(OpenModuleService.hIdc, out var stIdc))
                {
                    command.IdcStatus = new models.Module.IdcStatusDto
                    {
                        Device = stIdc.fwDevice,
                        ChipPower = stIdc.fwChipPower,
                        Media = stIdc.fwMedia,
                        RetainBin = stIdc.fwRetainBin,
                        usCards = stIdc.usCards,
                    };
                    if (stIdc.fwDevice != 0 && stIdc.fwDevice != 6) HaveError = true;
                }
            }

            // ==================== PTR ====================
            if (OpenModuleService.EnsurePtrOpen())
                PrinterXFsLogic(ref HaveError, command);

            // ==================== SIU ====================
            if (OpenModuleService.EnsureSensorOpen())
            {
                const int WFS_SIU_OPERATORSWITCH = 0;      // fwSensors[0]
                const int WFS_SIU_OPENCLOSE = 0;      // fwIndicators[0]
                const ushort WFS_SIU_RUN = 0x0001;
                const ushort WFS_SIU_OPEN = 0x0002;

                WFSSIUSTATUS st;
                if (TryGetInfo(OpenModuleService.hSiu, SIU.WFS_INF_SIU_STATUS, 10000, "SIU", out st, out hr))
                {
                    var sensors = st.fwSensors ?? new ushort[0];
                    var indicators = st.fwIndicators ?? new ushort[0];

                    ushort openClose = (indicators.Length > WFS_SIU_OPENCLOSE)
                                            ? indicators[WFS_SIU_OPENCLOSE] : (ushort)0;
                    ushort opSwitch = (sensors.Length > WFS_SIU_OPERATORSWITCH)
                                     ? sensors[WFS_SIU_OPERATORSWITCH] : (ushort)0;

                    bool isOpen = (openClose & WFS_SIU_OPEN) == WFS_SIU_OPEN;

                    // اگر SP کلید اپراتور را پشتیبانی نکند (NOT_AVAILABLE = 0)،
                    // به وضعیت OPENCLOSE تکیه کن — Hyosung اینطور است.
                    bool switchSupported = opSwitch != 0;
                    bool isRun = switchSupported
                               ? (opSwitch & WFS_SIU_RUN) == WFS_SIU_RUN
                               : isOpen;

                    bool canConnect = PortChecker.CanConnect(HostCheckIp, HostCheckPort);

                    if (isRun && canConnect) InService = true;
                    else if (isRun && !canConnect) offline = true;
                    else if (!isRun && canConnect) online = true;

                    command.SiuStatus = new SiuStatusModel
                    {
                        Device = st.fwDevice,
                        Doors = st.fwDoors ?? new ushort[0],
                        Auxiliaries = st.fwAuxiliaries ?? new ushort[0],
                        GuidLights = st.fwGuidLights ?? new ushort[0],
                        Indicators = indicators,
                    };
                }
                else if (OpenModuleService.IsFatalServiceError(hr))
                {
                    OpenModuleService.InvalidateSensors();
                }
            }

            // ==================== CAMERA ====================
            if (OpenModuleService.EnsureCameraOpen())
            {
                const int CAM_PERSON = 1;
                CAM.WFSCAMSTATUS st;
                if (TryGetInfo(OpenModuleService.hCam, CAM.WFS_INF_CAM_STATUS, 10000, "CAM", out st, out hr))
                {
                    command.CameraStatus = new CameraStatusDto
                    {
                        Device = st.fwDevice,
                        AntiFraudModule =0,
                        Detailes = new List<CameradetailDto>
                        {
                            PrintCamLine(st, CAM.WFS_CAM_ROOM,     "ROOM    "),
                            PrintCamLine(st, CAM.WFS_CAM_PERSON,   "PERSON  "),
                            PrintCamLine(st, CAM.WFS_CAM_EXITSLOT, "EXITSLOT"),
                        }
                    };
                }
                if (st.fwDevice != 0 && st.fwDevice != 6)
                {
                    HaveError = true;
                    Console.WriteLine($"[CAM] خطای ماژول دوربین fwDevice={st.fwDevice}");
                }

                if (st.fwCameras != null && st.fwCameras.Length > CAM_PERSON)
                {
                    ushort camState = st.fwCameras[CAM_PERSON];

                    if (camState == 2)          // WFS_CAM_CAMINOP
                    {
                        HaveError = true;
                        Console.WriteLine("[CAM] دوربین مشتری از کار افتاده (CAMINOP).");
                    }
                }

                if (st.fwMedia != null && st.fwMedia.Length > CAM_PERSON)
                {
                    if (st.fwMedia[CAM_PERSON] == 2)   // WFS_CAM_MEDIAFULL
                    {
                        HaveError = true;
                        Console.WriteLine("[CAM] حافظه دوربین مشتری پر است — عکس جدید ثبت نمی‌شود.");
                    }
                }
                else if (OpenModuleService.IsFatalServiceError(hr))
                {
                    OpenModuleService.InvalidateCamera();
                }
            }

            // ==================== PIN ====================
            if (OpenModuleService.EnsurePinOpen())
            {
                PIN.WFSPINSTATUS st;
                if (TryGetInfo(OpenModuleService.hPin, PIN.WFS_INF_PIN_STATUS, 10000, "PIN", out st, out hr))
                {
                    command.PinStatus = new PinStatusDto { Device = st.fwDevice };
                    if (st.fwDevice != 0 && st.fwDevice != 6) PinEror = true;
                }
                else
                {
                    OpenModuleService.InvalidatePin();
                }
            }

            // ==================== MODE ====================
            bool paperWarn = command.ptrStatus.Paper == PaperStatus.Low
                                     || command.ptrStatus.Paper == PaperStatus.Empty
                                     || command.ptrStatus.Paper == PaperStatus.Jammed;

            long totalMoney = command.Cashunit.Sum(x => (long)x.Count * x.Denomination);
            bool moneyWarn = totalMoney < MoneyWarningThreshold;

            if (HaveError) command.Mode = DeviceMode.Error;
            else if (paperWarn) command.Mode = DeviceMode.warning_paper;   // کاغذ اولویت بالاتر
            else if (moneyWarn) command.Mode = DeviceMode.warning_Money;
            else if (PinEror) command.Mode = DeviceMode.Supervisor;
            else if (InService) command.Mode = DeviceMode.InService;
            else if (offline) command.Mode = DeviceMode.Offline;
            else if (online) command.Mode = DeviceMode.Online;
            else command.Mode = DeviceMode.Supervisor;

            return command;
        }
        /// <summary>
        /// یک WFSGetInfo سینک را امن اجرا می‌کند و ساختار خروجی را برمی‌گرداند.
        /// در صورت هر خطایی false برمی‌گرداند و بافر را حتماً آزاد می‌کند.
        /// </summary>
        private static bool TryGetInfo<T>(ushort hService, int category, int timeoutMs,
                                          string tag, out T value, out int hr) where T : struct
        {
            value = default(T);
            hr = XfsErrors.WFS_SUCCESS;
            IntPtr pRes = IntPtr.Zero;

            if (hService == 0)
            {
                hr = XfsErrors.WFS_ERR_INVALID_HSERVICE;
                Console.WriteLine($"[{tag}] handle=0, skip");
                return false;
            }

            try
            {
                hr = XfsApi.WFSGetInfo(hService, category, IntPtr.Zero, timeoutMs, ref pRes);

                if (hr != XfsErrors.WFS_SUCCESS || pRes == IntPtr.Zero)
                {
                    Console.WriteLine($"[{tag}] WFSGetInfo failed hr={hr}");
                    return false;
                }

                var res = (WFSRESULT)Marshal.PtrToStructure(pRes, typeof(WFSRESULT));

                if (res.hResult != XfsErrors.WFS_SUCCESS || res.lpBuffer == IntPtr.Zero)
                {
                    hr = res.hResult;
                    Console.WriteLine($"[{tag}] result hResult={res.hResult}");
                    return false;
                }

                value = (T)Marshal.PtrToStructure(res.lpBuffer, typeof(T));
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{tag}] EXCEPTION {ex.GetType().Name}: {ex.Message}");
                hr = XfsErrors.WFS_ERR_INTERNAL_ERROR;
                return false;
            }
            finally
            {
                // فقط و فقط یک بار — و بعدش صفر می‌شود
                if (pRes != IntPtr.Zero)
                {
                    try { XfsApi.WFSFreeResult(pRes); } catch { }
                    pRes = IntPtr.Zero;
                }
            }
        }

        private static void PrinterXFsLogic(ref bool HaveError, DeviceMuduleStatusCommand command)
        {
            PTR.WFSPTRSTATUS st;
            int hr;

            if (!TryGetInfo(OpenModuleService.hPtr, PTR.WFS_INF_PTR_STATUS, 10000, "PTR", out st, out hr))
            {
                if (OpenModuleService.IsFatalServiceError(hr))
                    OpenModuleService.InvalidatePtr();

                command.ptrStatus = new PtrStatusDto
                {
                    Device = 0,
                    Media = 0,
                    Ink = 0,
                    Toner = 0,
                    Paper = PaperStatus.Unknown
                };
                return;
            }

            command.ptrStatus = new models.Module.PtrStatusDto
            {
                Device = st.fwDevice,
                Media = st.fwMedia,
                Ink = st.fwInk,
                Toner = st.fwToner,
                Paper = GetOverallPaperStatus(st.fwPaper)
            };

            if (st.fwDevice != 0 && st.fwDevice != 6)
                HaveError = true;
            var paper = command.ptrStatus.Paper;

            if (paper == PaperStatus.Jammed)
            {
                HaveError = true;
                Console.WriteLine("[PTR] کاغذ گیر کرده است.");
            }
        }

        static string PtrToAnsi(IntPtr p) => p == IntPtr.Zero ? "" : (Marshal.PtrToStringAnsi(p) ?? "");
        static string BytesToAscii(byte[] b)
        {
            if (b == null) return "";
            var s = Encoding.ASCII.GetString(b);
            int z = s.IndexOf('\0');
            s = s ?? string.Empty;
            return ((z >= 0 && z <= s.Length) ? s.Substring(0, z) : s).Trim();
        }
        static string MapDev(ushort fw)
        {
            switch (fw)
            {
                case WFS_STAT_DEVONLINE: return "ONLINE";
                case WFS_STAT_DEVOFFLINE: return "OFFLINE";
                case WFS_STAT_DEVPOWEROFF: return "POWEROFF";
                case WFS_STAT_DEVNODEVICE: return "NODEVICE";
                case WFS_STAT_DEVHWERROR: return "HWERROR";
                case WFS_STAT_DEVUSERERROR: return "USERERROR";
                case WFS_STAT_DEVBUSY: return "BUSY";
                case WFS_STAT_DEVFRAUDATTEMPT: return "FRAUD";
                case WFS_STAT_DEVPOTENTIALFRAUD: return "POTENTIALFRAUD";
                default: return string.Format("UNKNOWN({0})", fw);
            }
        }
        static CameradetailDto PrintCamLine(CAM.WFSCAMSTATUS st, int index, string label)
        {
            if (st.fwMedia == null || st.fwCameras == null || st.usPictures == null) return new CameradetailDto();
            if (index < 0 || index >= st.fwCameras.Length) return new CameradetailDto();

            ushort cam = st.fwCameras[index];
            ushort media = st.fwMedia[index];
            ushort pics = st.usPictures[index];


            return new CameradetailDto
            {
                Lable = label,
                Camera = cam,
                Media = media,
                Pictures = pics
            };
        }

        private static PaperStatus MapXfsPaperToStatus(ushort value)
        {
            // مقادیر استاندارد XFS:
            // 0 = WFS_PTR_PAPERFULL
            // 1 = WFS_PTR_PAPERLOW
            // 2 = WFS_PTR_PAPEROUT
            // 3 = WFS_PTR_PAPERNOTSUPP
            // 4 = WFS_PTR_PAPERUNKNOWN
            // 5 = WFS_PTR_PAPERJAMMED

            switch (value)
            {
                case 0: return PaperStatus.Full;
                case 1: return PaperStatus.Low;
                case 2: return PaperStatus.Empty;
                case 3: return PaperStatus.NotSupported;
                case 4: return PaperStatus.Unknown;
                case 5: return PaperStatus.Jammed;
                default:
                    return PaperStatus.Unknown;
            }
        }

        // محاسبه وضعیت کلی کاغذ از روی آرایه fwPaper[16]
        private static PaperStatus GetOverallPaperStatus(ushort[] fwPaper)
        {
            // اگر پرینتر اصلاً چیزی نداد
            if (fwPaper == null || fwPaper.Length == 0)
                return PaperStatus.Unknown;

            // فقط اندیس‌های استاندارد رو بررسی می‌کنیم: 0..5
            // 0 = UPPER, 1 = LOWER, 2 = EXTERNAL, 3 = AUX, 4 = AUX2, 5 = PARK
            var mapped = new List<PaperStatus>(6);
            int maxIndex = Math.Min(fwPaper.Length, 6);

            for (int i = 0; i < maxIndex; i++)
            {
                mapped.Add(MapXfsPaperToStatus(fwPaper[i]));
            }

            // اگر همه NOT SUPPORTED بودن → کلّاً پشتیبانی نمی‌شود
            if (mapped.TrueForAll(s => s == PaperStatus.NotSupported))
                return PaperStatus.NotSupported;

            // اولویت‌ها:
            // 1) Jammed (بدترین حالت)
            if (mapped.Contains(PaperStatus.Jammed))
                return PaperStatus.Jammed;

            // 2) Empty
            if (mapped.Contains(PaperStatus.Empty))
                return PaperStatus.Empty;

            // 3) Low
            if (mapped.Contains(PaperStatus.Low))
                return PaperStatus.Low;

            // 4) اگر حداقل یک سینی Full بود و بقیه NotSupported/Unknown بودن → Full
            if (mapped.Contains(PaperStatus.Full))
                return PaperStatus.Full;

            // اگر رسیدیم اینجا یعنی فقط Unknown / ترکیب عجیب بوده
            return PaperStatus.Unknown;
        }
        private static string GetUniqueLogicalUnitId(string rawUnitId, IList<CashUnitInfo> existing)
        {
            // Normalization: PCU02 → LCU02
            string id = rawUnitId
                .Replace('p', 'L')
                .Replace('P', 'L');

            // اگر تکراری نیست، همونو برگردون
            if (!existing.Any(cu => string.Equals(cu.UnitId, id, StringComparison.OrdinalIgnoreCase)))
                return id;

            // اگر تکراری بود، عدد ته‌ش رو زیاد کن
            string prefix = id;
            int number = 0;

            // پیدا کردن دو رقم آخر به روش قدیمی سازگار با C# 7.3
            if (id.Length >= 2 && char.IsDigit(id[id.Length - 1]) && char.IsDigit(id[id.Length - 2]))
            {
                prefix = id.Substring(0, id.Length - 2);       // مثل "LCU"
                string numPart = id.Substring(id.Length - 2);  // مثل "02"
                int.TryParse(numPart, out number);             // 2
            }

            // حالا تا وقتی تکراریه، عدد رو ++ کن
            while (true)
            {
                number++;
                string candidate = $"{prefix}{number:00}"; // LCU03, LCU04, ...

                if (!existing.Any(cu => string.Equals(cu.UnitId, candidate, StringComparison.OrdinalIgnoreCase)))
                    return candidate;
            }
        }



    }



}
