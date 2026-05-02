using CBAD.Diagnostics;
using CBAD.Models;

namespace CBAD.Tests;

/// <summary>
/// Unit tests for the Stage 3/4 diagnostic inference engine.
/// </summary>
public class DiagnosticAnalyzerTests
{
    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>Creates a standard SLA profile for a 6 V pack (3 cells × 2 V).</summary>
    private static BatteryProfile MakeProfile(double targetCapPct = 70.0, double volts = 6.0)
        => new()
        {
            ProfileName                    = "Test Profile",
            BatteryType                    = "SLA",
            TargetCapPercentage            = targetCapPct,
            Volts                          = volts,
            CapacityMAh                    = 8000,
            MinTempCelsius                 = 0,
            MaxTempCelsius                 = 45,
            MaxStandbyVoltagePerCell       = 2.3,
            MaxChargeVoltagePerCell        = 2.45,
            EndOfChargeC                   = 0.05,
            EndOfDischargeVoltagePerCell   = 1.75,
        };

    /// <summary>Builds a <see cref="StationState"/> with a specified health %.</summary>
    private static StationState MakeStateWithHealth(int healthPct) => new()
    {
        Station = 1,
        Latest  = new CadexRecord { HealthCurrent = healthPct, EventCode = 250, Station = 1 },
    };

    /// <summary>
    /// Builds a state that includes a discharge history with a given voltage sag.
    /// </summary>
    private static StationState MakeStateWithDischargeSag(int healthPct, int peakMv, int nadirMv, int? resistanceMOhm = null)
    {
        var state = new StationState
        {
            Station = 1,
            Latest  = new CadexRecord
            {
                HealthCurrent  = healthPct,
                ResistanceMOhm = resistanceMOhm,
                EventCode      = 250,
                Station        = 1,
            },
        };

        // Simulate discharge records spanning from peak to nadir voltage.
        for (int mV = peakMv; mV >= nadirMv; mV -= 50)
        {
            state.History.Add(new CadexRecord
            {
                EventCode   = 250,
                ProcessCode = 7,
                VoltageMv   = mV,
                Station     = 1,
            });
        }

        return state;
    }

    // ── Tests: CapacityFail ───────────────────────────────────────────────

    [Fact]
    public void Analyze_HealthAboveTarget_IsPass()
    {
        var analyzer = new DiagnosticAnalyzer();
        var state    = MakeStateWithHealth(85);   // 85% > 70% target
        var profile  = MakeProfile(targetCapPct: 70.0);

        var report = analyzer.Analyze(state, profile);

        Assert.True(report.IsPass);
        Assert.False(report.Classification.CapacityFail);
        Assert.StartsWith("PASS", report.TechnicianExplanation, StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_HealthBelowTarget_CapacityFail()
    {
        var analyzer = new DiagnosticAnalyzer();
        var state    = MakeStateWithHealth(29);   // 29% < 70% target
        var profile  = MakeProfile(targetCapPct: 70.0);

        var report = analyzer.Analyze(state, profile);

        Assert.False(report.IsPass);
        Assert.True(report.Classification.CapacityFail);
    }

    // ── Tests: IRSeverity ─────────────────────────────────────────────────

    [Fact]
    public void Analyze_LowIR_NormalSeverity()
    {
        var analyzer = new DiagnosticAnalyzer();
        var state = new StationState
        {
            Station = 1,
            // Latest record has low IR (e.g. 20 mΩ for a 6 V pack → 3 cells × 15 mΩ limit = 45 mΩ)
            Latest = new CadexRecord { HealthCurrent = 85, ResistanceMOhm = 20, EventCode = 250, Station = 1 },
        };
        var profile = MakeProfile(volts: 6.0);

        var report = analyzer.Analyze(state, profile);

        Assert.Equal(Severity.Normal, report.Classification.IRSeverity);
    }

    [Fact]
    public void Analyze_HighIR_CriticalSeverity()
    {
        // 145 mΩ for a 6 V (3-cell) pack → per-pack severe threshold = 3 × 50 = 150 mΩ.
        // 145 just below severe but > elevated (3 × 30 = 90). Expect Severe.
        // At 160 mΩ (> 150), expect Critical.
        var analyzer = new DiagnosticAnalyzer();
        var state = new StationState
        {
            Station = 1,
            Latest = new CadexRecord { HealthCurrent = 29, ResistanceMOhm = 160, EventCode = 250, Station = 1 },
        };
        var profile = MakeProfile(volts: 6.0);

        var report = analyzer.Analyze(state, profile);

        Assert.Equal(Severity.Critical, report.Classification.IRSeverity);
    }

    // ── Tests: Rule mapping ───────────────────────────────────────────────

    [Fact]
    public void Analyze_CriticalIR_SevereVoltageSag_InternalResistanceCollapseRule()
    {
        // Simulate the example from the problem statement:
        // IR ≈ 145–160 mΩ (critical), large voltage sag (severe/critical), low capacity.
        var analyzer = new DiagnosticAnalyzer();
        var state    = MakeStateWithDischargeSag(
            healthPct:     29,
            peakMv:        6200,
            nadirMv:       4500,  // sag = 1700 mV → per cell 1700/3 ≈ 567 mV → Critical
            resistanceMOhm: 160); // Critical for 6V pack
        var profile = MakeProfile(targetCapPct: 70.0, volts: 6.0);

        var report = analyzer.Analyze(state, profile);

        Assert.False(report.IsPass);
        Assert.Equal(Severity.Critical, report.Classification.IRSeverity);
        Assert.Contains("Internal resistance collapse", report.TechnicianExplanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Analyze_ElevatedIR_LowCapacity_SulfationRule()
    {
        // Elevated IR + capacity fail → sulfation rule
        var analyzer = new DiagnosticAnalyzer();
        var state = new StationState
        {
            Station = 1,
            Latest = new CadexRecord { HealthCurrent = 50, ResistanceMOhm = 100, EventCode = 250, Station = 1 },
        };
        var profile = MakeProfile(targetCapPct: 70.0, volts: 6.0);

        var report = analyzer.Analyze(state, profile);

        Assert.False(report.IsPass);
        Assert.Contains("Sulfation", report.TechnicianExplanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Analyze_GoodCapacity_ElevatedIR_AgingButServiceableRule()
    {
        // Good capacity + elevated IR → aging but serviceable
        var analyzer = new DiagnosticAnalyzer();
        var state = new StationState
        {
            Station = 1,
            Latest = new CadexRecord { HealthCurrent = 80, ResistanceMOhm = 60, EventCode = 250, Station = 1 },
        };
        var profile = MakeProfile(targetCapPct: 70.0, volts: 6.0); // 3 × 15 = 45 healthy, 3 × 30 = 90 elevated threshold → 60 is elevated

        var report = analyzer.Analyze(state, profile);

        Assert.False(report.IsPass);  // Elevated IR triggers IsPass = false or caution
        Assert.Contains("serviceable", report.TechnicianExplanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Analyze_NormalIR_SevereVoltageSag_ElectrolyteLossRule()
    {
        // Normal IR but severe voltage collapse → electrolyte loss
        var analyzer = new DiagnosticAnalyzer();
        var state    = MakeStateWithDischargeSag(
            healthPct:      30,
            peakMv:         6200,
            nadirMv:        4600,  // sag = 1600 mV / 3 = 533 mV per cell → Critical
            resistanceMOhm: 30);   // Normal: < 3 × 15 = 45 mΩ threshold
        var profile = MakeProfile(targetCapPct: 70.0, volts: 6.0);

        var report = analyzer.Analyze(state, profile);

        Assert.False(report.IsPass);
        Assert.Contains("Electrolyte loss", report.TechnicianExplanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Analyze_NoData_ReturnsNeutralMessage()
    {
        // State with no Latest record — should not throw and should return a safe message.
        var analyzer = new DiagnosticAnalyzer();
        var state    = new StationState { Station = 1 };
        var profile  = MakeProfile();

        var report = analyzer.Analyze(state, profile);

        Assert.False(string.IsNullOrWhiteSpace(report.TechnicianExplanation));
    }

    [Fact]
    public void Analyze_ParallelImbalance_SetWhenIRElevatedAndVoltageCollapseSevere()
    {
        var analyzer = new DiagnosticAnalyzer();
        var state    = MakeStateWithDischargeSag(
            healthPct:     29,
            peakMv:        6200,
            nadirMv:       4500,   // Critical voltage sag
            resistanceMOhm: 160);  // Critical IR
        var profile = MakeProfile(targetCapPct: 70.0, volts: 6.0);

        var report = analyzer.Analyze(state, profile);

        Assert.True(report.Classification.ParallelImbalanceProbable);
    }
}
