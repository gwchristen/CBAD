using System.IO.Ports;

namespace CBAD.Tests;

public class SettingsManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsPath;

    public SettingsManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        _settingsPath = Path.Combine(_tempDir, "settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var settings = SettingsManager.Load(_settingsPath);

        Assert.Null(settings.Port);
        Assert.Equal(9600, settings.Baud);
        Assert.Equal("None", settings.Parity);
        Assert.Equal(8, settings.DataBits);
        Assert.Equal("One", settings.StopBits);
        Assert.Equal("None", settings.Handshake);
        Assert.Equal(string.Empty, settings.OutDir);
        Assert.Equal("cadex_raw", settings.Prefix);
        Assert.False(settings.Csv);
        Assert.True(settings.Reconnect);
        Assert.Equal(2000, settings.ReconnectDelayMs);
        Assert.False(settings.DarkMode);
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaults()
    {
        File.WriteAllText(_settingsPath, "{ not valid json");

        var settings = SettingsManager.Load(_settingsPath);

        Assert.Equal(9600, settings.Baud);
        Assert.Equal("None", settings.Parity);
        Assert.Equal(8, settings.DataBits);
        Assert.Equal("One", settings.StopBits);
        Assert.Equal("None", settings.Handshake);
        Assert.Equal(string.Empty, settings.OutDir);
        Assert.Equal("cadex_raw", settings.Prefix);
        Assert.False(settings.Csv);
        Assert.True(settings.Reconnect);
        Assert.Equal(2000, settings.ReconnectDelayMs);
        Assert.False(settings.DarkMode);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsSettings()
    {
        var expected = new AppSettings
        {
            Port = "COM7",
            Baud = 115200,
            Parity = Parity.Even.ToString(),
            DataBits = 7,
            StopBits = StopBits.Two.ToString(),
            Handshake = Handshake.RequestToSend.ToString(),
            OutDir = Path.Combine(_tempDir, "output"),
            Prefix = "capture",
            Csv = true,
            Reconnect = false,
            ReconnectDelayMs = 500,
            DarkMode = true,
        };

        SettingsManager.Save(expected, _settingsPath);
        var actual = SettingsManager.Load(_settingsPath);

        Assert.Equal(expected.Port, actual.Port);
        Assert.Equal(expected.Baud, actual.Baud);
        Assert.Equal(expected.Parity, actual.Parity);
        Assert.Equal(expected.DataBits, actual.DataBits);
        Assert.Equal(expected.StopBits, actual.StopBits);
        Assert.Equal(expected.Handshake, actual.Handshake);
        Assert.Equal(expected.OutDir, actual.OutDir);
        Assert.Equal(expected.Prefix, actual.Prefix);
        Assert.Equal(expected.Csv, actual.Csv);
        Assert.Equal(expected.Reconnect, actual.Reconnect);
        Assert.Equal(expected.ReconnectDelayMs, actual.ReconnectDelayMs);
        Assert.Equal(expected.DarkMode, actual.DarkMode);

        var json = File.ReadAllText(_settingsPath);
        Assert.Contains(Environment.NewLine + "  \"Port\": \"COM7\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Save_InvalidPath_DoesNotThrow()
    {
        var ex = Record.Exception(() => SettingsManager.Save(new AppSettings(), "bad\0path"));

        Assert.Null(ex);
    }
}
