using Sixel.Terminal;
using Sixel.Terminal.Models;
using Sixel.Protocols;
using System.Management.Automation;
using System.Text;


namespace Sixel.Cmdlet;

[Cmdlet(VerbsData.ConvertTo, "Sixel", DefaultParameterSetName = "Path")]
[Alias("cts", "ConvertTo-InlineImage")]
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
            HelpMessage = "Force the command to attempt to output sixel data even if the terminal does not support sixel."
      )]
      public SwitchParameter Force { get; set; }

      [Parameter(
            HelpMessage = "Choose ImageProtocol to use for conversion."
      )]
      public ImageProtocol Protocol { get; set; } = Compatibility.GetTerminalInfo().Protocol;
      protected override void ProcessRecord()
      {
            try
            {
                  if (null != Url)
                  {
                        using var client = new HttpClient();
                        var response = client.GetAsync(Url).Result;
                        response.EnsureSuccessStatusCode();
                        WriteObject(
                        Load.ConsoleImage(
                                    Protocol,
                                    response.Content.ReadAsStream(),
                                    MaxColors,
                                    Width,
                                    Force
                              )
                        );
                  }
                  if (null != Path)
                  {
                        var resolvedPath = SessionState.Path.GetResolvedPSPathFromPSPath(Path)[0].Path;
                        using var imageStream = new FileStream(resolvedPath, FileMode.Open, FileAccess.Read);
                        WriteObject(
                        Load.ConsoleImage(
                                    Protocol,
                                    imageStream,
                                    MaxColors,
                                    Width,
                                    Force
                              )
                        );
                  }
            }
            catch (Exception ex)
            {
                  WriteError(new ErrorRecord(ex, "SixelError", ErrorCategory.NotSpecified, null));
            }
      }
}
