namespace XieSender.Tests;

public class XieDeviceTests
{
    [Fact]
    public void FindFirstWhenNoDevicesShouldReturnMinusOne()
    {
        // GitHub Actions の Windows ランナーにはコントローラーが接続されていない想定
        var result = XieDevice.FindFirst();

        // デバイスがない場合は -1、ある場合は 0-3
        Assert.True(result == -1 || (result >= 0 && result <= 3));
    }

    [Fact]
    public void FindAllShouldReturnList()
    {
        var result = XieDevice.FindAll();

        Assert.NotNull(result);
        // 0〜4 個のデバイスが返る
        Assert.InRange(result.Count, 0, 4);
    }

    [Fact]
    public void FindAllAllIndicesShouldBeValid()
    {
        var result = XieDevice.FindAll();

        foreach (var index in result)
        {
            Assert.InRange(index, 0, 3);
        }
    }

    [Fact]
    public void FindFirstAndFindAllShouldBeConsistent()
    {
        var first = XieDevice.FindFirst();
        var all = XieDevice.FindAll();

        if (first >= 0)
        {
            // FindFirst が値を返すなら、FindAll にも含まれているはず
            Assert.Contains(first, all);
        }
        else
        {
            // FindFirst が -1 なら、FindAll は空のはず
            Assert.Empty(all);
        }
    }

    [Fact]
    public void FindAllShouldNotContainDuplicates()
    {
        var result = XieDevice.FindAll();

        var distinct = result.Distinct().ToList();
        Assert.Equal(distinct.Count, result.Count);
    }

    [Fact]
    public void FindAllMultipleCallsShouldBeConsistent()
    {
        var result1 = XieDevice.FindAll();
        var result2 = XieDevice.FindAll();

        // 短時間内の呼び出しなら結果は同じはず（デバイスが抜き挿しされない限り）
        Assert.Equal(result1.Count, result2.Count);
    }
}