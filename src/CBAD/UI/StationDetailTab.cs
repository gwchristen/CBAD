using CBAD.Models;
using CBAD.Parsing;
using System.Windows.Forms.DataVisualization.Charting;

namespace CBAD.UI;

internal sealed class StationDetailTab : UserControl
{
    private readonly int _station;

    // Detail labels
    private readonly Label _lblStation    = new() { AutoSize = true };
    private readonly Label _lblBatteryId  = new() { AutoSize = true };
    private readonly Label _lblStatus     = new() { AutoSize = true };
    private readonly Label _lblLastUpdate = new() { AutoSize = true };
    private readonly Label _lblDate       = new() { AutoSize = true };
    private readonly Label _lblTime       = new() { AutoSize = true };
    private readonly Label _lblValue      = new() { AutoSize = true };
    private readonly Label _lblBatteryType = new() { AutoSize = true };
    private readonly Label _lblCapacity   = new() { AutoSize = true };
    private readonly Label _lblCycles     = new() { AutoSize = true };
    private readonly Label _lblHealth     = new() { AutoSize = true };
    private readonly Label _lblParamBlock = new() { AutoSize = true };
    private readonly Label _lblStatusCode = new() { AutoSize = true };

    // Chart
    private readonly Chart _chart = new();
    private int _chartPointCount = 0;

    // Stream
    private readonly TextBox _txtStream;
    private readonly Button _btnExport = new() { Text = "Export Raw Data...", Dock = DockStyle.Bottom, Height = 30 };
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

        var streamPanel = new Panel { Dock = DockStyle.Fill };
        streamPanel.Controls.Add(_txtStream);
        streamPanel.Controls.Add(_btnExport);

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
        area.AxisY.Title = "Capacity (mAh)";
        area.AxisY2.Title = "Health (%)";
        area.AxisY2.Minimum = 0;
        area.AxisY2.Maximum = 100;
        area.AxisY2.Enabled = AxisEnabled.True;
        _chart.ChartAreas.Add(area);

        var capSeries = new Series("Capacity")
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
        _chart.Series.Add(capSeries);
        _chart.Series.Add(healthSeries);
        _chart.Legends.Add(new Legend { Docking = Docking.Bottom });
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

        AddPair(table, "Station:",      _lblStation,    "Battery ID:",   _lblBatteryId);
        AddPair(table, "Status:",       _lblStatus,     "Last Update:",  _lblLastUpdate);
        AddPair(table, "Date:",         _lblDate,       "Time:",         _lblTime);
        AddPair(table, "Value:",        _lblValue,      "Battery Type:", _lblBatteryType);
        AddPair(table, "Capacity:",     _lblCapacity,   "Cycles:",       _lblCycles);
        AddPair(table, "Health:",       _lblHealth,     "Status Code:",  _lblStatusCode);

        // Param Block spans last row
        var paramLabel = MakeLabel("Param Block:");
        table.Controls.Add(paramLabel);
        _lblParamBlock.AutoSize = true;
        _lblParamBlock.Margin = new Padding(3, 5, 3, 3);
        table.Controls.Add(_lblParamBlock);
        table.SetColumnSpan(_lblParamBlock, 3);

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
            _lblStation.Text    = rec.Station.ToString();
            _lblBatteryId.Text  = string.IsNullOrWhiteSpace(rec.BatteryId) ? "(no label)" : rec.BatteryId;
            _lblStatus.Text     = string.IsNullOrEmpty(rec.StatusCode) ? "—" : CadexStatusCodes.Describe(rec.StatusCode);
            _lblLastUpdate.Text = rec.ReceivedAt.ToString("HH:mm:ss UTC");
            _lblDate.Text       = rec.Timestamp.ToString("MM/dd/yyyy");
            _lblTime.Text       = rec.Timestamp.ToString("HH:mm:ss");
            _lblValue.Text      = rec.Value.ToString();
            _lblBatteryType.Text  = rec.BatteryTypeCode?.ToString() ?? "—";
            _lblCapacity.Text   = rec.CapacityMah.HasValue ? $"{rec.CapacityMah} mAh" : "—";
            _lblCycles.Text     = rec.Cycles?.ToString() ?? "—";
            _lblHealth.Text     = rec.HealthPct.HasValue ? $"{rec.HealthPct}%" : "—";
            _lblParamBlock.Text = string.IsNullOrEmpty(rec.ParamBlock) ? "—" : rec.ParamBlock;
            _lblStatusCode.Text = string.IsNullOrEmpty(rec.StatusCode) ? "—" : rec.StatusCode;
        }

        // Chart: only add new records with full param block
        var chartable = state.History.Where(r => r.CapacityMah.HasValue && r.HealthPct.HasValue).ToList();
        for (int i = _chartPointCount; i < chartable.Count; i++)
        {
            var r = chartable[i];
            double x = r.ReceivedAt.DateTime.ToOADate();
            _chart.Series["Capacity"].Points.AddXY(x, r.CapacityMah!.Value);
            _chart.Series["Health %"].Points.AddXY(x, r.HealthPct!.Value);
        }
        _chartPointCount = chartable.Count;
        if (_chartPointCount > 0)
            _chart.ChartAreas["Main"].RecalculateAxesScale();

        // Stream: last 15 lines only
        _txtStream.Text = string.Join(Environment.NewLine, state.RawLines.TakeLast(15));
        _txtStream.SelectionStart = _txtStream.TextLength;
        _txtStream.ScrollToCaret();
    }
}
