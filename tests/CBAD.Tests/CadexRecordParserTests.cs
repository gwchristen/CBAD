using CBAD.Parsing;

namespace CBAD.Tests;

public class CadexRecordParserTests
{
    private static readonly DateTimeOffset TestTime =
        new DateTimeOffset(2026, 4, 24, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FullValid8FieldLine_ParsesCorrectly()
    {
        var line = @"0,1,""          "",""04/24/2026"",""144500"",250,""16\1965\0\26"",""45""";
        var rec = CadexRecordParser.TryParse(line, TestTime);

        Assert.NotNull(rec);
        Assert.Equal(0, rec.RecordType);
        Assert.Equal(1, rec.Station);
        Assert.Equal(string.Empty, rec.BatteryId);
        Assert.Equal(250, rec.Value);
        Assert.Equal("16\\1965\\0\\26", rec.ParamBlock);
        Assert.Equal(16, rec.BatteryTypeCode);
        Assert.Equal(1965, rec.CapacityMah);
        Assert.Equal(0, rec.Cycles);
        Assert.Equal(26, rec.HealthPct);
        Assert.Equal("45", rec.StatusCode);
        Assert.Equal(TestTime, rec.ReceivedAt);
        Assert.Equal(line, rec.RawLine);
    }

    [Fact]
    public void ShortParamBlock_2Fields_CapacityAndHealthAreNull()
    {
        var line = @"0,1,""bat"",""04/24/2026"",""144708"",17,""4\70""";
        var rec = CadexRecordParser.TryParse(line, TestTime);

        Assert.NotNull(rec);
        Assert.Equal(17, rec.Value);
        Assert.Equal("4\\70", rec.ParamBlock);
        Assert.Null(rec.BatteryTypeCode);
        Assert.Null(rec.CapacityMah);
        Assert.Null(rec.Cycles);
        Assert.Null(rec.HealthPct);
        Assert.Equal(string.Empty, rec.StatusCode);
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
    public void StatusCode_MapsToCorrectDescription()
    {
        Assert.Equal("Ready", CadexStatusCodes.Describe("0"));
        Assert.Equal("Charge", CadexStatusCodes.Describe("1"));
        Assert.Equal("Discharge", CadexStatusCodes.Describe("2"));
        Assert.Equal("Rest", CadexStatusCodes.Describe("3"));
        Assert.Equal("Prime", CadexStatusCodes.Describe("4"));
        Assert.Equal("Complete", CadexStatusCodes.Describe("5"));
        Assert.Equal("Error", CadexStatusCodes.Describe("12"));
        Assert.Equal("Standby", CadexStatusCodes.Describe("24"));
        Assert.Equal("Float Charge", CadexStatusCodes.Describe("26"));
        Assert.Equal("Recondition", CadexStatusCodes.Describe("27"));
        Assert.Equal("Auto-test", CadexStatusCodes.Describe("37"));
        Assert.Equal("Standby Charge", CadexStatusCodes.Describe("45"));
        Assert.Equal("Status 99", CadexStatusCodes.Describe("99"));
    }
}
