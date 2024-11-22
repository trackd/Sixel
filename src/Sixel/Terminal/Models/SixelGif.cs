using System.Collections.Generic;
using Sixel.Protocols;
namespace Sixel.Terminal.Models;
/// <summary>
/// Gif in sixel format.
/// </summary>
public class SixelGif
{
  /// <summary>
  /// The sixel data for each frame of the gif.
  /// </summary>
  public List<string> Sixel { get; set; } = new List<string>();
  /// <summary>
  /// The delay in milliseconds between each frame.
  /// </summary>
  public int Delay { get; set; }
  /// <summary>
  /// The number of times the gif should loop.
  /// </summary>
  public int LoopCount { get; set; }

  public int Height { get; set; }
  /// <summary>
  /// The audio data for the gif.
  /// </summary>
  // public string? Audio { get; set; }
  // public static string ToString()
  // {
  //   GifToSixel.PlaySixelGif(this);
  // }
}
