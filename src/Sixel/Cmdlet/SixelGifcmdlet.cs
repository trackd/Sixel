using Sixel.Terminal;
using Sixel.Terminal.Models;
using Sixel.Protocols;
using System.Management.Automation;
using System.Threading;

namespace Sixel.Cmdlet;

[Cmdlet(VerbsData.ConvertTo, "SixelGif", DefaultParameterSetName = "Path")]
[Alias("gif")]
[OutputType(typeof(SixelGif))]
public sealed class ConvertSixelGifCmdlet : PSCmdlet
{
  [Parameter(
        HelpMessage = "A path to a local image to convert to sixel.",
        Mandatory = true,
        ValueFromPipelineByPropertyName = true,
        Position = 0,
        ParameterSetName = "Path"
  )]
  [ValidateNotNullOrEmpty]
  [Alias("FullName")]
  public string Path { get; set; } = null!;

  [Parameter(
        HelpMessage = "A URL of the image to download and convert to sixel.",
        Mandatory = true,
        ValueFromPipeline = true,
        ParameterSetName = "Url"
  )]
  [ValidateNotNullOrEmpty]
  [Alias("Uri")]
  public string Url { get; set; } = null!;

  [Parameter(
        HelpMessage = "The maximum number of colors to use in the image."
  )]
  [ValidateRange(1, 256)]
  public int MaxColors { get; set; } = 256;

  [Parameter(
        HelpMessage = "Width of the image in character cells, the height will be scaled to maintain aspect ratio."
  )]
  [ValidateTerminalWidth()]
  public int Width { get; set; }

  [Parameter(
        HelpMessage = "Force the command to attempt to output sixel data even if the terminal does not support sixel."
  )]
  public SwitchParameter Force { get; set; }

  [Parameter(
      HelpMessage = "The number of times to loop the gif."
  )]
  [ValidateRange(1, 256)]
  public int LoopCount { get; set; } = 3;
  [Parameter(
        HelpMessage = "The audio track to overlay the gif"
  )]
  public string? AudioFile { get; set; }
  protected override void ProcessRecord()
  {
    try
    {
      Stream? imageStream = null;
      switch (ParameterSetName)
      {
        case "Path":
          {
            var resolvedPath = SessionState.Path.GetResolvedPSPathFromPSPath(Path)[0].Path;
            imageStream = new FileStream(resolvedPath, FileMode.Open, FileAccess.Read);
            break;
          }
        case "Url":
          {
            using var client = new HttpClient();
            var response = client.GetAsync(Url).Result;
            response.EnsureSuccessStatusCode();
            imageStream = response.Content.ReadAsStream();
            break;
          }
      }

      if (imageStream is null) return;

      using (imageStream)
      {
        if (AudioFile is not null)
        {
          var resolvedAudio = SessionState.Path.GetResolvedPSPathFromPSPath(AudioFile)[0].Path;
          WriteObject(GifToSixel.LoadGif(imageStream, MaxColors, Width, LoopCount, resolvedAudio));
        }
        else {
          WriteObject(GifToSixel.LoadGif(imageStream, MaxColors, Width, LoopCount));
        }
      }
    }
    catch (Exception ex)
    {
      WriteError(new ErrorRecord(ex, "SixelError", ErrorCategory.NotSpecified, null));
    }
  }
}

[Cmdlet(VerbsCommon.Show, "SixelGif", DefaultParameterSetName = "Path")]
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
      CancellationTokenSource cts = new CancellationTokenSource();
      // Handle Ctrl+C
      Console.CancelKeyPress += (sender, args) =>
      {
        args.Cancel = true;
        cts.Cancel();
      };
        GifToSixel.PlaySixelGif(Gif, cts.Token);
    }
    catch (Exception ex)
    {
      WriteError(new ErrorRecord(ex, "SixelError", ErrorCategory.NotSpecified, null));
    }
  }
}
