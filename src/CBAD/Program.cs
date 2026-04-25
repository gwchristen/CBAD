using System.IO.Ports;
using System.Runtime.InteropServices;

namespace CBAD;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // ── Console-only commands ────────────────────────────────────────
        // These exit immediately without opening a GUI window.
        if (args.Length > 0)
        {
            bool wantsHelp = args.Any(a =>
                string.Equals(a, "--help", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(a, "-h", StringComparison.OrdinalIgnoreCase));

            bool wantsList = args.Any(a =>
                string.Equals(a, "--list-ports", StringComparison.OrdinalIgnoreCase));

            if (wantsHelp || wantsList)
            {
                // When run via `dotnet run` or from a terminal the parent
                // console is already attached.  For a compiled WinExe, try to
                // reuse the parent console (cmd / PowerShell).
                AttachParentConsole();

                if (wantsHelp)
                    HelpText.Print();
                else
                    ListPorts();

                return;
            }

            // ── Startup defaults ─────────────────────────────────────────
            // Any remaining args are treated as port/baud/… startup defaults
            // that pre-populate the GUI.  Unknown or malformed args surface
            // as an error message rather than silently being ignored.
            if (!AppOptionsParser.TryParse(args, out var opts, out var error))
            {
                AttachParentConsole();
                Console.Error.WriteLine($"Error: {error}");
                Console.Error.WriteLine("Run with --help for usage information.");
                return;
            }

            AppLog.Info($"Startup defaults: port={opts.Port} baud={opts.Baud}");
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm(opts));
            return;
        }

        // ── Normal GUI launch ────────────────────────────────────────────
        AppLog.Info("Starting CBAD GUI");
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }

    private static void ListPorts()
    {
        var ports = SerialPort.GetPortNames()
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (ports.Length == 0)
        {
            Console.WriteLine("No serial ports found.");
        }
        else
        {
            Console.WriteLine("Available serial ports:");
            foreach (var port in ports)
                Console.WriteLine($"  {port}");
        }
    }

    /// <summary>
    /// Attempts to attach to the parent process console so that console output
    /// is visible when the WinExe is invoked from cmd or PowerShell.
    /// No-op when already attached (e.g. <c>dotnet run</c>) or when there is
    /// no parent console.
    /// </summary>
    private static void AttachParentConsole()
    {
        // ATTACH_PARENT_PROCESS = -1
        NativeMethods.AttachConsole(-1);
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AttachConsole(int dwProcessId);
    }
}
