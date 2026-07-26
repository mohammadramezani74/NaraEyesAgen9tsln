

namespace NaraEyesAgent.Core.models.Module
{
    public sealed class SiuStatusModel
    {
        public ushort Device { get; set; }
        public ushort[] Doors { get; set; }
        public ushort[] Indicators { get; set; }
        public ushort[] Auxiliaries { get; set; }
        public ushort[] GuidLights { get; set; }
    }
}
