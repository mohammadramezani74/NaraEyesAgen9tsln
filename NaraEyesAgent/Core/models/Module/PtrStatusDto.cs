

namespace NaraEyesAgent.Core.models.Module
{
    public class PtrStatusDto
    {
        public ushort Device { get; set; }
        public ushort Media { get; set; }
        public ushort Toner { get; set; }
        public ushort Ink { get; set; }
        public PaperStatus Paper { get; set; }
    }
}
