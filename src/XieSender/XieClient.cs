using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
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
    private readonly CancellationTokenSource _disposeCts = new();

    // WouldBlock によるドロップ数（SendLoop スレッドとの共有）
    private long _droppedPackets;

    // タイムスタンプの起点（プロセス起動時刻）
    private static readonly long s_startTick = Stopwatch.GetTimestamp();

    // -----------------------------------------------------------------------
    // 構築
    // -----------------------------------------------------------------------

    /// <summary>ホスト名と UDP ポートを指定してクライアントを生成する。</summary>
    public XieClient(string host, int port, XieClientOptions? options = null)
        : this(new IPEndPoint(IPAddress.Parse(host), port), options)
    {
    }

    /// <summary>送信先エンドポイントを直接指定してクライアントを生成する。</summary>
    public XieClient(IPEndPoint endpoint, XieClientOptions? options = null)
    {
        _endpoint = endpoint;
        _options = options ?? new XieClientOptions();
        _intervalTicks = Stopwatch.Frequency / _options.TargetHz;
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
    public async IAsyncEnumerable<XieEvent> RunStreamAsync()
    {
        var ct = _disposeCts.Token;

        // ソケット生成
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Connect(_endpoint);
        socket.Blocking = false;

        // SendLoop（専用スレッド）→ RunStreamAsync（呼び出し元）へのイベント橋渡し
        // イベントは稀なので容量 64 で十分。溢れた場合は古いものを破棄する
        var channel = Channel.CreateBounded<XieEvent>(new BoundedChannelOptions(64)
        {
            SingleWriter = true,
            SingleReader = true,
            FullMode = BoundedChannelFullMode.DropOldest,
        });

        var thread = new Thread(() => SendLoop(socket, channel.Writer, ct))
        {
            IsBackground = true,
            Priority = ThreadPriority.Highest,
            Name = $"XieSendLoop_{_options.UserIndex}",
        };
        thread.Start();

        // SendLoop が writer.Complete() を呼ぶまで読み続ける
        await foreach (var ev in channel.Reader.ReadAllAsync(CancellationToken.None))
        {
            yield return ev;
        }

        // スレッドの終了を待ってからソケットを閉じる
        thread.Join();
        socket.Close();
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
        bool wasConnected = false;
        ushort sampleId = 0;
        byte heartbeat = 0;

        // インターバルの半分より余裕があれば Sleep(0) でCPUを手放す
        long yieldThreshold = _intervalTicks / 2;

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
                        Thread.Sleep(500);
                        continue;
                    }

                    wasConnected = true;
                    writer.TryWrite(new ControllerConnected(_options.UserIndex));

                    baseTick  = Stopwatch.GetTimestamp();
                    loopCount = 0;
                    sampleId  = 0;

                    SendPacket(socket, sendBuffer, ref state.Gamepad,
                               ref sampleId, ref heartbeat, writer);
                    loopCount++;
                }
                else
                {
                    // 接続中: ハイブリッドウェイトで指定レートを維持
                    long target = baseTick + loopCount * _intervalTicks;

                    while (true)
                    {
                        long now = Stopwatch.GetTimestamp();
                        if (now >= target)
                            break;

                        if (target - now > yieldThreshold)
                            Thread.Sleep(0);    // 余裕があれば OS に譲る
                        else
                            Thread.SpinWait(10); // 終端の微調整はスピン
                    }

                    XINPUT_STATE state;
                    if (XInput.XInputGetState((uint)_options.UserIndex, &state) != 0)
                    {
                        wasConnected = false;
                        writer.TryWrite(new ControllerDisconnected(_options.UserIndex));
                        continue;
                    }

                    SendPacket(socket, sendBuffer, ref state.Gamepad,
                               ref sampleId, ref heartbeat, writer);
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

    private static uint GetMonotonicUs()
    {
        long elapsed = Stopwatch.GetTimestamp() - s_startTick;
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
        _disposeCts.Cancel();
        _disposeCts.Dispose();
    }
}