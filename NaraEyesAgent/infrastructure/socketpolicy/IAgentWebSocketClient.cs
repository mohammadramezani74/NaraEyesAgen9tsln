
using NaraEyesAgent.Core.Models.Basic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace NaraEyesAgent.infrastructure.SocketPolicy
{
    public interface IAgentWebSocketClient : IAsyncDisposable
    {
        Task<bool> ConnectAsync(string deviceIp, CancellationToken cancel);
        bool IsConnected { get; }

        Task<PollResponse> PollAsync(string deviceIp, CancellationToken cancel);
        Task<PollResponse> PollAsync(string deviceIp, List<InBoxDeviceMessage> reports, CancellationToken cancel);
    }
}
