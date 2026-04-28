using System.Drawing;
using System.IO.Ports;
using CBAD.Simulation;
using CBAD.UI;
using CBAD.WebServer;

namespace CBAD;

internal class MainForm : Form
{
    // ── Connection settings (backing state — UI lives in ConnectionSettingsForm) ──
    private AppOptions _currentOptions = new AppOptions
    {
        Baud             = 9600,
        Parity           = Parity.None,
        DataBits         = 8,
        StopBits         = StopBits.One,
        Handshake        = Handshake.None,
        OutDir           = Path.Combine(AppContext.BaseDirectory, "logs"),
        Prefix           = "cadex_raw",
        Csv              = false,
        Reconnect        = true,
        ReconnectDelayMs = 2000,
    };
    private bool _simulationMode = false;

    // Status indicator controls (header)
    private readonly Label _lblStatusDot    = new() { AutoSize = false, Width = 14, Height = 14, Margin = new Padding(0, 2, 8, 0) };
    private readonly Label _lblStatusState  = new() { AutoSize = true, Margin = new Padding(0, 0, 10, 0) };
    private readonly Label _lblStatusDetail = new() { AutoSize = true, Margin = new Padding(0, 0, 3, 0) };
    private readonly Label _lblWebUrl       = new() { AutoSize = true, Margin = new Padding(16, 0, 3, 0) };
    private CaptureLifecycleState _lifecycleState = CaptureLifecycleState.Idle;

    // Global metadata fields (left panel)
    private readonly TextBox _txtOperatorId      = new() { PlaceholderText = "Tech / Operator ID" };
    private readonly TextBox _txtAnalyzerModel   = new() { Text = "Cadex C7x00", PlaceholderText = "e.g. Cadex C7x00" };
    private readonly TextBox _txtAnalyzerSerial  = new() { PlaceholderText = "Analyzer serial #" };
    private Panel? _leftPanel;
    private GroupBox? _globalMetadataGroup;
    private TableLayoutPanel? _globalMetadataLayout;

    // Status strip (bottom of form)
    private ToolStripStatusLabel _statusStripLabel = null!;

    // Dark mode toggle
    private Button _btnDarkMode = new();
    private bool _isDarkMode = false;

    // Quick-access toolbar controls
    private readonly ComboBox _cbQuickPort = new();
    private readonly Button   _btnStart    = new();
    private readonly Button   _btnStop     = new();

    private CancellationTokenSource? _cts;
    private Task? _captureTask;
    private ILineSink? _sink;

    // Station state manager — holds ring-buffer history; owns parse + update logic.
    private readonly StationStateManager _stateManager = new();

    // Embedded web dashboard server for remote monitoring.
    private readonly DashboardServer _dashboardServer;

    // Tab UI references
    private OverviewTab _overviewTab = null!;
    private StationDetailTab[] _detailTabs = null!;

    // Header color shared between header and settings border
    private static readonly Color HeaderColor = Color.FromArgb(30, 46, 78);

    public MainForm(AppOptions? startupDefaults = null)
    {
        Text = "CBAD — Battery Analyzer Dashboard";
        Width = 1400;
        Height = 950;
        MinimumSize = new Size(1100, 750);
        StartPosition = FormStartPosition.CenterScreen;

        _dashboardServer = new DashboardServer(_stateManager);

        BuildUi();

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

        SetLifecycleState(CaptureLifecycleState.Idle);

        // Start the embedded web dashboard and update the URL label.
        try
        {
            _dashboardServer.Start();
            var url = _dashboardServer.GetLocalUrl();
            _lblWebUrl.Text      = $"🌐 {url}";
            _lblWebUrl.ForeColor = Color.FromArgb(120, 200, 255);
            AppLog.Info($"Web dashboard listening on {url}");
        }
        catch (Exception ex)
        {
            _lblWebUrl.Text      = "🌐 Dashboard unavailable";
            _lblWebUrl.ForeColor = Color.FromArgb(255, 120, 120);
            AppLog.Warn($"Could not start web dashboard: {ex.Message}");
        }
    }

    private void BuildUi()
    {
        // ── Status strip (bottom) — added first so it claims the bottom edge ──
        var statusStrip = new StatusStrip();
        _statusStripLabel = new ToolStripStatusLabel("🔴 Disconnected")
        {
            Spring    = true,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        statusStrip.Items.Add(_statusStripLabel);
        Controls.Add(statusStrip);

        // ── Menu strip (top) ─────────────────────────────────────────────
        var menuStrip = new MenuStrip();

        var connectionsMenu = new ToolStripMenuItem("Connections");
        connectionsMenu.Click += (_, __) => OpenConnectionSettings();

        var settingsMenu = new ToolStripMenuItem("Settings");
        settingsMenu.Click += (_, __) => new SettingsForm().ShowDialog(this);

        var recordsMenu = new ToolStripMenuItem("Records");
        recordsMenu.Click += (_, __) =>
            MessageBox.Show(this,
                "Records and database integration coming soon.",
                "Records",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

        menuStrip.Items.Add(connectionsMenu);
        menuStrip.Items.Add(settingsMenu);
        menuStrip.Items.Add(recordsMenu);
        Controls.Add(menuStrip);
        MainMenuStrip = menuStrip;

        // ── Header strip (title/status row + quick-access toolbar row) ──────
        Controls.Add(BuildHeaderStrip());

        // ── Left panel — global metadata (fixed 250px, docked Left) ─────
        _leftPanel = BuildGlobalMetadataPanel();
        Controls.Add(_leftPanel);

        // ── Tab control — fills all remaining space (right of left panel) ──
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

        Controls.Add(tabs);
    }

    // ── Header strip: title/status row + quick-access toolbar row ────────
    // Two-row panel so the form has exactly 5 top-level docked controls in
    // the correct order: StatusStrip(Bottom) → MenuStrip(Top) →
    // HeaderStrip(Top) → LeftPanel(Left) → TabControl(Fill).
    private Panel BuildHeaderStrip()
    {
        // Outer panel contains two horizontal bands stacked vertically:
        // Row 0 (top, 58 px)  — app title | live status | dark-mode toggle
        // Row 1 (bottom, 36 px) — COM port | refresh | Start | Stop
        var strip = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 94,           // 58 (title row) + 36 (toolbar row)
            BackColor = HeaderColor,
            Padding   = new Padding(0),
        };

        // ── Row 0: title | live status | actions ────────────────────────
        var titleRow = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 58,
            BackColor = Color.Transparent,
        };

        var table = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 3,
            RowCount    = 1,
            BackColor   = Color.Transparent,
            Padding     = new Padding(10, 0, 8, 0),
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        // Column 0: app title
        var titleLabel = new Label
        {
            Text      = "CBAD  Battery Analyzer",
            Font      = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize  = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin    = new Padding(0, 0, 20, 0),
        };

        // Column 1: live status
        _lblStatusState.Font     = new Font(SystemFonts.DefaultFont, FontStyle.Bold);
        _lblStatusState.ForeColor = Color.White;
        _lblStatusDetail.ForeColor = Color.FromArgb(180, 220, 255);

        var statusLabel = new Label
        {
            Text      = "Status:",
            AutoSize  = true,
            Font      = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
            ForeColor = Color.FromArgb(160, 190, 225),
            Margin    = new Padding(0, 0, 8, 0),
        };

        var statusFlow = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents  = false,
            BackColor     = Color.Transparent,
            Padding       = new Padding(0, 15, 0, 0),
        };
        statusFlow.Controls.Add(statusLabel);
        statusFlow.Controls.Add(_lblStatusDot);
        statusFlow.Controls.Add(_lblStatusState);
        statusFlow.Controls.Add(_lblStatusDetail);
        statusFlow.Controls.Add(_lblWebUrl);

        // Column 2: dark-mode toggle
        _btnDarkMode.Text      = "🌙 Dark";
        _btnDarkMode.AutoSize  = true;
        _btnDarkMode.Height    = 28;
        _btnDarkMode.FlatStyle = FlatStyle.Flat;
        _btnDarkMode.ForeColor = Color.White;
        _btnDarkMode.BackColor = Color.FromArgb(55, 70, 100);
        _btnDarkMode.FlatAppearance.BorderColor = Color.FromArgb(90, 110, 155);
        _btnDarkMode.Margin    = new Padding(4, 12, 4, 12);
        _btnDarkMode.Click    += (_, __) => ApplyTheme(!_isDarkMode);

        var actionsFlow = new FlowLayoutPanel
        {
            AutoSize      = true,
            Dock          = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents  = false,
            BackColor     = Color.Transparent,
        };
        actionsFlow.Controls.Add(_btnDarkMode);

        table.Controls.Add(titleLabel, 0, 0);
        table.Controls.Add(statusFlow, 1, 0);
        table.Controls.Add(actionsFlow, 2, 0);

        titleRow.Controls.Add(table);

        // ── Row 1: quick-access toolbar (COM port | Refresh | Start | Stop) ─
        var toolbarRow = new Panel
        {
            Dock      = DockStyle.Bottom,
            Height    = 36,
            BackColor = Color.FromArgb(45, 58, 82),
            Padding   = new Padding(6, 0, 6, 0),
        };

        var toolFlow = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents  = false,
            BackColor     = Color.Transparent,
        };

        var portLabel = new Label
        {
            Text      = "COM Port:",
            AutoSize  = true,
            ForeColor = Color.White,
            Margin    = new Padding(4, 8, 4, 0),
        };

        _cbQuickPort.Width         = 100;
        _cbQuickPort.DropDownStyle = ComboBoxStyle.DropDownList;
        _cbQuickPort.Margin        = new Padding(0, 5, 2, 0);
        RefreshQuickPorts();

        var btnRefresh = new Button
        {
            Text      = "🔄",
            Width     = 32,
            Height    = 26,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(55, 72, 100),
            Margin    = new Padding(0, 4, 12, 0),
        };
        btnRefresh.FlatAppearance.BorderColor = Color.FromArgb(90, 110, 155);
        btnRefresh.Click += (_, __) => RefreshQuickPorts();

        _btnStart.Text      = "▶  Start";
        _btnStart.Font      = new Font(SystemFonts.DefaultFont, FontStyle.Bold);
        _btnStart.AutoSize  = true;
        _btnStart.Height    = 26;
        _btnStart.FlatStyle = FlatStyle.Flat;
        _btnStart.ForeColor = Color.White;
        _btnStart.BackColor = Color.FromArgb(34, 139, 34);
        _btnStart.FlatAppearance.BorderColor = Color.FromArgb(20, 100, 20);
        _btnStart.Margin    = new Padding(0, 4, 4, 0);
        _btnStart.Click    += OnQuickStartClicked;

        _btnStop.Text      = "■  Stop";
        _btnStop.Font      = new Font(SystemFonts.DefaultFont, FontStyle.Bold);
        _btnStop.AutoSize  = true;
        _btnStop.Height    = 26;
        _btnStop.FlatStyle = FlatStyle.Flat;
        _btnStop.ForeColor = Color.White;
        _btnStop.BackColor = Color.FromArgb(180, 40, 40);
        _btnStop.FlatAppearance.BorderColor = Color.FromArgb(120, 20, 20);
        _btnStop.Margin    = new Padding(0, 4, 0, 0);
        _btnStop.Click    += (_, __) =>
            _ = StopCaptureAsync().ContinueWith(
                t => AppLog.Error("StopCaptureAsync error", t.Exception?.InnerException ?? t.Exception ?? new Exception("Unknown error")),
                TaskContinuationOptions.OnlyOnFaulted);

        toolFlow.Controls.Add(portLabel);
        toolFlow.Controls.Add(_cbQuickPort);
        toolFlow.Controls.Add(btnRefresh);
        toolFlow.Controls.Add(_btnStart);
        toolFlow.Controls.Add(_btnStop);

        toolbarRow.Controls.Add(toolFlow);

        // Add toolbar row FIRST (Dock=Bottom) then title row (Dock=Top) so
        // both rows fill the 94px strip without overlap.
        strip.Controls.Add(toolbarRow);
        strip.Controls.Add(titleRow);

        return strip;
    }

    // ── Global Metadata left panel ───────────────────────────────────────
    private Panel BuildGlobalMetadataPanel()
    {
        var panel = new Panel
        {
            Dock      = DockStyle.Left,
            Width     = 250,
            BackColor = AppTheme.LightPanelBg,
            Padding   = new Padding(8, 8, 8, 8),
        };

        var group = new GroupBox
        {
            Text         = "Global Metadata",
            Dock         = DockStyle.Top,
            AutoSize     = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding      = new Padding(8, 16, 8, 8),
            Margin       = new Padding(0),
        };

        var layout = new TableLayoutPanel
        {
            ColumnCount  = 1,
            AutoSize     = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock         = DockStyle.Top,
            Padding      = new Padding(0),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        void AddRow(string labelText, TextBox txt)
        {
            var lbl = new Label
            {
                Text     = labelText,
                AutoSize = true,
                Font     = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
                Margin   = new Padding(0, 6, 0, 2),
            };
            txt.Dock   = DockStyle.Top;
            txt.Margin = new Padding(0, 0, 0, 4);
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(lbl);
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(txt);
        }

        AddRow("Operator / Tech ID:", _txtOperatorId);
        AddRow("Analyzer Model:", _txtAnalyzerModel);
        AddRow("Analyzer Serial #:", _txtAnalyzerSerial);

        group.Controls.Add(layout);
        panel.Controls.Add(group);

        _globalMetadataGroup  = group;
        _globalMetadataLayout = layout;

        return panel;
    }

    /// <summary>Returns current global metadata values for use when committing a test record.</summary>
    internal (string OperatorId, string AnalyzerModel, string AnalyzerSerial) GetGlobalMetadata() =>
        (_txtOperatorId.Text.Trim(),
         _txtAnalyzerModel.Text.Trim(),
         _txtAnalyzerSerial.Text.Trim());

    private void RefreshQuickPorts()    {
        var selected = _cbQuickPort.Text;
        _cbQuickPort.Items.Clear();

        var ports = SerialPort.GetPortNames()
                              .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                              .ToArray();
        _cbQuickPort.Items.AddRange(ports);

        if (!string.IsNullOrWhiteSpace(selected) &&
            ports.Contains(selected, StringComparer.OrdinalIgnoreCase))
            _cbQuickPort.Text = selected;
        else if (!string.IsNullOrWhiteSpace(_currentOptions.Port) &&
                 ports.Contains(_currentOptions.Port, StringComparer.OrdinalIgnoreCase))
            _cbQuickPort.Text = _currentOptions.Port;
        else if (ports.Length > 0)
            _cbQuickPort.Text = ports[0];
    }

    private void OnQuickStartClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_cbQuickPort.Text))
        {
            MessageBox.Show(this, "Please select a COM port.", "No Port Selected",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _currentOptions = new AppOptions
        {
            Port             = _cbQuickPort.Text.Trim(),
            Baud             = _currentOptions.Baud,
            Parity           = _currentOptions.Parity,
            DataBits         = _currentOptions.DataBits,
            StopBits         = _currentOptions.StopBits,
            Handshake        = _currentOptions.Handshake,
            OutDir           = _currentOptions.OutDir,
            Prefix           = _currentOptions.Prefix,
            Csv              = _currentOptions.Csv,
            Reconnect        = _currentOptions.Reconnect,
            ReconnectDelayMs = _currentOptions.ReconnectDelayMs,
        };
        _simulationMode = false;
        _ = StartCaptureAsync().ContinueWith(
            t => AppLog.Error("StartCaptureAsync error", t.Exception?.InnerException ?? t.Exception ?? new Exception("Unknown error")),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    // ── Dark mode theming ────────────────────────────────────────────────
    // Note: native WinForms controls (ComboBox drop-down list, scrollbars,
    // TabControl tabs) cannot be fully themed without owner-draw overrides,
    // which is out of scope for this focused PR.
    private void ApplyTheme(bool isDark)
    {
        _isDarkMode = isDark;
        _btnDarkMode.Text = isDark ? "☀ Light" : "🌙 Dark";

        var formBg  = isDark ? AppTheme.DarkFormBg  : AppTheme.LightFormBg;

        BackColor = formBg;

        // Theme the global metadata left panel — targeted per control type
        if (_leftPanel != null)
            _leftPanel.BackColor = AppTheme.PanelBg(isDark);

        if (_globalMetadataGroup != null)
        {
            _globalMetadataGroup.BackColor = AppTheme.PanelBg(isDark);
            _globalMetadataGroup.ForeColor = AppTheme.LabelFg(isDark);
        }

        if (_globalMetadataLayout != null)
        {
            _globalMetadataLayout.BackColor = AppTheme.PanelBg(isDark);
            foreach (Control c in _globalMetadataLayout.Controls)
            {
                if (c is TextBox txt)
                {
                    txt.BackColor = AppTheme.InputBg(isDark);
                    txt.ForeColor = AppTheme.InputFg(isDark);
                }
                else if (c is Label lbl)
                {
                    lbl.BackColor = AppTheme.PanelBg(isDark);
                    lbl.ForeColor = AppTheme.LabelFg(isDark);
                }
            }
        }

        foreach (var tab in _detailTabs)
            tab.ApplyTheme(isDark);
        _overviewTab.ApplyTheme(isDark);
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

        // Update status strip (bottom bar) with a friendly connection status
        if (_statusStripLabel is not null)
        {
            _statusStripLabel.Text = state switch
            {
                CaptureLifecycleState.Running =>
                    _simulationMode
                        ? "🟢 Connected: Demo Mode"
                        : $"🟢 Connected: {_currentOptions.Port} ({_currentOptions.Baud})",
                CaptureLifecycleState.Starting => "🟡 Connecting…",
                CaptureLifecycleState.Stopping => "🟡 Disconnecting…",
                CaptureLifecycleState.Error    => "🔴 Error",
                _                              => "🔴 Disconnected",
            };
        }

        // Update quick-access toolbar button states
        bool canStart = state is CaptureLifecycleState.Idle or CaptureLifecycleState.Error;
        bool canStop  = state == CaptureLifecycleState.Running;
        _btnStart.Enabled    = canStart;
        _btnStop.Enabled     = canStop;
        _cbQuickPort.Enabled = canStart;
    }

    private async Task StartCaptureAsync()
    {
        if (_captureTask is not null)
            return;

        SetLifecycleState(CaptureLifecycleState.Starting);

        try
        {
            _sink = CreateSink();

            _cts = new CancellationTokenSource();

            if (_simulationMode)
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
                AppLog.Info($"Starting capture on {_currentOptions.Port} @ {_currentOptions.Baud} baud");
                var service = new SerialCaptureService(
                    _currentOptions,
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
        var outDir = _currentOptions.OutDir?.Trim();
        if (string.IsNullOrWhiteSpace(outDir))
            throw new InvalidOperationException("Please select an output folder.");

        var prefix = _currentOptions.Prefix?.Trim();
        if (string.IsNullOrWhiteSpace(prefix))
            prefix = "cadex_raw";

        return _currentOptions.Csv
            ? new CsvLineSink(outDir, prefix)
            : new RawLineSink(outDir, prefix);
    }

    private void OnRawLine(string line)
    {
        // All state mutations and UI updates happen on the UI thread to avoid data races.
        BeginInvoke(() => _stateManager.ProcessLine(line));
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

        // Preserve error state visibility; only reset to Idle on a clean stop.
        if (_lifecycleState != CaptureLifecycleState.Error)
            SetLifecycleState(CaptureLifecycleState.Idle, "Stopped");
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
        _currentOptions = opts;
    }

    /// <summary>
    /// Opens the Connection Settings popup.  Handles Connect / Disconnect actions
    /// returned from the dialog and starts or stops the serial capture accordingly.
    /// </summary>
    private void OpenConnectionSettings()
    {
        bool isRunning = _captureTask is not null;
        using var form = new ConnectionSettingsForm(_currentOptions, isRunning, _simulationMode);
        form.ShowDialog(this);

        switch (form.Action)
        {
            case ConnectionAction.Connect:
                _currentOptions  = form.GetOptions();
                _simulationMode  = form.SimulationMode;
                _ = StartCaptureAsync().ContinueWith(
                    t => AppLog.Error("StartCaptureAsync error", t.Exception?.InnerException ?? t.Exception!),
                    TaskContinuationOptions.OnlyOnFaulted);
                break;

            case ConnectionAction.Disconnect:
                _ = StopCaptureAsync().ContinueWith(
                    t => AppLog.Error("StopCaptureAsync error", t.Exception?.InnerException ?? t.Exception!),
                    TaskContinuationOptions.OnlyOnFaulted);
                break;
        }
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
                    return _dashboardServer.DisposeAsync().AsTask().ContinueWith(
                        dt =>
                        {
                            if (dt.IsFaulted)
                                AppLog.Error("Dashboard server shutdown error", dt.Exception?.InnerException ?? dt.Exception!);
                            Invoke(Close);
                        },
                        TaskScheduler.Default);
                },
                TaskScheduler.Default);
            return;
        }

        // Stop the web dashboard gracefully on close; log any failure.
        _ = _dashboardServer.DisposeAsync().AsTask().ContinueWith(
            t =>
            {
                if (t.IsFaulted)
                    AppLog.Error("Dashboard server shutdown error", t.Exception?.InnerException ?? t.Exception!);
            },
            TaskScheduler.Default);

        base.OnFormClosing(e);
    }
}
