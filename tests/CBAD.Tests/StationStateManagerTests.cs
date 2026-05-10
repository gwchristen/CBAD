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
    public void ProcessLine_Code116_AlwaysSetsTargetCapacityNotMet_EvenAfterOhmTest()
    {
        var mgr = new StationStateManager();
        // Event 27 (OhmTest) with 114 mΩ resistance
        var ohmLine = @"0,1,""          "",""04/27/2026"",""143257"",27,""7\70"",114";
        // Event 116 (Program Fail)
        var failLine = @"0,1,""          "",""04/27/2026"",""143300"",116,""7\70"",""0""";

        mgr.ProcessLine(ohmLine);
        mgr.ProcessLine(failLine);

        Assert.Equal("Target Capacity Not Met", mgr.GetState(1).FailureReason);
    }

    [Fact]
    public void ProcessLine_Code116_AfterCode144_SetsTargetCapacityReason()
    {
        var mgr = new StationStateManager();
        var timeoutLine = @"0,1,""          "",""04/27/2026"",""143257"",144,""2\3943\0\26"",""89\87""";
        var failLine    = @"0,1,""          "",""04/27/2026"",""143300"",116,""2\3943\0\26"",""89\87""";

        mgr.ProcessLine(timeoutLine);
        mgr.ProcessLine(failLine);

        Assert.Equal("Target Capacity Not Met", mgr.GetState(1).FailureReason);
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
    public void ProcessLine_Code116_NoPrecedingCode_SetsTargetCapacityReason()
    {
        var mgr = new StationStateManager();
        // Send 116 with no preceding non-telemetry event
        var failLine = @"0,1,""          "",""04/27/2026"",""143300"",116,""2\3943\0\26"",""89\87""";

        mgr.ProcessLine(failLine);

        Assert.Equal("Target Capacity Not Met", mgr.GetState(1).FailureReason);
    }

    [Fact]
    public void ProcessLine_Code116_AfterCode250Only_SetsTargetCapacityReason()
    {
        var mgr = new StationStateManager();
        // Only normal telemetry before the failure
        mgr.ProcessLine(@"0,1,""          "",""04/24/2026"",""141900"",250,""2\3943\796\26"",""89\87""");
        mgr.ProcessLine(@"0,1,""          "",""04/27/2026"",""143300"",116,""2\3943\0\26"",""89\87""");

        Assert.Equal("Target Capacity Not Met", mgr.GetState(1).FailureReason);
    }

    [Fact]
    public void ProcessLine_Code177_SetsUnderchargedFailureReason()
    {
        var mgr = new StationStateManager();

        mgr.ProcessLine(@"0,1,""          "",""04/27/2026"",""143257"",177,""7\70"",""0""");

        Assert.Equal("Battery Undercharged", mgr.GetState(1).FailureReason);
    }

    [Fact]
    public void ProcessLine_Code175_DoesNotSetFailureReason()
    {
        var mgr = new StationStateManager();

        mgr.ProcessLine(@"0,1,""          "",""04/27/2026"",""143257"",175,""7\70"",""0""");

        Assert.Null(mgr.GetState(1).FailureReason);
    }

    [Fact]
    public void ProcessLine_Code176_DoesNotSetFailureReason()
    {
        var mgr = new StationStateManager();

        mgr.ProcessLine(@"0,1,""          "",""04/27/2026"",""143257"",176,""7\70"",""0""");

        Assert.Null(mgr.GetState(1).FailureReason);
    }

    [Fact]
    public void ProcessLine_Code178_SetsOverchargedFailureReason()
    {
        var mgr = new StationStateManager();

        mgr.ProcessLine(@"0,1,""          "",""04/27/2026"",""143257"",178,""7\70"",""0""");

        Assert.Equal("Battery Overcharged", mgr.GetState(1).FailureReason);
    }

    [Fact]
    public void ProcessLine_Code179_SetsUnableToLearnMatrixFailureReason()
    {
        var mgr = new StationStateManager();

        mgr.ProcessLine(@"0,1,""          "",""04/27/2026"",""143257"",179,""7\70"",""0""");

        Assert.NotNull(mgr.GetState(1).FailureReason);
        Assert.Contains("Learn Matrix", mgr.GetState(1).FailureReason);
    }

    [Fact]
    public void ProcessLine_Code116_AfterCode142_SetsDischargeTimeoutReason()
    {
        var mgr = new StationStateManager();
        var timeoutLine = @"0,1,""          "",""04/27/2026"",""143257"",142,""7\70"",""90\87""";
        var failLine    = @"0,1,""          "",""04/27/2026"",""143300"",116,""7\70"",""0""";

        mgr.ProcessLine(timeoutLine);
        mgr.ProcessLine(failLine);

        Assert.Equal("Discharge Timeout", mgr.GetState(1).FailureReason);
    }

    [Fact]
    public void ProcessLine_Code116_AfterCode113_ThenCode19_SetsPlateauTimeoutReason()
    {
        var mgr = new StationStateManager();
        var plateauLine = @"0,1,""          "",""04/27/2026"",""143255"",113,""7\1419\-401\35"",""85\37""";
        var restLine    = @"0,1,""          "",""04/27/2026"",""143256"",19,""19\1419\0\35"",""85\37""";
        var failLine    = @"0,1,""          "",""04/27/2026"",""143257"",116,""7\1419\-401\35"",""85\37""";

        mgr.ProcessLine(plateauLine);
        mgr.ProcessLine(restLine);
        mgr.ProcessLine(failLine);

        Assert.Equal("Plateau Timeout", mgr.GetState(1).FailureReason);
    }

    [Fact]
    public void ProcessLine_Code116_AfterCode146_ThenCode19_SetsReconditionTimeoutReason()
    {
        var mgr = new StationStateManager();
        var reconditionLine = @"0,1,""          "",""04/27/2026"",""143255"",146,""4\1419\-401\35"",""85\37""";
        var restLine        = @"0,1,""          "",""04/27/2026"",""143256"",19,""19\1419\0\35"",""85\37""";
        var failLine        = @"0,1,""          "",""04/27/2026"",""143257"",116,""4\1419\-401\35"",""85\37""";

        mgr.ProcessLine(reconditionLine);
        mgr.ProcessLine(restLine);
        mgr.ProcessLine(failLine);

        Assert.Equal("Recondition Timeout", mgr.GetState(1).FailureReason);
    }

    [Fact]
    public void ProcessLine_Code16_AfterCode14_ThenCode19_SetsBatteryOverTemperatureReason()
    {
        var mgr = new StationStateManager();
        var thermalLine = @"0,1,""          "",""04/27/2026"",""143255"",14,""2\1419\-401\35"",""85\37""";
        var restLine    = @"0,1,""          "",""04/27/2026"",""143256"",19,""19\1419\0\35"",""85\37""";
        var failLine    = @"0,1,""          "",""04/27/2026"",""143257"",16,""2\1419\-401\35"",""85\37""";

        mgr.ProcessLine(thermalLine);
        mgr.ProcessLine(restLine);
        mgr.ProcessLine(failLine);

        Assert.Equal("Battery Over Temperature", mgr.GetState(1).FailureReason);
    }

    [Fact]
    public void ProcessLine_Code16_AfterCode27_SetsOhmTestFailureReason()
    {
        var mgr = new StationStateManager();
        var ohmLine  = @"0,1,""          "",""04/27/2026"",""143257"",27,""7\70"",114";
        var failLine = @"0,1,""          "",""04/27/2026"",""143300"",16,""7\70"",""0""";

        mgr.ProcessLine(ohmLine);
        mgr.ProcessLine(failLine);

        Assert.NotNull(mgr.GetState(1).FailureReason);
        Assert.Contains("Ohm Test Failed", mgr.GetState(1).FailureReason);
        Assert.Contains("114 mΩ", mgr.GetState(1).FailureReason);
    }

    [Fact]
    public void ProcessLine_Code16_NoPrecedingCode_SetsGenericFailure()
    {
        var mgr = new StationStateManager();

        mgr.ProcessLine(@"0,1,""          "",""04/27/2026"",""143300"",16,""7\70"",""0""");

        Assert.Equal("Program Failed", mgr.GetState(1).FailureReason);
    }

    [Fact]
    public void ProcessLine_Code112_DoesNotBecomeSticky_Code16_UsesActiveContext()
    {
        var mgr = new StationStateManager();
        var warnLine = @"0,1,""          "",""04/27/2026"",""143256"",112,""7\1419\-401\35"",""85\37""";
        var failLine = @"0,1,""          "",""04/27/2026"",""143257"",16,""7\1419\-401\35"",""85\37""";

        mgr.ProcessLine(warnLine);
        Assert.Null(mgr.GetState(1).LastFaultEventCode);

        mgr.ProcessLine(failLine);

        Assert.Equal("Cell Mismatch", mgr.GetState(1).FailureReason);
    }

    [Fact]
    public void ProcessLine_Code130_IsNotStickyFaultIndicator()
    {
        var mgr = new StationStateManager();
        var advisoryLine = @"0,1,""          "",""04/27/2026"",""143255"",130,""2\3943\796\26"",""89\87""";

        mgr.ProcessLine(advisoryLine);

        Assert.Null(mgr.GetState(1).LastFaultEventCode);
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
    public void ProcessLine_SessionStartCode11_ClearsFailureReason()
    {
        var mgr = new StationStateManager();
        mgr.ProcessLine(@"0,1,""          "",""04/27/2026"",""143257"",116,""7\70"",""0""");
        Assert.NotNull(mgr.GetState(1).FailureReason);

        mgr.ProcessLine(@"0,1,""          "",""04/27/2026"",""150000"",11,""0\80""");

        Assert.Null(mgr.GetState(1).FailureReason);
    }

    [Fact]
    public void ProcessLine_SessionStartCode201_ClearsFailureReason()
    {
        var mgr = new StationStateManager();
        mgr.ProcessLine(@"0,1,""          "",""04/27/2026"",""143257"",116,""7\70"",""0""");
        Assert.NotNull(mgr.GetState(1).FailureReason);

        mgr.ProcessLine(@"0,1,""          "",""04/27/2026"",""150000"",201,""0\80""");

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

    [Fact]
    public void ProcessLine_Code16_AfterCode129_ThenCode19_SetsIntermittentReason()
    {
        // Reproduces the real-world scenario: Cadex emits a fault indicator code
        // (129 Intermittent Battery), then a status event (19 Resting), then the
        // final failure code (16 Custom Program Has Failed).  Without the sticky
        // LastFaultEventCode the status event would overwrite the fault breadcrumb
        // and the failure reason would degrade to the generic "Program Failed".
        var mgr = new StationStateManager();
        var faultLine  = @"0,1,""          "",""04/27/2026"",""143255"",129,""7\1419\-401\35"",""85\37""";
        var restLine   = @"0,1,""          "",""04/27/2026"",""143256"",19,""19\1419\0\35"",""85\37""";
        var failLine   = @"0,1,""          "",""04/27/2026"",""143257"",16,""19\1419\0\35"",""85\37""";

        mgr.ProcessLine(faultLine);
        mgr.ProcessLine(restLine);
        mgr.ProcessLine(failLine);

        Assert.Equal("Intermittent Battery", mgr.GetState(1).FailureReason);
    }

    [Fact]
    public void ProcessLine_Code116_AfterCode115_ThenCode19_SetsTargetCapacityReason()
    {
        // Same scenario with code 115 (Target Capacity Not Met) followed by a
        // resting event before the 116 failure.
        var mgr = new StationStateManager();
        var capLine  = @"0,1,""          "",""04/27/2026"",""143255"",115,""7\1419\-401\35"",""85\37""";
        var restLine = @"0,1,""          "",""04/27/2026"",""143256"",19,""19\1419\0\35"",""85\37""";
        var failLine = @"0,1,""          "",""04/27/2026"",""143257"",116,""7\1419\-401\35"",""85\37""";

        mgr.ProcessLine(capLine);
        mgr.ProcessLine(restLine);
        mgr.ProcessLine(failLine);

        Assert.Equal("Target Capacity Not Met", mgr.GetState(1).FailureReason);
    }

    [Fact]
    public void ProcessLine_SessionStartCode_ClearsStickyFaultCode()
    {
        var mgr = new StationStateManager();
        // Set up a fault code
        mgr.ProcessLine(@"0,1,""          "",""04/27/2026"",""143255"",129,""7\1419\-401\35"",""85\37""");
        Assert.Equal(129, mgr.GetState(1).LastFaultEventCode);

        // A new session start should clear the sticky fault
        mgr.ProcessLine(@"0,1,""          "",""04/27/2026"",""150000"",20,""0\80""");
        Assert.Null(mgr.GetState(1).LastFaultEventCode);
        Assert.Null(mgr.GetState(1).LastFaultRecord);
    }

    [Fact]
    public void ProcessLine_SessionStartCode_SetsSessionStart()
    {
        var mgr = new StationStateManager();
        Assert.Null(mgr.GetState(1).SessionStart);

        mgr.ProcessLine(@"0,1,""          "",""04/27/2026"",""150000"",20,""0\80""");

        Assert.NotNull(mgr.GetState(1).SessionStart);
    }

    [Fact]
    public void ProcessLine_FirstRecord_SetsSessionStartWhenNoSessionStartCodeSeen()
    {
        var mgr = new StationStateManager();
        // A normal telemetry record (no session-start code) should still set SessionStart
        // the first time so that mid-session captures can display a runtime.
        mgr.ProcessLine(@"0,1,""          "",""04/24/2026"",""141900"",250,""2\3943\796\26"",""89\87""");

        Assert.NotNull(mgr.GetState(1).SessionStart);
    }

    [Fact]
    public void ProcessLine_SessionStartCode_ResetsSessionStart()
    {
        var mgr = new StationStateManager();
        // Establish a SessionStart with a normal record first
        mgr.ProcessLine(@"0,1,""          "",""04/24/2026"",""141900"",250,""2\3943\796\26"",""89\87""");
        var firstStart = mgr.GetState(1).SessionStart;
        Assert.NotNull(firstStart);

        // A new session-start event should update SessionStart
        System.Threading.Thread.Sleep(5);  // ensure a measurable time difference
        mgr.ProcessLine(@"0,1,""          "",""04/27/2026"",""150000"",20,""0\80""");
        var newStart = mgr.GetState(1).SessionStart;

        Assert.NotNull(newStart);
        // The new SessionStart should be at or after the first
        Assert.True(newStart >= firstStart);
    }

    [Fact]
    public void ProcessLine_Code116_AfterCode27_ThenNewDischargePhase_ClearsStickyFault()
    {
        // Reproduces the real-world scenario: OhmTest fault fires (ProcessCode=0,
        // pre-test phase), then the battery is allowed to proceed and enters Discharge
        // (ProcessCode=7).  Without the phase-transition clear the OhmTest fault
        // stays sticky and incorrectly becomes the failure reason when 116 fires
        // during the discharge phase.
        var mgr = new StationStateManager();

        // OhmTest with ProcessCode=0 (initial / pre-test phase), resistance 114 mΩ
        var ohmLine       = @"0,1,""          "",""04/27/2026"",""143257"",27,""0\70"",114";
        // Discharge phase starts (event code 7, ProcessCode=7)
        var dischargeLine = @"0,1,""          "",""04/27/2026"",""143258"",7,""7\1419\-401\35"",""85\37""";
        // Program fails during discharge (no code 115 before 116)
        var failLine      = @"0,1,""          "",""04/27/2026"",""143300"",116,""7\1419\-401\35"",""85\37""";

        mgr.ProcessLine(ohmLine);
        Assert.Equal(27, mgr.GetState(1).LastFaultEventCode);

        mgr.ProcessLine(dischargeLine); // Phase transition should clear the OhmTest sticky fault
        Assert.Null(mgr.GetState(1).LastFaultEventCode);

        mgr.ProcessLine(failLine);

        Assert.Equal("Target Capacity Not Met", mgr.GetState(1).FailureReason);
    }

    [Fact]
    public void ProcessLine_Code116_AfterCode27_ThenDischargePhase_WithCode115_SetsTargetCapacityReason()
    {
        // Same as above, but code 115 (Target Capacity Not Met) fires during
        // the discharge phase, which should become the failure reason.
        var mgr = new StationStateManager();

        var ohmLine       = @"0,1,""          "",""04/27/2026"",""143257"",27,""0\70"",114";
        var dischargeLine = @"0,1,""          "",""04/27/2026"",""143258"",7,""7\1419\-401\35"",""85\37""";
        var capLine       = @"0,1,""          "",""04/27/2026"",""143259"",115,""7\1419\-401\35"",""85\37""";
        var failLine      = @"0,1,""          "",""04/27/2026"",""143300"",116,""7\1419\-401\35"",""85\37""";

        mgr.ProcessLine(ohmLine);
        mgr.ProcessLine(dischargeLine); // Clears OhmTest sticky fault
        mgr.ProcessLine(capLine);       // Sets new sticky fault: Target Capacity Not Met
        mgr.ProcessLine(failLine);

        Assert.Equal("Target Capacity Not Met", mgr.GetState(1).FailureReason);
    }
}
