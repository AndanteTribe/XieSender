using System;
using System.Diagnostics;

namespace XieSender;

/// <summary>XieClient の動作設定。</summary>
public sealed record XieClientOptions
{
    private readonly uint _userIndex;
    private readonly int _targetHz = Math.Min(1000, (int)Stopwatch.Frequency);
    private readonly int? _cpuCoreAffinity;

    /// <summary>XInput ユーザーインデックス（0–3）。デフォルト 0。</summary>
    public uint UserIndex
    {
        get => _userIndex;
        init
        {
            if (value > 3)
            {
                throw new ArgumentOutOfRangeException(nameof(UserIndex), value, "XInput user index must be between 0 and 3.");
            }
            _userIndex = value;
        }
    }

    /// <summary>送信レート（Hz）。デフォルト 1000。</summary>
    public int TargetHz
    {
        get => _targetHz;
        init
        {
            if (value <= 0 || value > Stopwatch.Frequency)
            {
                throw new ArgumentOutOfRangeException(nameof(TargetHz), value, $"TargetHz must be between 1 and {Stopwatch.Frequency}.");
            }
            _targetHz = value;
        }
    }

    /// <summary>
    /// 送信スレッドを固定する CPU コア番号（0 始まり）。
    /// null の場合は OS のスケジューラに委ねる。
    /// </summary>
    public int? CpuCoreAffinity
    {
        get => _cpuCoreAffinity;
        init
        {
            var maxCpuCoreAffinity = Math.Min(Environment.ProcessorCount, IntPtr.Size * 8) - 1;
            if (value is < 0 || value > maxCpuCoreAffinity)
            {
                throw new ArgumentOutOfRangeException(nameof(CpuCoreAffinity), value, $"CpuCoreAffinity must be null or between 0 and {maxCpuCoreAffinity}.");
            }
            _cpuCoreAffinity = value;
        }
    }
}