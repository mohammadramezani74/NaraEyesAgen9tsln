
namespace NaraEyesAgent.Core.models.Module
{
    public sealed class CashUnitInfo
    {
        public string UnitId { get; set; }
        public string currency { get; set; }
        public uint Init { get; set; }
        public uint Count  { get; set; }
        public  uint Presented { get; set; }
        public int Denomination { get; set; }

    }
}
