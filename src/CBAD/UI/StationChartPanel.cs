using CBAD.Models;
using ScottPlot.WinForms;

namespace CBAD.UI;

internal sealed class StationChartPanel : UserControl
{
    private readonly FormsPlot _formsPlot = new() { Dock = DockStyle.Fill };
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

    private ScottPlot.IYAxis? _currentAxis;
    private ScottPlot.Plottables.Crosshair? _crosshair;
    private StationState? _lastState;

    public StationChartPanel()
    {
        Dock = DockStyle.Fill;
        Controls.Add(_formsPlot);
        Controls.Add(_lblChartTooltip);

        BuildChart();
        WireChartMouse();
    }

    public void UpdateChart(StationState state)
    {
        _lastState = state;

        var chartable = state.History
            .Where(r => r.EventCode == 250 && r.VoltageMv.HasValue)
            .ToList();

        var plot = _formsPlot.Plot;
        plot.Clear();

        if (chartable.Count > 0)
        {
            var phaseRecords = chartable
                .Select((r, i) => (record: r, index: i))
                .Where(x => x.record.ProcessCode.HasValue)
                .ToList();

            if (phaseRecords.Count > 0)
            {
                int currentCode = phaseRecords[0].record.ProcessCode!.Value;
                int spanStartIdx = phaseRecords[0].index;

                for (int i = 1; i <= phaseRecords.Count; i++)
                {
                    bool isLast = i == phaseRecords.Count;
                    int? nextCode = isLast ? null : phaseRecords[i].record.ProcessCode!.Value;

                    if (nextCode == null || nextCode != currentCode)
                    {
                        int spanEndIdx = isLast
                            ? phaseRecords[i - 1].index
                            : phaseRecords[i].index;

                        ScottPlot.Color? fillColor = currentCode switch
                        {
                            2 => ScottPlot.Colors.LightGreen.WithAlpha(0.2f),
                            7 => ScottPlot.Colors.LightCoral.WithAlpha(0.2f),
                            19 => ScottPlot.Colors.LightGray.WithAlpha(0.2f),
                            _ => null,
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
                            currentCode = nextCode.Value;
                            spanStartIdx = phaseRecords[i].index;
                        }
                    }
                }
            }

            var voltageXs = chartable.Select((_, i) => (double)i).ToArray();
            var voltageYs = chartable.Select(r => (double)r.VoltageMv!.Value).ToArray();
            var voltageScatter = plot.Add.Scatter(voltageXs, voltageYs);
            voltageScatter.LegendText = "Voltage (mV)";
            voltageScatter.Color = ScottPlot.Colors.DeepSkyBlue;
            voltageScatter.LineWidth = 2;
            voltageScatter.MarkerSize = 0;
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
                healthScatter.Color = ScottPlot.Colors.OrangeRed;
                healthScatter.LineWidth = 2;
                healthScatter.MarkerSize = 0;
                healthScatter.Axes.YAxis = plot.Axes.Right;
            }

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
                currentScatter.Color = ScottPlot.Colors.Orange;
                currentScatter.LineWidth = 2;
                currentScatter.MarkerSize = 0;
                currentScatter.Axes.YAxis = _currentAxis;
            }

            double xMin = voltageXs.Min();
            double xMax = voltageXs.Max();
            double xRange = xMax - xMin;
            double xPad = xRange > 0 ? xRange * 0.02 : 0.0001;
            plot.Axes.SetLimitsX(xMin - xPad, xMax + xPad);

            if (voltageYs.Length > 0)
            {
                double vMin = voltageYs.Min();
                double vMax = voltageYs.Max();
                double vPad = Math.Max((vMax - vMin) * 0.05, 50.0);
                plot.Axes.SetLimitsY(vMin - vPad, vMax + vPad, plot.Axes.Left);
            }

            plot.Axes.SetLimitsY(0, 100, plot.Axes.Right);

            if (currentYsForAxis is not null && _currentAxis is not null)
            {
                double cMin = currentYsForAxis.Min();
                double cMax = currentYsForAxis.Max();
                double cPad = Math.Max((cMax - cMin) * 0.05, 50.0);
                plot.Axes.SetLimitsY(cMin - cPad, cMax + cPad, _currentAxis);
            }
        }

        _crosshair = plot.Add.Crosshair(0, 0);
        _crosshair.IsVisible = false;
        _crosshair.HorizontalLine.Color = ScottPlot.Colors.Gray.WithAlpha(0.6f);
        _crosshair.VerticalLine.Color = ScottPlot.Colors.Gray.WithAlpha(0.6f);

        RefreshPlotIfVisible();
    }

    public void ClearChart()
    {
        _lastState = null;

        var plot = _formsPlot.Plot;
        plot.Clear();
        _crosshair = null;

        RefreshPlotIfVisible();

        _lblChartTooltip.Text = string.Empty;
        _lblChartTooltip.Visible = false;
    }

    public void ApplyTheme(bool isDark)
    {
        _lblChartTooltip.ForeColor = AppTheme.MutedFg(isDark);
        _lblChartTooltip.BackColor = AppTheme.PanelBg(isDark);
    }

    internal byte[] GetChartImageBytes(int width, int height) =>
        _formsPlot.Plot.GetImage(width, height).GetImageBytes();

    private void BuildChart()
    {
        var plot = _formsPlot.Plot;

        plot.Axes.Bottom.Label.Text = "Time (minutes)";
        plot.Axes.Left.Label.Text = "Voltage (mV)";
        plot.Axes.Right.Label.Text = "Health (%)";
        plot.Axes.Right.IsVisible = true;

        _currentAxis = plot.Axes.AddRightAxis();
        _currentAxis.Label.Text = "Current (mA)";

        plot.Grid.MajorLineColor = ScottPlot.Colors.LightGray.WithAlpha(0.5f);
        plot.FigureBackground.Color = ScottPlot.Colors.WhiteSmoke;
        plot.DataBackground.Color = ScottPlot.Colors.White;
        plot.ShowLegend(ScottPlot.Alignment.UpperLeft);
    }

    private void WireChartMouse()
    {
        _formsPlot.MouseMove += OnFormsPlotMouseMove;
        _formsPlot.MouseLeave += (_, __) =>
        {
            if (_crosshair is not null)
            {
                _crosshair.IsVisible = false;
                RefreshPlotIfVisible();
            }
            _lblChartTooltip.Text = string.Empty;
            _lblChartTooltip.Visible = false;
        };
    }

    private void OnFormsPlotMouseMove(object? sender, MouseEventArgs e)
    {
        if (_crosshair is null || _lastState is null)
            return;

        var chartable = _lastState.History
            .Where(r => r.EventCode == 250 && r.VoltageMv.HasValue)
            .ToList();

        if (chartable.Count == 0)
            return;

        var coords = _formsPlot.Plot.GetCoordinates(e.X, e.Y);
        double mouseX = coords.X;

        CadexRecord? nearest = null;
        int idx = 0;
        double minDist = double.MaxValue;
        for (int i = 0; i < chartable.Count; i++)
        {
            double dist = Math.Abs(i - mouseX);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = chartable[i];
                idx = i;
            }
        }

        if (nearest is null)
            return;

        _crosshair.X = idx;
        _crosshair.Y = nearest.VoltageMv!.Value;
        _crosshair.IsVisible = true;

        double absCurrentMa = nearest.CurrentMa.HasValue ? Math.Abs((double)nearest.CurrentMa.Value) : 0;
        _lblChartTooltip.Text =
            $"⏱ {idx} min  |  " +
            $"V: {(nearest.VoltageMv.HasValue ? $"{nearest.VoltageMv} mV" : "—")}  |  " +
            $"I: {(nearest.CurrentMa.HasValue ? $"{absCurrentMa} mA" : "—")}  |  " +
            $"Health: {(nearest.HealthCurrent.HasValue ? $"{nearest.HealthCurrent}%" : "—")}";
        _lblChartTooltip.Visible = true;

        RefreshPlotIfVisible();
    }

    private void RefreshPlotIfVisible()
    {
        if (_formsPlot.Width > 0 && _formsPlot.Height > 0)
            _formsPlot.Refresh();
    }
}
