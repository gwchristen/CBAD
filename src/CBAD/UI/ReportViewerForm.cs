using System.Diagnostics;
using Microsoft.Web.WebView2.WinForms;

namespace CBAD.UI;

internal sealed class ReportViewerForm : Form
{
    private readonly WebView2 _webView;
    private readonly string _tempFilePath;

    public ReportViewerForm(string htmlContent, string title = "Battery Test Report")
    {
        Text = title;
        Width = 1000;
        Height = 800;
        StartPosition = FormStartPosition.CenterParent;
        ShowIcon = false;

        _webView = new WebView2 { Dock = DockStyle.Fill };
        Controls.Add(_webView);

        _tempFilePath = Path.Combine(Path.GetTempPath(), $"CBAD_Report_{Guid.NewGuid()}.html");
        File.WriteAllText(_tempFilePath, htmlContent);

        Load += OnLoad;
        FormClosed += OnClosed;
    }

    private async void OnLoad(object? sender, EventArgs e)
    {
        try
        {
            await _webView.EnsureCoreWebView2Async(null);
            _webView.CoreWebView2.Navigate(new Uri(_tempFilePath).AbsoluteUri);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize WebView2. Please ensure the Microsoft Edge WebView2 Runtime is installed.\n\nError: {ex.Message}", 
                "Report Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            
            // Fallback: open in default browser
            try { Process.Start(new ProcessStartInfo { FileName = _tempFilePath, UseShellExecute = true }); } catch { }
            Close();
        }
    }

    private void OnClosed(object? sender, FormClosedEventArgs e)
    {
        try
        {
            if (File.Exists(_tempFilePath))
                File.Delete(_tempFilePath);
        }
        catch { }
    }
}
