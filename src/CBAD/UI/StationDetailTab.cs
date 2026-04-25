using CBAD.Models;
using CBAD.Parsing;

namespace CBAD.UI;

internal sealed class StationDetailTab : UserControl
{
    private readonly int _station;

    private readonly Label _lblStation = new() { AutoSize = true };
    private readonly Label _lblBatteryId = new() { AutoSize = true };
    private readonly Label _lblStatus = new() { AutoSize = true };
    private readonly Label _lblLastUpdate = new() { AutoSize = true };
    private readonly Label _lblDate = new() { AutoSize = true };
    private readonly Label _lblTime = new() { AutoSize = true };
    private readonly Label _lblValue = new() { AutoSize = true };
    private readonly Label _lblBatteryType = new() { AutoSize = true };
    private readonly Label _lblCapacity = new() { AutoSize = true };
    private readonly Label _lblCycles = new() { AutoSize = true };
    private readonly Label _lblHealth = new() { AutoSize = true };
    private readonly Label _lblParamBlock = new() { AutoSize = true };
    private readonly Label _lblStatusCode = new() { AutoSize = true };

    private readonly TextBox _txtStream;
    private int _streamLineCount;

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

        var detailPanel = BuildDetailPanel();

        var streamGroup = new GroupBox
        {
            Text = "Live Stream",
            Dock = DockStyle.Fill,
            Padding = new Padding(4)
        };
        streamGroup.Controls.Add(_txtStream);

        var splitter = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 220,
            FixedPanel = FixedPanel.Panel1
        };
        splitter.Panel1.Controls.Add(detailPanel);
        splitter.Panel2.Controls.Add(streamGroup);

        Controls.Add(splitter);
    }

    private Panel BuildDetailPanel()
    {
        var table = new TableLayoutPanel
        {
            ColumnCount = 2,
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(8)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(table, "Station", _lblStation);
        AddRow(table, "Battery ID", _lblBatteryId);
        AddRow(table, "Status", _lblStatus);
        AddRow(table, "Last Update", _lblLastUpdate);
        AddRow(table, "Date", _lblDate);
        AddRow(table, "Time", _lblTime);
        AddRow(table, "Value", _lblValue);
        AddRow(table, "Battery Type", _lblBatteryType);
        AddRow(table, "Capacity", _lblCapacity);
        AddRow(table, "Cycles", _lblCycles);
        AddRow(table, "Health", _lblHealth);
        AddRow(table, "Param Block", _lblParamBlock);
        AddRow(table, "Status Code", _lblStatusCode);

        var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        panel.Controls.Add(table);

        _lblStation.Text = _station.ToString();
        return panel;
    }

    private static void AddRow(TableLayoutPanel table, string label, Control value)
    {
        var lbl = new Label
        {
            Text = label + ":",
            AutoSize = true,
            Font = new System.Drawing.Font(SystemFonts.DefaultFont, System.Drawing.FontStyle.Bold),
            Margin = new Padding(3, 5, 10, 3),
            TextAlign = System.Drawing.ContentAlignment.MiddleRight
        };
        table.Controls.Add(lbl);
        value.Margin = new Padding(3, 5, 3, 3);
        value.AutoSize = true;
        table.Controls.Add(value);
    }

    public void UpdateStation(StationState state)
    {
        var rec = state.Latest;
        if (rec is not null)
        {
            _lblStation.Text = rec.Station.ToString();
            _lblBatteryId.Text = string.IsNullOrWhiteSpace(rec.BatteryId) ? "(no label)" : rec.BatteryId;
            _lblStatus.Text = string.IsNullOrEmpty(rec.StatusCode)
                ? "—"
                : CadexStatusCodes.Describe(rec.StatusCode);
            _lblLastUpdate.Text = rec.ReceivedAt.ToString("HH:mm:ss UTC");
            _lblDate.Text = rec.Timestamp.ToString("MM/dd/yyyy");
            _lblTime.Text = rec.Timestamp.ToString("HH:mm:ss");
            _lblValue.Text = rec.Value.ToString();
            _lblBatteryType.Text = rec.BatteryTypeCode?.ToString() ?? "—";
            _lblCapacity.Text = rec.CapacityMah.HasValue ? $"{rec.CapacityMah} mAh" : "—";
            _lblCycles.Text = rec.Cycles?.ToString() ?? "—";
            _lblHealth.Text = rec.HealthPct.HasValue ? $"{rec.HealthPct}%" : "—";
            _lblParamBlock.Text = string.IsNullOrEmpty(rec.ParamBlock) ? "—" : rec.ParamBlock;
            _lblStatusCode.Text = string.IsNullOrEmpty(rec.StatusCode) ? "—" : rec.StatusCode;
        }

        // Append any new raw lines
        var newLines = state.RawLines.Skip(_streamLineCount).ToList();
        if (newLines.Count > 0)
        {
            foreach (var line in newLines)
                _txtStream.AppendText(line + Environment.NewLine);

            _streamLineCount = state.RawLines.Count;
            _txtStream.SelectionStart = _txtStream.TextLength;
            _txtStream.ScrollToCaret();
        }
    }
}
