using CBAD.Models;

namespace CBAD.Tests;

public class StationStateManagerTests
{
    // A valid 4-field Cadex line for station 1.
    private const string ValidLine1 =
        @"0,1,""          "",""04/24/2026"",""141900"",250,""2\3943\796\26"",""89\87""";

    // A valid line for station 2.
    private const string ValidLine2 =
        @"0,2,""CDX01"",""01/24/2001"",""100200"",250,""7\1419\-401\35"",""85\37""";

    [Fact]
    public void ProcessLine_ValidLine_UpdatesState()
    {
        var mgr = new StationStateManager();
        StationState? updated = null;
        mgr.StationUpdated += s => updated = s;

        mgr.ProcessLine(ValidLine1);

        Assert.NotNull(updated);
        Assert.Equal(1, updated.Station);
        Assert.NotNull(updated.Latest);
        Assert.Single(updated.History);
        Assert.Single(updated.RawLines);
    }

    [Fact]
    public void ProcessLine_ValidLine_DoesNotFireParseFailed()
    {
        var mgr = new StationStateManager();
        bool parseFailed = false;
        mgr.ParseFailed += _ => parseFailed = true;

        mgr.ProcessLine(ValidLine1);

        Assert.False(parseFailed);
    }

    [Fact]
    public void ProcessLine_UnparsableLine_FiresParseFailedOnly()
    {
        var mgr = new StationStateManager();
        bool stationUpdated = false;
        string? failedLine = null;
        mgr.StationUpdated += _ => stationUpdated = true;
        mgr.ParseFailed    += l => failedLine = l;

        mgr.ProcessLine("GARBAGE LINE NOT A CADEX RECORD");

        Assert.False(stationUpdated);
        Assert.Equal("GARBAGE LINE NOT A CADEX RECORD", failedLine);
    }

    [Fact]
    public void ProcessLine_EmptyLine_FiresParseFailedOnly()
    {
        var mgr = new StationStateManager();
        bool stationUpdated = false;
        bool parseFailed = false;
        mgr.StationUpdated += _ => stationUpdated = true;
        mgr.ParseFailed    += _ => parseFailed = true;

        mgr.ProcessLine(string.Empty);

        Assert.False(stationUpdated);
        Assert.True(parseFailed);
    }

    [Fact]
    public void ProcessLine_TwoDifferentStations_UpdatesEachIndependently()
    {
        var mgr = new StationStateManager();

        mgr.ProcessLine(ValidLine1);
        mgr.ProcessLine(ValidLine2);

        Assert.Single(mgr.GetState(1).History);
        Assert.Single(mgr.GetState(2).History);
        Assert.Empty(mgr.GetState(3).History);
        Assert.Empty(mgr.GetState(4).History);
    }

    [Fact]
    public void ProcessLine_HistoryCapacity_OldestDropped()
    {
        var mgr = new StationStateManager();

        for (int i = 0; i <= StationStateManager.HistoryCapacity + 5; i++)
            mgr.ProcessLine(ValidLine1);

        Assert.Equal(StationStateManager.HistoryCapacity, mgr.GetState(1).History.Count);
    }

    [Fact]
    public void ProcessLine_RawLineCapacity_OldestDropped()
    {
        var mgr = new StationStateManager();

        for (int i = 0; i <= StationStateManager.RawLineCapacity + 10; i++)
            mgr.ProcessLine(ValidLine1);

        Assert.Equal(StationStateManager.RawLineCapacity, mgr.GetState(1).RawLines.Count);
    }

    [Fact]
    public void GetState_ReturnsCorrectStation()
    {
        var mgr = new StationStateManager();

        for (int s = 1; s <= 4; s++)
            Assert.Equal(s, mgr.GetState(s).Station);
    }

    [Fact]
    public void ProcessLine_LatestRecord_UpdatedEachTime()
    {
        var mgr = new StationStateManager();

        mgr.ProcessLine(ValidLine1);
        var first = mgr.GetState(1).Latest;

        mgr.ProcessLine(ValidLine1);
        var second = mgr.GetState(1).Latest;

        Assert.NotNull(first);
        Assert.NotNull(second);
        // Both are independent record objects even though the raw line is the same.
        Assert.NotSame(first, second);
    }

    [Fact]
    public void ProcessLine_Event250WithCapacityPayload_SetsTargetCapacity()
    {
        var mgr = new StationStateManager();
        var line = @"0,1,"" "",""04/24/2026"",""141900"",250,""7\1419\-4000\26"",""89\87"",""1\2""";

        mgr.ProcessLine(line);

        Assert.Equal("1\\2", mgr.GetState(1).TargetCapacity);
    }

    [Fact]
    public void ProcessLine_Event201WithTargetCapacityPct_SetsTargetCapacity()
    {
        var mgr = new StationStateManager();
        var line = @"0,2,"" "",""01/24/2001"",""085120"",201,""0\80""";

        mgr.ProcessLine(line);

        Assert.Equal("80%", mgr.GetState(2).TargetCapacity);
    }

    // ── Failure tracking ─────────────────────────────────────────────────

    [Fact]
    public void ProcessLine_Code116_AfterCode27_SetsOhmTestFailureReason()
    {
        var mgr = new StationStateManager();
        // Event 27 (OhmTest) with 114 mΩ resistance
        var ohmLine = @"0,1,""          "",""04/27/2026"",""143257"",27,""7\70"",114";
        // Event 116 (Program Fail)
        var failLine = @"0,1,""          "",""04/27/2026"",""143300"",116,""7\70"",""0""";

        mgr.ProcessLine(ohmLine);
        mgr.ProcessLine(failLine);

        var state = mgr.GetState(1);
        Assert.NotNull(state.FailureReason);
        Assert.Contains("Ohm Test Failed", state.FailureReason);
        Assert.Contains("114 mΩ", state.FailureReason);
    }

    [Fact]
    public void ProcessLine_Code116_AfterCode144_SetsChargeTimeoutReason()
    {
        var mgr = new StationStateManager();
        var timeoutLine = @"0,1,""          "",""04/27/2026"",""143257"",144,""2\3943\0\26"",""89\87""";
        var failLine    = @"0,1,""          "",""04/27/2026"",""143300"",116,""2\3943\0\26"",""89\87""";

        mgr.ProcessLine(timeoutLine);
        mgr.ProcessLine(failLine);

        Assert.Equal("Charge Timeout", mgr.GetState(1).FailureReason);
    }

    [Fact]
    public void ProcessLine_Code116_AfterCode115_SetsTargetCapacityReason()
    {
        var mgr = new StationStateManager();
        var capLine  = @"0,1,""          "",""04/27/2026"",""143257"",115,""7\1419\-401\35"",""85\37""";
        var failLine = @"0,1,""          "",""04/27/2026"",""143300"",116,""7\1419\-401\35"",""85\37""";

        mgr.ProcessLine(capLine);
        mgr.ProcessLine(failLine);

        Assert.Equal("Target Capacity Not Met", mgr.GetState(1).FailureReason);
    }

    [Fact]
    public void ProcessLine_Code116_NoPrecedingCode_SetsGenericFailure()
    {
        var mgr = new StationStateManager();
        // Send 116 with no preceding non-telemetry event
        var failLine = @"0,1,""          "",""04/27/2026"",""143300"",116,""2\3943\0\26"",""89\87""";

        mgr.ProcessLine(failLine);

        Assert.Equal("Program Failed", mgr.GetState(1).FailureReason);
    }

    [Fact]
    public void ProcessLine_Code116_AfterCode250Only_SetsGenericFailure()
    {
        var mgr = new StationStateManager();
        // Only normal telemetry before the failure
        mgr.ProcessLine(@"0,1,""          "",""04/24/2026"",""141900"",250,""2\3943\796\26"",""89\87""");
        mgr.ProcessLine(@"0,1,""          "",""04/27/2026"",""143300"",116,""2\3943\0\26"",""89\87""");

        // 250 lines do not update LastActiveEventCode, so no preceding event is known
        Assert.Equal("Program Failed", mgr.GetState(1).FailureReason);
    }

    [Fact]
    public void ProcessLine_SessionStartCode_ClearsFailureReason()
    {
        var mgr = new StationStateManager();
        // Produce a failure first
        mgr.ProcessLine(@"0,1,""          "",""04/27/2026"",""143257"",116,""7\70"",""0""");
        Assert.NotNull(mgr.GetState(1).FailureReason);

        // A new session start (code 20 = Battery Inserted) should clear it
        mgr.ProcessLine(@"0,1,""          "",""04/27/2026"",""150000"",20,""0\80""");
        Assert.Null(mgr.GetState(1).FailureReason);
    }

    [Fact]
    public void ProcessLine_Code116_DoesNotUpdateLastActiveEventCode()
    {
        var mgr = new StationStateManager();
        var ohmLine  = @"0,1,""          "",""04/27/2026"",""143257"",27,""7\70"",114";
        var failLine = @"0,1,""          "",""04/27/2026"",""143300"",116,""7\70"",""0""";

        mgr.ProcessLine(ohmLine);
        mgr.ProcessLine(failLine);

        // LastActiveEventCode should still be 27 (the ohm test), not 116
        Assert.Equal(27, mgr.GetState(1).LastActiveEventCode);
    }
}
