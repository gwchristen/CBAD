namespace CBAD.Diagnostics;

/// <summary>
/// Stage 4 final output: a complete diagnostic report for a single battery test result.
/// Produced by <see cref="DiagnosticAnalyzer.Analyze"/>.
/// </summary>
internal sealed class DiagnosticReport
{
    /// <summary>The Stage 3 classification flags that drove the inference.</summary>
    public DiagnosticClassification Classification { get; init; } = new();

    /// <summary>
    /// Human-readable technician explanation of why the battery passed or failed.
    /// Suitable for display in the UI and inclusion in printed test reports.
    /// </summary>
    public string TechnicianExplanation { get; init; } = string.Empty;

    /// <summary>
    /// <c>true</c> when all diagnostic criteria are within acceptable limits.
    /// A battery displaying only a "CAUTION – Aging" result (elevated IR, serviceable
    /// capacity) still returns <c>false</c> here because it does not strictly meet every
    /// acceptance criterion, even though it remains in service.  This naming makes the
    /// semantic distinction explicit: passing all criteria is a stricter bar than merely
    /// being serviceable.
    /// </summary>
    public required bool MeetsAcceptanceCriteria { get; init; }
}
