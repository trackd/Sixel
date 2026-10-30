using System.Diagnostics;
using System.Text;
using Sixel.Protocols;

namespace Sixel.Terminal;

internal interface IQueryTransport {
    bool IsRedirected { get; }
    bool KeyAvailable { get; }
    long ElapsedMilliseconds { get; }
    char Read();
    void Write(string text);
    void Wait();
}

internal sealed class ConsoleQueryTransport : IQueryTransport, IQueryInputMode {
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
    public IDisposable EnterQueryMode() => new QueryInputMode();
}

/// <summary>Bounded queries used by explicit refresh or the first eligible cmdlet execution.</summary>
internal static class TerminalQuery {
    internal const int TimeoutMilliseconds = 1500;
    // DA is a fence: unsupported Kitty queries may be ignored, but DA still
    // replies. Both replies must be consumed before returning to the line editor.
    internal const string KittyAndDeviceAttributes = "_Gi=31,s=1,v=1,a=q,t=d,f=24;AAAA\x1b\\\x1b[c";
    // One batch, one timeout, one trailing DA fence. Do not start additional
    // queries after a timeout: delayed replies could then reach the line editor.
    internal const string RuntimeProbe = "_Gi=31,s=1,v=1,a=q,t=d,f=24;AAAA\x1b\\" +
        "\x1b[>q\x1b[16t\x1b[14t\x1b[18t\x1b[c";

    internal static string Read(string query, IQueryTransport transport) {
        try {
            // Pending input belongs to the user. Never drain it to make room.
            if (transport.IsRedirected || transport.KeyAvailable)
                return string.Empty;

            using IDisposable? inputMode = (transport as IQueryInputMode)?.EnterQueryMode();

            bool fenced = query is KittyAndDeviceAttributes or RuntimeProbe;
            var result = new StringBuilder();
            var sequence = new StringBuilder();
            long started = transport.ElapsedMilliseconds;
            transport.Write("\x1b" + query);
            while (transport.ElapsedMilliseconds - started < TimeoutMilliseconds) {
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
                    if (IsKittyQueryResponse(response, out _))
                        result.Append(response);
                    else if (query == RuntimeProbe && (response.StartsWith("\x1bP>|", StringComparison.Ordinal) ||
                        Compatibility.TryParseSizeReport(response, 6, out _, out _) ||
                        Compatibility.TryParseSizeReport(response, 4, out _, out _) ||
                        Compatibility.TryParseSizeReport(response, 8, out _, out _)))
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

    internal static bool IsKittyQueryResponse(string response, out bool success) {
        bool matched = KittyResponse.TryParse(response, out KittyResponse? reply) && reply?.ImageId == 31;
        success = matched && reply!.Success;
        return matched;
    }

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
