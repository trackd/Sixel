#if NET472
using System.Text;

namespace Sixel.Terminal.Models;
internal static class StringBuilderExtensions
{
  public static StringBuilder Append(this StringBuilder builder, ReadOnlySpan<char> span)
  {
    // NET472 lacks the override for StringBuilder to add the span.
    // We'll need to convert the span to a string for net472.
    return builder.Append(span.ToString());
  }
}
#endif
