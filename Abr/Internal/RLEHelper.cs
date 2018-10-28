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

// Portions of this file has been adapted from:
/////////////////////////////////////////////////////////////////////////////////
//
// Photoshop PSD FileType Plugin for Paint.NET
// http://psdplugin.codeplex.com/
//
// This software is provided under the MIT License:
//   Copyright (c) 2006-2007 Frank Blumenberg
//   Copyright (c) 2010-2011 Tao Yue
//
// Portions of this file are provided under the BSD 3-clause License:
//   Copyright (c) 2006, Jonas Beckeman
//
// See LICENSE.txt for complete licensing and attribution information.
//
/////////////////////////////////////////////////////////////////////////////////

namespace BrushFactory.Abr.Internal
{
    internal static class RLEHelper
    {
        public static void DecodedRow(BigEndianBinaryReader reader, byte[] imgData, int startIdx, int columns)
        {
            int count = 0;
            while (count < columns)
            {
                byte byteValue = reader.ReadByte();

                int len = byteValue;
                if (len < 128)
                {
                    len++;
                    while (len != 0 && (startIdx + count) < imgData.Length)
                    {
                        byteValue = reader.ReadByte();

                        imgData[startIdx + count] = byteValue;
                        count++;
                        len--;
                    }
                }
                else if (len > 128)
                {
                    // Next -len+1 bytes in the dest are replicated from next source byte.
                    // (Interpret len as a negative 8-bit int.)
                    len ^= 0x0FF;
                    len += 2;

                    byteValue = reader.ReadByte();

                    while (len != 0 && (startIdx + count) < imgData.Length)
                    {
                        imgData[startIdx + count] = byteValue;
                        count++;
                        len--;
                    }
                }
            }
        }
    }
}
