namespace CBAD.Parsing;

internal sealed class LineReassembler
{
    private readonly object _lock = new();
    private string _buffer = string.Empty;

    /// <summary>Feeds a raw chunk; returns zero or more complete lines.</summary>
    public IReadOnlyList<string> Feed(string chunk)
    {
        lock (_lock)
        {
            _buffer += chunk;
            var lines = new List<string>();

            int idx;
            while ((idx = _buffer.IndexOf('\n')) >= 0)
            {
                var line = _buffer[..idx].TrimEnd('\r');
                _buffer = _buffer[(idx + 1)..];
                lines.Add(line);
            }

            return lines;
        }
    }
}
