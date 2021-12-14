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

namespace DynamicDraw.Abr.Internal
{
    internal sealed class SampledBrush
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SampledBrush"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="tag">The tag.</param>
        /// <param name="diameter">The diameter.</param>
        public SampledBrush(string name, string tag, int diameter)
        {
            Name = name;
            Tag = tag;
            Diameter = diameter;
        }

        public string Name { get; }

        public string Tag { get; }

        public int Diameter { get; }
    }
}
