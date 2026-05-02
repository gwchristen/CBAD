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
            CellsInSeries                  = (int)Math.Round(volts / 2.0),
            StringsInParallel              = 2,
            NominalCellVoltage             = 2.0,
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

    // ── Tests: new granular enums and derived metrics ─────────────────────

    [Fact]
    public void Analyze_CatastrophicCellIR_ProducesCatastrophicIRClassification()
    {
        // 3S2P pack: topology divisor = 3/2 = 1.5
        // Pack IR = 150 mΩ → cell IR = 150 / 1.5 = 100 mΩ → Catastrophic (> 80)
        var analyzer = new DiagnosticAnalyzer();
        var state = new StationState
        {
            Station = 1,
            Latest = new CadexRecord { HealthCurrent = 29, ResistanceMOhm = 150, EventCode = 250, Station = 1 },
        };
        var profile = MakeProfile(targetCapPct: 70.0, volts: 6.0); // CellsInSeries=3, StringsInParallel=2

        var report = analyzer.Analyze(state, profile);

        Assert.Equal(IRStatus.Catastrophic, report.Classification.IRClassification);
        Assert.StartsWith("FAIL – Internal resistance collapse", report.TechnicianExplanation, StringComparison.Ordinal);
        Assert.Contains("3S2P", report.TechnicianExplanation, StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_CatastrophicCellIR_ExplanationContainsNominalCellVoltage()
    {
        // Ensure the rule output includes the NominalCellVoltage from the profile.
        var analyzer = new DiagnosticAnalyzer();
        var state = new StationState
        {
            Station = 1,
            Latest = new CadexRecord { HealthCurrent = 29, ResistanceMOhm = 150, EventCode = 250, Station = 1 },
        };
        var profile = MakeProfile(targetCapPct: 70.0, volts: 6.0);

        var report = analyzer.Analyze(state, profile);

        Assert.Contains("2V cell", report.TechnicianExplanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Analyze_CalculatedCellIR_UsesTopologyDivisor()
    {
        // 3S2P: divisor = 1.5 → pack IR 90 mΩ → cell IR = 60 mΩ → Fail (> 40, ≤ 80)
        var analyzer = new DiagnosticAnalyzer();
        var state = new StationState
        {
            Station = 1,
            Latest = new CadexRecord { HealthCurrent = 80, ResistanceMOhm = 90, EventCode = 250, Station = 1 },
        };
        var profile = MakeProfile(targetCapPct: 70.0, volts: 6.0);

        var report = analyzer.Analyze(state, profile);

        Assert.Equal(60.0, report.Classification.CalculatedCellIR, precision: 1);
        Assert.Equal(IRStatus.Fail, report.Classification.IRClassification);
    }

    [Fact]
    public void Analyze_CapacityRatio_IsNormalisedTo0To1()
    {
        var analyzer = new DiagnosticAnalyzer();
        var state    = MakeStateWithHealth(75);
        var profile  = MakeProfile(targetCapPct: 70.0);

        var report = analyzer.Analyze(state, profile);

        Assert.Equal(0.75, report.Classification.CapacityRatio, precision: 2);
        Assert.Equal(CapacityStatus.Pass, report.Classification.CapacityClassification);
    }

    [Fact]
    public void Analyze_CapacityRatio_MarginalBand()
    {
        // 60% health → 0.60 ratio → Marginal (0.50–0.69)
        var analyzer = new DiagnosticAnalyzer();
        var state    = MakeStateWithHealth(60);
        var profile  = MakeProfile(targetCapPct: 70.0);

        var report = analyzer.Analyze(state, profile);

        Assert.Equal(0.60, report.Classification.CapacityRatio, precision: 2);
        Assert.Equal(CapacityStatus.Marginal, report.Classification.CapacityClassification);
    }

    [Fact]
    public void Analyze_SagPerCell_OhmicCollapseClassification()
    {
        // 3S pack: sag = 6200 - 3800 = 2400 mV → per cell = 2400/3/1000 = 0.8 V → OhmicCollapse (> 0.3V)
        var analyzer = new DiagnosticAnalyzer();
        var state    = MakeStateWithDischargeSag(
            healthPct:     29,
            peakMv:        6200,
            nadirMv:       3800,
            resistanceMOhm: 150);
        var profile = MakeProfile(targetCapPct: 70.0, volts: 6.0);

        var report = analyzer.Analyze(state, profile);

        Assert.Equal(SagStatus.OhmicCollapse, report.Classification.SagClassification);
    }

    [Fact]
    public void Analyze_ParallelImbalanceConfirmed_WhenIRFailAndSagOhmicCollapse()
    {
        // IR = 150 mΩ → cell IR = 100 mΩ → Catastrophic (>= Fail)
        // Sag = 2400 mV / 3 / 1000 = 0.8 V → OhmicCollapse
        var analyzer = new DiagnosticAnalyzer();
        var state    = MakeStateWithDischargeSag(
            healthPct:     29,
            peakMv:        6200,
            nadirMv:       3800,
            resistanceMOhm: 150);
        var profile = MakeProfile(targetCapPct: 70.0, volts: 6.0);

        var report = analyzer.Analyze(state, profile);

        Assert.True(report.Classification.ParallelImbalanceConfirmed);
    }

    [Fact]
    public void Analyze_ParallelImbalanceLikely_WhenIRPoorAndCapacityFail()
    {
        // 3S2P: pack IR 75 mΩ → cell IR = 75/1.5 = 50 mΩ → Fail (>40, ≤80)
        // Capacity = 29% → Fail (< 0.50)
        var analyzer = new DiagnosticAnalyzer();
        var state = new StationState
        {
            Station = 1,
            Latest = new CadexRecord { HealthCurrent = 29, ResistanceMOhm = 75, EventCode = 250, Station = 1 },
        };
        var profile = MakeProfile(targetCapPct: 70.0, volts: 6.0);

        var report = analyzer.Analyze(state, profile);

        Assert.True(report.Classification.ParallelImbalanceLikely);
    }

    [Fact]
    public void BatteryProfile_DefaultTopologyValues_AreCorrect()
    {
        var profile = new BatteryProfile();

        Assert.Equal(3, profile.CellsInSeries);
        Assert.Equal(2, profile.StringsInParallel);
        Assert.Equal(2.0, profile.NominalCellVoltage);
    }
}
