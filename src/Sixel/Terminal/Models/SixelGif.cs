using System.Collections.Generic;
using System.IO;
using SixLabors.ImageSharp;

using Sixel.Protocols;

namespace Sixel.Terminal.Models;
/// <summary>
/// Gif in sixel format.
/// </summary>
public class SixelGif
{
  /// <summary>
  /// The delay in milliseconds between each frame.
  /// </summary>
  public int Delay { get; set; }
  /// <summary>
  /// The number of times the gif should loop.
  /// </summary>
  public int LoopCount { get; set; }
  /// <summary>
  /// The height of the gif, in characters.
  /// </summary>
  public int Height { get; set; }
  /// <summary>
  /// The width of the gif, in characters.
  /// </summary>
  public int Width { get; set; }
  /// <summary>
  /// The audio data for the gif, optional
  /// </summary>
  public string? Audio { get; set; }
  /// <summary>
  /// The sixel data for each frame of the gif.
  /// </summary>
  internal List<string> Sixel { get; set; } = new List<string>();
  /// <summary>
  /// The number of frames in the gif.
  /// </summary>
  public int FrameCount => Sixel.Count;
}
