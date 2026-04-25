using CBAD.Models;

namespace CBAD.Parsing;

internal static class CadexRecordParser
{
    public static CadexRecord? TryParse(string line, DateTimeOffset receivedAt)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        var fields = TokenizeCsv(line);
        if (fields.Count < 6)
            return null;

        if (!int.TryParse(fields[0].Trim(), out var recordType))
            return null;

        if (!int.TryParse(fields[1].Trim(), out var station) || station < 1 || station > 4)
            return null;

        var batteryId = fields[2].Trim();

        if (!DateTimeOffset.TryParseExact(
                $"{fields[3].Trim()} {fields[4].Trim()}",
                "MM/dd/yyyy HHmmss",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal,
                out var timestamp))
            return null;

        if (!int.TryParse(fields[5].Trim(), out var value))
            return null;

        var paramBlock = fields.Count > 6 ? fields[6] : string.Empty;
        var statusCode = fields.Count > 7 ? fields[7].Trim() : string.Empty;

        int? batteryTypeCode = null;
        int? capacityMah = null;
        int? cycles = null;
        int? healthPct = null;

        if (!string.IsNullOrEmpty(paramBlock))
        {
            var parts = paramBlock.Split('\\');
            if (parts.Length >= 4)
            {
                if (int.TryParse(parts[0], out var btc)) batteryTypeCode = btc;
                if (int.TryParse(parts[1], out var cap)) capacityMah = cap;
                if (int.TryParse(parts[2], out var cyc)) cycles = cyc;
                if (int.TryParse(parts[3], out var hlt)) healthPct = hlt;
            }
        }

        return new CadexRecord
        {
            RecordType = recordType,
            Station = station,
            BatteryId = batteryId,
            Timestamp = timestamp,
            ReceivedAt = receivedAt,
            Value = value,
            ParamBlock = paramBlock,
            BatteryTypeCode = batteryTypeCode,
            CapacityMah = capacityMah,
            Cycles = cycles,
            HealthPct = healthPct,
            StatusCode = statusCode,
            RawLine = line
        };
    }

    private static List<string> TokenizeCsv(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inQuotes = false;
        int i = 0;

        while (i < line.Length)
        {
            char c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i += 2;
                    }
                    else
                    {
                        inQuotes = false;
                        i++;
                    }
                }
                else
                {
                    current.Append(c);
                    i++;
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                    i++;
                }
                else if (c == ',')
                {
                    fields.Add(current.ToString());
                    current.Clear();
                    i++;
                }
                else
                {
                    current.Append(c);
                    i++;
                }
            }
        }

        fields.Add(current.ToString());
        return fields;
    }
}
