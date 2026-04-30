using System.Net;

namespace CBAD.Reporting;

internal static class ReportGenerator
{
    public static string GenerateHtml(ReportData data)
    {
        var visualStatus = data.PassedVisualInspection ? "<span style='color:green'>Passed</span>" : "<span style='color:red'>Failed</span>";

        var chartHtml = string.IsNullOrEmpty(data.ChartImageBase64) 
            ? "" 
            : $"<div class='chart-container'><h2>Test Chart</h2><img src='data:image/png;base64,{data.ChartImageBase64}' alt='Test Chart' /></div>";

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Battery Test Report - {data.BatterySerial}</title>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; padding: 20px; color: #333; max-width: 900px; margin: 0 auto; }}
        h1 {{ color: #1E2E4E; border-bottom: 2px solid #1E2E4E; padding-bottom: 10px; }}
        .section {{ margin-bottom: 20px; padding: 15px; border: 1px solid #ddd; background: #f9f9f9; border-radius: 5px; }}
        .section h2 {{ margin-top: 0; font-size: 1.2em; border-bottom: 1px solid #ccc; padding-bottom: 5px; color: #444; }}
        table {{ width: 100%; border-collapse: collapse; margin-top: 10px; }}
        th, td {{ padding: 8px; text-align: left; border-bottom: 1px solid #eee; }}
        th {{ width: 200px; color: #555; background: #f1f1f1; }}
        .notes {{ white-space: pre-wrap; font-family: 'Consolas', monospace; font-size: 0.9em; background: #fff; padding: 10px; border: 1px solid #ccc; border-radius: 4px; min-height: 50px; }}
        .chart-container {{ text-align: center; margin-top: 20px; border: 1px solid #ddd; padding: 10px; background: #fff; border-radius: 5px; }}
        img {{ max-width: 100%; height: auto; }}
        .btn-print {{ padding: 8px 16px; font-size: 14px; background: #1E2E4E; color: white; border: none; border-radius: 4px; cursor: pointer; }}
        .btn-print:hover {{ background: #2A406B; }}
        @media print {{
            body {{ padding: 0; background: #fff; max-width: none; }}
            .section {{ border: none; background: transparent; padding: 0; margin-bottom: 15px; }}
            .chart-container {{ border: none; padding: 0; }}
            .no-print {{ display: none; }}
            th {{ background: transparent; border-bottom: 1px solid #ddd; }}
        }}
    </style>
</head>
<body>
    <div style='text-align: right; margin-bottom: -40px;' class='no-print'>
        <button class='btn-print' onclick='window.print()'>🖨 Print / Save PDF</button>
    </div>
    <h1>Battery Analysis Report</h1>
    
    <div class='section'>
        <h2>Test Metadata</h2>
        <table>
            <tr><th>Date</th><td>{data.Date}</td></tr>
            <tr><th>Work Order</th><td>{WebUtility.HtmlEncode(data.WorkOrder)}</td></tr>
            <tr><th>Battery Serial</th><td>{WebUtility.HtmlEncode(data.BatterySerial)}</td></tr>
            <tr><th>Station</th><td>{data.Station}</td></tr>
            <tr><th>Visual Inspection</th><td>{visualStatus}</td></tr>
        </table>
    </div>

    <div class='section'>
        <h2>Final Results</h2>
        <table>
            <tr><th>Status</th><td><strong>{data.FinalStatus}</strong></td></tr>
            <tr><th>Process Code</th><td>{data.ProcessCode}</td></tr>
            <tr><th>Target Capacity</th><td>{data.TargetCapacity}</td></tr>
            <tr><th>Health</th><td>{data.FinalHealth}</td></tr>
            <tr><th>Voltage</th><td>{data.FinalVoltage}</td></tr>
            <tr><th>Resistance</th><td>{data.Resistance}</td></tr>
        </table>
    </div>

    <div class='section'>
        <h2>Notes / Remarks</h2>
        <div class='notes'>{WebUtility.HtmlEncode(data.Notes)}</div>
    </div>

    {chartHtml}
</body>
</html>";
    }
}
