using System.Management.Automation;
using Sixel.Terminal;

namespace Sixel;

[Cmdlet(VerbsData.ConvertTo, "Sixel", DefaultParameterSetName = "Path")]
[Alias("cts")]
[OutputType(typeof(string))]
public sealed class ConvertSixelCmdlet : PSCmdlet
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
      HelpMessage = "Width of the image in Pixels, the height will be scaled to maintain aspect ratio."
  )]
  public int PixelWidth { get; set; }

  [Parameter(
        HelpMessage = "Force the command to attempt to output sixel data even if the terminal does not support sixel."
  )]
  public SwitchParameter Force { get; set; }
  protected override void BeginProcessing()
  {
    if (Compatibility.TerminalSupportsSixel() == false && Force == false)
    {
      ThrowTerminatingError(new ErrorRecord(new System.Exception("Terminal does not support sixel, override with -Force"), "SixelError", ErrorCategory.NotImplemented, null));
    }
  }
  protected override void ProcessRecord()
  {
    string? tempFilePath = null;
    try
    {
      if (null != Url)
      {
        tempFilePath = System.IO.Path.GetTempFileName();
        using (var client = new HttpClient())
        {
          var response = client.GetAsync(Url).Result;
          response.EnsureSuccessStatusCode();
          using (var fileStream = System.IO.File.OpenWrite(tempFilePath))
          {
            response.Content.ReadAsStream().CopyTo(fileStream);
          }
        }
        Path = tempFilePath;
      }
      var resolvedPath = SessionState.Path.GetResolvedPSPathFromPSPath(Path)[0].Path;
      if (Width > 0)
      {
        WriteObject(Convert.ImgToSixel(resolvedPath, MaxColors, Width));
        return;
      }
      if (PixelWidth > 0)
      {
        WriteObject(Convert.ImgToSixelPixelWidth(resolvedPath, MaxColors, PixelWidth));
        return;
      }
      WriteObject(Convert.ImgToSixel(resolvedPath, MaxColors));
    }
    catch (Exception ex)
    {
      WriteError(new ErrorRecord(ex, "SixelError", ErrorCategory.NotSpecified, null));
    }
    finally
    {
      if (!string.IsNullOrEmpty(tempFilePath))
      {
        System.IO.File.Delete(tempFilePath);
      }
    }
  }
}
