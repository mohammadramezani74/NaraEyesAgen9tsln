using NaraEyesAgent.Core.Models.Basic;
using NaraEyesAgent.infrastructure.SocketPolicy;
using Newtonsoft.Json;
using System.Net.WebSockets;
using System.Text;

public sealed class AgentWebSocketClient :
    IAgentWebSocketClient,
    IAsyncDisposable
{
    private readonly Uri _uri;
    private readonly TimeSpan _timeout;
    private ClientWebSocket? _socket;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public bool IsConnected =>
        _socket?.State == WebSocketState.Open;

    public AgentWebSocketClient(string wsUrl, int timeoutMs = 65000)
    {
        _uri = new Uri(wsUrl);
        _timeout = TimeSpan.FromMilliseconds(timeoutMs);
    }

    public async Task<bool> ConnectAsync(string deviceIp, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (IsConnected) return true;

            _socket?.Dispose();
            _socket = new ClientWebSocket();

            // TLS و هدرهای WAF
            _socket.Options.RemoteCertificateValidationCallback =
                (_, _, _, _) => true; // اینترانت بانکی

            _socket.Options.SetRequestHeader("User-Agent", "NaraEyesAgent9/1.0");
            _socket.Options.SetRequestHeader("X-Forwarded-For", deviceIp);

            using var cts = CancellationTokenSource
                .CreateLinkedTokenSource(ct);
            cts.CancelAfter(_timeout);

            await _socket.ConnectAsync(_uri, cts.Token);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WS] ConnectAsync failed: {ex.Message}");
            _socket?.Dispose();
            _socket = null;
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task<PollResponse> PollAsync(string deviceIp, CancellationToken ct)
        => PollAsync(deviceIp, null, ct);

    public async Task<PollResponse> PollAsync(
        string deviceIp,
        List<InBoxDeviceMessage>? reports,
        CancellationToken ct)
    {
        await _lock.WaitAsync(ct); // ✅ lock روی send+receive
        try
        {
            if (!IsConnected)
                throw new InvalidOperationException("WebSocket disconnected");

            // Send
            string json = JsonConvert.SerializeObject(reports);
            byte[] send = Encoding.UTF8.GetBytes(json);

            using var sendCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            sendCts.CancelAfter(_timeout);

            await _socket!.SendAsync(
                send, WebSocketMessageType.Text, true, sendCts.Token);

            // Receive - چند chunk تا EndOfMessage ✅
            using var ms = new MemoryStream();
            var buffer = new byte[8192];
            WebSocketReceiveResult result;

            using var recvCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            recvCts.CancelAfter(_timeout);

            do
            {
                result = await _socket.ReceiveAsync(buffer, recvCts.Token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await DisconnectAsync();
                    return new PollResponse();
                }

                ms.Write(buffer, 0, result.Count);

            } while (!result.EndOfMessage);

            string message = Encoding.UTF8.GetString(ms.ToArray());

            try
            {
                return JsonConvert.DeserializeObject<PollResponse>(message)
                    ?? new PollResponse();
            }
            catch
            {
                return new PollResponse();
            }
        }
        catch (WebSocketException ex)
        {
            Console.WriteLine($"[WS] PollAsync error: {ex.Message}");
            await DisconnectAsync();
            throw; // بذار PollLoop بفهمه و fallback کنه
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        if (_socket == null) return;
        try
        {
            if (IsConnected)
                await _socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "close",
                    CancellationToken.None);
        }
        catch { }
        finally
        {
            _socket.Dispose();
            _socket = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _lock.Dispose();
    }
}