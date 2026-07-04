using System.Net;
using System.Threading;

namespace XieSender.Tests;

public class XieClientTests
{
    [Fact]
    public void ConstructorWithHostAndPortShouldCreate()
    {
        using var client = new XieClient("127.0.0.1", 5000);

        Assert.NotNull(client);
    }

    [Fact]
    public void ConstructorWithIPEndPointShouldCreate()
    {
        var endpoint = new IPEndPoint(IPAddress.Loopback, 5000);
        using var client = new XieClient(endpoint);

        Assert.NotNull(client);
    }

    [Fact]
    public void ConstructorWithOptionsShouldCreate()
    {
        var options = new XieClientOptions
        {
            UserIndex = 1,
            TargetHz = 500,
            CpuCoreAffinity = 2
        };

        using var client = new XieClient("127.0.0.1", 5000, options);

        Assert.NotNull(client);
    }

    [Fact]
    public void ConstructorWithNullOptionsShouldUseDefaults()
    {
        using var client = new XieClient("127.0.0.1", 5000, null);

        Assert.NotNull(client);
    }

    [Fact]
    public void DisposeShouldNotThrow()
    {
        var client = new XieClient("127.0.0.1", 5000);

        var exception = Record.Exception(() => client.Dispose());

        Assert.Null(exception);
    }

    [Fact]
    public void DisposeCalledMultipleTimesShouldNotThrow()
    {
        var client = new XieClient("127.0.0.1", 5000);

        client.Dispose();
        var exception = Record.Exception(() => client.Dispose());

        Assert.Null(exception);
    }

    [Fact]
    public async Task DisposeCalledConcurrentlyShouldNotThrow()
    {
        var client = new XieClient("127.0.0.1", 5000);
        using var gate = new ManualResetEventSlim(false);

        var tasks = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() =>
            {
                gate.Wait();
                client.Dispose();
            }))
            .ToArray();

        gate.Set();

        await Task.WhenAll(tasks);
    }

    [Fact]
    public void ConstructorWithInvalidHostShouldThrow()
    {
        Assert.Throws<FormatException>(() =>
        {
            using var client = new XieClient("invalid-ip", 5000);
        });
    }

    [Fact]
    public async Task RunStreamAsyncWithDisposeShouldComplete()
    {
        using var client = new XieClient("127.0.0.1", 5000);

        var task = Task.Run(async () =>
        {
            var count = 0;
            await foreach (var ev in client.RunStreamAsync())
            {
                count++;
                if (count > 100)
                    break; // 無限ループ防止
            }
        });

        // 少し待ってから Dispose
        await Task.Delay(100);
        client.Dispose();

        // Dispose により RunStreamAsync が完了するはず
        await task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RunStreamAsyncWithCancellationTokenShouldCancel()
    {
        using var client = new XieClient("127.0.0.1", 5000);
        using var cts = new CancellationTokenSource();

        var task = Task.Run(async () =>
        {
            await foreach (var ev in client.RunStreamAsync(cts.Token))
            {
            }
        });

        await Task.Delay(100);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task RunStreamAsyncWithEnumeratorCancellationShouldCancel()
    {
        using var client = new XieClient("127.0.0.1", 5000);
        using var cts = new CancellationTokenSource();

        var task = Task.Run(async () =>
        {
            await foreach (var ev in client.RunStreamAsync().WithCancellation(cts.Token))
            {
            }
        });

        await Task.Delay(100);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task RunStreamAsyncCalledTwiceShouldThrow()
    {
        using var client = new XieClient("127.0.0.1", 5000);
        await using var firstEnumerator = client.RunStreamAsync().GetAsyncEnumerator();

        var firstMoveNextTask = firstEnumerator.MoveNextAsync().AsTask();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var secondEnumerator = client.RunStreamAsync().GetAsyncEnumerator();
            await secondEnumerator.MoveNextAsync();
        });

        Assert.Equal("RunStreamAsync can only be called once.", exception.Message);

        client.Dispose();
        await firstMoveNextTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void UsingStatementShouldDisposeCorrectly()
    {
        var exception = Record.Exception(() =>
        {
            using (var client = new XieClient("127.0.0.1", 5000))
            {
                // 何もしない
            }
        });

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(2000)]
    public void ConstructorWithDifferentTargetHzShouldCreate(int targetHz)
    {
        var options = new XieClientOptions { TargetHz = targetHz };
        using var client = new XieClient("127.0.0.1", 5000, options);

        Assert.NotNull(client);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ConstructorWithValidUserIndexShouldCreate(int userIndex)
    {
        var options = new XieClientOptions { UserIndex = userIndex };
        using var client = new XieClient("127.0.0.1", 5000, options);

        Assert.NotNull(client);
    }

    [Fact]
    public void ConstructorWithAllOptionsShouldCreate()
    {
        var options = new XieClientOptions
        {
            UserIndex = 1,
            TargetHz = 500,
            CpuCoreAffinity = 0
        };

        using var client = new XieClient("127.0.0.1", 5000, options);

        Assert.NotNull(client);
    }

    [Fact]
    public void ConstructorWithLoopbackAddressShouldCreate()
    {
        var endpoint = IPEndPoint.Parse("127.0.0.1:5000");
        using var client = new XieClient(endpoint);

        Assert.NotNull(client);
    }
}