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
        /// The closeness of applied brush images while drawing.
        /// </summary>
        public int BrushDensity
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
        /// Adds a random amount of hue to each stroke.
        /// </summary>
        public int RandMaxH
        {
            get;
            set;
        }

        /// <summary>
        /// Adds a random amount of saturation to each stroke.
        /// </summary>
        public int RandMaxS
        {
            get;
            set;
        }

        /// <summary>
        /// Adds a random amount of value to each stroke.
        /// </summary>
        public int RandMaxV
        {
            get;
            set;
        }

        /// <summary>
        /// Subtracts a random amount of hue from each stroke.
        /// </summary>
        public int RandMinH
        {
            get;
            set;
        }

        /// <summary>
        /// Subtracts a random amount of saturation from each stroke.
        /// </summary>
        public int RandMinS
        {
            get;
            set;
        }

        /// <summary>
        /// Subtracts a random amount of value from each stroke.
        /// </summary>
        public int RandMinV
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

        public int TabPressureBrushAlpha { get; set; }
        public int TabPressureBrushDensity { get; set; }
        public int TabPressureBrushRotation { get; set; }
        public int TabPressureBrushSize { get; set; }
        public int TabPressureMaxBlueJitter { get; set; }
        public int TabPressureMaxGreenJitter { get; set; }
        public int TabPressureMaxHueJitter { get; set; }
        public int TabPressureMaxRedJitter { get; set; }
        public int TabPressureMaxSatJitter { get; set; }
        public int TabPressureMaxValueJitter { get; set; }
        public int TabPressureMinBlueJitter { get; set; }
        public int TabPressureMinDrawDistance { get; set; }
        public int TabPressureMinGreenJitter { get; set; }
        public int TabPressureMinHueJitter { get; set; }
        public int TabPressureMinRedJitter { get; set; }
        public int TabPressureMinSatJitter { get; set; }
        public int TabPressureMinValueJitter { get; set; }
        public int TabPressureRandHorShift { get; set; }
        public int TabPressureRandMaxSize { get; set; }
        public int TabPressureRandMinAlpha { get; set; }
        public int TabPressureRandMinSize { get; set; }
        public int TabPressureRandRotLeft { get; set; }
        public int TabPressureRandRotRight { get; set; }
        public int TabPressureRandVerShift { get; set; }
        public int CmbxTabPressureBrushAlpha { get; set; }
        public int CmbxTabPressureBrushDensity { get; set; }
        public int CmbxTabPressureBrushRotation { get; set; }
        public int CmbxTabPressureBrushSize { get; set; }
        public int CmbxTabPressureBlueJitter { get; set; }
        public int CmbxTabPressureGreenJitter { get; set; }
        public int CmbxTabPressureHueJitter { get; set; }
        public int CmbxTabPressureMinDrawDistance { get; set; }
        public int CmbxTabPressureRedJitter { get; set; }
        public int CmbxTabPressureSatJitter { get; set; }
        public int CmbxTabPressureValueJitter { get; set; }
        public int CmbxTabPressureRandHorShift { get; set; }
        public int CmbxTabPressureRandMaxSize { get; set; }
        public int CmbxTabPressureRandMinAlpha { get; set; }
        public int CmbxTabPressureRandMinSize { get; set; }
        public int CmbxTabPressureRandRotLeft { get; set; }
        public int CmbxTabPressureRandRotRight { get; set; }
        public int CmbxTabPressureRandVerShift { get; set; }
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
            BrushDensity = 10;
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
            RandMaxH = 0;
            RandMaxS = 0;
            RandMaxV = 0;
            RandMinH = 0;
            RandMinS = 0;
            RandMinV = 0;
            SizeChange = 0;
            RotChange = 0;
            AlphaChange = 0;
            Symmetry = SymmetryMode.None;
            CmbxTabPressureBrushAlpha = 0;
            CmbxTabPressureBrushDensity = 0;
            CmbxTabPressureBrushRotation = 0;
            CmbxTabPressureBrushSize = 0;
            CmbxTabPressureBlueJitter = 0;
            CmbxTabPressureGreenJitter = 0;
            CmbxTabPressureHueJitter = 0;
            CmbxTabPressureMinDrawDistance = 0;
            CmbxTabPressureRedJitter = 0;
            CmbxTabPressureSatJitter = 0;
            CmbxTabPressureValueJitter = 0;
            CmbxTabPressureRandHorShift = 0;
            CmbxTabPressureRandMaxSize = 0;
            CmbxTabPressureRandMinAlpha = 0;
            CmbxTabPressureRandMinSize = 0;
            CmbxTabPressureRandRotLeft = 0;
            CmbxTabPressureRandRotRight = 0;
            CmbxTabPressureRandVerShift = 0;
            TabPressureBrushAlpha = 0;
            TabPressureBrushDensity = 0;
            TabPressureBrushRotation = 0;
            TabPressureBrushSize = 0;
            TabPressureMaxBlueJitter = 0;
            TabPressureMaxGreenJitter = 0;
            TabPressureMaxHueJitter = 0;
            TabPressureMaxRedJitter = 0;
            TabPressureMaxSatJitter = 0;
            TabPressureMaxValueJitter = 0;
            TabPressureMinBlueJitter = 0;
            TabPressureMinDrawDistance = 0;
            TabPressureMinGreenJitter = 0;
            TabPressureMinHueJitter = 0;
            TabPressureMinRedJitter = 0;
            TabPressureMinSatJitter = 0;
            TabPressureMinValueJitter = 0;
            TabPressureRandHorShift = 0;
            TabPressureRandMaxSize = 0;
            TabPressureRandMinAlpha = 0;
            TabPressureRandMinSize = 0;
            TabPressureRandRotLeft = 0;
            TabPressureRandRotRight = 0;
            TabPressureRandVerShift = 0;
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
            BrushDensity = other.BrushDensity;
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
            RandMaxH = other.RandMaxH;
            RandMaxS = other.RandMaxS;
            RandMaxV = other.RandMaxV;
            RandMinH = other.RandMinH;
            RandMinS = other.RandMinS;
            RandMinV = other.RandMinV;
            CmbxTabPressureBrushAlpha = other.CmbxTabPressureBrushAlpha;
            CmbxTabPressureBrushDensity = other.CmbxTabPressureBrushDensity;
            CmbxTabPressureBrushRotation = other.CmbxTabPressureBrushRotation;
            CmbxTabPressureBrushSize = other.CmbxTabPressureBrushSize;
            CmbxTabPressureBlueJitter = other.CmbxTabPressureBlueJitter;
            CmbxTabPressureGreenJitter = other.CmbxTabPressureGreenJitter;
            CmbxTabPressureHueJitter = other.CmbxTabPressureHueJitter;
            CmbxTabPressureMinDrawDistance = other.CmbxTabPressureMinDrawDistance;
            CmbxTabPressureRedJitter = other.CmbxTabPressureRedJitter;
            CmbxTabPressureSatJitter = other.CmbxTabPressureSatJitter;
            CmbxTabPressureValueJitter = other.CmbxTabPressureValueJitter;
            CmbxTabPressureRandHorShift = other.CmbxTabPressureRandHorShift;
            CmbxTabPressureRandMaxSize = other.CmbxTabPressureRandMaxSize;
            CmbxTabPressureRandMinAlpha = other.CmbxTabPressureRandMinAlpha;
            CmbxTabPressureRandMinSize = other.CmbxTabPressureRandMinSize;
            CmbxTabPressureRandRotLeft = other.CmbxTabPressureRandRotLeft;
            CmbxTabPressureRandRotRight = other.CmbxTabPressureRandRotRight;
            CmbxTabPressureRandVerShift = other.CmbxTabPressureRandVerShift;
            TabPressureBrushAlpha = other.TabPressureBrushAlpha;
            TabPressureBrushDensity = other.TabPressureBrushDensity;
            TabPressureBrushRotation = other.TabPressureBrushRotation;
            TabPressureBrushSize = other.TabPressureBrushSize;
            TabPressureMaxBlueJitter = other.TabPressureMaxBlueJitter;
            TabPressureMaxGreenJitter = other.TabPressureMaxGreenJitter;
            TabPressureMaxHueJitter = other.TabPressureMaxHueJitter;
            TabPressureMaxRedJitter = other.TabPressureMaxRedJitter;
            TabPressureMaxSatJitter = other.TabPressureMaxSatJitter;
            TabPressureMaxValueJitter = other.TabPressureMaxValueJitter;
            TabPressureMinBlueJitter = other.TabPressureMinBlueJitter;
            TabPressureMinDrawDistance = other.TabPressureMinDrawDistance;
            TabPressureMinGreenJitter = other.TabPressureMinGreenJitter;
            TabPressureMinHueJitter = other.TabPressureMinHueJitter;
            TabPressureMinRedJitter = other.TabPressureMinRedJitter;
            TabPressureMinSatJitter = other.TabPressureMinSatJitter;
            TabPressureMinValueJitter = other.TabPressureMinValueJitter;
            TabPressureRandHorShift = other.TabPressureRandHorShift;
            TabPressureRandMaxSize = other.TabPressureRandMaxSize;
            TabPressureRandMinAlpha = other.TabPressureRandMinAlpha;
            TabPressureRandMinSize = other.TabPressureRandMinSize;
            TabPressureRandRotLeft = other.TabPressureRandRotLeft;
            TabPressureRandRotRight = other.TabPressureRandRotRight;
            TabPressureRandVerShift = other.TabPressureRandVerShift;
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