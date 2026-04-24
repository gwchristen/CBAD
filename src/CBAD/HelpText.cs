namespace CBAD;

internal static class HelpText
{
    public static void Print()
    {
        Console.WriteLine("CBAD Serial Logger v2");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project src/CBAD -- --port COM3 [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --list-ports                               List available serial ports");
        Console.WriteLine("  --port COM3                                Serial port name (required unless --list-ports)");
        Console.WriteLine("  --baud 9600                                Baud rate (default 9600)");
        Console.WriteLine("  --parity None|Odd|Even|Mark|Space          Parity (default None)");
        Console.WriteLine("  --data-bits 8                              Data bits (default 8)");
        Console.WriteLine("  --stop-bits One|Two|OnePointFive           Stop bits (default One)");
        Console.WriteLine("  --handshake None|XOnXOff|RequestToSend|RequestToSendXOnXOff");
        Console.WriteLine("                                              Handshake (default None)");
        Console.WriteLine("  --out-dir logs                             Output directory (default logs)");
        Console.WriteLine("  --prefix cadex_raw                         File name prefix (default cadex_raw)");
        Console.WriteLine("  --csv true|false                           CSV mode (default false)");
        Console.WriteLine("  --reconnect true|false                     Auto reconnect (default true)");
        Console.WriteLine("  --reconnect-delay-ms 2000                  Reconnect delay ms (default 2000)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run --project src/CBAD -- --list-ports");
        Console.WriteLine("  dotnet run --project src/CBAD -- --port COM4 --baud 19200");
        Console.WriteLine("  dotnet run --project src/CBAD -- --port COM4 --csv true");
    }
}
