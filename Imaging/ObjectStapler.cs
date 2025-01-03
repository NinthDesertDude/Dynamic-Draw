using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DynamicDraw.Imaging
{
    internal static class ObjectStapler
    {
        private static readonly ConditionalWeakTable<object, HashSet<object>> staples = new();

        /// <summary>
        /// Creates a virtual reference from the source to the target. This ensures that the 
        /// target will not be garbage collected so long as the source is still referenced.
        /// </summary>
        public static bool Add(object source, object target)
        {
            while (true)
            {
                if (!staples.TryGetValue(source, out HashSet<object> sourceStaples))
                {
                    staples.TryAdd(source, new HashSet<object>());
                    continue;
                }

                lock (sourceStaples)
                {
                    return sourceStaples.Add(target);
                }
            }
        }
    }
}
