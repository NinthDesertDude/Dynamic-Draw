using System;
using System.Collections;
using System.Collections.Generic;

namespace DynamicDraw.Abr
{
    /// <summary>
    /// A collection of AbrBrushes.
    /// </summary>
    internal sealed class AbrBrushCollection : IReadOnlyList<AbrBrush>, IDisposable
    {
        private readonly List<AbrBrush> brushes;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="AbrBrushCollection"/> class.
        /// </summary>
        /// <param name="list">The list of brushes.</param>
        /// <exception cref="ArgumentNullException"><paramref name="list"/> is null.</exception>
        public AbrBrushCollection(List<AbrBrush> list)
        {
            brushes = list ?? throw new ArgumentNullException(nameof(list));
            disposed = false;
        }

        public AbrBrush this[int index]
        {
            get
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(nameof(AbrBrushCollection));
                }

                return brushes[index];
            }
        }

        public int Count
        {
            get
            {
                return brushes.Count;
            }
        }

        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;

                for (int i = 0; i < brushes.Count; i++)
                {
                    brushes[i].Dispose();
                }
            }
        }

        public IEnumerator<AbrBrush> GetEnumerator()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(AbrBrushCollection));
            }

            return brushes.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(AbrBrushCollection));
            }

            return brushes.GetEnumerator();
        }
    }
}
