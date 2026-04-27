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
}
