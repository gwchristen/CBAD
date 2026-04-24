using System.IO.Ports;

namespace CBAD.Tests;

public class AppOptionsParserTests
{
    [Fact]
    public void ValidMinimal_PortOnly_ParsesWithDefaults()
    {
        var result = AppOptionsParser.TryParse(["--port", "COM3"], out var opts, out var error);

        Assert.True(result);
        Assert.Null(error);
        Assert.Equal("COM3", opts.Port);
        Assert.Equal(9600, opts.Baud);
        Assert.Equal(8, opts.DataBits);
        Assert.Equal(Parity.None, opts.Parity);
        Assert.Equal(StopBits.One, opts.StopBits);
        Assert.Equal(Handshake.None, opts.Handshake);
        Assert.Equal("logs", opts.OutDir);
        Assert.Equal("cadex_raw", opts.Prefix);
        Assert.False(opts.Csv);
        Assert.True(opts.Reconnect);
        Assert.Equal(2000, opts.ReconnectDelayMs);
        Assert.False(opts.ListPorts);
    }

    [Fact]
    public void ValidAllFlags_ParsesCorrectly()
    {
        string[] args =
        [
            "--port", "COM5",
            "--baud", "115200",
            "--parity", "Even",
            "--data-bits", "7",
            "--stop-bits", "Two",
            "--handshake", "RequestToSend",
            "--out-dir", "mydir",
            "--prefix", "mypfx",
            "--csv", "true",
            "--reconnect", "false",
            "--reconnect-delay-ms", "500"
        ];

        var result = AppOptionsParser.TryParse(args, out var opts, out var error);

        Assert.True(result);
        Assert.Null(error);
        Assert.Equal("COM5", opts.Port);
        Assert.Equal(115200, opts.Baud);
        Assert.Equal(Parity.Even, opts.Parity);
        Assert.Equal(7, opts.DataBits);
        Assert.Equal(StopBits.Two, opts.StopBits);
        Assert.Equal(Handshake.RequestToSend, opts.Handshake);
        Assert.Equal("mydir", opts.OutDir);
        Assert.Equal("mypfx", opts.Prefix);
        Assert.True(opts.Csv);
        Assert.False(opts.Reconnect);
        Assert.Equal(500, opts.ReconnectDelayMs);
    }

    [Fact]
    public void MissingPort_ReturnsError()
    {
        var result = AppOptionsParser.TryParse([], out _, out var error);

        Assert.False(result);
        Assert.NotNull(error);
        Assert.Contains("--port", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BadBaud_ReturnsError()
    {
        var result = AppOptionsParser.TryParse(["--port", "COM3", "--baud", "0"], out _, out var error);

        Assert.False(result);
        Assert.NotNull(error);
        Assert.Contains("baud", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BadDataBits_ReturnsError()
    {
        var result = AppOptionsParser.TryParse(["--port", "COM3", "--data-bits", "9"], out _, out var error);

        Assert.False(result);
        Assert.NotNull(error);
        Assert.Contains("data-bits", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ListPorts_WorksWithoutPort()
    {
        var result = AppOptionsParser.TryParse(["--list-ports"], out var opts, out var error);

        Assert.True(result);
        Assert.Null(error);
        Assert.True(opts.ListPorts);
    }
}
