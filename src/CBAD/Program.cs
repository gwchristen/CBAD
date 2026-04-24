using System.IO.Ports;
using CBAD;

if (!AppOptionsParser.TryParse(args, out var options, out var error))
{
    Console.WriteLine($"Error: {error}");
    Console.WriteLine();
    HelpText.Print();
    return;
}

if (options.ListPorts)
{
    var ports = SerialPort.GetPortNames().OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
    if (ports.Length == 0)
    {
        Console.WriteLine("No serial ports found.");
    }
    else
    {
        Console.WriteLine("Available serial ports:");
        foreach (var p in ports)
            Console.WriteLine($" - {p}");
    }
    return;
}

ILineSink sink = options.Csv
    ? new CsvLineSink(options.OutDir, options.Prefix)
    : new RawLineSink(options.OutDir, options.Prefix);

using (sink)
{
    Console.WriteLine($"Logging to: {sink.Path}");
    Console.WriteLine("Press Ctrl+C to stop.");

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
        Console.WriteLine();
        Console.WriteLine("Stopping...");
    };

    var service = new SerialCaptureService(options, sink);
    try
    {
        await service.RunAsync(cts.Token);
    }
    catch (OperationCanceledException)
    {
        // graceful shutdown
    }
}
