

namespace NaraEyesAgent.Core.models.Module
{
    public class CameraStatusDto
    {
        public ushort Device { get; set; }
        public ushort AntiFraudModule { get; set; }
        public List<CameradetailDto> Detailes { get; set; } = new List<CameradetailDto>();
    }
    public class CameradetailDto
    {
        public string Lable { get; set; }
        public ushort Camera { get; set; }
        public ushort Media { get; set; }
        public ushort Pictures { get; set; }
    }
}
