using PaintDotNet.Collections;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace DynamicDraw
{
    /// <summary>
    /// Extensions to various collections or enumerables in places severely lacking.
    /// </summary>
    public static class CollectionExtensions
    {
        /// <summary>
        /// Casts an enumerable to a list of objects.
        /// </summary>
        public static List<object> EnumToList(this IEnumerable enumerable)
        {
            List<object> result = new();
            foreach (var entry in enumerable)
            {
                result.Add(entry);
            }
            return result;
        }

        /// <summary>
        /// Attempts to cast an enumerable to a list casting to the given type.
        /// </summary>
        public static List<T> EnumToList<T>(this IEnumerable enumerable)
        {
            List<T> result = new();
            foreach (var entry in enumerable)
            {
                result.Add((T)entry);
            }
            return result;
        }

        /// <summary>
        /// Returns the associated value if available, else the default value of its type (null for object types).
        /// </summary>
        public static TValue Get<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key)
        {
            return dict.ContainsKey(key) ? dict[key] : default;
        }

        /// <summary>
        /// Adds or updates the key to the given value.
        /// </summary>
        public static void Set<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, TValue value)
        {
            if (dict.ContainsKey(key))
            {
                dict[key] = value;
            }
            else
            {
                dict.Add(key, value);
            }
        }
    }
}