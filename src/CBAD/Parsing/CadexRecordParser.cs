using CBAD.Models;

namespace CBAD.Parsing;

internal static class CadexRecordParser
{
    private const int MaxStation = 4;

    public static CadexRecord? TryParse(string line, DateTimeOffset receivedAt)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        var fields = TokenizeCsv(line);
        if (fields.Count < 6)
            return null;

        if (!int.TryParse(fields[0].Trim(), out var analyzerId))
            return null;

        if (!int.TryParse(fields[1].Trim(), out var station) || station < 1 || station > MaxStation)
            return null;

        var batteryId = fields[2].Trim();

        if (!DateTimeOffset.TryParseExact(
                $"{fields[3].Trim()} {fields[4].Trim()}",
                "MM/dd/yyyy HHmmss",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal,
                out var timestamp))
            return null;

        if (!int.TryParse(fields[5].Trim(), out var eventCode))
            return null;

        var paramBlock = fields.Count > 6 ? fields[6] : string.Empty;
        var healthField = fields.Count > 7 ? fields[7].Trim() : string.Empty;

        int? processCode = null;
        int? voltageMv = null;
        int? currentMa = null;
        int? temperatureC = null;
        int? targetCapacityPct = null;

        if (!string.IsNullOrEmpty(paramBlock))
        {
            var parts = paramBlock.Split('\\');
            if (parts.Length >= 4)
            {
                if (int.TryParse(parts[0].Trim(), out var pc))  processCode   = pc;
                if (int.TryParse(parts[1].Trim(), out var mv))  voltageMv     = mv;
                if (int.TryParse(parts[2].Trim(), out var ma))  currentMa     = ma;
                if (int.TryParse(parts[3].Trim(), out var tc))  temperatureC  = tc;
            }
            else if (parts.Length == 2)
            {
                if (int.TryParse(parts[0].Trim(), out var pc)) processCode = pc;
                if (eventCode == 201 || eventCode == 20)
                {
                    if (int.TryParse(parts[1].Trim(), out var tcp)) targetCapacityPct = tcp;
                }
                else
                {
                    if (int.TryParse(parts[1].Trim(), out var mv)) voltageMv = mv;
                }
            }
        }

        int? healthCurrent = null;
        int? healthPrevious = null;

        if (!string.IsNullOrEmpty(healthField))
        {
            // Delimiter may be '\' or '/'
            var hParts = healthField.Contains('\\')
                ? healthField.Split('\\')
                : healthField.Split('/');

            if (hParts.Length >= 1 && int.TryParse(hParts[0].Trim(), out var hc)) healthCurrent  = hc;
            if (hParts.Length >= 2 && int.TryParse(hParts[1].Trim(), out var hp)) healthPrevious = hp;
        }

        // Optional field 8:
        //   event 27  → ResistanceMOhm (OhmTest)
        //   event 250 → CapacityPayload (Target / Measured Capacity string, e.g. "1\2")
        int? resistanceMOhm = null;
        string? capacityPayload = null;
        if (fields.Count > 8)
        {
            var field8 = fields[8].Trim();
            if (eventCode == 27 && int.TryParse(field8, out var res))
                resistanceMOhm = res;
            else if (eventCode == 250 && !string.IsNullOrEmpty(field8))
                capacityPayload = field8;
        }

        return new CadexRecord
        {
            AnalyzerId        = analyzerId,
            Station           = station,
            BatteryId         = batteryId,
            Timestamp         = timestamp,
            ReceivedAt        = receivedAt,
            EventCode         = eventCode,
            ParamBlock        = paramBlock,
            ProcessCode       = processCode,
            VoltageMv         = voltageMv,
            CurrentMa         = currentMa,
            TemperatureC      = temperatureC,
            TargetCapacityPct = targetCapacityPct,
            CapacityPayload   = capacityPayload,
            HealthField       = healthField,
            HealthCurrent     = healthCurrent,
            HealthPrevious    = healthPrevious,
            ResistanceMOhm    = resistanceMOhm,
            RawLine           = line
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
