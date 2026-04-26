using CBAD.Models;
using CBAD.Parsing;

namespace CBAD.UI;

internal sealed class OverviewTab : UserControl
{
    private readonly StationPanel[] _panels = new StationPanel[4];

    public OverviewTab()
    {
        Dock = DockStyle.Fill;
        Padding = new Padding(8);

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(4),
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        for (int i = 0; i < 4; i++)
        {
            _panels[i] = new StationPanel(i + 1);
            grid.Controls.Add(_panels[i], i % 2, i / 2);
        }

        Controls.Add(grid);
    }

    public void UpdateStation(StationState state)
    {
        _panels[state.Station - 1].Update(state);
    }

    private sealed class StationPanel : Panel
    {
        private readonly Label _header;
        private readonly Label _statusDot;
        private readonly Label _statusText;
        private readonly Label _voltage;
        private readonly Label _current;
        private readonly Label _temp;
        private readonly Label _health;
        private readonly Label _lastUpdate;
        private readonly Label _emptyState;

        public StationPanel(int station)
        {
            Dock = DockStyle.Fill;
            BorderStyle = BorderStyle.FixedSingle;
            BackColor = System.Drawing.Color.White;
            Padding = new Padding(14, 10, 14, 10);
            Margin = new Padding(4);

            _header = new Label
            {
                Text = $"Station {station}",
                Font = new System.Drawing.Font("Segoe UI", 13, System.Drawing.FontStyle.Bold),
                AutoSize = true,
                ForeColor = System.Drawing.Color.FromArgb(30, 46, 78),
                Margin = new Padding(0, 0, 0, 6),
            };

            _statusDot = new Label
            {
                AutoSize = false,
                Width = 14,
                Height = 14,
                BackColor = System.Drawing.Color.LightGray,
                Margin = new Padding(0, 3, 8, 0),
            };
            _statusText = new Label
            {
                AutoSize = true,
                Font = new System.Drawing.Font(Font.FontFamily, 9.5f, System.Drawing.FontStyle.Bold),
            };

            var statusRow = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 10),
            };
            statusRow.Controls.Add(_statusDot);
            statusRow.Controls.Add(_statusText);

            // Metric labels
            _voltage    = MakeMetricLabel("Voltage:", "—");
            _current    = MakeMetricLabel("Current:", "—");
            _temp       = MakeMetricLabel("Temp:", "—");
            _health     = MakeMetricLabel("Health:", "—");
            _lastUpdate = new Label
            {
                AutoSize = true,
                ForeColor = System.Drawing.Color.Gray,
                Font = new System.Drawing.Font(Font.FontFamily, 8f),
                Margin = new Padding(0, 8, 0, 0),
            };

            var metricsGrid = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 2,
                Margin = new Padding(0, 0, 0, 4),
            };
            metricsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            metricsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            metricsGrid.Controls.Add(_voltage, 0, 0);
            metricsGrid.Controls.Add(_current, 1, 0);
            metricsGrid.Controls.Add(_health,  0, 1);
            metricsGrid.Controls.Add(_temp,    1, 1);

            _emptyState = new Label
            {
                Text = "No data — connect and start capture",
                AutoSize = true,
                ForeColor = System.Drawing.Color.Gray,
                Font = new System.Drawing.Font(Font.FontFamily, 9f, System.Drawing.FontStyle.Italic),
                Margin = new Padding(0, 4, 0, 0),
            };

            var layout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
            };
            layout.Controls.Add(_header);
            layout.Controls.Add(statusRow);
            layout.Controls.Add(metricsGrid);
            layout.Controls.Add(_lastUpdate);
            layout.Controls.Add(_emptyState);

            Controls.Add(layout);
            ResetToNoData();
        }

        private static Label MakeMetricLabel(string caption, string value) => new()
        {
            Text = $"{caption}  {value}",
            AutoSize = true,
            Margin = new Padding(0, 0, 12, 4),
        };

        private void ResetToNoData()
        {
            _statusDot.BackColor = System.Drawing.Color.LightGray;
            _statusText.Text     = "No data";
            _statusText.ForeColor = System.Drawing.Color.Gray;
            _voltage.Text    = "Voltage:   —";
            _current.Text    = "Current:   —";
            _temp.Text       = "Temp:      —";
            _health.Text     = "Health:    —";
            _lastUpdate.Text = string.Empty;
            _emptyState.Visible = true;
        }

        public void Update(StationState state)
        {
            var rec = state.Latest;
            if (rec is null) { ResetToNoData(); return; }

            _emptyState.Visible = false;

            var processCodeStr = rec.ProcessCode?.ToString() ?? "";
            var statusColor = CadexStatusCodes.StatusColor(processCodeStr);
            _statusDot.BackColor = statusColor;
            _statusText.Text     = CadexStatusCodes.Describe(processCodeStr);
            _statusText.ForeColor = statusColor == System.Drawing.Color.LightGray
                ? System.Drawing.Color.DimGray
                : System.Drawing.Color.FromArgb(40, 40, 40);

            _voltage.Text = rec.VoltageMv.HasValue     ? $"Voltage: {rec.VoltageMv} mV"    : "Voltage: —";
            _current.Text = rec.CurrentMa.HasValue     ? $"Current: {rec.CurrentMa} mA"    : "Current: —";
            _temp.Text    = rec.TemperatureC.HasValue  ? $"Temp: {rec.TemperatureC} °C"    : "Temp: —";
            _health.Text  = rec.HealthCurrent.HasValue ? $"Health: {rec.HealthCurrent}%"   : "Health: —";
            _lastUpdate.Text = $"Updated {rec.ReceivedAt:HH:mm:ss} UTC";
        }
    }
}
