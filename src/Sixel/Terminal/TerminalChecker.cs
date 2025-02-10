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
        TerminalInfo? envTerminalInfo = null;

        // First check environment variables to identify the terminal
        foreach (DictionaryEntry item in env)
        {
            var key = item.Key?.ToString();
            var value = item.Value?.ToString();

            if (key == "TERM_PROGRAM" && value != null && Helpers.GetTerminal(value) is Terminals terminal)
            {
                if (Helpers.SupportedProtocol.TryGetValue(terminal, out var protocol))
                {
                    envTerminalInfo = new TerminalInfo
                    {
                        Terminal = terminal,
                        Protocol = protocol
                    };
                    break;
                }
            }

            if (key != null && Helpers.GetTerminal(key) is Terminals _terminal)
            {
                if (Helpers.SupportedProtocol.TryGetValue(_terminal, out var protocol))
                {
                    envTerminalInfo = new TerminalInfo
                    {
                        Terminal = _terminal,
                        Protocol = protocol
                    };
                    break;
                }
            }
        }

        // Then check VT capabilities and override protocol if supported
        if (Compatibility._terminalSupportsKitty ?? false)
        {
            return new TerminalInfo
            {
                Terminal = envTerminalInfo?.Terminal ?? Terminals.Kitty,
                Protocol = ImageProtocol.KittyGraphicsProtocol
            };
        }

        if (Compatibility._terminalSupportsSixel ?? false)
        {
            return new TerminalInfo
            {
                Terminal = envTerminalInfo?.Terminal ?? Terminals.MicrosoftTerminal,
                Protocol = ImageProtocol.Sixel
            };
        }

        // Return environment-detected terminal or fallback to unknown
        return envTerminalInfo ?? new TerminalInfo
        {
            Terminal = Terminals.unknown,
            Protocol = ImageProtocol.Blocks
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
            Protocol = ImageProtocol.Blocks
        };
    }
}
