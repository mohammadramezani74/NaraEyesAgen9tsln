using NaraEyesAgent.Core.models;
using NaraEyesAgent.Core.Models.Basic;
using NaraEyesAgent.Core.Models.Metrics;

namespace NaraEyesAgent.Core.Services
{
    public interface IDeviceServiceClient
    {
        Task RegisterAsync(DeviceRegisterRequest request,CancellationToken ct);
        Task<PollResponse> PollAsync(string deviceIp, CancellationToken ct);
        Task<PollResponse> PollAsync(string deviceIp, List<InBoxDeviceMessage> reports, CancellationToken ct);
        Task SendMetricsAsync(DeviceMetricsDto metrics, CancellationToken ctl);
        Task SendModuleStatusAsync(DeviceMuduleStatusCommand command, CancellationToken ct);
        Task AgentPowerOffAsync(IpModel metrics, CancellationToken ct);
    }
}
