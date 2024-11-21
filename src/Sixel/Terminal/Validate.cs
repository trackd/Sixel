using System.Management.Automation;
namespace Sixel.Terminal;

class ValidateTerminalWidth : ValidateArgumentsAttribute
{
  /// <summary>
  /// Validates that the requested Width is not greater than the terminal width.
  /// </summary>
  protected override void Validate(object arguments, EngineIntrinsics engineIntrinsics)
  {
    var requestedWidth = (int)arguments;
    var hostWidth = engineIntrinsics.Host.UI.RawUI.WindowSize.Width;
    if (requestedWidth > hostWidth)
    {
      throw new ValidationMetadataException($"{requestedWidth} width is greater than terminal width ({hostWidth}).");
    }
  }
}
