using CBAD.Models;
using CBAD.Parsing;

namespace CBAD.Tests;

public class CadexEventParserTests
{
    // ── CadexEventParser.Describe ────────────────────────────────────────

    [Theory]
    [InlineData(0,   "Station Off Line")]
    [InlineData(2,   "Charging")]
    [InlineData(7,   "Discharging")]
    [InlineData(11,  "Start Battery Process")]
    [InlineData(20,  "Battery Inserted")]
    [InlineData(27,  "Resistance Test")]
    [InlineData(28,  "Resistance Test (Auto)")]
    [InlineData(30,  "Charge Cycle Complete")]
    [InlineData(31,  "Discharge Cycle Complete")]
    [InlineData(33,  "User Programmed Timeout")]
    [InlineData(35,  "Program Complete")]
    [InlineData(112, "Cell Mismatch")]
    [InlineData(113, "Plateau Timeout")]
    [InlineData(115, "Target Capacity Not Met")]
    [InlineData(116, "Program Fail")]
    [InlineData(135, "High Cell Resistance")]
    [InlineData(136, "High Cell Resistance")]
    [InlineData(142, "Discharge Timeout")]
    [InlineData(144, "Charge Timeout")]
    [InlineData(201, "Adapter Inserted")]
    [InlineData(250, "Normal Processing")]
    public void Describe_KnownCode_ReturnsCorrectDescription(int code, string expected)
    {
        Assert.Equal(expected, CadexEventParser.Describe(code));
    }

    [Fact]
    public void Describe_UnknownCode_ReturnsFallback()
    {
        // Truly unknown codes use the fallback "Event {code}" format.
        Assert.Equal("Event 999", CadexEventParser.Describe(999));
        // Code 0 is a known code – it returns the proper description, not the fallback.
        Assert.Equal("Station Off Line", CadexEventParser.Describe(0));
    }

    [Fact]
    public void Describe_UnknownCode_UsesFallbackFormat()
    {
        Assert.Equal("Event 42", CadexEventParser.Describe(42));
    }

    // ── CadexEventParser.FormatPayload ───────────────────────────────────

    [Fact]
    public void FormatPayload_OhmTestEvent27_WithResistance_AppendsMOhm()
    {
        var rec = MakeRecord(eventCode: 27, resistanceMOhm: 114);
        Assert.Equal("114 mΩ", CadexEventParser.FormatPayload(rec));
    }

    [Fact]
    public void FormatPayload_OhmTestEvent28_WithResistance_AppendsMOhm()
    {
        var rec = MakeRecord(eventCode: 28, resistanceMOhm: 230);
        Assert.Equal("230 mΩ", CadexEventParser.FormatPayload(rec));
    }

    [Fact]
    public void FormatPayload_HighResistanceEvent135_WithResistance_AppendsMOhm()
    {
        var rec = MakeRecord(eventCode: 135, resistanceMOhm: 500);
        Assert.Equal("500 mΩ", CadexEventParser.FormatPayload(rec));
    }

    [Fact]
    public void FormatPayload_OhmTestEvent27_NoResistance_ReturnsEmpty()
    {
        var rec = MakeRecord(eventCode: 27, resistanceMOhm: null);
        Assert.Equal(string.Empty, CadexEventParser.FormatPayload(rec));
    }

    [Fact]
    public void FormatPayload_NormalProcessing_ReturnsEmpty()
    {
        var rec = MakeRecord(eventCode: 250);
        Assert.Equal(string.Empty, CadexEventParser.FormatPayload(rec));
    }

    [Fact]
    public void FormatPayload_ChargeTimeout_WithHealthCurrent_AppendsMins()
    {
        var rec = MakeRecord(eventCode: 144, healthCurrent: 90);
        Assert.Equal("90 min", CadexEventParser.FormatPayload(rec));
    }

    [Fact]
    public void FormatPayload_DischargeTimeout_WithHealthCurrent_AppendsMins()
    {
        var rec = MakeRecord(eventCode: 142, healthCurrent: 45);
        Assert.Equal("45 min", CadexEventParser.FormatPayload(rec));
    }

    [Fact]
    public void FormatPayload_ChargeTimeout_NoHealthCurrent_ReturnsEmpty()
    {
        var rec = MakeRecord(eventCode: 144, healthCurrent: null);
        Assert.Equal(string.Empty, CadexEventParser.FormatPayload(rec));
    }

    // ── CadexEventParser.DetermineFailureReason ──────────────────────────

    [Fact]
    public void DetermineFailureReason_Code27_WithResistance_ReturnsOhmTestFailed()
    {
        var rec = MakeRecord(eventCode: 27, resistanceMOhm: 114);
        var reason = CadexEventParser.DetermineFailureReason(27, rec);
        Assert.Equal("Ohm Test Failed (114 mΩ)", reason);
    }

    [Fact]
    public void DetermineFailureReason_Code27_NoResistance_ReturnsOhmTestFailed()
    {
        var reason = CadexEventParser.DetermineFailureReason(27, null);
        Assert.Equal("Ohm Test Failed", reason);
    }

    [Fact]
    public void DetermineFailureReason_Code28_WithResistance_ReturnsOhmTestFailed()
    {
        var rec = MakeRecord(eventCode: 28, resistanceMOhm: 320);
        var reason = CadexEventParser.DetermineFailureReason(28, rec);
        Assert.Equal("Ohm Test Failed (320 mΩ)", reason);
    }

    [Fact]
    public void DetermineFailureReason_Code135_WithResistance_ReturnsHighCellResistance()
    {
        var rec = MakeRecord(eventCode: 135, resistanceMOhm: 450);
        var reason = CadexEventParser.DetermineFailureReason(135, rec);
        Assert.Equal("High Cell Resistance (450 mΩ)", reason);
    }

    [Fact]
    public void DetermineFailureReason_Code136_WithResistance_ReturnsHighCellResistance()
    {
        var rec = MakeRecord(eventCode: 136, resistanceMOhm: 501);
        var reason = CadexEventParser.DetermineFailureReason(136, rec);
        Assert.Equal("High Cell Resistance (501 mΩ)", reason);
    }

    [Fact]
    public void DetermineFailureReason_Code115_ReturnsTargetCapacityNotMet()
    {
        Assert.Equal("Target Capacity Not Met",
            CadexEventParser.DetermineFailureReason(115, null));
    }

    [Fact]
    public void DetermineFailureReason_Code144_ReturnsChargeTimeout()
    {
        Assert.Equal("Charge Timeout",
            CadexEventParser.DetermineFailureReason(144, null));
    }

    [Fact]
    public void DetermineFailureReason_Code142_ReturnsDischargeTimeout()
    {
        Assert.Equal("Discharge Timeout",
            CadexEventParser.DetermineFailureReason(142, null));
    }

    [Fact]
    public void DetermineFailureReason_Code146_ReturnsReconditionTimeout()
    {
        Assert.Equal("Recondition Timeout",
            CadexEventParser.DetermineFailureReason(146, null));
    }

    [Fact]
    public void DetermineFailureReason_Code113_ReturnsPlateauTimeout()
    {
        Assert.Equal("Plateau Timeout",
            CadexEventParser.DetermineFailureReason(113, null));
    }

    [Fact]
    public void DetermineFailureReason_Code120_ReturnsOverVoltage()
    {
        Assert.Equal("Over Voltage",
            CadexEventParser.DetermineFailureReason(120, null));
    }

    [Fact]
    public void DetermineFailureReason_Code122_ReturnsBatteryShorted()
    {
        Assert.Equal("Battery Shorted",
            CadexEventParser.DetermineFailureReason(122, null));
    }

    [Fact]
    public void DetermineFailureReason_UnknownPrecedingCode_ReturnsNull()
    {
        // A non-failure preceding code like 250 (normal) has no specific reason
        Assert.Null(CadexEventParser.DetermineFailureReason(250, null));
    }

    // ── CadexEventParser.IsFailureCode ───────────────────────────────────

    [Theory]
    [InlineData(116, true)]
    [InlineData(16,  true)]
    [InlineData(115, false)]
    [InlineData(250, false)]
    [InlineData(27,  false)]
    public void IsFailureCode_ReturnsExpected(int code, bool expected)
    {
        Assert.Equal(expected, CadexEventParser.IsFailureCode(code));
    }

    // ── CadexEventParser.IsSessionStartCode ─────────────────────────────

    [Theory]
    [InlineData(11,  true)]
    [InlineData(20,  true)]
    [InlineData(201, true)]
    [InlineData(200, true)]
    [InlineData(116, false)]
    [InlineData(250, false)]
    public void IsSessionStartCode_ReturnsExpected(int code, bool expected)
    {
        Assert.Equal(expected, CadexEventParser.IsSessionStartCode(code));
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static CadexRecord MakeRecord(
        int eventCode,
        int? resistanceMOhm = null,
        int? healthCurrent  = null)
    {
        return new CadexRecord
        {
            AnalyzerId     = 0,
            Station        = 1,
            BatteryId      = string.Empty,
            Timestamp      = DateTimeOffset.UtcNow,
            ReceivedAt     = DateTimeOffset.UtcNow,
            EventCode      = eventCode,
            ParamBlock     = string.Empty,
            HealthField    = string.Empty,
            ResistanceMOhm = resistanceMOhm,
            HealthCurrent  = healthCurrent,
        };
    }
}
