using System.Drawing;
using System.IO.Ports;
using System.Text;
using CBAD.Simulation;
using CBAD.UI;

namespace CBAD;

internal class MainForm : Form
{
    // Serial connection controls
    private ComboBox cbPort = new();
    private ComboBox cbBaud = new();
    private ComboBox cbParity = new();
    private ComboBox cbDataBits = new();
    private ComboBox cbStopBits = new();
    private ComboBox cbHandshake = new();
    private TextBox txtOutDir = new();
    private TextBox txtPrefix = new();
    private CheckBox chkCsv = new();
    private CheckBox chkReconnect = new();
    private NumericUpDown numReconnectMs = new();
    private CheckBox chkSimulation = new();
    private Button btnBrowse = new();
    private Button btnRefreshPorts = new();
    private Button btnStart = new();
    private Button btnStop = new();

    // Status indicator controls
    private readonly Label _lblStatusDot    = new() { AutoSize = false, Width = 14, Height = 14, Margin = new Padding(0, 2, 8, 0) };
    private readonly Label _lblStatusState  = new() { AutoSize = true, Margin = new Padding(0, 0, 10, 0) };
    private readonly Label _lblStatusDetail = new() { AutoSize = true, Margin = new Padding(0, 0, 3, 0) };
    private CaptureLifecycleState _lifecycleState = CaptureLifecycleState.Idle;

    // Settings panel toggle
    private Panel _settingsPanel = null!;
    private Button _btnToggleSettings = new();
    private bool _settingsExpanded = true;

    // Dark mode toggle
    private Button _btnDarkMode = new();
    private bool _isDarkMode = false;

    private CancellationTokenSource? _cts;
    private Task? _captureTask;
    private ILineSink? _sink;

    // Station state manager — holds ring-buffer history; owns parse + update logic.
    private readonly StationStateManager _stateManager = new();

    // Tab UI references
    private OverviewTab _overviewTab = null!;
    private StationDetailTab[] _detailTabs = null!;

    // Header color shared between header and settings border
    private static readonly Color HeaderColor = Color.FromArgb(30, 46, 78);

    public MainForm(AppOptions? startupDefaults = null)
    {
        Text = "CBAD — Battery Analyzer Dashboard";
        Width = 1200;
        Height = 800;
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;

        BuildUi();
        LoadDefaults();

        // Wire station-state events after UI controls exist.
        _stateManager.StationUpdated += state =>
        {
            _overviewTab.UpdateStation(state);
            _detailTabs[state.Station - 1].UpdateStation(state);
        };
        _stateManager.ParseFailed += line =>
            AppLog.Warn($"Parse failed: {line}");

        if (startupDefaults is not null)
            ApplyStartupDefaults(startupDefaults);

        RefreshPorts();
        SetRunningState(false);
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        // ── Top panel ──────────────────────────────────────────────────
        var topPanel = new Panel { Dock = DockStyle.Top, AutoSize = true };

        // Settings panel added first → sits below header (last-added = topmost with DockStyle.Top)
        _settingsPanel = BuildSettingsPanel();
        topPanel.Controls.Add(_settingsPanel);

        // Header strip added last → sits at the very top
        topPanel.Controls.Add(BuildHeaderStrip());

        root.Controls.Add(topPanel);

        // ── Tab control ─────────────────────────────────────────────────
        var tabs = new TabControl { Dock = DockStyle.Fill };

        _overviewTab = new OverviewTab();
        var tabOverview = new TabPage("Overview");
        tabOverview.Controls.Add(_overviewTab);
        tabs.TabPages.Add(tabOverview);

        _detailTabs = new StationDetailTab[4];
        for (int i = 0; i < 4; i++)
        {
            _detailTabs[i] = new StationDetailTab(i + 1);
            var tp = new TabPage($"Station {i + 1}");
            tp.Controls.Add(_detailTabs[i]);
            tabs.TabPages.Add(tp);
        }

        root.Controls.Add(tabs);
    }

    // ── Header strip: title | status | actions ──────────────────────────
    private Panel BuildHeaderStrip()
    {
        var strip = new Panel
        {
            Dock = DockStyle.Top,
            Height = 58,
            BackColor = HeaderColor,
            Padding = new Padding(0),
        };

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
            Padding = new Padding(10, 0, 8, 0),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        // Column 0: app title
        var titleLabel = new Label
        {
            Text = "CBAD  Battery Analyzer",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 0, 20, 0),
        };

        // Column 1: live status
        _lblStatusState.Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold);
        _lblStatusState.ForeColor = Color.White;
        _lblStatusDetail.ForeColor = Color.FromArgb(180, 220, 255);

        var statusLabel = new Label
        {
            Text = "Status:",
            AutoSize = true,
            Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
            ForeColor = Color.FromArgb(160, 190, 225),
            Margin = new Padding(0, 0, 8, 0),
        };

        var statusFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 15, 0, 0),
        };
        statusFlow.Controls.Add(statusLabel);
        statusFlow.Controls.Add(_lblStatusDot);
        statusFlow.Controls.Add(_lblStatusState);
        statusFlow.Controls.Add(_lblStatusDetail);

        // Column 2: action controls (right-aligned)
        var toolTip = new ToolTip();
        toolTip.SetToolTip(chkSimulation, "Run with simulated Cadex data — no hardware required");

        chkSimulation.Text = "Demo Mode";
        chkSimulation.AutoSize = true;
        chkSimulation.ForeColor = Color.White;
        chkSimulation.BackColor = Color.Transparent;
        chkSimulation.Margin = new Padding(4, 16, 8, 4);
        chkSimulation.CheckedChanged += OnSimulationCheckedChanged;

        _btnDarkMode.Text = "🌙 Dark";
        _btnDarkMode.AutoSize = true;
        _btnDarkMode.Height = 28;
        _btnDarkMode.FlatStyle = FlatStyle.Flat;
        _btnDarkMode.ForeColor = Color.White;
        _btnDarkMode.BackColor = Color.FromArgb(55, 70, 100);
        _btnDarkMode.FlatAppearance.BorderColor = Color.FromArgb(90, 110, 155);
        _btnDarkMode.Margin = new Padding(4, 12, 4, 12);
        _btnDarkMode.Click += (_, __) => ApplyTheme(!_isDarkMode);

        _btnToggleSettings.Text = "⚙ Settings ▾";
        _btnToggleSettings.AutoSize = true;
        _btnToggleSettings.Height = 28;
        _btnToggleSettings.FlatStyle = FlatStyle.Flat;
        _btnToggleSettings.ForeColor = Color.White;
        _btnToggleSettings.BackColor = Color.FromArgb(60, 90, 130);
        _btnToggleSettings.FlatAppearance.BorderColor = Color.FromArgb(90, 130, 180);
        _btnToggleSettings.Margin = new Padding(4, 12, 8, 12);
        _btnToggleSettings.Click += (_, __) => ToggleSettings();

        btnStart.Text = "▶  Start";
        btnStart.Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold);
        btnStart.Size = new Size(96, 32);
        btnStart.BackColor = Color.FromArgb(34, 139, 34);
        btnStart.ForeColor = Color.White;
        btnStart.FlatStyle = FlatStyle.Flat;
        btnStart.FlatAppearance.BorderColor = Color.FromArgb(20, 100, 20);
        btnStart.Margin = new Padding(4, 10, 4, 10);
        btnStart.Click += async (_, __) => await StartCaptureAsync();

        btnStop.Text = "■  Stop";
        btnStop.Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold);
        btnStop.Size = new Size(96, 32);
        btnStop.BackColor = Color.FromArgb(180, 40, 40);
        btnStop.ForeColor = Color.White;
        btnStop.FlatStyle = FlatStyle.Flat;
        btnStop.FlatAppearance.BorderColor = Color.FromArgb(120, 20, 20);
        btnStop.Margin = new Padding(4, 10, 6, 10);
        btnStop.Click += async (_, __) => await StopCaptureAsync();

        var actionsFlow = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
        };
        actionsFlow.Controls.Add(chkSimulation);
        actionsFlow.Controls.Add(_btnDarkMode);
        actionsFlow.Controls.Add(_btnToggleSettings);
        actionsFlow.Controls.Add(btnStart);
        actionsFlow.Controls.Add(btnStop);

        table.Controls.Add(titleLabel, 0, 0);
        table.Controls.Add(statusFlow, 1, 0);
        table.Controls.Add(actionsFlow, 2, 0);

        strip.Controls.Add(table);
        SetLifecycleState(CaptureLifecycleState.Idle);
        return strip;
    }

    // ── Settings panel: Connection | Output ─────────────────────────────
    private Panel BuildSettingsPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = Color.FromArgb(245, 247, 250),
            Padding = new Padding(8, 6, 8, 8),
        };

        var outerTable = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0),
        };
        outerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        outerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));

        // ── Left: Connection Settings ────────────────────────────────
        var connGroup = new GroupBox
        {
            Text = "Connection Settings",
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(8, 2, 8, 8),
        };

        var connGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 4,
            RowCount = 4,
        };
        for (int i = 0; i < 4; i++) connGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        AddLabeled(connGrid, "Port", cbPort, 0, 0);
        AddLabeled(connGrid, "Baud", cbBaud, 1, 0);
        AddLabeled(connGrid, "Parity", cbParity, 2, 0);
        AddLabeled(connGrid, "Data Bits", cbDataBits, 3, 0);
        AddLabeled(connGrid, "Stop Bits", cbStopBits, 0, 2);
        AddLabeled(connGrid, "Handshake", cbHandshake, 1, 2);

        btnRefreshPorts.Text = "Refresh Ports";
        btnRefreshPorts.AutoSize = true;
        btnRefreshPorts.Margin = new Padding(3, 8, 10, 3);
        btnRefreshPorts.Click += (_, __) => RefreshPorts();
        connGrid.Controls.Add(new Label { Text = " ", AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, 2, 2);
        connGrid.Controls.Add(btnRefreshPorts, 2, 3);

        connGroup.Controls.Add(connGrid);

        // ── Right: Output & Logging ─────────────────────────────────
        var outGroup = new GroupBox
        {
            Text = "Output & Logging",
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(8, 2, 8, 8),
        };

        var outGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 4,
        };
        outGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        outGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

        var outDirPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };
        txtOutDir.Width = 200;
        btnBrowse.Text = "Browse…";
        btnBrowse.AutoSize = true;
        btnBrowse.Margin = new Padding(4, 0, 0, 0);
        btnBrowse.Click += (_, __) => BrowseOutDir();
        outDirPanel.Controls.Add(txtOutDir);
        outDirPanel.Controls.Add(btnBrowse);

        AddLabeled(outGrid, "Output Folder", outDirPanel, 0, 0);
        AddLabeled(outGrid, "File Prefix", txtPrefix, 1, 0);

        chkCsv.Text = "CSV Output";
        chkCsv.AutoSize = true;
        chkReconnect.Text = "Auto-Reconnect";
        chkReconnect.AutoSize = true;
        numReconnectMs.Minimum = 100;
        numReconnectMs.Maximum = 600000;
        numReconnectMs.Increment = 100;
        numReconnectMs.Width = 80;

        var flagsRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
        };
        flagsRow.Controls.Add(chkCsv);
        flagsRow.Controls.Add(chkReconnect);
        flagsRow.Controls.Add(new Label { Text = "Delay (ms):", AutoSize = true, Margin = new Padding(12, 5, 4, 3) });
        flagsRow.Controls.Add(numReconnectMs);
        outGrid.Controls.Add(flagsRow, 0, 2);
        outGrid.SetColumnSpan(flagsRow, 2);

        outGroup.Controls.Add(outGrid);

        outerTable.Controls.Add(connGroup, 0, 0);
        outerTable.Controls.Add(outGroup, 1, 0);
        panel.Controls.Add(outerTable);
        return panel;
    }

    private void ToggleSettings()
    {
        _settingsExpanded = !_settingsExpanded;
        _settingsPanel.Visible = _settingsExpanded;
        _btnToggleSettings.Text = _settingsExpanded ? "⚙ Settings ▾" : "⚙ Settings ▸";
    }

    // ── Dark mode theming ────────────────────────────────────────────────
    // Applies a coherent dark or light theme to the main UI surfaces.
    // Note: native WinForms controls (ComboBox drop-down list, scrollbars,
    // TabControl tabs) cannot be fully themed without owner-draw overrides,
    // which is out of scope for this focused PR.
    private void ApplyTheme(bool isDark)
    {
        _isDarkMode = isDark;
        _btnDarkMode.Text = isDark ? "☀ Light" : "🌙 Dark";

        var formBg  = isDark ? AppTheme.DarkFormBg  : AppTheme.LightFormBg;
        var panelBg = AppTheme.PanelBg(isDark);
        var inputBg = AppTheme.InputBg(isDark);
        var inputFg = AppTheme.InputFg(isDark);
        var labelFg = AppTheme.LabelFg(isDark);
        var mutedFg = AppTheme.MutedFg(isDark);

        BackColor = formBg;
        _settingsPanel.BackColor = panelBg;
        ApplyThemeToChildren(_settingsPanel, panelBg, inputBg, inputFg, labelFg);

        foreach (var tab in _detailTabs)
            tab.ApplyTheme(isDark);
        _overviewTab.ApplyTheme(isDark);
    }

    private static void ApplyThemeToChildren(
        Control parent,
        Color panelBg,
        Color inputBg,
        Color inputFg,
        Color labelFg)
    {
        foreach (Control child in parent.Controls)
        {
            switch (child)
            {
                case ComboBox cb:
                    cb.BackColor = inputBg;
                    cb.ForeColor = inputFg;
                    break;
                case TextBox tb:
                    tb.BackColor = inputBg;
                    tb.ForeColor = inputFg;
                    break;
                case NumericUpDown nud:
                    nud.BackColor = inputBg;
                    nud.ForeColor = inputFg;
                    break;
                case CheckBox chk:
                    chk.ForeColor = labelFg;
                    break;
                case GroupBox gb:
                    gb.ForeColor = labelFg;
                    gb.BackColor = panelBg;
                    break;
                case Label lbl:
                    lbl.ForeColor = labelFg;
                    break;
                case TableLayoutPanel tlp:
                    tlp.BackColor = panelBg;
                    break;
                case FlowLayoutPanel flp:
                    flp.BackColor = panelBg;
                    break;
                case Panel pnl:
                    pnl.BackColor = panelBg;
                    break;
            }

            if (child.HasChildren)
                ApplyThemeToChildren(child, panelBg, inputBg, inputFg, labelFg);
        }
    }

    private static void AddLabeled(TableLayoutPanel grid, string label, Control control, int col, int row)
    {
        var lbl = new Label { Text = label, AutoSize = true, Margin = new Padding(3, 8, 3, 3) };
        grid.Controls.Add(lbl, col, row);
        control.Dock = DockStyle.Top;
        control.Margin = new Padding(3, 3, 10, 8);
        grid.Controls.Add(control, col, row + 1);
    }

    private void SetLifecycleState(CaptureLifecycleState state, string? detail = null)
    {
        if (InvokeRequired) { BeginInvoke(() => SetLifecycleState(state, detail)); return; }

        _lifecycleState = state;
        (_lblStatusDot.BackColor, _lblStatusState.Text) = state switch
        {
            CaptureLifecycleState.Idle     => (Color.LightGray,  "Idle"),
            CaptureLifecycleState.Starting => (Color.Gold,       "Starting…"),
            CaptureLifecycleState.Running  => (Color.LimeGreen,  "Running"),
            CaptureLifecycleState.Stopping => (Color.Orange,     "Stopping…"),
            CaptureLifecycleState.Error    => (Color.Crimson,    "Error"),
            _                              => (Color.LightGray,  state.ToString()),
        };
        _lblStatusDetail.Text      = detail ?? string.Empty;
        _lblStatusDetail.ForeColor = state == CaptureLifecycleState.Error
            ? Color.FromArgb(255, 120, 120)
            : Color.FromArgb(180, 220, 255);
    }

    private void LoadDefaults()
    {
        cbBaud.Items.AddRange(new object[] { "1200", "2400", "4800", "9600", "19200", "38400", "57600", "115200" });
        cbBaud.Text = "9600";

        cbParity.Items.AddRange(Enum.GetNames(typeof(Parity)));
        cbParity.Text = Parity.None.ToString();

        cbDataBits.Items.AddRange(new object[] { "5", "6", "7", "8" });
        cbDataBits.Text = "8";

        cbStopBits.Items.AddRange(Enum.GetNames(typeof(StopBits)));
        cbStopBits.Text = StopBits.One.ToString();

        cbHandshake.Items.AddRange(Enum.GetNames(typeof(Handshake)));
        cbHandshake.Text = Handshake.None.ToString();

        txtOutDir.Text = Path.Combine(AppContext.BaseDirectory, "logs");
        txtPrefix.Text = "cadex_raw";
        chkCsv.Checked = false;
        chkReconnect.Checked = true;
        numReconnectMs.Value = 2000;
    }

    private void RefreshPorts()
    {
        var selected = cbPort.Text;
        cbPort.Items.Clear();

        var ports = SerialPort.GetPortNames().OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
        cbPort.Items.AddRange(ports);

        if (!string.IsNullOrWhiteSpace(selected) && ports.Contains(selected, StringComparer.OrdinalIgnoreCase))
            cbPort.Text = selected;
        else if (ports.Length > 0)
            cbPort.Text = ports[0];
        else
            cbPort.Text = string.Empty;
    }

    private void BrowseOutDir()
    {
        using var dlg = new FolderBrowserDialog();
        dlg.SelectedPath = Directory.Exists(txtOutDir.Text) ? txtOutDir.Text : AppContext.BaseDirectory;
        if (dlg.ShowDialog(this) == DialogResult.OK)
            txtOutDir.Text = dlg.SelectedPath;
    }

    private AppOptions BuildOptionsFromUi()
    {
        if (string.IsNullOrWhiteSpace(cbPort.Text))
            throw new InvalidOperationException("Please select a COM port.");

        if (!int.TryParse(cbBaud.Text, out var baud) || baud <= 0)
            throw new InvalidOperationException("Invalid baud rate.");

        if (!int.TryParse(cbDataBits.Text, out var dataBits) || dataBits is < 5 or > 8)
            throw new InvalidOperationException("Data bits must be between 5 and 8.");

        if (!Enum.TryParse<Parity>(cbParity.Text, true, out var parity))
            throw new InvalidOperationException("Invalid parity value.");

        if (!Enum.TryParse<StopBits>(cbStopBits.Text, true, out var stopBits))
            throw new InvalidOperationException("Invalid stop bits value.");

        if (!Enum.TryParse<Handshake>(cbHandshake.Text, true, out var handshake))
            throw new InvalidOperationException("Invalid handshake value.");

        var outDir = txtOutDir.Text.Trim();
        if (string.IsNullOrWhiteSpace(outDir))
            throw new InvalidOperationException("Please select an output folder.");

        var prefix = txtPrefix.Text.Trim();
        if (string.IsNullOrWhiteSpace(prefix))
            prefix = "cadex_raw";

        return new AppOptions
        {
            Port = cbPort.Text.Trim(),
            Baud = baud,
            Parity = parity,
            DataBits = dataBits,
            StopBits = stopBits,
            Handshake = handshake,
            OutDir = outDir,
            Prefix = prefix,
            Csv = chkCsv.Checked,
            Reconnect = chkReconnect.Checked,
            ReconnectDelayMs = (int)numReconnectMs.Value,
            ListPorts = false
        };
    }

    private async Task StartCaptureAsync()
    {
        if (_captureTask is not null)
            return;

        SetRunningState(true);
        SetLifecycleState(CaptureLifecycleState.Starting);

        try
        {
            _sink = CreateSink();

            _cts = new CancellationTokenSource();

            if (chkSimulation.Checked)
            {
                AppLog.Info("Starting simulation mode");
                var sim = new SimulationService(
                    onRawLine: OnRawLine,
                    onStatus: SetStatus,
                    onData: null);

                _captureTask = Task.Run(() => sim.RunAsync(_cts.Token));
            }
            else
            {
                var options = BuildOptionsFromUi();
                AppLog.Info($"Starting capture on {options.Port} @ {options.Baud} baud");
                var service = new SerialCaptureService(
                    options,
                    _sink,
                    onData: null,
                    onStatus: SetStatus,
                    onRawLine: OnRawLine);

                _captureTask = Task.Run(() => service.RunAsync(_cts.Token));
            }

            SetLifecycleState(CaptureLifecycleState.Running, $"Logging to: {_sink.Path}");

            await Task.Yield();
        }
        catch (Exception ex)
        {
            AppLog.Error("Cannot start capture", ex);
            SetLifecycleState(CaptureLifecycleState.Error, ex.Message);
            MessageBox.Show(this, ex.Message, "Cannot start capture", MessageBoxButtons.OK, MessageBoxIcon.Error);
            await StopCaptureAsync();
        }
    }

    private ILineSink CreateSink()
    {
        var outDir = txtOutDir.Text.Trim();
        if (string.IsNullOrWhiteSpace(outDir))
            throw new InvalidOperationException("Please select an output folder.");

        var prefix = txtPrefix.Text.Trim();
        if (string.IsNullOrWhiteSpace(prefix))
            prefix = "cadex_raw";

        return chkCsv.Checked
            ? new CsvLineSink(outDir, prefix)
            : new RawLineSink(outDir, prefix);
    }

    private void OnRawLine(string line)
    {
        // All state mutations and UI updates happen on the UI thread to avoid data races.
        BeginInvoke(() => _stateManager.ProcessLine(line));
    }

    private void OnSimulationCheckedChanged(object? sender, EventArgs e)
    {
        bool sim = chkSimulation.Checked;
        cbPort.Enabled          = !sim;
        cbBaud.Enabled          = !sim;
        cbParity.Enabled        = !sim;
        cbDataBits.Enabled      = !sim;
        cbStopBits.Enabled      = !sim;
        cbHandshake.Enabled     = !sim;
        btnRefreshPorts.Enabled = !sim;
    }

    private async Task StopCaptureAsync()
    {
        var cts = _cts;
        var captureTask = _captureTask;
        var sink = _sink;

        // Clear fields first to prevent re-entry.
        _cts = null;
        _captureTask = null;
        _sink = null;

        // Show Stopping state only if we weren't already in an error state.
        if (_lifecycleState != CaptureLifecycleState.Error)
            SetLifecycleState(CaptureLifecycleState.Stopping);

        try { cts?.Cancel(); }
        catch (Exception ex)
        {
            AppLog.Error("CancellationTokenSource.Cancel error", ex);
            System.Diagnostics.Debug.WriteLine($"Cancel error: {ex.Message}");
        }
        cts?.Dispose();

        if (captureTask is not null)
        {
            try { await captureTask; }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                AppLog.Error("Capture task stopped with error", ex);
                System.Diagnostics.Debug.WriteLine($"Capture task stopped with error: {ex.Message}");
            }
        }

        sink?.Dispose();

        AppLog.Info("Capture stopped");
        SetRunningState(false);

        // Preserve error state visibility; only reset to Idle on a clean stop.
        if (_lifecycleState != CaptureLifecycleState.Error)
            SetLifecycleState(CaptureLifecycleState.Idle, "Stopped");
    }

    private void SetRunningState(bool running)
    {
        btnStart.Enabled = !running;
        btnStop.Enabled = running;
        chkSimulation.Enabled = !running;
        btnRefreshPorts.Enabled = !running && !chkSimulation.Checked;
        cbPort.Enabled = !running && !chkSimulation.Checked;
        cbBaud.Enabled = !running && !chkSimulation.Checked;
        cbParity.Enabled = !running && !chkSimulation.Checked;
        cbDataBits.Enabled = !running && !chkSimulation.Checked;
        cbStopBits.Enabled = !running && !chkSimulation.Checked;
        cbHandshake.Enabled = !running && !chkSimulation.Checked;
        txtOutDir.Enabled = !running;
        btnBrowse.Enabled = !running;
        txtPrefix.Enabled = !running;
        chkCsv.Enabled = !running;
        chkReconnect.Enabled = !running;
        numReconnectMs.Enabled = !running;
    }

    /// <summary>
    /// Updates the status detail message without changing the lifecycle state badge.
    /// Called by serial/simulation services to report transient messages (e.g.,
    /// "Connecting…", "Reconnecting in 2000ms") while the lifecycle state badge
    /// remains authoritative for the overall connection state color.
    /// </summary>
    private void SetStatus(string status)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(SetStatus), status);
            return;
        }

        _lblStatusDetail.Text = status;
    }

    private void ApplyStartupDefaults(AppOptions opts)
    {
        if (!string.IsNullOrWhiteSpace(opts.Port))
            cbPort.Text = opts.Port;

        if (opts.Baud > 0)
            cbBaud.Text = opts.Baud.ToString();

        cbParity.Text    = opts.Parity.ToString();
        cbDataBits.Text  = opts.DataBits.ToString();
        cbStopBits.Text  = opts.StopBits.ToString();
        cbHandshake.Text = opts.Handshake.ToString();

        if (!string.IsNullOrWhiteSpace(opts.OutDir))
            txtOutDir.Text = opts.OutDir;

        if (!string.IsNullOrWhiteSpace(opts.Prefix))
            txtPrefix.Text = opts.Prefix;

        chkCsv.Checked       = opts.Csv;
        chkReconnect.Checked = opts.Reconnect;

        if (opts.ReconnectDelayMs >= (int)numReconnectMs.Minimum &&
            opts.ReconnectDelayMs <= (int)numReconnectMs.Maximum)
            numReconnectMs.Value = opts.ReconnectDelayMs;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_captureTask is not null)
        {
            // Defer close until the capture task has fully stopped to avoid
            // disposing the sink while the background task may still be writing.
            e.Cancel = true;
            _ = StopCaptureAsync().ContinueWith(
                t =>
                {
                    if (t.IsFaulted)
                        System.Diagnostics.Debug.WriteLine($"StopCaptureAsync error during close: {t.Exception}");
                    Invoke(Close);
                },
                TaskScheduler.Default);
            return;
        }
        base.OnFormClosing(e);
    }
}
