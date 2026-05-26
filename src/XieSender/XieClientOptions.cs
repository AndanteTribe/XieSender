namespace XieSender;

/// <summary>XieClient の動作設定。</summary>
public sealed record XieClientOptions
{
    /// <summary>XInput ユーザーインデックス（0–3）。デフォルト 0。</summary>
    public int UserIndex { get; init; } = 0;

    /// <summary>送信レート（Hz）。デフォルト 1000。</summary>
    public int TargetHz { get; init; } = 1000;

    /// <summary>
    /// 送信スレッドを固定する CPU コア番号（0 始まり）。
    /// null の場合は OS のスケジューラに委ねる。
    /// </summary>
    public int? CpuCoreAffinity { get; init; } = null;
}