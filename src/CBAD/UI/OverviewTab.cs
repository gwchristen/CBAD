using CBAD.Models;
using CBAD.Parsing;

namespace CBAD.UI;

internal sealed class OverviewTab : UserControl
{
    private readonly StationPanel[] _panels = new StationPanel[4];

    public OverviewTab()
    {
        Dock = DockStyle.Fill;

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
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
        private readonly Label _batteryId;
        private readonly Label _statusDot;
        private readonly Label _statusText;
        private readonly Label _lastSeen;
        private readonly Label _capacity;
        private readonly Label _health;
        private readonly Label _cycles;
        private readonly Label _value;

        public StationPanel(int station)
        {
            Dock = DockStyle.Fill;
            BorderStyle = BorderStyle.FixedSingle;
            Padding = new Padding(8);

            _header = new Label
            {
                Text = $"Station {station}",
                Font = new System.Drawing.Font(Font.FontFamily, 11, System.Drawing.FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };

            _batteryId = new Label { AutoSize = true, ForeColor = System.Drawing.Color.Gray };
            _statusDot = new Label
            {
                AutoSize = false,
                Width = 16,
                Height = 16,
                BackColor = System.Drawing.Color.LightGray
            };
            _statusText = new Label { AutoSize = true };
            _lastSeen = new Label { AutoSize = true, ForeColor = System.Drawing.Color.DimGray };
            _capacity = new Label { AutoSize = true };
            _health = new Label { AutoSize = true };
            _cycles = new Label { AutoSize = true };
            _value = new Label { AutoSize = true, ForeColor = System.Drawing.Color.DimGray };

            var statusRow = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            _statusDot.Margin = new Padding(0, 2, 4, 0);
            statusRow.Controls.Add(_statusDot);
            statusRow.Controls.Add(_statusText);

            var layout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true
            };
            layout.Controls.Add(_header);
            layout.Controls.Add(_batteryId);
            layout.Controls.Add(statusRow);
            layout.Controls.Add(_lastSeen);
            layout.Controls.Add(_capacity);
            layout.Controls.Add(_health);
            layout.Controls.Add(_cycles);
            layout.Controls.Add(_value);

            Controls.Add(layout);

            ResetToNoData();
        }

        private void ResetToNoData()
        {
            _batteryId.Text = "(no data)";
            _batteryId.ForeColor = System.Drawing.Color.Gray;
            _statusDot.BackColor = System.Drawing.Color.LightGray;
            _statusText.Text = "—";
            _lastSeen.Text = string.Empty;
            _capacity.Text = string.Empty;
            _health.Text = string.Empty;
            _cycles.Text = string.Empty;
            _value.Text = string.Empty;
        }

        public void Update(StationState state)
        {
            var rec = state.Latest;
            if (rec is null)
            {
                ResetToNoData();
                return;
            }

            var bid = string.IsNullOrWhiteSpace(rec.BatteryId) ? "(no label)" : rec.BatteryId;
            _batteryId.Text = bid;
            _batteryId.ForeColor = string.IsNullOrWhiteSpace(rec.BatteryId)
                ? System.Drawing.Color.Gray
                : System.Drawing.Color.Black;

            _statusDot.BackColor = string.IsNullOrEmpty(rec.StatusCode)
                ? System.Drawing.Color.LightGray
                : CadexStatusCodes.StatusColor(rec.StatusCode);

            _statusText.Text = string.IsNullOrEmpty(rec.StatusCode)
                ? "—"
                : CadexStatusCodes.Describe(rec.StatusCode);

            _lastSeen.Text = $"Last seen: {rec.ReceivedAt:HH:mm:ss}";

            _capacity.Text = rec.CapacityMah.HasValue ? $"Capacity: {rec.CapacityMah} mAh" : string.Empty;
            _health.Text = rec.HealthPct.HasValue ? $"Health: {rec.HealthPct}%" : string.Empty;
            _cycles.Text = rec.Cycles.HasValue ? $"Cycles: {rec.Cycles}" : string.Empty;
            _value.Text = $"Value: {rec.Value} s";
        }
    }
}
