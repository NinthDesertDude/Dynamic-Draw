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

using DynamicDraw.Abr.Internal;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;

namespace DynamicDraw.Abr
{
    /// <summary>
    /// Reads the Abr file type to construct bitmaps.
    /// </summary>
	internal static class AbrReader
	{
		private enum BrushType : short
		{
			Computed = 1,
			Sampled
		}

		private enum BrushCompression
		{
			None = 0,
			RLE = 1
		}

		/// <summary>
		/// Loads the brushes from the specified path.
		/// </summary>
		/// <param name="path">The path.</param>
		/// <returns>An <see cref="AbrBrushCollection"/> containing the brushes.</returns>
		/// <exception cref="FileNotFoundException">The file can not be found.</exception>
		/// <exception cref="FormatException">The ABR version is not supported.</exception>
		public static AbrBrushCollection LoadBrushes(string path)
		{
			AbrBrushCollection brushes;

			FileStream stream = null;

			try
			{
				stream = new FileStream(path, FileMode.Open, FileAccess.Read);

				using (BigEndianBinaryReader reader = new BigEndianBinaryReader(stream))
				{
					stream = null;

					short version = reader.ReadInt16();

					switch (version)
					{
						case 1:
						case 2:
							brushes = DecodeVersion1(reader, version);
							break;
						case 6:
						case 7: // Used by Photoshop CS and later for brushes containing 16-bit data.
						case 10: // Used by Photoshop CS6 and/or CC?
							brushes = DecodeVersion6(reader, version);
							break;
						default:
							throw new FormatException(string.Format(
                                CultureInfo.CurrentCulture,
                                Localization.Strings.AbrUnsupportedMajorVersionFormat,
								version));
					}
				}
			}
			finally
			{
				if (stream != null)
				{
					stream.Dispose();
					stream = null;
				}
			}

			return brushes;
		}

		/// <summary>
		/// Decodes the version 1 and 2 brushes.
		/// </summary>
		/// <param name="reader">The reader.</param>
		/// <param name="majorVersion">The major version.</param>
		/// <returns>An <see cref="AbrBrushCollection"/> containing the brushes.</returns>
		private static AbrBrushCollection DecodeVersion1(BigEndianBinaryReader reader, short version)
		{
			short count = reader.ReadInt16();

			List<AbrBrush> brushes = new List<AbrBrush>(count);

			for (int i = 0; i < count; i++)
			{
				BrushType type = (BrushType)reader.ReadInt16();
				int size = reader.ReadInt32();

				long endOffset = reader.Position + size;

#if DEBUG
				System.Diagnostics.Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Brush: {0}, type: {1}, size: {2} bytes", i, type, size));
#endif
				if (type == BrushType.Computed)
				{
#if DEBUG
					int misc = reader.ReadInt32();
					short spacing = reader.ReadInt16();

					string name = string.Empty;
					if (version == 2)
					{
						name = reader.ReadUnicodeString();
					}

					short diameter = reader.ReadInt16();
					short roundness = reader.ReadInt16();
					short angle = reader.ReadInt16();
					short hardness = reader.ReadInt16();
#else
					reader.Position += size;
#endif
				}
				else if (type == BrushType.Sampled)
				{
					int misc = reader.ReadInt32();
					short spacing = reader.ReadInt16();

					string name = string.Empty;
					if (version == 2)
					{
						name = reader.ReadUnicodeString();
					}

					bool antiAlias = reader.ReadByte() != 0;

					// Skip the Int16 bounds.
					reader.Position += 8L;

					Rectangle bounds = reader.ReadInt32Rectangle();
					if (bounds.Width <= 0 || bounds.Height <= 0)
					{
						// Skip any brushes that have invalid dimensions.
						reader.Position += (endOffset - reader.Position);
						continue;
					}

					short depth = reader.ReadInt16();

					if (depth != 8)
					{
						// The format specs state that brushes must be 8-bit, skip any that are not.
						reader.Position += (endOffset - reader.Position);
						continue;
					}
					int height = bounds.Height;
					int width = bounds.Width;

					int rowsRemaining = height;
					int rowsRead = 0;

					byte[] alphaData = new byte[width * height];
					bool unknownCompressionType = false;

					do
					{
						// Sampled brush data is broken into repeating chunks for brushes taller that 16384 pixels.
						int chunkHeight = Math.Min(rowsRemaining, 16384);
						// The format specs state that compression is stored as a 2-byte field, but it is written as a 1-byte field in actual files.
						BrushCompression compression = (BrushCompression)reader.ReadByte();

						if (compression == BrushCompression.RLE)
						{
							short[] compressedRowLengths = new short[height];

							for (int y = 0; y < height; y++)
							{
								compressedRowLengths[y] = reader.ReadInt16();
							}

							for (int y = 0; y < chunkHeight; y++)
							{
								int row = rowsRead + y;
								RLEHelper.DecodedRow(reader, alphaData, row * width, width);
							}
						}
						else if (compression == BrushCompression.None)
						{
							int numBytesToRead = chunkHeight * width;
							int numBytesRead = rowsRead * width;
							while (numBytesToRead > 0)
							{
								// Read may return anything from 0 to numBytesToRead.
								int n = reader.Read(alphaData, numBytesRead, numBytesToRead);
								// The end of the file is reached.
								if (n == 0)
								{
									break;
								}
								numBytesRead += n;
								numBytesToRead -= n;
							}
						}
						else
						{
							// The format specs state that the brush data can be either uncompressed or RLE compressed with PackBits.
							// Ignore any brushes with an unknown compression type.
							unknownCompressionType = true;
							break;
						}

						rowsRead += 16384;
						rowsRemaining -= 16384;

					} while (rowsRemaining > 0);

					if (unknownCompressionType)
					{
						reader.Position += (endOffset - reader.Position);
						continue;
					}
					else
					{
						brushes.Add(CreateSampledBrush(width, height, depth, alphaData, name));
					}
				}
				else
				{
					// Skip any unknown brush types.
					reader.Position += size;
				}
			}

			return new AbrBrushCollection(brushes);
		}

		/// <summary>
		/// Decodes the descriptor-based brushes in version 6 and later.
		/// </summary>
		/// <param name="reader">The reader.</param>
		/// <param name="majorVersion">The major version.</param>
		/// <returns>An <see cref="AbrBrushCollection"/> containing the brushes.</returns>
		/// <exception cref="FormatException">The ABR version is not supported.</exception>
		private static AbrBrushCollection DecodeVersion6(BigEndianBinaryReader reader, short majorVersion)
		{
			short minorVersion = reader.ReadInt16();
			long unusedDataLength;

			switch (minorVersion)
			{
				case 1:
					// Skip the Int16 bounds rectangle and the unknown Int16.
					unusedDataLength = 10L;
					break;
				case 2:
					// Skip the unknown bytes.
					unusedDataLength = 264L;
					break;
				default:
					throw new FormatException(string.Format(
                        CultureInfo.CurrentCulture,
						Localization.Strings.AbrUnsupportedMinorVersionFormat,
						majorVersion, minorVersion));
			}

			BrushSectionParser parser = new BrushSectionParser(reader);

			List<AbrBrush> brushes = new List<AbrBrush>(parser.SampledBrushes.Count);

			long sampleSectionOffset = parser.SampleSectionOffset;

			if (parser.SampledBrushes.Count > 0 && sampleSectionOffset >= 0)
			{
				reader.Position = sampleSectionOffset;

				uint sectionLength = reader.ReadUInt32();

				long sectionEnd = reader.Position + sectionLength;

				while (reader.Position < sectionEnd)
				{
					uint brushLength = reader.ReadUInt32();

					// The brush data is padded to 4 byte alignment.
					long paddedBrushLength = ((long)brushLength + 3) & ~3;

					long endOffset = reader.Position + paddedBrushLength;

					string tag = reader.ReadPascalString();

					// Skip the unneeded data that comes before the Int32 bounds rectangle.
					reader.Position += unusedDataLength;

					Rectangle bounds = reader.ReadInt32Rectangle();
					if (bounds.Width <= 0 || bounds.Height <= 0)
					{
						// Skip any brushes that have invalid dimensions.
						reader.Position += (endOffset - reader.Position);
						continue;
					}

					short depth = reader.ReadInt16();
					if (depth != 8 && depth != 16)
					{
						// Skip any brushes with an unknown bit depth.
						reader.Position += (endOffset - reader.Position);
						continue;
					}

					SampledBrush sampledBrush = parser.SampledBrushes.FindLargestBrush(tag);
					if (sampledBrush != null)
					{
						BrushCompression compression = (BrushCompression)reader.ReadByte();

						int height = bounds.Height;
						int width = bounds.Width;

						byte[] alphaData = null;

						if (compression == BrushCompression.RLE)
						{
							short[] compressedRowLengths = new short[height];

							for (int y = 0; y < height; y++)
							{
								compressedRowLengths[y] = reader.ReadInt16();
							}

							int alphaDataSize = width * height;
							int bytesPerRow = width;

							if (depth == 16)
							{
								alphaDataSize *= 2;
								bytesPerRow *= 2;
							}

							alphaData = new byte[alphaDataSize];

							for (int y = 0; y < height; y++)
							{
								RLEHelper.DecodedRow(reader, alphaData, y * width, bytesPerRow);
							}
						}
						else if (compression == BrushCompression.None)
						{
							int alphaDataSize = width * height;

							if (depth == 16)
							{
								alphaDataSize *= 2;
							}

							alphaData = reader.ReadBytes(alphaDataSize);
						}
						else
						{
							// Skip any brushes with an unknown compression type.
							reader.Position += (endOffset - reader.Position);
							continue;
						}

						AbrBrush brush = CreateSampledBrush(width, height, depth, alphaData, sampledBrush.Name);

						brushes.Add(brush);

						// Some brushes only store the largest item and scale it down.
						var scaledBrushes = parser.SampledBrushes.Where(i => i.Tag.Equals(tag, StringComparison.Ordinal) && i.Diameter < sampledBrush.Diameter);
						if (scaledBrushes.Any())
						{
							int originalWidth = brush.Image.Width;
							int originalHeight = brush.Image.Height;

							foreach (SampledBrush item in scaledBrushes.OrderByDescending(p => p.Diameter))
							{
								Size size = Utils.ComputeBrushSize(originalWidth, originalHeight, item.Diameter);

								AbrBrush scaledBrush = new AbrBrush(size.Width, size.Height, item.Name);

								using (Graphics gr = Graphics.FromImage(scaledBrush.Image))
								{
									gr.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
									gr.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
									gr.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
									gr.DrawImage(brush.Image, 0, 0, size.Width, size.Height);
								}

								brushes.Add(scaledBrush);
							}
						}
					}

					long remaining = endOffset - reader.Position;
					// Skip any remaining bytes until the next sampled brush.
					if (remaining > 0)
					{
						reader.Position += remaining;
					}
				}
			}

			return new AbrBrushCollection(brushes);
		}

        /// <summary>
        /// Creates a brush with the given dimensions given bit depth, alpha
        /// data, and name.
        /// </summary>
        /// <param name="width">Desired width.</param>
        /// <param name="height">Desired height.</param>
        /// <param name="bitDepth">Bit depth of the data.</param>
        /// <param name="alphaData">
        /// The pixel data, stored only as an alpha channel and flattened to a
        /// 1-d array.
        /// </param>
        /// <param name="name">Desired brush name.</param>
        /// <returns>
        /// An AbrBrush with a bitmap constructed from the alpha data.
        /// </returns>
		private static unsafe AbrBrush CreateSampledBrush(
            int width,
            int height,
            int bitDepth,
            byte[] alphaData,
            string name)
		{
			AbrBrush brush = new AbrBrush(width, height, name);

			BitmapData bd = brush.Image.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppPArgb);

			try
			{
				fixed (byte* ptr = alphaData)
				{
					byte* scan0 = (byte*)bd.Scan0.ToPointer();
					int stride = bd.Stride;

					if (bitDepth == 16)
					{
						int srcStride = width * 2;
						for (int y = 0; y < height; y++)
						{
							byte* src = ptr + (y * srcStride);
							byte* dst = scan0 + (y * stride);

							for (int x = 0; x < width; x++)
							{
								ushort val = (ushort)((src[0] << 8) | src[1]);

								dst[0] = dst[1] = dst[2] = 0;
								dst[3] = (byte)((val * 10) / 1285);

								src += 2;
								dst += 4;
							}
						}
					}
					else
					{
						for (int y = 0; y < height; y++)
						{
							byte* src = ptr + (y * width);
							byte* dst = scan0 + (y * stride);

							for (int x = 0; x < width; x++)
							{
								dst[0] = dst[1] = dst[2] = 0;
								dst[3] = *src;

								src++;
								dst += 4;
							}
						}
					}
				}
			}
			finally
			{
				brush.Image.UnlockBits(bd);
			}

			return brush;
		}
	}
}
