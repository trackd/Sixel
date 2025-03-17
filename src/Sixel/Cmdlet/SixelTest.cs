using System.Management.Automation;
using Sixel.Terminal;
using Sixel.Terminal.Models;
using System.Net.Http;

namespace Sixel.Cmdlet;

[Cmdlet(VerbsData.ConvertTo, "SixelTest", DefaultParameterSetName = "Path")]
[OutputType(typeof(string))]
public sealed class ConvertSixelTestCmdlet : PSCmdlet
{
  [Parameter(
        HelpMessage = "A path to a local image to convert to sixel.",
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
        HelpMessage = "A URL of the image to download and convert to sixel.",
        Mandatory = true,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        ParameterSetName = "Url"
  )]
  [ValidateNotNullOrEmpty]
  [Alias("Uri")]
  public Uri Url { get; set; } = null!;

  [Parameter(
        HelpMessage = "A stream of the image to convert to sixel.",
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
        HelpMessage = "Choose ImageProtocol to use for conversion."
  )]
  public ImageProtocol Protocol { get; set; } = Compatibility.GetTerminalInfo().Protocol;

  [Parameter(
        HelpMessage = "Xterm specific options for cursor hiding and restoring."
  )]
  public SwitchParameter Xterm { get; set; }
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
        if (!Xterm.IsPresent)
        {
        WriteObject(
        Load.ConsoleImage(
              Protocol,
              imageStream,
              MaxColors,
              Width,
              Force.IsPresent));
        }
        else
        {
              // cursor shenanigans .. sigh just temporary testing.
              // var size = SizeHelper.GetTerminalImageSize(imageStream, Width);
              Console.CursorVisible = false;
              Console.Write(Constants.HideCursor);
              // Console.Write(Constants.DECSDM);
              Console.Write(Constants.xtermHideCursor);
              WriteObject(
              Load.ConsoleImage(
                    Protocol,
                    imageStream,
                    MaxColors,
                    Width,
                    Force.IsPresent));
              // Console.Write($"{Constants.ESC}[{size.Height}B");
              Console.CursorVisible = true;
              Console.Write(Constants.ShowCursor);
              Console.Write(Constants.xtermShowCursor);
        }
      }
    }
    catch (Exception ex)
    {
      WriteError(new ErrorRecord(ex, "SixelError", ErrorCategory.NotSpecified, null));
    }
  }
}
