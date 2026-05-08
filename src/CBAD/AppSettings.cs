namespace CBAD;

internal sealed class AppSettings
{
    public string? Port { get; set; }
    public int Baud { get; set; } = 9600;
    public string Parity { get; set; } = "None";
    public int DataBits { get; set; } = 8;
    public string StopBits { get; set; } = "One";
    public string Handshake { get; set; } = "None";
    public string OutDir { get; set; } = string.Empty;
    public string Prefix { get; set; } = "cadex_raw";
    public bool Csv { get; set; }
    public bool Reconnect { get; set; } = true;
    public int ReconnectDelayMs { get; set; } = 2000;
    public bool DarkMode { get; set; }
}
