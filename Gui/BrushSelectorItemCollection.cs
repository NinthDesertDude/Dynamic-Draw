using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BrushFactory
{
    /// <summary>
    /// The collection of current brushes
    /// </summary>
    /// <seealso cref="System.Collections.ObjectModel.Collection{BrushSelectorItem}"/>
    /// <seealso cref="IDisposable"/>
    internal sealed class BrushSelectorItemCollection : Collection<BrushSelectorItem>, IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BrushSelectorItemCollection"/> class.
        /// </summary>
        public BrushSelectorItemCollection() : base(new List<BrushSelectorItem>())
        {
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            IList<BrushSelectorItem> items = Items;

            for (int i = 0; i < items.Count; i++)
            {
                items[i].Dispose();
            }
        }

        /// <summary>
        /// Searches for an element that matches the conditions defined by the specified
        /// predicate, and returns the zero-based index of the first occurrence within the
        /// entire <see cref="BrushSelectorItemCollection"/>.
        /// </summary>
        /// <param name="match">The <see cref="Predicate{T}"/> delegate that defines the conditions of the element to search for.</param>
        /// <returns>
        /// The zero-based index of the first occurrence of an element that matches the conditions defined by match, if found; otherwise, –1.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="match"/> is null.</exception>
        public int FindIndex(Predicate<BrushSelectorItem> match)
        {
            return ((List<BrushSelectorItem>)Items).FindIndex(match);
        }

        /// <summary>
        /// Removes the element at the specified index of the <see cref="Collection{T}" />.
        /// </summary>
        /// <param name="index">The zero-based index of the element to remove.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="index"/> is less than zero.-or-
        /// <paramref name="index"/> is equal to or greater than <see cref="P:System.Collections.ObjectModel.Collection`1.Count"/>.
        /// </exception>
        protected override void RemoveItem(int index)
        {
            Items[index]?.Dispose();

            base.RemoveItem(index);
        }

        /// <summary>
        /// Replaces the element at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the element to replace.</param>
        /// <param name="item">The new value for the element at the specified index. The value can be <see langword="null"/> for reference types.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="index"/> is less than zero.-or-
        /// <paramref name="index"/> is greater than <see cref="P:System.Collections.ObjectModel.Collection`1.Count"/>.
        /// </exception>
        protected override void SetItem(int index, BrushSelectorItem item)
        {
            // Dispose of the existing item.
            Items[index]?.Dispose();

            base.SetItem(index, item);
        }

        /// <summary>
        /// Removes all elements from the <see cref="Collection{T}"/>.
        /// </summary>
        protected override void ClearItems()
        {
            IList<BrushSelectorItem> items = Items;

            for (int i = 0; i < items.Count; i++)
            {
                items[i]?.Dispose();
            }

            base.ClearItems();
        }
    }
}
