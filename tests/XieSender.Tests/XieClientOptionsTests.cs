using System.Diagnostics;

namespace XieSender.Tests;

public class XieClientOptionsTests
{
    [Fact]
    public void DefaultValuesShouldBeCorrect()
    {
        var options = new XieClientOptions();

        Assert.Equal(0u, options.UserIndex);
        Assert.Equal(1000, options.TargetHz);
        Assert.Null(options.CpuCoreAffinity);
    }

    [Fact]
    public void InitShouldSetProperties()
    {
        var options = new XieClientOptions
        {
            UserIndex = 2,
            TargetHz = 500,
            CpuCoreAffinity = 0
        };

        Assert.Equal(2u, options.UserIndex);
        Assert.Equal(500, options.TargetHz);
        Assert.Equal(0, options.CpuCoreAffinity);
    }

    [Fact]
    public void RecordEqualityShouldWork()
    {
        var options1 = new XieClientOptions { UserIndex = 1, TargetHz = 1000 };
        var options2 = new XieClientOptions { UserIndex = 1, TargetHz = 1000 };
        var options3 = new XieClientOptions { UserIndex = 2, TargetHz = 1000 };

        Assert.Equal(options1, options2);
        Assert.NotEqual(options1, options3);
    }

    [Fact]
    public void WithShouldCreateModifiedCopy()
    {
        var original = new XieClientOptions { UserIndex = 0 };
        var modified = original with { UserIndex = 1 };

        Assert.Equal(0u, original.UserIndex);
        Assert.Equal(1u, modified.UserIndex);
    }

    [Fact]
    public void InitWithUserIndexGreaterThanThreeShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new XieClientOptions { UserIndex = 4 });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InitWithInvalidTargetHzShouldThrow(int targetHz)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new XieClientOptions { TargetHz = targetHz });
    }

    [Fact]
    public void InitWithTargetHzAboveStopwatchFrequencyShouldThrow()
    {
        if (Stopwatch.Frequency >= int.MaxValue)
        {
            return;
        }

        var invalidTargetHz = (int)Stopwatch.Frequency + 1;

        Assert.Throws<ArgumentOutOfRangeException>(() => new XieClientOptions { TargetHz = invalidTargetHz });
    }

    [Fact]
    public void InitWithNegativeCpuCoreAffinityShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new XieClientOptions { CpuCoreAffinity = -1 });
    }

    [Fact]
    public void InitWithCpuCoreAffinityAboveMaximumShouldThrow()
    {
        var invalidCore = Math.Min(Environment.ProcessorCount, IntPtr.Size * 8);

        Assert.Throws<ArgumentOutOfRangeException>(() => new XieClientOptions { CpuCoreAffinity = invalidCore });
    }
}