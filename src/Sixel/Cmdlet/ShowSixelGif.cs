using Sixel.Terminal;
using Sixel.Terminal.Models;
using Sixel.Protocols;
using System.Management.Automation;
using System.Threading;

namespace Sixel.Cmdlet;

/// <summary>
/// this cmdlet is not exported, it's only used in the formatting.
/// this is mostly for ease of use, could potentially break in the future.
/// this cmdlet uses Console.Write and is not captureable by the pipeline.
/// </summary>

[Cmdlet(VerbsCommon.Show, "SixelGif")]
[OutputType(typeof(void))]
public sealed class ShowSixelGifCmdlet : PSCmdlet
{
  [Parameter(
    HelpMessage = "SixelGif object to play.",
    Mandatory = true,
    ValueFromPipeline = true,
    Position = 0
  )]
  [ValidateNotNullOrEmpty]
  public SixelGif? Gif { get; set; }
  protected override void ProcessRecord()
  {
    try
    {
      if (Gif is null) return;
      CancellationTokenSource CancellationToken = new();
      // Handle Ctrl+C
      Console.CancelKeyPress += (sender, args) => {
        args.Cancel = true;
        CancellationToken.Cancel();
      };
      GifToSixel.PlaySixelGif(Gif, CancellationToken.Token);
    }
    catch (Exception ex)
    {
      WriteError(new ErrorRecord(ex, "ShowSixelGifCmdlet", ErrorCategory.NotSpecified, MyInvocation.BoundParameters));
    }
  }
}
