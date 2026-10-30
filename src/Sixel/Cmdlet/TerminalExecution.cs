using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Sixel.Terminal;

namespace Sixel.Cmdlet;

/// <summary>Only cmdlet execution may initiate lazy probing; discovery/getters stay passive.</summary>
internal static class TerminalExecution {
    internal static void Prepare(PSCmdlet cmdlet, bool force) {
        if (force || Compatibility.RuntimeProbeAttempted || Console.IsInputRedirected || Console.IsOutputRedirected) return;
        Runspace? runspace = Runspace.DefaultRunspace;
        if (runspace is null || runspace.RunspaceAvailability != RunspaceAvailability.Busy) return;
        try {
            IEnumerable<string> commands = runspace.Debugger.GetCallStack().Select(frame => frame.FunctionName);
            if (!CanProbe(cmdlet.Host.Name, commands)) return;
            Compatibility.EnsureRuntimeProbe();
        }
        catch (InvalidOperationException) { }
        catch (NotSupportedException) { }
    }

    internal static bool CanProbe(string hostName, IEnumerable<string> callStack) =>
        string.Equals(hostName, "ConsoleHost", StringComparison.OrdinalIgnoreCase) &&
        !callStack.Any(name => name.Equals("prompt", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("PSConsoleHostReadLine", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("TabExpansion", StringComparison.OrdinalIgnoreCase));
}
