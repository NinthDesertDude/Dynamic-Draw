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

using System;
using System.Drawing;
using System.IO;
using System.Text;

namespace DynamicDraw.Abr.Internal
{
    // Adapted from 'Problem and Solution: The Terrible Inefficiency of FileStream and BinaryReader'
    // https://jacksondunstan.com/articles/3568

    internal sealed class BigEndianBinaryReader : IDisposable
    {
        private Stream stream;
        private byte[] buffer;
        private int readOffset;
        private int readLength;

        private readonly int bufferSize;

        private const int MaxBufferSize = 4096;

        /// <summary>
        /// Initializes a new instance of the <see cref="BigEndianBinaryReader"/> class.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
        public BigEndianBinaryReader(Stream stream)
        {
            this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
            bufferSize = (int)Math.Min(stream.Length, MaxBufferSize);
            buffer = new byte[bufferSize];
            readOffset = 0;
            readLength = 0;
        }

        /// <summary>
        /// Gets the length of the stream.
        /// </summary>
        /// <value>
        /// The length of the stream.
        /// </value>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public long Length
        {
            get
            {
                if (stream == null)
                {
                    throw new ObjectDisposedException(nameof(BigEndianBinaryReader));
                }

                return stream.Length;
            }
        }

        /// <summary>
        /// Gets or sets the position in the stream.
        /// </summary>
        /// <value>
        /// The position in the stream.
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">value is negative.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public long Position
        {
            get
            {
                if (stream == null)
                {
                    throw new ObjectDisposedException(nameof(BigEndianBinaryReader));
                }

                return stream.Position - readLength + readOffset;
            }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException("value");
                }
                if (stream == null)
                {
                    throw new ObjectDisposedException(nameof(BigEndianBinaryReader));
                }

                long current = Position;

                if (value != current)
                {
                    long diff = value - current;

                    long newOffset = readOffset + diff;

                    // Avoid reading from the stream if the offset is within the current buffer.
                    if (newOffset >= 0 && newOffset <= readLength)
                    {
                        readOffset = (int)newOffset;
                    }
                    else
                    {
                        // Invalidate the existing buffer.
                        readOffset = 0;
                        readLength = 0;
                        stream.Seek(value, SeekOrigin.Begin);
                    }
                }
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (stream != null)
            {
                stream.Dispose();
                stream = null;
            }
        }

        /// <summary>
        /// Reads the specified number of bytes from the stream, starting from a specified point in the byte array.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        /// <param name="offset">The starting offset in the array.</param>
        /// <param name="count">The count.</param>
        /// <returns>The number of bytes read from the stream.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="bytes"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public int Read(byte[] bytes, int offset, int count)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            if (stream == null)
            {
                throw new ObjectDisposedException(nameof(BigEndianBinaryReader));
            }

            if (count == 0)
            {
                return 0;
            }

            if ((readOffset + count) <= readLength)
            {
                Buffer.BlockCopy(buffer, readOffset, bytes, offset, count);
                readOffset += count;

                return count;
            }
            else
            {
                // Ensure that any bytes at the end of the current buffer are included.
                int bytesUnread = readLength - readOffset;

                if (bytesUnread > 0)
                {
                    Buffer.BlockCopy(buffer, readOffset, bytes, 0, bytesUnread);
                }

                // Invalidate the existing buffer.
                readOffset = 0;
                readLength = 0;

                int totalBytesRead = bytesUnread;

                totalBytesRead += stream.Read(bytes, offset + bytesUnread, count - bytesUnread);

                return totalBytesRead;
            }
        }

        /// <summary>
        /// Reads the next byte from the current stream.
        /// </summary>
        /// <returns>The next byte read from the current stream.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public byte ReadByte()
        {
            if (stream == null)
            {
                throw new ObjectDisposedException(nameof(BigEndianBinaryReader));
            }

            if ((readOffset + sizeof(byte)) > readLength)
            {
                FillBuffer(sizeof(byte));
            }

            byte val = buffer[readOffset];
            readOffset += sizeof(byte);

            return val;
        }

        /// <summary>
        /// Reads the specified number of bytes from the stream.
        /// </summary>
        /// <param name="count">The number of bytes to read.</param>
        /// <returns>An array containing the specified bytes.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public byte[] ReadBytes(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            if (stream == null)
            {
                throw new ObjectDisposedException(nameof(BigEndianBinaryReader));
            }

            if (count == 0)
            {
                return Array.Empty<byte>();
            }

            byte[] bytes = new byte[count];

            if ((readOffset + count) <= readLength)
            {
                Buffer.BlockCopy(buffer, readOffset, bytes, 0, count);
                readOffset += count;
            }
            else
            {
                // Ensure that any bytes at the end of the current buffer are included.
                int bytesUnread = readLength - readOffset;

                if (bytesUnread > 0)
                {
                    Buffer.BlockCopy(buffer, readOffset, bytes, 0, bytesUnread);
                }

                int numBytesToRead = count - bytesUnread;
                int numBytesRead = bytesUnread;
                do
                {
                    int n = stream.Read(bytes, numBytesRead, numBytesToRead);

                    if (n == 0)
                    {
                        throw new EndOfStreamException();
                    }

                    numBytesRead += n;
                    numBytesToRead -= n;

                } while (numBytesToRead > 0);

                // Invalidate the existing buffer.
                readOffset = 0;
                readLength = 0;
            }

            return bytes;
        }

        /// <summary>
        /// Reads a 8-byte floating point value in big endian byte order.
        /// </summary>
        /// <returns>The 8-byte floating point value.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public unsafe double ReadDouble()
        {
            ulong temp = ReadUInt64();

            return *(double*)&temp;
        }

        /// <summary>
        /// Reads a 2-byte signed integer in big endian byte order.
        /// </summary>
        /// <returns>The 4-byte signed integer.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public short ReadInt16()
        {
            return (short)ReadUInt16();
        }

        /// <summary>
        /// Reads a 2-byte unsigned integer in big endian byte order.
        /// </summary>
        /// <returns>The 2-byte unsigned integer.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public ushort ReadUInt16()
        {
            if (stream == null)
            {
                throw new ObjectDisposedException(nameof(BigEndianBinaryReader));
            }

            if ((readOffset + sizeof(ushort)) > readLength)
            {
                FillBuffer(sizeof(ushort));
            }

            ushort val = (ushort)((buffer[readOffset] << 8) | buffer[readOffset + 1]);
            readOffset += sizeof(ushort);

            return val;
        }

        /// <summary>
        /// Reads a 4-byte signed integer in big endian byte order.
        /// </summary>
        /// <returns>The 4-byte signed integer.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public int ReadInt32()
        {
            return (int)ReadUInt32();
        }

        /// <summary>
        /// Reads a 4-byte unsigned integer in big endian byte order.
        /// </summary>
        /// <returns>The 4-byte unsigned integer.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public uint ReadUInt32()
        {
            if (stream == null)
            {
                throw new ObjectDisposedException(nameof(BigEndianBinaryReader));
            }

            if ((readOffset + sizeof(uint)) > readLength)
            {
                FillBuffer(sizeof(uint));
            }

            uint val = (uint)((buffer[readOffset] << 24) | (buffer[readOffset + 1] << 16) | (buffer[readOffset + 2] << 8) | buffer[readOffset + 3]);
            readOffset += sizeof(uint);

            return val;
        }

        /// <summary>
        /// Reads a 4-byte floating point value in big endian byte order.
        /// </summary>
        /// <returns>The 4-byte floating point value.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public unsafe float ReadSingle()
        {
            uint temp = ReadUInt32();

            return *(float*)&temp;
        }

        /// <summary>
        /// Reads a 8-byte signed integer in big endian byte order.
        /// </summary>
        /// <returns>The 8-byte signed integer.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public long ReadInt64()
        {
            return (long)ReadUInt64();
        }

        /// <summary>
        /// Reads a 8-byte unsigned integer in big endian byte order.
        /// </summary>
        /// <returns>The 8-byte unsigned integer.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public ulong ReadUInt64()
        {
            if (stream == null)
            {
                throw new ObjectDisposedException(nameof(BigEndianBinaryReader));
            }

            if ((readOffset + sizeof(ulong)) > readLength)
            {
                FillBuffer(sizeof(ulong));
            }

            uint hi = (uint)((buffer[readOffset] << 24) | (buffer[readOffset + 1] << 16) | (buffer[readOffset + 2] << 8) | buffer[readOffset + 3]);
            uint lo = (uint)((buffer[readOffset + 4] << 24) | (buffer[readOffset + 5] << 16) | (buffer[readOffset + 6] << 8) | buffer[readOffset + 7]);
            readOffset += sizeof(ulong);

            return (((ulong)hi) << 32) | lo;
        }

        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reads a Pascal-style string.
        /// </summary>
        /// <returns>A string containing the characters of the Pascal-style string.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public string ReadPascalString()
        {
            if (stream == null)
            {
                throw new ObjectDisposedException(nameof(BigEndianBinaryReader));
            }

            byte stringLength = ReadByte();

            byte[] bytes = ReadBytes(stringLength);

            return Encoding.ASCII.GetString(bytes);
        }

        /// <summary>
        /// Reads a rectangle comprised of 4-byte signed integers.
        /// </summary>
        /// <returns>A rectangle comprised of 4-byte signed integers.</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public Rectangle ReadInt32Rectangle()
        {
            if (stream == null)
            {
                throw new ObjectDisposedException(nameof(BigEndianBinaryReader));
            }

            Rectangle rect = new Rectangle();

            rect.Y = ReadInt32();
            rect.X = ReadInt32();
            rect.Height = ReadInt32() - rect.Y;
            rect.Width = ReadInt32() - rect.X;

            return rect;
        }

        /// <summary>
        /// Reads a length-prefixed UTF-16 string.
        /// </summary>
        /// <returns>A string containing the characters of the UTF-16 string..</returns>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        public string ReadUnicodeString()
        {
            if (stream == null)
            {
                throw new ObjectDisposedException(nameof(BigEndianBinaryReader));
            }

            int lengthInChars = ReadInt32();
            byte[] bytes = ReadBytes(lengthInChars * 2);

            return Encoding.BigEndianUnicode.GetString(bytes).TrimEnd('\0');
        }

        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Fills the buffer with at least the number of bytes requested.
        /// </summary>
        /// <param name="minBytes">The minimum number of bytes to place in the buffer.</param>
        /// <exception cref="EndOfStreamException">The end of the stream has been reached.</exception>
        private void FillBuffer(int minBytes)
        {
            int bytesUnread = readLength - readOffset;

            if (bytesUnread > 0)
            {
                Buffer.BlockCopy(buffer, readOffset, buffer, 0, bytesUnread);
            }

            int numBytesToRead = bufferSize - bytesUnread;
            int numBytesRead = bytesUnread;
            do
            {
                int n = stream.Read(buffer, numBytesRead, numBytesToRead);

                if (n == 0)
                {
                    throw new EndOfStreamException();
                }

                numBytesRead += n;
                numBytesToRead -= n;

            } while (numBytesRead < minBytes);

            readOffset = 0;
            readLength = numBytesRead;
        }
    }
}
