using CBAD.Diagnostics;
using CBAD.Models;

namespace CBAD.UI;

internal sealed class StationDetailTab : UserControl
{
    private readonly int _station;

    private readonly DiagnosticAnalyzer _diagnosticAnalyzer = new();
    private ProfileManager? _profileManager;

    private readonly TextBox _txtWorkOrder = new() { PlaceholderText = "Work order / ticket #" };
    private readonly TextBox _txtBatterySerial = new() { PlaceholderText = "Battery serial / asset tag" };
    private readonly CheckBox _chkVisualPass = new() { Text = "Passed Visual Inspection", AutoSize = true };
    private readonly TextBox _txtNotes = new() { Multiline = true, Height = 56, ScrollBars = ScrollBars.Vertical, PlaceholderText = "Notes / remarks…" };
    private readonly Button _btnCommitRecord = new() { Text = "📋  Commit Record / Generate Report", AutoSize = true, Height = 30 };
    private GroupBox? _recordMetaGroup;
    private TableLayoutPanel? _recordMetaTable;

    private readonly TextBox _txtDiagExplanation = new()
    {
        Multiline = true,
        ReadOnly = true,
        Height = 120,
        ScrollBars = ScrollBars.Vertical,
        BackColor = System.Drawing.Color.FromArgb(240, 244, 250),
        ForeColor = System.Drawing.Color.FromArgb(30, 46, 78),
        Font = new System.Drawing.Font("Segoe UI", 8.5f),
        Text = "No diagnostic data — run a test with an active profile.",
    };
    private GroupBox? _diagGroup;

    private readonly StationEssentialsPanel _essentials;
    private readonly StationChartPanel _chart;
    private readonly StationStreamPanel _stream;

    private bool _isDark;
    private StationState? _lastState;

    public StationDetailTab(int station, ProfileManager? profileManager = null)
    {
        _station = station;
        _profileManager = profileManager;
        Dock = DockStyle.Fill;

        _essentials = new StationEssentialsPanel(station);
        _chart = new StationChartPanel();
        _stream = new StationStreamPanel(station);
        _essentials.ClearRequested += OnClearRequested;

        var mainCol = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        mainCol.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        mainCol.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainCol.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        mainCol.Controls.Add(_essentials, 0, 0);
        mainCol.Controls.Add(BuildChartStreamTabs(), 0, 1);

        var rightCol = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        rightCol.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        rightCol.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rightCol.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        rightCol.Controls.Add(BuildDiagnosticGroup(), 0, 0);
        rightCol.Controls.Add(BuildRecordMetadataGroup(), 0, 1);

        var outer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
        };
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260f));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        outer.Controls.Add(mainCol, 0, 0);
        outer.Controls.Add(rightCol, 1, 0);

        Controls.Add(outer);
    }

    private GroupBox BuildRecordMetadataGroup()
    {
        var group = new GroupBox
        {
            Text = "Test Record Metadata",
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 20, 8, 8),
            Margin = new Padding(0),
        };

        var table = new TableLayoutPanel
        {
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            Padding = new Padding(2),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        void AddField(string labelText, Control ctrl)
        {
            var lbl = new Label
            {
                Text = labelText,
                AutoSize = true,
                Font = new System.Drawing.Font(SystemFonts.DefaultFont, System.Drawing.FontStyle.Bold),
                Margin = new Padding(0, 4, 0, 2),
            };
            ctrl.Dock = DockStyle.Top;
            ctrl.Margin = new Padding(0, 0, 0, 4);
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(lbl);
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(ctrl);
        }

        AddField("Work Order / Ticket #:", _txtWorkOrder);
        AddField("Battery Serial / Asset Tag:", _txtBatterySerial);

        _chkVisualPass.Margin = new Padding(0, 6, 0, 4);
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(_chkVisualPass);

        AddField("Notes / Remarks:", _txtNotes);

        _btnCommitRecord.Dock = DockStyle.Top;
        _btnCommitRecord.Margin = new Padding(0, 8, 0, 4);
        _btnCommitRecord.FlatStyle = FlatStyle.Flat;
        _btnCommitRecord.BackColor = System.Drawing.Color.FromArgb(30, 100, 180);
        _btnCommitRecord.ForeColor = System.Drawing.Color.White;
        _btnCommitRecord.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(20, 70, 140);
        _btnCommitRecord.Click += OnCommitRecord;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(_btnCommitRecord);

        group.Controls.Add(table);

        _recordMetaGroup = group;
        _recordMetaTable = table;

        group.Paint += (_, e) => ThemedGroupBoxPainter.Paint(group, _isDark, e);

        return group;
    }

    private GroupBox BuildDiagnosticGroup()
    {
        var group = new GroupBox
        {
            Text = "🔬 Diagnostic Explanation",
            Dock = DockStyle.Top,
            Padding = new Padding(8, 20, 8, 8),
            Margin = new Padding(0, 0, 0, 4),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };

        _txtDiagExplanation.Dock = DockStyle.Top;
        _txtDiagExplanation.Margin = new Padding(0);

        group.Controls.Add(_txtDiagExplanation);

        _diagGroup = group;

        group.Paint += (_, e) => ThemedGroupBoxPainter.Paint(group, _isDark, e);

        return group;
    }

    private void OnCommitRecord(object? sender, EventArgs e)
    {
        if (_lastState is null || _lastState.Latest is null)
        {
            MessageBox.Show("No test data available to generate a report.", "Report Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var rec = _lastState.Latest;

        string chartBase64 = "";
        try
        {
            byte[] bytes = _chart.GetChartImageBytes(800, 400);
            chartBase64 = Convert.ToBase64String(bytes);
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Failed to generate chart image for report: {ex.Message}");
        }

        var processCodeStr = rec.ProcessCode?.ToString() ?? "";
        string statusText = !string.IsNullOrEmpty(_lastState.FailureReason)
            ? $"FAIL: {_lastState.FailureReason}"
            : CBAD.Parsing.CadexStatusCodes.Describe(processCodeStr);

        var data = new CBAD.Reporting.ReportData
        {
            Station = _station.ToString(),
            WorkOrder = _txtWorkOrder.Text,
            BatterySerial = _txtBatterySerial.Text,
            PassedVisualInspection = _chkVisualPass.Checked,
            Notes = _txtNotes.Text,
            ChartImageBase64 = chartBase64,

            Date = rec.ReceivedAt.ToString("yyyy-MM-dd HH:mm:ss UTC"),
            FinalStatus = statusText,
            ProcessCode = string.IsNullOrEmpty(processCodeStr) ? "—" : $"{processCodeStr} ({CBAD.Parsing.CadexStatusCodes.Describe(processCodeStr)})",
            TargetCapacity = _lastState.TargetCapacity ?? "—",
            FinalVoltage = rec.VoltageMv.HasValue ? $"{rec.VoltageMv} mV" : "—",
            FinalCurrent = rec.CurrentMa.HasValue ? $"{rec.CurrentMa} mA" : "—",
            FinalHealth = rec.HealthCurrent.HasValue ? $"{rec.HealthCurrent}%" : "—",
            Resistance = rec.ResistanceMOhm.HasValue ? $"{rec.ResistanceMOhm} mΩ" : "—"
        };

        var html = CBAD.Reporting.ReportGenerator.GenerateHtml(data);

        using var viewer = new ReportViewerForm(html, $"Report - Station {_station}");
        viewer.ShowDialog(this);
    }

    private TabControl BuildChartStreamTabs()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };

        var chartTab = new TabPage("Chart");
        chartTab.Controls.Add(_chart);
        tabs.TabPages.Add(chartTab);

        var streamTab = new TabPage("Raw Stream");
        streamTab.Controls.Add(_stream);
        tabs.TabPages.Add(streamTab);

        return tabs;
    }

    public void UpdateStation(StationState state)
    {
        _lastState = state;
        _essentials.UpdateStation(state, _isDark);
        _chart.UpdateChart(state);
        _stream.UpdateStream(state);
        RunDiagnosticAnalysis(state);
    }

    public void ClearStation()
    {
        if (_lastState is not null)
        {
            _lastState.History.Clear();
            _lastState.RawLines.Clear();
            _lastState.Latest = null;
            _lastState.TargetCapacity = null;
            _lastState.FailureReason = null;
            _lastState.LastActiveEventCode = null;
            _lastState.LastActiveRecord = null;
        }
        _lastState = null;

        _chart.ClearChart();
        _stream.ClearStream();
        _essentials.Reset(_isDark);

        _txtDiagExplanation.Text = "No diagnostic data — run a test with an active profile.";
        _txtDiagExplanation.ForeColor = AppTheme.MutedFg(_isDark);
    }

    private void OnClearRequested(object? sender, EventArgs e)
    {
        var confirm = MessageBox.Show(
            $"Clear all captured data for Station {_station}?{Environment.NewLine}This cannot be undone.",
            "Clear Station Data",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes)
            return;

        ClearStation();
    }

    private void RunDiagnosticAnalysis(StationState state)
    {
        var profile = _profileManager?.ActiveProfile;
        if (profile is null)
        {
            _txtDiagExplanation.Text = "No active battery profile — select a profile in Settings › Battery Profiles to enable diagnostics.";
            _txtDiagExplanation.ForeColor = AppTheme.MutedFg(_isDark);
            return;
        }

        if (state.Latest is null)
        {
            _txtDiagExplanation.Text = "No diagnostic data — run a test with an active profile.";
            _txtDiagExplanation.ForeColor = AppTheme.MutedFg(_isDark);
            return;
        }

        try
        {
            var report = _diagnosticAnalyzer.Analyze(state, profile);

            _txtDiagExplanation.Text = report.TechnicianExplanation;
            _txtDiagExplanation.ForeColor = report.MeetsAcceptanceCriteria
                ? System.Drawing.Color.FromArgb(0, 130, 60)
                : (report.TechnicianExplanation.StartsWith("CAUTION", StringComparison.OrdinalIgnoreCase)
                    ? System.Drawing.Color.FromArgb(180, 100, 0)
                    : System.Drawing.Color.FromArgb(180, 30, 30));
        }
        catch (Exception ex)
        {
            _txtDiagExplanation.Text = $"Diagnostic analysis error: {ex.Message}";
            _txtDiagExplanation.ForeColor = AppTheme.MutedFg(_isDark);
            AppLog.Warn($"DiagnosticAnalyzer error on station {_station}: {ex.Message}");
        }
    }

    public void ApplyTheme(bool isDark)
    {
        _isDark = isDark;

        BackColor = AppTheme.PanelBg(isDark);
        _essentials.ApplyTheme(isDark);
        _chart.ApplyTheme(isDark);
        _stream.ApplyTheme(isDark);

        if (_recordMetaGroup != null)
        {
            _recordMetaGroup.ForeColor = AppTheme.LabelFg(isDark);
            _recordMetaGroup.BackColor = AppTheme.PanelBg(isDark);
            _recordMetaGroup.Invalidate();
        }

        if (_recordMetaTable != null)
        {
            _recordMetaTable.BackColor = AppTheme.PanelBg(isDark);
            foreach (Control c in _recordMetaTable.Controls)
            {
                if (c is Label lbl)
                {
                    lbl.ForeColor = AppTheme.LabelFg(isDark);
                    lbl.BackColor = AppTheme.PanelBg(isDark);
                }
                else if (c is TextBox txt)
                {
                    txt.BackColor = AppTheme.InputBg(isDark);
                    txt.ForeColor = AppTheme.InputFg(isDark);
                }
                else if (c is CheckBox chk)
                {
                    chk.ForeColor = AppTheme.LabelFg(isDark);
                    chk.BackColor = AppTheme.PanelBg(isDark);
                }
                else if (c is Button btn && btn == _btnCommitRecord)
                {
                    btn.BackColor = isDark
                        ? System.Drawing.Color.FromArgb(25, 75, 145)
                        : System.Drawing.Color.FromArgb(30, 100, 180);
                    btn.ForeColor = System.Drawing.Color.White;
                    btn.FlatAppearance.BorderColor = isDark
                        ? System.Drawing.Color.FromArgb(15, 55, 110)
                        : System.Drawing.Color.FromArgb(20, 70, 140);
                }
            }
        }

        if (_diagGroup != null)
        {
            _diagGroup.ForeColor = AppTheme.LabelFg(isDark);
            _diagGroup.BackColor = AppTheme.PanelBg(isDark);
            _diagGroup.Invalidate();
        }

        _txtDiagExplanation.BackColor = isDark
            ? System.Drawing.Color.FromArgb(22, 32, 52)
            : System.Drawing.Color.FromArgb(240, 244, 250);
    }
}
