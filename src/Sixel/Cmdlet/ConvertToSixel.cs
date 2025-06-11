using System.Management.Automation;
using System.Net.Http;
using Sixel.Terminal;
using Sixel.Terminal.Models;

namespace Sixel.Cmdlet;

[Cmdlet(VerbsData.ConvertTo, "Sixel", DefaultParameterSetName = "Path")]
[Alias("cts", "ConvertTo-InlineImage", "ConvertTo-KittyImage")]
[OutputType(typeof(string))]
public sealed class ConvertSixelCmdlet : PSCmdlet
{
  [Parameter(
      HelpMessage = "InputObject from Pipeline, can be filepath or base64 encoded image.",
      Mandatory = true,
      ValueFromPipeline = true,
      ParameterSetName = "InputObject"
  )]
  [ValidateNotNullOrEmpty]
  public string? InputObject { get; set; }
  [Parameter(
        HelpMessage = "A path to a local image to convert to sixel.",
        Mandatory = true,
        ValueFromPipelineByPropertyName = true,
        Position = 0,
        ParameterSetName = "Path"
  )]
  [ValidateNotNullOrEmpty]
  [Alias("FullName")]
  public string? Path { get; set; }

  [Parameter(
        HelpMessage = "A URL of the image to download and convert to sixel.",
        Mandatory = true,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        Position = 0,
        ParameterSetName = "Url"
  )]
  [ValidateNotNullOrEmpty]
  [Alias("Uri")]
  public Uri? Url { get; set; }

  [Parameter(
        HelpMessage = "A stream of the image to convert to sixel.",
        Mandatory = true,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        Position = 0,
        ParameterSetName = "Stream"
  )]
  [ValidateNotNullOrEmpty]
  [Alias("RawContentStream", "FileStream", "InputStream", "ImageStream", "ContentStream")]
  public Stream? Stream { get; set; }

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
        HelpMessage = "Height of the image in character cells, the width will be scaled to maintain aspect ratio."
  )]
  [ValidateTerminalHeight()]
  public int Height { get; set; }

  [Parameter(
        HelpMessage = "Force the command to attempt to output sixel data even if the terminal does not support sixel."
  )]
  public SwitchParameter Force { get; set; }

  [Parameter(
        HelpMessage = "Choose ImageProtocol to use for conversion."
  )]
  // public ImageProtocol Protocol { get; set; } = Compatibility.GetTerminalInfo().Protocol?.FirstOrDefault() ?? ImageProtocol.Blocks;
  public ImageProtocol Protocol { get; set; } = ImageProtocol.Auto;

  protected override void ProcessRecord()
  {
    Stream? imageStream = null;
    try
    {
      switch (ParameterSetName)
      {
        case "InputObject":
          {
            if (InputObject is not null && InputObject.Length > 512)
            {
              // assume it's a base64 encoded image
              if (InputObject.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase))
              {
                // Length of "data:image/png;base64," = 22
                InputObject = InputObject.Substring(22);
              }
              imageStream = new MemoryStream(Convert.FromBase64String(InputObject));
            }
            else
            {
              /// assume it's a path to a file
              var resolvedPath = SessionState.Path.GetUnresolvedProviderPathFromPSPath(InputObject);
              imageStream = new FileStream(resolvedPath, FileMode.Open, FileAccess.Read);
            }
            break;
          }
        case "Path":
          {
            var resolvedPath = SessionState.Path.GetUnresolvedProviderPathFromPSPath(Path);
            imageStream = new FileStream(resolvedPath, FileMode.Open, FileAccess.Read);
          }
          break;
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
            if (Stream is not null && Stream.Position != 0)
            {
              // if something has already read the stream, reset it.. maybe risky
              Stream.Position = 0;
            }
            imageStream = Stream;
            break;
          }
      }
      if (imageStream is null)
      {
        return;
      }

      (ImageSize size, string image) = ConvertTo.ConsoleImage(
        Protocol,
        imageStream,
        MaxColors,
        Width,
        Height,
        Force.IsPresent
      );
      /// add the ImageSize as noteproperty on the string object
      var wrappedImage = PSObject.AsPSObject(image);
      // wrappedImage.Properties.Add(new PSNoteProperty("Width", size.CellWidth));
      // wrappedImage.Properties.Add(new PSNoteProperty("Height", size.CellHeight));
      wrappedImage.Properties.Add(new PSNoteProperty("ImageSize", size));
      WriteObject(wrappedImage);
    }
    catch (Exception ex)
    {
      WriteError(new ErrorRecord(ex, "ConvertSixelCmdlet", ErrorCategory.NotSpecified, null));
    }
    finally
    {
      if (ParameterSetName != "Stream" && imageStream is not null)
      {
        imageStream.Dispose();
      }
    }
  }
}
