using CBAD.UI;

namespace CBAD.Tests;

public class CaptureControllerTests
{
    [Fact]
    public async Task StartStopAsync_SimulationMode_TransitionsLifecycleAndIsRunning()
    {
        var controller = new CaptureController();
        var lifecycle = new List<(CaptureLifecycleState State, string? Detail)>();
        var outDir = CreateTempDirectory();

        controller.LifecycleChanged += (state, detail) => lifecycle.Add((state, detail));

        Assert.False(controller.IsRunning);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await controller.StartAsync(CreateOptions(outDir), simulationMode: true).WaitAsync(timeout.Token);

            Assert.True(controller.IsRunning);
            Assert.Collection(
                lifecycle,
                e =>
                {
                    Assert.Equal(CaptureLifecycleState.Starting, e.State);
                    Assert.Null(e.Detail);
                },
                e =>
                {
                    Assert.Equal(CaptureLifecycleState.Running, e.State);
                    Assert.StartsWith("Logging to: ", e.Detail);
                });

            await controller.StopAsync().WaitAsync(timeout.Token);

            Assert.False(controller.IsRunning);
            Assert.Collection(
                lifecycle,
                e =>
                {
                    Assert.Equal(CaptureLifecycleState.Starting, e.State);
                    Assert.Null(e.Detail);
                },
                e =>
                {
                    Assert.Equal(CaptureLifecycleState.Running, e.State);
                    Assert.StartsWith("Logging to: ", e.Detail);
                },
                e =>
                {
                    Assert.Equal(CaptureLifecycleState.Stopping, e.State);
                    Assert.Null(e.Detail);
                },
                e =>
                {
                    Assert.Equal(CaptureLifecycleState.Idle, e.State);
                    Assert.Equal("Stopped", e.Detail);
                });
        }
        finally
        {
            await EnsureStoppedAsync(controller);
            DeleteDirectory(outDir);
        }
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyRunning_IsNoOp()
    {
        var controller = new CaptureController();
        var lifecycle = new List<(CaptureLifecycleState State, string? Detail)>();
        var outDir = CreateTempDirectory();

        controller.LifecycleChanged += (state, detail) => lifecycle.Add((state, detail));

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await controller.StartAsync(CreateOptions(outDir), simulationMode: true).WaitAsync(timeout.Token);

            var firstSink = controller.CurrentSink;

            await controller.StartAsync(CreateOptions(outDir), simulationMode: true).WaitAsync(timeout.Token);

            Assert.True(controller.IsRunning);
            Assert.Same(firstSink, controller.CurrentSink);
            Assert.Collection(
                lifecycle,
                e => Assert.Equal(CaptureLifecycleState.Starting, e.State),
                e => Assert.Equal(CaptureLifecycleState.Running, e.State));
        }
        finally
        {
            await EnsureStoppedAsync(controller);
            DeleteDirectory(outDir);
        }
    }

    [Fact]
    public async Task StopAsync_WhenNotRunning_IsNoOp()
    {
        var controller = new CaptureController();
        var lifecycle = new List<(CaptureLifecycleState State, string? Detail)>();

        controller.LifecycleChanged += (state, detail) => lifecycle.Add((state, detail));

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await controller.StopAsync().WaitAsync(timeout.Token);

        Assert.False(controller.IsRunning);
        Assert.Empty(lifecycle);
    }

    [Fact]
    public async Task StartAsync_WithInvalidOutDir_RaisesErrorAndRethrows()
    {
        var controller = new CaptureController();
        var lifecycle = new List<(CaptureLifecycleState State, string? Detail)>();

        controller.LifecycleChanged += (state, detail) => lifecycle.Add((state, detail));

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await controller.StartAsync(CreateOptions(" "), simulationMode: true).WaitAsync(timeout.Token));

        Assert.Equal("Please select an output folder.", exception.Message);
        Assert.False(controller.IsRunning);
        Assert.Collection(
            lifecycle,
            e =>
            {
                Assert.Equal(CaptureLifecycleState.Starting, e.State);
                Assert.Null(e.Detail);
            },
            e =>
            {
                Assert.Equal(CaptureLifecycleState.Error, e.State);
                Assert.Equal("Please select an output folder.", e.Detail);
            });
    }

    [Fact]
    public async Task StartStopAsync_SimulationMode_RaisesLifecycleEventsInExactOrder()
    {
        var controller = new CaptureController();
        var lifecycle = new List<(CaptureLifecycleState State, string? Detail)>();
        var outDir = CreateTempDirectory();

        controller.LifecycleChanged += (state, detail) => lifecycle.Add((state, detail));

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await controller.StartAsync(CreateOptions(outDir), simulationMode: true).WaitAsync(timeout.Token);
            await controller.StopAsync().WaitAsync(timeout.Token);

            Assert.Equal(
                new[]
                {
                    CaptureLifecycleState.Starting,
                    CaptureLifecycleState.Running,
                    CaptureLifecycleState.Stopping,
                    CaptureLifecycleState.Idle
                },
                lifecycle.Select(e => e.State).ToArray());

            Assert.Equal([null, "Stopped"], [lifecycle[0].Detail, lifecycle[3].Detail]);
            Assert.StartsWith("Logging to: ", lifecycle[1].Detail);
            Assert.Null(lifecycle[2].Detail);
        }
        finally
        {
            await EnsureStoppedAsync(controller);
            DeleteDirectory(outDir);
        }
    }

    [Fact]
    public async Task StartAsync_SimulationMode_RaisesRawLineReceived()
    {
        var controller = new CaptureController();
        var outDir = CreateTempDirectory();
        var rawLine = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        controller.RawLineReceived += line => rawLine.TrySetResult(line);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await controller.StartAsync(CreateOptions(outDir), simulationMode: true).WaitAsync(timeout.Token);

            var received = await rawLine.Task.WaitAsync(timeout.Token);

            Assert.False(string.IsNullOrWhiteSpace(received));
        }
        finally
        {
            await EnsureStoppedAsync(controller);
            DeleteDirectory(outDir);
        }
    }

    private static AppOptions CreateOptions(string outDir) => new()
    {
        Port = "COM1",
        OutDir = outDir,
        Prefix = "test"
    };

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "CBAD.Tests", nameof(CaptureControllerTests), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }

    private static async Task EnsureStoppedAsync(CaptureController controller)
    {
        if (!controller.IsRunning)
            return;

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await controller.StopAsync().WaitAsync(timeout.Token);
    }
}
