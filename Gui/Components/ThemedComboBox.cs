// Adapted from https://stackoverflow.com/a/65976649/7197632 by author Reza Aghaei, borrowed under CC-BY-SA 4.0.
// This file follows CC-BY-SA 4.0, waiving the attribution requirement for future edits. Original author must be attributed.
// Link to license: https://creativecommons.org/licenses/by-sa/4.0/

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DynamicDraw
{
    /// <summary>
    /// A themed combobox that has a custom border and button color.
    /// </summary>
    public class ThemedComboBox : ComboBox
    {
        public ThemedComboBox()
        {
            SemanticTheme.ThemeChanged += HandleTheme;
            HandleTheme();
        }

        private void HandleTheme()
        {
            BackColor = SemanticTheme.GetColor(ThemeSlot.ControlBg);
            ForeColor = SemanticTheme.GetColor(ThemeSlot.Text);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_PAINT && DropDownStyle != ComboBoxStyle.Simple)
            {
                Rectangle clientRect = ClientRectangle;
                int dropDownButtonWidth = SystemInformation.HorizontalScrollBarArrowWidth;
                Rectangle outerBorder = new Rectangle(
                    clientRect.Location, new Size(clientRect.Width - 1, clientRect.Height - 1));
                Rectangle innerBorder = new Rectangle(outerBorder.X + 1, outerBorder.Y + 1,
                    outerBorder.Width - dropDownButtonWidth - 2, outerBorder.Height - 2);
                Rectangle innerInnerBorder = new Rectangle(innerBorder.X + 1, innerBorder.Y + 1,
                    innerBorder.Width - 2, innerBorder.Height - 2);
                Rectangle dropDownRect = new Rectangle(innerBorder.Right + 1, innerBorder.Y,
                    dropDownButtonWidth, innerBorder.Height + 1);

                if (RightToLeft == RightToLeft.Yes)
                {
                    innerBorder.X = clientRect.Width - innerBorder.Right;
                    innerInnerBorder.X = clientRect.Width - innerInnerBorder.Right;
                    dropDownRect.X = clientRect.Width - dropDownRect.Right;
                    dropDownRect.Width += 1;
                }

                ThemeSlot outerBorderThemeSlot = Enabled
                    ? ThemeSlot.TextSubtle
                    : ThemeSlot.ControlBgDisabled;
                Pen borderThemeSlot = Enabled
                        ? SemanticTheme.Instance.GetPen(ThemeSlot.ControlBg)
                        : SemanticTheme.Instance.GetPen(ThemeSlot.ControlBg);
                Point middle = new Point(
                    dropDownRect.Left + dropDownRect.Width / 2,
                    dropDownRect.Top + dropDownRect.Height / 2);

                Point[] arrow = new Point[]
                {
                    new Point(middle.X - 3, middle.Y - 2),
                    new Point(middle.X + 4, middle.Y - 2),
                    new Point(middle.X, middle.Y + 2)
                };

                var ps = new PAINTSTRUCT();
                bool shoulEndPaint = false;
                IntPtr dc;

                if (m.WParam == IntPtr.Zero)
                {
                    dc = BeginPaint(Handle, ref ps);
                    m.WParam = dc;
                    shoulEndPaint = true;
                }
                else
                {
                    dc = m.WParam;
                }

                IntPtr rgn = CreateRectRgn(
                    innerInnerBorder.Left, innerInnerBorder.Top,
                    innerInnerBorder.Right, innerInnerBorder.Bottom);

                SelectClipRgn(dc, rgn);
                DefWndProc(ref m);
                DeleteObject(rgn);

                rgn = CreateRectRgn(
                    clientRect.Left, clientRect.Top,
                    clientRect.Right, clientRect.Bottom);

                SelectClipRgn(dc, rgn);

                using (var g = Graphics.FromHdc(dc))
                {
                    g.FillRectangle(SemanticTheme.Instance.GetBrush(ThemeSlot.ControlBgHighlight), dropDownRect);
                    g.FillPolygon(SemanticTheme.Instance.GetBrush(outerBorderThemeSlot), arrow);
                    g.DrawRectangle(borderThemeSlot, innerBorder);
                    g.DrawRectangle(borderThemeSlot, innerInnerBorder);
                    g.DrawRectangle(SemanticTheme.Instance.GetPen(ThemeSlot.ControlBgHighlight), outerBorder);
                }

                if (shoulEndPaint)
                {
                    EndPaint(Handle, ref ps);
                }

                DeleteObject(rgn);
            }
            else
            {
                base.WndProc(ref m);
            }
        }

        #region P/Invoke Interop
        private const int WM_PAINT = 0xF;

        [StructLayout(LayoutKind.Sequential)]
        public struct PAINTSTRUCT
        {
            public IntPtr hdc;
            public bool fErase;
            public int rcPaint_left;
            public int rcPaint_top;
            public int rcPaint_right;
            public int rcPaint_bottom;
            public bool fRestore;
            public bool fIncUpdate;
            public int reserved1;
            public int reserved2;
            public int reserved3;
            public int reserved4;
            public int reserved5;
            public int reserved6;
            public int reserved7;
            public int reserved8;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr BeginPaint(IntPtr hWnd,
            [In, Out] ref PAINTSTRUCT lpPaint);

        [DllImport("user32.dll")]
        private static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);

        [DllImport("gdi32.dll")]
        private static extern int SelectClipRgn(IntPtr hDC, IntPtr hRgn);

        [DllImport("gdi32.dll")]
        internal static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRectRgn(int x1, int y1, int x2, int y2);
        #endregion
    }
}
