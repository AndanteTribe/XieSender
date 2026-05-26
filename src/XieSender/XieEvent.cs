using System;

namespace XieSender;

/// <summary>XieClient が RunStreamAsync ストリームを通じて通知するイベントの基底型。</summary>
public abstract record XieEvent;

/// <summary>コントローラーが接続された。</summary>
/// <param name="Index">XInput ユーザーインデックス (0–3)。</param>
public sealed record ControllerConnected(int Index) : XieEvent;

/// <summary>コントローラーが切断された。</summary>
/// <param name="Index">XInput ユーザーインデックス (0–3)。</param>
public sealed record ControllerDisconnected(int Index) : XieEvent;

/// <summary>送信中に回復不能なエラーが発生した。</summary>
/// <param name="Exception">発生した例外。</param>
/// <param name="TotalDropped">エラー発生時点までの累積ドロップ数（WouldBlock 起因）。</param>
public sealed record SendError(Exception Exception, long TotalDropped) : XieEvent;