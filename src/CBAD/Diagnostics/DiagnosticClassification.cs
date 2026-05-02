namespace CBAD.Diagnostics;

/// <summary>
/// Stage 3 classification flags produced by comparing raw test data against
/// the active <see cref="CBAD.Models.BatteryProfile"/> limits.
/// Each axis is independent; a battery may fail on one axis while passing all others.
/// </summary>
internal sealed class DiagnosticClassification
{
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
}
