using System.Management.Automation;
namespace Sixel.Terminal;

internal sealed class ValidateTerminalWidth : ValidateArgumentsAttribute
{
  /// <summary>
  /// Validates that the requested Width is not greater than the terminal width.
  /// </summary>
  protected override void Validate(object arguments, EngineIntrinsics engineIntrinsics)
  {
    var requestedWidth = (int)arguments;
    var hostWidth = Console.WindowWidth;
    if (requestedWidth > hostWidth)
    {
      throw new ValidationMetadataException($"{requestedWidth} width is greater than terminal width ({hostWidth}).");
    }
  }
}
