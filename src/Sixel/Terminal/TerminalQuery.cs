using System.Diagnostics;
using System.Text;

namespace Sixel.Terminal;

internal interface IQueryTransport {
    bool IsRedirected { get; }
    bool KeyAvailable { get; }
    long ElapsedMilliseconds { get; }
    char Read();
    void Write(string text);
    void Wait();
}

internal sealed class ConsoleQueryTransport : IQueryTransport {
    private readonly Stopwatch timer = Stopwatch.StartNew();
    public bool IsRedirected => Console.IsInputRedirected || Console.IsOutputRedirected;
    public bool KeyAvailable => Console.KeyAvailable;
    public long ElapsedMilliseconds => timer.ElapsedMilliseconds;
    public char Read() => Console.ReadKey(intercept: true).KeyChar;
    public void Write(string text) {
        Console.Write(text);
        Console.Out.Flush();
    }
    public void Wait() => Thread.Sleep(1);
}

/// <summary>Bounded, single-attempt terminal queries, used only by explicit callers.</summary>
internal static class TerminalQuery {
    // DA is a fence: unsupported Kitty queries may be ignored, but DA still
    // replies. Both replies must be consumed before returning to the line editor.
    internal const string KittyAndDeviceAttributes = "_Gi=31,s=1,v=1,a=q,t=d,f=24;AAAA\x1b\\\x1b[c";

    internal static string Read(string query, IQueryTransport transport) {
        try {
            // Pending input belongs to the user. Never drain it to make room.
            if (transport.IsRedirected || transport.KeyAvailable)
                return string.Empty;

            bool fenced = query == KittyAndDeviceAttributes;
            var result = new StringBuilder();
            var sequence = new StringBuilder();
            long started = transport.ElapsedMilliseconds;
            transport.Write("\x1b" + query);
            while (transport.ElapsedMilliseconds - started < 500) {
                if (!transport.KeyAvailable) {
                    transport.Wait();
                    continue;
                }
                char value = transport.Read();
                if (sequence.Length == 0 && value != '\x1b') {
                    // Input ownership was lost. Do not keep consuming keys.
                    return string.Empty;
                }
                sequence.Append(value);
                if (sequence.Length > 4096)
                    return string.Empty;
                if (!IsComplete(sequence))
                    continue;

                string response = sequence.ToString();
                sequence.Clear();
                if (fenced) {
                    if (response.StartsWith("\x1b_Gi=31;", StringComparison.Ordinal))
                        result.Append(response);
                    if (IsDeviceAttributes(response))
                        return result.Append(response).ToString();
                }
                else if (Matches(query, response)) {
                    return response;
                }
            }
        }
        catch (InvalidOperationException) { }
        catch (IOException) { }
        // No partial results and no retry: another query would invite late replies.
        return string.Empty;
    }

    private static bool IsDeviceAttributes(string response) =>
        response.StartsWith("\x1b[?", StringComparison.Ordinal) && response[response.Length - 1] == 'c';

    private static bool Matches(string query, string response) {
        return query switch {
            "[c" => IsDeviceAttributes(response),
            "[16t" => Compatibility.TryParseSizeReport(response, 6, out _, out _),
            "[14t" => Compatibility.TryParseSizeReport(response, 4, out _, out _),
            "[18t" => Compatibility.TryParseSizeReport(response, 8, out _, out _),
            "[6n" => response.StartsWith("\x1b[", StringComparison.Ordinal) && response[response.Length - 1] == 'R',
            _ => true,
        };
    }

    private static bool IsComplete(StringBuilder sequence) {
        int length = sequence.Length;
        if (length < 3) return false;
        char last = sequence[length - 1];
        if (sequence[1] == '[')
            return last is >= '@' and <= '~';
        return (last == '\\' && sequence[length - 2] == '\x1b') || (sequence[1] == ']' && last == '\a');
    }
}
