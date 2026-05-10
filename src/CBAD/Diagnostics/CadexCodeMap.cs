namespace CBAD.Diagnostics;

internal enum DiagnosticAxis
{
    None,
    Capacity,
    Resistance,
    VoltageBehavior,
    ChargeAcceptance,
    Thermal,
    Stability,
    CurrentDelivery,
    TestValidity,
    SystemCondition,
}

internal enum FailureMode
{
    None,
    DegradedChemistry,
    HighImpedance,
    InternalShort,
    Reversal,
    OverVoltage,
    ThermalRunaway,
    VoltageInstability,
    ChargeInstability,
    IntermittentConnection,
    SensorFailure,
    HardwareFailure,
    ConfigurationError,
    InterruptedTest,
}

internal sealed record CadexCodeDefinition(
    int Code,
    string Message,
    DiagnosticAxis Axis,
    FailureMode Mode,
    bool ForcesFail = false,
    bool InvalidatesTest = false,
    bool IsAdvisory = false);

internal static class CadexCodeMap
{
    private static readonly IReadOnlyDictionary<int, CadexCodeDefinition> _definitions = Build();

    public static bool TryGetDefinition(int code, out CadexCodeDefinition definition) =>
        _definitions.TryGetValue(code, out definition!);

    private static Dictionary<int, CadexCodeDefinition> Build()
    {
        var map = new Dictionary<int, CadexCodeDefinition>();

        static void Add(
            Dictionary<int, CadexCodeDefinition> target,
            int code,
            string message,
            DiagnosticAxis axis,
            FailureMode mode,
            bool forcesFail = false,
            bool invalidatesTest = false,
            bool isAdvisory = false)
            => target[code] = new(code, message, axis, mode, forcesFail, invalidatesTest, isAdvisory);

        static void AddRange(
            Dictionary<int, CadexCodeDefinition> target,
            int min,
            int max,
            string message,
            DiagnosticAxis axis,
            FailureMode mode,
            bool forcesFail = false,
            bool invalidatesTest = false,
            bool isAdvisory = false)
        {
            for (int code = min; code <= max; code++)
                Add(target, code, message, axis, mode, forcesFail, invalidatesTest, isAdvisory);
        }

        Add(map, 129, "Intermittent Battery", DiagnosticAxis.Stability, FailureMode.IntermittentConnection, forcesFail: true);
        Add(map, 128, "Unable to Clamp Charge Voltage", DiagnosticAxis.ChargeAcceptance, FailureMode.ChargeInstability, forcesFail: true);
        AddRange(map, 123, 127, "Low Voltage", DiagnosticAxis.VoltageBehavior, FailureMode.VoltageInstability, forcesFail: true);
        AddRange(map, 135, 136, "High Cell Resistance", DiagnosticAxis.Resistance, FailureMode.HighImpedance, forcesFail: true);
        Add(map, 115, "Target Capacity Not Met", DiagnosticAxis.Capacity, FailureMode.DegradedChemistry, forcesFail: true);
        Add(map, 14, "Battery Over Temperature", DiagnosticAxis.Thermal, FailureMode.ThermalRunaway, forcesFail: true);
        Add(map, 112, "Cell Mismatch", DiagnosticAxis.Stability, FailureMode.None, isAdvisory: true);
        Add(map, 113, "Plateau Timeout", DiagnosticAxis.ChargeAcceptance, FailureMode.ChargeInstability, forcesFail: true);
        Add(map, 130, "Current Rise at Full Charge", DiagnosticAxis.ChargeAcceptance, FailureMode.None, isAdvisory: true);
        Add(map, 146, "Recondition Timeout", DiagnosticAxis.ChargeAcceptance, FailureMode.ChargeInstability, forcesFail: true);
        Add(map, 154, "Charge Complete Temp Rise", DiagnosticAxis.Thermal, FailureMode.ThermalRunaway, forcesFail: true);
        Add(map, 179, "Unable to Learn Matrix", DiagnosticAxis.ChargeAcceptance, FailureMode.ChargeInstability, forcesFail: true);
        Add(map, 152, "Rapid Heat Rise", DiagnosticAxis.Thermal, FailureMode.ThermalRunaway, forcesFail: true);
        Add(map, 156, "Hot Battery, Low Voltage", DiagnosticAxis.Thermal, FailureMode.ThermalRunaway, forcesFail: true);
        Add(map, 158, "Heat Termination", DiagnosticAxis.Thermal, FailureMode.ThermalRunaway, forcesFail: true);
        Add(map, 159, "Hot Battery on Trickle Charge", DiagnosticAxis.Thermal, FailureMode.ThermalRunaway, forcesFail: true);
        Add(map, 120, "Over Voltage", DiagnosticAxis.SystemCondition, FailureMode.OverVoltage, forcesFail: true);
        Add(map, 121, "Battery Reversed", DiagnosticAxis.SystemCondition, FailureMode.Reversal, forcesFail: true);
        Add(map, 122, "Battery Shorted", DiagnosticAxis.SystemCondition, FailureMode.InternalShort, forcesFail: true);
        Add(map, 162, "Discharge Current Low", DiagnosticAxis.CurrentDelivery, FailureMode.HighImpedance, forcesFail: true);
        Add(map, 160, "Bad Fuse or Driver", DiagnosticAxis.SystemCondition, FailureMode.HardwareFailure, invalidatesTest: true);
        Add(map, 164, "Charge Current Low", DiagnosticAxis.SystemCondition, FailureMode.HardwareFailure, invalidatesTest: true);
        Add(map, 150, "Thermistor Failure", DiagnosticAxis.SystemCondition, FailureMode.SensorFailure, invalidatesTest: true);
        AddRange(map, 170, 172, "Configuration / Setup Fault", DiagnosticAxis.SystemCondition, FailureMode.ConfigurationError, invalidatesTest: true);
        AddRange(map, 208, 214, "Configuration / Setup Fault", DiagnosticAxis.SystemCondition, FailureMode.ConfigurationError, invalidatesTest: true);
        Add(map, 177, "Battery Undercharged", DiagnosticAxis.Capacity, FailureMode.None, isAdvisory: true);
        Add(map, 178, "Battery Overcharged", DiagnosticAxis.Capacity, FailureMode.None, isAdvisory: true);
        Add(map, 1, "No Adapter", DiagnosticAxis.TestValidity, FailureMode.InterruptedTest, invalidatesTest: true);
        Add(map, 10, "No Battery", DiagnosticAxis.TestValidity, FailureMode.InterruptedTest, invalidatesTest: true);
        Add(map, 17, "Battery Removed", DiagnosticAxis.TestValidity, FailureMode.InterruptedTest, invalidatesTest: true);
        Add(map, 18, "Process Suspended", DiagnosticAxis.TestValidity, FailureMode.InterruptedTest, invalidatesTest: true);
        Add(map, 188, "Service Interrupted", DiagnosticAxis.TestValidity, FailureMode.InterruptedTest, invalidatesTest: true);
        Add(map, 144, "Charge Timeout", DiagnosticAxis.ChargeAcceptance, FailureMode.ChargeInstability, forcesFail: true);
        Add(map, 142, "Discharge Timeout", DiagnosticAxis.ChargeAcceptance, FailureMode.ChargeInstability, forcesFail: true);

        return map;
    }
}
