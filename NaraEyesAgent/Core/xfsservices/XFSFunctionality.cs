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
        public static void ResteCdm()
        {
            try
            {


                var cdm = OpenModuleService.hCdm;
                Console.WriteLine($"hcdm given {cdm}");
                IntPtr pLock = IntPtr.Zero;
                int hrLock = XfsApi.WFSLock(cdm, 15000, ref pLock);
                if (hrLock == WFS_SUCCESS && pLock != IntPtr.Zero) XfsApi.WFSFreeResult(pLock);

                IntPtr pRes = IntPtr.Zero;
                int hr = XfsApi.WFSExecute(cdm, CDM.WFS_CMD_CDM_RESET, IntPtr.Zero, 60000, ref pRes);
                if (hr != WFS_SUCCESS) Console.WriteLine($"WFSExecute(CDM_RESET) failed hr=0x{hr:X}");

                //var res = Marshal.PtrToStructure<WFSRESULT>(pRes);
                //Console.WriteLine(res.hResult == WFS_SUCCESS ? "CDM RESET: WFS_SUCCESS" : $"CDM RESET failed: 0x{res.hResult:X}");
                XfsApi.WFSFreeResult(pRes);
                XfsApi.WFSUnlock(cdm);
            }
            catch (Exception)
            {

                throw;
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
            if (hrLock == WFS_SUCCESS && pLock != IntPtr.Zero) XfsApi.WFSFreeResult(pLock);

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
            bool inserviseSituation = false;
            bool HaveError = false;
            bool PinEror = false;
            bool InService = false;
            bool outOfService = false;
            bool online = false;
            bool offline = false;

            var command = new DeviceMuduleStatusCommand();
            if (OpenModuleService.EnsureCdmOpen())
            {
                IntPtr pRes = IntPtr.Zero;
                try
                {



                    XfsApi.WFSGetInfo(OpenModuleService.hCdm, CDM.WFS_INF_CDM_STATUS, IntPtr.Zero, 10000, ref pRes);

                    var res = (WFSRESULT)Marshal.PtrToStructure(pRes, typeof(WFSRESULT));
                    //Console.WriteLine($"m.hresult = {res.hResult}");
                    if (res.hResult != WFS_SUCCESS || res.lpBuffer == IntPtr.Zero)
                    {
                        Console.WriteLine($"CDM STATUS failed: 0x{res.hResult:X}");

                        if (OpenModuleService.IsFatalServiceError(res.hResult))
                        {
                            OpenModuleService.InvalidateCdm();
                        }


                        command.CdmStatus = new CdmStatusDto
                        {
                            Device = 0,
                            Dispenser = 0,
                            IntermediateStacker = 0,
                            SafeDoor = 0
                        };
                    }
                    else
                    {

                        var st = (CDM.WFSCDMSTATUS)Marshal.PtrToStructure(res.lpBuffer, typeof(CDM.WFSCDMSTATUS));
                        //  Console.WriteLine($"CDM Device={MapDev(st.fwDevice)}  Dispenser={st.fwDispenser}  Stacker={st.fwIntermediateStacker}  SafeDoor={st.fwSafeDoor}");
                        command.CdmStatus = new CdmStatusDto
                        {
                            Device = st.fwDevice,
                            Dispenser = st.fwDispenser,
                            IntermediateStacker = st.fwIntermediateStacker,
                            SafeDoor = st.fwSafeDoor,
                        };
                        if (st.fwDevice != 0 && st.fwDevice != 6)
                        {
                            HaveError = true;
                        }
                    }
                }
                catch
                {

                }
                finally
                {
                    if(pRes!=IntPtr.Zero) XfsApi.WFSFreeResult(pRes);
                }
            
            }
            else
            {
                command.CdmStatus = new CdmStatusDto
                {
                    Device = 0,
                    Dispenser = 0,
                    IntermediateStacker = 0,
                    SafeDoor = 0
                };
            }
            if (OpenModuleService.EnsureCdmOpen())
            {

                IntPtr pRes = IntPtr.Zero;

                try
                {
                    XfsApi.WFSGetInfo(OpenModuleService.hCdm, CDM.WFS_INF_CDM_CASH_UNIT_INFO, IntPtr.Zero, 20000, ref pRes);

                    var res = (WFSRESULT)Marshal.PtrToStructure(pRes, typeof(WFSRESULT));
                    if (res.hResult != WFS_SUCCESS || res.lpBuffer == IntPtr.Zero)
                    {
                        Console.WriteLine($"CDM CASH_UNIT_INFO failed: 0x{res.hResult:X}");

                        if (OpenModuleService.IsFatalServiceError(res.hResult))
                        {
                            // هندل خراب شده، دفعه بعد دوباره Open می‌کنیم
                            OpenModuleService.InvalidateCdm();
                        }

                      
                        command.Cashunit = new List<CashUnitInfo>();

                    }
                    else
                    {
                        var cuInfo = (CDM.WFSCDMCUINFO)Marshal.PtrToStructure(res.lpBuffer, typeof(CDM.WFSCDMCUINFO));
                        command.Cashunit = new List<CashUnitInfo>();

                        for (int i = 0; i < cuInfo.usCount; i++)
                        {
                            IntPtr pCu = Marshal.ReadIntPtr(cuInfo.lppList, i * IntPtr.Size);
                            var cu = (CDM.WFSCDMCASHUNIT)Marshal.PtrToStructure(pCu, typeof(CDM.WFSCDMCASHUNIT));

                            string name = PtrToAnsi(cu.lpszCashUnitName);
                            string unitId = BytesToAscii(cu.cUnitID);
                            string cur = BytesToAscii(cu.cCurrencyID);

                            uint denomValue = cu.ulValues;



                            //Console.WriteLine($"Denome is {denomValue}");
                            // --- Physicals ---
                            if (cu.usNumPhysicalCUs > 0 && cu.lppPhysical != IntPtr.Zero)
                            {
                                //Console.WriteLine($"   Physical CUs: {cu.usNumPhysicalCUs}");
                                for (int j = 0; j < cu.usNumPhysicalCUs; j++)
                                {
                                    IntPtr pPh = Marshal.ReadIntPtr(cu.lppPhysical, j * IntPtr.Size);
                                    var ph = (CDM.WFSCDMPHCU)Marshal.PtrToStructure(pPh, typeof(CDM.WFSCDMPHCU));

                                    
                                    string phyId = BytesToAscii(ph.cUnitID);

                                    string logicalUnitId = GetUniqueLogicalUnitId(unitId, command.Cashunit);
                                    command.Cashunit.Add(new CashUnitInfo
                                    {
                                        Init = cu.ulInitialCount,
                                        currency = cur,
                                        Count = cu.ulCount,
                                        Presented = cu.ulPresentedCount,
                                        UnitId = logicalUnitId,
                                        Denomination = (int)denomValue
                                    });
                                }
                            }
                        }
                    }
                }
                catch { }
                finally
                {
                    if (pRes != IntPtr.Zero) XfsApi.WFSFreeResult(pRes);
                }
            }
            else
            {
                command.Cashunit = new List<CashUnitInfo>();
            }
            if (OpenModuleService.EnsureIdcOpen())
            {
                if (IDC.TryGetStatus(OpenModuleService.hIdc, out var st))
                {
                    command.IdcStatus = new models.Module.IdcStatusDto
                    {
                        Device = st.fwDevice,
                        ChipPower = st.fwChipPower,
                        Media = st.fwMedia,
                        RetainBin = st.fwRetainBin,
                        usCards = st.usCards,
                    };
                    if (st.fwDevice != 0 && st.fwDevice != 6)
                    {
                        HaveError = true;
                    }
                    // Console.WriteLine($"IDC Device={MapDev(st.fwDevice)}  Media={st.fwMedia}  RetainBin={st.fwRetainBin}  CardsRetained={st.usCards}  ChipPower={st.fwChipPower}");
                }
                else
                {
                    command.IdcStatus = new models.Module.IdcStatusDto
                    {
                        Device = 0,
                        ChipPower = 0,
                        Media = 0,
                        RetainBin = 0,
                        usCards = 0,
                    };
                }
                 
            }
            else
            {
                command.IdcStatus = new models.Module.IdcStatusDto
                {
                    Device = 0,
                    ChipPower = 0,
                    Media = 0,
                    RetainBin = 0,
                    usCards = 0,
                };
            }

            if (OpenModuleService.EnsurePtrOpen())
            {
                IntPtr pRes = IntPtr.Zero;
                PrinterXFsLogic(ref HaveError, command, ref pRes);
            }
            else
            {
                command.ptrStatus = new PtrStatusDto
                {
                    Device = 0,
                    Media = 0,
                    Ink = 0,
                    Toner = 0,
                    Paper = PaperStatus.Unknown
                };
            }

            if (OpenModuleService.EnsureSensorOpen())
            {
                const int WFS_SIU_OPERATORSWITCH = 0;  // fwSensors[0]
                const int WFS_SIU_OPENCLOSE = 0;
                const ushort WFS_SIU_RUN = 0x0001;
                const ushort WFS_SIU_MAINTENANCE = 0x0002; // اگر خواستی بعداً گزارش جدا بده
                const ushort WFS_SIU_SUPERVISOR = 0x0004; // اگر خواستی بعداً گزارش جدا بده
                const ushort WFS_SIU_CLOSED = 0x0001;
                const ushort WFS_SIU_OPEN = 0x0002;

                IntPtr pRes = IntPtr.Zero;
                try
                {



                    XfsApi.WFSGetInfo(OpenModuleService.hSiu, SIU.WFS_INF_SIU_STATUS, IntPtr.Zero, 10000, ref pRes);
                    var res = (WFSRESULT)Marshal.PtrToStructure(pRes, typeof(WFSRESULT));
                    if (res.hResult != WFS_SUCCESS || res.lpBuffer == IntPtr.Zero)
                    {
                        if (OpenModuleService.IsFatalServiceError(res.hResult))
                        {
                            OpenModuleService.InvalidateSensors();
                        }
                    }
                    else
                    {
                        var st = (WFSSIUSTATUS)Marshal.PtrToStructure(res.lpBuffer, typeof(WFSSIUSTATUS));
                        var sensors = st.fwSensors;
                        var indicators = st.fwIndicators;
                        ushort opSwitch = (sensors.Length > WFS_SIU_OPERATORSWITCH) ? sensors[WFS_SIU_OPERATORSWITCH] : (ushort)0;
                        ushort openClose = (indicators.Length > WFS_SIU_OPENCLOSE) ? indicators[WFS_SIU_OPENCLOSE] : (ushort)0;
                        bool isRun = (opSwitch & WFS_SIU_RUN) == WFS_SIU_RUN;
                        bool isOpen = (openClose & WFS_SIU_OPEN) == WFS_SIU_OPEN;

                        string siuMode = (isRun && isOpen) ? "inservice" : "outofService";
                        var canConnect = PortChecker.CanConnect("10.119.254.69", 8001);
                        if (isRun && canConnect)
                        {
                            inserviseSituation = true;
                            InService = true;
                        }
                        else if (isRun && !canConnect)
                        {
                            offline = true;
                        }
                        else if (!isRun && canConnect)
                        {
                            online = true;
                        }
                        else
                        {
                            outOfService = true;
                        }


                        command.SiuStatus = new SiuStatusModel
                        {
                            Device = st.fwDevice,
                            Doors = st.fwDoors,
                            Auxiliaries = st.fwAuxiliaries,
                            GuidLights = st.fwGuidLights,
                            Indicators = st.fwIndicators,
                        };
                    }
                }
                catch { }
                finally
                {
                    if (pRes != IntPtr.Zero) XfsApi.WFSFreeResult(pRes);

                }
            }
            else
            {
                command.SiuStatus = new SiuStatusModel
                {
                    Device = 0,
                    Doors = new ushort[0],
                    Auxiliaries = new ushort[0],
                    GuidLights = new ushort[0],
                    Indicators = new ushort[0]
                };
            }

            if (OpenModuleService.EnsureCameraOpen())
            {
                IntPtr pRes = IntPtr.Zero;
                XfsApi.WFSGetInfo(OpenModuleService.hCam, CAM.WFS_INF_CAM_STATUS, IntPtr.Zero, 10000, ref pRes);

                try
                {
                    var res = (WFSRESULT)Marshal.PtrToStructure(pRes, typeof(WFSRESULT));
                    if (res.hResult != WFS_SUCCESS || res.lpBuffer == IntPtr.Zero)
                    {
                        if (OpenModuleService.IsFatalServiceError(res.hResult))
                        {
                            OpenModuleService.InvalidateCamera();
                        }
                    }
                    else
                    {

                        var st = (CAM.WFSCAMSTATUS)Marshal.PtrToStructure(res.lpBuffer, typeof(CAM.WFSCAMSTATUS));
                        command.CameraStatus = new CameraStatusDto
                        {
                            Device = st.fwDevice,
                            AntiFraudModule = st.wAntiFraudModule,
                            Detailes = new List<CameradetailDto>
                        {
                               PrintCamLine(st, CAM.WFS_CAM_ROOM, "ROOM    "),
                    PrintCamLine(st, CAM.WFS_CAM_PERSON, "PERSON  "),
                    PrintCamLine(st, CAM.WFS_CAM_EXITSLOT, "EXITSLOT"),
                        }
                        };
                    }
                   



                }
                finally
                {
                    if (pRes != IntPtr.Zero) XfsApi.WFSFreeResult(pRes);
                }
            }
            if (OpenModuleService.EnsurePinOpen())
            {
                IntPtr pRes = IntPtr.Zero;
        

                try
                {
                    XfsApi.WFSGetInfo(OpenModuleService.hPin, PIN.WFS_INF_PIN_STATUS, IntPtr.Zero, 10000, ref pRes);
                    var res = (WFSRESULT)Marshal.PtrToStructure(pRes, typeof(WFSRESULT));
                    if (res.hResult != WFS_SUCCESS || res.lpBuffer == IntPtr.Zero)
                    {
                        OpenModuleService.InvalidatePin();
                    }
                    else
                    {

                        var st = (PIN.WFSPINSTATUS)Marshal.PtrToStructure(res.lpBuffer, typeof(PIN.WFSPINSTATUS));
                        //  Console.WriteLine($"PIN Device={MapDev(st.fwDevice)}");
                        command.PinStatus = new PinStatusDto { Device = st.fwDevice };
                        if (st.fwDevice != 0 && st.fwDevice != 6)
                        {
                            PinEror = true;
                            outOfService = true;
                        }
                    }
                }
                finally
                {
                    if (pRes != IntPtr.Zero) XfsApi.WFSFreeResult(pRes);
                }

            }

            if (command.ptrStatus.Paper == PaperStatus.Low || command.ptrStatus.Paper == PaperStatus.Empty)
            {
                command.Mode = DeviceMode.warning_paper;
            }
            var haveSummationMoney = command.Cashunit.Sum(x =>( x.Count * x.Denomination));
            //Console.WriteLine($"mablagh= {haveSummationMoney}");
            if (haveSummationMoney < 20_000_000)
            {
                command.Mode = DeviceMode.warning_Money;
            }

            if (HaveError)
            {
                command.Mode = DeviceMode.Error;
            }
            else if (command.Mode== DeviceMode.warning_paper)
            {
                command.Mode = DeviceMode.warning_paper;
            }
            else if (command.Mode == DeviceMode.warning_Money)
            {
                command.Mode = DeviceMode.warning_Money;
            }
            else if (PinEror)
            {

                command.Mode = DeviceMode.Supervisor;
            }
            else if (InService)
            {
                command.Mode = DeviceMode.InService;
            }
            else if (offline)
            {
                command.Mode = DeviceMode.Offline;
            }
            else if (online)
            {
                command.Mode = DeviceMode.Online;
            }
            else
            {

                command.Mode = DeviceMode.Supervisor;
            }

            return command;
        }

        private static void PrinterXFsLogic(ref bool HaveError, DeviceMuduleStatusCommand command, ref IntPtr pRes)
        {
            try
            {
                XfsApi.WFSGetInfo(OpenModuleService.hPtr, PTR.WFS_INF_PTR_STATUS, IntPtr.Zero, 10000, ref pRes);

                var res = (WFSRESULT)Marshal.PtrToStructure(pRes, typeof(WFSRESULT));
                if (res.hResult != WFS_SUCCESS || res.lpBuffer == IntPtr.Zero)
                {
                    Console.WriteLine($"PTR STATUS failed: 0x{res.hResult:X}");

                    if (OpenModuleService.IsFatalServiceError(res.hResult))
                    {
                        OpenModuleService.InvalidatePtr();
                    }


                    command.ptrStatus = new PtrStatusDto
                    {
                        Device = 0,
                        Media = 0,
                        Ink = 0,
                        Toner = 0,
                        Paper = PaperStatus.Unknown
                    };
                }
                else
                {
                    var st = (PTR.WFSPTRSTATUS)Marshal.PtrToStructure(res.lpBuffer, typeof(PTR.WFSPTRSTATUS));
                    var paperStatus = GetOverallPaperStatus(st.fwPaper);
                    command.ptrStatus = new models.Module.PtrStatusDto
                    {
                        Device = st.fwDevice,
                        Media = st.fwMedia,
                        Ink = st.fwInk,
                        Toner = st.fwToner,
                        Paper = paperStatus
                    };
                    //Console.WriteLine($"printer status is : {st.fwDevice}");
                    if (st.fwDevice != 0 && st.fwDevice != 6)
                    {
                        HaveError = true;
                    }
                    XfsApi.WFSFreeResult(pRes);
                }
            }
            catch
            {

            }
            finally
            {
                if (pRes != IntPtr.Zero) XfsApi.WFSFreeResult(pRes);
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
