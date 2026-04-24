using System.Globalization;
using System.IO.Ports;

namespace CBAD;

internal sealed class AppOptions
{
    public string? Port { get; init; }
    public int Baud { get; init; } = 9600;
    public Parity Parity { get; init; } = Parity.None;
    public int DataBits { get; init; } = 8;
    public StopBits StopBits { get; init; } = StopBits.One;
    public Handshake Handshake { get; init; } = Handshake.None;
    public string OutDir { get; init; } = "logs";
    public string Prefix { get; init; } = "cadex_raw";
    public bool Csv { get; init; } = false;
    public bool Reconnect { get; init; } = true;
    public int ReconnectDelayMs { get; init; } = 2000;
    public bool ListPorts { get; init; } = false;
}

internal static class AppOptionsParser
{
    public static bool TryParse(string[] args, out AppOptions options, out string? error)
    {
        options = new AppOptions();
        error = null;

        var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (!a.StartsWith("--", StringComparison.Ordinal))
            {
                error = $"Unknown argument '{a}'.";
                return false;
            }

            var key = a[2..];
            string? value = null;

            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = args[++i];
            }
            else
            {
                value = "true";
            }

            map[key] = value;
        }

        try
        {
            options = new AppOptions
            {
                Port = GetString(map, "port"),
                Baud = GetInt(map, "baud", 9600),
                Parity = GetEnum(map, "parity", Parity.None),
                DataBits = GetInt(map, "data-bits", 8),
                StopBits = GetEnum(map, "stop-bits", StopBits.One),
                Handshake = GetEnum(map, "handshake", Handshake.None),
                OutDir = GetString(map, "out-dir") ?? "logs",
                Prefix = GetString(map, "prefix") ?? "cadex_raw",
                Csv = GetBool(map, "csv", false),
                Reconnect = GetBool(map, "reconnect", true),
                ReconnectDelayMs = GetInt(map, "reconnect-delay-ms", 2000),
                ListPorts = GetBool(map, "list-ports", false)
            };
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }

        if (!options.ListPorts && string.IsNullOrWhiteSpace(options.Port))
        {
            error = "Missing required --port (unless using --list-ports).";
            return false;
        }

        if (options.Baud <= 0)
        {
            error = "--baud must be > 0.";
            return false;
        }

        if (options.DataBits is < 5 or > 8)
        {
            error = "--data-bits must be between 5 and 8.";
            return false;
        }

        if (options.ReconnectDelayMs < 100)
        {
            error = "--reconnect-delay-ms should be >= 100.";
            return false;
        }

        return true;
    }

    private static string? GetString(Dictionary<string, string?> map, string key)
        => map.TryGetValue(key, out var v) ? v : null;

    private static int GetInt(Dictionary<string, string?> map, string key, int def)
    {
        if (!map.TryGetValue(key, out var v) || string.IsNullOrWhiteSpace(v))
            return def;

        if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
            throw new ArgumentException($"Invalid integer for --{key}: '{v}'.");

        return n;
    }

    private static bool GetBool(Dictionary<string, string?> map, string key, bool def)
    {
        if (!map.TryGetValue(key, out var v) || string.IsNullOrWhiteSpace(v))
            return def;

        if (bool.TryParse(v, out var b))
            return b;

        if (v == "1") return true;
        if (v == "0") return false;

        throw new ArgumentException($"Invalid boolean for --{key}: '{v}'. Use true/false.");
    }

    private static TEnum GetEnum<TEnum>(Dictionary<string, string?> map, string key, TEnum def)
        where TEnum : struct, Enum
    {
        if (!map.TryGetValue(key, out var v) || string.IsNullOrWhiteSpace(v))
            return def;

        if (Enum.TryParse<TEnum>(v, ignoreCase: true, out var parsed))
            return parsed;

        throw new ArgumentException($"Invalid value for --{key}: '{v}'.");
    }
}
