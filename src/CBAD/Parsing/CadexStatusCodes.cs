namespace CBAD.Parsing;

internal static class CadexStatusCodes
{
    private static readonly Dictionary<string, string> _map = new()
    {
        { "0",  "Ready" },
        { "1",  "Charge" },
        { "2",  "Charging" },
        { "3",  "Rest" },
        { "4",  "Prime" },
        { "5",  "Ready (Trickle)" },
        { "7",  "Discharging" },
        { "19", "Resting" },
        { "35", "QuickTest Complete" },
    };

    public static string Describe(string? code) =>
        string.IsNullOrEmpty(code) ? "—"
        : _map.TryGetValue(code.Trim(), out var d) ? d : $"Process {code}";

    public static System.Drawing.Color StatusColor(string? code) =>
        (code ?? "").Trim() switch
        {
            "0" or "5" or "35" => System.Drawing.Color.LimeGreen,
            "1" or "2" or "4"  => System.Drawing.Color.Gold,
            "7"                => System.Drawing.Color.DeepSkyBlue,
            "3"                => System.Drawing.Color.Orange,
            _                  => System.Drawing.Color.LightGray,
        };
}
