using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SecsCore.Codec;
using SecsCore.Extensions; // 引用 SmlFormatter

namespace SecsCore.Transport;

public enum ConnectionState { Disconnected, Listening, Connected, Selected, RetryDelay }

public class HsmsConnection : IDisposable
{
    private readonly SecsOptions _options;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts = new();

    private Socket? _socket;         // 通信用的 Socket
    private Socket? _listenerSocket; // Passive 模式专用的监听 Socket

    // 状态管理
    private volatile ConnectionState _state = ConnectionState.Disconnected;
    public ConnectionState State
    {
        get => _state;
        private set
        {
            if (_state != value)
            {
                _state = value;
                OnStateChanged?.Invoke(_state);
                _logger.LogInformation("State Changed: {State}", _state);
            }
        }
    }

    private static int _systemBytesCounter = 0;
    private readonly ConcurrentDictionary<uint, TaskCompletionSource<SecsMessage>> _transactions = new();

    // 事件
    public event Action<ConnectionState>? OnStateChanged;
    public event Action<SecsMessage>? OnPrimaryMessageReceived;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">配置项</param>
    /// <param name="logger">日志接口 (可选)</param>
    public HsmsConnection(SecsOptions options, ILogger? logger = null)
    {
        _options = options;
        _logger = logger ?? NullLogger.Instance; // 如果没有传 logger，就用空实现，不报错
    }

    // --- 1. 启动接口 ---

    public void Start()
    {
        _logger.LogInformation("Starting Driver in {Mode} mode on {Ip}:{Port}", _options.Mode, _options.IpAddress, _options.Port);

        // 启动后台维护线程
        _ = Task.Factory.StartNew(MaintenanceLoopAsync, TaskCreationOptions.LongRunning);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _socket?.Dispose();
        _listenerSocket?.Dispose();
    }

    // --- 2. 智能维护循环 (The Brain) ---

    private async Task MaintenanceLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                if (_options.Mode == HsmsConnectionMode.Active)
                {
                    await RunActiveModeAsync();
                }
                else
                {
                    await RunPassiveModeAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Maintenance Loop Critical Error");
            }

            // 如果异常断开，清理现场并冷却
            Cleanup();

            if (!_cts.IsCancellationRequested)
            {
                State = ConnectionState.RetryDelay;
                _logger.LogDebug("Waiting {Time}ms before retry...", _options.T5);
                await Task.Delay(_options.T5, _cts.Token);
            }
        }
    }

    // --- 模式 A: 主动连接 (Active) ---
    private async Task RunActiveModeAsync()
    {
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        _logger.LogInformation("Active Mode: Connecting to {Ip}:{Port}...", _options.IpAddress, _options.Port);
        using var connectCts = new CancellationTokenSource(5000);
        await _socket.ConnectAsync(IPAddress.Parse(_options.IpAddress), _options.Port, connectCts.Token);

        await HandleConnectedSessionAsync();
    }

    // --- 模式 B: 被动监听 (Passive) ---
    private async Task RunPassiveModeAsync()
    {
        State = ConnectionState.Listening;

        _listenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _listenerSocket.Bind(new IPEndPoint(IPAddress.Parse(_options.IpAddress), _options.Port));
        _listenerSocket.Listen(1); // 这里的 1 表示只允许一个客户端排队 (SECS 通常是 1对1)

        _logger.LogInformation("Passive Mode: Listening on {Ip}:{Port}...", _options.IpAddress, _options.Port);

        // 等待客户端连接
        _socket = await _listenerSocket.AcceptAsync(_cts.Token);
        _logger.LogInformation("Accepted connection from {Remote}", _socket.RemoteEndPoint);

        // 连上后，关闭监听器 (通常 SECS 是点对点，连上一个就专心服务，不再接受别的)
        // 这里的策略可以根据需求改，专家建议：连上后暂停监听，断开后再重新 Listen
        _listenerSocket.Close();

        await HandleConnectedSessionAsync();
    }

    // --- 通用会话处理 (Active/Passive 共用) ---
    private async Task HandleConnectedSessionAsync()
    {
        State = ConnectionState.Connected;
        _logger.LogInformation("TCP Established. Starting Pipeline.");

        // 启动心跳定时器
        _ = LinkTestLoopAsync();

        // 如果是 Active 模式，立即握手
        // 如果是 Passive 模式，等待对方发 Select.Req
        if (_options.Mode == HsmsConnectionMode.Active)
        {
            _ = SendSelectRequestAsync();
        }

        // 阻塞在这里，直到断开
        await ProcessPipelineAsync();
    }

    private void Cleanup()
    {
        State = ConnectionState.Disconnected;
        try { _socket?.Shutdown(SocketShutdown.Both); } catch { }
        _socket?.Close();
        _transactions.Clear();
    }

    // --- 3. 管道处理 ---

    private async Task ProcessPipelineAsync()
    {
        var pipe = new Pipe();
        var fillTask = FillPipeAsync(pipe.Writer);
        var readTask = ReadPipeAsync(pipe.Reader);
        await Task.WhenAny(fillTask, readTask);
    }

    private async Task FillPipeAsync(PipeWriter writer)
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                var memory = writer.GetMemory(1024);
                int bytesRead = await _socket!.ReceiveAsync(memory, SocketFlags.None, _cts.Token);
                if (bytesRead == 0) break;
                writer.Advance(bytesRead);
            }
            catch { break; }
            var result = await writer.FlushAsync();
            if (result.IsCompleted) break;
        }
        await writer.CompleteAsync();
    }

    private async Task ReadPipeAsync(PipeReader reader)
    {
        while (!_cts.IsCancellationRequested)
        {
            var result = await reader.ReadAsync();
            var buffer = result.Buffer;
            while (HsmsDecoder.TryDecode(ref buffer, out var msg))
            {
                if (msg != null) HandleMessage(msg);
            }
            reader.AdvanceTo(buffer.Start, buffer.End);
            if (result.IsCompleted) break;
        }
        await reader.CompleteAsync();
    }

    // --- 4. 业务逻辑 ---

    private async Task SendSelectRequestAsync()
    {
        try
        {
            var selectReq = new SecsMessage(0, 0, false)
            {
                Header = new SecsHeader
                {
                    SessionId = _options.DeviceId,
                    SType = HsmsType.SelectReq,
                    SystemBytes = GetNextSystemBytes()
                }
            };
            var rsp = await SendRequestAsync(selectReq);
            if (rsp.Header.SType == HsmsType.SelectRsp)
            {
                State = ConnectionState.Selected;
                _logger.LogInformation("HSMS Selected.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Handshake Failed: {Message}", ex.Message);
            _socket?.Close(); // 触发重连
        }
    }

    public async Task<SecsMessage> SendRequestAsync(SecsMessage request)
    {
        if (State != ConnectionState.Connected && State != ConnectionState.Selected)
            throw new InvalidOperationException("Not connected");

        if (request.Header.SystemBytes == 0)
        {
            var header = request.Header;
            request.Header = header with { SystemBytes = GetNextSystemBytes(), SessionId = _options.DeviceId };
        }

        var tcs = new TaskCompletionSource<SecsMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _transactions.TryAdd(request.Header.SystemBytes, tcs);

        try
        {
            await SendRawAsync(request);
            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(_options.T3));

            if (completedTask == tcs.Task) return await tcs.Task;
            else throw new TimeoutException($"T3 Timeout (SB={request.Header.SystemBytes})");
        }
        finally
        {
            _transactions.TryRemove(request.Header.SystemBytes, out _);
        }
    }

    public async Task SendReplyAsync(SecsMessage reply, SecsMessage primary)
    {
        var header = reply.Header;
        reply.Header = header with { SystemBytes = primary.Header.SystemBytes, SessionId = _options.DeviceId };
        await SendRawAsync(reply);
    }

    private async Task SendRawAsync(SecsMessage msg)
    {
        if (_socket == null || !_socket.Connected) throw new SocketException((int)SocketError.NotConnected);

        var writer = new ArrayBufferWriter<byte>(1024);
        EncodeMessageToBuffer(msg, writer);
        await _socket.SendAsync(writer.WrittenMemory, SocketFlags.None);

        // 专家日志：SML 输出
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            // 只有 Debug 级别才打 SML，防止刷屏
            if (msg.Body != null)
                _logger.LogDebug("[SEND] {Msg}{NewLine}{Sml}", msg, Environment.NewLine, msg.Body.ToSml());
            else
                _logger.LogDebug("[SEND] {Msg}", msg);
        }
        else
        {
            _logger.LogInformation("[SEND] {Msg}", msg);
        }
    }

    // 复用之前的 EncodeMessageToBuffer (含 Span 处理)
    private void EncodeMessageToBuffer(SecsMessage msg, ArrayBufferWriter<byte> writer)
    {
        // ... (保持之前的同步代码不变) ...
        // 为了篇幅，请确保这里使用的是包含 MemoryMarshal 的正确版本
        var lengthSpan = writer.GetSpan(4);
        writer.Advance(4);
        var headerSpan = writer.GetSpan(10);
        msg.Header.EncodeTo(headerSpan);
        writer.Advance(10);
        if (msg.Body != null) msg.Body.Encode(writer);
        uint totalLength = (uint)(writer.WrittenCount - 4);
        var writtenMemory = writer.WrittenMemory;
        var backingSpan = MemoryMarshal.AsMemory(writtenMemory).Span;
        BinaryPrimitives.WriteUInt32BigEndian(backingSpan.Slice(0, 4), totalLength);
    }

    private void HandleMessage(SecsMessage msg)
    {
        // 专家日志：SML 接收输出
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            if (msg.Body != null)
                _logger.LogDebug("[RECV] {Msg} (Type={Type})\n{Sml}", msg, msg.Header.SType, msg.Body.ToSml());
            else
                _logger.LogDebug("[RECV] {Msg} (Type={Type})", msg, msg.Header.SType);
        }

        // 1. 控制消息处理
        if (msg.Header.SType == HsmsType.SelectReq)
        {
            // 作为 Passive 端，收到 Select.Req，必须回 Select.Rsp
            var rsp = new SecsMessage(0, 0, false) { Header = new SecsHeader { SType = HsmsType.SelectRsp } };
            _ = SendReplyAsync(rsp, msg);
            State = ConnectionState.Selected;
            _logger.LogInformation("Handshake Accepted (Passive).");
            return;
        }
        else if (msg.Header.SType == HsmsType.SelectRsp)
        {
            if (_transactions.TryRemove(msg.Header.SystemBytes, out var tcs)) tcs.SetResult(msg);
            return;
        }
        else if (msg.Header.SType == HsmsType.LinktestReq)
        {
            var rsp = new SecsMessage(0, 0, false) { Header = new SecsHeader { SType = HsmsType.LinktestRsp } };
            _ = SendReplyAsync(rsp, msg);
            return;
        }

        // 2. 数据消息处理
        if (msg.Header.SType == HsmsType.DataMessage)
        {
            if (_transactions.TryGetValue(msg.Header.SystemBytes, out var pendingTcs))
            {
                _transactions.TryRemove(msg.Header.SystemBytes, out _);
                pendingTcs.SetResult(msg);
            }
            else
            {
                OnPrimaryMessageReceived?.Invoke(msg);
            }
        }
    }

    // 自动心跳循环
    private async Task LinkTestLoopAsync()
    {
        while (!_cts.IsCancellationRequested && _socket != null && _socket.Connected)
        {
            await Task.Delay(_options.LinkTestInterval, _cts.Token);
            if (State == ConnectionState.Selected) // 只有 Selected 才需要心跳
            {
                try
                {
                    // 发送 LinkTest.Req (Active/Passive 都可以发)
                    var req = new SecsMessage(0, 0, false)
                    {
                        Header = new SecsHeader { SType = HsmsType.LinktestReq, SessionId = _options.DeviceId, SystemBytes = GetNextSystemBytes() }
                    };
                    await SendRawAsync(req);
                }
                catch { /* 忽略心跳发送错误 */ }
            }
        }
    }

    private uint GetNextSystemBytes() => (uint)Interlocked.Increment(ref _systemBytesCounter);
}