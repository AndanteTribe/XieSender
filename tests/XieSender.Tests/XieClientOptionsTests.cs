namespace XieSender.Tests;

public class XieClientOptionsTests
{
    [Fact]
    public void DefaultValuesShouldBeCorrect()
    {
        var options = new XieClientOptions();

        Assert.Equal(0, options.UserIndex);
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
            CpuCoreAffinity = 3
        };

        Assert.Equal(2, options.UserIndex);
        Assert.Equal(500, options.TargetHz);
        Assert.Equal(3, options.CpuCoreAffinity);
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

        Assert.Equal(0, original.UserIndex);
        Assert.Equal(1, modified.UserIndex);
    }
}