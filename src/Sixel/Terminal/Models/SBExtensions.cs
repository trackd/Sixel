#if NET472
using System.Text;

namespace Sixel.Terminal.Models;
internal static class StringBuilderExtensions
{
  public static StringBuilder Append(this StringBuilder builder, ReadOnlySpan<char> span)
  {
    // NET472 acks the override for StringBuilder to add the span. We'll need to convert the span
    // to a string for it, but for .NET 6.0 or newer we'll use the override.
    return builder.Append(span.ToString());
  }
}
#endif
