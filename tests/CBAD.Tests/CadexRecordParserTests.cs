using CBAD.Parsing;

namespace CBAD.Tests;

public class CadexRecordParserTests
{
    private static readonly DateTimeOffset TestTime =
        new DateTimeOffset(2026, 4, 24, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Full4FieldRecord_ParsesCorrectly()
    {
        var line = @"0,1,""          "",""04/24/2026"",""141900"",250,""16\1961\796\26"",""40\45""";
        var rec = CadexRecordParser.TryParse(line, TestTime);

        Assert.NotNull(rec);
        Assert.Equal(0, rec.AnalyzerId);
        Assert.Equal(1, rec.Station);
        Assert.Equal(string.Empty, rec.BatteryId);
        Assert.Equal(250, rec.EventCode);
        Assert.Equal("16\\1961\\796\\26", rec.ParamBlock);
        Assert.Equal(16, rec.ProcessCode);
        Assert.Equal(1961, rec.VoltageMv);
        Assert.Equal(796, rec.CurrentMa);
        Assert.Equal(26, rec.TemperatureC);
        Assert.Equal(40, rec.HealthCurrent);
        Assert.Equal(45, rec.HealthPrevious);
        Assert.Equal(TestTime, rec.ReceivedAt);
        Assert.Equal(line, rec.RawLine);
    }

    [Fact]
    public void NegativeCurrent_Discharging_ParsesCorrectly()
    {
        var line = @"0,2,""CDX01"",""01/24/2001"",""100200"",250,""7\1419\-401\35"",""85\37""";
        var rec = CadexRecordParser.TryParse(line, TestTime);

        Assert.NotNull(rec);
        Assert.Equal(7, rec.ProcessCode);
        Assert.Equal(1419, rec.VoltageMv);
        Assert.Equal(-401, rec.CurrentMa);
        Assert.Equal(35, rec.TemperatureC);
        Assert.Equal(85, rec.HealthCurrent);
        Assert.Equal(37, rec.HealthPrevious);
    }

    [Fact]
    public void EmptyHealthField_BothHealthValuesNull()
    {
        var line = @"0,2,""CDX01"",""01/24/2001"",""085140"",250,""2\1416\398\21"",""""";
        var rec = CadexRecordParser.TryParse(line, TestTime);

        Assert.NotNull(rec);
        Assert.Null(rec.HealthCurrent);
        Assert.Null(rec.HealthPrevious);
    }

    [Fact]
    public void PreTestInsertion_2FieldParam_TargetCapacityParsed()
    {
        var line = @"0,2,"" "",""01/24/2001"",""085120"",201,""0\80""";
        var rec = CadexRecordParser.TryParse(line, TestTime);

        Assert.NotNull(rec);
        Assert.Equal(201, rec.EventCode);
        Assert.Equal(0, rec.ProcessCode);
        Assert.Equal(80, rec.TargetCapacityPct);
        Assert.Null(rec.VoltageMv);
    }

    [Fact]
    public void OhmTest_ResistanceParsed()
    {
        var line = @"0,2,""CDX01"",""01/24/2001"",""085456"",27,""0\80"",341";
        var rec = CadexRecordParser.TryParse(line, TestTime);

        Assert.NotNull(rec);
        Assert.Equal(27, rec.EventCode);
        Assert.Equal(341, rec.ResistanceMOhm);
    }

    [Fact]
    public void SingleHealthValue_NoPrevious()
    {
        var line = @"0,2,""CDX01"",""01/25/2001"",""090500"",250,""5\1694\7\28"",""89""";
        var rec = CadexRecordParser.TryParse(line, TestTime);

        Assert.NotNull(rec);
        Assert.Equal(89, rec.HealthCurrent);
        Assert.Null(rec.HealthPrevious);
    }

    [Fact]
    public void HealthField_SlashDelimiter_ParsesCorrectly()
    {
        // Health field uses '/' as delimiter instead of '\'
        var line = @"0,1,""          "",""04/24/2026"",""141900"",250,""16\1961\796\26"",""40/45""";
        var rec = CadexRecordParser.TryParse(line, TestTime);

        Assert.NotNull(rec);
        Assert.Equal(40, rec.HealthCurrent);
        Assert.Equal(45, rec.HealthPrevious);
    }

    [Fact]
    public void BatteryId_AllWhitespace_BatteryIdIsEmpty()
    {
        var line = @"0,2,""          "",""04/24/2026"",""144500"",250,""16\1965\0\26"",""2""";
        var rec = CadexRecordParser.TryParse(line, TestTime);

        Assert.NotNull(rec);
        Assert.Equal(string.Empty, rec.BatteryId);
    }

    [Fact]
    public void NormalEvent250_WithCapacityPayload_ParsedCorrectly()
    {
        // Format "measured\cycle" seen in real Cadex C7x00 logs during discharge
        var line = @"0,1,"" "",""04/24/2026"",""141900"",250,""7\1419\-4000\26"",""89\87"",""1\2""";
        var rec = CadexRecordParser.TryParse(line, TestTime);

        Assert.NotNull(rec);
        Assert.Equal(250, rec.EventCode);
        Assert.Equal("1\\2", rec.CapacityPayload);
        Assert.Null(rec.ResistanceMOhm);
    }

    [Fact]
    public void NormalEvent250_WithSimpleCapacityPayload_ParsedCorrectly()
    {
        var line = @"0,1,"" "",""04/24/2026"",""141900"",250,""7\1419\-4000\26"",""89\87"",""0""";
        var rec = CadexRecordParser.TryParse(line, TestTime);

        Assert.NotNull(rec);
        Assert.Equal("0", rec.CapacityPayload);
        Assert.Null(rec.ResistanceMOhm);
    }

    [Fact]
    public void OhmTest_Event27_ResistanceStillParsedAfterCapacityChange()
    {
        // Ensure OhmTest resistance parsing is unaffected by the capacity payload change
        var line = @"0,2,""CDX01"",""01/24/2001"",""085456"",27,""0\80"",341";
        var rec = CadexRecordParser.TryParse(line, TestTime);

        Assert.NotNull(rec);
        Assert.Equal(27, rec.EventCode);
        Assert.Equal(341, rec.ResistanceMOhm);
        Assert.Null(rec.CapacityPayload);
    }

    [Fact]
    public void NormalEvent250_NoField8_CapacityPayloadNull()
    {
        var line = @"0,1,""          "",""04/24/2026"",""141900"",250,""16\1961\796\26"",""40\45""";
        var rec = CadexRecordParser.TryParse(line, TestTime);

        Assert.NotNull(rec);
        Assert.Null(rec.CapacityPayload);
        Assert.Null(rec.ResistanceMOhm);
    }

    [Fact]
    public void MalformedLine_ReturnsNull()
    {
        var rec = CadexRecordParser.TryParse("this is not a valid record", TestTime);
        Assert.Null(rec);
    }

    [Fact]
    public void EmptyLine_ReturnsNull()
    {
        var rec = CadexRecordParser.TryParse(string.Empty, TestTime);
        Assert.Null(rec);
    }

    [Fact]
    public void ProcessCode_MapsToCorrectDescription()
    {
        Assert.Equal("Ready", CadexStatusCodes.Describe("0"));
        Assert.Equal("Charge", CadexStatusCodes.Describe("1"));
        Assert.Equal("Charging", CadexStatusCodes.Describe("2"));
        Assert.Equal("Rest", CadexStatusCodes.Describe("3"));
        Assert.Equal("Prime", CadexStatusCodes.Describe("4"));
        Assert.Equal("Ready (Trickle)", CadexStatusCodes.Describe("5"));
        Assert.Equal("Discharging", CadexStatusCodes.Describe("7"));
        Assert.Equal("QuickTest Complete", CadexStatusCodes.Describe("35"));
        Assert.Equal("Process 99", CadexStatusCodes.Describe("99"));
        Assert.Equal("—", CadexStatusCodes.Describe(null));
        Assert.Equal("—", CadexStatusCodes.Describe(""));
    }

    [Fact]
    public void TwoFieldParam_NonInsertionEvent_VoltageParsed()
    {
        // 2-field param for an active-test event (not 201/20) → process + voltage
        var line = @"0,2,""CDX01"",""01/24/2001"",""085130"",11,""2\1416""";
        var rec = CadexRecordParser.TryParse(line, TestTime);

        Assert.NotNull(rec);
        Assert.Equal(11, rec.EventCode);
        Assert.Equal(2, rec.ProcessCode);
        Assert.Equal(1416, rec.VoltageMv);
        Assert.Null(rec.TargetCapacityPct);
    }
}

