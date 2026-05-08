using CBAD.Models;

namespace CBAD.UI;

internal sealed class StationStreamPanel : UserControl
{
    private readonly int _station;
    private readonly TextBox _txtStream;
    private readonly Button _btnExport = new() { Text = "Export Raw Data…", Dock = DockStyle.Bottom, Height = 30 };
    private readonly CheckBox _chkAutoScroll = new() { Text = "Auto-scroll", Checked = true, AutoSize = true, Margin = new Padding(4, 4, 4, 3) };
    private readonly Button _btnPauseStream = new() { Text = "⏸  Pause", AutoSize = true, Margin = new Padding(4, 2, 4, 2) };
    private readonly TextBox _txtFilter = new() { Width = 160, PlaceholderText = "Filter lines…", Margin = new Padding(4, 2, 4, 2) };

    private StationState? _lastState;
    private bool _streamPaused;

    public StationStreamPanel(int station)
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
            BackColor = System.Drawing.Color.FromArgb(18, 24, 36),
            ForeColor = System.Drawing.Color.FromArgb(130, 210, 130),
        };

        Controls.Add(_txtStream);
        Controls.Add(_btnExport);
        Controls.Add(BuildStreamToolbar());

        WireExport();
    }

    public void UpdateStream(StationState state)
    {
        _lastState = state;
        if (!_streamPaused)
            RefreshStream();
    }

    public void ClearStream()
    {
        _lastState = null;
        _txtStream.Clear();
    }

    public void ApplyTheme(bool isDark)
    {
    }

    private FlowLayoutPanel BuildStreamToolbar()
    {
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(4, 2, 4, 2),
            BackColor = System.Drawing.SystemColors.ControlLight,
        };

        var clearFilter = new Button { Text = "✕", Width = 26, Height = 23, Margin = new Padding(0, 2, 6, 2) };
        clearFilter.Click += (_, __) => _txtFilter.Clear();

        _txtFilter.TextChanged += (_, __) => RefreshStream();
        _btnPauseStream.Click += (_, __) =>
        {
            _streamPaused = !_streamPaused;
            _btnPauseStream.Text = _streamPaused ? "▶  Resume" : "⏸  Pause";
            if (!_streamPaused)
                RefreshStream();
        };

        toolbar.Controls.Add(new Label { Text = "Filter:", AutoSize = true, Margin = new Padding(4, 6, 4, 3) });
        toolbar.Controls.Add(_txtFilter);
        toolbar.Controls.Add(clearFilter);
        toolbar.Controls.Add(_chkAutoScroll);
        toolbar.Controls.Add(_btnPauseStream);

        return toolbar;
    }

    private void RefreshStream()
    {
        if (_lastState is null)
            return;

        var lines = _lastState.RawLines.AsEnumerable();
        var filter = _txtFilter.Text.Trim();
        if (!string.IsNullOrEmpty(filter))
            lines = lines.Where(l => l.Contains(filter, StringComparison.OrdinalIgnoreCase));

        _txtStream.Text = string.Join(Environment.NewLine, lines.TakeLast(200));

        if (_chkAutoScroll.Checked)
        {
            _txtStream.SelectionStart = _txtStream.TextLength;
            _txtStream.ScrollToCaret();
        }
    }

    private void WireExport()
    {
        _btnExport.Click += (_, __) =>
        {
            if (_lastState is null || _lastState.RawLines.Count == 0)
            {
                MessageBox.Show("No data to export.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dlg = new SaveFileDialog
            {
                Title = "Export Raw Station Data",
                Filter = "CSV files (*.csv)|*.csv|Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = $"station{_station}_export_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            };
            if (dlg.ShowDialog() != DialogResult.OK)
                return;

            File.WriteAllLines(dlg.FileName, _lastState.RawLines);
            MessageBox.Show(
                $"Exported {_lastState.RawLines.Count} lines to:{Environment.NewLine}{dlg.FileName}",
                "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
    }
}
