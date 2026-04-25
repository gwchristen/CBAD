# CBAD
Application for capture data from Cadex 7400 series battery analyzer over serial.

## Version 2 (serial logger)

This app captures raw serial data and saves it to timestamped files.

### Requirements

- Windows PC
- .NET 8 SDK
- USB-to-Serial adapter connected to Cadex 7400ER

### Build & Run

```powershell
dotnet run --project src/CBAD
```

### GUI launch with startup defaults

You can pre-populate the GUI connection fields by passing serial-port options:

```powershell
dotnet run --project src/CBAD -- --port COM3 --baud 9600
```

All options are optional; the GUI lets you change them before pressing **Start**.

### CLI-only commands

These commands write output to the console and exit without opening a window:

```powershell
# List available COM ports
dotnet run --project src/CBAD -- --list-ports

# Show full usage help
dotnet run --project src/CBAD -- --help
```

> **Note:** When running the compiled `.exe` from a terminal (cmd / PowerShell)
> the app attaches to the parent console automatically so that `--list-ports`
> and `--help` output is visible.  Double-clicking the `.exe` without arguments
> opens the GUI as normal.

### Common options

- `--port COM3` (pre-selects port in GUI; required for CLI headless use)
- `--baud 9600` (default: 9600)
- `--parity None|Odd|Even|Mark|Space` (default: None)
- `--data-bits 8` (default: 8)
- `--stop-bits One|Two|OnePointFive` (default: One)
- `--handshake None|XOnXOff|RequestToSend|RequestToSendXOnXOff` (default: None)
- `--out-dir logs` (default: ./logs)
- `--prefix cadex_raw` (default: cadex_raw)
- `--csv true|false` (default: false)
- `--reconnect true|false` (default: true)
- `--reconnect-delay-ms 2000` (default: 2000)
- `--list-ports` to show available COM ports and exit
- `--help` to show usage and exit

### Examples

List ports:

```powershell
dotnet run --project src/CBAD -- --list-ports
```

Capture to raw log:

```powershell
dotnet run --project src/CBAD -- --port COM4 --baud 19200
```

Capture to CSV:

```powershell
dotnet run --project src/CBAD -- --port COM4 --csv true
```

### Notes

- Match serial settings to the Cadex output settings.
- Press **Stop** in the GUI (or close the window) to stop gracefully.
- Trace-level logs are written to the debug output in development builds.
  Add a `TextWriterTraceListener` via `AppLog.AddFileListener(path)` in
  `Program.Main` to persist logs to a file.
