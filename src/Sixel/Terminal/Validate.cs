﻿using System;
using System.Linq;
using System.Reflection;
using SixLabors.ImageSharp.Processing;
using System.Management.Automation;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace Sixel.Terminal;

/// <summary>
/// Argument validation attribute to ensure the requested terminal width does not exceed the actual terminal width.
/// </summary>
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
/// <summary>
/// Argument validation attribute to ensure the requested terminal height does not exceed the actual terminal height.
/// </summary>
internal sealed class ValidateTerminalHeight : ValidateArgumentsAttribute
{
  /// <summary>
  /// Validates that the requested Height is not greater than the terminal height.
  /// </summary>
  protected override void Validate(object arguments, EngineIntrinsics engineIntrinsics)
  {
    var requestedHeight = (int)arguments;
    var hostHeight = Console.WindowHeight;
    if (requestedHeight > hostHeight)
    {
      throw new ValidationMetadataException($"{requestedHeight} height is greater than terminal height ({hostHeight}).");
    }
  }
}
