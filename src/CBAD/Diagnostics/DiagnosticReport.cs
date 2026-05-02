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
    /// </summary>
    public bool IsPass { get; init; }
}
