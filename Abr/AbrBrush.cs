using System;
using System.Drawing;

namespace BrushFactory.Abr
{
    /// <summary>
    /// Contains data associated with a brush constructed from an ABR file.
    /// </summary>
    internal sealed class AbrBrush : IDisposable
    {
        private Bitmap image;

        /// <summary>
        /// Initializes a new instance of the <see cref="AbrBrush"/> class.
        /// </summary>
        /// <param name="width">The width of the brush image.</param>
        /// <param name="height">The height of the brush image.</param>
        /// <param name="name">The name of the brush.</param>
        public AbrBrush(int width, int height, string name)
        {
            image = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            Name = name;
        }

        /// <summary>
        /// Gets the brush image.
        /// </summary>
        /// <value>
        /// The brush image.
        /// </value>
        public Bitmap Image
        {
            get
            {
                if (image == null)
                {
                    throw new ObjectDisposedException(nameof(AbrBrush));
                }

                return image;
            }
        }

        /// <summary>
        /// Gets the brush name.
        /// </summary>
        /// <value>
        /// The brush name.
        /// </value>
        public string Name
        {
            get;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (image != null)
            {
                image.Dispose();
                image = null;
            }
        }
    }
}
