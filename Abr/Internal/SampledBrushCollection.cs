/////////////////////////////////////////////////////////////////////////////////
//
// ABR FileType Plugin for Paint.NET
//
// This software is provided under the MIT License:
//   Copyright (c) 2012, 2013, 2017, 2018 Nicholas Hayes
//
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DynamicDraw.Abr.Internal
{
    internal sealed class SampledBrushCollection : Collection<SampledBrush>
    {
        /// <summary>
        /// Finds the largest diameter brush matching the specified tag.
        /// </summary>
        /// <param name="tag">The tag.</param>
        /// <returns>The largest diameter brush matching the specified tag.</returns>
        public SampledBrush FindLargestBrush(string tag)
        {
            IList<SampledBrush> items = Items;
            SampledBrush brush = null;

            int maxDiameter = 0;
            for (int i = 0; i < items.Count; i++)
            {
                SampledBrush item = items[i];
                if (item.Tag.Equals(tag, StringComparison.Ordinal) && item.Diameter > maxDiameter)
                {
                    maxDiameter = item.Diameter;
                    brush = item;
                }
            }

            return brush;
        }
    }
}
