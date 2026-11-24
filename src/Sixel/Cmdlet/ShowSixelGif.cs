using System.Management.Automation;
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
    [Parameter(
        HelpMessage = "SixelGif object to play.",
        Mandatory = true,
        ValueFromPipeline = true,
        Position = 0
    )]
    [ValidateNotNullOrEmpty]
    public SixelGif? Gif { get; set; }
    protected override void ProcessRecord() {
        try {
            if (Gif is null) return;
            using CancellationTokenSource cancellation = new();
            // Handle Ctrl+C
            ConsoleCancelEventHandler handler = (sender, args) => {
                args.Cancel = true;
                cancellation.Cancel();
            };
            Console.CancelKeyPress += handler;
            try {
                GifToSixel.PlaySixelGif(Gif, cancellation.Token);
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
