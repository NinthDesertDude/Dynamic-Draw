// Copyright © Serge Weinstock 2014.
//
// This library is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public License
// along with this library.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using ComboBox = System.Windows.Forms.ComboBox;

namespace DynamicDraw
{
    /// <summary>
    /// A themed combobox that keeps a search box open for all typed text.
    /// </summary>
    public class SearchBox : ThemedComboBox
    {
        #region Members
        private readonly DropdownControl dropdownControl;
        private readonly ThemedListBox suggestionListbox;
        private Font fontBold;
        private StringMatchingMethod stringMatchingMethod;

        /// <summary>
        /// True after the user types. This variable is read when updating suggestions.
        /// </summary>
        private bool doUpdateSuggestionsFromUserTyping;

        public bool DropdownActive { get; private set; }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new bool DroppedDown
        {
            get { return base.DroppedDown || dropdownControl.Visible; }
            set
            {
                dropdownControl.Visible = false;
                base.DroppedDown = value;
            }
        }

        [
            DefaultValue(StringMatchingMethod.NoWildcards),
            Description("How strings are matched against the user input"),
            Browsable(true),
            EditorBrowsable(EditorBrowsableState.Always),
            Category("Behavior")
        ]

        public StringMatchingMethod MatchingMethod
        {
            get { return stringMatchingMethod; }
            set
            {
                if (stringMatchingMethod != value)
                {
                    stringMatchingMethod = value;
                    if (dropdownControl.Visible)
                    {
                        // recalculate the matches
                        ShowDropDown();
                    }
                }
            }
        }

        /// <summary>
        /// Specifies whether items in the list portion of the combobox are sorted.
        /// </summary>
        public new bool Sorted
        {
            get { return base.Sorted; }
            set
            {
                suggestionListbox.Sorted = value;
                base.Sorted = value;
            }
        }
        #endregion

        public SearchBox()
        {
            AutoCompleteMode = AutoCompleteMode.None;
            DropDownStyle = ComboBoxStyle.DropDown;
            stringMatchingMethod = StringMatchingMethod.NoWildcards;

            suggestionListbox = new ThemedListBox
            {
                DisplayMember = "Text",
                TabStop = false, 
                Dock = DockStyle.Fill,
                DrawMode = DrawMode.OwnerDrawFixed,
                IntegralHeight = true,
                Sorted = false,
            };

            suggestionListbox.Click += SuggestionListBox_Click;
            suggestionListbox.DrawItem += SuggestionListBox_DrawItem;
            FontChanged += OnFontChanged;
            suggestionListbox.MouseMove += SuggestionListBox_MouseMove;
            dropdownControl = new DropdownControl(suggestionListbox);
            OnFontChanged(null, null);
        }

        #region Overrides
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (fontBold != null)
                {
                    fontBold.Dispose();
                }
                dropdownControl.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            if (!dropdownControl.Focused && !suggestionListbox.Focused)
            {
                HideDropDown();
            }

            base.OnLostFocus(e);
        }

        protected override void OnDropDown(EventArgs e)
        {
            HideDropDown();
            base.OnDropDown(e);
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            HideDropDown();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            dropdownControl.Width = Width;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if ((keyData == Keys.Tab) && (dropdownControl.Visible))
            {
                // we change the selection but will also allow the navigation to the next control
                if (suggestionListbox.Text.Length == 0)
                {
                    suggestionListbox.SelectedIndex = 0;
                }

                Text = suggestionListbox.Text;

                Select(0, Text.Length);
                HideDropDown();
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            doUpdateSuggestionsFromUserTyping = true;

            if (!dropdownControl.Visible)
            {
                base.OnKeyDown(e);
                return;
            }
            switch (e.KeyCode)
            {
                case Keys.Down:
                    if (suggestionListbox.SelectedIndex < 0)
                    {
                        suggestionListbox.SelectedIndex = 0;
                    }
                    else if (suggestionListbox.SelectedIndex < suggestionListbox.Items.Count - 1)
                    {
                        suggestionListbox.SelectedIndex++;
                    }
                    break;
                case Keys.Up:
                    if (suggestionListbox.SelectedIndex > 0)
                    {
                        suggestionListbox.SelectedIndex--;
                    }
                    else if (suggestionListbox.SelectedIndex < 0)
                    {
                        suggestionListbox.SelectedIndex = suggestionListbox.Items.Count - 1;
                    }
                    break;
                case Keys.Enter:
                    if (suggestionListbox.Text.Length == 0)
                    {
                        suggestionListbox.SelectedIndex = 0;
                    }
                    Text = suggestionListbox.Text;

                    Select(0, Text.Length);
                    HideDropDown();
                    break;
                case Keys.Escape:
                    HideDropDown();
                    break;
                default:
                    base.OnKeyDown(e);
                    return;
            }
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        /// <summary>
        /// We need to know if the last text changed event was due to one of the dropdowns 
        /// or to the keyboard
        /// </summary>
        /// <param name="e"></param>
        protected override void OnDropDownClosed(EventArgs e)
        {
            doUpdateSuggestionsFromUserTyping = false;
            base.OnDropDownClosed(e);
        }

        /// <summary>
        /// this were we can make suggestions
        /// </summary>
        /// <param name="e"></param>
        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);


            if (!doUpdateSuggestionsFromUserTyping || !Focused)
            {
                return;
            }

            suggestionListbox.BeginUpdate();
            suggestionListbox.Items.Clear();
            StringMatcher matcher = new StringMatcher(MatchingMethod, Text);
            foreach (object item in Items)
            {
                StringMatch sm = matcher.Match(GetItemText(item));
                if (sm != null)
                {
                    suggestionListbox.Items.Add(sm);
                }
            }
            suggestionListbox.EndUpdate();

            bool visible = suggestionListbox.Items.Count != 0;

            if (suggestionListbox.Items.Count == 1 && ((StringMatch)suggestionListbox.Items[0]).Text.Length == Text.Trim().Length)
            {
                StringMatch sel = (StringMatch)suggestionListbox.Items[0];
                Text = sel.Text;
                Select(0, Text.Length);
                visible = false;
            }

            if (visible)
            {
                ShowDropDown();
            }
            else
            {
                HideDropDown();
            }

            doUpdateSuggestionsFromUserTyping = false;
        }
        #endregion

        #region Public methods
        /// <summary>
        /// Shows the drop down.
        /// </summary>
        public void ShowDropDown()
        {
            if (DesignMode)
            {
                return;
            }
            // Hide the "standard" drop down if any
            if (base.DroppedDown)
            {
                BeginUpdate();
                // setting DroppedDown to false may select an item
                // so we save the editbox state
                string oText = Text;
                int selStart = SelectionStart;
                int selLen = SelectionLength;

                // close the "standard" dropdown
                base.DroppedDown = false;
                
                // and restore the contents of the editbox
                Text = oText;
                Select(selStart, selLen);
                EndUpdate();
            }

            // pop it up and resize it
            int h = Math.Min(MaxDropDownItems, suggestionListbox.Items.Count) * suggestionListbox.ItemHeight;
            DropdownActive = true;
            dropdownControl.Show(this, new Size(DropDownWidth, h));
        }

        /// <summary>
        /// Hides the drop down.
        /// </summary>
        public void HideDropDown()
        {
            DropdownActive = false;
            if (dropdownControl.Visible)
            {
                dropdownControl.Close();
            }
        }

        public bool IsSuggestionBoxFocused()
        {
            return suggestionListbox.Focused;
        }
        #endregion

        #region keystroke and mouse events
        /// <summary>
        /// Called when the user clicks on an item in the list
        /// </summary>
        private void SuggestionListBox_Click(object sender, EventArgs e)
        {
            doUpdateSuggestionsFromUserTyping = false;
            StringMatch sel = (StringMatch)suggestionListbox.SelectedItem;
            Text = sel.Text;
            Select(0, Text.Length);
            Focus();
        }

        /// <summary>
        /// We highlight the selection under the mouse in the suggestion listbox
        /// </summary>
        private void SuggestionListBox_MouseMove(object sender, MouseEventArgs e)
        {
            int idx = suggestionListbox.IndexFromPoint(e.Location);
            if ((idx >= 0) && (idx != suggestionListbox.SelectedIndex))
            {
                suggestionListbox.SelectedIndex = idx;
            }
        }
        #endregion

        #region owner drawn
        /// <summary>
        /// We keep track of system settings changes for the font
        /// </summary>
        private void OnFontChanged(object sender, EventArgs e)
        {
            if (fontBold != null)
            {
                fontBold.Dispose();
            }
            suggestionListbox.Font = Font;
            fontBold = new Font(Font, FontStyle.Bold);
            suggestionListbox.ItemHeight = fontBold.Height + 2;
        }

        /// <summary>
        /// Draw a segment of a string and updates the bound rectangle for being used for the next segment drawing
        /// </summary>
        private static void DrawString(Graphics g, Color color, ref Rectangle rect, string text, Font font)
        {
            Size proposedSize = new Size(int.MaxValue, int.MaxValue);
            Size sz = TextRenderer.MeasureText(g, text, font, proposedSize, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, text, font, rect, color, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            rect.X += sz.Width;
            rect.Width -= sz.Width;
        }

        /// <summary>
        /// Draw an item in the suggestion listbox
        /// </summary>
        private void SuggestionListBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            StringMatch sm = (StringMatch) suggestionListbox.Items[e.Index];

            e.DrawBackground();
            
            bool isHighlighted = sm.StartsOnMatch;
            Rectangle rBounds = e.Bounds;

            foreach (string str in sm.Segments)
            {
                Font font = isHighlighted ? fontBold : Font;
                Color color = isHighlighted ? SemanticTheme.GetColor(ThemeSlot.RedAccent) : e.ForeColor;
                DrawString(e.Graphics, color, ref rBounds, str, font);
                isHighlighted = !isHighlighted;
            }

            e.DrawFocusRectangle();
        }
        #endregion
        
        #region Hidden inherited properties
        /// <summary>This property is not relevant for this class.</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), EditorBrowsable(EditorBrowsableState.Never)]
        public new AutoCompleteSource AutoCompleteSource
        {
            get { return base.AutoCompleteSource; }
            set { base.AutoCompleteSource = value; }
        }

        /// <summary>This property is not relevant for this class.</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), EditorBrowsable(EditorBrowsableState.Never)]
        public new AutoCompleteStringCollection AutoCompleteCustomSource 
        {
            get { return base.AutoCompleteCustomSource; }
            set { base.AutoCompleteCustomSource = value; }
        }

        /// <summary>This property is not relevant for this class.</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), EditorBrowsable(EditorBrowsableState.Never)]
        public new AutoCompleteMode AutoCompleteMode
        {
            get { return base.AutoCompleteMode; }
            set { base.AutoCompleteMode = value; }
        }

        /// <summary>This property is not relevant for this class.</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), EditorBrowsable(EditorBrowsableState.Never)]
        public new ComboBoxStyle DropDownStyle
        {
            get { return base.DropDownStyle; }
            set { base.DropDownStyle = value; }
        }
        #endregion
    }
}