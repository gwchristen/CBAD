using System.Diagnostics;

namespace CBAD;

/// <summary>
/// Lightweight application logger backed by <see cref="Trace"/>.
/// In debug builds the <see cref="DefaultTraceListener"/> writes to the
/// debugger output window automatically.  Call <see cref="AddFileListener"/>
/// early in <c>Program.Main</c> to also persist entries to a file.
/// </summary>
internal static class AppLog
{
    private const string Prefix = "[CBAD]";

    /// <summary>Logs an informational message.</summary>
    public static void Info(string message)
        => Trace.TraceInformation($"{Prefix} {message}");

    /// <summary>Logs a warning message.</summary>
    public static void Warn(string message)
        => Trace.TraceWarning($"{Prefix} {message}");

    /// <summary>Logs an error message.</summary>
    public static void Error(string message)
        => Trace.TraceError($"{Prefix} {message}");

    /// <summary>Logs an error message with exception details.</summary>
    public static void Error(string message, Exception ex)
        => Trace.TraceError($"{Prefix} {message}: {ex}");

    /// <summary>
    /// Registers a <see cref="TextWriterTraceListener"/> that appends to
    /// <paramref name="path"/>.  Enables auto-flush so entries survive
    /// crashes.  Safe to call multiple times; duplicate paths are ignored.
    /// </summary>
    public static void AddFileListener(string path)
    {
        foreach (TraceListener existing in Trace.Listeners)
        {
            if (existing is TextWriterTraceListener twl &&
                string.Equals(twl.Name, path, StringComparison.OrdinalIgnoreCase))
                return; // already registered
        }

        var listener = new TextWriterTraceListener(path, path);
        Trace.Listeners.Add(listener);
        Trace.AutoFlush = true;
    }
}
