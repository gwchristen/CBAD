using System.IO.Ports;
using System.Text;

namespace CBAD;

public class MainForm : Form
{
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
    private Button btnBrowse = new();
    private Button btnRefreshPorts = new();
    private Button btnStart = new();
    private Button btnStop = new();
    private TextBox txtStatus = new();
    private TextBox txtPreview = new();

    private CancellationTokenSource? _cts;
    private Task? _captureTask;
    private ILineSink? _sink;

    public MainForm()
    {
        Text = "CBAD Serial Logger";
        Width = 1000;
        Height = 700;
        StartPosition = FormStartPosition.CenterScreen;

        BuildUi();
        LoadDefaults();
        RefreshPorts();
        SetRunningState(false);
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 4,
            RowCount = 6
        };
        for (int i = 0; i < 4; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        AddLabeled(grid, "Port", cbPort, 0, 0);
        AddLabeled(grid, "Baud", cbBaud, 1, 0);
        AddLabeled(grid, "Parity", cbParity, 2, 0);
        AddLabeled(grid, "Data Bits", cbDataBits, 3, 0);

        AddLabeled(grid, "Stop Bits", cbStopBits, 0, 2);
        AddLabeled(grid, "Handshake", cbHandshake, 1, 2);

        var outDirPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        txtOutDir.Width = 280;
        btnBrowse.Text = "Browse...";
        btnBrowse.Click += (_, __) => BrowseOutDir();
        outDirPanel.Controls.Add(txtOutDir);
        outDirPanel.Controls.Add(btnBrowse);
        AddLabeled(grid, "Output Folder", outDirPanel, 2, 2);

        AddLabeled(grid, "File Prefix", txtPrefix, 3, 2);

        chkCsv.Text = "CSV";
        chkReconnect.Text = "Auto Reconnect";
        numReconnectMs.Minimum = 100;
        numReconnectMs.Maximum = 600000;
        numReconnectMs.Increment = 100;

        var flags = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        flags.Controls.Add(chkCsv);
        flags.Controls.Add(chkReconnect);
        flags.Controls.Add(new Label { Text = "Reconnect (ms)", AutoSize = true, Margin = new Padding(20, 8, 3, 3) });
        flags.Controls.Add(numReconnectMs);
        grid.Controls.Add(flags, 0, 4);
        grid.SetColumnSpan(flags, 4);

        root.Controls.Add(grid);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        btnRefreshPorts.Text = "Refresh Ports";
        btnRefreshPorts.Click += (_, __) => RefreshPorts();
        btnStart.Text = "Start";
        btnStart.Click += async (_, __) => await StartCaptureAsync();
        btnStop.Text = "Stop";
        btnStop.Click += (_, __) => StopCapture();

        txtStatus.ReadOnly = true;
        txtStatus.Width = 500;

        buttons.Controls.Add(btnRefreshPorts);
        buttons.Controls.Add(btnStart);
        buttons.Controls.Add(btnStop);
        buttons.Controls.Add(new Label { Text = "Status:", AutoSize = true, Margin = new Padding(20, 8, 3, 3) });
        buttons.Controls.Add(txtStatus);

        root.Controls.Add(buttons);

        txtPreview.Multiline = true;
        txtPreview.ScrollBars = ScrollBars.Both;
        txtPreview.WordWrap = false;
        txtPreview.Dock = DockStyle.Fill;
        txtPreview.Font = new System.Drawing.Font("Consolas", 10);

        root.Controls.Add(txtPreview);
    }

    private static void AddLabeled(TableLayoutPanel grid, string label, Control control, int col, int row)
    {
        var lbl = new Label { Text = label, AutoSize = true, Margin = new Padding(3, 8, 3, 3) };
        grid.Controls.Add(lbl, col, row);
        control.Dock = DockStyle.Top;
        control.Margin = new Padding(3, 3, 10, 8);
        grid.Controls.Add(control, col, row + 1);
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

        try
        {
            var options = BuildOptionsFromUi();
            _sink = options.Csv
                ? new CsvLineSink(options.OutDir, options.Prefix)
                : new RawLineSink(options.OutDir, options.Prefix);

            txtPreview.Clear();
            SetStatus($"Logging to: {_sink.Path}");

            _cts = new CancellationTokenSource();
            var service = new SerialCaptureService(
                options,
                _sink,
                onData: AppendPreview,
                onStatus: SetStatus);

            _captureTask = Task.Run(() => service.RunAsync(_cts.Token));
            SetRunningState(true);

            await Task.Yield();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Cannot start capture", MessageBoxButtons.OK, MessageBoxIcon.Error);
            StopCapture();
        }
    }

    private void StopCapture()
    {
        try
        {
            _cts?.Cancel();
        }
        catch { }

        _cts?.Dispose();
        _cts = null;

        try
        {
            _captureTask?.Wait(500);
        }
        catch { }
        _captureTask = null;

        _sink?.Dispose();
        _sink = null;

        SetRunningState(false);
        SetStatus("Stopped");
    }

    private void SetRunningState(bool running)
    {
        btnStart.Enabled = !running;
        btnStop.Enabled = running;
        btnRefreshPorts.Enabled = !running;
        cbPort.Enabled = !running;
        cbBaud.Enabled = !running;
        cbParity.Enabled = !running;
        cbDataBits.Enabled = !running;
        cbStopBits.Enabled = !running;
        cbHandshake.Enabled = !running;
        txtOutDir.Enabled = !running;
        btnBrowse.Enabled = !running;
        txtPrefix.Enabled = !running;
        chkCsv.Enabled = !running;
        chkReconnect.Enabled = !running;
        numReconnectMs.Enabled = !running;
    }

    private void AppendPreview(string data)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(AppendPreview), data);
            return;
        }

        txtPreview.AppendText(data);

        const int maxChars = 200_000;
        if (txtPreview.TextLength > maxChars)
        {
            txtPreview.Text = txtPreview.Text[^maxChars..];
            txtPreview.SelectionStart = txtPreview.TextLength;
            txtPreview.ScrollToCaret();
        }
    }

    private void SetStatus(string status)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(SetStatus), status);
            return;
        }

        txtStatus.Text = status;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        StopCapture();
        base.OnFormClosing(e);
    }
}
