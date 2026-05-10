using CBAD.Models;
using CBAD.Diagnostics;

namespace CBAD.Parsing;

/// <summary>
/// Context-aware parser and formatter for Cadex C7x00 event codes.
/// Maps every code defined in the C7x00-C User Manual (Appendix A, pages 125-147)
/// to a human-readable description and, where applicable, formats the trailing
/// payload value using the correct unit (mΩ, %, etc.).
/// </summary>
internal static class CadexEventParser
{
    // ── Event code → detailed message map (Appendix A, By Code table) ──────

    private static readonly Dictionary<int, string> _descriptions = new()
    {
        {   0, "Station Off Line" },
        {   1, "No Adapter" },
        {   2, "Charging" },
        {   3, "Trickle Charge" },
        {   4, "Reconditioning" },
        {   5, "Ready" },
        {   6, "Discharge Wait" },
        {   7, "Discharging" },
        {   8, "Insert the Battery" },
        {   9, "Charge Wait" },
        {  10, "No Battery" },
        {  11, "Start Battery Process" },
        {  12, "Battery Too Cold" },
        {  13, "Battery Too Hot" },
        {  14, "Battery Over Temp" },
        {  15, "Process Complete" },
        {  16, "Custom Program Has Failed" },
        {  17, "Battery Removed" },
        {  18, "Process Suspended" },
        {  19, "Resting" },
        {  20, "Battery Inserted" },
        {  21, "Resting" },
        {  22, "Setting Up Calibration" },
        {  23, "Station Calibrating" },
        {  25, "Process Resuming" },
        {  26, "Battery Removed" },
        {  27, "Resistance Test" },
        {  28, "Resistance Test (Auto)" },
        {  29, "Station Calibrating" },
        {  30, "Charge Cycle Complete" },
        {  31, "Discharge Cycle Complete" },
        {  32, "Cycle Resumed" },
        {  33, "User Programmed Timeout" },
        {  34, "BatteryShop Mode Wait" },
        {  35, "Program Complete" },
        {  36, "Program Complete" },
        { 112, "Cell Mismatch" },
        { 113, "Plateau Timeout" },
        { 115, "Target Capacity Not Met" },
        { 116, "Program Fail" },
        { 118, "Charge Current Reduced" },
        { 120, "Over Voltage" },
        { 121, "Battery Reversed" },
        { 122, "Battery Shorted" },
        { 123, "Low Voltage (Timeout 1)" },
        { 124, "Low Voltage (Timeout 2)" },
        { 125, "No Negative Slope on Timeout 1" },
        { 126, "Low Voltage at Negative Slope" },
        { 127, "Low Voltage (Timeout 3)" },
        { 128, "Unable to Clamp Charge Voltage" },
        { 129, "Intermittent Battery" },
        { 130, "Current Rise at Full Charge" },
        { 135, "High Cell Resistance" },
        { 136, "High Cell Resistance" },
        { 142, "Discharge Timeout" },
        { 144, "Charge Timeout" },
        { 146, "Recondition Timeout" },
        { 150, "Thermistor Failure" },
        { 152, "Rapid Heat Rise" },
        { 154, "Charge Complete Temp Rise" },
        { 156, "Hot Battery, Low Voltage" },
        { 158, "Heat Termination" },
        { 159, "Hot Battery on Trickle Charge" },
        { 160, "Bad Fuse or Driver" },
        { 162, "Discharge Current Low" },
        { 164, "Charge Current Low" },
        { 170, "Calibration Fault" },
        { 171, "Smart Adapter Fault" },
        { 172, "Smart Battery Fault" },
        { 175, "Battery Undercharged" },
        { 176, "Battery Overcharged" },
        { 177, "Battery Undercharged" },
        { 178, "Battery Overcharged" },
        { 179, "Unable to Learn Matrix" },
        { 188, "Service Interrupted" },
        { 192, "Cell Mismatch Corrected" },
        { 195, "Capacity Improved to Target" },
        { 200, "Power On" },
        { 201, "Adapter Inserted" },
        { 202, "Adapter Removed" },
        { 203, "Password Entered" },
        { 204, "Invalid Password Entered" },
        { 205, "Security Enabled" },
        { 206, "Adapter Setup Updated" },
        { 207, "System Temp High: Cooling" },
        { 208, "Adapter Not Set Up" },
        { 209, "Adapter Data Invalid" },
        { 210, "Bad Adapter" },
        { 211, "Null C-Code in Adapter" },
        { 214, "C-Code Not Usable" },
        { 250, "Normal Processing" },
    };

    /// <summary>
    /// Returns the human-readable description for <paramref name="eventCode"/>.
    /// Falls back to <c>"Event {code}"</c> for unknown codes.
    /// </summary>
    public static string Describe(int eventCode) =>
        _descriptions.TryGetValue(eventCode, out var desc) ? desc : $"Event {eventCode}";

    /// <summary>
    /// Returns an annotated payload string for the given <paramref name="record"/>,
    /// appending the correct unit based on its event code.  Returns an empty string
    /// when the event carries no meaningful additional payload.
    /// </summary>
    public static string FormatPayload(CadexRecord record) =>
        record.EventCode switch
        {
            27 or 28 or 135 or 136 when record.ResistanceMOhm.HasValue
                => $"{record.ResistanceMOhm} mΩ",

            // Timeout codes: show elapsed time from health field when available
            33 or 142 or 144 or 146 when record.HealthCurrent.HasValue
                => $"{record.HealthCurrent} min",

            _ => string.Empty,
        };

    /// <summary>
    /// Derives a human-readable failure reason to display for a terminal
    /// failure or for the contextual event that explains a code 16 failure.
    /// Uses <paramref name="lastEventCode"/> and its associated record to
    /// produce a context-specific message such as "Ohm Test Failed (114 mΩ)".
    /// </summary>
    /// <param name="lastEventCode">The terminal or contextual event code.</param>
    /// <param name="lastRecord">The <see cref="CadexRecord"/> for that event.</param>
    /// <returns>A descriptive failure string, or <c>null</c> when no specific
    /// reason can be determined.</returns>
    public static string? DetermineFailureReason(int lastEventCode, CadexRecord? lastRecord) =>
        lastEventCode switch
        {
            // OhmTest events – include measured resistance
            27 or 28 when lastRecord?.ResistanceMOhm.HasValue == true
                => $"Ohm Test Failed ({lastRecord.ResistanceMOhm} mΩ)",
            27 or 28
                => "Ohm Test Failed",

            // High cell resistance
            135 or 136 when lastRecord?.ResistanceMOhm.HasValue == true
                => $"High Cell Resistance ({lastRecord.ResistanceMOhm} mΩ)",

            _ when CadexCodeMap.TryGetDefinition(lastEventCode, out var definition)
                => definition.Message,

            _ => null,
        };

    /// <summary>
    /// Returns <c>true</c> for event codes that indicate a terminal failure of
    /// the battery program and should trigger the failure-reason analysis.
    /// </summary>
    public static bool IsFailureCode(int eventCode) =>
        eventCode is 116 or 16 or 177 or 178 or 179;

    /// <summary>
    /// Returns <c>true</c> when the code is classified in the causal registry
    /// as a forced-failure or test-invalidating condition.
    /// </summary>
    public static bool IsFaultIndicatorCode(int eventCode) =>
        CadexCodeMap.TryGetDefinition(eventCode, out var definition)
        && (definition.ForcesFail || definition.InvalidatesTest);

    /// <summary>
    /// Returns <c>true</c> for event codes that represent a new battery-service
    /// session starting, used to reset any previously stored failure reason.
    /// </summary>
    public static bool IsSessionStartCode(int eventCode) =>
        eventCode is 11 or 20 or 201 or 200;

    /// <summary>
    /// Returns <c>true</c> for <see cref="CadexRecord.ProcessCode"/> values that
    /// represent a major testing phase (Charge, Reconditioning, or Discharge).
    /// Transitioning into a new major phase means the station has moved on to a
    /// fresh measurement cycle, so any sticky fault from a previous phase should
    /// be cleared.
    /// </summary>
    public static bool IsMajorPhase(int processCode) =>
        processCode is 2 or 4 or 7;  // Charging, Reconditioning, Discharging
}
