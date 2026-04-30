namespace CBAD.Reporting;

public sealed record ReportData
{
    public string Station { get; init; } = string.Empty;
    public string WorkOrder { get; init; } = string.Empty;
    public string BatterySerial { get; init; } = string.Empty;
    public bool PassedVisualInspection { get; init; }
    public string Notes { get; init; } = string.Empty;
    public string ChartImageBase64 { get; init; } = string.Empty;
    
    // Test parameters
    public string Date { get; init; } = string.Empty;
    public string FinalStatus { get; init; } = string.Empty;
    public string ProcessCode { get; init; } = string.Empty;
    public string TargetCapacity { get; init; } = string.Empty;
    public string FinalVoltage { get; init; } = string.Empty;
    public string FinalCurrent { get; init; } = string.Empty;
    public string FinalHealth { get; init; } = string.Empty;
    public string Resistance { get; init; } = string.Empty;
}
