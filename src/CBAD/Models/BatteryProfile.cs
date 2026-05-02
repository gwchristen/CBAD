namespace CBAD.Models;

/// <summary>
/// Represents a complete set of C-code test parameters for a Cadex 7400 series
/// battery analyzer.  Instances are stored and loaded via <see cref="ProfileManager"/>.
/// </summary>
public sealed class BatteryProfile
{
    /// <summary>User-visible name that uniquely identifies this profile.</summary>
    public string ProfileName { get; set; } = string.Empty;

    /// <summary>Target capacity / health expressed as a percentage (0–100).</summary>
    public double TargetCapPercentage { get; set; }

    /// <summary>Battery chemistry / type (e.g. "Li-Ion", "NiMH").</summary>
    public string BatteryType { get; set; } = string.Empty;

    /// <summary>Nominal battery pack voltage.</summary>
    public double Volts { get; set; }

    /// <summary>Battery pack capacity in milliamp-hours.</summary>
    public double CapacityMAh { get; set; }

    /// <summary>
    /// Charge current expressed as a C-ratio (0–1).
    /// 1 C = the <see cref="CapacityMAh"/> value in mA over 60 minutes.
    /// </summary>
    public double ChargeCurrentC { get; set; }

    /// <summary>
    /// Discharge current expressed as a C-ratio (0–1).
    /// 1 C = the <see cref="CapacityMAh"/> value in mA over 60 minutes.
    /// </summary>
    public double DischargeCurrentC { get; set; }

    /// <summary>Minimum safe operating temperature in degrees Celsius.</summary>
    public double MinTempCelsius { get; set; }

    /// <summary>Maximum safe operating temperature in degrees Celsius.</summary>
    public double MaxTempCelsius { get; set; }

    /// <summary>Maximum acceptable resting / standby voltage per cell.</summary>
    public double MaxStandbyVoltagePerCell { get; set; }

    /// <summary>Maximum acceptable charge voltage per cell.</summary>
    public double MaxChargeVoltagePerCell { get; set; }

    /// <summary>End-of-Charge (EoC) threshold expressed as a C-ratio (0–1).</summary>
    public double EndOfChargeC { get; set; }

    /// <summary>Minimum accepted voltage per cell at the end of discharge.</summary>
    public double EndOfDischargeVoltagePerCell { get; set; }
}
