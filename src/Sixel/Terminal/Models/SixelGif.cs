using System.Collections.Generic;
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
  /// The audio data for the gif.
  /// </summary>
  // public string? Audio { get; set; }
  /// <summary>
  /// The sixel data for each frame of the gif.
  /// </summary>
  public List<string> Sixel { get; set; } = new List<string>();
  /// <summary>
  /// this is just a hacky way to autoplay when outputting the object to the console.
  /// </summary>
  /// <returns>Console Write :|</returns>
  // public override string ToString()
  // {
  //   GifToSixel.PlaySixelGif(this);
  //   return string.Empty;
  // }
}
