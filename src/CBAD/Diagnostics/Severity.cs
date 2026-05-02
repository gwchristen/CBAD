namespace CBAD.Diagnostics;

/// <summary>
/// Severity levels used by the battery diagnostic classification engine.
/// Values are ordered so that comparisons such as <c>&gt;= Severity.Elevated</c> work correctly.
/// </summary>
internal enum Severity
{
    /// <summary>Metric is within the expected healthy envelope.</summary>
    Normal,

    /// <summary>Metric is outside the healthy envelope but not yet critical.</summary>
    Elevated,

    /// <summary>Metric indicates significant degradation that will impair service life.</summary>
    Severe,

    /// <summary>Metric indicates failure or imminent failure.</summary>
    Critical,
}
