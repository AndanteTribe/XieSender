# XieSender

[![NuGet](https://img.shields.io/nuget/v/AndanteTribe.XieSender.svg)](https://www.nuget.org/packages/AndanteTribe.XieSender/) [![Releases](https://img.shields.io/github/release/AndanteTribe/XieSender.svg)](https://github.com/AndanteTribe/XieSender/releases) [![GitHub license](https://img.shields.io/badge/License-MIT-yellow.svg)](./LICENSE)

日本語

## 概要

**XieSender** は、Windows 上で XInput 対応コントローラーの入力を 1000Hz で UDP 送信する .NET ライブラリです。

[XInputEdge](https://github.com/Oryosan59/XInputEdge) の Sender 実装をリファクタリングし、モダンな C# API として切り出したものです。Receiver 側は XInputEdge の実装を使用してください。

主な特徴：

1. **モダンな非同期 API** — `IAsyncEnumerable<XieEvent>` による型安全なイベントストリーム
2. **高速送信** — 1000Hz（1ms 周期）の高精度タイミング制御
3. **低 CPU 負荷** — ハイブリッドウェイト（`Sleep(0)` + `SpinWait`）で効率化
4. **ゼロアロケーション** — `stackalloc` によるスタック割り当てで GC 圧力ゼロ
5. **外部依存なし** — XInput 1.4 の P/Invoke のみ

## インストール

### 必要条件

このライブラリは .NET 8.0 以上および Windows OS（XInput 1.4）が必要です。

### NuGet

パッケージは NuGet から取得できます。

#### .NET CLI

```sh
dotnet add package AndanteTribe.XieSender
```

#### パッケージマネージャー

```powershell
Install-Package AndanteTribe.XieSender
```

## クイックスタート

```csharp
using XieSender;

int index = XieDevice.FindFirst() ?? throw new InvalidOperationException("デバイスなし");
using var client = new XieClient("192.168.4.100", 5000, new XieClientOptions { UserIndex = index });

await foreach (var ev in client.RunStreamAsync())
{
    switch (ev)
    {
        case ControllerConnected c:    Console.WriteLine($"接続: {c.Index}"); break;
        case ControllerDisconnected d: Console.WriteLine($"切断: {d.Index}"); break;
        case SendError e:              Console.WriteLine($"エラー: {e.Exception.Message}"); break;
    }
}
```

## API リファレンス

### XieDevice

コントローラーデバイスの検出を行う静的クラスです。

#### `XieDevice.FindFirst()`

最初に見つかったコントローラーのインデックスを返します。見つからない場合は `null` を返します。

```csharp
int? index = XieDevice.FindFirst();
if (index.HasValue)
{
    Console.WriteLine($"デバイス検出: Index {index.Value}");
}
```

#### `XieDevice.FindAll()`

接続されているすべてのコントローラーのインデックス一覧を返します。

```csharp
IReadOnlyList<int> devices = XieDevice.FindAll();
foreach (var index in devices)
{
    Console.WriteLine($"デバイス {index} が接続中");
}
```

---

### XieClientOptions

クライアントの動作設定を行う `record` 型です。

```csharp
public sealed record XieClientOptions
{
    public int UserIndex { get; init; } = 0;           // XInput インデックス (0-3)
    public int TargetHz { get; init; } = 1000;         // 送信レート (Hz)
    public int? CpuCoreAffinity { get; init; } = null; // CPU コア固定 (null で未固定)
}
```

#### 使用例

```csharp
var options = new XieClientOptions
{
    UserIndex = 0,
    TargetHz = 1000,
    CpuCoreAffinity = 2  // CPU コア 2 に固定
};

using var client = new XieClient("192.168.4.100", 5000, options);
```

---

### XieClient

UDP 送信を行うクライアント本体です。

#### コンストラクタ

```csharp
public XieClient(string host, int port, XieClientOptions? options = null)
public XieClient(IPEndPoint endpoint, XieClientOptions? options = null)
```

送信先と設定を指定してクライアントを生成します。

```csharp
// ホストとポートで指定
using var client1 = new XieClient("192.168.4.100", 5000);

// IPEndPoint で指定
var endpoint = new IPEndPoint(IPAddress.Parse("192.168.4.100"), 5000);
using var client2 = new XieClient(endpoint);
```

#### `RunStreamAsync()`

送信ループを開始し、イベントストリームを返します。`Dispose()` を呼ぶと送信を停止し、列挙が完了します。

```csharp
using var client = new XieClient("192.168.4.100", 5000);

await foreach (var ev in client.RunStreamAsync())
{
    // イベント処理
}
```

---

### XieEvent

イベント型（抽象 `record` による discriminated union）です。

#### `ControllerConnected`

```csharp
public sealed record ControllerConnected(int Index) : XieEvent;
```

コントローラーが接続されたときに発火します。

#### `ControllerDisconnected`

```csharp
public sealed record ControllerDisconnected(int Index) : XieEvent;
```

コントローラーが切断されたときに発火します。

#### `SendError`

```csharp
public sealed record SendError(Exception Exception, long TotalDropped) : XieEvent;
```

送信中に深刻なエラーが発生したときに発火します。`TotalDropped` はエラー発生時点までの累積ドロップ数（`SocketError.WouldBlock` 起因）を示します。

#### 使用例

```csharp
await foreach (var ev in client.RunStreamAsync())
{
    switch (ev)
    {
        case ControllerConnected c:
            Console.WriteLine($"コントローラー {c.Index} が接続されました");
            break;

        case ControllerDisconnected d:
            Console.WriteLine($"コントローラー {d.Index} が切断されました");
            break;

        case SendError e:
            Console.WriteLine($"エラー: {e.Exception.Message}");
            Console.WriteLine($"累積ドロップ数: {e.TotalDropped}");
            break;
    }
}
```

## プロトコル仕様

### パケットフォーマット

22 bytes, Little Endian

| Offset | Type | Field | Description |
|--------|------|-------|-------------|
| 0 | `ushort` | `magic` | `0x5849` ("XI") |
| 2 | `byte` | `version` | `1` |
| 3 | `byte` | `typeAndFlags` | 上位4bit: flags / 下位4bit: type |
| 4 | `ushort` | `sampleId` | パケット連番 (0-65535) |
| 6 | `uint` | `timestampUs` | タイムスタンプ (μs) |
| 10 | `short` | `lx` | 左スティック X (-32768 ~ 32767) |
| 12 | `short` | `ly` | 左スティック Y |
| 14 | `short` | `rx` | 右スティック X |
| 16 | `short` | `ry` | 右スティック Y |
| 18 | `byte` | `lt` | 左トリガー (0-255) |
| 19 | `byte` | `rt` | 右トリガー (0-255) |
| 20 | `ushort` | `buttons` | ボタンビットフラグ |

### プロトコル定数

| 定数 | 値 | 説明 |
|------|-----|------|
| `XIE_MAGIC` | `0x5849` | パケット識別子 |
| `XIE_VERSION` | `1` | プロトコルバージョン |
| `XIE_TYPE_GAMEPAD` | `1` | ゲームパッド入力タイプ |
| `XIE_FLAG_HEARTBEAT` | `0x4` | 死活監視トグル |

## ライセンス

このライブラリは [MIT ライセンス](./LICENSE) のもとで公開されています。