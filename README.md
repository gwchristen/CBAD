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
dotnet run --project src/CBAD -- --port COM3 --baud 9600
```

### Common options

- `--port COM3` (required)
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
- `--list-ports` to show available COM ports

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
- Press `Ctrl+C` to stop gracefully.
