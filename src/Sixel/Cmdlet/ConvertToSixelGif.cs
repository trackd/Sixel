using Sixel.Terminal;
using Sixel.Terminal.Models;
using Sixel.Protocols;
using System.Management.Automation;
using System.Net.Http;

namespace Sixel.Cmdlet;

[Cmdlet(VerbsData.ConvertTo, "SixelGif", DefaultParameterSetName = "Path")]
[Alias("gif")]
[OutputType(typeof(SixelGif))]
public sealed class ConvertSixelGifCmdlet : PSCmdlet
{
  [Parameter(
        HelpMessage = "A path to a local gif to convert to a sixel object.",
        Mandatory = true,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        Position = 0,
        ParameterSetName = "Path"
  )]
  [ValidateNotNullOrEmpty]
  [Alias("FullName")]
  public string Path { get; set; } = null!;

  [Parameter(
        HelpMessage = "An URL of the gif to download and convert to a sixel object.",
        Mandatory = true,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        ParameterSetName = "Url"
  )]
  [ValidateNotNullOrEmpty]
  [Alias("Uri")]
  public Uri Url { get; set; } = null!;

  [Parameter(
      HelpMessage = "A stream of the gif to convert to a sixel object.",
      Mandatory = true,
      ValueFromPipeline = true,
      ValueFromPipelineByPropertyName = true,
      Position = 0,
      ParameterSetName = "Stream"
)]
  [ValidateNotNullOrEmpty]
  [Alias("FileStream", "InputStream", "ImageStream", "ContentStream")]
  public Stream Stream { get; set; } = null!;

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
  // [Parameter(
  //       HelpMessage = "The audio track to overlay the gif"
  // )]
  // [ValidateNotNullOrEmpty]
  // [Alias("AudioFile")]
  // public string? AudioPath { get; set; }
  protected override void ProcessRecord()
  {
    try
    {
      Stream? imageStream = null;
      switch (ParameterSetName)
      {
        case "Path":
          {
            var resolvedPath = SessionState.Path.GetUnresolvedProviderPathFromPSPath(Path);
            imageStream = new FileStream(resolvedPath, FileMode.Open, FileAccess.Read);
            break;
          }
        case "Url":
          {
            using var client = new HttpClient();
            var response = client.GetAsync(Url).Result;
            response.EnsureSuccessStatusCode();
            // imageStream = response.Content.ReadAsStream();
            imageStream = response.Content.ReadAsStreamAsync().Result;
            break;
          }
        case "Stream":
          {
            if (Stream.Position != 0)
            {
              // if something has already read the stream, reset it.. maybe risky
              Stream.Position = 0;
            }
            imageStream = Stream;
            break;
          }
      }

      if (imageStream is null) return;

      using (imageStream)
      {
        // if (AudioPath is not null)
        // {
        //   var resolvedAudio = SessionState.Path.GetUnresolvedProviderPathFromPSPath(AudioPath);
        //   WriteObject(GifToSixel.LoadGif(imageStream, MaxColors, Width, LoopCount, resolvedAudio));
        // }
        // else {
        WriteObject(GifToSixel.ConvertGif(imageStream, MaxColors, Width, LoopCount));
        // }
      }
    }
    catch (Exception ex)
    {
      WriteError(new ErrorRecord(ex, "ConvertSixelGifCmdlet", ErrorCategory.NotSpecified, null));
    }
  }
}
