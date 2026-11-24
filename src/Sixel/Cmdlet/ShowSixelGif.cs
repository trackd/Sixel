using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;
using Sixel.Protocols;
using Sixel.Terminal;
using Sixel.Terminal.Models;

namespace Sixel.Cmdlet;

/// <summary>
/// this cmdlet is not exported, it's only used in the formatting.
/// this is mostly for ease of use, could potentially break in the future.
/// this cmdlet uses Console.Write and is not captureable by the pipeline.
/// </summary>

[Cmdlet(VerbsCommon.Show, "SixelGif")]
[OutputType(typeof(void))]
public sealed class ShowSixelGifCmdlet : PSCmdlet {
    // The GIF played last: (runspace, history id). One command line has one history
    // id, so a match means this GIF follows another in the same output.
    private static (Guid Runspace, long HistoryId) s_lastPlayed;

    [Parameter(
        HelpMessage = "SixelGif object to play.",
        Mandatory = true,
        ValueFromPipeline = true,
        Position = 0
    )]
    [ValidateNotNullOrEmpty]
    public SixelGif? Gif { get; set; }

    [Parameter(
        HelpMessage = "History id of the command line that output the GIF. The format view passes its own; this cmdlet's is always -1 there.",
        DontShow = true
    )]
    public long HistoryId { get; set; } = -1;
    protected override void ProcessRecord() {
        try {
            if (Gif is null) return;
            (Guid Runspace, long HistoryId) current = (Runspace.DefaultRunspace?.InstanceId ?? Guid.Empty, HistoryId);
            // Consecutive GIFs share the blank row the previous one left below itself.
            // Without a history id there is no way to tell, so always lead with a blank row.
            bool leadingBlankLine = HistoryId < 0 || current != s_lastPlayed;
            using CancellationTokenSource cancellation = new();
            // Handle Ctrl+C
            ConsoleCancelEventHandler handler = (sender, args) => {
                args.Cancel = true;
                cancellation.Cancel();
            };
            Console.CancelKeyPress += handler;
            try {
                GifToSixel.PlaySixelGif(Gif, leadingBlankLine, cancellation.Token);
                s_lastPlayed = current;
            }
            finally {
                Console.CancelKeyPress -= handler;
            }
        }
        catch (Exception ex) {
            WriteError(new ErrorRecord(ex, "ShowSixelGifCmdlet", ErrorCategory.NotSpecified, MyInvocation.BoundParameters));
        }
    }
}
