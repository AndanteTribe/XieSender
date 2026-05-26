namespace XieSender.Tests;

public class XieEventTests
{
    [Fact]
    public void ControllerConnectedShouldCreateCorrectly()
    {
        var ev = new ControllerConnected(2);

        Assert.Equal(2, ev.Index);
        Assert.IsAssignableFrom<XieEvent>(ev);
    }

    [Fact]
    public void ControllerDisconnectedShouldCreateCorrectly()
    {
        var ev = new ControllerDisconnected(1);

        Assert.Equal(1, ev.Index);
        Assert.IsAssignableFrom<XieEvent>(ev);
    }

    [Fact]
    public void SendErrorShouldCreateCorrectly()
    {
        var exception = new InvalidOperationException("test error");
        var ev = new SendError(exception, 42);

        Assert.Equal(exception, ev.Exception);
        Assert.Equal(42, ev.TotalDropped);
        Assert.IsAssignableFrom<XieEvent>(ev);
    }

    [Fact]
    public void RecordEqualityShouldWork()
    {
        var c1 = new ControllerConnected(1);
        var c2 = new ControllerConnected(1);
        var c3 = new ControllerConnected(2);

        Assert.Equal(c1, c2);
        Assert.NotEqual(c1, c3);
    }

    [Fact]
    public void DifferentEventTypesShouldNotBeEqual()
    {
        var connected = new ControllerConnected(1);
        var disconnected = new ControllerDisconnected(1);

        Assert.NotEqual<XieEvent>(connected, disconnected);
    }

    [Fact]
    public void SendErrorWithDifferentDropCountsShouldNotBeEqual()
    {
        var ex = new Exception("test");
        var e1 = new SendError(ex, 10);
        var e2 = new SendError(ex, 20);

        Assert.NotEqual(e1, e2);
    }
}