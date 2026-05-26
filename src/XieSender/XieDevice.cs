using System.Collections.Generic;

namespace XieSender;

/// <summary>接続中の XInput デバイスを検出するユーティリティ。</summary>
public static class XieDevice
{
    private const uint ErrorSuccess = 0;

    /// <summary>最初に見つかった接続済みデバイスのインデックスを返す。見つからなければ -1。</summary>
    public static unsafe int FindFirst()
    {
        for (uint i = 0; i < 4; i++)
        {
            XINPUT_STATE state;
            if (XInput.XInputGetState(i, &state) == ErrorSuccess)
            {
                return (int)i;
            }
        }
        return -1;
    }

    /// <summary>接続済みデバイスのインデックス一覧を返す。</summary>
    public static unsafe IReadOnlyList<int> FindAll()
    {
        var list = new List<int>(4);
        for (uint i = 0; i < 4; i++)
        {
            XINPUT_STATE state;
            if (XInput.XInputGetState(i, &state) == ErrorSuccess)
            {
                list.Add((int)i);
            }
        }
        return list;
    }
}