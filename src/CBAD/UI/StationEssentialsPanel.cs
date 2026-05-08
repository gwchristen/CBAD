using CBAD.Models;
using CBAD.Parsing;

namespace CBAD.UI;

internal sealed class StationEssentialsPanel : UserControl
{
    private readonly int _station;

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
    private readonly Label _lblRuntime     = new() { AutoSize = true };

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

    private readonly Button _btnClearStation = new() { Text = "🗑  Clear Data", AutoSize = true, Margin = new Padding(16, 0, 0, 0) };

    private Panel? _essentialsPanel;
    private GroupBox? _advGroup;
    private TableLayoutPanel? _advTable;
    private bool _isDark;

    public event EventHandler? ClearRequested;

    public StationEssentialsPanel(int station)
    {
        _station = station;
        Dock = DockStyle.Fill;
        Controls.Add(BuildEssentialsPanel());

        _btnClearStation.Click += (_, __) => ClearRequested?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateStation(StationState state, bool isDark)
    {
        _isDark = isDark;

        var rec = state.Latest;
        if (rec is not null)
        {
            var processCodeStr = rec.ProcessCode?.ToString() ?? "";
            var statusColor = CadexStatusCodes.StatusColor(processCodeStr);

            _lblBatteryId.Text = string.IsNullOrWhiteSpace(rec.BatteryId) ? "Battery: (no label)" : $"Battery: {rec.BatteryId}";

            if (!string.IsNullOrEmpty(state.FailureReason))
            {
                _lblStatusDot.BackColor = System.Drawing.Color.Red;
                _lblStatus.Text = $"FAIL: {state.FailureReason}";
                _lblStatus.ForeColor = System.Drawing.Color.Red;
                _lblStatus.Font = new System.Drawing.Font(
                    _lblStatus.Font.FontFamily,
                    _lblStatus.Font.Size,
                    System.Drawing.FontStyle.Bold);
            }
            else
            {
                _lblStatusDot.BackColor = statusColor;
                _lblStatus.Text = CadexStatusCodes.Describe(processCodeStr);
                _lblStatus.ForeColor = statusColor == System.Drawing.Color.LightGray
                    ? AppTheme.MutedFg(_isDark)
                    : AppTheme.LabelFg(_isDark);
                _lblStatus.Font = new System.Drawing.Font(
                    _lblStatus.Font.FontFamily,
                    _lblStatus.Font.Size,
                    System.Drawing.FontStyle.Regular);
            }

            _lblVoltage.Text = rec.VoltageMv.HasValue ? $"Voltage: {rec.VoltageMv} mV" : "Voltage: —";
            _lblCurrent.Text = rec.CurrentMa.HasValue ? $"Current: {rec.CurrentMa} mA" : "Current: —";
            _lblHealth.Text = rec.HealthCurrent.HasValue ? $"Health: {rec.HealthCurrent}%" : "Health: —";
            _lblTemp.Text = rec.TemperatureC.HasValue ? $"Temp: {rec.TemperatureC} °C" : "Temp: —";

            _lblRuntime.Text = state.SessionStart.HasValue
                ? $"Runtime: {FormatRuntime(DateTimeOffset.UtcNow - state.SessionStart.Value)}"
                : string.Empty;

            _lblLastUpdate.Text = $"Last update: {rec.ReceivedAt:HH:mm:ss} UTC";

            _lblStation.Text = rec.Station.ToString();
            _lblLastUpdateAdv.Text = rec.ReceivedAt.ToString("HH:mm:ss UTC");
            _lblDate.Text = rec.Timestamp.ToString("MM/dd/yyyy");
            _lblTime.Text = rec.Timestamp.ToString("HH:mm:ss");

            var eventDesc = CadexEventParser.Describe(rec.EventCode);
            var eventPayload = CadexEventParser.FormatPayload(rec);
            _lblEventCode.Text = string.IsNullOrEmpty(eventPayload)
                ? $"{rec.EventCode} ({eventDesc})"
                : $"{rec.EventCode} ({eventDesc}: {eventPayload})";

            _lblBatteryType.Text = processCodeStr == "" ? "—" : $"{processCodeStr} ({CadexStatusCodes.Describe(processCodeStr)})";
            _lblHealthCurrent.Text = rec.HealthCurrent.HasValue ? $"{rec.HealthCurrent}%" : "—";
            _lblHealthPrev.Text = rec.HealthPrevious.HasValue ? $"{rec.HealthPrevious}%" : "—";
            _lblResistance.Text = rec.ResistanceMOhm.HasValue ? $"{rec.ResistanceMOhm} mΩ" : "—";
        }

        _lblTargetCap.Text = !string.IsNullOrEmpty(state.TargetCapacity) ? state.TargetCapacity : "—";
    }

    public void ApplyTheme(bool isDark)
    {
        _isDark = isDark;

        BackColor = AppTheme.PanelBg(isDark);

        if (_essentialsPanel != null)
            _essentialsPanel.BackColor = AppTheme.PanelBg(isDark);

        _lblStationBig.ForeColor = AppTheme.HeadingFg(isDark);
        _lblBatteryId.ForeColor = AppTheme.MutedFg(isDark);
        _lblStatus.ForeColor = AppTheme.MutedFg(isDark);
        _lblVoltage.ForeColor = AppTheme.LabelFg(isDark);
        _lblCurrent.ForeColor = AppTheme.LabelFg(isDark);
        _lblHealth.ForeColor = AppTheme.LabelFg(isDark);
        _lblTemp.ForeColor = AppTheme.LabelFg(isDark);
        _lblLastUpdate.ForeColor = AppTheme.MutedFg(isDark);
        _lblRuntime.ForeColor = AppTheme.MutedFg(isDark);

        _lblStation.ForeColor = AppTheme.LabelFg(isDark);
        _lblLastUpdateAdv.ForeColor = AppTheme.LabelFg(isDark);
        _lblDate.ForeColor = AppTheme.LabelFg(isDark);
        _lblTime.ForeColor = AppTheme.LabelFg(isDark);
        _lblEventCode.ForeColor = AppTheme.LabelFg(isDark);
        _lblBatteryType.ForeColor = AppTheme.LabelFg(isDark);
        _lblHealthCurrent.ForeColor = AppTheme.LabelFg(isDark);
        _lblHealthPrev.ForeColor = AppTheme.LabelFg(isDark);
        _lblTargetCap.ForeColor = AppTheme.LabelFg(isDark);
        _lblResistance.ForeColor = AppTheme.LabelFg(isDark);

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

    public void Reset(bool isDark)
    {
        _isDark = isDark;

        _lblBatteryId.Text = "Battery: —";
        _lblStatusDot.BackColor = System.Drawing.Color.LightGray;
        _lblStatus.Text = "No data";
        _lblStatus.ForeColor = AppTheme.MutedFg(_isDark);
        _lblStatus.Font = new System.Drawing.Font(
            _lblStatus.Font.FontFamily,
            _lblStatus.Font.Size,
            System.Drawing.FontStyle.Regular);
        _lblVoltage.Text = "Voltage: —";
        _lblCurrent.Text = "Current: —";
        _lblHealth.Text = "Health: —";
        _lblTemp.Text = "Temp: —";
        _lblLastUpdate.Text = "No data yet — connect and start capture";
        _lblRuntime.Text = string.Empty;

        _lblStation.Text = "—";
        _lblLastUpdateAdv.Text = "—";
        _lblDate.Text = "—";
        _lblTime.Text = "—";
        _lblEventCode.Text = "—";
        _lblBatteryType.Text = "—";
        _lblHealthCurrent.Text = "—";
        _lblHealthPrev.Text = "—";
        _lblTargetCap.Text = "—";
        _lblResistance.Text = "—";
    }

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

        _lblStationBig.Font = new System.Drawing.Font("Segoe UI", 14f, System.Drawing.FontStyle.Bold);
        _lblStationBig.ForeColor = System.Drawing.Color.FromArgb(30, 46, 78);
        _lblStationBig.Text = $"Station {_station}";
        _lblStationBig.Margin = new Padding(0, 0, 0, 4);

        _lblBatteryId.Font = new System.Drawing.Font(Font.FontFamily, 9.5f);
        _lblBatteryId.ForeColor = System.Drawing.Color.Gray;
        _lblBatteryId.Text = "Battery: —";
        _lblBatteryId.Margin = new Padding(0, 0, 0, 8);

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

        StyleIconLabel(_lblVoltageIcon, "⚡", System.Drawing.Color.Gold);
        StyleIconLabel(_lblCurrentIcon, "🔌", System.Drawing.Color.DeepSkyBlue);
        StyleIconLabel(_lblHealthIcon, "🔋", System.Drawing.Color.LimeGreen);
        StyleIconLabel(_lblTempIcon, "🌡️", System.Drawing.Color.Tomato);
        StyleMetricLabel(_lblVoltage, "Voltage", "—");
        StyleMetricLabel(_lblCurrent, "Current", "—");
        StyleMetricLabel(_lblHealth, "Health", "—");
        StyleMetricLabel(_lblTemp, "Temp", "—");

        var metricsFlow = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 8),
        };
        metricsFlow.Controls.Add(MakeMetricCell(_lblVoltageIcon, _lblVoltage));
        metricsFlow.Controls.Add(MakeMetricCell(_lblCurrentIcon, _lblCurrent));
        metricsFlow.Controls.Add(MakeMetricCell(_lblHealthIcon, _lblHealth));
        metricsFlow.Controls.Add(MakeMetricCell(_lblTempIcon, _lblTemp));

        _lblLastUpdate.ForeColor = System.Drawing.Color.Gray;
        _lblLastUpdate.Font = new System.Drawing.Font(Font.FontFamily, 8.5f);
        _lblLastUpdate.Text = "No data yet — connect and start capture";

        _lblRuntime.ForeColor = System.Drawing.Color.Gray;
        _lblRuntime.Font = new System.Drawing.Font(Font.FontFamily, 8.5f);
        _lblRuntime.Text = string.Empty;

        var advGroup = BuildAdvancedGroup();

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            BackColor = System.Drawing.Color.Transparent,
            Padding = new Padding(0),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 180f));

        layout.Controls.Add(_lblStationBig, 0, 0);
        layout.Controls.Add(_lblBatteryId, 0, 1);
        layout.Controls.Add(statusRow, 0, 2);
        layout.Controls.Add(metricsFlow, 0, 3);
        layout.Controls.Add(_lblRuntime, 0, 4);
        layout.Controls.Add(_lblLastUpdate, 0, 5);
        layout.Controls.Add(advGroup, 0, 6);

        panel.Controls.Add(layout);
        return panel;
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

        AddPair(table, "Station:", _lblStation, "Last Update:", _lblLastUpdateAdv);
        AddPair(table, "Date:", _lblDate, "Time:", _lblTime);
        AddPair(table, "Event Code:", _lblEventCode, "Process Code:", _lblBatteryType);
        AddPair(table, "Health Current:", _lblHealthCurrent, "Health Prev:", _lblHealthPrev);
        AddPair(table, "Target Cap:", _lblTargetCap, "Resistance:", _lblResistance);

        group.Controls.Add(table);

        _advGroup = group;
        _advTable = table;

        group.Paint += (_, e) => ThemedGroupBoxPainter.Paint(group, _isDark, e);

        return group;
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
}

internal static class ThemedGroupBoxPainter
{
    public static void Paint(GroupBox groupBox, bool isDark, PaintEventArgs e)
    {
        if (!isDark)
            return;

        var g = e.Graphics;
        var textSize = g.MeasureString(groupBox.Text, groupBox.Font);
        int borderTop = (int)(textSize.Height / 2);

        using var bgBrush = new System.Drawing.SolidBrush(groupBox.BackColor);
        g.FillRectangle(bgBrush, 0, borderTop, groupBox.Width, groupBox.Height - borderTop);
        g.FillRectangle(bgBrush, 0, 0, groupBox.Width, borderTop);

        using var pen = new System.Drawing.Pen(AppTheme.BorderColor(true));
        g.DrawRectangle(pen, new System.Drawing.Rectangle(0, borderTop, groupBox.Width - 1, groupBox.Height - borderTop - 1));

        const float textX = 9f;
        using var textBgBrush = new System.Drawing.SolidBrush(groupBox.BackColor);
        g.FillRectangle(textBgBrush, textX - 2, 0, textSize.Width + 4, textSize.Height);
        using var textBrush = new System.Drawing.SolidBrush(groupBox.ForeColor);
        g.DrawString(groupBox.Text, groupBox.Font, textBrush, textX, 0);
    }
}
