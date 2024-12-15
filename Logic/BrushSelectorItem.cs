using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace DynamicDraw
{
    /// <summary>
    /// The state of the <see cref="BrushSelectorItem"/>
    /// </summary>
    internal enum BrushSelectorItemState
    {
        /// <summary>
        /// The item is built-in and will always be stored in memory.
        /// </summary>
        Builtin,

        /// <summary>
        /// The item is disposed.
        /// </summary>
        Disposed,

        /// <summary>
        /// The item is stored on disk.
        /// </summary>
        Disk,

        /// <summary>
        /// The item is stored in memory.
        /// </summary>
        Memory
    }

    /// <summary>
    /// Represents an item to be used by the brush selector ListView.
    /// </summary>
    internal sealed class BrushSelectorItem : IDisposable
    {
        #region Fields
        private Bitmap brush;
        private Bitmap thumbnail;
        private bool disposed;

        private readonly string backingFile;
        #endregion

        #region Properties
        /// <summary>
        /// The brush ID is its location, if set, else its name.
        /// </summary>
        public string ID
        {
            get
            {
                return string.IsNullOrWhiteSpace(Location) ? Name : Location;
            }
        }

        /// <summary>
        /// Returns the item's name.
        /// </summary>
        public string Name
        {
            get;
            set;
        }

        /// <summary>
        /// Returns the item's location.
        /// </summary>
        public string Location
        {
            get;
            set;
        }

        /// <summary>
        /// Returns the associated image.
        /// </summary>
        public Bitmap Brush
        {
            get
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(nameof(BrushSelectorItem));
                }

                return brush;
            }
        }

        /// <summary>
        /// Gets the ListView thumbnail.
        /// </summary>
        public Bitmap Thumbnail
        {
            get
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(nameof(BrushSelectorItem));
                }

                return thumbnail;
            }
        }

        /// <summary>
        /// Gets the item state.
        /// </summary>
        public BrushSelectorItemState State
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the width of the brush.
        /// </summary>
        public int BrushWidth
        {
            get;
        }

        /// <summary>
        /// Gets the height of the brush.
        /// </summary>
        public int BrushHeight
        {
            get;
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Sets the name to be displayed in the ListView and its image for the built-in brushes.
        /// </summary>
        public BrushSelectorItem(string name, Bitmap brush) : this(name, null, brush, null, 0)
        {
            State = BrushSelectorItemState.Builtin;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BrushSelectorItem"/> class, with the specified custom brush.
        /// </summary>
        /// <param name="name">The name of the brush.</param>
        /// <param name="location">The location of the brush.</param>
        /// <param name="brush">The brush image.</param>
        /// <param name="backingFile">The backing file.</param>
        /// <param name="maxThumbnailHeight">The maximum height of the thumbnail image.</param>
        public BrushSelectorItem(string name, string location, Bitmap brush, string backingFile, int maxThumbnailHeight)
        {
            Name = name;
            Location = location;
            this.brush = brush;
            thumbnail = null;
            State = BrushSelectorItemState.Memory;
            BrushWidth = brush?.Width ?? 1;
            BrushHeight = brush?.Height ?? 1;
            this.backingFile = backingFile;
            disposed = false;

            if (maxThumbnailHeight > 0)
            {
                GenerateListViewThumbnail(maxThumbnailHeight, false);
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;

                if (brush != null)
                {
                    brush.Dispose();
                    brush = null;
                }
                if (thumbnail != null)
                {
                    thumbnail.Dispose();
                    thumbnail = null;
                }

                if (!string.IsNullOrEmpty(backingFile))
                {
                    try
                    {
                        File.Delete(backingFile);
                    }
                    catch (ArgumentException)
                    {
                    }
                    catch (IOException)
                    {
                    }
                    catch (NotSupportedException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
                State = BrushSelectorItemState.Disposed;
            }
        }

        /// <summary>
        /// Generates the ListView thumbnail.
        /// </summary>
        /// <param name="itemHeight">The ListView item height.</param>
        /// <param name="selected"><c>true</c> if the item is selected; otherwise, <c>false</c></param>
        public void GenerateListViewThumbnail(int itemHeight, bool selected)
        {
            Rectangle drawRect = new Rectangle(0, 0, BrushWidth, BrushHeight);

            // The brush image is always square.
            if (BrushHeight > itemHeight)
            {
                drawRect.Width = Math.Max(1, BrushWidth * itemHeight / BrushHeight);
                drawRect.Height = itemHeight;
            }

            int width = drawRect.Width;
            int height = drawRect.Height;

            if (thumbnail == null || thumbnail.Width != width || thumbnail.Height != height)
            {
                thumbnail?.Dispose();

                if (State == BrushSelectorItemState.Disk)
                {
                    ToMemory();
                }

                thumbnail = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                using (Graphics gr = Graphics.FromImage(thumbnail))
                {
                    gr.SmoothingMode = SmoothingMode.HighQuality;
                    gr.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    gr.PixelOffsetMode = PixelOffsetMode.HighQuality;

                    gr.DrawImage(Brush, drawRect);
                }

                if (State == BrushSelectorItemState.Memory && !selected)
                {
                    ToDisk();
                }
            }
        }

        /// <summary>
        /// Saves the brush image to disk.
        /// </summary>
        public void ToDisk()
        {
            if (State == BrushSelectorItemState.Memory)
            {
                using (FileStream stream = new FileStream(backingFile, FileMode.Create, FileAccess.ReadWrite))
                {
                    brush.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                }
                brush.Dispose();
                brush = null;
                State = BrushSelectorItemState.Disk;
            }
        }

        /// <summary>
        /// Loads the brush image from disk.
        /// </summary>
        public void ToMemory()
        {
            if (State == BrushSelectorItemState.Disk)
            {
                using (FileStream stream = new FileStream(backingFile, FileMode.Open, FileAccess.Read))
                {
                    brush = new Bitmap(stream);
                }
                State = BrushSelectorItemState.Memory;
            }
        }

        /// <summary>
        /// Returns the item's name.
        /// </summary>
        public override string ToString()
        {
            return Name;
        }
        #endregion
    }
}
