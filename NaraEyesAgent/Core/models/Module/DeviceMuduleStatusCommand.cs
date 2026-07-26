using NaraEyesAgent.Core.models.Module;


namespace NaraEyesAgent.Core.models
{
    public class DeviceMuduleStatusCommand
    {
        public bool IsInservice { get; set; }
        public DeviceMode Mode { get; set; }
        public string DeviceIp { get; set; } 
        public CdmStatusDto CdmStatus { get; set; }
        public IdcStatusDto IdcStatus { get; set; }
        public PtrStatusDto ptrStatus { get; set; }
        public CameraStatusDto CameraStatus { get; set; }
        public PinStatusDto PinStatus { get; set; }
        public SiuStatusModel SiuStatus { get; set; }
        public List<CashUnitInfo> Cashunit { get; set; }=new List<CashUnitInfo>();
    }
    public enum DeviceMode
    {
    
        InService = 1,
   
        Supervisor = 2,
     
        warning = 3,
      
        Error = 4,
       
        Offline = 5,
   
        Online = 6,
  
        warning_paper = 7,

        warning_Money = 8,

    }
}
