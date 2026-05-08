using CBAD.Parsing;
using CBAD.UI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;

namespace CBAD.WebServer;

/// <summary>
/// Hosts a lightweight ASP.NET Core Minimal API server inside the WinForms
/// process so that the live test status can be viewed from any browser on the
/// local network.
/// </summary>
internal sealed class DashboardServer : IAsyncDisposable
{
    /// <summary>TCP port the dashboard listens on.</summary>
    public const int Port = 5000;

    private readonly StationStateManager _manager;
    private int _lifecycleStateValue = (int)CaptureLifecycleState.Idle;
    private readonly object _lifecycleDetailLock = new();
    private string? _lifecycleDetail;
    private DateTimeOffset _lifecycleTimestampUtc = DateTimeOffset.UtcNow;
    private WebApplication? _app;

    public DashboardServer(StationStateManager manager)
    {
        _manager = manager;
    }

    /// <summary>
    /// Returns the most likely LAN URL for the dashboard
    /// (e.g. <c>http://192.168.1.10:5000</c>).
    /// Falls back to <c>http://localhost:5000</c> when no LAN adapter is found.
    /// </summary>
    public string GetLocalUrl()
    {
        var ip = GetLocalIpAddress() ?? "localhost";
        return $"http://{ip}:{Port}";
    }

    /// <summary>Configures and starts the web server on a background thread.</summary>
    public void Start()
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Production",
            Args = ["--urls", $"http://*:{Port}"],
        });

        // Suppress hosting infrastructure logs so they don't interfere with
        // the WinForms application output.
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        _app = builder.Build();

        RegisterRoutes(_app);

        // StartAsync begins listening; errors are surfaced through the returned
        // task so we observe them and log rather than swallowing them silently.
        _ = _app.StartAsync().ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                var ex = t.Exception?.InnerException ?? t.Exception;
                if (ex is not null)
                    AppLog.Error("Web dashboard failed to start", ex);
                else
                    AppLog.Error("Web dashboard failed to start");
            }
        }, TaskScheduler.Default);
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await _app.StopAsync(cts.Token).ConfigureAwait(false);
            await _app.DisposeAsync().ConfigureAwait(false);
            _app = null;
        }
    }

    public void SetLifecycleState(CaptureLifecycleState state, string? detail)
    {
        Interlocked.Exchange(ref _lifecycleStateValue, (int)state);
        lock (_lifecycleDetailLock)
        {
            _lifecycleDetail = detail;
            _lifecycleTimestampUtc = DateTimeOffset.UtcNow;
        }
    }

    // ── Route registrations ──────────────────────────────────────────────

    private void RegisterRoutes(WebApplication app)
    {
        app.MapGet("/", () => Results.Content(DashboardHtml, "text/html; charset=utf-8"));

        app.MapGet("/api/stations", () =>
        {
            var summaries = new StationSummary[4];
            for (int i = 1; i <= 4; i++)
            {
                var state = _manager.GetState(i);
                var r = state.Latest;
                summaries[i - 1] = new StationSummary(
                    StationId:    state.Station,
                    BatteryId:    r?.BatteryId ?? string.Empty,
                    Status:       CadexStatusCodes.Describe(r?.ProcessCode?.ToString()),
                    VoltageMv:    r?.VoltageMv,
                    CurrentMa:    r?.CurrentMa,
                    HealthCurrent:  r?.HealthCurrent,
                    HealthPrevious: r?.HealthPrevious,
                    TemperatureC:   r?.TemperatureC,
                    LastUpdate:     r?.ReceivedAt,
                    FailureReason:  state.FailureReason,
                    SessionStart:   state.SessionStart
                );
            }
            return Results.Json(new DashboardPayload(
                Connection: GetConnectionSummary(),
                Stations: summaries
            ));
        });
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static string? GetLocalIpAddress()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                            n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.Address.ToString())
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private ConnectionSummary GetConnectionSummary()
    {
        var state = (CaptureLifecycleState)Volatile.Read(ref _lifecycleStateValue);
        string? detail;
        DateTimeOffset timestamp;
        lock (_lifecycleDetailLock)
        {
            detail = _lifecycleDetail;
            timestamp = _lifecycleTimestampUtc;
        }
        return new ConnectionSummary(
            State: state.ToString(),
            Detail: detail,
            Timestamp: timestamp
        );
    }

    // ── DTO ──────────────────────────────────────────────────────────────

    private sealed record DashboardPayload(
        ConnectionSummary Connection,
        StationSummary[] Stations
    );

    private sealed record ConnectionSummary(
        string State,
        string? Detail,
        DateTimeOffset Timestamp
    );

    private sealed record StationSummary(
        int StationId,
        string BatteryId,
        string Status,
        int? VoltageMv,
        int? CurrentMa,
        int? HealthCurrent,
        int? HealthPrevious,
        int? TemperatureC,
        DateTimeOffset? LastUpdate,
        string? FailureReason,
        DateTimeOffset? SessionStart
    );

    // ── Embedded HTML dashboard ──────────────────────────────────────────

    private const string DashboardHtml = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8"/>
          <meta name="viewport" content="width=device-width, initial-scale=1"/>
          <title>CBAD — Live Dashboard</title>
          <style>
            *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

            :root {
              --bg:        #0f1117;
              --surface:   #1c2033;
              --border:    #2e3556;
              --accent:    #3b82f6;
              --accent-lo: #1d3a6e;
              --text:      #e4e8f4;
              --muted:     #7a84a8;
              --green:     #22c55e;
              --yellow:    #eab308;
              --red:       #ef4444;
              --orange:    #f97316;
              --radius:    12px;
              --gap:       18px;
            }

            body {
              background: var(--bg);
              color: var(--text);
              font-family: "Segoe UI", system-ui, sans-serif;
              font-size: 14px;
              min-height: 100vh;
            }

            header {
              background: #1a2040;
              border-bottom: 1px solid var(--border);
              padding: 14px 24px;
              display: flex;
              align-items: center;
              justify-content: space-between;
              gap: 12px;
            }
            header h1 {
              font-size: 1.25rem;
              font-weight: 700;
              letter-spacing: .5px;
              color: #fff;
            }
            header h1 span { color: var(--accent); }

            #refresh-indicator {
              display: flex;
              align-items: center;
              gap: 8px;
              font-size: .8rem;
              color: var(--muted);
            }
            #refresh-indicator .dot {
              width: 8px; height: 8px;
              border-radius: 50%;
              background: var(--green);
              animation: pulse 2s ease-in-out infinite;
            }
            @keyframes pulse {
              0%, 100% { opacity: 1; }
              50%       { opacity: .3; }
            }

            #connection-banner {
              margin: 16px 16px 0 16px;
              padding: 12px 14px;
              border: 1px solid var(--border);
              border-radius: 10px;
              background: #151a2b;
              display: flex;
              flex-wrap: wrap;
              align-items: center;
              gap: 10px;
            }
            .connection-dot {
              width: 10px;
              height: 10px;
              border-radius: 50%;
              background: #94a3b8;
              box-shadow: 0 0 0 3px rgba(148, 163, 184, .15);
            }
            .connection-dot.running {
              background: var(--green);
              box-shadow: 0 0 0 3px rgba(34, 197, 94, .18);
            }
            .connection-dot.transitioning {
              background: var(--yellow);
              box-shadow: 0 0 0 3px rgba(234, 179, 8, .16);
            }
            .connection-dot.error {
              background: var(--red);
              box-shadow: 0 0 0 3px rgba(239, 68, 68, .16);
            }
            #connection-state {
              font-size: .88rem;
              font-weight: 700;
              color: #fff;
            }
            #connection-detail {
              font-size: .82rem;
              color: var(--muted);
              min-width: 220px;
              flex: 1 1 220px;
            }
            #connection-time {
              font-size: .75rem;
              color: var(--muted);
            }

            main {
              padding: var(--gap);
              display: grid;
              grid-template-columns: repeat(auto-fill, minmax(310px, 1fr));
              gap: var(--gap);
            }

            .card {
              background: var(--surface);
              border: 1px solid var(--border);
              border-radius: var(--radius);
              padding: 20px;
              transition: border-color .2s;
            }
            .card:hover { border-color: var(--accent); }

            .card-header {
              display: flex;
              align-items: center;
              justify-content: space-between;
              margin-bottom: 14px;
            }
            .station-name {
              font-size: 1rem;
              font-weight: 700;
              color: #fff;
            }
            .status-badge {
              padding: 3px 10px;
              border-radius: 20px;
              font-size: .75rem;
              font-weight: 600;
              background: var(--accent-lo);
              color: var(--accent);
              white-space: nowrap;
            }
            .status-badge.active  { background: #14532d; color: var(--green);  }
            .status-badge.idle    { background: #292524; color: var(--muted);   }
            .status-badge.warning { background: #451a03; color: var(--orange);  }
            .status-badge.fail    { background: #450a0a; color: var(--red);     }

            .card-fail { border-color: var(--red); }
            .card-fail:hover { border-color: #ff6b6b; }

            .failure-reason {
              background: #450a0a;
              border: 1px solid #7f1d1d;
              border-radius: 6px;
              color: #fca5a5;
              font-size: .8rem;
              font-weight: 600;
              padding: 6px 10px;
              margin-bottom: 12px;
            }

            .battery-id {
              font-size: .8rem;
              color: var(--muted);
              margin-bottom: 16px;
              white-space: nowrap;
              overflow: hidden;
              text-overflow: ellipsis;
            }
            .battery-id span { color: var(--text); }

            .metrics {
              display: grid;
              grid-template-columns: 1fr 1fr;
              gap: 10px;
            }
            .metric {
              background: #0f1117;
              border: 1px solid #252a40;
              border-radius: 8px;
              padding: 10px 12px;
            }
            .metric-label {
              font-size: .7rem;
              color: var(--muted);
              text-transform: uppercase;
              letter-spacing: .6px;
              margin-bottom: 4px;
            }
            .metric-value {
              font-size: 1.1rem;
              font-weight: 700;
              color: #fff;
            }
            .metric-value.good    { color: var(--green);  }
            .metric-value.warn    { color: var(--yellow); }
            .metric-value.bad     { color: var(--red);    }
            .metric-unit {
              font-size: .75rem;
              color: var(--muted);
              margin-left: 2px;
              font-weight: 400;
            }

            .last-update {
              margin-top: 14px;
              font-size: .73rem;
              color: var(--muted);
              text-align: right;
            }

            footer {
              text-align: center;
              padding: 20px;
              font-size: .75rem;
              color: var(--muted);
              border-top: 1px solid var(--border);
            }

            #error-banner {
              display: none;
              margin: 16px;
              padding: 12px 16px;
              background: #450a0a;
              border: 1px solid var(--red);
              border-radius: 8px;
              color: #fca5a5;
              font-size: .85rem;
            }
          </style>
        </head>
        <body>
          <header>
            <h1>CBAD &nbsp;<span>Battery Analyzer</span></h1>
            <div id="refresh-indicator">
              <div class="dot"></div>
              <span id="last-fetch">Connecting…</span>
            </div>
          </header>

          <div id="connection-banner">
            <div id="connection-dot" class="connection-dot"></div>
            <span id="connection-state">Idle</span>
            <span id="connection-detail"></span>
            <span id="connection-time"></span>
          </div>

          <div id="error-banner"></div>
          <main id="grid"></main>

          <footer>CBAD Embedded Web Dashboard &mdash; auto-refreshes every 5&nbsp;s</footer>

          <script>
            const REFRESH_MS = 5000;

            function fmtVoltage(mv) {
              if (mv == null) return { text: '—', cls: '' };
              const v = (mv / 1000).toFixed(3);
              const cls = mv < 3000 ? 'bad' : mv < 3500 ? 'warn' : 'good';
              return { text: v, unit: 'V', cls };
            }

            function fmtCurrent(ma) {
              if (ma == null) return { text: '—', cls: '' };
              const a = (Math.abs(ma) / 1000).toFixed(3);
              const label = ma < 0 ? '−' + a : '+' + a;
              return { text: label, unit: 'A', cls: ma < 0 ? 'warn' : 'good' };
            }

            function fmtHealth(hc, hp) {
              if (hc == null) return { text: '—', cls: '' };
              const pct = hp ? Math.round((hc / hp) * 100) : hc;
              const cls = pct < 60 ? 'bad' : pct < 80 ? 'warn' : 'good';
              return { text: String(pct), unit: '%', cls };
            }

            function fmtTemp(tc) {
              if (tc == null) return { text: '—', cls: '' };
              const cls = tc > 45 ? 'bad' : tc > 35 ? 'warn' : 'good';
              return { text: String(tc), unit: '°C', cls };
            }

            function statusBadge(status, lastUpdate, failureReason) {
              if (failureReason) return { text: 'FAIL', cls: 'fail' };
              if (!lastUpdate) return { text: 'No Data', cls: 'idle' };
              const age = (Date.now() - new Date(lastUpdate)) / 1000;
              if (age > 60) return { text: 'Stale', cls: 'warning' };
              return { text: status || 'Active', cls: 'active' };
            }

            function metricHtml(label, val) {
              const cls = val.cls ? ` class="metric-value ${val.cls}"` : ' class="metric-value"';
              const unit = val.unit ? `<span class="metric-unit">${val.unit}</span>` : '';
              return `<div class="metric">
                <div class="metric-label">${label}</div>
                <div${cls}>${val.text}${unit}</div>
              </div>`;
            }

            function fmtRuntime(sessionStart) {
              if (!sessionStart) return '—';
              const totalSec = Math.floor((Date.now() - new Date(sessionStart)) / 1000);
              const h = Math.floor(totalSec / 3600);
              const m = Math.floor((totalSec % 3600) / 60);
              const s = totalSec % 60;
              if (h > 0) return `${h}h ${String(m).padStart(2,'0')}m ${String(s).padStart(2,'0')}s`;
              return `${m}m ${String(s).padStart(2,'0')}s`;
            }

            function renderCard(s) {
              const badge   = statusBadge(s.status, s.lastUpdate, s.failureReason);
              const voltage = fmtVoltage(s.voltageMv);
              const current = fmtCurrent(s.currentMa);
              const health  = fmtHealth(s.healthCurrent, s.healthPrevious);
              const temp    = fmtTemp(s.temperatureC);
              const runtime = fmtRuntime(s.sessionStart);

              const lu = s.lastUpdate
                ? new Date(s.lastUpdate).toLocaleTimeString()
                : 'never';

              const failureBanner = s.failureReason
                ? `<div class="failure-reason">⚠ ${s.failureReason}</div>`
                : '';

              return `<div class="card${s.failureReason ? ' card-fail' : ''}">
                <div class="card-header">
                  <span class="station-name">Station ${s.stationId}</span>
                  <span class="status-badge ${badge.cls}">${badge.text}</span>
                </div>
                <div class="battery-id">Battery: <span>${s.batteryId || '—'}</span></div>
                ${failureBanner}
                <div class="metrics">
                  ${metricHtml('Voltage',  voltage)}
                  ${metricHtml('Current',  current)}
                  ${metricHtml('Health',   health)}
                  ${metricHtml('Temp',     temp)}
                </div>
                <div class="last-update">Runtime: ${runtime} &nbsp;|&nbsp; Last update: ${lu}</div>
              </div>`;
            }

            function connectionClass(state) {
              switch (state) {
                case 'Running': return 'running';
                case 'Starting':
                case 'Stopping': return 'transitioning';
                case 'Error': return 'error';
                default: return '';
              }
            }

            function renderConnection(connection) {
              const c = connection || { state: 'Idle', detail: '', timestamp: null };
              const dot = document.getElementById('connection-dot');
              dot.className = `connection-dot ${connectionClass(c.state)}`.trim();
              document.getElementById('connection-state').textContent = c.state || 'Idle';
              document.getElementById('connection-detail').textContent = c.detail || '';
              document.getElementById('connection-time').textContent =
                c.timestamp ? ('As of ' + new Date(c.timestamp).toLocaleTimeString()) : '';
            }

            async function refresh() {
              const errorBanner = document.getElementById('error-banner');
              try {
                const res = await fetch('/api/stations');
                if (!res.ok) throw new Error(`HTTP ${res.status}`);
                const payload = await res.json();
                const stations = payload.stations || [];

                renderConnection(payload.connection);

                document.getElementById('grid').innerHTML =
                  stations.map(renderCard).join('');

                errorBanner.style.display = 'none';
                document.getElementById('last-fetch').textContent =
                  'Updated ' + new Date().toLocaleTimeString();
              } catch (err) {
                errorBanner.style.display = 'block';
                errorBanner.textContent   = 'Could not reach the analyzer: ' + err.message;
                document.getElementById('last-fetch').textContent = 'Error';
              }
            }

            refresh();
            setInterval(refresh, REFRESH_MS);
          </script>
        </body>
        </html>
        """;
}
