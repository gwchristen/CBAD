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
        private readonly Label _statusDot;
        private readonly Label _statusText;
        private readonly Label _capacity;
        private readonly Label _health;

        public StationPanel(int station)
        {
            Dock = DockStyle.Fill;
            BorderStyle = BorderStyle.FixedSingle;
            Padding = new Padding(12);

            _header = new Label
            {
                Text = $"Station {station}",
                Font = new System.Drawing.Font(Font.FontFamily, 11, System.Drawing.FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8)
            };

            _statusDot = new Label
            {
                AutoSize = false,
                Width = 16,
                Height = 16,
                BackColor = System.Drawing.Color.LightGray,
                Margin = new Padding(0, 2, 6, 0)
            };
            _statusText = new Label { AutoSize = true };

            var statusRow = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 6)
            };
            statusRow.Controls.Add(_statusDot);
            statusRow.Controls.Add(_statusText);

            _capacity = new Label { AutoSize = true, Margin = new Padding(0, 4, 0, 2) };
            _health    = new Label { AutoSize = true, Margin = new Padding(0, 2, 0, 0) };

            var layout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true
            };
            layout.Controls.Add(_header);
            layout.Controls.Add(statusRow);
            layout.Controls.Add(_capacity);
            layout.Controls.Add(_health);

            Controls.Add(layout);
            ResetToNoData();
        }

        private void ResetToNoData()
        {
            _statusDot.BackColor = System.Drawing.Color.LightGray;
            _statusText.Text = "—";
            _capacity.Text = "Capacity: —";
            _health.Text   = "Health: —";
        }

        public void Update(StationState state)
        {
            var rec = state.Latest;
            if (rec is null) { ResetToNoData(); return; }

            _statusDot.BackColor = string.IsNullOrEmpty(rec.StatusCode)
                ? System.Drawing.Color.LightGray
                : CadexStatusCodes.StatusColor(rec.StatusCode);

            _statusText.Text = string.IsNullOrEmpty(rec.StatusCode)
                ? "—"
                : CadexStatusCodes.Describe(rec.StatusCode);

            _capacity.Text = rec.CapacityMah.HasValue ? $"Capacity: {rec.CapacityMah} mAh" : "Capacity: —";
            _health.Text   = rec.HealthPct.HasValue   ? $"Health: {rec.HealthPct}%"         : "Health: —";
        }
    }
}
