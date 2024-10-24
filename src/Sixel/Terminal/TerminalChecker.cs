using Sixel.Terminal.Models;
using System.Collections;

namespace Sixel.Terminal;
public static class TerminalChecker
{
    /// <summary>
    /// Check the terminal for compatibility.
    /// use enviroment variables to try and figure out which terminal is being used.
    /// </summary>
    internal static TerminalInfo CheckTerminal()
    {
        var env = Environment.GetEnvironmentVariables();
        foreach (DictionaryEntry item in env)
        {
            var key = item.Key?.ToString();
            var value = item.Value?.ToString();

            if (key == "TERM_PROGRAM" && value != null && Helpers.GetTerminal(value) is Terminals terminal)
            {
                if (Helpers.SupportedProtocol.TryGetValue(terminal, out var protocol))
                {
                    return new TerminalInfo
                    {
                        Terminal = terminal,
                        Protocol = protocol
                    };
                }
            }

            if (key != null && Helpers.GetTerminal(key) is Terminals _terminal)
            {
                if (Helpers.SupportedProtocol.TryGetValue(_terminal, out var protocol))
                {
                    return new TerminalInfo
                    {
                        Terminal = _terminal,
                        Protocol = protocol
                    };
                }
            }
        }
        return new TerminalInfo
        {
            Terminal = Terminals.unknown,
            Protocol = ImageProtocol.None
        };
    }
    internal static TerminalInfo CheckTerminal(Terminals terminal)
    {
        if (Helpers.SupportedProtocol.TryGetValue(terminal, out var protocol))
        {
            return new TerminalInfo
            {
                Terminal = terminal,
                Protocol = protocol
            };
        }
        return new TerminalInfo
        {
            Terminal = Terminals.unknown,
            Protocol = ImageProtocol.None
        };
    }
}
