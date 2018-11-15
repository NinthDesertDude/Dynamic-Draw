using System.Collections.Generic;
using System.Drawing;

namespace BrushFactory
{
    /// <summary>
    /// Represents the settings used in the dialog so they can be stored and
    /// loaded when applying the effect consecutively for convenience.
    /// </summary>
    public class PersistentSettings : PaintDotNet.Effects.EffectConfigToken
    {
        #region Fields
        /// <summary>
        /// Increments/decrements the alpha by an amount after each stroke.
        /// </summary>
        public int AlphaChange
        {
            get;
            set;
        }

        /// <summary>
        /// The brush's transparency.
        /// </summary>
        public int BrushAlpha
        {
            get;
            set;
        }

        /// <summary>
        /// The color of the brush, which replaces the brush color.
        /// </summary>
        public Color BrushColor
        {
            get;
            set;
        }

        /// <summary>
        /// The active brush index, as chosen from built-in brushes.
        /// </summary>
        public string BrushName
        {
            get;
            set;
        }

        /// <summary>
        /// The brush's orientation in degrees.
        /// </summary>
        public int BrushRotation
        {
            get;
            set;
        }

        /// <summary>
        /// The brush's radius.
        /// </summary>
        public int BrushSize
        {
            get;
            set;
        }

        /// <summary>
        /// Contains a list of all custom brushes to reload. The dialog will
        /// attempt to read the paths of each brush and add them if possible.
        /// </summary>
        public HashSet<string> CustomBrushLocations
        {
            get;
            set;
        }

        /// <summary>
        /// Whether the brush rotates with the mouse direction or not.
        /// </summary>
        public bool DoRotateWithMouse
        {
            get;
            set;
        }

        /// <summary>
        /// Whether to overwrite brush colors when drawing or not.
        /// </summary>
        public bool DoColorizeBrush
        {
            get;
            set;
        }

        /// <summary>
        /// Whether to prevent brush strokes from changing alpha or not.
        /// </summary>
        public bool DoLockAlpha
        {
            get;
            set;
        }

        /// <summary>
        /// Whether to save the settings when the effect is applied.
        /// </summary>
        public bool DoSaveSettings
        {
            get;
            set;
        }

        /// <summary>
        /// Randomized maximum brush transparency.
        /// </summary>
        public int RandMaxAlpha
        {
            get;
            set;
        }

        /// <summary>
        /// Randomized maximum brush size.
        /// </summary>
        public int RandMaxSize
        {
            get;
            set;
        }

        /// <summary>
        /// Randomized minimum brush transparency.
        /// </summary>
        public int RandMinAlpha
        {
            get;
            set;
        }

        /// <summary>
        /// Randomized minimum brush size.
        /// </summary>
        public int RandMinSize
        {
            get;
            set;
        }

        /// <summary>
        /// Randomized counter-clockwise rotation.
        /// </summary>
        public int RandRotLeft
        {
            get;
            set;
        }

        /// <summary>
        /// Randomized clockwise rotation.
        /// </summary>
        public int RandRotRight
        {
            get;
            set;
        }

        /// <summary>
        /// Randomized horizontal shifting with respect to canvas size.
        /// </summary>
        public int RandHorzShift
        {
            get;
            set;
        }

        /// <summary>
        /// Randomized vertical shifting with respect to canvas size.
        /// </summary>
        public int RandVertShift
        {
            get;
            set;
        }

        /// <summary>
        /// Doesn't apply brush strokes until the mouse is a certain distance
        /// from its last location.
        /// </summary>
        public int MinDrawDistance
        {
            get;
            set;
        }

        /// <summary>
        /// Adds a random amount of red to each stroke.
        /// </summary>
        public int RandMaxR
        {
            get;
            set;
        }

        /// <summary>
        /// Adds a random amount of green to each stroke.
        /// </summary>
        public int RandMaxG
        {
            get;
            set;
        }

        /// <summary>
        /// Adds a random amount of blue to each stroke.
        /// </summary>
        public int RandMaxB
        {
            get;
            set;
        }

        /// <summary>
        /// Subtracts a random amount of red from each stroke.
        /// </summary>
        public int RandMinR
        {
            get;
            set;
        }

        /// <summary>
        /// Subtracts a random amount of green from each stroke.
        /// </summary>
        public int RandMinG
        {
            get;
            set;
        }

        /// <summary>
        /// Subtracts a random amount of blue from each stroke.
        /// </summary>
        public int RandMinB
        {
            get;
            set;
        }

        /// <summary>
        /// Increments/decrements the size by an amount after each stroke.
        /// </summary>
        public int SizeChange
        {
            get;
            set;
        }

        /// <summary>
        /// Increments/decrements the rotation by an amount after each stroke.
        /// </summary>
        public int RotChange
        {
            get;
            set;
        }

        /// <summary>
        /// Sets whether to draw horizontal, vertical, or radial reflections
        /// of the current image.
        /// </summary>
        public SymmetryMode Symmetry
        {
            get;
            set;
        }
        #endregion

        /// <summary>
        /// Creates a new settings token.
        /// </summary>
        public PersistentSettings()
        {
            BrushSize = 20;
            BrushName = string.Empty;
            BrushRotation = 0;
            BrushAlpha = 0;
            BrushColor = UserSettings.userPrimaryColor;
            RandMaxAlpha = 0;
            RandMinAlpha = 0;
            RandMaxSize = 0;
            RandMinSize = 0;
            RandRotLeft = 0;
            RandRotRight = 0;
            RandHorzShift = 0;
            RandVertShift = 0;
            DoRotateWithMouse = false;
            DoColorizeBrush = true;
            DoLockAlpha = false;
            MinDrawDistance = 0;
            RandMaxR = 0;
            RandMaxG = 0;
            RandMaxB = 0;
            RandMinR = 0;
            RandMinG = 0;
            RandMinB = 0;
            SizeChange = 0;
            RotChange = 0;
            AlphaChange = 0;
            Symmetry = SymmetryMode.None;
            CustomBrushLocations = new HashSet<string>();
        }

        /// <summary>
        /// Copies all settings to another token.
        /// </summary>
        protected PersistentSettings(PersistentSettings other)
            : base(other)
        {
            BrushSize = other.BrushSize;
            BrushName = other.BrushName;
            BrushRotation = other.BrushRotation;
            BrushAlpha = other.BrushAlpha;
            BrushColor = other.BrushColor;
            RandMaxAlpha = other.RandMaxAlpha;
            RandMinAlpha = other.RandMinAlpha;
            RandMaxSize = other.RandMaxSize;
            RandMinSize = other.RandMinSize;
            RandRotLeft = other.RandRotLeft;
            RandRotRight = other.RandRotRight;
            RandHorzShift = other.RandHorzShift;
            RandVertShift = other.RandVertShift;
            DoRotateWithMouse = other.DoRotateWithMouse;
            DoColorizeBrush = other.DoColorizeBrush;
            DoLockAlpha = other.DoLockAlpha;
            MinDrawDistance = other.MinDrawDistance;
            RandMaxR = other.RandMaxR;
            RandMaxG = other.RandMaxG;
            RandMaxB = other.RandMaxB;
            RandMinR = other.RandMinR;
            RandMinG = other.RandMinG;
            RandMinB = other.RandMinB;
            SizeChange = other.SizeChange;
            RotChange = other.RotChange;
            AlphaChange = other.AlphaChange;
            Symmetry = other.Symmetry;
            CustomBrushLocations = new HashSet<string>(
                other.CustomBrushLocations,
                other.CustomBrushLocations.Comparer);
        }

        /// <summary>
        /// Copies all settings to another token.
        /// </summary>
        public override object Clone()
        {
            return new PersistentSettings(this);
        }
    }
}