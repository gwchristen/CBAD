using System.IO.Ports;

namespace CBAD.UI;

/// <summary>Indicates what action the user took when closing the ConnectionSettingsForm.</summary>
internal enum ConnectionAction { None, Connect, Disconnect }

/// <summary>
/// Popup dialog that hosts all serial connection and output settings,
/// plus Connect / Disconnect buttons.  The main form opens this via
/// <c>ShowDialog</c> and inspects <see cref="Action"/> to decide what to do.
/// </summary>
internal class ConnectionSettingsForm : Form
{
    // ── Connection controls ──────────────────────────────────────────────
    private readonly ComboBox _cbPort        = new();
    private readonly ComboBox _cbBaud        = new();
    private readonly ComboBox _cbParity      = new();
    private readonly ComboBox _cbDataBits    = new();
    private readonly ComboBox _cbStopBits    = new();
    private readonly ComboBox _cbHandshake   = new();

    // ── Output controls ─────────────────────────────────────────────────
    private readonly TextBox       _txtOutDir       = new();
    private readonly TextBox       _txtPrefix       = new();
    private readonly CheckBox      _chkCsv          = new();
    private readonly CheckBox      _chkReconnect    = new();
    private readonly NumericUpDown _numReconnectMs  = new();
    private readonly CheckBox      _chkSimulation   = new();

    // ── Action buttons ───────────────────────────────────────────────────
    private readonly Button _btnRefreshPorts = new();
    private readonly Button _btnBrowse       = new();
    private readonly Button _btnConnect      = new();
    private readonly Button _btnDisconnect   = new();
    private readonly Button _btnClose        = new();

    /// <summary>The action the user chose before the dialog was closed.</summary>
    public ConnectionAction Action { get; private set; } = ConnectionAction.None;

    public ConnectionSettingsForm(AppOptions currentOptions, bool isRunning, bool simulationMode)
    {
        Text            = "Connection Settings";
        Width           = 640;
        Height          = 500;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterParent;

        BuildUi();
        LoadDefaults();
        PopulateFromOptions(currentOptions, simulationMode);
        RefreshPorts();

        // Apply running-state lock: when capture is active the user can only
        // view settings and disconnect; editing is disabled.
        SetControlsEnabled(!isRunning);
        _btnConnect.Enabled    = !isRunning;
        _btnDisconnect.Enabled = isRunning;
    }

    // ── Build UI ─────────────────────────────────────────────────────────
    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 1,
            RowCount    = 2,
            Padding     = new Padding(10),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        // ── Settings area ────────────────────────────────────────────────
        var settingsTable = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 2,
            RowCount    = 1,
            Padding     = new Padding(0),
        };
        settingsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        settingsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

        settingsTable.Controls.Add(BuildConnectionGroup(), 0, 0);
        settingsTable.Controls.Add(BuildOutputGroup(), 1, 0);
        root.Controls.Add(settingsTable, 0, 0);

        // ── Button strip ─────────────────────────────────────────────────
        var btnFlow = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize      = true,
            Padding       = new Padding(0, 6, 0, 0),
        };

        _btnClose.Text      = "Close";
        _btnClose.AutoSize  = true;
        _btnClose.Height    = 30;
        _btnClose.Margin    = new Padding(4, 0, 0, 0);
        _btnClose.Click    += (_, __) => { Action = ConnectionAction.None; Close(); };

        _btnDisconnect.Text      = "■  Disconnect";
        _btnDisconnect.Font      = new Font(SystemFonts.DefaultFont, FontStyle.Bold);
        _btnDisconnect.Size      = new Size(120, 30);
        _btnDisconnect.BackColor = Color.FromArgb(180, 40, 40);
        _btnDisconnect.ForeColor = Color.White;
        _btnDisconnect.FlatStyle = FlatStyle.Flat;
        _btnDisconnect.FlatAppearance.BorderColor = Color.FromArgb(120, 20, 20);
        _btnDisconnect.Margin    = new Padding(4, 0, 4, 0);
        _btnDisconnect.Click    += (_, __) => { Action = ConnectionAction.Disconnect; Close(); };

        _btnConnect.Text      = "▶  Connect";
        _btnConnect.Font      = new Font(SystemFonts.DefaultFont, FontStyle.Bold);
        _btnConnect.Size      = new Size(110, 30);
        _btnConnect.BackColor = Color.FromArgb(34, 139, 34);
        _btnConnect.ForeColor = Color.White;
        _btnConnect.FlatStyle = FlatStyle.Flat;
        _btnConnect.FlatAppearance.BorderColor = Color.FromArgb(20, 100, 20);
        _btnConnect.Margin    = new Padding(4, 0, 4, 0);
        _btnConnect.Click    += OnConnectClicked;

        btnFlow.Controls.Add(_btnClose);
        btnFlow.Controls.Add(_btnDisconnect);
        btnFlow.Controls.Add(_btnConnect);
        root.Controls.Add(btnFlow, 0, 1);
    }

    private GroupBox BuildConnectionGroup()
    {
        var group = new GroupBox
        {
            Text     = "Connection",
            Dock     = DockStyle.Fill,
            AutoSize = true,
            Padding  = new Padding(8, 2, 8, 8),
        };

        var grid = new TableLayoutPanel
        {
            Dock        = DockStyle.Top,
            AutoSize    = true,
            ColumnCount = 2,
            RowCount    = 8,
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        AddLabeled(grid, "Port",      _cbPort,      0, 0);
        AddLabeled(grid, "Baud Rate", _cbBaud,      1, 0);
        AddLabeled(grid, "Parity",    _cbParity,    0, 2);
        AddLabeled(grid, "Data Bits", _cbDataBits,  1, 2);
        AddLabeled(grid, "Stop Bits", _cbStopBits,  0, 4);
        AddLabeled(grid, "Handshake", _cbHandshake, 1, 4);

        _btnRefreshPorts.Text    = "🔄 Refresh Ports";
        _btnRefreshPorts.AutoSize = true;
        _btnRefreshPorts.Margin   = new Padding(3, 8, 3, 3);
        _btnRefreshPorts.Click   += (_, __) => RefreshPorts();
        grid.Controls.Add(_btnRefreshPorts, 0, 6);
        grid.SetColumnSpan(_btnRefreshPorts, 2);

        _chkSimulation.Text     = "Demo Mode (simulated data)";
        _chkSimulation.AutoSize = true;
        _chkSimulation.Margin   = new Padding(3, 8, 3, 3);
        _chkSimulation.CheckedChanged += OnSimulationCheckedChanged;
        grid.Controls.Add(_chkSimulation, 0, 7);
        grid.SetColumnSpan(_chkSimulation, 2);

        group.Controls.Add(grid);
        return group;
    }

    private GroupBox BuildOutputGroup()
    {
        var group = new GroupBox
        {
            Text     = "Output & Logging",
            Dock     = DockStyle.Fill,
            AutoSize = true,
            Padding  = new Padding(8, 2, 8, 8),
        };

        var grid = new TableLayoutPanel
        {
            Dock        = DockStyle.Top,
            AutoSize    = true,
            ColumnCount = 1,
            RowCount    = 6,
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Output folder row
        var outDirFlow = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            AutoSize      = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents  = false,
        };
        _txtOutDir.Width  = 140;
        _btnBrowse.Text   = "…";
        _btnBrowse.Width  = 30;
        _btnBrowse.Margin = new Padding(4, 0, 0, 0);
        _btnBrowse.Click += (_, __) => BrowseOutDir();
        outDirFlow.Controls.Add(_txtOutDir);
        outDirFlow.Controls.Add(_btnBrowse);

        AddLabeled(grid, "Output Folder", outDirFlow, 0, 0);
        AddLabeled(grid, "File Prefix",   _txtPrefix, 0, 2);

        _chkCsv.Text      = "CSV Output";
        _chkCsv.AutoSize  = true;
        _chkCsv.Margin    = new Padding(3, 8, 3, 3);
        _chkReconnect.Text    = "Auto-Reconnect";
        _chkReconnect.AutoSize = true;
        _chkReconnect.Margin   = new Padding(3, 8, 3, 3);
        grid.Controls.Add(_chkCsv, 0, 4);
        grid.Controls.Add(_chkReconnect, 0, 5);

        _numReconnectMs.Minimum   = 100;
        _numReconnectMs.Maximum   = 600000;
        _numReconnectMs.Increment = 100;
        _numReconnectMs.Width     = 80;

        var delayRow = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            AutoSize      = true,
            FlowDirection = FlowDirection.LeftToRight,
        };
        delayRow.Controls.Add(new Label { Text = "Reconnect delay (ms):", AutoSize = true, Margin = new Padding(3, 5, 4, 3) });
        delayRow.Controls.Add(_numReconnectMs);

        grid.Controls.Add(delayRow, 0, 6);

        group.Controls.Add(grid);
        return group;
    }

    // ── Defaults & population ─────────────────────────────────────────────
    private void LoadDefaults()
    {
        _cbBaud.Items.AddRange(new object[] { "1200", "2400", "4800", "9600", "19200", "38400", "57600", "115200" });
        _cbBaud.Text = "9600";

        _cbParity.Items.AddRange(Enum.GetNames(typeof(Parity)));
        _cbParity.Text = Parity.None.ToString();

        _cbDataBits.Items.AddRange(new object[] { "5", "6", "7", "8" });
        _cbDataBits.Text = "8";

        _cbStopBits.Items.AddRange(Enum.GetNames(typeof(StopBits)));
        _cbStopBits.Text = StopBits.One.ToString();

        _cbHandshake.Items.AddRange(Enum.GetNames(typeof(Handshake)));
        _cbHandshake.Text = Handshake.None.ToString();

        _txtOutDir.Text       = Path.Combine(AppContext.BaseDirectory, "logs");
        _txtPrefix.Text       = "cadex_raw";
        _chkCsv.Checked       = false;
        _chkReconnect.Checked = true;
        _numReconnectMs.Value = 2000;
    }

    private void PopulateFromOptions(AppOptions opts, bool simulationMode)
    {
        if (!string.IsNullOrWhiteSpace(opts.Port))
            _cbPort.Text = opts.Port;

        if (opts.Baud > 0)
            _cbBaud.Text = opts.Baud.ToString();

        _cbParity.Text    = opts.Parity.ToString();
        _cbDataBits.Text  = opts.DataBits.ToString();
        _cbStopBits.Text  = opts.StopBits.ToString();
        _cbHandshake.Text = opts.Handshake.ToString();

        if (!string.IsNullOrWhiteSpace(opts.OutDir))
            _txtOutDir.Text = opts.OutDir;

        if (!string.IsNullOrWhiteSpace(opts.Prefix))
            _txtPrefix.Text = opts.Prefix;

        _chkCsv.Checked       = opts.Csv;
        _chkReconnect.Checked = opts.Reconnect;

        if (opts.ReconnectDelayMs >= (int)_numReconnectMs.Minimum &&
            opts.ReconnectDelayMs <= (int)_numReconnectMs.Maximum)
            _numReconnectMs.Value = opts.ReconnectDelayMs;

        _chkSimulation.Checked = simulationMode;
    }

    // ── Event handlers ────────────────────────────────────────────────────
    private void OnConnectClicked(object? sender, EventArgs e)
    {
        try
        {
            // Validate before accepting
            _ = BuildOptions();
            Action = ConnectionAction.Connect;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void OnSimulationCheckedChanged(object? sender, EventArgs e)
    {
        bool sim = _chkSimulation.Checked;
        _cbPort.Enabled          = !sim;
        _cbBaud.Enabled          = !sim;
        _cbParity.Enabled        = !sim;
        _cbDataBits.Enabled      = !sim;
        _cbStopBits.Enabled      = !sim;
        _cbHandshake.Enabled     = !sim;
        _btnRefreshPorts.Enabled = !sim;
    }

    private void SetControlsEnabled(bool enabled)
    {
        bool notSimulation = enabled && !_chkSimulation.Checked;
        _cbPort.Enabled          = notSimulation;
        _cbBaud.Enabled          = notSimulation;
        _cbParity.Enabled        = notSimulation;
        _cbDataBits.Enabled      = notSimulation;
        _cbStopBits.Enabled      = notSimulation;
        _cbHandshake.Enabled     = notSimulation;
        _btnRefreshPorts.Enabled = notSimulation;
        _chkSimulation.Enabled   = enabled;
        _txtOutDir.Enabled       = enabled;
        _btnBrowse.Enabled       = enabled;
        _txtPrefix.Enabled       = enabled;
        _chkCsv.Enabled          = enabled;
        _chkReconnect.Enabled    = enabled;
        _numReconnectMs.Enabled  = enabled;
    }

    private void RefreshPorts()
    {
        var selected = _cbPort.Text;
        _cbPort.Items.Clear();

        var ports = SerialPort.GetPortNames()
                              .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                              .ToArray();
        _cbPort.Items.AddRange(ports);

        if (!string.IsNullOrWhiteSpace(selected) && ports.Contains(selected, StringComparer.OrdinalIgnoreCase))
            _cbPort.Text = selected;
        else if (ports.Length > 0)
            _cbPort.Text = ports[0];
        else
            _cbPort.Text = string.Empty;
    }

    private void BrowseOutDir()
    {
        using var dlg = new FolderBrowserDialog();
        dlg.SelectedPath = Directory.Exists(_txtOutDir.Text) ? _txtOutDir.Text : AppContext.BaseDirectory;
        if (dlg.ShowDialog(this) == DialogResult.OK)
            _txtOutDir.Text = dlg.SelectedPath;
    }

    // ── Public accessors ──────────────────────────────────────────────────

    /// <summary>Returns the validated <see cref="AppOptions"/> from the form's controls.</summary>
    public AppOptions GetOptions() => BuildOptions();

    /// <summary>Whether "Demo Mode" was checked.</summary>
    public bool SimulationMode => _chkSimulation.Checked;

    // ── Helpers ───────────────────────────────────────────────────────────
    private AppOptions BuildOptions()
    {
        if (!_chkSimulation.Checked && string.IsNullOrWhiteSpace(_cbPort.Text))
            throw new InvalidOperationException("Please select a COM port.");

        if (!int.TryParse(_cbBaud.Text, out var baud) || baud <= 0)
            throw new InvalidOperationException("Invalid baud rate.");

        if (!int.TryParse(_cbDataBits.Text, out var dataBits) || dataBits is < 5 or > 8)
            throw new InvalidOperationException("Data bits must be between 5 and 8.");

        if (!Enum.TryParse<Parity>(_cbParity.Text, true, out var parity))
            throw new InvalidOperationException("Invalid parity value.");

        if (!Enum.TryParse<StopBits>(_cbStopBits.Text, true, out var stopBits))
            throw new InvalidOperationException("Invalid stop bits value.");

        if (!Enum.TryParse<Handshake>(_cbHandshake.Text, true, out var handshake))
            throw new InvalidOperationException("Invalid handshake value.");

        var outDir = _txtOutDir.Text.Trim();
        if (string.IsNullOrWhiteSpace(outDir))
            throw new InvalidOperationException("Please select an output folder.");

        var prefix = _txtPrefix.Text.Trim();
        if (string.IsNullOrWhiteSpace(prefix))
            prefix = "cadex_raw";

        return new AppOptions
        {
            Port             = _chkSimulation.Checked ? string.Empty : _cbPort.Text.Trim(),
            Baud             = baud,
            Parity           = parity,
            DataBits         = dataBits,
            StopBits         = stopBits,
            Handshake        = handshake,
            OutDir           = outDir,
            Prefix           = prefix,
            Csv              = _chkCsv.Checked,
            Reconnect        = _chkReconnect.Checked,
            ReconnectDelayMs = (int)_numReconnectMs.Value,
            ListPorts        = false,
        };
    }

    private static void AddLabeled(TableLayoutPanel grid, string labelText, Control control, int col, int row)
    {
        var lbl = new Label { Text = labelText, AutoSize = true, Margin = new Padding(3, 8, 3, 3) };
        grid.Controls.Add(lbl, col, row);
        control.Dock   = DockStyle.Top;
        control.Margin = new Padding(3, 3, 10, 8);
        grid.Controls.Add(control, col, row + 1);
    }
}
