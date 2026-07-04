using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;

namespace XieSender;

/// <summary>
/// XInput コントローラーの入力を UDP で継続送信するクライアント。
/// <para>
/// ライフサイクルは <see cref="Dispose"/> で制御する。
/// Dispose を呼ぶと <c>await foreach</c> が自然に完了する。
/// </para>
/// </summary>
public sealed class XieClient : IDisposable
{
    private readonly IPEndPoint _endpoint;
    private readonly XieClientOptions _options;
    private readonly long _intervalTicks;
    private readonly long _startTick;
    private readonly object _disposeLock = new();
    private readonly CancellationTokenSource _disposeCts = new();
    private int _runStreamStarted;

    // WouldBlock によるドロップ数（SendLoop スレッドとの共有）
    private long _droppedPackets;

    // -----------------------------------------------------------------------
    // 構築
    // -----------------------------------------------------------------------

    /// <summary>
    /// ホスト名と UDP ポートを指定してクライアントを生成する。
    /// </summary>
    public XieClient(string host, int port, XieClientOptions? options = null)
        : this(new IPEndPoint(IPAddress.Parse(host), port), options)
    {
    }

    /// <summary>
    /// 送信先エンドポイントを直接指定してクライアントを生成する。
    /// </summary>
    public XieClient(IPEndPoint endpoint, XieClientOptions? options = null)
    {
        _endpoint = endpoint;
        _options = options ?? new XieClientOptions();
        _intervalTicks = Stopwatch.Frequency / _options.TargetHz;
        _startTick = Stopwatch.GetTimestamp();
    }

    // -----------------------------------------------------------------------
    // 公開 API
    // -----------------------------------------------------------------------

    /// <summary>
    /// 送信ループを開始し、接続状態の変化やエラーを <see cref="XieEvent"/> として非同期列挙する。
    /// <para>
    /// <see cref="Dispose"/> を呼ぶと送信を停止し、列挙が完了する。
    /// </para>
    /// </summary>
    /// <param name="cancellationToken">列挙と送信ループをキャンセルするトークン。</param>
    public async IAsyncEnumerable<XieEvent> RunStreamAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Interlocked.CompareExchange(ref _runStreamStarted, 1, 0) != 0)
        {
            throw new InvalidOperationException("RunStreamAsync can only be called once.");
        }

        CancellationTokenSource runCts;
        lock (_disposeLock)
        {
            ObjectDisposedException.ThrowIf(_disposeCts.IsCancellationRequested, this);
            runCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token, cancellationToken);
        }

        Socket? socket = null;
        Thread? thread = null;
        var threadStarted = false;

        try
        {
            // ソケット生成
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(_endpoint);
            socket.Blocking = false;

            // SendLoop（専用スレッド）→ RunStreamAsync（呼び出し元）へのイベント橋渡し
            // イベントは稀なので容量 64 で十分。溢れた場合は古いものを破棄する。
            var channel = Channel.CreateBounded<XieEvent>(new BoundedChannelOptions(64)
            {
                SingleWriter = true,
                SingleReader = true,
                FullMode = BoundedChannelFullMode.DropOldest,
            });

            var loopSocket = socket;
            var ct = runCts.Token;
            thread = new Thread(() => SendLoop(loopSocket, channel.Writer, ct))
            {
                IsBackground = true,
                Priority = ThreadPriority.Highest,
                Name = $"XieSendLoop_{_options.UserIndex}",
            };
            thread.Start();
            threadStarted = true;

            // Dispose は channel 完了で自然終了し、外部キャンセルは OperationCanceledException で抜ける。
            await foreach (var ev in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return ev;
            }
        }
        finally
        {
            // 列挙キャンセルや途中 break でも送信ループとソケットを確実に片付ける。
            runCts.Cancel();

            if (threadStarted)
            {
                thread!.Join();
            }

            socket?.Close();
            runCts.Dispose();
        }
    }

    // -----------------------------------------------------------------------
    // 内部実装
    // -----------------------------------------------------------------------

    private unsafe void SendLoop(Socket socket, ChannelWriter<XieEvent> writer, CancellationToken ct)
    {
        // CPU コアアフィニティの適用（オプション）
        if (_options.CpuCoreAffinity.HasValue)
        {
            var mask = (nuint)(1 << _options.CpuCoreAffinity.Value);
            Kernel32.SetThreadAffinityMask(Kernel32.GetCurrentThread(), mask);
        }

        long baseTick = 0;
        long loopCount = 0;
        var wasConnected = false;
        ushort sampleId = 0;
        byte heartbeat = 0;

        // インターバルの半分より余裕があれば Sleep(0) でCPUを手放す
        var yieldThreshold = _intervalTicks / 2;

        Span<byte> sendBuffer = stackalloc byte[XieProtocol.XIE_PACKET_SIZE];

        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (!wasConnected)
                {
                    // 未接続: 低頻度ポーリングで待機
                    XINPUT_STATE state;
                    if (XInput.XInputGetState((uint)_options.UserIndex, &state) != 0)
                    {
                        if (ct.WaitHandle.WaitOne(500))
                        {
                            return;
                        }

                        continue;
                    }

                    wasConnected = true;
                    writer.TryWrite(new ControllerConnected(_options.UserIndex));

                    baseTick = Stopwatch.GetTimestamp();
                    loopCount = 0;
                    sampleId = 0;

                    SendPacket(socket, sendBuffer, ref state.Gamepad, ref sampleId, ref heartbeat, writer);
                    loopCount++;
                }
                else
                {
                    // 接続中: ハイブリッドウェイトで指定レートを維持
                    var target = baseTick + loopCount * _intervalTicks;

                    while (true)
                    {
                        var now = Stopwatch.GetTimestamp();
                        if (now >= target)
                        {
                            break;
                        }

                        if (target - now > yieldThreshold)
                        {
                            Thread.Sleep(0);
                        }
                        else
                        {
                            Thread.SpinWait(10);
                        }
                    }

                    XINPUT_STATE state;
                    if (XInput.XInputGetState((uint)_options.UserIndex, &state) != 0)
                    {
                        wasConnected = false;
                        writer.TryWrite(new ControllerDisconnected(_options.UserIndex));
                        continue;
                    }

                    SendPacket(socket, sendBuffer, ref state.Gamepad, ref sampleId, ref heartbeat, writer);
                    loopCount++;
                }
            }
        }
        finally
        {
            // ここが呼ばれると ReadAllAsync が完了し RunStreamAsync が return する
            writer.Complete();
        }
    }

    [ExcludeFromCodeCoverage]
    private unsafe void SendPacket(
        Socket socket,
        Span<byte> buffer,
        ref XINPUT_GAMEPAD gamepad,
        ref ushort sampleId,
        ref byte heartbeat,
        ChannelWriter<XieEvent> writer)
    {
        heartbeat ^= XieProtocol.XIE_FLAG_HEARTBEAT;

        fixed (byte* ptr = buffer)
        {
            var p = (XiePacket*)ptr;
            p->magic = XieProtocol.XIE_MAGIC;
            p->version = XieProtocol.XIE_VERSION;
            p->typeAndFlags = XieProtocol.MakeTypeFlags(XieProtocol.XIE_TYPE_GAMEPAD, heartbeat);
            p->sampleId = sampleId++;
            p->timestampUs = GetMonotonicUs();
            p->lx = gamepad.sThumbLX;
            p->ly = gamepad.sThumbLY;
            p->rx = gamepad.sThumbRX;
            p->ry = gamepad.sThumbRY;
            p->lt = gamepad.bLeftTrigger;
            p->rt = gamepad.bRightTrigger;
            p->buttons = gamepad.wButtons;
        }

        try
        {
            socket.Send(buffer, SocketFlags.None);
        }
        catch (SocketException ex)
        {
            if (ex.SocketErrorCode == SocketError.WouldBlock)
            {
                // 高頻度で発生しうる軽微な事象: カウントのみ、イベントは発行しない
                Interlocked.Increment(ref _droppedPackets);
            }
            else
            {
                // 深刻なエラー: 累積ドロップ数を添えてイベントを発行
                writer.TryWrite(new SendError(ex, Interlocked.Read(ref _droppedPackets)));
                Thread.Sleep(100);
            }
        }
        catch (Exception ex)
        {
            writer.TryWrite(new SendError(ex, Interlocked.Read(ref _droppedPackets)));
            Thread.Sleep(100);
        }
    }

    private uint GetMonotonicUs()
    {
        var elapsed = Stopwatch.GetTimestamp() - _startTick;
        return (uint)((elapsed * 1_000_000) / Stopwatch.Frequency);
    }

    // -----------------------------------------------------------------------
    // IDisposable
    // -----------------------------------------------------------------------

    /// <summary>
    /// 送信ループを停止する。
    /// <c>using</c> または明示的な呼び出しで使用する。
    /// </summary>
    public void Dispose()
    {
        lock (_disposeLock)
        {
            if (_disposeCts.IsCancellationRequested)
            {
                return;
            }

            _disposeCts.Cancel();
            _disposeCts.Dispose();
        }
    }
}