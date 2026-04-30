using System.Drawing.Imaging;
using CBAD.Data;
using CBAD.Models;
using CBAD.Reporting;

namespace CBAD.UI;

internal sealed class RecordCommitHandler
{
    public static void HandleCommit(
        int station,
        StationState? lastState,
        string workOrder,
        string batterySerial,
        bool visualPass,
        string notes,
        ScottPlot.WinForms.FormsPlot formsPlot)
    {
        var rec = lastState?.Latest;
        if (rec is null)
        {
            MessageBox.Show(
                "No live data available to commit. Please wait for the station to report data.",
                "Commit Failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        string dbPath = DatabaseManager.GetDefaultDbPath();
        if (!File.Exists(dbPath))
        {
            var result = MessageBox.Show(
                "The database file does not exist. Would you like to generate it now?",
                "Database Missing",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                try
                {
                    DatabaseManager.GenerateDatabase(dbPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to generate database:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            else
            {
                return;
            }
        }

        string? base64Chart = null;
        try
        {
            if (formsPlot.Plot.GetPlottables().Any())
            {
                var imgBytes = formsPlot.Plot.GetImageBytes(formsPlot.Width, formsPlot.Height, ScottPlot.ImageFormat.Png);
                base64Chart = Convert.ToBase64String(imgBytes);
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Failed to capture chart image for commit: {ex.Message}");
        }

        var reportData = new ReportData
        {
            Station = station.ToString(),
            WorkOrder = string.IsNullOrWhiteSpace(workOrder) ? "N/A" : workOrder,
            BatterySerial = string.IsNullOrWhiteSpace(batterySerial) ? rec.BatteryId : batterySerial,
            PassedVisualInspection = visualPass,
            Date = rec.Timestamp.ToString("o"),
            FinalStatus = lastState?.FailureReason ?? CadexStatusCodes.Describe(rec.ProcessCode?.ToString()),
            ProcessCode = rec.ProcessCode?.ToString(),
            TargetCapacity = lastState?.TargetCapacity,
            FinalVoltage = rec.VoltageMv?.ToString(),
            FinalCurrent = rec.CurrentMa?.ToString(),
            FinalHealth = rec.HealthCurrent?.ToString(),
            Resistance = rec.ResistanceMOhm?.ToString(),
            Notes = notes,
            ChartImageBase64 = base64Chart
        };

        try
        {
            DatabaseManager.SaveRecord(dbPath, reportData);
            MessageBox.Show(
                $"Record successfully committed to the database.",
                "Commit Success",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to commit record to database:\n{ex.Message}",
                "Commit Failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}