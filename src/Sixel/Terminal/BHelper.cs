namespace Sixel.Terminal;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
internal static class BHelper
{
#if NET472
  internal static bool IsBase64String(string input)
{
    if (string.IsNullOrWhiteSpace(input))
    {
        return false;
    }
    // Remove any whitespace or line breaks
    input = input.Trim();
    // Check if the length is a multiple of 4
    if (input.Length % 4 != 0)
    {
        return false;
    }
    // Try to decode the string
    try
    {
        Convert.FromBase64String(input);
        return true;
    }
    catch (FormatException)
    {
        return false;
    }
}
#elif NET5_0_OR_GREATER
  internal static bool IsBase64String(string input)
  {
    // Check if the string is a valid Base64 string
    Span<byte> buffer = new Span<byte>(new byte[input.Length]);
    return Convert.TryFromBase64String(input, buffer, out _);
  }
#endif
}
