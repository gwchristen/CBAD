using CBAD.Models;
using CBAD.Parsing;
using ScottPlot.WinForms;

namespace CBAD.UI;

internal sealed class StationDetailTab : UserControl
{
    private readonly int _station;

    // ── Essentials section labels ───────────────────────────────────────
    private readonly Label _lblStationBig  = new() { AutoSize = true };
    private readonly Label _lblBatteryId   = new() { AutoSize = true };
    private readonly Label _lblStatusDot   = new() { AutoSize = false, Width = 14, Height = 14, Margin = new Padding(0, 3, 8, 0) };
    private readonly Label _lblStatus      = new() { AutoSize = true };
    private readonly Label _lblVoltageIcon = new() { AutoSize = true };
    private readonly Label _lblCurrentIcon = new() { AutoSize = true };
    private readonly Label _lblHealthIcon  = new() { AutoSize = true };
    private readonly Label _lblTempIcon    = new() { AutoSize = true };
    private readonly Label _lblVoltage     = new() { AutoSize = true };
    private readonly Label _lblCurrent     = new() { AutoSize = true };
    private readonly Label _lblHealth      = new() { AutoSize = true };
    private readonly Label _lblTemp        = new() { AutoSize = true };
    private readonly Label _lblLastUpdate  = new() { AutoSize = true };

    // ── Advanced detail labels ──────────────────────────────────────────
    private readonly Label _lblStation       = new() { AutoSize = true };
    private readonly Label _lblLastUpdateAdv = new() { AutoSize = true };
    private readonly Label _lblDate          = new() { AutoSize = true };
    private readonly Label _lblTime          = new() { AutoSize = true };
    private readonly Label _lblEventCode     = new() { AutoSize = true };
    private readonly Label _lblBatteryType   = new() { AutoSize = true };
    private readonly Label _lblHealthCurrent = new() { AutoSize = true };
    private readonly Label _lblHealthPrev    = new() { AutoSize = true };
    private readonly Label _lblTargetCap     = new() { AutoSize = true };
    private readonly Label _lblResistance    = new() { AutoSize = true };

    // Theming support
    private Panel? _essentialsPanel;
    private GroupBox? _advGroup;
    private TableLayoutPanel? _advTable;
    private bool _isDark;

    // Chart
    private readonly FormsPlot _formsPlot = new() { Dock = DockStyle.Fill };

    // Stream
    private readonly TextBox _txtStream;
    private readonly Button _btnExport = new() { Text = "Export Raw Data…", Dock = DockStyle.Bottom, Height = 30 };

    // Stream controls
    private readonly CheckBox _chkAutoScroll  = new() { Text = "Auto-scroll", Checked = true, AutoSize = true, Margin = new Padding(4, 4, 4, 3) };
    private readonly Button   _btnPauseStream = new() { Text = "⏸  Pause", AutoSize = true, Margin = new Padding(4, 2, 4, 2) };
    private readonly TextBox  _txtFilter      = new() { Width = 160, PlaceholderText = "Filter lines…", Margin = new Padding(4, 2, 4, 2) };
    private bool _streamPaused;

    private StationState? _lastState;

    public StationDetailTab(int station)
    {
        _station = station;
        Dock = DockStyle.Fill;

        _txtStream = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            WordWrap = false,
            ScrollBars = ScrollBars.Both,
            Dock = DockStyle.Fill,
            Font = new System.Drawing.Font("Consolas", 9),
            BackColor = System.Drawing.Color.FromArgb(18, 24, 36),
            ForeColor = System.Drawing.Color.FromArgb(130, 210, 130),
        };

        BuildChart();
        WireExport();

        // ── Outer split: essentials+details (top) | chart+stream (bottom) ──
        // Panel1 is fixed to ensure the Additional Details card is always fully
        // visible without needing a scrollbar; Panel2MinSize ensures the chart
        // always has visible space.
        var outer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 380,
            FixedPanel = FixedPanel.Panel1,
            IsSplitterFixed = true,
            Panel1MinSize = 380,
            Panel2MinSize = 150,
        };
        outer.Panel1.Controls.Add(BuildEssentialsPanel());
        outer.Panel2.Controls.Add(BuildChartStreamTabs());

        Controls.Add(outer);
    }

    // ── Essentials panel ───────────────────────────────────────────────
    private Panel BuildEssentialsPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
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

        // Last update
        _lblLastUpdate.ForeColor = System.Drawing.Color.Gray;
        _lblLastUpdate.Font = new System.Drawing.Font(Font.FontFamily, 8.5f);
        _lblLastUpdate.Text = "No data yet — connect and start capture";

        // Additional details GroupBox
        var advGroup = BuildAdvancedGroup();

        // Use a single-column TableLayoutPanel so every row — including the
        // GroupBox — spans the full available width (unlike TopDown FlowLayout
        // which does not stretch children horizontally by default).
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            BackColor = System.Drawing.Color.Transparent,
            Padding = new Padding(0),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // heading
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // battery id
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // status row
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // metrics
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));  // last update
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f)); // adv group — fills remaining

        layout.Controls.Add(_lblStationBig,  0, 0);
        layout.Controls.Add(_lblBatteryId,   0, 1);
        layout.Controls.Add(statusRow,       0, 2);
        layout.Controls.Add(metricsFlow,     0, 3);
        layout.Controls.Add(_lblLastUpdate,  0, 4);
        layout.Controls.Add(advGroup,        0, 5);

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

    private GroupBox BuildAdvancedGroup()
    {
        var group = new GroupBox
        {
            Text = "Additional Details",
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 2, 8, 8),
            Margin = new Padding(0, 4, 0, 4),
        };

        // Use Percent 50/50 for value columns so they expand to fill the full
        // GroupBox width (previously 28/28 left 44 % of horizontal space unused).
        // Dock = Fill on the table (rather than AutoSize + DockTop) ensures the
        // Percent column widths are calculated against the actual GroupBox width.
        var table = new TableLayoutPanel
        {
            ColumnCount = 4,
            Dock = DockStyle.Fill,
            Padding = new Padding(2),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

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
        group.Paint += OnAdvGroupPaint;

        return group;
    }

    private void OnAdvGroupPaint(object? sender, PaintEventArgs e)
    {
        if (!_isDark) return;

        var gb = (GroupBox)sender!;
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

    // ── Chart + Stream tabs ─────────────────────────────────────────────
    private TabControl BuildChartStreamTabs()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };

        // Chart tab
        var chartTab = new TabPage("Chart");
        chartTab.Controls.Add(_formsPlot);
        tabs.TabPages.Add(chartTab);

        // Stream tab
        var streamTab = new TabPage("Raw Stream");
        var streamPanel = new Panel { Dock = DockStyle.Fill };
        streamPanel.Controls.Add(_txtStream);                   // Fill — added first
        streamPanel.Controls.Add(_btnExport);                   // Bottom
        streamPanel.Controls.Add(BuildStreamToolbar());         // Top — added last
        streamTab.Controls.Add(streamPanel);
        tabs.TabPages.Add(streamTab);

        return tabs;
    }

    private void BuildChart()
    {
        var plot = _formsPlot.Plot;

        // Axis labels
        plot.Axes.Bottom.Label.Text = "Time";
        plot.Axes.Left.Label.Text   = "Voltage (mV)";
        plot.Axes.Right.Label.Text  = "Health (%)";
        plot.Axes.Right.IsVisible   = true;

        // DateTime ticks on the X axis
        plot.Axes.DateTimeTicksBottom();

        // Grid style
        plot.Grid.MajorLineColor = ScottPlot.Colors.LightGray.WithAlpha(0.5f);

        // Background
        plot.FigureBackground.Color = ScottPlot.Colors.WhiteSmoke;
        plot.DataBackground.Color   = ScottPlot.Colors.White;

        // Show legend
        plot.ShowLegend();
    }

    private void WireExport()
    {
        _btnExport.Click += (_, __) =>
        {
            if (_lastState is null || _lastState.RawLines.Count == 0)
            {
                MessageBox.Show("No data to export.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using var dlg = new SaveFileDialog
            {
                Title = "Export Raw Station Data",
                Filter = "CSV files (*.csv)|*.csv|Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = $"station{_station}_export_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            File.WriteAllLines(dlg.FileName, _lastState.RawLines);
            MessageBox.Show(
                $"Exported {_lastState.RawLines.Count} lines to:{Environment.NewLine}{dlg.FileName}",
                "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
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

        _txtStream.Text = string.Join(Environment.NewLine, lines.TakeLast(200));

        if (_chkAutoScroll.Checked)
        {
            _txtStream.SelectionStart = _txtStream.TextLength;
            _txtStream.ScrollToCaret();
        }
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
            _lblStatusDot.BackColor = statusColor;
            _lblStatus.Text      = CadexStatusCodes.Describe(processCodeStr);
            _lblStatus.ForeColor = statusColor == System.Drawing.Color.LightGray
                ? AppTheme.MutedFg(_isDark)
                : AppTheme.LabelFg(_isDark);

            _lblVoltage.Text = rec.VoltageMv.HasValue    ? $"Voltage: {rec.VoltageMv} mV"    : "Voltage: —";
            _lblCurrent.Text = rec.CurrentMa.HasValue    ? $"Current: {rec.CurrentMa} mA"    : "Current: —";
            _lblHealth.Text  = rec.HealthCurrent.HasValue  ? $"Health: {rec.HealthCurrent}%"  : "Health: —";
            _lblTemp.Text    = rec.TemperatureC.HasValue ? $"Temp: {rec.TemperatureC} °C"    : "Temp: —";
            _lblLastUpdate.Text = $"Last update: {rec.ReceivedAt:HH:mm:ss} UTC";

            // Advanced section
            _lblStation.Text        = rec.Station.ToString();
            _lblLastUpdateAdv.Text  = rec.ReceivedAt.ToString("HH:mm:ss UTC");
            _lblDate.Text           = rec.Timestamp.ToString("MM/dd/yyyy");
            _lblTime.Text           = rec.Timestamp.ToString("HH:mm:ss");
            _lblEventCode.Text      = $"{rec.EventCode} ({DescribeEvent(rec.EventCode)})";
            _lblBatteryType.Text    = processCodeStr == "" ? "—" : $"{processCodeStr} ({CadexStatusCodes.Describe(processCodeStr)})";
            _lblHealthCurrent.Text  = rec.HealthCurrent.HasValue  ? $"{rec.HealthCurrent}%"  : "—";
            _lblHealthPrev.Text     = rec.HealthPrevious.HasValue ? $"{rec.HealthPrevious}%" : "—";
            _lblTargetCap.Text      = rec.TargetCapacityPct.HasValue ? $"{rec.TargetCapacityPct}%" : "—";
            _lblResistance.Text     = rec.ResistanceMOhm.HasValue ? $"{rec.ResistanceMOhm} mΩ" : "—";
        }

        // Chart: rebuild from bounded history (capped at 200 entries) so the chart
        // remains correct when the ring buffer removes oldest entries.
        var chartable = state.History
            .Where(r => r.EventCode == 250 && r.VoltageMv.HasValue)
            .ToList();

        var plot = _formsPlot.Plot;
        plot.Clear();

        if (chartable.Count > 0)
        {
            var voltageXs = chartable.Select(r => r.ReceivedAt.DateTime.ToOADate()).ToArray();
            var voltageYs = chartable.Select(r => (double)r.VoltageMv!.Value).ToArray();
            var voltageScatter = plot.Add.Scatter(voltageXs, voltageYs);
            voltageScatter.LegendText = "Voltage (mV)";
            voltageScatter.Color      = ScottPlot.Colors.DeepSkyBlue;
            voltageScatter.LineWidth  = 2;
            voltageScatter.MarkerSize = 0;

            var healthPoints = chartable.Where(r => r.HealthCurrent.HasValue).ToList();
            if (healthPoints.Count > 0)
            {
                var healthXs = healthPoints.Select(r => r.ReceivedAt.DateTime.ToOADate()).ToArray();
                var healthYs = healthPoints.Select(r => (double)r.HealthCurrent!.Value).ToArray();
                var healthScatter = plot.Add.Scatter(healthXs, healthYs);
                healthScatter.LegendText = "Health (%)";
                healthScatter.Color      = ScottPlot.Colors.OrangeRed;
                healthScatter.LineWidth  = 2;
                healthScatter.MarkerSize = 0;
                healthScatter.Axes.YAxis = plot.Axes.Right;
            }

            plot.Axes.AutoScale();
            // Lock the health axis to 0–100 %
            plot.Axes.Right.Min = 0;
            plot.Axes.Right.Max = 100;
        }

        plot.Axes.DateTimeTicksBottom();
        _formsPlot.Refresh();

        // Stream: update display only when not paused.
        if (!_streamPaused)
            RefreshStream();
    }

    private static string DescribeEvent(int code) => code switch
    {
        11  => "Processing Began",
        20  => "Battery Inserted",
        27  => "OhmTest",
        201 => "Adapter Inserted",
        250 => "Normal Processing",
        _   => $"{code} sec elapsed",
    };

    // ── Theming ─────────────────────────────────────────────────────────
    public void ApplyTheme(bool isDark)
    {
        _isDark = isDark;

        BackColor = AppTheme.PanelBg(isDark);

        if (_essentialsPanel != null)
            _essentialsPanel.BackColor = AppTheme.PanelBg(isDark);

        // Essentials section
        _lblStationBig.ForeColor = AppTheme.HeadingFg(isDark);
        _lblBatteryId.ForeColor  = AppTheme.MutedFg(isDark);
        _lblStatus.ForeColor     = AppTheme.MutedFg(isDark);
        _lblVoltage.ForeColor    = AppTheme.LabelFg(isDark);
        _lblCurrent.ForeColor    = AppTheme.LabelFg(isDark);
        _lblHealth.ForeColor     = AppTheme.LabelFg(isDark);
        _lblTemp.ForeColor       = AppTheme.LabelFg(isDark);
        _lblLastUpdate.ForeColor = AppTheme.MutedFg(isDark);

        // Advanced detail value labels
        _lblStation.ForeColor       = AppTheme.LabelFg(isDark);
        _lblLastUpdateAdv.ForeColor = AppTheme.LabelFg(isDark);
        _lblDate.ForeColor          = AppTheme.LabelFg(isDark);
        _lblTime.ForeColor          = AppTheme.LabelFg(isDark);
        _lblEventCode.ForeColor     = AppTheme.LabelFg(isDark);
        _lblBatteryType.ForeColor   = AppTheme.LabelFg(isDark);
        _lblHealthCurrent.ForeColor = AppTheme.LabelFg(isDark);
        _lblHealthPrev.ForeColor    = AppTheme.LabelFg(isDark);
        _lblTargetCap.ForeColor     = AppTheme.LabelFg(isDark);
        _lblResistance.ForeColor    = AppTheme.LabelFg(isDark);

        // GroupBox border/background and its key-label children
        if (_advGroup != null)
        {
            _advGroup.ForeColor = AppTheme.LabelFg(isDark);
            _advGroup.BackColor = AppTheme.PanelBg(isDark);
            _advGroup.Invalidate();
        }

        if (_advTable != null)
        {
            _advTable.BackColor = AppTheme.PanelBg(isDark);
            foreach (Control c in _advTable.Controls)
                if (c is Label lbl)
                    lbl.ForeColor = AppTheme.LabelFg(isDark);
        }
    }
}
