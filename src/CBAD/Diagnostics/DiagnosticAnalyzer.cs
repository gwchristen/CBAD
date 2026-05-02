using CBAD.Models;

namespace CBAD.Diagnostics;

/// <summary>
/// Implements the Stage 3 (classification) and Stage 4 (failure-reason inference)
/// layers of the 4-stage battery diagnostic model.
/// </summary>
/// <remarks>
/// Stage 1 (signal extraction) and Stage 2 (normalisation to chemistry / topology)
/// are handled upstream by the serial-capture service and <see cref="BatteryProfile"/>.
/// This class consumes the already-parsed <see cref="CBAD.Models.StationState"/> and
/// compares its fields against the limits stored in the active <see cref="BatteryProfile"/>
/// to produce a structured <see cref="DiagnosticReport"/>.
/// </remarks>
internal sealed class DiagnosticAnalyzer
{
    // ── Legacy IR thresholds (mΩ per cell, used for Severity enum) ────────
    private const double IrNormalMOhmPerCell   = 15.0;
    private const double IrElevatedMOhmPerCell = 30.0;
    private const double IrSevereMOhmPerCell   = 50.0;

    // ── Granular per-cell IR thresholds (mΩ) – per problem spec ──────────
    private const double IrExcellentMOhmPerCell   =  5.0;
    private const double IrGoodMOhmPerCell        = 10.0;
    private const double IrAcceptableMOhmPerCell  = 20.0;
    private const double IrPoorMOhmPerCell        = 40.0;
    private const double IrFailMOhmPerCell        = 80.0;

    // ── Voltage-sag thresholds (mV per cell, during discharge) ────────────
    private const double VsagNormalMvPerCell   = 100;
    private const double VsagElevatedMvPerCell = 200;
    private const double VsagSevereMvPerCell   = 350;

    // ── Granular sag thresholds (V per cell) ─────────────────────────────
    private const double SagNormalVPerCell  = 0.1;
    private const double SagAgingVPerCell   = 0.3;

    // ── Temperature-deviation thresholds (°C above or below safe range) ──
    private const double TempDeviationElevatedC = 5;
    private const double TempDeviationSevereC   = 15;

    // ── ΔT threshold for resistive heating ───────────────────────────────
    private const double DeltaTResistiveHeatingC = 5.0;

    // ── Default cell count when pack voltage is unknown ───────────────────
    private const double DefaultCellCount = 6;

    /// <summary>
    /// Runs a full diagnostic analysis and returns a <see cref="DiagnosticReport"/>
    /// containing the Stage 3 classification and a Stage 4 technician explanation.
    /// </summary>
    /// <param name="state">Current station state with parsed test data.</param>
    /// <param name="profile">Active battery profile defining expected envelopes.</param>
    public DiagnosticReport Analyze(StationState state, BatteryProfile profile)
    {
        var classification = ClassifyData(state, profile);
        var explanation    = EvaluateRules(classification, state, profile);

        bool isPass = !classification.CapacityFail
                      && classification.IRSeverity                 <= Severity.Normal
                      && classification.VoltageCollapseSeverity    <= Severity.Normal
                      && classification.ThermalAbnormalitySeverity <= Severity.Normal;

        return new DiagnosticReport
        {
            Classification        = classification,
            TechnicianExplanation = explanation,
            IsPass                = isPass,
        };
    }

    // ── Stage 3: Classification ───────────────────────────────────────────

    /// <summary>
    /// Compares raw test data from <paramref name="state"/> against the limits defined
    /// in <paramref name="profile"/> to produce a set of orthogonal failure-axis flags.
    /// Populates both the legacy <see cref="Severity"/>-based fields and the new
    /// granular enum classifications and derived metrics.
    /// </summary>
    private static DiagnosticClassification ClassifyData(StationState state, BatteryProfile profile)
    {
        var latest = state.Latest;
        var c      = new DiagnosticClassification();

        // ── Stage 2: Derived metrics ──────────────────────────────────────
        // Topology divisor: Rpack = Rcell × series / parallel → Rcell = Rpack / (series/parallel)
        double topologyDivisor = profile.CellsInSeries > 0 && profile.StringsInParallel > 0
            ? (double)profile.CellsInSeries / profile.StringsInParallel
            : 1.0;

        // ── Capacity ──────────────────────────────────────────────────────
        // Health is reported as an integer percentage in CadexRecord.HealthCurrent.
        if (latest?.HealthCurrent is int healthPct)
        {
            c.CapacityFail  = healthPct < profile.TargetCapPercentage;
            c.CapacityRatio = healthPct / 100.0;

            c.CapacityClassification = c.CapacityRatio switch
            {
                >= 0.70 => CapacityStatus.Pass,
                >= 0.50 => CapacityStatus.Marginal,
                _       => CapacityStatus.Fail,
            };
        }

        // ── Internal Resistance ───────────────────────────────────────────
        // ResistanceMOhm is the pack resistance from the OhmTest phase (event 131).
        if (latest?.ResistanceMOhm is int irMOhm)
        {
            // Legacy Severity classification (uses estimated cell count from voltage)
            double nominalCells       = EstimateCellCount(profile.Volts);
            double healthyPackIrMOhm  = nominalCells * IrNormalMOhmPerCell;
            double elevatedPackIrMOhm = nominalCells * IrElevatedMOhmPerCell;
            double severePackIrMOhm   = nominalCells * IrSevereMOhmPerCell;

            c.IRSeverity = irMOhm switch
            {
                _ when irMOhm <= healthyPackIrMOhm  => Severity.Normal,
                _ when irMOhm <= elevatedPackIrMOhm => Severity.Elevated,
                _ when irMOhm <= severePackIrMOhm   => Severity.Severe,
                _                                    => Severity.Critical,
            };

            // Granular per-cell IR classification (uses explicit topology from profile)
            c.CalculatedCellIR = irMOhm / topologyDivisor;

            c.IRClassification = c.CalculatedCellIR switch
            {
                <= IrExcellentMOhmPerCell  => IRStatus.Excellent,
                <= IrGoodMOhmPerCell       => IRStatus.Good,
                <= IrAcceptableMOhmPerCell => IRStatus.Acceptable,
                <= IrPoorMOhmPerCell       => IRStatus.Poor,
                <= IrFailMOhmPerCell       => IRStatus.Fail,
                _                          => IRStatus.Catastrophic,
            };
        }

        // ── Voltage Collapse ──────────────────────────────────────────────
        // Inspect the discharge history (ProcessCode 7, EventCode 250) to measure
        // the peak-to-nadir voltage sag and normalise it per cell.
        var dischargeRecords = state.History
            .Where(r => r.EventCode == 250 && r.ProcessCode == 7 && r.VoltageMv.HasValue)
            .ToList();

        if (dischargeRecords.Count >= 2)
        {
            double peakMv       = dischargeRecords.Max(r => r.VoltageMv!.Value);
            double nadirMv      = dischargeRecords.Min(r => r.VoltageMv!.Value);
            double sagMv        = peakMv - nadirMv;
            double nominalCells = EstimateCellCount(profile.Volts);
            double sagPerCellMv = sagMv / nominalCells;

            c.VoltageCollapseSeverity = sagPerCellMv switch
            {
                <= VsagNormalMvPerCell   => Severity.Normal,
                <= VsagElevatedMvPerCell => Severity.Elevated,
                <= VsagSevereMvPerCell   => Severity.Severe,
                _                        => Severity.Critical,
            };

            // Granular sag classification (V per cell, using explicit CellsInSeries)
            int seriesCells = profile.CellsInSeries > 0 ? profile.CellsInSeries : (int)nominalCells;
            c.SagPerCell = sagMv / 1000.0 / seriesCells;

            c.SagClassification = c.SagPerCell switch
            {
                < SagNormalVPerCell => SagStatus.Normal,
                <= SagAgingVPerCell => SagStatus.Aging,
                _                   => SagStatus.OhmicCollapse,
            };
        }

        // ── Thermal Abnormality ────────────────────────────────────────────
        // Compare the maximum temperature observed during the session against the
        // profile's safe operating range.
        var tempRecords = state.History
            .Where(r => r.TemperatureC.HasValue)
            .ToList();

        if (tempRecords.Count > 0)
        {
            double maxTemp   = tempRecords.Max(r => r.TemperatureC!.Value);
            double overMax   = maxTemp - profile.MaxTempCelsius;
            double underMin  = profile.MinTempCelsius - maxTemp;
            double deviation = Math.Max(overMax, underMin);

            c.ThermalAbnormalitySeverity = deviation switch
            {
                <= 0                        => Severity.Normal,
                <= TempDeviationElevatedC   => Severity.Elevated,
                <= TempDeviationSevereC     => Severity.Severe,
                _                           => Severity.Critical,
            };

            // Granular thermal classification: DeltaT = rise across the session
            double minTemp = tempRecords.Min(r => r.TemperatureC!.Value);
            c.DeltaTemp           = maxTemp - minTemp;
            c.ThermalClassification = c.DeltaTemp > DeltaTResistiveHeatingC
                ? ThermalStatus.ResistiveHeating
                : ThermalStatus.Normal;
        }

        // ── Parallel Imbalance ────────────────────────────────────────────
        // Legacy flag: elevated/critical IR + severe/critical voltage collapse.
        c.ParallelImbalanceProbable =
            c.IRSeverity >= Severity.Elevated &&
            c.VoltageCollapseSeverity >= Severity.Severe;

        // Granular flags based on the new enum classifications.
        c.ParallelImbalanceLikely =
            c.IRClassification >= IRStatus.Poor &&
            c.CapacityClassification == CapacityStatus.Fail;

        c.ParallelImbalanceConfirmed =
            c.IRClassification >= IRStatus.Fail &&
            c.SagClassification == SagStatus.OhmicCollapse;

        return c;
    }

    // ── Stage 4: Rule Evaluation ─────────────────────────────────────────

    /// <summary>
    /// Maps combinations of classification flags to a human-readable technician
    /// explanation using a prioritised rule table.
    /// </summary>
    private static string EvaluateRules(
        DiagnosticClassification flags,
        StationState state,
        BatteryProfile profile)
    {
        // ── All-pass ──────────────────────────────────────────────────────
        if (!flags.CapacityFail
            && flags.IRSeverity                 <= Severity.Normal
            && flags.VoltageCollapseSeverity    <= Severity.Normal
            && flags.ThermalAbnormalitySeverity <= Severity.Normal)
        {
            return "PASS – Battery meets all acceptance criteria. " +
                   "Capacity, internal resistance, and voltage behaviour are within normal limits.";
        }

        // ── Rule 1: Catastrophic IR – internal resistance collapse ────────
        // Catastrophic per-cell IR (> 80 mΩ) with ohmic sag collapse.
        // In a multi-parallel topology this is the signature of a failed parallel leg.
        if (flags.IRClassification == IRStatus.Catastrophic)
        {
            string cellIrStr = flags.CalculatedCellIR > 0
                ? $"{flags.CalculatedCellIR:F0}"
                : (state.Latest?.ResistanceMOhm?.ToString() ?? "?");

            return $"FAIL – Internal resistance collapse\n" +
                   $"Battery exhibits severely elevated internal resistance ({cellIrStr} mΩ per " +
                   $"{profile.NominalCellVoltage}V cell), causing excessive voltage sag under load " +
                   $"and premature discharge cutoff. In a " +
                   $"{profile.CellsInSeries}S{profile.StringsInParallel}P SLA configuration, this " +
                   $"behavior indicates failure of one or more parallel cells, resulting in loss of " +
                   $"current sharing and effective capacity. Battery does not meet minimum performance " +
                   $"requirements.";
        }

        // ── Rule 2: Critical severity IR + severe voltage sag ─────────────
        // (Legacy path for packs where topology is not yet set in the profile)
        if (flags.IRSeverity == Severity.Critical && flags.VoltageCollapseSeverity >= Severity.Severe)
        {
            double nominalCells = EstimateCellCount(profile.Volts);
            string topoNote     = $"{nominalCells:0}S topology";
            int?   irMOhm       = state.Latest?.ResistanceMOhm;
            string irNote       = irMOhm.HasValue ? $" Measured IR: {irMOhm} mΩ." : string.Empty;
            string parallelNote = flags.ParallelImbalanceProbable
                ? " Parallel cell imbalance is probable — one or more parallel legs may have failed, reducing current-sharing capacity and accelerating voltage sag."
                : string.Empty;

            return $"FAIL – Internal resistance collapse. " +
                   $"Measured internal resistance indicates severe cell degradation causing excessive voltage sag under load.{irNote} " +
                   $"In a {topoNote}, this pattern is consistent with advanced plate degradation or active material shedding." +
                   parallelNote;
        }

        // ── Rule 3: Sulfation / plate degradation ─────────────────────────
        // High IR (Elevated or above) + failed capacity
        if (flags.IRSeverity >= Severity.Elevated && flags.CapacityFail)
        {
            return "FAIL – Sulfation / plate degradation suspected. " +
                   "Elevated internal resistance combined with reduced capacity indicates lead-sulfate crystal build-up " +
                   "on the active plate material, a common end-of-life failure mode in SLA batteries. " +
                   "Reconditioning cycles are unlikely to restore full capacity at this stage.";
        }

        // ── Rule 4: Aging but serviceable ────────────────────────────────
        // Good capacity + bad IR (Elevated or Severe)
        if (!flags.CapacityFail && flags.IRSeverity >= Severity.Elevated)
        {
            return "CAUTION – Aging detected, currently serviceable. " +
                   "Capacity is still within the acceptance threshold; however, internal resistance is elevated, " +
                   "which will cause increased voltage sag under heavy loads. " +
                   "Monitor closely and plan replacement at the next scheduled service interval.";
        }

        // ── Rule 5: Electrolyte loss ──────────────────────────────────────
        // Good IR + early voltage knee (severe voltage collapse without elevated IR)
        if (flags.IRSeverity <= Severity.Normal && flags.VoltageCollapseSeverity >= Severity.Severe)
        {
            return "FAIL – Electrolyte loss suspected. " +
                   "Internal resistance is within normal limits but a premature voltage knee was observed during discharge. " +
                   "This pattern is consistent with electrolyte loss (drying out), " +
                   "reduced active plate surface area, or separator damage.";
        }

        // ── Rule 6: Thermal fault ──────────────────────────────────────────
        if (flags.ThermalAbnormalitySeverity >= Severity.Severe)
        {
            return $"FAIL – Thermal abnormality detected. " +
                   $"Battery temperature exceeded the safe operating range defined in the active profile " +
                   $"({profile.MinTempCelsius}–{profile.MaxTempCelsius} °C). " +
                   "Excessive heat during charge or discharge accelerates electrolyte loss and plate corrosion.";
        }

        // ── Rule 7: Capacity-only failure with normal IR ──────────────────
        if (flags.CapacityFail && flags.IRSeverity == Severity.Normal)
        {
            return "FAIL – Capacity below acceptance threshold. " +
                   "Internal resistance is within normal limits, suggesting the battery retains structural integrity " +
                   "but has experienced active material loss or permanent capacity fade. " +
                   "May be suitable for standby / float applications with reduced load expectations.";
        }

        // ── Fallback: multiple mild deviations ────────────────────────────
        var parts = new List<string>();
        if (flags.CapacityFail)                                  parts.Add("reduced capacity");
        if (flags.IRSeverity > Severity.Normal)                  parts.Add($"{flags.IRSeverity.ToString().ToLower()} IR");
        if (flags.VoltageCollapseSeverity > Severity.Normal)     parts.Add($"{flags.VoltageCollapseSeverity.ToString().ToLower()} voltage sag");
        if (flags.ThermalAbnormalitySeverity > Severity.Normal)  parts.Add($"{flags.ThermalAbnormalitySeverity.ToString().ToLower()} thermal deviation");

        return parts.Count > 0
            ? $"FAIL – Multiple deviations detected: {string.Join(", ", parts)}. Manual inspection recommended."
            : "No significant failure indicators detected. Verify that the test completed normally before accepting this result.";
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Estimates the number of series cells from the pack nominal voltage,
    /// assuming 2 V per cell (standard for SLA chemistry).
    /// Returns at least 1 so division is always safe.
    /// </summary>
    private static double EstimateCellCount(double packVoltage)
        => packVoltage > 0 ? Math.Max(1, Math.Round(packVoltage / 2.0)) : DefaultCellCount;
}

