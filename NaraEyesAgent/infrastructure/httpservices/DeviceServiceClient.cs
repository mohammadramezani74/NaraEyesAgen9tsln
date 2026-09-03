using NaraEyesAgent.Core.models;
using NaraEyesAgent.Core.Models.Basic;
using NaraEyesAgent.Core.Models.Metrics;
using NaraEyesAgent.Core.Services;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace NaraEyesAgent.Infrastructure.HttpServices;

public sealed class DeviceServiceClient : IDeviceServiceClient
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    private const int DefaultTimeoutSeconds = 65;

    public DeviceServiceClient(
     
        string baseUrl)
    {
        var handler =
           new SocketsHttpHandler
           {
               UseProxy = false,

               AutomaticDecompression =
                   DecompressionMethods.GZip
                   |
                   DecompressionMethods.Deflate,

               PooledConnectionLifetime =
                   TimeSpan.FromMinutes(2),
               KeepAlivePingPolicy = HttpKeepAlivePingPolicy.WithActiveRequests,
               KeepAlivePingDelay = TimeSpan.FromSeconds(30),
               KeepAlivePingTimeout = TimeSpan.FromSeconds(10),
               SslOptions =
               {
                   EnabledSslProtocols=
                   System.Security.Authentication.SslProtocols.Tls12
                   |
                      System.Security.Authentication.SslProtocols.Tls13
               }
           };
        _http =
             new HttpClient(
                 handler);


        _baseUrl =
            baseUrl
            ?? throw new InvalidOperationException(
                "ApiBaseUrl not configured");

      _http.BaseAddress=  new Uri(
     _baseUrl.EndsWith("/")
         ? _baseUrl
         : _baseUrl + "/");

        _http.Timeout =
            TimeSpan.FromSeconds(
                DefaultTimeoutSeconds);
        _http.DefaultRequestVersion = HttpVersion.Version11;
        _http.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;

        _http.DefaultRequestHeaders.Accept
            .Add(
                new MediaTypeWithQualityHeaderValue(
                    "application/json"));

        _http.DefaultRequestHeaders.UserAgent
            .ParseAdd(
                "NaraEyesAgent9/1.0");
        _http.DefaultRequestHeaders.Add("X-Agent-Key", "K7mQ2xV9pL4zR8nT6wY3cH5sD1fG0bA");
    }

    public async Task RegisterAsync(DeviceRegisterRequest request,CancellationToken ct)
    {
        await PostAsync(
            "device/register",
            request,
            request.Ip,
            ct);
    }

    public async Task<PollResponse> PollAsync(string deviceIp,CancellationToken ct)
    {
        return await GetAsync<PollResponse>($"poll?ip={deviceIp}",
            deviceIp,
            ct)
            ?? new PollResponse();
    }

    public async Task<PollResponse> PollAsync( string deviceIp, List<InBoxDeviceMessage> reports,CancellationToken ct)
    {
        return await PostAsync<PollResponse>(
            $"poll?ip={deviceIp}",
            reports,
            deviceIp,
            ct)
            ?? new PollResponse();
    }

    public async Task SendMetricsAsync( DeviceMetricsDto metrics,CancellationToken ct)
    {
        await PostAsync(
            "device/SubmitMetrics",
            metrics,
            metrics.DeviceIp,
            ct);
    }

    public async Task AgentPowerOffAsync(IpModel model,CancellationToken ct)
    {
        await PostAsync(
            "device/AgentMode",
            model,
            model.Ip,
            ct);
    }

    public async Task SendModuleStatusAsync(DeviceMuduleStatusCommand model,CancellationToken ct)
    {
        await PostAsync(
            "device/SubmitStatus",
            model,
            model.DeviceIp,
            ct);
    }

    private async Task<T?> GetAsync<T>( string url, string ip,CancellationToken ct)
    {
        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                url);

        AddForwardedFor(
            request,
            ip);

        using var response =
            await _http.SendAsync(
                request,
                ct);

        response.EnsureSuccessStatusCode();

        var body =
            await response.Content
                .ReadAsStringAsync(ct);

        return JsonConvert
            .DeserializeObject<T>(
                body);
    }

    private async Task PostAsync(string url,object payload,string ip,CancellationToken ct)
    {
        await PostAsync<object>(
            url,
            payload,
            ip,
            ct);
    }

    private async Task<T?> PostAsync<T>(string url,object payload,string ip,CancellationToken ct)
    {
        var requestId = Guid.NewGuid().ToString("N");


           try
        {

     
        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                url);

        AddForwardedFor(
            request,
            ip);
 
        request.Content =
            new StringContent(
                JsonConvert.SerializeObject(
                    payload),
                Encoding.UTF8,
                "application/json");

        using var response =
            await _http.SendAsync(
                request,
                ct);
        try
        {
     
            response.EnsureSuccessStatusCode();
        }
        catch (Exception)
        {

          
        }
      

        if (typeof(T) == typeof(object))
            return default;

        var body =
            await response.Content
                .ReadAsStringAsync(ct);

        return JsonConvert
            .DeserializeObject<T>(
                body);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{requestId}] ===== ERROR =====");
            Console.WriteLine($"[{requestId}] Type: {ex.GetType().FullName}");
            Console.WriteLine($"[{requestId}] Message: {ex.Message}");
            var inner = ex.InnerException;
            var level = 1;

            while (inner != null)
            {
                Console.WriteLine(
                    $"[{requestId}] Inner[{level}] {inner.GetType().FullName}");

                Console.WriteLine(
                    $"[{requestId}] Inner[{level}] {inner.Message}");

                inner = inner.InnerException;
                level++;
            }

            //Console.WriteLine($"[{requestId}] Stack:");
            //Console.WriteLine(ex.StackTrace);
                return default;

        }
    }

    private static void AddForwardedFor(
        HttpRequestMessage req,
        string ip)
    {
        if (!string.IsNullOrWhiteSpace(ip))
        {
            req.Headers.TryAddWithoutValidation(
                "X-Forwarded-For",
                ip);
        }
    }
}