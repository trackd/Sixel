#if NET472
namespace Sixel.Terminal.Models;

/// <summary>
/// Extension methods for KeyValuePair to support deconstruction in .NET Framework 4.7.2.
/// </summary>
internal static class KeyValuePairExtensions
{
        internal static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> kvp, out TKey key, out TValue value)
        {
            key = kvp.Key;
            value = kvp.Value;
    }
}
#endif
