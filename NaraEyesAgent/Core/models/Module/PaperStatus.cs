

namespace NaraEyesAgent.Core.models.Module
{
    public enum PaperStatus
    {
        NotSupported = 0,
        Unknown = 1,
        Full = 2,
        Low = 3,
        Empty = 4,
        Jammed = 5
    }

    public enum PaperSupplyKind
    {
        Upper,
        Lower,
        External,
        Aux,
        Aux2,
        Park
    }
}
