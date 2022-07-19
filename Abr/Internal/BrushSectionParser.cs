/////////////////////////////////////////////////////////////////////////////////
//
// ABR FileType Plugin for Paint.NET
//
// This software is provided under the MIT License:
//   Copyright (c) 2012, 2013, 2017, 2018, 2022 Nicholas Hayes
//
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

using System;
using System.Globalization;
using System.Text;

namespace DynamicDraw.Abr.Internal
{
    /// <summary>
    /// Parser for the 8BIM sections used by ABR version 6 and later.
    /// </summary>
    internal sealed class BrushSectionParser
    {
        private readonly BrushSectionOffsets sectionOffsets;
        private readonly SampledBrushCollection sampledBrushes;

        /// <summary>
        /// Initializes a new instance of the <see cref="BrushSectionParser"/> class.
        /// </summary>
        /// <param name="reader">The reader.</param>
        public BrushSectionParser(BigEndianBinaryReader reader)
        {
            sectionOffsets = GetBrushSectionOffsets(reader);
            sampledBrushes = new SampledBrushCollection();

            if (sectionOffsets != null && sectionOffsets.descriptorSectionOffset >= 0)
            {
                reader.Position = sectionOffsets.descriptorSectionOffset;
                ParseBrushDescriptorSection(reader);
            }
        }

        private enum DescriptorTypes : uint
        {
            Alias = 0x976c6973, // 'alis'
            Boolean = 0x626f6f6c, // 'bool'
            String = 0x54455854, // 'TEXT'
            Class = 0x74797065, // 'type'
            Descriptor = 0x4f626a63, // 'Objc'
            Enumerated = 0x656e756d, // 'enum'
            Float = 0x646f7562, // 'doub'
            Integer = 0x6c6f6e67, // 'long'
            List = 0x566c4c73, // 'VlLs'
            Null = 0x6e756c6c, // 'null'
            ObjectRefrence = 0x6f626a20, // 'obj '
            Path = 0x50746820, // 'Pat '
            UnitFloat = 0x556e7446, // 'UntF'
        }

        private enum UnitTypes : uint
        {
            Angle = 0x23416e67, // '#Ang'
            Density = 0x2352736c, // '#Rsl'
            Distance = 0x23526c74,// '#Rlt'
            None = 0x234e6e65, // '#Nne'
            Percent = 0x23507263, // '#Prc'
            Pixel = 0x2350786c // '#Pxl'
        }

        /// <summary>
        /// Gets a collection containing the sampled brush information.
        /// </summary>
        /// <value>
        /// The sampled brush information.
        /// </value>
        public SampledBrushCollection SampledBrushes
        {
            get
            {
                return sampledBrushes;
            }
        }

        /// <summary>
        /// Gets the sample section offset.
        /// </summary>
        /// <value>
        /// The sample section offset.
        /// </value>
        public long SampleSectionOffset
        {
            get
            {
                return sectionOffsets?.sampleSectionOffset ?? -1;
            }
        }

        private static BrushSectionOffsets GetBrushSectionOffsets(BigEndianBinaryReader reader)
        {
            const uint PhotoshopSignature = 0x3842494D; // 8BIM
            const uint SampleSectionId = 0x73616D70; // samp
            const uint DescriptorSectionId = 0x64657363; // desc

            long sampleSectionOffset = -1;
            long descriptorSectionOffset = -1;

            while (reader.Position < reader.Length)
            {
                uint sig = reader.ReadUInt32();

                if (sig != PhotoshopSignature)
                {
                    break;
                }

                uint sectionId = reader.ReadUInt32();

                switch (sectionId)
                {
                    case SampleSectionId:
                        sampleSectionOffset = reader.Position;
                        break;
                    case DescriptorSectionId:
                        descriptorSectionOffset = reader.Position;
                        break;
                }

                uint size = reader.ReadUInt32();

                reader.Position += size;
            }

            return new BrushSectionOffsets(sampleSectionOffset, descriptorSectionOffset);
        }

        private void ParseBrushDescriptorSection(BigEndianBinaryReader reader)
        {
            uint sectionSize = reader.ReadUInt32();

            long sectionEnd = reader.Position + sectionSize;
            // Skip the unknown data.
            reader.Position += 22L;

            if (reader.Position < sectionEnd)
            {
                string key = ParseKey(reader);
                DescriptorTypes type = (DescriptorTypes)reader.ReadUInt32();

#if DEBUG
                System.Diagnostics.Debug.WriteLine(string.Format(
                    CultureInfo.CurrentCulture,
                    "Item: {0} ({1}) at {2:X8}",
                    new object[] { key, type, reader.Position }));
#endif

                ParseType(reader, type);
            }
        }

        private static string ParseKey(BigEndianBinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length == 0)
            {
                length = 4;
            }

            byte[] bytes = reader.ReadBytes(length);

            return Encoding.ASCII.GetString(bytes);
        }

        private static string ParseClassId(BigEndianBinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length == 0)
            {
                length = 4;
            }

            return Encoding.ASCII.GetString(reader.ReadBytes(length)).TrimEnd('\0');
        }

        private void ParseList(BigEndianBinaryReader reader)
        {
            uint count = reader.ReadUInt32();
            for (int i = 0; i < count; i++)
            {
                DescriptorTypes type = (DescriptorTypes)reader.ReadUInt32();

                ParseType(reader, type);
            }
        }

        private void ParseDescriptor(BigEndianBinaryReader reader)
        {
            string name = ParseString(reader);

            string classId = ParseClassId(reader);

            uint count = reader.ReadUInt32();

#if DEBUG
            System.Diagnostics.Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Parsing descriptor '{0}' ({1} items)", classId, count));
#endif
            if (classId.Equals("brushPreset", StringComparison.Ordinal))
            {
                ParseBrushPreset(reader, count);
            }
            else
            {
                for (uint i = 0; i < count; i++)
                {
                    string key = ParseKey(reader);
                    DescriptorTypes type = (DescriptorTypes)reader.ReadUInt32();

#if DEBUG
                    System.Diagnostics.Debug.WriteLine(string.Format(
                        CultureInfo.CurrentCulture,
                        "Item {0}: {1} ({2}) at 0x{3:X8}",
                        new object[] { i, key, type, reader.Position }));
#endif
                    ParseType(reader, type);
                }
            }
        }

        private void ParseBrushPreset(BigEndianBinaryReader reader, uint count)
        {
            string presetName = null;
            BrushData brushData = null;

            for (uint i = 0; i < count; i++)
            {
                string key = ParseKey(reader);
                DescriptorTypes type = (DescriptorTypes)reader.ReadUInt32();

#if DEBUG
                System.Diagnostics.Debug.WriteLine(string.Format(
                    CultureInfo.CurrentCulture,
                    "brushPreset item {0}: {1} ({2}) at 0x{3:X8}",
                    new object[] { i, key, type, reader.Position }));
#endif
                if (key.Equals("Nm  ", StringComparison.Ordinal))
                {
                    presetName = ParseString(reader);
                }
                else if (key.Equals("Brsh", StringComparison.Ordinal) && type == DescriptorTypes.Descriptor)
                {
                    brushData = ParseBrushDescriptor(reader);
                }
                else
                {
                    ParseType(reader, type);
                }
            }

            if (brushData != null && !string.IsNullOrEmpty(brushData.sampledDataTag))
            {
                sampledBrushes.Add(new SampledBrush(presetName, brushData.sampledDataTag, brushData.diameter));
            }
        }

        private BrushData ParseBrushDescriptor(BigEndianBinaryReader reader)
        {
            string name = ParseString(reader);

            string classId = ParseClassId(reader);

            uint count = reader.ReadUInt32();

#if DEBUG
            System.Diagnostics.Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Parsing {0} ({1} items)", classId, count));
#endif

            BrushData data = null;

            if (classId.Equals("sampledBrush", StringComparison.Ordinal))
            {
                data = ParseSampledBrush(reader, count);
            }
            else
            {
                for (uint i = 0; i < count; i++)
                {
                    string key = ParseKey(reader);
                    DescriptorTypes type = (DescriptorTypes)reader.ReadUInt32();

#if DEBUG
                    System.Diagnostics.Debug.WriteLine(string.Format(
                        CultureInfo.CurrentCulture,
                        "{0} item {1}: {2} ({3}) at 0x{4:X8}",
                        new object[] { classId, i, key, type, reader.Position }));
#endif
                    ParseType(reader, type);
                }
            }

            return data;
        }

        private BrushData ParseSampledBrush(BigEndianBinaryReader reader, uint count)
        {
            BrushData data = new BrushData();

            for (uint i = 0; i < count; i++)
            {
                string key = ParseKey(reader);

                DescriptorTypes type = (DescriptorTypes)reader.ReadUInt32();

#if DEBUG
                System.Diagnostics.Debug.WriteLine(string.Format(
                    CultureInfo.CurrentCulture,
                    "sampledBrush item {0}: {1} ({2}) at 0x{3:X8}",
                    new object[] { i, key, type, reader.Position }));
#endif
                UnitFloat unitFloat;
                switch (key)
                {
                    case "Dmtr":
                        unitFloat = ParseUnitFloat(reader);
                        if (unitFloat.type == UnitTypes.Pixel)
                        {
                            data.diameter = (int)unitFloat.value;
                        }
                        break;
                    case "sampledData":
                        data.sampledDataTag = ParseString(reader);
                        break;
                    default:
                        ParseType(reader, type);
                        break;
                }
            }

            return data;
        }

        private static string ParseString(BigEndianBinaryReader reader)
        {
            return reader.ReadUnicodeString();
        }

        private static UnitFloat ParseUnitFloat(BigEndianBinaryReader reader)
        {
            return new UnitFloat()
            {
                type = (UnitTypes)reader.ReadUInt32(),
                value = reader.ReadDouble()
            };
        }

        private static bool ParseBoolean(BigEndianBinaryReader reader)
        {
            return reader.ReadByte() != 0;
        }

        private static uint ParseInteger(BigEndianBinaryReader reader)
        {
            return reader.ReadUInt32();
        }

        private static double ParseFloat(BigEndianBinaryReader reader)
        {
            return reader.ReadDouble();
        }

        private static EnumeratedValue ParseEnumerated(BigEndianBinaryReader reader)
        {
            return new EnumeratedValue()
            {
                type = ParseKey(reader),
                value = ParseKey(reader)
            };
        }

        private void ParseType(BigEndianBinaryReader reader, DescriptorTypes type)
        {
            switch (type)
            {
                case DescriptorTypes.List:
                    ParseList(reader);
                    break;
                case DescriptorTypes.Descriptor:
                    ParseDescriptor(reader);
                    break;
                case DescriptorTypes.String:
                    ParseString(reader);
                    break;
                case DescriptorTypes.UnitFloat:
                    ParseUnitFloat(reader);
                    break;
                case DescriptorTypes.Boolean:
                    ParseBoolean(reader);
                    break;
                case DescriptorTypes.Integer:
                    ParseInteger(reader);
                    break;
                case DescriptorTypes.Float:
                    ParseFloat(reader);
                    break;
                case DescriptorTypes.Enumerated:
                    ParseEnumerated(reader);
                    break;
                default:
                    throw new FormatException(string.Format(CultureInfo.CurrentCulture, "Unsupported brush descriptor type: '{0}'", DescriptorTypeToString(type)));
            }
        }

        private static string DescriptorTypeToString(DescriptorTypes type)
        {
            byte[] bytes = BitConverter.GetBytes((uint)type);
            return Encoding.ASCII.GetString(bytes);
        }

        private struct UnitFloat
        {
            public UnitTypes type;
            public double value;
        }

        private sealed class BrushData
        {
            public int diameter;
            public string sampledDataTag;
        }

        private sealed class BrushSectionOffsets
        {
            public readonly long sampleSectionOffset;
            public readonly long descriptorSectionOffset;

            public BrushSectionOffsets(long sampleSectionOffset, long descriptorSectionOffset)
            {
                this.sampleSectionOffset = sampleSectionOffset;
                this.descriptorSectionOffset = descriptorSectionOffset;
            }
        }

        private sealed class EnumeratedValue
        {
            public string type;
            public string value;
        }
    }
}
