using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace DynamicDraw
{
    /// <summary>
    /// A button that hides/shows associated controls when toggled. If the accordion is hidden, all its controls are
    /// unbound from their parent controls and reinserted when the accordion is shown again. This method is prone to
    /// causing differences if the order changes for sibling controls in a bound control's parent; this can be mostly
    /// handled by encapsulating all into a container control that doesn't do this, then binding that instead.
    /// </summary>
    public class Accordion : Button
    {
        private bool isHovered = false;

        private string title = "";
        private readonly List<(Control ctrl, Control parent, int ctrlIndex)> boundControls = new List<(Control, Control, int)>();
        private bool isCollapsed = false;
        private readonly bool redAccented = false;

        /// <summary>
        /// Fires when the collapsed state changes, passing true if collapsed, false if open.
        /// </summary>
        public event Action<bool> OnCollapsedChanged;

        public Accordion(bool redAccented = false)
        {
            this.redAccented = redAccented;

            Click += (a, b) =>
            {
                ToggleCollapsed(!isCollapsed);
                Refresh();
            };

            MouseEnter += Accordion_MouseEnter;
            MouseLeave += Accordion_MouseLeave;
            VisibleChanged += Accordion_VisibleChanged;
        }

        private void Accordion_MouseLeave(object sender, System.EventArgs e)
        {
            isHovered = false;
        }

        private void Accordion_MouseEnter(object sender, System.EventArgs e)
        {
            isHovered = true;
        }

        /// <summary>
        /// Completely hides or restores bound controls along with the accordion if hidden/revealed.
        /// </summary>
        private void Accordion_VisibleChanged(object sender, System.EventArgs e)
        {
            for (int i = 0; i < boundControls.Count; i++)
            {
                var (ctrl, parent, ctrlIndex) = boundControls[i];
                if (ctrl != null)
                {
                    // disconnects/reconnects the bound control from its parent.
                    if (!Visible && ctrl.Parent != null)
                    {
                        parent.Controls.Remove(ctrl);
                    }
                    else if (Visible && ctrl.Parent == null)
                    {
                        // Suspend to avoid double-updating between adding & restoring index.
                        parent.SuspendLayout();
                        parent.Controls.Add(ctrl);
                        parent.Controls.SetChildIndex(ctrl, ctrlIndex);
                        parent.ResumeLayout();
                        parent.PerformLayout();
                    }
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Brush accordionBg =
                (Enabled && isHovered)
                    ? SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlBgHighlight)
                : (Enabled && redAccented)
                    ? SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlRedAccent)
                : Enabled
                    ? SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlBg)
                : SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlBgDisabled);

            e.Graphics.FillRectangle(accordionBg, 0, 0, Width, Height);

            var measures = e.Graphics.MeasureString(Text, Font);
            e.Graphics.DrawString(
                Text,
                Font,
                Enabled && redAccented && !isHovered
                ? SemanticTheme.Instance.GetBrush(ThemeName.Dark, ThemeSlot.MenuControlText)
                : SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlText),
                new Point(
                4,
                (int)((Height - measures.Height) / 2f)
            ));

            string collapseStr = isCollapsed ? "⮞" : "⮟";
            measures = e.Graphics.MeasureString(collapseStr, Font);
            e.Graphics.DrawString(
                collapseStr,
                Font,
                SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlTextSubtle),
                new Point(Width - 16, (int)((Height - measures.Height) / 2f)));

            // Draws a rectangle indicating focus.
            if (Enabled && Focused && ShowFocusCues)
            {
                e.Graphics.DrawRectangle(
                    SemanticTheme.Instance.GetPen(ThemeSlot.MenuControlActive), 0, 0, Width - 1, Height - 1);
            }
        }

        /// <summary>
        /// Replaces the list of controls that get shown/hidden when the button is toggled.
        /// </summary>
        public void UpdateAccordion(string title, bool isCollapsed, IEnumerable<Control> controls)
        {
            this.isCollapsed = isCollapsed;
            this.title = title;
            boundControls.Clear();

            foreach (Control ctrl in controls)
            {
                boundControls.Add((ctrl, ctrl.Parent, ctrl.Parent.Controls.IndexOf(ctrl)));
            }

            UpdateCollapsedState();
        }

        /// <summary>
        /// Updates the button and bound control(s) visibility.
        /// </summary>
        private void UpdateCollapsedState()
        {
            Text = title;

            for (int i = 0; i < boundControls.Count; i++)
            {
                if (boundControls[i].ctrl != null)
                {
                    boundControls[i].ctrl.Visible = !isCollapsed;
                }
            }
        }

        /// <summary>
        /// Toggles the accordion, same as clicking it.
        /// </summary>
        public void ToggleCollapsed(bool isCollapsed)
        {
            if (this.isCollapsed != isCollapsed)
            {
                OnCollapsedChanged?.Invoke(isCollapsed);
                this.isCollapsed = isCollapsed;
                UpdateCollapsedState();
            }
        }
    }
}
