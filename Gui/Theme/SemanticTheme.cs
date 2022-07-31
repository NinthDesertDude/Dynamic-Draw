using System;
using System.Collections.Generic;
using System.Drawing;

namespace DynamicDraw
{
    /// <summary>
    /// The default Winforms controls are wrapped so they automatically take advantage of this theming system. All
    /// necessary colors are identified by how they're used, and those colors are determined by theme, allowing
    /// controls to automatically receive the current theme.
    /// </summary>
    public class SemanticTheme : IDisposable
    {
        #region Static
        private static ThemeName currentTheme;
        private static SemanticTheme instance;
        private static readonly Dictionary<ThemeName, Dictionary<ThemeSlot, Color>> themeData;

        /// <summary>
        /// Gets or sets the current theme.
        /// </summary>
        public static ThemeName CurrentTheme
        {
            get
            {
                return currentTheme;
            }
            set
            {
                if (currentTheme != value)
                {
                    currentTheme = value;
                    ThemeChanged?.Invoke();
                }
            }
        }

        /// <summary>
        /// The singleton instance of this class (lazy-instantiated on access).
        /// </summary>
        public static SemanticTheme Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new SemanticTheme();
                }

                return instance;
            }
        }

        /// <summary>
        /// Invoked whenever the theme is set to a new value.
        /// </summary>
        public static event Action ThemeChanged;

        static SemanticTheme()
        {
            CurrentTheme = ThemeName.Light;

            var lightTheme = new Dictionary<ThemeSlot, Color>()
            {
                { ThemeSlot.CanvasBg, Color.FromArgb(255, 207, 207, 207) },
                { ThemeSlot.MenuBg, Color.FromArgb(255, 255, 255, 255) },
                { ThemeSlot.MenuControlActive, Color.FromArgb(255, 40, 162, 255) },
                { ThemeSlot.MenuControlActiveSelected, Color.FromArgb(255, 52, 58, 226) },
                { ThemeSlot.MenuControlActiveHover, Color.FromArgb(255, 179, 207, 229) },
                { ThemeSlot.MenuControlBg, Color.FromArgb(255, 227, 227, 227) },
                { ThemeSlot.MenuControlBgDisabled, Color.FromArgb(255, 204, 204, 204) },
                { ThemeSlot.MenuControlBgHighlight, Color.FromArgb(255, 237, 237, 237) },
                { ThemeSlot.MenuControlBgHighlightDisabled, Color.FromArgb(255, 187, 187, 187) },
                { ThemeSlot.MenuControlText, Color.FromArgb(255, 28, 28, 28) },
                { ThemeSlot.MenuControlRedAccent, Color.FromArgb(255, 188, 77, 77) },
                { ThemeSlot.MenuControlTextDisabled, Color.FromArgb(255, 155, 155, 155) },
                { ThemeSlot.MenuControlTextSubtle, Color.FromArgb(255, 55, 55, 55) }
            };

            var darkTheme = new Dictionary<ThemeSlot, Color>()
            {
                { ThemeSlot.CanvasBg, Color.FromArgb(255, 48, 48, 48) },
                { ThemeSlot.MenuBg, Color.FromArgb(255, 37, 37, 37) },
                { ThemeSlot.MenuControlActive, Color.FromArgb(255, 0, 120, 215) },
                { ThemeSlot.MenuControlActiveSelected, Color.FromArgb(255, 24, 50, 75) },
                { ThemeSlot.MenuControlActiveHover, Color.FromArgb(255, 24, 50, 75) },
                { ThemeSlot.MenuControlBg, Color.FromArgb(255, 32, 32, 32) },
                { ThemeSlot.MenuControlBgDisabled, Color.FromArgb(255, 32, 32, 32) },
                { ThemeSlot.MenuControlBgHighlight, Color.FromArgb(255, 128, 128, 128) },
                { ThemeSlot.MenuControlBgHighlightDisabled, Color.FromArgb(255, 48, 48, 48) },
                { ThemeSlot.MenuControlText, Color.FromArgb(255, 227, 227, 227) },
                { ThemeSlot.MenuControlRedAccent, Color.FromArgb(255, 188, 77, 77) },
                { ThemeSlot.MenuControlTextDisabled, Color.FromArgb(255, 100, 100, 100) },
                { ThemeSlot.MenuControlTextSubtle, Color.FromArgb(255, 200, 200, 200) }
            };

            themeData = new Dictionary<ThemeName, Dictionary<ThemeSlot, Color>>()
            {
                { ThemeName.Light, lightTheme },
                { ThemeName.Dark, darkTheme }
            };
        }

        /// <summary>
        /// Returns the color associated with the given semantic slot for the current theme.
        /// </summary>
        public static Color GetColor(ThemeSlot semanticSlot)
        {
            return themeData[CurrentTheme][semanticSlot];
        }

        /// <summary>
        /// Returns the color associated with the given semantic slot for the given theme.
        /// </summary>
        public static Color GetColor(ThemeName themeName, ThemeSlot semanticSlot)
        {
            return themeData[themeName][semanticSlot];
        }

        /// <summary>
        /// Draws the given image with automatic swapping to pure black color for light themes. This is intended for
        /// use with lightly colored icons.
        /// </summary>
        public static void DrawImageForTheme(Graphics g, Image image, bool disabled, int x, int y)
        {
            if (CurrentTheme == ThemeName.Light)
            {
                Point[] destination = new Point[3];
                destination[0] = new Point(x, y);
                destination[1] = new Point(x + image.Width, y);
                destination[2] = new Point(x, y + image.Height);

                using (var attrs = Utils.ColorImageAttr(0, 0, 0, disabled ? 0.5f : 1))
                {
                    g.DrawImage(
                        image,
                        destination,
                        new Rectangle(0, 0, image.Width, image.Height),
                        GraphicsUnit.Pixel,
                        attrs);
                }
            }
            else
            {
                g.DrawImage(image, new Point(x, y));
            }
        }
        #endregion

        #region Members
        private bool disposedValue;

        /// <summary>
        /// Contains a list of solid brushes for every theme color (lazy-instantiated).
        /// </summary>
        public Dictionary<ThemeName, Dictionary<ThemeSlot, SolidBrush>> ThemeBrushes { get; private set; }

        /// <summary>
        /// Contains a list of pen for every theme color (lazy-instantiated).
        /// </summary>
        public Dictionary<ThemeName, Dictionary<ThemeSlot, Pen>> ThemePens { get; private set; }
        #endregion

        #region Constructors
        /// <summary>
        /// Private, called only by singleton logic.
        /// </summary>
        private SemanticTheme()
        {
            ThemeBrushes = new Dictionary<ThemeName, Dictionary<ThemeSlot, SolidBrush>>();
            ThemePens = new Dictionary<ThemeName, Dictionary<ThemeSlot, Pen>>();

            ThemeName[] names = Enum.GetValues<ThemeName>();
            ThemeSlot[] slots = Enum.GetValues<ThemeSlot>();

            foreach (var name in names)
            {
                ThemeBrushes.Add(name, new Dictionary<ThemeSlot, SolidBrush>());
                ThemePens.Add(name, new Dictionary<ThemeSlot, Pen>());

                foreach (var slot in slots)
                {
                    ThemeBrushes[name].Add(slot, null);
                    ThemePens[name].Add(slot, null);
                }
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Returns a solid brush for the given semantic slot for the current theme. It's internally managed; do not
        /// dispose or modify it.
        /// </summary>
        public SolidBrush GetBrush(ThemeSlot semanticSlot)
        {
            if (disposedValue)
            {
                return (SolidBrush)Brushes.Transparent;
            }

            if (ThemeBrushes[CurrentTheme][semanticSlot] == null)
            {
                ThemeBrushes[CurrentTheme][semanticSlot] = new SolidBrush(GetColor(semanticSlot));
            }

            return ThemeBrushes[CurrentTheme][semanticSlot];
        }

        /// <summary>
        /// Returns a solid brush for the given semantic slot for the given theme. It's internally managed; do not
        /// dispose or modify it.
        /// </summary>
        public SolidBrush GetBrush(ThemeName themeName, ThemeSlot semanticSlot)
        {
            if (disposedValue)
            {
                return (SolidBrush)Brushes.Transparent;
            }

            if (ThemeBrushes[themeName][semanticSlot] == null)
            {
                ThemeBrushes[themeName][semanticSlot] = new SolidBrush(GetColor(themeName, semanticSlot));
            }

            return ThemeBrushes[themeName][semanticSlot];
        }

        /// <summary>
        /// Returns a pen for the given semantic slot for the current theme. It's internally managed; do not
        /// dispose or modify it.
        /// </summary>
        public Pen GetPen(ThemeSlot semanticSlot)
        {
            if (disposedValue)
            {
                return Pens.Transparent;
            }

            if (ThemePens[CurrentTheme][semanticSlot] == null)
            {
                ThemePens[CurrentTheme][semanticSlot] = new Pen(GetColor(semanticSlot));
            }

            return ThemePens[CurrentTheme][semanticSlot];
        }

        /// <summary>
        /// Returns a pen for the given semantic slot for the given theme. It's internally managed; do not
        /// dispose or modify it.
        /// </summary>
        public Pen GetPen(ThemeName themeName, ThemeSlot semanticSlot)
        {
            if (disposedValue)
            {
                return Pens.Transparent;
            }

            if (ThemePens[themeName][semanticSlot] == null)
            {
                ThemePens[themeName][semanticSlot] = new Pen(GetColor(themeName, semanticSlot));
            }

            return ThemePens[themeName][semanticSlot];
        }
        #endregion

        #region IDisposable
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                ThemeName[] names = Enum.GetValues<ThemeName>();
                ThemeSlot[] slots = Enum.GetValues<ThemeSlot>();

                foreach (var name in names)
                {
                    foreach (var slot in slots)
                    {
                        ThemeBrushes[name][slot]?.Dispose();
                        ThemePens[name][slot]?.Dispose();
                    }
                }

                instance = null;
                disposedValue = true;
            }
        }

        ~SemanticTheme()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
