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

    // Theming support
    private Panel? _essentialsPanel;
    private GroupBox? _advGroup;
    private TableLayoutPanel? _advTable;
    private bool _isDark;

    // Chart
    private readonly FormsPlot _formsPlot = new() { Dock = DockStyle.Fill };

    // Chart interactive controls
    private ScottPlot.IYAxis? _currentAxis;
    private ScottPlot.Plottables.Crosshair? _crosshair;
    private readonly Label _lblChartTooltip = new()
    {
        Dock = DockStyle.Bottom,
        Height = 24,
        Text = string.Empty,
        TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
        Font = new System.Drawing.Font("Consolas", 8.5f),
        ForeColor = System.Drawing.Color.DimGray,
        Padding = new Padding(4, 0, 0, 0),
        Visible = false,
    };

    // Stream
    private const int NormalTelemetryEventCode = 250;
    private readonly RichTextBox _txtStream;
    private readonly Button _btnExport       = new() { Text = "Export Raw Data…", Dock = DockStyle.Bottom, Height = 30 };
    private readonly Button _btnClearStation = new() { Text = "🗑  Clear Data", AutoSize = true, Margin = new Padding(16, 0, 0, 0) };

    // Stream controls
    private readonly CheckBox _chkAutoScroll  = new() { Text = "Auto-scroll", Checked = true, AutoSize = true, Margin = new Padding(4, 4, 4, 3) };
    private readonly Button   _btnPauseStream = new() { Text = "⏸  Pause", AutoSize = true, Margin = new Padding(4, 2, 4, 2) };
    private readonly TextBox  _txtFilter      = new() { Width = 160, PlaceholderText = "Filter lines…", Margin = new Padding(4, 2, 4, 2) };
    private bool _streamPaused;

    private bool _isDark;
    private StationState? _lastState;

    public StationDetailTab(int station, ProfileManager? profileManager = null)
    {
        _station = station;
        _profileManager = profileManager;
        Dock = DockStyle.Fill;

        _txtStream = new RichTextBox
        {
            Multiline = true,
            ReadOnly = true,
            WordWrap = false,
            ScrollBars = RichTextBoxScrollBars.Both,
            Dock = DockStyle.Fill,
            Font = new System.Drawing.Font("Consolas", 9),
            BackColor = System.Drawing.Color.FromArgb(18, 24, 36),
            ForeColor = System.Drawing.Color.FromArgb(130, 210, 130),
        };

        BuildChart();
        WireExport();
        WireClearStation();
        WireChartMouse();

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

    // ── Essentials panel ───────────────────────────────────────────────
    private Panel BuildEssentialsPanel()
    {
        var panel = new Panel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = System.Drawing.Color.FromArgb(245, 247, 250),
            Padding = new Padding(14, 10, 14, 10),
        };
        _essentialsPanel = panel;

        // Station heading
        _lblStationBig.Font = new System.Drawing.Font("Segoe UI", 14f, System.Drawing.FontStyle.Bold);
        _lblStationBig.ForeColor = System.Drawing.Color.FromArgb(30, 46, 78);
        _lblStationBig.Text = $"Station {_station}";
        _lblStationBig.Margin = new Padding(0, 0, 0, 4);

        // Battery ID
        _lblBatteryId.Font = new System.Drawing.Font(Font.FontFamily, 9.5f);
        _lblBatteryId.ForeColor = System.Drawing.Color.Gray;
        _lblBatteryId.Text = "Battery: —";
        _lblBatteryId.Margin = new Padding(0, 0, 0, 8);

        // Status row
        _lblStatusDot.BackColor = System.Drawing.Color.LightGray;
        _lblStatus.Font = new System.Drawing.Font(Font.FontFamily, 9.5f, System.Drawing.FontStyle.Bold);
        _lblStatus.Text = "No data";
        _lblStatus.ForeColor = System.Drawing.Color.Gray;

        var statusRow = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 10),
        };
        statusRow.Controls.Add(_lblStatusDot);
        statusRow.Controls.Add(_lblStatus);
        statusRow.Controls.Add(_btnClearStation);

        // Key metrics row — icon labels have fixed colors; text labels respond to theme
        StyleIconLabel(_lblVoltageIcon, "⚡", System.Drawing.Color.Gold);
        StyleIconLabel(_lblCurrentIcon, "🔌", System.Drawing.Color.DeepSkyBlue);
        StyleIconLabel(_lblHealthIcon,  "🔋", System.Drawing.Color.LimeGreen);
        StyleIconLabel(_lblTempIcon,    "🌡️", System.Drawing.Color.Tomato);
        StyleMetricLabel(_lblVoltage,  "Voltage",  "—");
        StyleMetricLabel(_lblCurrent,  "Current",  "—");
        StyleMetricLabel(_lblHealth,   "Health",   "—");
        StyleMetricLabel(_lblTemp,     "Temp",     "—");

        var metricsFlow = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 8),
        };
        metricsFlow.Controls.Add(MakeMetricCell(_lblVoltageIcon, _lblVoltage));
        metricsFlow.Controls.Add(MakeMetricCell(_lblCurrentIcon, _lblCurrent));
        metricsFlow.Controls.Add(MakeMetricCell(_lblHealthIcon,  _lblHealth));
        metricsFlow.Controls.Add(MakeMetricCell(_lblTempIcon,    _lblTemp));

        // Last update and runtime
        _lblLastUpdate.ForeColor = System.Drawing.Color.Gray;
        _lblLastUpdate.Font = new System.Drawing.Font(Font.FontFamily, 8.5f);
        _lblLastUpdate.Text = "No data yet — connect and start capture";

        _lblRuntime.ForeColor = System.Drawing.Color.Gray;
        _lblRuntime.Font = new System.Drawing.Font(Font.FontFamily, 8.5f);
        _lblRuntime.Text = string.Empty;

        // Additional details GroupBox
        var advGroup = BuildAdvancedGroup();

        // Use a single-column AutoSize TableLayoutPanel so every row sizes to its
        // content height, which in turn lets the parent panel auto-size correctly.
        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            BackColor = System.Drawing.Color.Transparent,
            Padding = new Padding(0),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // heading
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // battery id
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // status row
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // metrics
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // runtime
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // last update
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 180f));  // adv group

        layout.Controls.Add(_lblStationBig,  0, 0);
        layout.Controls.Add(_lblBatteryId,   0, 1);
        layout.Controls.Add(statusRow,       0, 2);
        layout.Controls.Add(metricsFlow,     0, 3);
        layout.Controls.Add(_lblRuntime,     0, 4);
        layout.Controls.Add(_lblLastUpdate,  0, 5);
        layout.Controls.Add(advGroup,        0, 6);

        panel.Controls.Add(layout);
        return panel;
    }

    private static void StyleMetricLabel(Label lbl, string caption, string value)
    {
        lbl.Text = $"{caption}: {value}";
        lbl.Font = new System.Drawing.Font(SystemFonts.DefaultFont.FontFamily, 10.5f, System.Drawing.FontStyle.Bold);
        lbl.AutoSize = true;
        lbl.Margin = new Padding(0, 0, 24, 0);
    }

    private static void StyleIconLabel(Label lbl, string icon, System.Drawing.Color color)
    {
        lbl.Text = icon;
        lbl.Font = new System.Drawing.Font("Segoe UI Emoji", 10.5f, System.Drawing.FontStyle.Bold);
        lbl.ForeColor = color;
        lbl.AutoSize = true;
        lbl.Margin = new Padding(0, 0, 4, 0);
        lbl.BackColor = System.Drawing.Color.Transparent;
    }

    private static FlowLayoutPanel MakeMetricCell(Label icon, Label text)
    {
        var fp = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = System.Drawing.Color.Transparent,
        };
        fp.Controls.Add(icon);
        fp.Controls.Add(text);
        return fp;
    }

    private static string FormatRuntime(TimeSpan elapsed)
    {
        if (elapsed.TotalHours >= 1)
            return $"{(int)elapsed.TotalHours}h {elapsed.Minutes:D2}m {elapsed.Seconds:D2}s";
        return $"{elapsed.Minutes}m {elapsed.Seconds:D2}s";
    }

    private GroupBox BuildAdvancedGroup()
    {
        var group = new GroupBox
        {
            Text = "Additional Details",
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 24, 8, 8),
            Margin = new Padding(0, 4, 0, 4),
        };

        const int rowCount = 5;
        var table = new TableLayoutPanel
        {
            ColumnCount = 4,
            RowCount = rowCount,
            Dock = DockStyle.Fill,
            Padding = new Padding(2),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (int i = 0; i < rowCount; i++)
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        AddPair(table, "Station:",        _lblStation,       "Last Update:",     _lblLastUpdateAdv);
        AddPair(table, "Date:",           _lblDate,          "Time:",            _lblTime);
        AddPair(table, "Event Code:",     _lblEventCode,     "Process Code:",    _lblBatteryType);
        AddPair(table, "Health Current:", _lblHealthCurrent, "Health Prev:",     _lblHealthPrev);
        AddPair(table, "Target Cap:",     _lblTargetCap,     "Resistance:",      _lblResistance);

        group.Controls.Add(table);

        _advGroup = group;
        _advTable = table;

        // Custom border painting for dark mode — draws a flat themed border
        // and title text over the system-rendered GroupBox in dark mode.
        group.Paint += (s, e) => PaintThemedGroupBox(s, e, _isDark);

        return group;
    }

    private static void PaintThemedGroupBox(object? sender, PaintEventArgs e, bool isDark)
    {
        if (!isDark || sender is not GroupBox gb) return;
        var g  = e.Graphics;

        // Measure title text height to find where the top border line sits.
        var textSize  = g.MeasureString(gb.Text, gb.Font);
        int borderTop = (int)(textSize.Height / 2);

        // Cover the system-rendered border/background with the theme color.
        using var bgBrush = new System.Drawing.SolidBrush(gb.BackColor);
        g.FillRectangle(bgBrush, 0, borderTop, gb.Width, gb.Height - borderTop);
        g.FillRectangle(bgBrush, 0, 0, gb.Width, borderTop);

        // Draw flat themed border.
        using var pen = new System.Drawing.Pen(AppTheme.BorderColor(true));
        g.DrawRectangle(pen, new System.Drawing.Rectangle(0, borderTop, gb.Width - 1, gb.Height - borderTop - 1));

        // Redraw title text with theme foreground color.
        const float textX = 9f;
        using var textBgBrush = new System.Drawing.SolidBrush(gb.BackColor);
        g.FillRectangle(textBgBrush, textX - 2, 0, textSize.Width + 4, textSize.Height);
        using var textBrush = new System.Drawing.SolidBrush(gb.ForeColor);
        g.DrawString(gb.Text, gb.Font, textBrush, textX, 0);
    }

    // ── Test Record Metadata group ───────────────────────────────────────
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

        group.Paint += (s, e) => PaintThemedGroupBox(s, e, _isDark);

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

        group.Paint += (s, e) => PaintThemedGroupBox(s, e, _isDark);

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

        // Find the nearest plotted data point to the mouse X position using index.
        CadexRecord? nearest = null;
        int idx = 0;
        double minDist = double.MaxValue;
        for (int i = 0; i < chartable.Count; i++)
        {
            double dist = Math.Abs(i - mouseX);
            if (dist < minDist) { minDist = dist; nearest = chartable[i]; idx = i; }
        }

        if (nearest is null) return;

        _crosshair.X         = (double)idx;
        _crosshair.Y         = (double)nearest.VoltageMv!.Value;
        _crosshair.IsVisible = true;

        double absCurrentMa = nearest.CurrentMa.HasValue ? Math.Abs((double)nearest.CurrentMa.Value) : 0;
        _lblChartTooltip.Text =
            $"⏱ {idx} min  |  " +
            $"V: {(nearest.VoltageMv.HasValue  ? $"{nearest.VoltageMv} mV"       : "—")}  |  " +
            $"I: {(nearest.CurrentMa.HasValue  ? $"{absCurrentMa} mA"            : "—")}  |  " +
            $"Health: {(nearest.HealthCurrent.HasValue ? $"{nearest.HealthCurrent}%" : "—")}";
        _lblChartTooltip.Visible = true;

        if (_formsPlot.Width > 0 && _formsPlot.Height > 0)
        {
            _formsPlot.Refresh();
        }
    }

    private FlowLayoutPanel BuildStreamToolbar()
    {
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(4, 2, 4, 2),
            BackColor = System.Drawing.SystemColors.ControlLight,
        };

        var clearFilter = new Button { Text = "✕", Width = 26, Height = 23, Margin = new Padding(0, 2, 6, 2) };
        clearFilter.Click += (_, __) => _txtFilter.Clear();

        _txtFilter.TextChanged += (_, __) => RefreshStream();
        _btnPauseStream.Click += (_, __) =>
        {
            _streamPaused = !_streamPaused;
            _btnPauseStream.Text = _streamPaused ? "▶  Resume" : "⏸  Pause";
            if (!_streamPaused)
                RefreshStream();
        };

        toolbar.Controls.Add(new Label { Text = "Filter:", AutoSize = true, Margin = new Padding(4, 6, 4, 3) });
        toolbar.Controls.Add(_txtFilter);
        toolbar.Controls.Add(clearFilter);
        toolbar.Controls.Add(_chkAutoScroll);
        toolbar.Controls.Add(_btnPauseStream);

        return toolbar;
    }

    private void RefreshStream()
    {
        if (_lastState is null) return;

        var lines = _lastState.RawLines.AsEnumerable();
        var filter = _txtFilter.Text.Trim();
        if (!string.IsNullOrEmpty(filter))
            lines = lines.Where(l => l.Contains(filter, StringComparison.OrdinalIgnoreCase));

        var filteredLines = lines.TakeLast(200);

        _txtStream.SuspendLayout();
        _txtStream.Clear();
        foreach (var line in filteredLines)
        {
            _txtStream.SelectionColor = GetLineColor(line);
            _txtStream.AppendText(line + Environment.NewLine);
        }
        _txtStream.ResumeLayout();

        if (_chkAutoScroll.Checked)
        {
            _txtStream.SelectionStart = _txtStream.TextLength;
            _txtStream.ScrollToCaret();
        }
    }

    private static System.Drawing.Color GetLineColor(string line)
    {
        if (ContainsParseFailureMarker(line))
        {
            return System.Drawing.Color.FromArgb(255, 100, 100);
        }

        var fields = line.Split(',');
        if (fields.Length <= 2 || !int.TryParse(fields[2].Trim(), out var eventCode))
            return System.Drawing.Color.FromArgb(255, 100, 100);

        if (CadexEventParser.IsFailureCode(eventCode))
            return System.Drawing.Color.FromArgb(255, 80, 80);
        if (CadexEventParser.IsSessionStartCode(eventCode))
            return System.Drawing.Color.FromArgb(100, 220, 255);
        if (CadexEventParser.IsFaultIndicatorCode(eventCode))
            return System.Drawing.Color.FromArgb(255, 200, 80);
        if (eventCode == NormalTelemetryEventCode)
            return System.Drawing.Color.FromArgb(130, 210, 130);

        return System.Drawing.Color.FromArgb(200, 200, 200);
    }

    private static bool ContainsParseFailureMarker(string line)
    {
        // Parse-failure markers may include separators (PARSE_FAIL, PARSE-FAIL, Parse failed:)
        // so normalize common delimiters before checking.
        var normalizedLine = line
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Replace(':', ' ');

        return normalizedLine.Contains("PARSE FAIL", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddPair(TableLayoutPanel t, string lbl1, Control val1, string lbl2, Control val2)
    {
        t.Controls.Add(MakeLabel(lbl1));
        val1.Margin = new Padding(3, 5, 3, 3);
        t.Controls.Add(val1);
        t.Controls.Add(MakeLabel(lbl2));
        val2.Margin = new Padding(3, 5, 3, 3);
        t.Controls.Add(val2);
    }

    private static Label MakeLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new System.Drawing.Font(SystemFonts.DefaultFont, System.Drawing.FontStyle.Bold),
        Margin = new Padding(3, 5, 8, 3),
        TextAlign = System.Drawing.ContentAlignment.MiddleRight,
    };

    public void UpdateStation(StationState state)
    {
        _lastState = state;
        var rec = state.Latest;
        if (rec is not null)
        {
            var processCodeStr = rec.ProcessCode?.ToString() ?? "";
            var statusColor    = CadexStatusCodes.StatusColor(processCodeStr);

            // Essentials section
            _lblBatteryId.Text   = string.IsNullOrWhiteSpace(rec.BatteryId) ? "Battery: (no label)" : $"Battery: {rec.BatteryId}";

            // If a failure reason has been determined, show it prominently in red.
            if (!string.IsNullOrEmpty(state.FailureReason))
            {
                _lblStatusDot.BackColor = System.Drawing.Color.Red;
                _lblStatus.Text         = $"FAIL: {state.FailureReason}";
                _lblStatus.ForeColor    = System.Drawing.Color.Red;
                _lblStatus.Font         = new System.Drawing.Font(
                    _lblStatus.Font.FontFamily,
                    _lblStatus.Font.Size,
                    System.Drawing.FontStyle.Bold);
            }
            else
            {
                _lblStatusDot.BackColor = statusColor;
                _lblStatus.Text         = CadexStatusCodes.Describe(processCodeStr);
                _lblStatus.ForeColor    = statusColor == System.Drawing.Color.LightGray
                    ? AppTheme.MutedFg(_isDark)
                    : AppTheme.LabelFg(_isDark);
                _lblStatus.Font         = new System.Drawing.Font(
                    _lblStatus.Font.FontFamily,
                    _lblStatus.Font.Size,
                    System.Drawing.FontStyle.Regular);
            }

            _lblVoltage.Text = rec.VoltageMv.HasValue    ? $"Voltage: {rec.VoltageMv} mV"    : "Voltage: —";
            _lblCurrent.Text = rec.CurrentMa.HasValue    ? $"Current: {rec.CurrentMa} mA"    : "Current: —";
            _lblHealth.Text  = rec.HealthCurrent.HasValue  ? $"Health: {rec.HealthCurrent}%"  : "Health: —";
            _lblTemp.Text    = rec.TemperatureC.HasValue ? $"Temp: {rec.TemperatureC} °C"    : "Temp: —";

            _lblRuntime.Text = state.SessionStart.HasValue
                ? $"Runtime: {FormatRuntime(DateTimeOffset.UtcNow - state.SessionStart.Value)}"
                : string.Empty;

            _lblLastUpdate.Text = $"Last update: {rec.ReceivedAt:HH:mm:ss} UTC";

            // Advanced section
            _lblStation.Text        = rec.Station.ToString();
            _lblLastUpdateAdv.Text  = rec.ReceivedAt.ToString("HH:mm:ss UTC");
            _lblDate.Text           = rec.Timestamp.ToString("MM/dd/yyyy");
            _lblTime.Text           = rec.Timestamp.ToString("HH:mm:ss");

            var eventDesc    = CadexEventParser.Describe(rec.EventCode);
            var eventPayload = CadexEventParser.FormatPayload(rec);
            _lblEventCode.Text = string.IsNullOrEmpty(eventPayload)
                ? $"{rec.EventCode} ({eventDesc})"
                : $"{rec.EventCode} ({eventDesc}: {eventPayload})";

            _lblBatteryType.Text    = processCodeStr == "" ? "—" : $"{processCodeStr} ({CadexStatusCodes.Describe(processCodeStr)})";
            _lblHealthCurrent.Text  = rec.HealthCurrent.HasValue  ? $"{rec.HealthCurrent}%"  : "—";
            _lblHealthPrev.Text     = rec.HealthPrevious.HasValue ? $"{rec.HealthPrevious}%" : "—";
            _lblResistance.Text     = rec.ResistanceMOhm.HasValue ? $"{rec.ResistanceMOhm} mΩ" : "—";
        }

        // Target / measured capacity: use the state-level value, which is
        // updated by StationStateManager from every record that carries a
        // CapacityPayload (event 250 field 8) or TargetCapacityPct (events 201/20).
        _lblTargetCap.Text = !string.IsNullOrEmpty(state.TargetCapacity) ? state.TargetCapacity : "—";

        // Chart: rebuild from bounded history so the chart remains correct when
        // the ring buffer removes oldest entries.
        var chartable = state.History
            .Where(r => r.EventCode == 250 && r.VoltageMv.HasValue)
            .ToList();

        var plot = _formsPlot.Plot;
        plot.Clear();

        if (chartable.Count > 0)
        {
            // Phase shading: draw colored VSpan/HSpan backgrounds for each ProcessCode
            // block before adding scatter plots so the shading renders behind the
            // data lines.
            var phaseRecords = chartable
                .Select((r, i) => (record: r, index: i))
                .Where(x => x.record.ProcessCode.HasValue)
                .ToList();

            if (phaseRecords.Count > 0)
            {
                int currentCode  = phaseRecords[0].record.ProcessCode!.Value;
                int spanStartIdx = phaseRecords[0].index;

                for (int i = 1; i <= phaseRecords.Count; i++)
                {
                    bool isLast   = i == phaseRecords.Count;
                    int? nextCode = isLast ? null : phaseRecords[i].record.ProcessCode!.Value;

                    if (nextCode == null || nextCode != currentCode)
                    {
                        int spanEndIdx = isLast
                            ? phaseRecords[i - 1].index
                            : phaseRecords[i].index;

                        ScottPlot.Color? fillColor = currentCode switch
                        {
                            2  => ScottPlot.Colors.LightGreen.WithAlpha(0.2f),  // Charge
                            7  => ScottPlot.Colors.LightCoral.WithAlpha(0.2f),  // Discharge
                            19 => ScottPlot.Colors.LightGray.WithAlpha(0.2f),   // Resting/Wait
                            _  => null,
                        };

                        if (fillColor.HasValue)
                        {
                            var hspan = plot.Add.HorizontalSpan(
                                (double)spanStartIdx,
                                (double)spanEndIdx);
                            hspan.FillColor = fillColor.Value;
                            hspan.LineWidth = 0;
                        }

                        if (nextCode != null)
                        {
                            currentCode  = nextCode.Value;
                            spanStartIdx = phaseRecords[i].index;
                        }
                    }
                }
            }

            var voltageXs = chartable.Select((_, i) => (double)i).ToArray();
            var voltageYs = chartable.Select(r => (double)r.VoltageMv!.Value).ToArray();
            var voltageScatter = plot.Add.Scatter(voltageXs, voltageYs);
            voltageScatter.LegendText = "Voltage (mV)";
            voltageScatter.Color      = ScottPlot.Colors.DeepSkyBlue;
            voltageScatter.LineWidth  = 2;
            voltageScatter.MarkerSize = 0;
            
            // Explicitly map voltage data to the left axis
            voltageScatter.Axes.YAxis = plot.Axes.Left;

            var healthIndexedPoints = chartable
                .Select((r, i) => (record: r, index: i))
                .Where(x => x.record.HealthCurrent.HasValue)
                .ToList();
            if (healthIndexedPoints.Count > 0)
            {
                var healthXs = healthIndexedPoints.Select(x => (double)x.index).ToArray();
                var healthYs = healthIndexedPoints.Select(x => (double)x.record.HealthCurrent!.Value).ToArray();
                var healthScatter = plot.Add.Scatter(healthXs, healthYs);
                healthScatter.LegendText = "Health (%)";
                healthScatter.Color      = ScottPlot.Colors.OrangeRed;
                healthScatter.LineWidth  = 2;
                healthScatter.MarkerSize = 0;
                
                // Explicitly map health data to the default right axis
                healthScatter.Axes.YAxis = plot.Axes.Right;
            }

            // Current (mA) line on the dedicated third axis.
            double[]? currentYsForAxis = null;
            var currentIndexedPoints = chartable
                .Select((r, i) => (record: r, index: i))
                .Where(x => x.record.CurrentMa.HasValue)
                .ToList();
            if (currentIndexedPoints.Count > 0 && _currentAxis is not null)
            {
                var currentXs = currentIndexedPoints.Select(x => (double)x.index).ToArray();
                currentYsForAxis = currentIndexedPoints.Select(x => Math.Abs((double)x.record.CurrentMa!.Value)).ToArray();
                var currentScatter = plot.Add.Scatter(currentXs, currentYsForAxis);
                currentScatter.LegendText = "Current (mA)";
                currentScatter.Color      = ScottPlot.Colors.Orange;
                currentScatter.LineWidth  = 2;
                currentScatter.MarkerSize = 0;
                
                // Explicitly map current data to the secondary right axis
                currentScatter.Axes.YAxis = _currentAxis;
            }

            // We CANNOT use plot.Axes.AutoScale() generically. ScottPlot 5's global auto scale 
            // has a known bug/limitation when calculating scale bounds across multiple disparate Y axes,
            // resulting in Current limits (45,000) overriding Voltage limits (2,000).

            // 1. AutoScale X axis only
            double xMin = voltageXs.Min();
            double xMax = voltageXs.Max();
            double xRange = xMax - xMin;
            double xPad = xRange > 0 ? xRange * 0.02 : 0.0001;
            plot.Axes.SetLimitsX(xMin - xPad, xMax + xPad);

            // 2. Lock Voltage (Left) axis
            if (voltageYs.Length > 0)
            {
                double vMin = voltageYs.Min();
                double vMax = voltageYs.Max();
                double vPad = Math.Max((vMax - vMin) * 0.05, 50.0);
                // MUST use SetLimits so ScottPlot's render engine respects the explicit clamp bounds
                plot.Axes.SetLimitsY(vMin - vPad, vMax + vPad, plot.Axes.Left);
            }

            // 3. Lock Health (Right 1) axis
            plot.Axes.SetLimitsY(0, 100, plot.Axes.Right);

            // 4. Lock Current (Right 2) axis
            if (currentYsForAxis is not null && _currentAxis is not null)
            {
                double cMin = currentYsForAxis.Min();
                double cMax = currentYsForAxis.Max();
                double cPad = Math.Max((cMax - cMin) * 0.05, 50.0);
                plot.Axes.SetLimitsY(cMin - cPad, cMax + cPad, _currentAxis);
            }
        }

        // Re-add crosshair after plot.Clear() (crosshairs are plottables and are
        // removed by Clear).  Start hidden; it becomes visible on MouseMove.
        _crosshair                      = plot.Add.Crosshair(0, 0);
        _crosshair.IsVisible            = false;
        _crosshair.HorizontalLine.Color = ScottPlot.Colors.Gray.WithAlpha(0.6f);
        _crosshair.VerticalLine.Color   = ScottPlot.Colors.Gray.WithAlpha(0.6f);

        // WinForms hidden tabs have Width/Height of 0. Refreshing a 0x0 chart
        // corrupts ScottPlot's internal axis math. Only refresh when the control
        // is actually visible and has real dimensions.
        if (_formsPlot.Width > 0 && _formsPlot.Height > 0)
        {
            _formsPlot.Refresh();
        }

        // Stream: update display only when not paused.
        if (!_streamPaused)
            RefreshStream();

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

    private static System.Drawing.Color GetLineColor(string line) =>
        StationStreamPanel.GetLineColor(line);

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
