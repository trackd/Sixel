using System.Collections.Generic;

namespace Sixel.Terminal.Models
{
    internal static class KeyValuePairExtensions
    {
        internal static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> kvp, out TKey key, out TValue value)
        {
            key = kvp.Key;
            value = kvp.Value;
        }
    }
}
