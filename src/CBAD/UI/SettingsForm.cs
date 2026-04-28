namespace CBAD.UI;

/// <summary>
/// Placeholder Settings dialog.  Future application settings (e.g., theme
/// preferences, data refresh options) will be added here over time.
/// </summary>
internal class SettingsForm : Form
{
    public SettingsForm()
    {
        Text            = "Settings";
        Width           = 420;
        Height          = 260;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterParent;

        BuildUi();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 1,
            RowCount    = 2,
            Padding     = new Padding(20),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        // ── Placeholder message ──────────────────────────────────────────
        var msgPanel = new Panel { Dock = DockStyle.Fill };

        var icon = new Label
        {
            Text      = "⚙",
            Font      = new Font("Segoe UI Emoji", 28f),
            AutoSize  = true,
            ForeColor = Color.FromArgb(90, 120, 170),
            Margin    = new Padding(0, 0, 0, 8),
        };

        var heading = new Label
        {
            Text      = "Settings",
            Font      = new Font("Segoe UI", 13f, FontStyle.Bold),
            AutoSize  = true,
            ForeColor = Color.FromArgb(40, 60, 100),
            Margin    = new Padding(0, 0, 0, 6),
        };

        var subtitle = new Label
        {
            Text      = "Additional settings will be available in a future update.",
            Font      = new Font("Segoe UI", 9f),
            AutoSize  = true,
            ForeColor = Color.Gray,
        };

        var contentFlow = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents  = false,
            AutoSize      = true,
            Padding       = new Padding(10, 20, 0, 0),
        };
        contentFlow.Controls.Add(icon);
        contentFlow.Controls.Add(heading);
        contentFlow.Controls.Add(subtitle);

        msgPanel.Controls.Add(contentFlow);
        root.Controls.Add(msgPanel, 0, 0);

        // ── OK button ────────────────────────────────────────────────────
        var btnOk = new Button
        {
            Text        = "OK",
            Width       = 80,
            Height      = 30,
            DialogResult = DialogResult.OK,
            Anchor      = AnchorStyles.Right,
        };
        btnOk.Click += (_, __) => Close();

        var btnFlow = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize      = true,
        };
        btnFlow.Controls.Add(btnOk);
        root.Controls.Add(btnFlow, 0, 1);
    }
}
