using System;
using System.Windows.Forms;

namespace DynamicDraw
{
    /// <summary>
    /// A ListView that is double buffered.
    /// </summary>
    /// <seealso cref="ListView"/>
    internal sealed class DoubleBufferedListView : ListView
    {
        private int previousItemIndex;
        private int currentItemIndex;

        /// <summary>
        /// Gets the index of the previously selected item.
        /// </summary>
        public int PreviousItemIndex
        {
            get
            {
                return previousItemIndex;
            }
        }

        /// <summary>
        /// Gets or sets the number of <see cref="T:System.Windows.Forms.ListViewItem" /> objects contained in the list when in virtual mode.
        /// </summary>
        public new int VirtualListSize
        {
            get
            {
                return base.VirtualListSize;
            }
            set
            {
                if (value == 0)
                {
                    previousItemIndex = -1;
                    currentItemIndex = -1;
                }

                base.VirtualListSize = value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DoubleBufferedListView"/> class.
        /// </summary>
        public DoubleBufferedListView()
        {
            SemanticTheme.ThemeChanged += HandleTheme;
            HandleTheme();
            DoubleBuffered = true;
            previousItemIndex = -1;
            currentItemIndex = -1;
        }

        /// <summary>
        /// Raises the <see cref="E:System.Windows.Forms.ListView.SelectedIndexChanged" /> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs" /> that contains the event data.</param>
        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            if (SelectedIndices.Count > 0)
            {
                int index = SelectedIndices[0];
                if (currentItemIndex != index)
                {
                    previousItemIndex = currentItemIndex;
                    currentItemIndex = index;
                }
            }

            base.OnSelectedIndexChanged(e);
        }

        /// <summary>
        /// Any color logic that gets set only once, dependent on the current theme, needs to subscribe to the theme
        /// changed event so it can be recalculated when theme preference loads from asynchronous user settings.
        /// </summary>
        private void HandleTheme()
        {
            BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
        }
    }
}
