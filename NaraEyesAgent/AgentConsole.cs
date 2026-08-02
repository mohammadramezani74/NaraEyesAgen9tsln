using System.Runtime.InteropServices;

public sealed class AgentConsole
{
    #region Dependencies
    private DateTime _lastWsRetry = DateTime.MinValue;
    private Task? _journalThread;
    private Task? _pollThread;
    private Task? _metricsThread;
    private readonly CancellationTokenSource _stop =
        new();

    private readonly TaskCompletionSource _stopped =
        new();

    private readonly Random _rnd = new Random();
    private volatile int _metricsIntervalSec = 180;
    private const int _metricsJitterSec = 60;
    private const int _ejournalMaxJitterMinutes = 30;
    private static readonly TimeSpan _metricsMin = TimeSpan.FromSeconds(30);

    private IDeviceServiceClient _deviceService;
    private string _deviceIp = "";
    private string _pingTarget = "";
    private string _journalPath = "";
    private string _apiBase = "";
    private AppConfig _config;
    private Task? _xfsMsgThread;
    private string _configPathUsed = "";
    private readonly Logger _logger;
    private AgentWebSocketClient _wsClient;
    private volatile bool _wsConnected;
    private readonly SemaphoreSlim _cdmLock = new SemaphoreSlim(1, 1);
    private readonly SemaphoreSlim _idcLock = new SemaphoreSlim(1, 1);
    private readonly SemaphoreSlim _printerLock = new SemaphoreSlim(1, 1);
    public int _terminalCode { get; set; }
 
    public AgentConsole(Logger Log)
    {
        _logger = Log;
    }   
    #endregion
    public async Task Start()
    {
        Console.WriteLine("\\\\\\\\\\\\\\\\\\\\\\\\\\Nara Eyes Agent //////////////////////////");
        var publicKey =
    File.ReadAllText("publicKey.xml");

        var service =
            new LicenseService(
                "license.lic",
                publicKey);

        //if (!service.IsValid())
        //{
        //    Console.WriteLine("License Invalid");
        //    return;
        //}

        Console.WriteLine("License Valid");
        LoadConfig();

        var folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Certs");

        CertificateInstaller.InstallCertificatesFromFolder(folderPath);
        // Device service (HTTP client 3.5)
        _deviceService = new DeviceServiceClient(_apiBase);

        // Register device (best-effort)
        try
        {
            _deviceIp = GetIpHelper.GetLocalIPv4();
            Console.WriteLine(_deviceIp);
            var req = new DeviceRegisterRequest
            {
                TerminalCode = _terminalCode,
                Ip = _deviceIp,
                Model = Environment.MachineName,
                AgentVersion = GetIpHelper.GetAgentVersion(),
            };
          await  _deviceService.RegisterAsync(req, _stop.Token); // همگام، با لغو
            Info("✅ Device registered successfully");
        }
        catch (Exception ex)
        {
            Error(ex, "❌ Device register failed");
        }

        string wsUrl = _apiBase.Replace("http://", "ws://")
                        .Replace("https://", "wss://")
                        + "/ws";
        var wssocketurl = wsUrl + $"?ip={_deviceIp}";
        _wsClient = new AgentWebSocketClient(wssocketurl);
        _logger.Info($"WEBSOCET URL IS :{wssocketurl}");
        _wsConnected =await _wsClient.ConnectAsync(_deviceIp, _stop.Token);

        if (_wsConnected)
            Info("✅ WebSocket connected to {0}", wsUrl);
        else
            Info("ℹ WebSocket not available, using HTTP long-poll only.");
        // Threads
       await SendMetricsOnce();
        _xfsMsgThread =
      RunStaLoop(
          WinMsgPump);
        _pollThread = Task.Run(
        () => PollLoop(_stop.Token));
        _metricsThread = Task.Run(
        () => MetricsLoop(_stop.Token));

        _journalThread = Task.Run(() => JournalLoop(_stop.Token));


    }
    public async Task Stop()
    {
        Info("🛑 Stopping agent at {0}", DateTime.Now);
        _stop.Cancel();

        var tasks =
            new[]
            {
            _pollThread,
            _metricsThread,
            _journalThread,
            _xfsMsgThread
            }
            .Where(x => x != null);

        await Task.WhenAll(tasks!.Select(t =>
        t.WaitAsync(TimeSpan.FromSeconds(5))
         .ContinueWith(_ => { })));


        _stopped.TrySetResult();
    }

    private void WinMsgPump(CancellationToken token)
    {
        try
        {
            OpenModuleService.openAllModulesOnce();

            // حلقه‌ی پیام واقعی روی همین ترد STA.
            // GetMessage تا رسیدن پیام بلاک می‌شود (بدون مصرف CPU).
            while (!token.IsCancellationRequested)
            {
                NativeMessage msg;
                int r = GetMessage(out msg, IntPtr.Zero, 0, 0);

                if (r == 0 || r == -1) break;   // WM_QUIT یا خطا

                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }
        catch (Exception ex)
        {
            Error(ex, "❌ WinMsgPump failed");
        }
        finally
        {
            try { OpenModuleService.CloseAllModules(); } catch { }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int pt_x;
        public int pt_y;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetMessage(out NativeMessage lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool TranslateMessage(ref NativeMessage lpMsg);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr DispatchMessage(ref NativeMessage lpMsg);

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(uint idThread, uint Msg, IntPtr wParam, IntPtr lParam);



    public async Task WaitForStop()
    {
        await _stopped.Task;
    }



    // =================== POLL LOOP ===================

    private async Task PollLoop(CancellationToken ct)
    {

        string ip = SafeGetLocalIPv4Cached();

        while (!ct.IsCancellationRequested)
        {
            try
            {
              
                PollResponse resp = null;
                if (_wsClient != null && !_wsClient.IsConnected)
                {
                    if (DateTime.UtcNow - _lastWsRetry > TimeSpan.FromSeconds(60))
                    {
                        _lastWsRetry = DateTime.UtcNow;
                        _wsConnected = await _wsClient.ConnectAsync(ip, ct);
                        Info(_wsConnected ? "🔄 WebSocket reconnected" : "⏳ WebSocket still down, using HTTP");
                    }
                }
                bool useWs = _wsClient != null && _wsClient.IsConnected && _wsConnected;

                if (useWs)
                {
              
                    resp =await _wsClient.PollAsync(ip, ct);
                }
                else
                {
                  
                    resp =await _deviceService.PollAsync(ip, ct);
                }
                if (resp != null && resp.Commands != null && resp.Commands.Count > 0)
                {
                    List<InBoxDeviceMessage> reports =await ExecuteCommands(ip, resp.Commands);
                    if (reports.Count > 0)
                    {
                        if (useWs)
                        {
                            // 🔹 ارسال گزارش‌ها از WebSocket
                           await _wsClient.PollAsync(ip, reports, ct); // یا متد جدا مثل SendReports
                        }
                        else
                        {
                            // 🔹 ارسال گزارش‌ها با HTTP
                          await  _deviceService.PollAsync(ip, reports, ct);
                        }
                    }
                }

                await Task.Delay(5000,ct);
            }
            catch (TimeoutException)
            {
        
            }
            catch (Exception ex)
            {
                Error(ex, "❌ poll loop error");

             
         
               await Task.Delay(1000,ct); // backoff
            }
        }

        Info("PollLoop exited.");
    }

    // =================== METRICS LOOP ===================

    private async Task MetricsLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await SendMetricsOnce();
                var Status = XFSFunctionality.GetCassetInfo();
                Status.DeviceIp = SafeGetLocalIPv4Cached();
                var serilizedSys = JsonConvert.SerializeObject(Status);
                // Console.WriteLine(serilizedSys);
               await _deviceService.SendModuleStatusAsync(Status, ct);
                _logger.Info("Metircs And Modules Send ToServer SuccessFully");
            }
            catch (Exception ex)
            {
                //Error(ex, "❌ metrics loop error");
                await Task.Delay(30000, ct);
            }

            int jitter = _rnd.Next(-_metricsJitterSec, _metricsJitterSec + 1);
            int nextSec = Math.Max((int)_metricsMin.TotalSeconds, _metricsIntervalSec + jitter);
            await Task.Delay(nextSec * 1000, ct);
        }

        Info("MetricsLoop exited.");
    }
    // =================== Backup Journal LOOP ===================
    #region   Backup Journal LOOP
    private async Task JournalLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // هدف پایه: امروز ساعت 01:00
                DateTime now = DateTime.Now;
                DateTime today0100 = now.Date.AddHours(1);

                // جیتـِر روز جاری (0..30 دقیقه)
                int jitterMin = _rnd.Next(0, _ejournalMaxJitterMinutes + 1);
                DateTime target = today0100.AddMinutes(jitterMin);

                // اگر از پنجره امروز گذشت، ببر فردا 01:00 + جیتـِر جدید
                if (now > target)
                {
                    DateTime tomorrow0100 = now.Date.AddDays(1).AddHours(1);
                    jitterMin = _rnd.Next(0, _ejournalMaxJitterMinutes + 1);
                    target = tomorrow0100.AddMinutes(jitterMin);
                }

                // تا زمان هدف بخواب، با امکان Stop
                while (true)
                {
                    now = DateTime.Now;
                    TimeSpan remain = target - now;
                    if (remain <= TimeSpan.Zero) break;

                    // اسلیپ‌های تکه‌تکه تا Stop سریع عمل کند (حداکثر 60 ثانیه‌ای)
                    int chunkMs = (int)Math.Min(remain.TotalMilliseconds, 60_000);
                    if (!await DelayWithCancel(chunkMs, ct)) return; // استاپ شد
                }

                // زمان اجرای امروز/فردا رسیده
               await SendYesterdayJournalOnce(ct);
            }
            catch
            {
                // هر خطا → کمی عقب‌نشینی تا حلقه بی‌نهایت نشه
                if (! await DelayWithCancel(10_000, ct)) return;
            }

     
        }

        Info("JournalLoop exited.");
    }

    private async Task SendYesterdayJournalOnce(CancellationToken ct)
    {
        try
        {
            var ip = SafeGetLocalIPv4Cached();

            // کال کلاس ژورنال با متدی که ساختیم (nullable)
            string contentType, fileName;
            var gj = new GetJournals(_journalPath);
            byte[] data = gj.CollectYesterdayAsZipNullable(out contentType, out fileName);

            var payload = new JournalAckPayload
            {
                CommandId = Guid.Empty, // چون این ارسال خودکار است
                DataBase64 = (data != null && data.Length > 0) ? Convert.ToBase64String(data) : null,
                ContentType = (data != null && data.Length > 0) ? (contentType ?? "application/zip") : null,
                FileName = (data != null && data.Length > 0) ? (fileName ?? "journal.zip") : null,
                Message = (data != null && data.Length > 0) ? "Daily EJ (yesterday) sent" : "No EJ for yesterday"
            };

            var report = MakeMsg(ip, MessageType.EJournal, payload);
           await _deviceService.PollAsync(ip, new List<InBoxDeviceMessage> { report }, ct);

            Info("📤 Daily EJ sent at {0} (hasData={1})", DateTime.Now, data != null && data.Length > 0);
        }
        catch (Exception ex)
        {
            // گزارش خطا به سرور (اختیاری ولی مفید)
            try
            {
                var err = new { Error = ex.Message, Time = DateTime.UtcNow, Kind = "DailyEJ" };
                var msg = MakeMsg(SafeGetLocalIPv4Cached(), MessageType.ErrorReport, err);
               await _deviceService.PollAsync(_deviceIp, new List<InBoxDeviceMessage> { msg }, ct);
            }
            catch { /* ignore */ }
            // لاگ محلی
            //Error(ex, "❌ Daily EJ send failed");
        }
    }


    private async Task SendMetricsOnce(bool sendpollMetric = false, OutBoxDeviceMessage cmd = null)
    {
        NaraEyesAgent.Core.Models.Metrics.DeviceMetricsDto dto = DeviceMetrics.Capture(
            Guid.Empty,
            GetIpHelper.GetAgentVersion(),
            _pingTarget,
            800,
            false
        );
        dto.DeviceIp = SafeGetLocalIPv4Cached();
        var osInfo = OsInfoHelper.GetOsInfo();
        dto.OsFeatures = osInfo.Name + "-" + osInfo.Architecture;
        dto.AgentTime = DateTime.UtcNow;
       await _deviceService.SendMetricsAsync(dto, _stop.Token);
        if (sendpollMetric)
        {
            var pl = SafeDeserialize<CommandBaseUpload>(cmd.Payload);
            var message = MakeMsg(_deviceIp, MessageType.CommandAck, new CommandAckPayload { CommandId = pl.CommandId, Accepted = true, Message = "لیست متریک ها با موفقیت بروزرسانی شد." });
            var list = new List<InBoxDeviceMessage> { message };
          await  _deviceService.PollAsync(_deviceIp, list, _stop.Token);
        }
    }
    private InBoxDeviceMessage MakeMsg(string ip, MessageType t, object payload)
    {
        return new InBoxDeviceMessage
        {
            DeviceIp = ip,
            MessageType = t,
            Payload = JsonConvert.SerializeObject(payload)
        };
    }
    #endregion
    // =================== COMMAND EXECUTOR ===================
    #region Command Executor
    private async Task<System.Collections.Generic.List<InBoxDeviceMessage>> ExecuteCommands(string ip,
        System.Collections.Generic.List<OutBoxDeviceMessage> commands)
    {
        var reports = new System.Collections.Generic.List<InBoxDeviceMessage>();


        for (int i = commands.Count - 1; i >= 0; i--)
        {
            var cmd = commands[i];
            bool done = false;

            while (!done && !_stop.IsCancellationRequested)
            {
                try
                {
                    switch (cmd.CommandType)
                    {
                        case CommandsType.Screenshot:
                            {
                              

                          
                                int w, h;
                                byte[] jpg = NativeScreenCapture.CaptureAsJpeg(85, out w, out h);
                                var ack = new ScreenshotAckPayload
                                {
                                    CommandId = cmd.Id,
                                    ContentType = "image/jpeg",
                                    DataBase64 = Convert.ToBase64String(jpg),
                                    Width = w,
                                    Height = h
                                };
                                reports.Add(MakeMsg(ip, MessageType.ScreenshotAck, ack));
                                done = true;  // موفقیت، از حلقه خارج شو
                                break;
                                
                             
                            }
                        case CommandsType.Metrics:
                            {
                              await  SendMetricsOnce(true, cmd);
                                done = true;
                                break;
                            }
                        case CommandsType.ResetGroup:
                            {
                                var pl = SafeDeserialize<SendGroupInstructionModel>(cmd.Payload);

                                pl.Ip = ip;
                                reports.Add(MakeMsg(ip, MessageType.Group,
                                   pl));

                                   _ =
                                     Task.Run(async () =>
                                     {
                                         try
                                         {
                                             await DelayWithCancel(
                                                 3000,
                                                 _stop.Token);

                                             Exception err;

                                             SystemPowerManager.TryRestart(
                                                 0,
                                                 _stop.Token,
                                                 out err);
                                         }
                                         catch { }
                                     });

                                done = true;
                                break;

                            }
                        case CommandsType.UploadGroupFile:
                            {
                                var pl = SafeDeserialize<SendGroupInstructionModel>(cmd.Payload);
                                await SaveFiles.SaveFilesFromUrlAsync(pl.url, _stop.Token);

                                pl.Ip = ip;
                                reports.Add(MakeMsg(ip, MessageType.Group,
                                   pl));


                                done = true;
                                break;

                            }


                        case CommandsType.EJournal:
                            {
                                string startYmd = !IsNullOrWhiteSpace(cmd.StartDate) ? cmd.StartDate : DateTime.Now.ToString("yyyyMMdd");
                                string endYmd = !IsNullOrWhiteSpace(cmd.EndDate) ? cmd.EndDate : startYmd;

                                string ct, fn;
                                var journal = new GetJournals(_journalPath);
                                byte[] data = journal.Collect(startYmd, endYmd, out ct, out fn);

                                var ack = new JournalAckPayload
                                {
                                    CommandId = cmd.Id,
                                    DataBase64 = (data != null && data.Length > 0) ? Convert.ToBase64String(data) : null,
                                    ContentType = (data != null && data.Length > 0) ? ct : "text/plain",
                                    FileName = (data != null && data.Length > 0) ? fn : "no-journal.txt",
                                    Message = (data != null && data.Length > 0) ? null : "No journal files found"
                                };
                                reports.Add(MakeMsg(ip, MessageType.EJournal, ack));
                                done = true;  // موفقیت، از حلقه خارج شو
                                break;
                            }

                        // ---------- دستورات بدون ریترای (Side-effect) ----------
                        case CommandsType.Shutdown:
                            {
                                var pl = SafeDeserialize<CommandBasePayload>(cmd.Payload);
                                Guid id = (pl != null && pl.CommandId != Guid.Empty) ? pl.CommandId : cmd.Id;
                                int wait = (pl != null && pl.DelaySeconds.HasValue) ? pl.DelaySeconds.Value : 5;

                                reports.Add(MakeMsg(ip, MessageType.CommandAck,
                                    new CommandAckPayload { CommandId = id, Accepted = true, Message = "Shutdown scheduled in " + wait + "s" }));

                                _=Task.Run(async ()=> {
                                    try {await DelayWithCancel(wait * 1000, _stop.Token); Exception err; SystemPowerManager.TryShutdown(0, _stop.Token, out err); } catch { }
                                });
                                done = true;  // از حلقه خارج شو
                                break;
                            }

                        case CommandsType.Reset:
                            {
                                var pl = SafeDeserialize<CommandBasePayload>(cmd.Payload);
                                Guid id = (pl != null && pl.CommandId != Guid.Empty) ? pl.CommandId : cmd.Id;
                                int wait = (pl != null && pl.DelaySeconds.HasValue) ? pl.DelaySeconds.Value : 5;

                                reports.Add(MakeMsg(ip, MessageType.CommandAck,
                                    new CommandAckPayload { CommandId = id, Accepted = true, Message = "Reboot scheduled in " + wait + "s" }));

                                _ = Task.Run(async () => {
                                    try {await DelayWithCancel(wait * 1000, _stop.Token); Exception err; SystemPowerManager.TryRestart(0, _stop.Token, out err); } catch { }
                                });
                                done = true;  // از حلقه خارج شو
                                break;
                            }

                        case CommandsType.ResetCdm:
                            {
                                if (!await _cdmLock.WaitAsync(0)) // اگه در حال اجراست، رد کن
                                {
                                    reports.Add(MakeMsg(ip, MessageType.CommandAck,
                                        new CommandAckPayload
                                        {
                                            CommandId = cmd.Id,
                                            Accepted = true,
                                            Message = "Hardware busy, try again later"
                                        }));
                                    done = true;
                                    break;
                                }
                                try
                                {
                                    _ = RunSta(XFSFunctionality.ResteCdm)
                                          .ContinueWith(_ => _cdmLock.Release());
                                    var pl = SafeDeserialize<CommandBasePayload>(cmd.Payload);
                                                               pl ??= new();
                                    Guid id = (pl != null && pl.CommandId != Guid.Empty) ? pl.CommandId : cmd.Id;
                                    reports.Add(MakeMsg(ip, MessageType.CommandAck,
                                                               new CommandAckPayload { CommandId = id, Accepted = true, Message = "Resete Cdm scheduled" }));
                                }
                                catch { _cdmLock.Release(); }
                                done = true;
                                break;
                                //                            _ = RunSta(
                                //XFSFunctionality.ResteCdm);

                                //                            var pl = SafeDeserialize<CommandBasePayload>(cmd.Payload);
                                //                            pl ??= new();
                                //                            Guid id = (pl != null && pl.CommandId != Guid.Empty) ? pl.CommandId : cmd.Id;
                                //                            reports.Add(MakeMsg(ip, MessageType.CommandAck,
                                //                                new CommandAckPayload { CommandId = id, Accepted = true, Message = "Resete Cdm scheduled" }));
                                //                            done = true;  // از حلقه خارج شو
                                break;
                            }

                        case CommandsType.resetIdc:
                            {
                                var pl = SafeDeserialize<CommandBasePayload>(cmd.Payload);
                                pl ??= new();
                                Guid id = (pl.CommandId != Guid.Empty) ? pl.CommandId : cmd.Id;

                                if (!await _idcLock.WaitAsync(0))
                                {
                                    reports.Add(MakeMsg(ip, MessageType.CommandAck,
                                        new CommandAckPayload
                                        {
                                            CommandId = id,
                                            Accepted = true,
                                            Message = "Hardware busy, try again later"
                                        }));
                                    done = true;
                                    break;
                                }

                                _ = RunSta(XFSFunctionality.ReseteIDC)
                                      .ContinueWith(_ => _idcLock.Release());

                                reports.Add(MakeMsg(ip, MessageType.CommandAck,
                                    new CommandAckPayload
                                    {
                                        CommandId = id,
                                        Accepted = true,
                                        Message = "Reset IDC scheduled"
                                    }));
                                done = true;
                                break;

                                //                                _ = RunSta(
                                //XFSFunctionality.ReseteIDC);

                                //                                var pl = SafeDeserialize<CommandBasePayload>(cmd.Payload);
                                //                                Guid id = (pl != null && pl.CommandId != Guid.Empty) ? pl.CommandId : cmd.Id;
                                //                                reports.Add(MakeMsg(ip, MessageType.CommandAck,
                                //                                    new CommandAckPayload { CommandId = id, Accepted = true, Message = "Resete Idc scheduled" }));
                                //                                done = true;  // از حلقه خارج شو
                                //                                break;
                            }

                        case CommandsType.testprinter:
                            {
                                var pl = SafeDeserialize<CommandBasePayload>(cmd.Payload);
                                pl ??= new();
                                Guid id = (pl.CommandId != Guid.Empty) ? pl.CommandId : cmd.Id;

                                if (!await _printerLock.WaitAsync(0))
                                {
                                    reports.Add(MakeMsg(ip, MessageType.CommandAck,
                                        new CommandAckPayload
                                        {
                                            CommandId = id,
                                            Accepted = true,
                                            Message = "Hardware busy, try again later"
                                        }));
                                    done = true;
                                    break;
                                }

                                _ = RunSta(XFSFunctionality.Resetptr)
                                      .ContinueWith(_ => _printerLock.Release());

                                reports.Add(MakeMsg(ip, MessageType.CommandAck,
                                    new CommandAckPayload
                                    {
                                        CommandId = id,
                                        Accepted = true,
                                        Message = "Printer test scheduled"
                                    }));
                                done = true;
                                break;
                                //                                _ = RunSta(
                                //XFSFunctionality.Resetptr);


                                //                                var pl = SafeDeserialize<CommandBasePayload>(cmd.Payload);
                                //                                Guid id = (pl != null && pl.CommandId != Guid.Empty) ? pl.CommandId : cmd.Id;
                                //                                reports.Add(MakeMsg(ip, MessageType.CommandAck,
                                //                                    new CommandAckPayload { CommandId = id, Accepted = true, Message = "Printer test scheduled" }));
                                //                                done = true;  // از حلقه خارج شو
                                //                                break;
                            }

                        case CommandsType.UploadFile:
                            {
                                var pl = SafeDeserialize<CommandBaseUpload>(cmd.Payload);
                                SaveFiles.SaveBase64Files(pl);
                                Guid id = (pl != null && pl.CommandId != Guid.Empty) ? pl.CommandId : cmd.Id;
                                reports.Add(MakeMsg(ip, MessageType.CommandAck,
                                    new CommandAckPayload { CommandId = id, Accepted = true, Message = "Upload successfully" }));
                                done = true;  // از حلقه خارج شو
                                break;
                            }
                        case CommandsType.GetForcesStatus:
                            {
                                try
                                {

                              
                                var pl = SafeDeserialize<CommandBaseUpload>(cmd.Payload);
                                var Status = XFSFunctionality.GetCassetInfo();
                                Status.DeviceIp = SafeGetLocalIPv4Cached();
                                var serilizedSys = JsonConvert.SerializeObject(Status);

                             await   _deviceService.SendModuleStatusAsync(Status, _stop.Token);
                                Guid id = (pl != null && pl.CommandId != Guid.Empty) ? pl.CommandId : cmd.Id;
                                reports.Add(MakeMsg(ip, MessageType.CommandAck,
                                    new CommandAckPayload { CommandId = id, Accepted = true, Message = "Upload successfully" }));
                                done = true;  // از حلقه خارج شو
                                break;
                                }
                                catch (Exception EX)
                                {
                                    done = true;
                                    Console.WriteLine( EX.Message);
                                  break;
                                }
                            }
                        default:
                            {
                                var ack = new { CommandId = cmd.Id, Ok = true, Time = DateTime.UtcNow };
                                reports.Add(MakeMsg(ip, MessageType.CommandAck, ack));
                                done = true;  // از حلقه خارج شو
                                break;
                            }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"looperror:{ex}");
                    commands.RemoveAt(i);
                    done = true;

                    var err = new { CommandId = cmd.Id, Error = ex.Message, Time = DateTime.UtcNow };
                    var message = MakeMsg(ip, MessageType.ErrorReport, err);
                    var list = new List<InBoxDeviceMessage> { message };
                  await  _deviceService.PollAsync(ip, list, _stop.Token);
                }
            }
        }




        return reports;
    }
    #endregion
    // =================== HELPERS ===================

    #region Helpers
    private void LoadConfig()
    {
        try
        {
            try
            {
                _config = ConfigLoader.Load(out _configPathUsed);

                _apiBase = _config.ApiBase;
                _pingTarget = string.IsNullOrEmpty(_config.PingTarget) ? _config.ApiBase : _config.PingTarget;
                _journalPath = !string.IsNullOrEmpty(_config.JournalPath) ? _config.JournalPath : _config.EJournalFallback;
                _terminalCode = int.Parse(!string.IsNullOrEmpty(_config.TerminalCode) && _config.TerminalCode != "0" ? _config.TerminalCode : Random.Shared.Next(1433, 9999).ToString());


                if (IsNullOrWhiteSpace(_apiBase)) _apiBase = "";
                if (IsNullOrWhiteSpace(_pingTarget)) _pingTarget = _apiBase;
                if (IsNullOrWhiteSpace(_journalPath)) _journalPath = @"D:\ejournal";
            }
            catch (Exception ex) { }

        }
        catch (Exception ex)
        {
            Error(ex, "config load failed");
        }
    }
    private static Task RunSta(
Action action)
    {
        var tcs =
            new TaskCompletionSource<bool>();

        var t =
            new Thread(
                () =>
                {
                    try
                    {
                        action();

                        tcs.SetResult(
                            true);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(
                            ex);
                    }
                });

        t.SetApartmentState(
            ApartmentState.STA);
        t.IsBackground = true;
        t.Start();

        return tcs.Task;
    }
    private T SafeDeserialize<T>(string json) where T : class, new()
    {
        if (json == null || json.Length == 0) return new T();
        try { return JsonConvert.DeserializeObject<T>(json); }
        catch { return new T(); }
    }

    private void Info(string msg, params object[] args)
    {
        try { Console.WriteLine("[INFO] " + string.Format(msg, args)); } catch { }
    }

    private void Warn(string msg, params object[] args)
    {
        try { Console.WriteLine("[WARN] " + string.Format(msg, args)); } catch { }
    }

    private void Error(Exception ex, string msg, params object[] args)
    {
        try
        {
            Console.WriteLine("[ERR ] " + string.Format(msg, args));
            Console.WriteLine("       " + ex.GetType().FullName + ": " + ex.Message);
        }
        catch { }
    }

    private static async Task<bool> DelayWithCancel(
       int milliseconds,
       CancellationToken cancel)
    {
        if (milliseconds <= 0)
            return true;

        try
        {
            await Task.Delay(
                milliseconds,
                cancel);

            return true; // کامل صبر کرد
        }
        catch (OperationCanceledException)
        {
            return false; // لغو شد
        }
    }

    private string SafeGetLocalIPv4Cached()
    {
        if (!IsNullOrWhiteSpace(_deviceIp)) return _deviceIp;
        try { _deviceIp = GetIpHelper.GetLocalIPv4(); }
        catch { _deviceIp = "0.0.0.0"; }
        return _deviceIp;
    }

    private static bool IsNullOrWhiteSpace(string s)
    {
        if (s == null) return true;
        for (int i = 0; i < s.Length; i++)
            if (!char.IsWhiteSpace(s[i])) return false;
        return true;
    }
    private Task RunStaLoop(Action<CancellationToken> action)
    {
        var tcs = new TaskCompletionSource();

        var t = new Thread(() =>
        {
            _pumpThreadId = GetCurrentThreadId();
            try
            {
                action(_stop.Token);
                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        t.SetApartmentState(ApartmentState.STA);
        t.IsBackground = true;
        t.Start();

        return tcs.Task;
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
    private volatile uint _pumpThreadId;
    #endregion
}
