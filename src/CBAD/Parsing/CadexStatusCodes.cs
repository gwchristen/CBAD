namespace CBAD.Parsing;

internal static class CadexStatusCodes
{
    private static readonly Dictionary<string, string> _map = new()
    {
        { "0",  "Ready" },
        { "1",  "Charge" },
        { "2",  "Discharge" },
        { "3",  "Rest" },
        { "4",  "Prime" },
        { "5",  "Complete" },
        { "12", "Error" },
        { "24", "Standby" },
        { "26", "Float Charge" },
        { "27", "Recondition" },
        { "37", "Auto-test" },
        { "45", "Standby Charge" },
    };

    public static string Describe(string code) =>
        _map.TryGetValue(code.Trim(), out var desc) ? desc : $"Status {code}";

    public static System.Drawing.Color StatusColor(string code) =>
        code.Trim() switch
        {
            "0" or "5" or "24" or "26" or "45" => System.Drawing.Color.LimeGreen,
            "1" or "4"                          => System.Drawing.Color.Gold,
            "2"                                 => System.Drawing.Color.DeepSkyBlue,
            "3" or "27" or "37"                 => System.Drawing.Color.Orange,
            "12"                                => System.Drawing.Color.Red,
            _                                   => System.Drawing.Color.LightGray,
        };
}
