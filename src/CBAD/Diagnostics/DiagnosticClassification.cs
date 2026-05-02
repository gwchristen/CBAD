namespace CBAD.Diagnostics;

// ── New granular classification enums ────────────────────────────────────────

/// <summary>Capacity acceptance status based on the delivered / target ratio.</summary>
internal enum CapacityStatus
{
    /// <summary>Capacity ratio ≥ 0.70 – battery meets the acceptance threshold.</summary>
    Pass,
    /// <summary>Capacity ratio 0.50–0.69 – below target but not critically low.</summary>
    Marginal,
    /// <summary>Capacity ratio &lt; 0.50 – battery has critically insufficient capacity.</summary>
    Fail,
}

/// <summary>
/// Internal-resistance classification for a single 2 V SLA cell.
/// Thresholds are per-cell values derived from the pack measurement.
/// </summary>
internal enum IRStatus
{
    /// <summary>≤ 5 mΩ – new or near-new condition.</summary>
    Excellent,
    /// <summary>≤ 10 mΩ – healthy operating range.</summary>
    Good,
    /// <summary>≤ 20 mΩ – capacity likely reduced under load.</summary>
    Acceptable,
    /// <summary>≤ 40 mΩ – high voltage sag expected under load.</summary>
    Poor,
    /// <summary>≤ 80 mΩ – structural or plate failure.</summary>
    Fail,
    /// <summary>&gt; 80 mΩ – parallel leg or cell collapse.</summary>
    Catastrophic,
}

/// <summary>Voltage-sag classification normalised to a per-cell value.</summary>
internal enum SagStatus
{
    /// <summary>Sag &lt; 0.1 V per cell – normal load response.</summary>
    Normal,
    /// <summary>Sag 0.1–0.3 V per cell – consistent with an aging plate.</summary>
    Aging,
    /// <summary>Sag &gt; 0.3 V per cell – ohmic collapse under load.</summary>
    OhmicCollapse,
}

/// <summary>Thermal status based on temperature rise during the test.</summary>
internal enum ThermalStatus
{
    /// <summary>ΔT ≤ 5 °C – no abnormal heating detected.</summary>
    Normal,
    /// <summary>ΔT &gt; 5 °C – resistive heating detected, consistent with high IR.</summary>
    ResistiveHeating,
}

/// <summary>
/// Stage 3 classification flags produced by comparing raw test data against
/// the active <see cref="CBAD.Models.BatteryProfile"/> limits.
/// Each axis is independent; a battery may fail on one axis while passing all others.
/// </summary>
internal sealed class DiagnosticClassification
{
    // ── Legacy fields (retained for backward compatibility) ───────────────

    /// <summary>
    /// <c>true</c> when the measured capacity falls below the target threshold
    /// defined in the active profile.
    /// </summary>
    public bool CapacityFail { get; set; }

    /// <summary>
    /// Severity of internal-resistance degradation measured during the OhmTest phase.
    /// </summary>
    public Severity IRSeverity { get; set; }

    /// <summary>
    /// Severity of voltage collapse observed during the discharge phase.
    /// Derived from the peak-to-nadir voltage sag normalised per cell.
    /// </summary>
    public Severity VoltageCollapseSeverity { get; set; }

    /// <summary>
    /// Severity of any thermal abnormality detected during the test session.
    /// Compared against the temperature range defined in the active profile.
    /// </summary>
    public Severity ThermalAbnormalitySeverity { get; set; }

    /// <summary>
    /// <c>true</c> when the combined IR and voltage-collapse pattern suggests that
    /// one or more parallel cell legs have failed, reducing current-sharing capacity.
    /// </summary>
    public bool ParallelImbalanceProbable { get; set; }

    // ── Stage 2: Derived metrics ──────────────────────────────────────────

    /// <summary>
    /// Delivered capacity as a fraction of the rated capacity (0–1 scale).
    /// Calculated as <c>healthPercent / 100.0</c>.
    /// </summary>
    public double CapacityRatio { get; set; }

    /// <summary>
    /// Internal resistance normalised to a single 2 V cell (mΩ).
    /// Calculated as <c>measuredPackIR / (CellsInSeries / StringsInParallel)</c>.
    /// </summary>
    public double CalculatedCellIR { get; set; }

    /// <summary>
    /// Peak-to-nadir voltage sag during discharge, normalised per series cell (V).
    /// Calculated as <c>totalSagVolts / CellsInSeries</c>.
    /// </summary>
    public double SagPerCell { get; set; }

    /// <summary>
    /// Temperature rise (max − min) observed across all records in the test session (°C).
    /// </summary>
    public double DeltaTemp { get; set; }

    // ── Stage 3: Granular classifications ─────────────────────────────────

    /// <summary>Capacity acceptance classification using the <see cref="CapacityStatus"/> scale.</summary>
    public CapacityStatus CapacityClassification { get; set; }

    /// <summary>Per-cell internal-resistance classification using the <see cref="IRStatus"/> scale.</summary>
    public IRStatus IRClassification { get; set; }

    /// <summary>Voltage-sag classification using the <see cref="SagStatus"/> scale.</summary>
    public SagStatus SagClassification { get; set; }

    /// <summary>Thermal classification using the <see cref="ThermalStatus"/> scale.</summary>
    public ThermalStatus ThermalClassification { get; set; }

    // ── Parallel imbalance flags ──────────────────────────────────────────

    /// <summary>
    /// <c>true</c> when IR is Poor or worse AND capacity has failed — parallel imbalance
    /// is a probable contributing factor but cannot be confirmed from electrical data alone.
    /// </summary>
    public bool ParallelImbalanceLikely { get; set; }

    /// <summary>
    /// <c>true</c> when IR is Fail or worse AND sag has reached OhmicCollapse — the
    /// combined electrical signature strongly confirms a failed parallel leg.
    /// </summary>
    public bool ParallelImbalanceConfirmed { get; set; }
}
