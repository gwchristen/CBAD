using CBAD.Models;
using CBAD.Parsing;
using System.Windows.Forms.DataVisualization.Charting;

namespace CBAD.UI;

internal sealed class StationDetailTab : UserControl
{
    private readonly int _station;

    // Detail labels
    private readonly Label _lblStation       = new() { AutoSize = true };
    private readonly Label _lblBatteryId     = new() { AutoSize = true };
    private readonly Label _lblStatus        = new() { AutoSize = true };
    private readonly Label _lblLastUpdate    = new() { AutoSize = true };
    private readonly Label _lblDate          = new() { AutoSize = true };
    private readonly Label _lblTime          = new() { AutoSize = true };
    private readonly Label _lblEventCode     = new() { AutoSize = true };
    private readonly Label _lblBatteryType   = new() { AutoSize = true };
    private readonly Label _lblVoltage       = new() { AutoSize = true };
    private readonly Label _lblCurrent       = new() { AutoSize = true };
    private readonly Label _lblTemp          = new() { AutoSize = true };
    private readonly Label _lblHealthCurrent = new() { AutoSize = true };
    private readonly Label _lblHealthPrev    = new() { AutoSize = true };
    private readonly Label _lblTargetCap     = new() { AutoSize = true };
    private readonly Label _lblResistance    = new() { AutoSize = true };
    private readonly Label _lblParamBlock    = new() { AutoSize = true };

    // Chart
    private readonly Chart _chart = new();

    // Stream
    private readonly TextBox _txtStream;
    private readonly Button _btnExport = new() { Text = "Export Raw Data...", Dock = DockStyle.Bottom, Height = 30 };

    // Stream controls
    private readonly CheckBox _chkAutoScroll = new() { Text = "Auto-scroll", Checked = true, AutoSize = true, Margin = new Padding(4, 4, 4, 3) };
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
            BackColor = System.Drawing.Color.Black,
            ForeColor = System.Drawing.Color.LimeGreen,
        };

        BuildChart();
        WireExport();

        var detailPanel = BuildDetailPanel();

        // Stream panel: toolbar at top, stream fill, export at bottom.
        var streamPanel = new Panel { Dock = DockStyle.Fill };
        streamPanel.Controls.Add(_txtStream);      // DockStyle.Fill — added first, applied last
        streamPanel.Controls.Add(_btnExport);      // DockStyle.Bottom
        streamPanel.Controls.Add(BuildStreamToolbar()); // DockStyle.Top — added last, applied first

        var inner = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
        };
        inner.Panel1.Controls.Add(_chart);
        inner.Panel2.Controls.Add(streamPanel);

        var outer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 210,
            FixedPanel = FixedPanel.Panel1,
        };
        outer.Panel1.Controls.Add(detailPanel);
        outer.Panel2.Controls.Add(inner);

        Controls.Add(outer);
        _lblStation.Text = station.ToString();
    }

    private void BuildChart()
    {
        var area = new ChartArea("Main");
        area.BackColor = System.Drawing.Color.WhiteSmoke;
        area.AxisX.Title = "Time";
        area.AxisX.LabelStyle.Format = "HH:mm";
        area.AxisX.IntervalType = DateTimeIntervalType.Minutes;
        area.AxisX.IntervalAutoMode = IntervalAutoMode.VariableCount;
        area.AxisX.IsMarginVisible = true;
        area.AxisX.MajorGrid.LineColor = System.Drawing.Color.LightGray;
        area.AxisX.MajorGrid.LineDashStyle = ChartDashStyle.Dot;
        area.AxisY.Title = "Voltage (mV)";
        area.AxisY.IsStartedFromZero = false;
        area.AxisY.MajorGrid.LineColor = System.Drawing.Color.LightGray;
        area.AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Dot;
        area.AxisY2.Title = "Health (%)";
        area.AxisY2.Minimum = 0;
        area.AxisY2.Maximum = 100;
        area.AxisY2.Enabled = AxisEnabled.True;
        area.AxisY2.MajorGrid.Enabled = false;
        _chart.ChartAreas.Add(area);

        var voltageSeries = new Series("Voltage (mV)")
        {
            ChartType = SeriesChartType.Line,
            Color = System.Drawing.Color.DeepSkyBlue,
            BorderWidth = 2,
            XValueType = ChartValueType.DateTime,
            YAxisType = AxisType.Primary,
        };
        var healthSeries = new Series("Health %")
        {
            ChartType = SeriesChartType.Line,
            Color = System.Drawing.Color.OrangeRed,
            BorderWidth = 2,
            XValueType = ChartValueType.DateTime,
            YAxisType = AxisType.Secondary,
        };
        _chart.Series.Add(voltageSeries);
        _chart.Series.Add(healthSeries);
        var legend = new Legend { Docking = Docking.Bottom, IsTextAutoFit = true };
        _chart.Legends.Add(legend);
        _chart.Dock = DockStyle.Fill;
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

    private Panel BuildDetailPanel()
    {
        var table = new TableLayoutPanel
        {
            ColumnCount = 4,
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(6),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));

        AddPair(table, "Station:",        _lblStation,       "Battery ID:",      _lblBatteryId);
        AddPair(table, "Status:",         _lblStatus,        "Last Update:",     _lblLastUpdate);
        AddPair(table, "Date:",           _lblDate,          "Time:",            _lblTime);
        AddPair(table, "Event Code:",     _lblEventCode,     "Process Code:",    _lblBatteryType);
        AddPair(table, "Voltage:",        _lblVoltage,       "Current:",         _lblCurrent);
        AddPair(table, "Temperature:",    _lblTemp,          "Health Current:",  _lblHealthCurrent);
        AddPair(table, "Health Prev:",    _lblHealthPrev,    "Target Cap:",      _lblTargetCap);
        AddPair(table, "Resistance:",     _lblResistance,    "Param Block:",     _lblParamBlock);

        var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        panel.Controls.Add(table);
        return panel;
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
            _lblStation.Text       = rec.Station.ToString();
            _lblBatteryId.Text     = string.IsNullOrWhiteSpace(rec.BatteryId) ? "(no label)" : rec.BatteryId;
            _lblStatus.Text        = CadexStatusCodes.Describe(processCodeStr);
            _lblLastUpdate.Text    = rec.ReceivedAt.ToString("HH:mm:ss UTC");
            _lblDate.Text          = rec.Timestamp.ToString("MM/dd/yyyy");
            _lblTime.Text          = rec.Timestamp.ToString("HH:mm:ss");
            _lblEventCode.Text     = $"{rec.EventCode} ({DescribeEvent(rec.EventCode)})";
            _lblBatteryType.Text   = processCodeStr == "" ? "—" : $"{processCodeStr} ({CadexStatusCodes.Describe(processCodeStr)})";
            _lblVoltage.Text       = rec.VoltageMv.HasValue    ? $"{rec.VoltageMv} mV"   : "—";
            _lblCurrent.Text       = rec.CurrentMa.HasValue    ? $"{rec.CurrentMa} mA"   : "—";
            _lblTemp.Text          = rec.TemperatureC.HasValue ? $"{rec.TemperatureC} °C" : "—";
            _lblHealthCurrent.Text = rec.HealthCurrent.HasValue  ? $"{rec.HealthCurrent}%"  : "—";
            _lblHealthPrev.Text    = rec.HealthPrevious.HasValue ? $"{rec.HealthPrevious}%" : "—";
            _lblTargetCap.Text     = rec.TargetCapacityPct.HasValue ? $"{rec.TargetCapacityPct}%" : "—";
            _lblResistance.Text    = rec.ResistanceMOhm.HasValue ? $"{rec.ResistanceMOhm} mΩ" : "—";
            _lblParamBlock.Text    = string.IsNullOrEmpty(rec.ParamBlock) ? "—" : rec.ParamBlock;
        }

        // Chart: rebuild from bounded history (capped at 200 entries) so the chart
        // remains correct when the ring buffer removes oldest entries.
        var chartable = state.History
            .Where(r => r.EventCode == 250 && r.VoltageMv.HasValue)
            .ToList();

        _chart.Series["Voltage (mV)"].Points.Clear();
        _chart.Series["Health %"].Points.Clear();

        foreach (var r in chartable)
        {
            double x = r.ReceivedAt.DateTime.ToOADate();
            _chart.Series["Voltage (mV)"].Points.AddXY(x, r.VoltageMv!.Value);
            if (r.HealthCurrent.HasValue)
                _chart.Series["Health %"].Points.AddXY(x, r.HealthCurrent.Value);
        }

        if (chartable.Count > 0)
            _chart.ChartAreas["Main"].RecalculateAxesScale();

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
}
