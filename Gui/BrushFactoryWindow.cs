using BrushFactory.Abr;
using BrushFactory.Properties;
using PaintDotNet;
using PaintDotNet.Effects;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace BrushFactory
{
    /// <summary>
    /// The dialog used for working with the effect.
    /// </summary>
    public class WinBrushFactory : EffectConfigDialog
    {
        #region Fields
        /// <summary>
        /// Creates the list of brushes used by the brush selector.
        /// </summary>
        private BindingList<BrushSelectorItem> loadedBrushes;

        /// <summary>
        /// Stores the user's custom brushes by file and path until it can
        /// be copied to persistent settings, or ignored.
        /// </summary>
        private HashSet<string> loadedBrushPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Whether the user is drawing on the image.
        /// </summary>
        private bool isUserDrawing = false;

        /// <summary>
        /// Whether the user is panning the image.
        /// </summary>
        private bool isUserPanning = false;

        /// <summary>
        /// Stores the current mouse location.
        /// </summary>
        private Point mouseLoc = new Point();

        /// <summary>
        /// Stores the mouse location at the last place a brush stroke was
        /// successfully applied. Used exclusively by minimum draw distance.
        /// </summary>
        private Point? mouseLocBrush;

        /// <summary>
        /// Stores the previous mouse location.
        /// </summary>
        private Point mouseLocPrev = new Point();

        /// <summary>
        /// Sets up a randomizer for brush dynamics.
        /// </summary>
        private Random random = new Random();

        /// <summary>
        ///Stores a list of temporary files by name, to be used by redo. Files
        ///will be reloaded to redo changes.
        /// </summary>
        private Stack<string> redoHistory = new Stack<string>();

        /// <summary>
        ///Stores a list of temporary files by name, to be used by undo. Files
        ///will be reloaded to undo changes.
        /// </summary>
        private Stack<string> undoHistory = new Stack<string>();

        /// <summary>
        /// Contains the current brush (without modifications like alpha).
        /// </summary>
        private Bitmap bmpBrush;

        /// <summary>
        /// Contains the current brush with all modifications applied. This is
        /// overwritten by the original brush to apply new changes so changes
        /// are not cumulative. For example, applying 25% alpha repeatedly
        /// would otherwise make the brush unusable.
        /// </summary>
        private Bitmap bmpBrushEffects;

        /// <summary>
        /// Stores the current drawing in full.
        /// </summary>
        private Bitmap bmpCurrentDrawing = new Bitmap(1, 1);

        /// <summary>
        /// Controls the color (not alpha) of the drawing brush.
        /// </summary>
        private Button bttnBrushColor;

        /// <summary>
        /// Displays a list of brushes to choose from.
        /// </summary>
        private ComboBox bttnBrushSelector;

        /// <summary>
        /// Handles the smoothing (interpolation) of each brush stroke.
        /// </summary>
        private ComboBox bttnBrushSmoothing;

        /// <summary>
        /// Allows the user to cancel and exit without applying the effect.
        /// </summary>
        private Button bttnCancel;

        /// <summary>
        /// Removes all custom brushes imported by the user.
        /// </summary>
        private Button bttnClearBrushes;

        /// <summary>
        /// Resets all settings to their default values.
        /// </summary>
        private Button bttnClearSettings;

        /// <summary>
        /// Sets permanent directories to browse for brushes on load.
        /// </summary>
        private Button bttnCustomBrushLocations;

        /// <summary>
        /// Allows the user to accept and apply the effect.
        /// </summary>
        private Button bttnOk;

        /// <summary>
        /// Allows the user to redo a previously undone change.
        /// </summary>
        private Button bttnRedo;

        /// <summary>
        /// Allows the user to undo a committed change.
        /// </summary>
        private Button bttnUndo;

        /// <summary>
        /// When active, the brush color will be replaced with the chosen
        /// color. When inactive, the brush colors will be unchanged and
        /// color-changing settings have no effect.
        /// </summary>
        private CheckBox chkbxColorizeBrush;

        /// <summary>
        /// Preserves the alpha channel during brush strokes.
        /// </summary>
        private CheckBox chkbxLockAlpha;

        /// <summary>
        /// When active, the brush will be affected by the mouse angle. By
        /// default, brushes "facing" to the right (like an 'arrow brush')
        /// will point in the same direction as the mouse, while brushes that
        /// don't already point to the right will seem to be offset by some
        /// amount. The brush rotation can be used as a relative offset to fix
        /// this.
        /// </summary>
        private CheckBox chkbxOrientToMouse;

        /// <summary>
        /// All non-GUI controls must register the components container as the
        /// parent so they can be disposed when the form exits.
        /// </summary>
        private IContainer components;

        /// <summary>
        /// Contains the current image being drawn on.
        /// </summary>
        internal PictureBox displayCanvas;

        /// <summary>
        /// Draws a checkerboard background behind the drawing region.
        /// </summary>
        private Panel displayCanvasBG;

        /// <summary>
        /// Stores the zoom percentage for the drawing region.
        /// </summary>
        private float displayCanvasZoom = 1;

        /// <summary>
        /// Labels the miscellaneous brush options area.
        /// </summary>
        private GroupBox grpbxBrushOptions;

        /// <summary>
        /// Determines the direction of alpha shifting, which can be growing
        /// (true) or shrinking (false). Used by the alpha shift slider.
        /// </summary>
        private bool isGrowingAlpha = true;

        /// <summary>
        /// Determines the direction of size shifting, which can be growing
        /// (true) or shrinking (false). Used by the size shift slider.
        /// </summary>
        private bool isGrowingSize = true;

        /// <summary>
        /// The outline of the user's selection.
        /// </summary>
        private PdnRegion selectionOutline;

        /// <summary>
        /// Contains the list of all interpolation options for applying brush
        /// strokes.
        /// </summary>
        BindingList<InterpolationItem> smoothingMethods;

        /// <summary>
        /// Contains the list of all symmetry options for using brush strokes.
        /// </summary>
        BindingList<Tuple<string, SymmetryMode>> symmetryOptions;

        /// <summary>
        /// Controls the brush transparency.
        /// </summary>
        private TrackBar sliderBrushAlpha;

        /// <summary>
        /// Controls the brush orientation.
        /// </summary>
        private TrackBar sliderBrushRotation;

        /// <summary>
        /// Controls the size of the drawing brush.
        /// </summary>
        private TrackBar sliderBrushSize;

        /// <summary>
        /// Controls the zooming factor for the drawing region.
        /// </summary>
        private TrackBar sliderCanvasZoom;

        /// <summary>
        /// Controls the maximum brush color blueness.
        /// </summary>
        private TrackBar sliderRandMaxBlue;

        /// <summary>
        /// Controls the maximum brush color greenness.
        /// </summary>
        private TrackBar sliderRandMaxGreen;

        /// <summary>
        /// Controls the maximum brush color redness.
        /// </summary>
        private TrackBar sliderRandMaxRed;

        /// <summary>
        /// Controls the minimum brush color blueness.
        /// </summary>
        private TrackBar sliderRandMinBlue;

        /// <summary>
        /// The mouse must be at least this far away from its last successful
        /// brush stroke position to create another brush stroke. Used for
        /// spacing between strokes.
        /// </summary>
        private TrackBar sliderMinDrawDistance;

        /// <summary>
        /// Controls the minimum brush color greenness.
        /// </summary>
        private TrackBar sliderRandMinGreen;

        /// <summary>
        /// Controls the minimum brush color redness.
        /// </summary>
        private TrackBar sliderRandMinRed;

        /// <summary>
        /// Randomly repositions the brush left or right while drawing.
        /// </summary>
        private TrackBar sliderRandHorzShift;

        /// <summary>
        /// Controls the maximum transparency amount.
        /// </summary>
        private TrackBar sliderRandMaxAlpha;

        /// <summary>
        /// Controls the maximum brush size range.
        /// </summary>
        private TrackBar sliderRandMaxSize;

        /// <summary>
        /// Controls the minimum transparency amount.
        /// </summary>
        private TrackBar sliderRandMinAlpha;

        /// <summary>
        /// Controls the minimum brush size range.
        /// </summary>
        private TrackBar sliderRandMinSize;

        /// <summary>
        /// Controls the minimum brush rotation range, which is negative.
        /// </summary>
        private TrackBar sliderRandRotLeft;

        /// <summary>
        /// Controls the maximum brush rotation range, which is positive.
        /// </summary>
        private TrackBar sliderRandRotRight;

        /// <summary>
        /// Randomly repositions the brush up or down while drawing.
        /// </summary>
        private TrackBar sliderRandVertShift;

        /// <summary>
        /// Allows the brush transparency to change by adding this value,
        /// which may be negative, on each successful brush stroke.
        /// </summary>
        private TrackBar sliderShiftAlpha;

        /// <summary>
        /// Allows the brush rotation to change by adding this value, which
        /// may be negative, on each successful brush stroke.
        /// </summary>
        private TrackBar sliderShiftRotation;

        /// <summary>
        /// Allows the brush size to change by adding this value, which may be
        /// negative, on each successful brush stroke.
        /// </summary>
        private TrackBar sliderShiftSize;

        /// <summary>
        /// Contains all tab pages.
        /// </summary>
        private TabControl tabBar;

        /// <summary>
        /// Contains the main, important controls.
        /// </summary>
        private TabPage tabControls;

        /// <summary>
        /// Contains controls for randomly changing brush settings without
        /// regard to the previous randomly-selected settings.
        /// </summary>
        private TabPage tabJitter;

        /// <summary>
        /// Contains a collection of more complex or less used options.
        /// </summary>
        private TabPage tabColor;

        /// <summary>
        /// Contains controls for incrementing brush settings a specified
        /// amount on each successful brush stroke.
        /// </summary>
        private TabPage tabOther;

        /// <summary>
        /// Tracks when the user draws out-of-bounds and moves the canvas to
        /// accomodate them.
        /// </summary>
        private Timer timerRepositionUpdate;

        /// <summary>
        /// Draws the name of the brush transparency slider.
        /// </summary>
        private Label txtBrushAlpha;

        /// <summary>
        /// Draws the name of the brush rotation slider.
        /// </summary>
        private Label txtBrushRotation;

        /// <summary>
        /// Draws the name of the brush size slider.
        /// </summary>
        private Label txtBrushSize;

        /// <summary>
        /// Draws the name of the canvas zoom slider.
        /// </summary>
        private Label txtCanvasZoom;

        /// <summary>
        /// Draws the name of the maximum blueness slider.
        /// </summary>
        private Label txtRandMaxBlue;

        /// <summary>
        /// Draws the name of the maximum greenness slider.
        /// </summary>
        private Label txtRandMaxGreen;

        /// <summary>
        /// Draws the name of the maximum redness slider.
        /// </summary>
        private Label txtRandMaxRed;

        /// <summary>
        /// Draws the name of the minimum blueness slider.
        /// </summary>
        private Label txtRandMinBlue;

        /// <summary>
        /// Draws the name of the minimum drawing distance slider.
        /// </summary>
        private Label txtMinDrawDistance;

        /// <summary>
        /// Draws the name of the minimum greenness slider.
        /// </summary>
        private Label txtRandMinGreen;

        /// <summary>
        /// Draws the name of the minimum redness slider.
        /// </summary>
        private Label txtRandMinRed;

        /// <summary>
        /// Draws the name of the random horizontal shift slider.
        /// </summary>
        private Label txtRandHorzShift;

        /// <summary>
        /// Draws the name of the random rotation to the right slider.
        /// </summary>
        private Label txtRandRotRight;

        /// <summary>
        /// Draws the name of the random rotation to the left slider.
        /// </summary>
        private Label txtRandRotLeft;

        /// <summary>
        /// Draws the name of the random max alpha slider.
        /// </summary>
        private Label txtRandMaxAlpha;

        /// <summary>
        /// Draws the name of the random max size slider.
        /// </summary>
        private Label txtRandMaxSize;

        /// <summary>
        /// Draws the name of the random min alpha slider.
        /// </summary>
        private Label txtRandMinAlpha;

        /// <summary>
        /// Draws the name of the random min size slider.
        /// </summary>
        private Label txtRandMinSize;

        /// <summary>
        /// Draws the name of the random vertical shift slider.
        /// </summary>
        private Label txtRandVertShift;

        /// <summary>
        /// Draws the name of the shift alpha slider.
        /// </summary>
        private Label txtShiftAlpha;

        /// <summary>
        /// Draws the name of the shift rotation slider.
        /// </summary>
        private Label txtShiftRotation;

        /// <summary>
        /// Draws the name of the shift size slider.
        /// </summary>
        private Label txtShiftSize;

        /// <summary>
        /// The user can enable symmetry to draw mirrored brush strokes.
        /// </summary>
        private ComboBox bttnSymmetry;

        /// <summary>
        /// Provides useful messages when hovering over controls.
        /// </summary>
        private Label txtTooltip;

        /// <summary>
        /// A temporary folder that is deleted when the dialog exits.
        /// </summary>
        private TempDirectory tempDir;
        #endregion

        #region Constructors
        /// <summary>
        /// Initializes components and brushes.
        /// </summary>
        public WinBrushFactory()
        {
            InitializeComponent();

            TempDirectory.CleanupPreviousDirectories();
            tempDir = new TempDirectory();

            InitBrushes();

            //Configures items for the smoothing method combobox.
            smoothingMethods = new BindingList<InterpolationItem>();
            smoothingMethods.Add(new InterpolationItem("Smoothing: Normal", InterpolationMode.Bilinear));
            smoothingMethods.Add(new InterpolationItem("Smoothing: Higher", InterpolationMode.Bicubic));
            smoothingMethods.Add(new InterpolationItem("Smoothing: High", InterpolationMode.HighQualityBilinear));
            smoothingMethods.Add(new InterpolationItem("Smoothing: Highest", InterpolationMode.HighQualityBicubic));
            smoothingMethods.Add(new InterpolationItem("Smoothing: Jagged", InterpolationMode.NearestNeighbor));
            bttnBrushSmoothing.DataSource = smoothingMethods;
            bttnBrushSmoothing.DisplayMember = "Name";
            bttnBrushSmoothing.ValueMember = "Method";

            //Configures items for the symmetry options combobox.
            symmetryOptions = new BindingList<Tuple<string, SymmetryMode>>();
            symmetryOptions.Add(new Tuple<string, SymmetryMode>("Symmetry: None", SymmetryMode.None));
            symmetryOptions.Add(new Tuple<string, SymmetryMode>("Symmetry: Horizontal", SymmetryMode.Horizontal));
            symmetryOptions.Add(new Tuple<string, SymmetryMode>("Symmetry: Vertical", SymmetryMode.Vertical));
            symmetryOptions.Add(new Tuple<string, SymmetryMode>("Symmetry: Both", SymmetryMode.Star2));
            symmetryOptions.Add(new Tuple<string, SymmetryMode>("Symmetry: 3-point", SymmetryMode.Star3));
            symmetryOptions.Add(new Tuple<string, SymmetryMode>("Symmetry: 4-point", SymmetryMode.Star4));
            symmetryOptions.Add(new Tuple<string, SymmetryMode>("Symmetry: 5-point", SymmetryMode.Star5));
            symmetryOptions.Add(new Tuple<string, SymmetryMode>("Symmetry: 6-point", SymmetryMode.Star6));
            symmetryOptions.Add(new Tuple<string, SymmetryMode>("Symmetry: 7-point", SymmetryMode.Star7));
            symmetryOptions.Add(new Tuple<string, SymmetryMode>("Symmetry: 8-point", SymmetryMode.Star8));
            symmetryOptions.Add(new Tuple<string, SymmetryMode>("Symmetry: 9-point", SymmetryMode.Star9));
            symmetryOptions.Add(new Tuple<string, SymmetryMode>("Symmetry: 10-point", SymmetryMode.Star10));
            symmetryOptions.Add(new Tuple<string, SymmetryMode>("Symmetry: 11-point", SymmetryMode.Star11));
            symmetryOptions.Add(new Tuple<string, SymmetryMode>("Symmetry: 12-point", SymmetryMode.Star12));
            bttnSymmetry.DataSource = symmetryOptions;
            bttnSymmetry.DisplayMember = "Item1";
            bttnSymmetry.ValueMember = "Item2";

            //Forces the window to cover the screen without being maximized.
            Left = Top = 0;
            Width = Screen.PrimaryScreen.WorkingArea.Width;
            Height = Screen.PrimaryScreen.WorkingArea.Height;
        }
        #endregion

        #region Methods (overridden)
        /// <summary>
        /// Configures settings so they can be stored between consecutive
        /// calls of the effect.
        /// </summary>
        protected override void InitialInitToken()
        {
            theEffectToken = new PersistentSettings(20, "", 0, 0,
                UserSettings.userPrimaryColor, 0, 0, 0, 0, 0, 0, 0, 0, false, true,
                false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, SymmetryMode.None, new HashSet<string>());
        }

        /// <summary>
        /// Sets up the GUI to reflect the previously-used settings; i.e. this
        /// loads the settings. Called twice by a quirk of Paint.NET.
        /// </summary>
        protected override void InitDialogFromToken(EffectConfigToken effectToken)
        {
            //Copies GUI values from the settings.
            PersistentSettings token = (PersistentSettings)effectToken;
            sliderBrushSize.Value = token.BrushSize;

            //Loads custom brushes if possible, but skips duplicates. This
            //method is called twice by Paint.NET for some reason, so this
            //ensures there are no duplicates. Brush names are unique.
            if (token.CustomBrushLocations.Count > 0 && !token.CustomBrushLocations.SetEquals(loadedBrushPaths))
            {
                foreach (string path in token.CustomBrushLocations)
                {
                    if (loadedBrushPaths.Add(path))
                    {
                        //Ensures the brush location is preserved, then loads
                        //the brush.
                        ImportBrushes(
                            new string[] { path },
                            false,
                            false);
                    }
                }
            }

            //Attempts to find the brush's index in the current list of
            //brushes, by name. If it doesn't exist, it's set to default: "".
            int brushIndex = loadedBrushes.ToList()
                .FindIndex(o => o.Name.Equals(token.BrushName));

            //Doesn't copy custom brushes and brushes that weren't found.
            if (token.BrushName.Equals("") ||
                token.BrushName.Equals(BrushSelectorItem.CustomBrush.Name) ||
                brushIndex == -1)
            {
                bttnBrushSelector.SelectedIndex = 0;
            }
            else
            {
                bttnBrushSelector.SelectedIndex = brushIndex;
            }

            //Sets the brush color to the primary color if it was transparent.
            //Else, copies it. This works since the user colors are opaque.
            bttnBrushColor.BackColor = token.BrushColor;

            //Sets the text color for visibility against the back color.
            Color oppositeColor = Color.FromArgb(
                (byte)(255 - token.BrushColor.R),
                (byte)(255 - token.BrushColor.G),
                (byte)(255 - token.BrushColor.B));

            bttnBrushColor.ForeColor = oppositeColor;

            //Sets all other fields.
            sliderBrushRotation.Value = token.BrushRotation;
            sliderBrushAlpha.Value = token.BrushAlpha;
            sliderRandHorzShift.Value = token.RandHorzShift;
            sliderRandMaxAlpha.Value = token.RandMaxAlpha;
            sliderRandMaxSize.Value = token.RandMaxSize;
            sliderRandMinAlpha.Value = token.RandMinAlpha;
            sliderRandMinSize.Value = token.RandMinSize;
            sliderRandRotLeft.Value = token.RandRotLeft;
            sliderRandRotRight.Value = token.RandRotRight;
            sliderRandVertShift.Value = token.RandVertShift;
            chkbxOrientToMouse.Checked = token.DoRotateWithMouse;
            chkbxColorizeBrush.Checked = token.DoColorizeBrush;
            chkbxLockAlpha.Checked = token.DoLockAlpha;
            sliderMinDrawDistance.Value = token.MinDrawDistance;
            sliderRandMaxRed.Value = token.RandMaxR;
            sliderRandMaxGreen.Value = token.RandMaxG;
            sliderRandMaxBlue.Value = token.RandMaxB;
            sliderRandMinRed.Value = token.RandMinR;
            sliderRandMinGreen.Value = token.RandMinG;
            sliderRandMinBlue.Value = token.RandMinB;
            sliderShiftSize.Value = token.SizeChange;
            sliderShiftRotation.Value = token.RotChange;
            sliderShiftAlpha.Value  = token.AlphaChange;
            bttnSymmetry.SelectedIndex = (int)token.Symmetry;

            //Re-applies color and alpha information.
            UpdateBrush();
        }

        /// <summary>
        /// Overwrites the settings with the dialog's current settings so they
        /// can be reused later; i.e. this saves the settings.
        /// </summary>
        protected override void InitTokenFromDialog()
        {
            var token = (PersistentSettings)EffectToken;

            token.BrushSize = sliderBrushSize.Value;
            token.BrushName = (bttnBrushSelector.SelectedItem as BrushSelectorItem).Name;
            token.BrushColor = bttnBrushColor.BackColor;
            token.BrushRotation = sliderBrushRotation.Value;
            token.BrushAlpha = sliderBrushAlpha.Value;
            token.RandHorzShift = sliderRandHorzShift.Value;
            token.RandMaxAlpha = sliderRandMaxAlpha.Value;
            token.RandMaxSize = sliderRandMaxSize.Value;
            token.RandMinAlpha = sliderRandMinAlpha.Value;
            token.RandMinSize = sliderRandMinSize.Value;
            token.RandRotLeft = sliderRandRotLeft.Value;
            token.RandRotRight = sliderRandRotRight.Value;
            token.RandVertShift = sliderRandVertShift.Value;
            token.DoRotateWithMouse = chkbxOrientToMouse.Checked;
            token.DoColorizeBrush = chkbxColorizeBrush.Checked;
            token.DoLockAlpha = chkbxLockAlpha.Checked;
            token.MinDrawDistance = sliderMinDrawDistance.Value;
            token.RandMaxR = sliderRandMaxRed.Value;
            token.RandMaxG = sliderRandMaxGreen.Value;
            token.RandMaxB = sliderRandMaxBlue.Value;
            token.RandMinR = sliderRandMinRed.Value;
            token.RandMinG = sliderRandMinGreen.Value;
            token.RandMinB = sliderRandMinBlue.Value;
            token.SizeChange = sliderShiftSize.Value;
            token.RotChange = sliderShiftRotation.Value;
            token.AlphaChange = sliderShiftAlpha.Value;
            token.Symmetry = (SymmetryMode)bttnSymmetry.SelectedIndex;
            token.CustomBrushLocations = loadedBrushPaths;
        }

        /// <summary>
        /// Repaints only the visible areas of the drawing region.
        /// </summary>
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            SolidBrush colorBrush = new SolidBrush(BackColor);

            e.Graphics.FillRectangle(colorBrush,
                new Rectangle(0, 0, ClientRectangle.Width, displayCanvasBG.Top));

            e.Graphics.FillRectangle(colorBrush,
                new Rectangle(0, displayCanvasBG.Bottom, ClientRectangle.Width, ClientRectangle.Bottom));

            e.Graphics.FillRectangle(colorBrush,
                new Rectangle(0, 0, displayCanvasBG.Left, ClientRectangle.Height));

            e.Graphics.FillRectangle(colorBrush,
                new Rectangle(displayCanvasBG.Right, 0, ClientRectangle.Width - displayCanvasBG.Right,
                this.ClientRectangle.Height));
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (tempDir != null)
                {
                    tempDir.Dispose();
                    tempDir = null;
                }
            }

            base.Dispose(disposing);
        }
        #endregion

        #region Methods (not event handlers)
        /// <summary>
        /// Sets the brushes to be used, clearing any that already exist and
        /// removing all custom brushes as a result.
        /// </summary>
        private void InitBrushes()
        {
            bmpBrush = new Bitmap(Resources.BrCircle);

            //Configures the default list of brushes for the brush selector.
            loadedBrushes = new BindingList<BrushSelectorItem>();

            // Retrieves values from the registry for the gui.
            Microsoft.Win32.RegistryKey key =
                Microsoft.Win32.Registry.CurrentUser
                .CreateSubKey("software", true)
                .CreateSubKey("paint.net_brushfactory", true);

            //Gets whether default brushes should be used.
            bool useDefaultBrushes = true;
            string value = (string)key.GetValue("useDefaultBrushes");
            if (value != null)
            {
                Boolean.TryParse(value, out useDefaultBrushes);
            }

            //Gets the desired locations to load custom brushes from.
            string[] customBrushDirectories = { };
            value = (string)key.GetValue("customBrushLocations");
            if (value != null)
            {
                customBrushDirectories = value.Split(
                    new[] { "\r\n", "\r", "\n" },
                    StringSplitOptions.RemoveEmptyEntries);
            }

            key.Close();

            //Loads stored brushes.
            loadedBrushes.Add(new BrushSelectorItem("Circle 1", Resources.BrCircle));

            if (useDefaultBrushes)
            {
                loadedBrushes.Add(new BrushSelectorItem("Circle 2", Resources.BrCircleMedium));
                loadedBrushes.Add(new BrushSelectorItem("Circle 3", Resources.BrCircleHard));
                loadedBrushes.Add(new BrushSelectorItem("Rough", Resources.BrCircleRough));
                loadedBrushes.Add(new BrushSelectorItem("Sketchy", Resources.BrCircleSketchy));
                loadedBrushes.Add(new BrushSelectorItem("Segments", Resources.BrCircleSegmented));
                loadedBrushes.Add(new BrushSelectorItem("Spiral", Resources.BrSpiral));
                loadedBrushes.Add(new BrushSelectorItem("Cracks", Resources.BrCracks));
                loadedBrushes.Add(new BrushSelectorItem("Dirt 1", Resources.BrDirt));
                loadedBrushes.Add(new BrushSelectorItem("Dirt 2", Resources.BrDirt2));
                loadedBrushes.Add(new BrushSelectorItem("Dirt 3", Resources.BrDirt3));
                loadedBrushes.Add(new BrushSelectorItem("Dirt 4", Resources.BrFractalDirt));
                loadedBrushes.Add(new BrushSelectorItem("Scales", Resources.BrScales));
                loadedBrushes.Add(new BrushSelectorItem("Smoke", Resources.BrSmoke));
                loadedBrushes.Add(new BrushSelectorItem("Grass", Resources.BrGrass));
                loadedBrushes.Add(new BrushSelectorItem("Rain", Resources.BrRain));
                loadedBrushes.Add(new BrushSelectorItem("Gravel", Resources.BrGravel));
                loadedBrushes.Add(new BrushSelectorItem("Spark", Resources.BrSpark));
                loadedBrushes.Add(new BrushSelectorItem("Big Dots", Resources.BrDotsBig));
                loadedBrushes.Add(new BrushSelectorItem("Tiny Dots", Resources.BrDotsTiny));
                loadedBrushes.Add(new BrushSelectorItem("Line", Resources.BrLine));
            }

            loadedBrushes.Add(BrushSelectorItem.CustomBrush);

            //Enables dynamic binding and sets the list.
            bttnBrushSelector.DataSource = loadedBrushes;
            bttnBrushSelector.DisplayMember = "Name";
            bttnBrushSelector.ValueMember = "Brush";

            //Loads any custom brushes.
            ImportBrushes(FilesInDirectory(customBrushDirectories), true, false);
        }

        /// <summary>
        /// Sets/resets all persistent settings in the dialog to their default
        /// values.
        /// </summary>
        private void InitSettings()
        {
            InitialInitToken();
            InitDialogFromToken();
        }

        /// <summary>
        /// Allows the canvas to zoom in and out dependent on the mouse wheel.
        /// </summary>
        private void Zoom(int mouseWheelDetents, bool updateSlider)
        {
            //Causes the slider's update method to trigger, which calls this
            //and doesn't repeat anything since updateSlider is then false.
            if (updateSlider)
            {
                //Zooms in/out some amount for each mouse wheel movement.
                int zoom;
                if (sliderCanvasZoom.Value < 5)
                {
                    zoom = 1;
                }
                else if (sliderCanvasZoom.Value < 10)
                {
                    zoom = 3;
                }
                else if (sliderCanvasZoom.Value < 20)
                {
                    zoom = 5;
                }
                else if (sliderCanvasZoom.Value < 50)
                {
                    zoom = 10;
                }
                else if (sliderCanvasZoom.Value < 100)
                {
                    zoom = 15;
                }
                else if (sliderCanvasZoom.Value < 200)
                {
                    zoom = 30;
                }
                else if (sliderCanvasZoom.Value < 500)
                {
                    zoom = 50;
                }
                else if (sliderCanvasZoom.Value < 1000)
                {
                    zoom = 100;
                }
                else if (sliderCanvasZoom.Value < 2000)
                {
                    zoom = 200;
                }
                else
                {
                    zoom = 300;
                }

                zoom *= Math.Sign(mouseWheelDetents);

                //Updates the corresponding slider as well (within its range).
                sliderCanvasZoom.Value = Utils.Clamp(
                sliderCanvasZoom.Value + zoom,
                sliderCanvasZoom.Minimum,
                sliderCanvasZoom.Maximum);

                return;
            }

            //Calculates the zooming percent.
            float newZoomFactor = sliderCanvasZoom.Value / 100f;

            //Updates the canvas zoom factor.
            displayCanvasZoom = newZoomFactor;
            txtCanvasZoom.Text = String.Format(
                "{0} {1:p0}",
                Localization.Strings.CanvasZoom,
                newZoomFactor);

            //Gets the new width and height, adjusted for zooming.
            int zoomWidth = (int)(bmpCurrentDrawing.Width * newZoomFactor);
            int zoomHeight = (int)(bmpCurrentDrawing.Height * newZoomFactor);

            //Sets the new canvas position (center) and size using zoom.
            displayCanvas.Bounds = new Rectangle(
                (displayCanvasBG.Width - zoomWidth) / 2,
                (displayCanvasBG.Height - zoomHeight) / 2,
                zoomWidth, zoomHeight);
        }

        /// <summary>
        /// Applies the brush to the drawing region at the given location
        /// with the given radius. The brush is assumed square.
        /// </summary>
        /// <param name="loc">The location to apply the brush.</param>
        /// <param name="radius">The size to draw the brush at.</param>
        private void DrawBrush(Point loc, int radius)
        {
            //Stores the differences in mouse coordinates for some settings.
            int deltaX;
            int deltaY;

            //Ensures the mouse is far enough away if min drawing dist != 0.
            if (sliderMinDrawDistance.Value != 0 &&
                mouseLocBrush.HasValue)
            {
                deltaX = mouseLocBrush.Value.X - mouseLoc.X;
                deltaY = mouseLocBrush.Value.Y - mouseLoc.Y;

                //Aborts if the minimum drawing distance isn't met.
                if (Math.Sqrt(deltaX * deltaX + deltaY * deltaY) <
                    sliderMinDrawDistance.Value * displayCanvasZoom)
                {
                    return;
                }
            }

            //Sets the new brush location because the brush stroke succeeded.
            mouseLocBrush = mouseLoc;

            //Shifts the size.
            if (sliderShiftSize.Value != 0)
            {
                int tempSize = sliderBrushSize.Value;
                if (isGrowingSize)
                {
                    tempSize += sliderShiftSize.Value;
                }
                else
                {
                    tempSize -= sliderShiftSize.Value;
                }
                if (tempSize > sliderBrushSize.Maximum)
                {
                    tempSize = sliderBrushSize.Maximum;
                    isGrowingSize = !isGrowingSize; //handles values < 0.
                }
                else if (tempSize < sliderBrushSize.Minimum)
                {
                    tempSize = sliderBrushSize.Minimum;
                    isGrowingSize = !isGrowingSize;
                }

                sliderBrushSize.Value = Utils.Clamp(tempSize,
                    sliderBrushSize.Minimum, sliderBrushSize.Maximum);
            }

            //Shifts the alpha.
            if (sliderShiftAlpha.Value != 0)
            {
                int tempAlpha = sliderBrushAlpha.Value;
                if (isGrowingAlpha)
                {
                    tempAlpha += sliderShiftAlpha.Value;
                }
                else
                {
                    tempAlpha -= sliderShiftAlpha.Value;
                }
                if (tempAlpha > sliderBrushAlpha.Maximum)
                {
                    tempAlpha = sliderBrushAlpha.Maximum;
                    isGrowingAlpha = !isGrowingAlpha; //handles values < 0.
                }
                else if (tempAlpha < sliderBrushAlpha.Minimum)
                {
                    tempAlpha = sliderBrushAlpha.Minimum;
                    isGrowingAlpha = !isGrowingAlpha;
                }

                sliderBrushAlpha.Value = Utils.Clamp(tempAlpha,
                    sliderBrushAlpha.Minimum, sliderBrushAlpha.Maximum);
            }

            if (sliderShiftRotation.Value != 0)
            {
                int tempRot = sliderBrushRotation.Value + sliderShiftRotation.Value;
                if (tempRot > sliderBrushRotation.Maximum)
                {
                    //The range goes negative, and is a total of 2 * max.
                    tempRot -= (2 * sliderBrushRotation.Maximum);
                }
                else if (tempRot < sliderBrushRotation.Minimum)
                {
                    tempRot += (2 * sliderBrushRotation.Maximum) - Math.Abs(tempRot);
                }

                sliderBrushRotation.Value = Utils.Clamp(tempRot,
                    sliderBrushRotation.Minimum, sliderBrushRotation.Maximum);
            }

            //Randomly shifts the image by some percent of the canvas size,
            //horizontally and/or vertically.
            if (sliderRandHorzShift.Value != 0 ||
                sliderRandVertShift.Value != 0)
            {
                loc.X = (int)(loc.X
                    - bmpCurrentDrawing.Width * (sliderRandHorzShift.Value / 200f)
                    + bmpCurrentDrawing.Width * (random.Next(sliderRandHorzShift.Value) / 100f));

                loc.Y = (int)(loc.Y
                    - bmpCurrentDrawing.Height * (sliderRandVertShift.Value / 200f)
                    + bmpCurrentDrawing.Height * (random.Next(sliderRandVertShift.Value) / 100f));
            }

            //This is used to randomly rotate the image by some amount.
            int rotation = sliderBrushRotation.Value
                - random.Next(sliderRandRotLeft.Value)
                + random.Next(sliderRandRotRight.Value);

            if (chkbxOrientToMouse.Checked)
            {
                //Adds to the rotation according to mouse direction. Uses the
                //original rotation as an offset.
                deltaX = mouseLoc.X - mouseLocPrev.X;
                deltaY = mouseLoc.Y - mouseLocPrev.Y;
                rotation += (int)(Math.Atan2(deltaY, deltaX) * 180 / Math.PI);
            }

            //Creates a brush from a rotation of the current brush, then
            //draws it (adjusting to the center).
            using (Graphics g = Graphics.FromImage(bmpCurrentDrawing))
            {
                Bitmap bmpBrushRot = Utils.RotateImage(bmpBrushEffects, rotation);

                //Rotating the brush increases image bounds, so brush space
                //must increase to avoid making it visually shrink.
                double radAngle = (Math.Abs(rotation) % 90) * Math.PI / 180;
                float rotScaleFactor = (float)(Math.Cos(radAngle) + Math.Sin(radAngle));
                int scaleFactor = (int)(radius * rotScaleFactor);

                //Sets the interpolation mode based on preferences.
                g.InterpolationMode = (InterpolationMode)bttnBrushSmoothing.SelectedValue;

                //Draws the brush normally if color/alpha aren't randomized.
                if ((sliderRandMaxAlpha.Value == 0 &&
                    sliderRandMinAlpha.Value == 0 &&
                    sliderRandMaxRed.Value == 0 &&
                    sliderRandMinRed.Value == 0 &&
                    sliderRandMaxGreen.Value == 0 &&
                    sliderRandMinGreen.Value == 0 &&
                    sliderRandMaxBlue.Value == 0 &&
                    sliderRandMinBlue.Value == 0) ||
                    !chkbxColorizeBrush.Checked)
                {
                    //Draws the brush for normal and non-radial symmetry.
                    if (bttnSymmetry.SelectedIndex < 4)
                    {
                        g.DrawImage(
                            bmpBrushRot,
                            loc.X - (scaleFactor / 2),
                            loc.Y - (scaleFactor / 2),
                            scaleFactor,
                            scaleFactor);
                    }

                    //Draws the brush horizontally reflected.
                    if (bttnSymmetry.SelectedIndex ==
                        (int)SymmetryMode.Horizontal)
                    {
                        g.DrawImage(
                            bmpBrushRot,
                            bmpCurrentDrawing.Width - (loc.X - (scaleFactor / 2)),
                            loc.Y - (scaleFactor / 2),
                            scaleFactor * -1,
                            scaleFactor);
                    }

                    //Draws the brush vertically reflected.
                    else if (bttnSymmetry.SelectedIndex ==
                        (int)SymmetryMode.Vertical)
                    {
                        g.DrawImage(
                            bmpBrushRot,
                            loc.X - (scaleFactor / 2),
                            bmpCurrentDrawing.Height - (loc.Y - scaleFactor / 2),
                            scaleFactor,
                            scaleFactor * -1);
                    }

                    //Draws the brush horizontally and vertically reflected.
                    else if (bttnSymmetry.SelectedIndex ==
                        (int)SymmetryMode.Star2)
                    {
                        g.DrawImage(
                            bmpBrushRot,
                            bmpCurrentDrawing.Width - (loc.X - (scaleFactor / 2)),
                            bmpCurrentDrawing.Height - (loc.Y - (scaleFactor / 2)),
                            scaleFactor * -1,
                            scaleFactor * -1);
                    }

                    //Draws the brush with radial reflections.
                    else if (bttnSymmetry.SelectedIndex > 3)
                    {
                        //Gets the center of the image.
                        Point center = new Point(
                            (bmpCurrentDrawing.Width / 2) - (radius / 2),
                            (bmpCurrentDrawing.Height / 2) - (radius / 2));

                        //Gets the drawn location relative to center.
                        Point locRelativeToCenter = new Point(
                            loc.X - center.X,
                            loc.Y - center.Y);

                        //Gets the distance from the drawing point to center.
                        var dist = Math.Sqrt(
                            Math.Pow(locRelativeToCenter.X, 2) +
                            Math.Pow(locRelativeToCenter.Y, 2));

                        //Gets the angle of the drawing point.
                        var angle = Math.Atan2(
                            locRelativeToCenter.Y,
                            locRelativeToCenter.X);

                        //Draws an N-pt radial reflection.
                        int numPoints = bttnSymmetry.SelectedIndex - 1;
                        double angleIncrease = (2 * Math.PI) / numPoints;
                        for (int i = 0; i < numPoints; i++)
                        {
                            g.DrawImage(
                            bmpBrushRot,
                            center.X + (float)(dist * Math.Cos(angle)),
                            center.Y + (float)(dist * Math.Sin(angle)),
                            scaleFactor,
                            scaleFactor);
                            
                            angle += angleIncrease;
                        }
                    }
                }
                else
                {
                    //Sets the color data and transparency randomly.
                    float newAlpha = Utils.ClampF((100
                        - random.Next(sliderRandMinAlpha.Value)
                        + random.Next(sliderRandMaxAlpha.Value)) / 100f, 0, 1);

                    float newBlue = Utils.ClampF((bttnBrushColor.BackColor.B / 2.55f
                        - random.Next(sliderRandMinBlue.Value)
                        + random.Next(sliderRandMaxBlue.Value)) / 100f, 0, 1);

                    float newGreen = Utils.ClampF((bttnBrushColor.BackColor.G / 2.55f
                        - random.Next(sliderRandMinGreen.Value)
                        + random.Next(sliderRandMaxGreen.Value)) / 100f, 0, 1);

                    float newRed = Utils.ClampF((bttnBrushColor.BackColor.R / 2.55f
                        - random.Next(sliderRandMinRed.Value)
                        + random.Next(sliderRandMaxRed.Value)) / 100f, 0, 1);

                    //Determines the positions to draw the brush at.
                    Point[] destination = new Point[3];
                    int xPos = loc.X - (scaleFactor / 2);
                    int yPos = loc.Y - (scaleFactor / 2);

                    //Draws without reflection.
                    destination[0] = new Point(xPos, yPos);
                    destination[1] = new Point(xPos + scaleFactor, yPos);
                    destination[2] = new Point(xPos, yPos + scaleFactor);

                    //Draws the whole image and applies colorations and alpha.
                    if (bttnSymmetry.SelectedIndex < 4)
                    {
                        g.DrawImage(
                            bmpBrushRot,
                            destination,
                            new Rectangle(0, 0, bmpBrushRot.Width, bmpBrushRot.Height),
                            GraphicsUnit.Pixel,
                            Utils.ColorImageAttr(
                                bmpBrushRot,
                                newRed, newGreen, newBlue, newAlpha));
                    }

                    //Handles drawing reflections.
                    if (bttnSymmetry.SelectedIndex !=
                        (int)SymmetryMode.None)
                    {
                        //Draws the brush horizontally reflected
                        if (bttnSymmetry.SelectedIndex ==
                            (int)SymmetryMode.Horizontal)
                        {
                            destination[0] = new Point(bmpCurrentDrawing.Width - xPos, yPos);
                            destination[1] = new Point(bmpCurrentDrawing.Width - xPos - scaleFactor, yPos);
                            destination[2] = new Point(bmpCurrentDrawing.Width - xPos, yPos + scaleFactor);
                        }

                        //Draws the brush vertically reflected.
                        else if (bttnSymmetry.SelectedIndex ==
                            (int)SymmetryMode.Vertical)
                        {
                            destination[0] = new Point(xPos, bmpCurrentDrawing.Height - yPos);
                            destination[1] = new Point(xPos + scaleFactor, bmpCurrentDrawing.Height - yPos);
                            destination[2] = new Point(xPos, bmpCurrentDrawing.Height - yPos - scaleFactor);
                        }

                        //Draws the brush horizontally and vertically reflected.
                        else if (bttnSymmetry.SelectedIndex ==
                            (int)SymmetryMode.Star2)
                        {
                            destination[0] = new Point(bmpCurrentDrawing.Width - xPos,
                                bmpCurrentDrawing.Height - yPos);
                            destination[1] = new Point(bmpCurrentDrawing.Width - xPos - scaleFactor,
                                bmpCurrentDrawing.Height - yPos);
                            destination[2] = new Point(bmpCurrentDrawing.Width - xPos,
                                bmpCurrentDrawing.Height - yPos - scaleFactor);
                        }

                        //Draws the non-radial reflection.
                        if (bttnSymmetry.SelectedIndex < 4)
                        {
                            //Draws the whole image and applies colorations and alpha.
                            g.DrawImage(
                                bmpBrushRot,
                                destination,
                                new Rectangle(0, 0, bmpBrushRot.Width, bmpBrushRot.Height),
                                GraphicsUnit.Pixel,
                                Utils.ColorImageAttr(
                                    bmpBrushRot,
                                    newRed, newGreen, newBlue, newAlpha));
                        }

                        //Draws the brush with radial reflections.
                        else if (bttnSymmetry.SelectedIndex > 3)
                        {
                            //Gets the center of the image.
                            Point center = new Point(
                                (bmpCurrentDrawing.Width / 2) - (radius / 2),
                                (bmpCurrentDrawing.Height / 2) - (radius / 2));

                            //Gets the drawn location relative to center.
                            Point locRelativeToCenter = new Point(
                                loc.X - center.X,
                                loc.Y - center.Y);

                            //Gets the distance from the drawing point to center.
                            var dist = Math.Sqrt(
                                Math.Pow(locRelativeToCenter.X, 2) +
                                Math.Pow(locRelativeToCenter.Y, 2));

                            //Gets the angle of the drawing point.
                            var angle = Math.Atan2(
                                locRelativeToCenter.Y,
                                locRelativeToCenter.X);

                            //Draws an N-pt radial reflection.
                            int numPoints = bttnSymmetry.SelectedIndex - 1;
                            double angleIncrease = (2 * Math.PI) / numPoints;
                            for (int i = 0; i < numPoints; i++)
                            {
                                int posX = (int)(center.X + dist * Math.Cos(angle));
                                int posY = (int)(center.Y + dist * Math.Sin(angle));

                                destination[0] = new Point(posX, posY);
                                destination[1] = new Point(posX + scaleFactor, posY);
                                destination[2] = new Point(posX, posY + scaleFactor);

                                g.DrawImage(
                                    bmpBrushRot,
                                    destination,
                                    new Rectangle(0, 0,
                                        bmpBrushRot.Width,
                                        bmpBrushRot.Height),
                                    GraphicsUnit.Pixel,
                                    Utils.ColorImageAttr(
                                        bmpBrushRot,
                                        newRed, newGreen, newBlue, newAlpha));

                                angle += angleIncrease;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Presents an open file dialog to the user, allowing them to select
        /// any number of brush files to load and add as the custom brushes.
        /// Returns false if the user cancels or an error occurred.
        /// </summary>
        /// <param name="doAddToSettings">
        /// If true, the brush will be added to the settings.
        /// </param>
        private bool ImportBrushes(bool doAddToSettings)
        {
            //Configures a dialog to get the brush(es) path(s).
            OpenFileDialog openFileDialog = new OpenFileDialog();

            string defPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            openFileDialog.InitialDirectory = defPath;
            openFileDialog.Multiselect = true;
            openFileDialog.Title = "Load custom brushes";
            openFileDialog.Filter = "Supported images|" +
                "*.png;*.bmp;*.jpg;*.gif;*.tif;*.exif*.jpeg;*.tiff;*.abr;";

            //Displays the dialog. Loads the files if it worked.
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                return ImportBrushes(openFileDialog.FileNames, doAddToSettings, true);
            }

            return false;
        }

        /// <summary>
        /// Attempts to load any number of brush files and add them as custom
        /// brushes. This does not interact with the user.
        /// </summary>
        /// <param name="fileAndPath">
        /// If empty, the user will be presented with a dialog to select
        /// files.
        /// </param>
        /// <param name="doAddToSettings">
        /// If true, the brush will be added to the settings.
        /// </param>
        /// <param name="displayError">
        /// Errors should only be displayed if it's a user-initiated action.
        /// </param>
        private bool ImportBrushes(
            string[] filePaths,
            bool doAddToSettings,
            bool doDisplayErrors)
        {
            //Attempts to load a bitmap from a file to use as a brush.
            foreach (string file in filePaths)
            {
                try
                {
                    if (file.EndsWith(".abr", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            using (AbrBrushCollection brushes = AbrReader.LoadBrushes(file))
                            {
                                for (int i = 0; i < brushes.Count; i++)
                                {
                                    AbrBrush item = brushes[i];

                                    // Creates the brush space.
                                    int size = Math.Max(item.Image.Width, item.Image.Height);
                                    bmpBrush = new Bitmap(size, size);

                                    //Pads the image to be square if needed, makes fully
                                    //opaque images use intensity for alpha, and draws the
                                    //altered loaded bitmap to the brush.
                                    Utils.CopyBitmapPure(Utils.MakeBitmapSquare(
                                        Utils.MakeTransparent(item.Image)), bmpBrush);

                                    string filename = item.Name;
                                    if (string.IsNullOrEmpty(filename))
                                    {
                                        filename = string.Format(
                                            System.Globalization.CultureInfo.CurrentCulture,
                                            Localization.Strings.AbrBrushNameFallbackFormat,
                                            i);
                                    }

                                    //Appends invisible spaces to files with the same name
                                    //until they're unique.
                                    while (loadedBrushes.Any(a => a.Name.Equals(filename, StringComparison.Ordinal)))
                                    {
                                        filename += " ";
                                    }

                                    //Adds the brush without the period at the end.
                                    loadedBrushes.Add(new BrushSelectorItem(filename, bmpBrush));
                                }
                            }
                        }
                        catch (FormatException)
                        {
                            // The ABR version is not supported.
                            continue;
                        }
                    }
                    else
                    {
                        using (Bitmap bmp = (Bitmap)Image.FromFile(file))
                        {
                            //Creates the brush space.
                            int size = Math.Max(bmp.Width, bmp.Height);
                            bmpBrush = new Bitmap(size, size);

                            //Pads the image to be square if needed, makes fully
                            //opaque images use intensity for alpha, and draws the
                            //altered loaded bitmap to the brush.
                            Utils.CopyBitmapPure(Utils.MakeBitmapSquare(
                                Utils.MakeTransparent(bmp)), bmpBrush);
                        }

                        //Gets the last word in the filename without the path.
                        Regex getOnlyFilename = new Regex(@"[\w-]+\.");
                        string filename = getOnlyFilename.Match(file).Value;

                        //Removes the file extension dot.
                        if (filename.EndsWith("."))
                        {
                            filename = filename.Remove(filename.Length - 1);
                        }

                        //Appends invisible spaces to files with the same name
                        //until they're unique.
                        while (loadedBrushes.Any(a =>
                            { return (a.Name.Equals(filename)); }))
                        {
                            filename += " ";
                        }

                        //Adds the brush without the period at the end.
                        loadedBrushes.Add(
                            new BrushSelectorItem(filename, bmpBrush));
                    }

                    if (doAddToSettings)
                    {
                        //Adds the brush location into settings.
                        loadedBrushPaths.Add(file);
                    }

                    //Removes the custom brush so it can be appended on the end.
                    loadedBrushes.Remove(BrushSelectorItem.CustomBrush);
                    loadedBrushes.Add(BrushSelectorItem.CustomBrush);

                    //Makes the newest brush active (and not the custom brush).
                    bttnBrushSelector.SelectedIndex =
                        bttnBrushSelector.Items.Count - 2;
                }
                catch (ArgumentException)
                {
                    continue;
                }
                catch (FileNotFoundException)
                {
                    continue;
                }
                catch (OutOfMemoryException)
                {
                    if (doDisplayErrors)
                    {
                        MessageBox.Show("Cannot load brush: out of memory.");
                    }

                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Returns a list of files in the given directories. Any invalid
        /// or non-directory path is ignored.
        /// </summary>
        private string[] FilesInDirectory(string[] dirs)
        {
            List<string> pathsToReturn = new List<string>();

            foreach (string directory in dirs)
            {
                try
                {
                    //Excludes all non-image files.
                    foreach (string str in Directory.EnumerateFiles(directory))
                    {
                        if (str.EndsWith("png", StringComparison.OrdinalIgnoreCase) || str.EndsWith("bmp", StringComparison.OrdinalIgnoreCase) ||
                            str.EndsWith("jpg", StringComparison.OrdinalIgnoreCase) || str.EndsWith("gif", StringComparison.OrdinalIgnoreCase) ||
                            str.EndsWith("tif", StringComparison.OrdinalIgnoreCase) || str.EndsWith("exif", StringComparison.OrdinalIgnoreCase) ||
                            str.EndsWith("jpeg", StringComparison.OrdinalIgnoreCase) || str.EndsWith("tiff", StringComparison.OrdinalIgnoreCase) ||
                            str.EndsWith(".abr", StringComparison.OrdinalIgnoreCase))
                        {
                            pathsToReturn.Add(str);
                        }
                    }
                }
                catch
                {
                }
            }

            return pathsToReturn.ToArray();
        }

        /// <summary>
        /// Returns the amount of space between the display canvas and
        /// the display canvas background.
        /// </summary>
        private Rectangle GetRange()
        {
            //Gets the full region.
            Rectangle range = displayCanvas.ClientRectangle;

            //Calculates width.
            if (displayCanvas.ClientRectangle.Width >= displayCanvasBG.ClientRectangle.Width)
            {
                range.X = displayCanvasBG.ClientRectangle.Width - displayCanvas.ClientRectangle.Width;
                range.Width = displayCanvas.ClientRectangle.Width - displayCanvasBG.ClientRectangle.Width;
            }
            else
            {
                range.X = (displayCanvasBG.ClientRectangle.Width - displayCanvas.ClientRectangle.Width) / 2;
                range.Width = 0;
            }

            //Calculates height.
            if (displayCanvas.ClientRectangle.Height >= displayCanvasBG.ClientRectangle.Height)
            {
                range.Y = displayCanvasBG.ClientRectangle.Height - displayCanvas.ClientRectangle.Height;
                range.Height = displayCanvas.ClientRectangle.Height - displayCanvasBG.ClientRectangle.Height;
            }
            else
            {
                range.Y = (displayCanvasBG.ClientRectangle.Height - displayCanvas.ClientRectangle.Height) / 2;
                range.Height = 0;
            }

            return range;
        }

        /// <summary>
        /// Initializes all components. Auto-generated.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(WinBrushFactory));
            this.timerRepositionUpdate = new System.Windows.Forms.Timer(this.components);
            this.txtTooltip = new System.Windows.Forms.Label();
            this.displayCanvasBG = new System.Windows.Forms.Panel();
            this.displayCanvas = new System.Windows.Forms.PictureBox();
            this.tabJitter = new System.Windows.Forms.TabPage();
            this.sliderRandVertShift = new System.Windows.Forms.TrackBar();
            this.txtRandVertShift = new System.Windows.Forms.Label();
            this.sliderRandHorzShift = new System.Windows.Forms.TrackBar();
            this.txtRandHorzShift = new System.Windows.Forms.Label();
            this.sliderRandMaxAlpha = new System.Windows.Forms.TrackBar();
            this.txtRandMaxAlpha = new System.Windows.Forms.Label();
            this.sliderRandMinAlpha = new System.Windows.Forms.TrackBar();
            this.txtRandMinAlpha = new System.Windows.Forms.Label();
            this.sliderRandMaxSize = new System.Windows.Forms.TrackBar();
            this.txtRandMaxSize = new System.Windows.Forms.Label();
            this.sliderRandMinSize = new System.Windows.Forms.TrackBar();
            this.txtRandMinSize = new System.Windows.Forms.Label();
            this.sliderRandRotRight = new System.Windows.Forms.TrackBar();
            this.txtRandRotRight = new System.Windows.Forms.Label();
            this.sliderRandRotLeft = new System.Windows.Forms.TrackBar();
            this.txtRandRotLeft = new System.Windows.Forms.Label();
            this.txtMinDrawDistance = new System.Windows.Forms.Label();
            this.sliderMinDrawDistance = new System.Windows.Forms.TrackBar();
            this.tabControls = new System.Windows.Forms.TabPage();
            this.bttnRedo = new System.Windows.Forms.Button();
            this.chkbxColorizeBrush = new System.Windows.Forms.CheckBox();
            this.sliderBrushAlpha = new System.Windows.Forms.TrackBar();
            this.txtBrushAlpha = new System.Windows.Forms.Label();
            this.txtBrushSize = new System.Windows.Forms.Label();
            this.sliderBrushSize = new System.Windows.Forms.TrackBar();
            this.sliderBrushRotation = new System.Windows.Forms.TrackBar();
            this.txtBrushRotation = new System.Windows.Forms.Label();
            this.bttnOk = new System.Windows.Forms.Button();
            this.bttnBrushColor = new System.Windows.Forms.Button();
            this.bttnUndo = new System.Windows.Forms.Button();
            this.bttnCancel = new System.Windows.Forms.Button();
            this.sliderCanvasZoom = new System.Windows.Forms.TrackBar();
            this.txtCanvasZoom = new System.Windows.Forms.Label();
            this.bttnBrushSelector = new System.Windows.Forms.ComboBox();
            this.grpbxBrushOptions = new System.Windows.Forms.GroupBox();
            this.bttnSymmetry = new System.Windows.Forms.ComboBox();
            this.chkbxLockAlpha = new System.Windows.Forms.CheckBox();
            this.chkbxOrientToMouse = new System.Windows.Forms.CheckBox();
            this.tabBar = new System.Windows.Forms.TabControl();
            this.tabColor = new System.Windows.Forms.TabPage();
            this.txtRandMaxBlue = new System.Windows.Forms.Label();
            this.sliderRandMaxBlue = new System.Windows.Forms.TrackBar();
            this.txtRandMaxGreen = new System.Windows.Forms.Label();
            this.sliderRandMaxGreen = new System.Windows.Forms.TrackBar();
            this.txtRandMaxRed = new System.Windows.Forms.Label();
            this.sliderRandMaxRed = new System.Windows.Forms.TrackBar();
            this.txtRandMinBlue = new System.Windows.Forms.Label();
            this.sliderRandMinBlue = new System.Windows.Forms.TrackBar();
            this.txtRandMinGreen = new System.Windows.Forms.Label();
            this.sliderRandMinGreen = new System.Windows.Forms.TrackBar();
            this.txtRandMinRed = new System.Windows.Forms.Label();
            this.sliderRandMinRed = new System.Windows.Forms.TrackBar();
            this.tabOther = new System.Windows.Forms.TabPage();
            this.bttnCustomBrushLocations = new System.Windows.Forms.Button();
            this.bttnBrushSmoothing = new System.Windows.Forms.ComboBox();
            this.bttnClearSettings = new System.Windows.Forms.Button();
            this.bttnClearBrushes = new System.Windows.Forms.Button();
            this.sliderShiftAlpha = new System.Windows.Forms.TrackBar();
            this.txtShiftAlpha = new System.Windows.Forms.Label();
            this.sliderShiftRotation = new System.Windows.Forms.TrackBar();
            this.txtShiftRotation = new System.Windows.Forms.Label();
            this.sliderShiftSize = new System.Windows.Forms.TrackBar();
            this.txtShiftSize = new System.Windows.Forms.Label();
            this.displayCanvasBG.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.displayCanvas)).BeginInit();
            this.tabJitter.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandVertShift)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandHorzShift)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandMaxAlpha)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandMinAlpha)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandMaxSize)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandMinSize)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandRotRight)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandRotLeft)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderMinDrawDistance)).BeginInit();
            this.tabControls.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.sliderBrushAlpha)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderBrushSize)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderBrushRotation)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderCanvasZoom)).BeginInit();
            this.grpbxBrushOptions.SuspendLayout();
            this.tabBar.SuspendLayout();
            this.tabColor.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandMaxBlue)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandMaxGreen)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandMaxRed)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandMinBlue)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandMinGreen)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandMinRed)).BeginInit();
            this.tabOther.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.sliderShiftAlpha)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderShiftRotation)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderShiftSize)).BeginInit();
            this.SuspendLayout();
            // 
            // timerRepositionUpdate
            // 
            this.timerRepositionUpdate.Interval = 5;
            this.timerRepositionUpdate.Tick += new System.EventHandler(this.RepositionUpdate_Tick);
            // 
            // txtTooltip
            // 
            resources.ApplyResources(this.txtTooltip, "txtTooltip");
            this.txtTooltip.BackColor = System.Drawing.SystemColors.Control;
            this.txtTooltip.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.txtTooltip.Name = "txtTooltip";
            // 
            // displayCanvasBG
            // 
            resources.ApplyResources(this.displayCanvasBG, "displayCanvasBG");
            this.displayCanvasBG.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(207)))), ((int)(((byte)(207)))), ((int)(((byte)(207)))));
            this.displayCanvasBG.Controls.Add(this.displayCanvas);
            this.displayCanvasBG.Name = "displayCanvasBG";
            this.displayCanvasBG.MouseEnter += new System.EventHandler(this.DisplayCanvasBG_MouseEnter);
            // 
            // displayCanvas
            // 
            this.displayCanvas.BackgroundImage = global::BrushFactory.Properties.Resources.CheckeredBg;
            resources.ApplyResources(this.displayCanvas, "displayCanvas");
            this.displayCanvas.Name = "displayCanvas";
            this.displayCanvas.TabStop = false;
            this.displayCanvas.Paint += new System.Windows.Forms.PaintEventHandler(this.DisplayCanvas_Paint);
            this.displayCanvas.MouseDown += new System.Windows.Forms.MouseEventHandler(this.DisplayCanvas_MouseDown);
            this.displayCanvas.MouseEnter += new System.EventHandler(this.DisplayCanvas_MouseEnter);
            this.displayCanvas.MouseMove += new System.Windows.Forms.MouseEventHandler(this.DisplayCanvas_MouseMove);
            this.displayCanvas.MouseUp += new System.Windows.Forms.MouseEventHandler(this.DisplayCanvas_MouseUp);
            // 
            // tabJitter
            // 
            this.tabJitter.BackColor = System.Drawing.Color.Transparent;
            this.tabJitter.Controls.Add(this.sliderRandVertShift);
            this.tabJitter.Controls.Add(this.txtRandVertShift);
            this.tabJitter.Controls.Add(this.sliderRandHorzShift);
            this.tabJitter.Controls.Add(this.txtRandHorzShift);
            this.tabJitter.Controls.Add(this.sliderRandMaxAlpha);
            this.tabJitter.Controls.Add(this.txtRandMaxAlpha);
            this.tabJitter.Controls.Add(this.sliderRandMinAlpha);
            this.tabJitter.Controls.Add(this.txtRandMinAlpha);
            this.tabJitter.Controls.Add(this.sliderRandMaxSize);
            this.tabJitter.Controls.Add(this.txtRandMaxSize);
            this.tabJitter.Controls.Add(this.sliderRandMinSize);
            this.tabJitter.Controls.Add(this.txtRandMinSize);
            this.tabJitter.Controls.Add(this.sliderRandRotRight);
            this.tabJitter.Controls.Add(this.txtRandRotRight);
            this.tabJitter.Controls.Add(this.sliderRandRotLeft);
            this.tabJitter.Controls.Add(this.txtRandRotLeft);
            resources.ApplyResources(this.tabJitter, "tabJitter");
            this.tabJitter.Name = "tabJitter";
            // 
            // sliderRandVertShift
            // 
            resources.ApplyResources(this.sliderRandVertShift, "sliderRandVertShift");
            this.sliderRandVertShift.LargeChange = 1;
            this.sliderRandVertShift.Maximum = 100;
            this.sliderRandVertShift.Name = "sliderRandVertShift";
            this.sliderRandVertShift.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderRandVertShift.ValueChanged += new System.EventHandler(this.SliderRandVertShift_ValueChanged);
            this.sliderRandVertShift.MouseEnter += new System.EventHandler(this.SliderRandVertShift_MouseEnter);
            // 
            // txtRandVertShift
            // 
            this.txtRandVertShift.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.txtRandVertShift, "txtRandVertShift");
            this.txtRandVertShift.Name = "txtRandVertShift";
            // 
            // sliderRandHorzShift
            // 
            resources.ApplyResources(this.sliderRandHorzShift, "sliderRandHorzShift");
            this.sliderRandHorzShift.LargeChange = 1;
            this.sliderRandHorzShift.Maximum = 100;
            this.sliderRandHorzShift.Name = "sliderRandHorzShift";
            this.sliderRandHorzShift.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderRandHorzShift.ValueChanged += new System.EventHandler(this.SliderRandHorzShift_ValueChanged);
            this.sliderRandHorzShift.MouseEnter += new System.EventHandler(this.SliderRandHorzShift_MouseEnter);
            // 
            // txtRandHorzShift
            // 
            this.txtRandHorzShift.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.txtRandHorzShift, "txtRandHorzShift");
            this.txtRandHorzShift.Name = "txtRandHorzShift";
            // 
            // sliderRandMaxAlpha
            // 
            resources.ApplyResources(this.sliderRandMaxAlpha, "sliderRandMaxAlpha");
            this.sliderRandMaxAlpha.LargeChange = 1;
            this.sliderRandMaxAlpha.Maximum = 100;
            this.sliderRandMaxAlpha.Name = "sliderRandMaxAlpha";
            this.sliderRandMaxAlpha.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderRandMaxAlpha.ValueChanged += new System.EventHandler(this.SliderRandMaxAlpha_ValueChanged);
            this.sliderRandMaxAlpha.MouseEnter += new System.EventHandler(this.SliderRandMaxAlpha_MouseEnter);
            // 
            // txtRandMaxAlpha
            // 
            resources.ApplyResources(this.txtRandMaxAlpha, "txtRandMaxAlpha");
            this.txtRandMaxAlpha.BackColor = System.Drawing.Color.Transparent;
            this.txtRandMaxAlpha.Name = "txtRandMaxAlpha";
            // 
            // sliderRandMinAlpha
            // 
            resources.ApplyResources(this.sliderRandMinAlpha, "sliderRandMinAlpha");
            this.sliderRandMinAlpha.LargeChange = 1;
            this.sliderRandMinAlpha.Maximum = 100;
            this.sliderRandMinAlpha.Name = "sliderRandMinAlpha";
            this.sliderRandMinAlpha.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderRandMinAlpha.ValueChanged += new System.EventHandler(this.SliderRandMinAlpha_ValueChanged);
            this.sliderRandMinAlpha.MouseEnter += new System.EventHandler(this.SliderRandMinAlpha_MouseEnter);
            // 
            // txtRandMinAlpha
            // 
            resources.ApplyResources(this.txtRandMinAlpha, "txtRandMinAlpha");
            this.txtRandMinAlpha.BackColor = System.Drawing.Color.Transparent;
            this.txtRandMinAlpha.Name = "txtRandMinAlpha";
            // 
            // sliderRandMaxSize
            // 
            resources.ApplyResources(this.sliderRandMaxSize, "sliderRandMaxSize");
            this.sliderRandMaxSize.LargeChange = 1;
            this.sliderRandMaxSize.Maximum = 500;
            this.sliderRandMaxSize.Name = "sliderRandMaxSize";
            this.sliderRandMaxSize.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderRandMaxSize.ValueChanged += new System.EventHandler(this.SliderRandMaxSize_ValueChanged);
            this.sliderRandMaxSize.MouseEnter += new System.EventHandler(this.SliderRandMaxSize_MouseEnter);
            // 
            // txtRandMaxSize
            // 
            this.txtRandMaxSize.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.txtRandMaxSize, "txtRandMaxSize");
            this.txtRandMaxSize.Name = "txtRandMaxSize";
            // 
            // sliderRandMinSize
            // 
            resources.ApplyResources(this.sliderRandMinSize, "sliderRandMinSize");
            this.sliderRandMinSize.LargeChange = 1;
            this.sliderRandMinSize.Maximum = 500;
            this.sliderRandMinSize.Name = "sliderRandMinSize";
            this.sliderRandMinSize.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderRandMinSize.ValueChanged += new System.EventHandler(this.SliderRandMinSize_ValueChanged);
            this.sliderRandMinSize.MouseEnter += new System.EventHandler(this.SliderRandMinSize_MouseEnter);
            // 
            // txtRandMinSize
            // 
            this.txtRandMinSize.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.txtRandMinSize, "txtRandMinSize");
            this.txtRandMinSize.Name = "txtRandMinSize";
            // 
            // sliderRandRotRight
            // 
            resources.ApplyResources(this.sliderRandRotRight, "sliderRandRotRight");
            this.sliderRandRotRight.LargeChange = 1;
            this.sliderRandRotRight.Maximum = 180;
            this.sliderRandRotRight.Name = "sliderRandRotRight";
            this.sliderRandRotRight.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderRandRotRight.ValueChanged += new System.EventHandler(this.SliderRandRotRight_ValueChanged);
            this.sliderRandRotRight.MouseEnter += new System.EventHandler(this.SliderRandRotRight_MouseEnter);
            // 
            // txtRandRotRight
            // 
            resources.ApplyResources(this.txtRandRotRight, "txtRandRotRight");
            this.txtRandRotRight.BackColor = System.Drawing.Color.Transparent;
            this.txtRandRotRight.Name = "txtRandRotRight";
            // 
            // sliderRandRotLeft
            // 
            resources.ApplyResources(this.sliderRandRotLeft, "sliderRandRotLeft");
            this.sliderRandRotLeft.LargeChange = 1;
            this.sliderRandRotLeft.Maximum = 180;
            this.sliderRandRotLeft.Name = "sliderRandRotLeft";
            this.sliderRandRotLeft.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderRandRotLeft.ValueChanged += new System.EventHandler(this.SliderRandRotLeft_ValueChanged);
            this.sliderRandRotLeft.MouseEnter += new System.EventHandler(this.SliderRandRotLeft_MouseEnter);
            // 
            // txtRandRotLeft
            // 
            resources.ApplyResources(this.txtRandRotLeft, "txtRandRotLeft");
            this.txtRandRotLeft.BackColor = System.Drawing.Color.Transparent;
            this.txtRandRotLeft.Name = "txtRandRotLeft";
            // 
            // txtMinDrawDistance
            // 
            resources.ApplyResources(this.txtMinDrawDistance, "txtMinDrawDistance");
            this.txtMinDrawDistance.BackColor = System.Drawing.Color.Transparent;
            this.txtMinDrawDistance.Name = "txtMinDrawDistance";
            // 
            // sliderMinDrawDistance
            // 
            resources.ApplyResources(this.sliderMinDrawDistance, "sliderMinDrawDistance");
            this.sliderMinDrawDistance.LargeChange = 1;
            this.sliderMinDrawDistance.Maximum = 100;
            this.sliderMinDrawDistance.Name = "sliderMinDrawDistance";
            this.sliderMinDrawDistance.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderMinDrawDistance.ValueChanged += new System.EventHandler(this.SliderMinDrawDistance_ValueChanged);
            this.sliderMinDrawDistance.MouseEnter += new System.EventHandler(this.SliderMinDrawDistance_MouseEnter);
            // 
            // tabControls
            // 
            this.tabControls.BackColor = System.Drawing.Color.Transparent;
            this.tabControls.Controls.Add(this.bttnRedo);
            this.tabControls.Controls.Add(this.chkbxColorizeBrush);
            this.tabControls.Controls.Add(this.sliderBrushAlpha);
            this.tabControls.Controls.Add(this.txtBrushAlpha);
            this.tabControls.Controls.Add(this.txtBrushSize);
            this.tabControls.Controls.Add(this.sliderBrushSize);
            this.tabControls.Controls.Add(this.sliderBrushRotation);
            this.tabControls.Controls.Add(this.txtBrushRotation);
            this.tabControls.Controls.Add(this.bttnOk);
            this.tabControls.Controls.Add(this.bttnBrushColor);
            this.tabControls.Controls.Add(this.bttnUndo);
            this.tabControls.Controls.Add(this.bttnCancel);
            this.tabControls.Controls.Add(this.sliderCanvasZoom);
            this.tabControls.Controls.Add(this.txtCanvasZoom);
            this.tabControls.Controls.Add(this.bttnBrushSelector);
            resources.ApplyResources(this.tabControls, "tabControls");
            this.tabControls.Name = "tabControls";
            // 
            // bttnRedo
            // 
            resources.ApplyResources(this.bttnRedo, "bttnRedo");
            this.bttnRedo.Name = "bttnRedo";
            this.bttnRedo.UseVisualStyleBackColor = true;
            this.bttnRedo.Click += new System.EventHandler(this.BttnRedo_Click);
            this.bttnRedo.MouseEnter += new System.EventHandler(this.BttnRedo_MouseEnter);
            // 
            // chkbxColorizeBrush
            // 
            resources.ApplyResources(this.chkbxColorizeBrush, "chkbxColorizeBrush");
            this.chkbxColorizeBrush.Checked = true;
            this.chkbxColorizeBrush.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkbxColorizeBrush.Name = "chkbxColorizeBrush";
            this.chkbxColorizeBrush.UseVisualStyleBackColor = true;
            this.chkbxColorizeBrush.CheckedChanged += new System.EventHandler(this.ChkbxColorizeBrush_CheckedChanged);
            this.chkbxColorizeBrush.MouseEnter += new System.EventHandler(this.ChkbxColorizeBrush_MouseEnter);
            // 
            // sliderBrushAlpha
            // 
            resources.ApplyResources(this.sliderBrushAlpha, "sliderBrushAlpha");
            this.sliderBrushAlpha.LargeChange = 1;
            this.sliderBrushAlpha.Maximum = 100;
            this.sliderBrushAlpha.Name = "sliderBrushAlpha";
            this.sliderBrushAlpha.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderBrushAlpha.ValueChanged += new System.EventHandler(this.SliderBrushAlpha_ValueChanged);
            this.sliderBrushAlpha.MouseEnter += new System.EventHandler(this.SliderBrushAlpha_MouseEnter);
            // 
            // txtBrushAlpha
            // 
            resources.ApplyResources(this.txtBrushAlpha, "txtBrushAlpha");
            this.txtBrushAlpha.BackColor = System.Drawing.Color.Transparent;
            this.txtBrushAlpha.Name = "txtBrushAlpha";
            // 
            // txtBrushSize
            // 
            resources.ApplyResources(this.txtBrushSize, "txtBrushSize");
            this.txtBrushSize.BackColor = System.Drawing.Color.Transparent;
            this.txtBrushSize.Name = "txtBrushSize";
            // 
            // sliderBrushSize
            // 
            resources.ApplyResources(this.sliderBrushSize, "sliderBrushSize");
            this.sliderBrushSize.LargeChange = 1;
            this.sliderBrushSize.Maximum = 500;
            this.sliderBrushSize.Minimum = 2;
            this.sliderBrushSize.Name = "sliderBrushSize";
            this.sliderBrushSize.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderBrushSize.Value = 10;
            this.sliderBrushSize.ValueChanged += new System.EventHandler(this.SliderBrushSize_ValueChanged);
            this.sliderBrushSize.MouseEnter += new System.EventHandler(this.SliderBrushSize_MouseEnter);
            // 
            // sliderBrushRotation
            // 
            resources.ApplyResources(this.sliderBrushRotation, "sliderBrushRotation");
            this.sliderBrushRotation.LargeChange = 1;
            this.sliderBrushRotation.Maximum = 180;
            this.sliderBrushRotation.Minimum = -180;
            this.sliderBrushRotation.Name = "sliderBrushRotation";
            this.sliderBrushRotation.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderBrushRotation.ValueChanged += new System.EventHandler(this.SliderBrushRotation_ValueChanged);
            this.sliderBrushRotation.MouseEnter += new System.EventHandler(this.SliderBrushRotation_MouseEnter);
            // 
            // txtBrushRotation
            // 
            resources.ApplyResources(this.txtBrushRotation, "txtBrushRotation");
            this.txtBrushRotation.BackColor = System.Drawing.Color.Transparent;
            this.txtBrushRotation.Name = "txtBrushRotation";
            // 
            // bttnOk
            // 
            resources.ApplyResources(this.bttnOk, "bttnOk");
            this.bttnOk.Name = "bttnOk";
            this.bttnOk.UseVisualStyleBackColor = true;
            this.bttnOk.Click += new System.EventHandler(this.BttnOk_Click);
            this.bttnOk.MouseEnter += new System.EventHandler(this.BttnOk_MouseEnter);
            // 
            // bttnBrushColor
            // 
            resources.ApplyResources(this.bttnBrushColor, "bttnBrushColor");
            this.bttnBrushColor.BackColor = System.Drawing.Color.Black;
            this.bttnBrushColor.ForeColor = System.Drawing.Color.White;
            this.bttnBrushColor.Name = "bttnBrushColor";
            this.bttnBrushColor.UseVisualStyleBackColor = false;
            this.bttnBrushColor.Click += new System.EventHandler(this.BttnBrushColor_Click);
            this.bttnBrushColor.MouseEnter += new System.EventHandler(this.BttnBrushColor_MouseEnter);
            // 
            // bttnUndo
            // 
            resources.ApplyResources(this.bttnUndo, "bttnUndo");
            this.bttnUndo.Name = "bttnUndo";
            this.bttnUndo.UseVisualStyleBackColor = true;
            this.bttnUndo.Click += new System.EventHandler(this.BttnUndo_Click);
            this.bttnUndo.MouseEnter += new System.EventHandler(this.BttnUndo_MouseEnter);
            // 
            // bttnCancel
            // 
            resources.ApplyResources(this.bttnCancel, "bttnCancel");
            this.bttnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.bttnCancel.Name = "bttnCancel";
            this.bttnCancel.UseVisualStyleBackColor = true;
            this.bttnCancel.Click += new System.EventHandler(this.BttnCancel_Click);
            this.bttnCancel.MouseEnter += new System.EventHandler(this.BttnCancel_MouseEnter);
            // 
            // sliderCanvasZoom
            // 
            resources.ApplyResources(this.sliderCanvasZoom, "sliderCanvasZoom");
            this.sliderCanvasZoom.LargeChange = 1;
            this.sliderCanvasZoom.Maximum = 1600;
            this.sliderCanvasZoom.Minimum = 1;
            this.sliderCanvasZoom.Name = "sliderCanvasZoom";
            this.sliderCanvasZoom.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderCanvasZoom.Value = 100;
            this.sliderCanvasZoom.ValueChanged += new System.EventHandler(this.SliderCanvasZoom_ValueChanged);
            this.sliderCanvasZoom.MouseEnter += new System.EventHandler(this.SliderCanvasZoom_MouseEnter);
            // 
            // txtCanvasZoom
            // 
            resources.ApplyResources(this.txtCanvasZoom, "txtCanvasZoom");
            this.txtCanvasZoom.BackColor = System.Drawing.Color.Transparent;
            this.txtCanvasZoom.Name = "txtCanvasZoom";
            // 
            // bttnBrushSelector
            // 
            resources.ApplyResources(this.bttnBrushSelector, "bttnBrushSelector");
            this.bttnBrushSelector.BackColor = System.Drawing.Color.White;
            this.bttnBrushSelector.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
            this.bttnBrushSelector.DropDownHeight = 140;
            this.bttnBrushSelector.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.bttnBrushSelector.DropDownWidth = 20;
            this.bttnBrushSelector.FormattingEnabled = true;
            this.bttnBrushSelector.Name = "bttnBrushSelector";
            this.bttnBrushSelector.DrawItem += new System.Windows.Forms.DrawItemEventHandler(this.BttnBrushSelector_DrawItem);
            this.bttnBrushSelector.SelectedIndexChanged += new System.EventHandler(this.BttnBrushSelector_SelectedIndexChanged);
            this.bttnBrushSelector.MouseEnter += new System.EventHandler(this.BttnBrushSelector_MouseEnter);
            // 
            // grpbxBrushOptions
            // 
            this.grpbxBrushOptions.Controls.Add(this.bttnSymmetry);
            this.grpbxBrushOptions.Controls.Add(this.chkbxLockAlpha);
            this.grpbxBrushOptions.Controls.Add(this.chkbxOrientToMouse);
            resources.ApplyResources(this.grpbxBrushOptions, "grpbxBrushOptions");
            this.grpbxBrushOptions.Name = "grpbxBrushOptions";
            this.grpbxBrushOptions.TabStop = false;
            // 
            // bttnSymmetry
            // 
            resources.ApplyResources(this.bttnSymmetry, "bttnSymmetry");
            this.bttnSymmetry.BackColor = System.Drawing.Color.White;
            this.bttnSymmetry.DropDownHeight = 140;
            this.bttnSymmetry.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.bttnSymmetry.DropDownWidth = 20;
            this.bttnSymmetry.FormattingEnabled = true;
            this.bttnSymmetry.Name = "bttnSymmetry";
            this.bttnSymmetry.MouseEnter += new System.EventHandler(this.BttnSymmetry_MouseEnter);
            // 
            // chkbxLockAlpha
            // 
            resources.ApplyResources(this.chkbxLockAlpha, "chkbxLockAlpha");
            this.chkbxLockAlpha.Name = "chkbxLockAlpha";
            this.chkbxLockAlpha.UseVisualStyleBackColor = true;
            this.chkbxLockAlpha.MouseEnter += new System.EventHandler(this.ChkbxLockAlpha_MouseEnter);
            // 
            // chkbxOrientToMouse
            // 
            resources.ApplyResources(this.chkbxOrientToMouse, "chkbxOrientToMouse");
            this.chkbxOrientToMouse.Name = "chkbxOrientToMouse";
            this.chkbxOrientToMouse.UseVisualStyleBackColor = true;
            this.chkbxOrientToMouse.MouseEnter += new System.EventHandler(this.ChkbxOrientToMouse_MouseEnter);
            // 
            // tabBar
            // 
            this.tabBar.Controls.Add(this.tabControls);
            this.tabBar.Controls.Add(this.tabJitter);
            this.tabBar.Controls.Add(this.tabColor);
            this.tabBar.Controls.Add(this.tabOther);
            resources.ApplyResources(this.tabBar, "tabBar");
            this.tabBar.Multiline = true;
            this.tabBar.Name = "tabBar";
            this.tabBar.SelectedIndex = 0;
            // 
            // tabColor
            // 
            resources.ApplyResources(this.tabColor, "tabColor");
            this.tabColor.BackColor = System.Drawing.Color.Transparent;
            this.tabColor.Controls.Add(this.txtRandMaxBlue);
            this.tabColor.Controls.Add(this.sliderRandMaxBlue);
            this.tabColor.Controls.Add(this.txtRandMaxGreen);
            this.tabColor.Controls.Add(this.sliderRandMaxGreen);
            this.tabColor.Controls.Add(this.txtRandMaxRed);
            this.tabColor.Controls.Add(this.sliderRandMaxRed);
            this.tabColor.Controls.Add(this.txtRandMinBlue);
            this.tabColor.Controls.Add(this.sliderRandMinBlue);
            this.tabColor.Controls.Add(this.txtRandMinGreen);
            this.tabColor.Controls.Add(this.sliderRandMinGreen);
            this.tabColor.Controls.Add(this.txtRandMinRed);
            this.tabColor.Controls.Add(this.sliderRandMinRed);
            this.tabColor.Name = "tabColor";
            // 
            // txtRandMaxBlue
            // 
            resources.ApplyResources(this.txtRandMaxBlue, "txtRandMaxBlue");
            this.txtRandMaxBlue.BackColor = System.Drawing.Color.Transparent;
            this.txtRandMaxBlue.Name = "txtRandMaxBlue";
            // 
            // sliderRandMaxBlue
            // 
            resources.ApplyResources(this.sliderRandMaxBlue, "sliderRandMaxBlue");
            this.sliderRandMaxBlue.LargeChange = 1;
            this.sliderRandMaxBlue.Maximum = 100;
            this.sliderRandMaxBlue.Name = "sliderRandMaxBlue";
            this.sliderRandMaxBlue.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderRandMaxBlue.ValueChanged += new System.EventHandler(this.SliderRandMaxBlue_ValueChanged);
            this.sliderRandMaxBlue.MouseEnter += new System.EventHandler(this.SliderRandMaxBlue_MouseEnter);
            // 
            // txtRandMaxGreen
            // 
            resources.ApplyResources(this.txtRandMaxGreen, "txtRandMaxGreen");
            this.txtRandMaxGreen.BackColor = System.Drawing.Color.Transparent;
            this.txtRandMaxGreen.Name = "txtRandMaxGreen";
            // 
            // sliderRandMaxGreen
            // 
            resources.ApplyResources(this.sliderRandMaxGreen, "sliderRandMaxGreen");
            this.sliderRandMaxGreen.LargeChange = 1;
            this.sliderRandMaxGreen.Maximum = 100;
            this.sliderRandMaxGreen.Name = "sliderRandMaxGreen";
            this.sliderRandMaxGreen.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderRandMaxGreen.ValueChanged += new System.EventHandler(this.SliderRandMaxGreen_ValueChanged);
            this.sliderRandMaxGreen.MouseEnter += new System.EventHandler(this.SliderRandMaxGreen_MouseEnter);
            // 
            // txtRandMaxRed
            // 
            resources.ApplyResources(this.txtRandMaxRed, "txtRandMaxRed");
            this.txtRandMaxRed.BackColor = System.Drawing.Color.Transparent;
            this.txtRandMaxRed.Name = "txtRandMaxRed";
            // 
            // sliderRandMaxRed
            // 
            resources.ApplyResources(this.sliderRandMaxRed, "sliderRandMaxRed");
            this.sliderRandMaxRed.LargeChange = 1;
            this.sliderRandMaxRed.Maximum = 100;
            this.sliderRandMaxRed.Name = "sliderRandMaxRed";
            this.sliderRandMaxRed.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderRandMaxRed.ValueChanged += new System.EventHandler(this.SliderRandMaxRed_ValueChanged);
            this.sliderRandMaxRed.MouseEnter += new System.EventHandler(this.SliderRandMaxRed_MouseEnter);
            // 
            // txtRandMinBlue
            // 
            resources.ApplyResources(this.txtRandMinBlue, "txtRandMinBlue");
            this.txtRandMinBlue.BackColor = System.Drawing.Color.Transparent;
            this.txtRandMinBlue.Name = "txtRandMinBlue";
            // 
            // sliderRandMinBlue
            // 
            resources.ApplyResources(this.sliderRandMinBlue, "sliderRandMinBlue");
            this.sliderRandMinBlue.LargeChange = 1;
            this.sliderRandMinBlue.Maximum = 100;
            this.sliderRandMinBlue.Name = "sliderRandMinBlue";
            this.sliderRandMinBlue.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderRandMinBlue.ValueChanged += new System.EventHandler(this.SliderRandMinBlue_ValueChanged);
            this.sliderRandMinBlue.MouseEnter += new System.EventHandler(this.SliderRandMinBlue_MouseEnter);
            // 
            // txtRandMinGreen
            // 
            resources.ApplyResources(this.txtRandMinGreen, "txtRandMinGreen");
            this.txtRandMinGreen.BackColor = System.Drawing.Color.Transparent;
            this.txtRandMinGreen.Name = "txtRandMinGreen";
            // 
            // sliderRandMinGreen
            // 
            resources.ApplyResources(this.sliderRandMinGreen, "sliderRandMinGreen");
            this.sliderRandMinGreen.LargeChange = 1;
            this.sliderRandMinGreen.Maximum = 100;
            this.sliderRandMinGreen.Name = "sliderRandMinGreen";
            this.sliderRandMinGreen.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderRandMinGreen.ValueChanged += new System.EventHandler(this.SliderRandMinGreen_ValueChanged);
            this.sliderRandMinGreen.MouseEnter += new System.EventHandler(this.SliderRandMinGreen_MouseEnter);
            // 
            // txtRandMinRed
            // 
            resources.ApplyResources(this.txtRandMinRed, "txtRandMinRed");
            this.txtRandMinRed.BackColor = System.Drawing.Color.Transparent;
            this.txtRandMinRed.Name = "txtRandMinRed";
            // 
            // sliderRandMinRed
            // 
            resources.ApplyResources(this.sliderRandMinRed, "sliderRandMinRed");
            this.sliderRandMinRed.LargeChange = 1;
            this.sliderRandMinRed.Maximum = 100;
            this.sliderRandMinRed.Name = "sliderRandMinRed";
            this.sliderRandMinRed.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderRandMinRed.ValueChanged += new System.EventHandler(this.SliderRandMinRed_ValueChanged);
            this.sliderRandMinRed.MouseEnter += new System.EventHandler(this.SliderRandMinRed_MouseEnter);
            // 
            // tabOther
            // 
            this.tabOther.BackColor = System.Drawing.Color.Transparent;
            this.tabOther.Controls.Add(this.bttnCustomBrushLocations);
            this.tabOther.Controls.Add(this.bttnBrushSmoothing);
            this.tabOther.Controls.Add(this.bttnClearSettings);
            this.tabOther.Controls.Add(this.bttnClearBrushes);
            this.tabOther.Controls.Add(this.sliderShiftAlpha);
            this.tabOther.Controls.Add(this.txtShiftAlpha);
            this.tabOther.Controls.Add(this.sliderShiftRotation);
            this.tabOther.Controls.Add(this.txtShiftRotation);
            this.tabOther.Controls.Add(this.sliderShiftSize);
            this.tabOther.Controls.Add(this.txtShiftSize);
            this.tabOther.Controls.Add(this.txtMinDrawDistance);
            this.tabOther.Controls.Add(this.sliderMinDrawDistance);
            this.tabOther.Controls.Add(this.grpbxBrushOptions);
            resources.ApplyResources(this.tabOther, "tabOther");
            this.tabOther.Name = "tabOther";
            // 
            // bttnCustomBrushLocations
            // 
            resources.ApplyResources(this.bttnCustomBrushLocations, "bttnCustomBrushLocations");
            this.bttnCustomBrushLocations.Name = "bttnCustomBrushLocations";
            this.bttnCustomBrushLocations.UseVisualStyleBackColor = true;
            this.bttnCustomBrushLocations.Click += new System.EventHandler(this.BttnPreferences_Click);
            this.bttnCustomBrushLocations.MouseEnter += new System.EventHandler(this.BttnPreferences_MouseEnter);
            // 
            // bttnBrushSmoothing
            // 
            resources.ApplyResources(this.bttnBrushSmoothing, "bttnBrushSmoothing");
            this.bttnBrushSmoothing.BackColor = System.Drawing.Color.White;
            this.bttnBrushSmoothing.DropDownHeight = 140;
            this.bttnBrushSmoothing.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.bttnBrushSmoothing.DropDownWidth = 20;
            this.bttnBrushSmoothing.FormattingEnabled = true;
            this.bttnBrushSmoothing.Name = "bttnBrushSmoothing";
            this.bttnBrushSmoothing.MouseEnter += new System.EventHandler(this.BttnBrushSmoothing_MouseEnter);
            // 
            // bttnClearSettings
            // 
            resources.ApplyResources(this.bttnClearSettings, "bttnClearSettings");
            this.bttnClearSettings.Name = "bttnClearSettings";
            this.bttnClearSettings.UseVisualStyleBackColor = true;
            this.bttnClearSettings.Click += new System.EventHandler(this.BttnClearSettings_Click);
            this.bttnClearSettings.MouseEnter += new System.EventHandler(this.BttnClearSettings_MouseEnter);
            // 
            // bttnClearBrushes
            // 
            resources.ApplyResources(this.bttnClearBrushes, "bttnClearBrushes");
            this.bttnClearBrushes.Name = "bttnClearBrushes";
            this.bttnClearBrushes.UseVisualStyleBackColor = true;
            this.bttnClearBrushes.Click += new System.EventHandler(this.BttnClearBrushes_Click);
            this.bttnClearBrushes.MouseEnter += new System.EventHandler(this.BttnClearBrushes_MouseEnter);
            // 
            // sliderShiftAlpha
            // 
            resources.ApplyResources(this.sliderShiftAlpha, "sliderShiftAlpha");
            this.sliderShiftAlpha.LargeChange = 1;
            this.sliderShiftAlpha.Maximum = 100;
            this.sliderShiftAlpha.Minimum = -100;
            this.sliderShiftAlpha.Name = "sliderShiftAlpha";
            this.sliderShiftAlpha.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderShiftAlpha.ValueChanged += new System.EventHandler(this.SliderShiftAlpha_ValueChanged);
            this.sliderShiftAlpha.MouseEnter += new System.EventHandler(this.SliderShiftAlpha_MouseEnter);
            // 
            // txtShiftAlpha
            // 
            this.txtShiftAlpha.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.txtShiftAlpha, "txtShiftAlpha");
            this.txtShiftAlpha.Name = "txtShiftAlpha";
            // 
            // sliderShiftRotation
            // 
            resources.ApplyResources(this.sliderShiftRotation, "sliderShiftRotation");
            this.sliderShiftRotation.LargeChange = 1;
            this.sliderShiftRotation.Maximum = 180;
            this.sliderShiftRotation.Minimum = -180;
            this.sliderShiftRotation.Name = "sliderShiftRotation";
            this.sliderShiftRotation.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderShiftRotation.ValueChanged += new System.EventHandler(this.SliderShiftRotation_ValueChanged);
            this.sliderShiftRotation.MouseEnter += new System.EventHandler(this.SliderShiftRotation_MouseEnter);
            // 
            // txtShiftRotation
            // 
            this.txtShiftRotation.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.txtShiftRotation, "txtShiftRotation");
            this.txtShiftRotation.Name = "txtShiftRotation";
            // 
            // sliderShiftSize
            // 
            resources.ApplyResources(this.sliderShiftSize, "sliderShiftSize");
            this.sliderShiftSize.LargeChange = 1;
            this.sliderShiftSize.Maximum = 500;
            this.sliderShiftSize.Minimum = -500;
            this.sliderShiftSize.Name = "sliderShiftSize";
            this.sliderShiftSize.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderShiftSize.ValueChanged += new System.EventHandler(this.SliderShiftSize_ValueChanged);
            this.sliderShiftSize.MouseEnter += new System.EventHandler(this.SliderShiftSize_MouseEnter);
            // 
            // txtShiftSize
            // 
            this.txtShiftSize.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.txtShiftSize, "txtShiftSize");
            this.txtShiftSize.Name = "txtShiftSize";
            // 
            // WinBrushFactory
            // 
            this.AcceptButton = this.bttnOk;
            resources.ApplyResources(this, "$this");
            this.BackColor = System.Drawing.SystemColors.ControlLight;
            this.CancelButton = this.bttnCancel;
            this.Controls.Add(this.tabBar);
            this.Controls.Add(this.displayCanvasBG);
            this.Controls.Add(this.txtTooltip);
            this.DoubleBuffered = true;
            this.KeyPreview = true;
            this.MaximizeBox = true;
            this.Name = "WinBrushFactory";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.WinBrushFactory_FormClosing);
            this.Load += new System.EventHandler(this.WinBrushFactory_DialogLoad);
            this.Shown += new System.EventHandler(this.WinBrushFactory_DialogShown);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.WinBrushFactory_KeyDown);
            this.MouseWheel += new System.Windows.Forms.MouseEventHandler(this.DisplayCanvas_MouseWheel);
            this.Resize += new System.EventHandler(this.WinBrushFactory_Resize);
            this.displayCanvasBG.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.displayCanvas)).EndInit();
            this.tabJitter.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandVertShift)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandHorzShift)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandMaxAlpha)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandMinAlpha)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandMaxSize)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandMinSize)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandRotRight)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandRotLeft)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderMinDrawDistance)).EndInit();
            this.tabControls.ResumeLayout(false);
            this.tabControls.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.sliderBrushAlpha)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderBrushSize)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderBrushRotation)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderCanvasZoom)).EndInit();
            this.grpbxBrushOptions.ResumeLayout(false);
            this.grpbxBrushOptions.PerformLayout();
            this.tabBar.ResumeLayout(false);
            this.tabColor.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandMaxBlue)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandMaxGreen)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandMaxRed)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandMinBlue)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandMinGreen)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandMinRed)).EndInit();
            this.tabOther.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.sliderShiftAlpha)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderShiftRotation)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderShiftSize)).EndInit();
            this.ResumeLayout(false);

        }

        /// <summary>
        /// Determines whether the specified key is down.
        /// </summary>
        private static bool IsKeyDown(Keys key)
        {
            return Interop.SafeNativeMethods.GetKeyState((int)key) < 0;
        }

        /// <summary>
        /// Displays a context menu for changing background color options.
        /// </summary>
        /// <param name="sender">
        /// The control associated with the context menu.
        /// </param>
        /// <param name="location">
        /// The mouse location to appear at.
        /// </param>
        private void ShowBgContextMenu(Control sender, Point location)
        {
            ContextMenu contextMenu = new ContextMenu();

            //Options to set the background colors / image.
            contextMenu.MenuItems.Add(new MenuItem("Use transparent background",
                new EventHandler((a, b) =>
                {
                    displayCanvas.BackColor = Color.Transparent;
                    displayCanvas.BackgroundImageLayout = ImageLayout.Tile;
                    displayCanvas.BackgroundImage = Resources.CheckeredBg;
                })));
            contextMenu.MenuItems.Add(new MenuItem("Use white background",
                new EventHandler((a, b) =>
                {
                    displayCanvas.BackColor = Color.White;
                    displayCanvas.BackgroundImage = null;
                })));
            contextMenu.MenuItems.Add(new MenuItem("Use black background",
                new EventHandler((a, b) =>
                {
                    displayCanvas.BackColor = Color.Black;
                    displayCanvas.BackgroundImage = null;
                })));
            if (Clipboard.ContainsImage())
            {
                contextMenu.MenuItems.Add(new MenuItem("Use clipboard as background",
                    new EventHandler((a, b) =>
                    {
                        if (Clipboard.ContainsImage())
                        {
                            try
                            {
                                displayCanvas.BackgroundImage = Clipboard.GetImage();
                                displayCanvas.BackgroundImageLayout = ImageLayout.Stretch;
                                displayCanvas.BackColor = Color.Transparent;
                            }
                            catch
                            {
                                MessageBox.Show("Could not use clipboard image.");
                            }
                        }
                    })));
            }

            contextMenu.Show(sender, location);
        }

        /// <summary>
        /// Recreates the brush with color and alpha effects applied.
        /// </summary>
        private void UpdateBrush()
        {
            //Sets the color and alpha.
            Color setColor = bttnBrushColor.BackColor;
            float multAlpha = 1 - (sliderBrushAlpha.Value / 100f);

            //Applies the color and alpha changes.
            bmpBrushEffects = new Bitmap(bmpBrush);

            //Colorizes the image only if enabled.
            if (chkbxColorizeBrush.Checked)
            {
                Utils.ColorImage(bmpBrushEffects, setColor, multAlpha);
            }
            else
            {
                Utils.ColorImage(bmpBrushEffects, multAlpha);
            }
        }
        #endregion

        #region Methods (event handlers)
        /// <summary>
        /// Configures the drawing area and loads text localizations.
        /// </summary>
        private void WinBrushFactory_DialogLoad(object sender, EventArgs e)
        {
            //Sets the sizes of the canvas and drawing region.
            displayCanvas.Size = new RenderArgs(EffectSourceSurface).Bitmap.Size;
            bmpCurrentDrawing = new Bitmap(displayCanvas.Width, displayCanvas.Height);
            Utils.CopyBitmapPure(new RenderArgs(EffectSourceSurface).Bitmap, bmpCurrentDrawing);

            //Sets the canvas dimensions.
            displayCanvas.Left = (displayCanvasBG.Width - displayCanvas.Width) / 2;
            displayCanvas.Top = (displayCanvasBG.Height - displayCanvas.Height) / 2;

            //Adds versioning information to the window title.
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            this.Text = EffectPlugin.StaticName + " (version " +
                version.Major + "." +
                version.Minor + ")";

            //Loads globalization texts for regional support.
            txtBrushAlpha.Text = String.Format("{0} {1}",
                Localization.Strings.Alpha, sliderBrushAlpha.Value);

            txtBrushRotation.Text = String.Format("{0} {1}°",
                Localization.Strings.Rotation, sliderBrushRotation.Value);

            txtBrushSize.Text = String.Format("{0} {1}",
                Localization.Strings.Size, sliderBrushSize.Value);

            txtCanvasZoom.Text = String.Format("{0} {1}%",
                Localization.Strings.CanvasZoom, sliderCanvasZoom.Value);

            txtMinDrawDistance.Text = String.Format("{0} {1}",
                Localization.Strings.MinDrawDistance, sliderMinDrawDistance.Value);

            txtRandHorzShift.Text = String.Format("{0} {1}%",
                Localization.Strings.RandHorzShift, sliderRandHorzShift.Value);

            txtRandMaxSize.Text = String.Format("{0} {1}",
                Localization.Strings.RandMaxSize, sliderRandMaxSize.Value);

            txtRandMinSize.Text = String.Format("{0} {1}",
                Localization.Strings.RandMinSize, sliderRandMinSize.Value);

            txtRandRotLeft.Text = String.Format("{0} {1}°",
                Localization.Strings.RandRotLeft, sliderRandRotLeft.Value);

            txtRandRotRight.Text = String.Format("{0} {1}°",
                Localization.Strings.RandRotRight, sliderRandRotRight.Value);

            txtRandMaxAlpha.Text = String.Format("{0} {1}",
                Localization.Strings.RandMaxAlpha, sliderRandMaxAlpha.Value);

            txtRandMaxBlue.Text = String.Format("{0} {1}",
                Localization.Strings.RandMaxBlue, sliderRandMaxBlue.Value);

            txtRandMaxGreen.Text = String.Format("{0} {1}",
                Localization.Strings.RandMaxGreen, sliderRandMaxGreen.Value);

            txtRandMaxRed.Text = String.Format("{0} {1}",
                Localization.Strings.RandMaxRed, sliderRandMaxRed.Value);

            txtRandMinAlpha.Text = String.Format("{0} {1}",
                Localization.Strings.RandMinAlpha, sliderRandMinAlpha.Value);

            txtRandMinBlue.Text = String.Format("{0} {1}",
                Localization.Strings.RandMinBlue, sliderRandMinBlue.Value);

            txtRandMinGreen.Text = String.Format("{0} {1}",
                Localization.Strings.RandMinGreen, sliderRandMinGreen.Value);

            txtRandMinRed.Text = String.Format("{0} {1}",
                Localization.Strings.RandMinRed, sliderRandMinRed.Value);

            txtRandVertShift.Text = String.Format("{0} {1}%",
                Localization.Strings.RandVertShift, sliderRandVertShift.Value);

            txtShiftAlpha.Text = String.Format("{0} {1}",
                Localization.Strings.ShiftAlpha, sliderShiftAlpha.Value);

            txtShiftRotation.Text = String.Format("{0} {1}°",
                Localization.Strings.ShiftRotation, sliderShiftRotation.Value);

            txtShiftSize.Text = String.Format("{0} {1}",
                Localization.Strings.ShiftSize, sliderShiftSize.Value);

            txtTooltip.Text = Localization.Strings.GeneralTooltip;

            tabColor.Text = Localization.Strings.TabColor;
            tabControls.Text = Localization.Strings.TabControls;
            tabJitter.Text = Localization.Strings.TabJitter;
            tabOther.Text = Localization.Strings.TabOther;

            bttnBrushColor.Text = Localization.Strings.BrushColor;
            bttnCancel.Text = Localization.Strings.Cancel;
            bttnClearBrushes.Text = Localization.Strings.ClearBrushes;
            bttnClearSettings.Text = Localization.Strings.ClearSettings;
            bttnCustomBrushLocations.Text = Localization.Strings.CustomBrushLocations;
            bttnOk.Text = Localization.Strings.Ok;
            bttnUndo.Text = Localization.Strings.Undo;
            bttnRedo.Text = Localization.Strings.Redo;

            chkbxColorizeBrush.Text = Localization.Strings.ColorizeBrush;
            chkbxLockAlpha.Text = Localization.Strings.LockAlpha;
            chkbxOrientToMouse.Text = Localization.Strings.OrientToMouse;

            grpbxBrushOptions.Text = Localization.Strings.BrushOptions;
        }

        /// <summary>
        /// Sets the form resize restrictions.
        /// </summary>
        private void WinBrushFactory_DialogShown(object sender, EventArgs e)
        {
            MinimumSize = new Size(835, 526);
            MaximumSize = Size;
        }

        /// <summary>
        /// Disposes resources and deletes temporary files when the window
        /// closes for any reason.
        /// </summary>
        private void WinBrushFactory_FormClosing(object sender, FormClosingEventArgs e)
        {
            //Deletes all temporary files stored as undo/redo history.
            string path = Path.GetTempPath();
            List<string> files = new List<string>();
            files.AddRange(Directory.GetFiles(path, "HistoryBmp*.undo"));
            files.AddRange(Directory.GetFiles(path, "HistoryBmp*.redo"));
            foreach (string file in files)
            {
                File.Delete(file);
            }

            //Disposes all form bitmaps.
            bmpBrush.Dispose();
            bmpBrushEffects.Dispose();
            bmpCurrentDrawing.Dispose();
        }

        /// <summary>
        /// Handles keypresses for global commands.
        /// </summary>
        private void WinBrushFactory_KeyDown(object sender, KeyEventArgs e)
        {
            //Ctrl + Z: Undo.
            if (e.Control && e.KeyCode == Keys.Z)
            {
                BttnUndo_Click(this, e);
            }

            //Ctrl + Y: Redo.
            if (e.Control && e.KeyCode == Keys.Y)
            {
                BttnRedo_Click(this, e);
            }

            // [, Ctrl + [ increase rotation, alpha, size. ], Ctrl + ] decrease.
            int amountChange = 0;

            if (e.KeyCode == Keys.OemCloseBrackets && !e.Shift)
            {
                amountChange = e.Control ? 5 : 1;
            }
            else if (e.KeyCode == Keys.OemOpenBrackets && !e.Shift)
            {
                amountChange = e.Control ? -5 : -1;
            }

            if (amountChange != 0)
            {
                if (IsKeyDown(Keys.R))
                {
                    sliderBrushRotation.Value = Utils.Clamp(
                        sliderBrushRotation.Value + amountChange,
                        sliderBrushRotation.Minimum,
                        sliderBrushRotation.Maximum);
                }
                else if (IsKeyDown(Keys.A))
                {
                    sliderBrushAlpha.Value = Utils.Clamp(
                        sliderBrushAlpha.Value + amountChange,
                        sliderBrushAlpha.Minimum,
                        sliderBrushAlpha.Maximum);
                }
                else
                {
                    sliderBrushSize.Value = Utils.Clamp(
                        sliderBrushSize.Value + amountChange,
                        sliderBrushSize.Minimum,
                        sliderBrushSize.Maximum);
                }
            }

            //Prevents alt from making the form lose focus.
            if (e.Alt)
            {
                e.Handled = true;
            }
        }

        /// <summary>
        /// Recalculates the drawing region to maintain accuracy on resize.
        /// </summary>
        private void WinBrushFactory_Resize(object sender, EventArgs e)
        {
            displayCanvas.Left = (displayCanvasBG.Width - displayCanvas.Width) / 2;
            displayCanvas.Top = (displayCanvasBG.Height - displayCanvas.Height) / 2;
        }

        /// <summary>
        /// Sets up image panning and drawing to occur with mouse movement.
        /// </summary>
        private void DisplayCanvas_MouseDown(object sender, MouseEventArgs e)
        {
            //Displays a context menu for the background.
            if (e.Button == MouseButtons.Right)
            {
                ShowBgContextMenu(displayCanvas, e.Location);
            }

            //Enables and records image panning.
            else if (e.Button == MouseButtons.Middle)
            {
                isUserPanning = true;
                mouseLocPrev = e.Location;
            }

            //Enables and records brush drawing.
            else if (e.Button == MouseButtons.Left)
            {
                isUserDrawing = true;
                mouseLocPrev = e.Location;

                //Repositions the canvas when the user draws out-of-bounds.
                timerRepositionUpdate.Enabled = true;

                //Adds to the list of undo operations.
                string path = tempDir.GetTempPathName("HistoryBmp" + undoHistory.Count + ".undo");

                //Saves the drawing to the file and saves the file path.
                bmpCurrentDrawing.Save(path);
                undoHistory.Push(path);
                if (!bttnUndo.Enabled)
                {
                    bttnUndo.Enabled = true;
                }

                //Removes all redo history.
                redoHistory.Clear();

                //Draws the brush on the first canvas click.
                if (!chkbxOrientToMouse.Checked)
                {
                    DisplayCanvas_MouseMove(sender, e);
                }
            }
        }

        /// <summary>
        /// Ensures focusable controls cannot intercept keyboard/mouse input
        /// while the user is hovered over the display canvas. Sets a tooltip.
        /// </summary>
        private void DisplayCanvas_MouseEnter(object sender, EventArgs e)
        {
            displayCanvas.Focus();

            txtTooltip.Text = Localization.Strings.GeneralTooltip;
        }

        /// <summary>
        /// Sets up for drawing and handles panning.
        /// </summary>
        private void DisplayCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            //Updates the new location.
            mouseLoc = e.Location;

            //Handles panning the screen.
            if (isUserPanning && !displayCanvasBG.ClientRectangle.Contains(displayCanvas.ClientRectangle))
            {
                Rectangle range = GetRange();

                //Moves the drawing region.
                int locx = displayCanvas.Left + (mouseLoc.X - mouseLocPrev.X);
                int locy = displayCanvas.Top + (mouseLoc.Y - mouseLocPrev.Y);

                //Ensures the user cannot pan beyond the image bounds.
                if (locx <= range.Left) { locx = range.Left; }
                if (locx >= range.Right) { locx = range.Right; }
                if (locy <= range.Top) { locy = range.Top; }
                if (locy >= range.Bottom) { locy = range.Bottom; }

                //Updates the position of the canvas.
                Point loc = new Point(locx, locy);
                displayCanvas.Location = loc;
                displayCanvas.Refresh();
            }
            else if (isUserDrawing)
            {
                //Gets the brush's location without respect to canvas zooming.
                Point brushPoint = new Point(
                    (int)(mouseLoc.X / displayCanvasZoom),
                    (int)(mouseLoc.Y / displayCanvasZoom));

                //Randomly alters the radius according to random ranges.
                int newRadius = Utils.Clamp(sliderBrushSize.Value
                    - random.Next(sliderRandMinSize.Value)
                    + random.Next(sliderRandMaxSize.Value), 0, int.MaxValue);

                //Applies the brush drawing.
                DrawBrush(brushPoint, newRadius);

                mouseLocPrev = e.Location;
                displayCanvas.Refresh();
            }
            else
            {
                //Redraws to update the brush indicator (ellipse).
                displayCanvas.Refresh();
            }
        }

        /// <summary>
        /// Stops tracking panning and drawing.
        /// </summary>
        private void DisplayCanvas_MouseUp(object sender, MouseEventArgs e)
        {
            isUserPanning = false;
            isUserDrawing = false;
            timerRepositionUpdate.Enabled = false;

            //Lets the user click anywhere to draw again.
            mouseLocBrush = null;

            //Overwrites this brush stroke when done with the previous alpha.
            if (chkbxLockAlpha.Checked && undoHistory.Count != 0)
            {
                //Uses the undo bitmap to copy over current alpha values.
                string fileAndPath = undoHistory.Peek();
                if (File.Exists(fileAndPath))
                {
                    using (Bitmap prevStroke = new Bitmap(fileAndPath))
                    {
                        Utils.CopyAlpha(prevStroke, bmpCurrentDrawing);
                    }

                    displayCanvas.Refresh();
                }
            }
        }

        /// <summary>
        /// Zooms in and out of the drawing region.
        /// </summary>
        private void DisplayCanvas_MouseWheel(object sender, MouseEventArgs e)
        {
            //Ctrl + Wheel: Changes the brush size.
            if (ModifierKeys == Keys.Control)
            {
                //Ctrl + S + Wheel: Changes the brush size.
                if (IsKeyDown(Keys.S))
                {
                    int changeFactor = 10;

                    if (sliderBrushSize.Value < 5)
                    {
                        changeFactor = 1;
                    }
                    else if (sliderBrushSize.Value < 10)
                    {
                        changeFactor = 2;
                    }
                    else if (sliderBrushSize.Value < 30)
                    {
                        changeFactor = 5;
                    }
                    else if (sliderBrushSize.Value < 100)
                    {
                        changeFactor = 10;
                    }
                    else
                    {
                        changeFactor = 20;
                    }

                    sliderBrushSize.Value = Utils.Clamp(
                    sliderBrushSize.Value + Math.Sign(e.Delta) * changeFactor,
                    sliderBrushSize.Minimum,
                    sliderBrushSize.Maximum);
                }

                //Ctrl + R + Wheel: Changes the brush rotation.
                else if (IsKeyDown(Keys.R))
                {
                    sliderBrushRotation.Value = Utils.Clamp(
                    sliderBrushRotation.Value + Math.Sign(e.Delta) * 20,
                    sliderBrushRotation.Minimum,
                    sliderBrushRotation.Maximum);
                }

                //Ctrl + A + Wheel: Changes the brush alpha.
                else if (IsKeyDown(Keys.A))
                {
                    sliderBrushAlpha.Value = Utils.Clamp(
                    sliderBrushAlpha.Value + Math.Sign(e.Delta) * 10,
                    sliderBrushAlpha.Minimum,
                    sliderBrushAlpha.Maximum);
                }

                //Ctrl + Wheel: Zooms the canvas in/out.
                else
                {
                    Zoom(e.Delta, true);
                }
            }
        }

        /// <summary>
        /// Redraws the canvas and draws circles to indicate brush location.
        /// </summary>
        private void DisplayCanvas_Paint(object sender, PaintEventArgs e)
        {
            //Draws the whole canvas showing pixels and without smoothing.
            e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            e.Graphics.SmoothingMode = SmoothingMode.None;

            //Draws the image with an intentionally truncated extra size.
            //TODO: Remove the workaround (extra size) and find the cause.
            e.Graphics.DrawImage(bmpCurrentDrawing, 0, 0,
                displayCanvas.ClientRectangle.Width + (sliderCanvasZoom.Value / 100),
                displayCanvas.ClientRectangle.Height + (sliderCanvasZoom.Value / 100));

            //Draws the selection.
            if (Selection != null && Selection.GetRegionReadOnly() != null)
            {
                //Calculates the outline once the selection becomes valid.
                if (selectionOutline == null)
                {
                    selectionOutline = Selection.ConstructOutline(
                        new RectangleF(0, 0,
                        bmpCurrentDrawing.Width,
                        bmpCurrentDrawing.Height),
                        displayCanvasZoom);
                }

                //Scales to zoom so the drawing region accounts for scale.
                e.Graphics.ScaleTransform(displayCanvasZoom, displayCanvasZoom);

                //Creates the inverted region of the selection.
                var drawingArea = new Region(new Rectangle
                    (0, 0, bmpCurrentDrawing.Width, bmpCurrentDrawing.Height));
                drawingArea.Exclude(Selection.GetRegionReadOnly());

                //Draws the region as a darkening over unselected pixels.
                e.Graphics.FillRegion(
                    new SolidBrush(Color.FromArgb(63, 0, 0, 0)), drawingArea);

                //Draws the outline of the selection.
                if (selectionOutline?.GetRegionData() != null)
                {
                    e.Graphics.FillRegion(
                        SystemBrushes.Highlight,
                        selectionOutline.GetRegionReadOnly());
                }

                //Returns to ordinary scaling.
                e.Graphics.ScaleTransform(
                    1 / displayCanvasZoom,
                    1 / displayCanvasZoom);
            }

            //Draws the brush as a rectangle when not drawing by mouse.
            if (!isUserDrawing)
            {
                int radius = (int)(sliderBrushSize.Value * displayCanvasZoom);

                e.Graphics.DrawRectangle(
                    Pens.Black,
                    mouseLoc.X - (radius / 2),
                    mouseLoc.Y - (radius / 2),
                    radius,
                    radius);

                e.Graphics.DrawRectangle(
                    Pens.White,
                    mouseLoc.X - (radius / 2) - 1,
                    mouseLoc.Y - (radius / 2) - 1,
                    radius + 2,
                    radius + 2);
            }
        }

        /// <summary>
        /// Ensures focusable controls cannot intercept keyboard/mouse input
        /// while the user is hovered over the display canvas's panel. Sets a
        /// tooltip.
        /// </summary>
        private void DisplayCanvasBG_MouseEnter(object sender, EventArgs e)
        {
            displayCanvas.Focus();

            txtTooltip.Text = Localization.Strings.GeneralTooltip;
        }

        /// <summary>
        /// Sets the new color of the brush.
        /// </summary>
        private void BttnBrushColor_Click(object sender, EventArgs e)
        {
            //Creates and configures a color dialog to display.
            ColorDialog dialog = new ColorDialog();
            dialog.FullOpen = true;
            dialog.Color = bttnBrushColor.BackColor;

            //If the user successfully chooses a color.
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                //Makes the text that says 'colors' almost always legible.
                Color oppositeColor = Color.FromArgb(
                (byte)(255 - dialog.Color.R),
                (byte)(255 - dialog.Color.G),
                (byte)(255 - dialog.Color.B));
                bttnBrushColor.ForeColor = oppositeColor;

                //Sets the back color and updates the brushes.
                bttnBrushColor.BackColor = dialog.Color;
                UpdateBrush();
            }
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void BttnBrushColor_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.BrushColorTip;
        }

        /// <summary>
        /// Draws the current item's image and text. This is automatically
        /// called for each item to be drawn.
        /// </summary>
        private void BttnBrushSelector_DrawItem(object sender, DrawItemEventArgs e)
        {
            //Constrains the image drawing space of each item's picture so it
            //draws without distortion, which is why size is height * height.
            Rectangle pictureLocation = new Rectangle(2, e.Bounds.Top,
                e.Bounds.Height, e.Bounds.Height);

            //Repaints white over the image and text area.
            e.Graphics.FillRectangle(
                Brushes.White,
                new Rectangle(2, e.Bounds.Top, e.Bounds.Width, e.Bounds.Height));

            //Draws the image of the current item to be repainted.
            if (loadedBrushes[e.Index].Brush != null)
            {
                e.Graphics.DrawImage(loadedBrushes[e.Index].Brush, pictureLocation);
            }

            //Draws the text of the current item to be repainted.
            //Draws the custom brush text centered as there is no picture.
            if (bttnBrushSelector.Items[e.Index] == BrushSelectorItem.CustomBrush)
            {
                e.Graphics.DrawString(
                    bttnBrushSelector.GetItemText(bttnBrushSelector.Items[e.Index]),
                    bttnBrushSelector.Font,
                    Brushes.Black,
                    new Point(e.Bounds.X + 4, e.Bounds.Y + 6));
            }
            else
            {
                e.Graphics.DrawString(
                    bttnBrushSelector.GetItemText(bttnBrushSelector.Items[e.Index]),
                    bttnBrushSelector.Font,
                    Brushes.Black,
                    new Point(e.Bounds.X + pictureLocation.Width, e.Bounds.Y + 6));
            }
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void BttnBrushSelector_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.BrushSelectorTip;
        }

        /// <summary>
        /// Sets the brush when the user changes it with the selector.
        /// </summary>
        private void BttnBrushSelector_SelectedIndexChanged(object sender, EventArgs e)
        {
            //Gets the currently selected item.
            BrushSelectorItem currentItem =
                (bttnBrushSelector.SelectedItem as BrushSelectorItem);

            //Opens a file dialog for the user to load brushes.
            if (currentItem.Name.Equals(BrushSelectorItem.CustomBrush.Name))
            {
                ImportBrushes(true);
            }

            //Sets the brush otherwise.
            else
            {
                bmpBrush = Utils.FormatImage(
                    new Bitmap(currentItem.Brush),
                    PixelFormat.Format32bppArgb);
            }

            UpdateBrush();
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void BttnBrushSmoothing_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.BrushSmoothingTip;
        }

        /// <summary>
        /// Cancels and doesn't apply the effect.
        /// </summary>
        private void BttnCancel_Click(object sender, EventArgs e)
        {
            //Disables the button so it can't accidentally be called twice.
            bttnCancel.Enabled = false;

            this.Close();
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void BttnCancel_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.CancelTip;
        }

        /// <summary>
        /// Removes all brushes added by the user.
        /// </summary>
        private void BttnClearBrushes_Click(object sender, EventArgs e)
        {
            InitBrushes();
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void BttnClearBrushes_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.ClearBrushesTip;
        }

        /// <summary>
        /// Resets all settings back to their default values.
        /// </summary>
        private void BttnClearSettings_Click(object sender, EventArgs e)
        {
            InitSettings();
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void BttnClearSettings_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.ClearSettingsTip;
        }

        /// <summary>
        /// Accepts and applies the effect.
        /// </summary>
        private void BttnOk_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;

            //Disables the button so it can't accidentally be called twice.
            //Ensures settings will be saved.
            bttnOk.Enabled = false;

            //Sets the bitmap to draw. Locks to prevent concurrency.
            lock (RenderSettings.BmpToRender)
            {
                RenderSettings.BmpToRender = new Bitmap(bmpCurrentDrawing);
            }

            //Updates the saved effect settings and OKs the effect.
            RenderSettings.DoApplyEffect = true;
            FinishTokenUpdate();

            this.Close();
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void BttnOk_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.OkTip;
        }

        /// <summary>
        /// Opens the preferences dialog to define persistent settings.
        /// </summary>
        private void BttnPreferences_Click(object sender, EventArgs e)
        {
            new Gui.BrushFactoryPreferences().ShowDialog();
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void BttnPreferences_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.CustomBrushLocationsTip;
        }

        /// <summary>
        /// Reverts to a previously-undone drawing stored in a temporary file.
        /// </summary>
        private void BttnRedo_Click(object sender, EventArgs e)
        {
            //Does nothing if there is nothing to redo.
            if (redoHistory.Count == 0)
            {
                return;
            }

            //Prevents an error that would occur if redo was pressed in the
            //middle of a drawing operation by aborting it.
            isUserDrawing = false;

            //Acquires the bitmap from the file and loads it if it exists.
            string fileAndPath = redoHistory.Pop();
            if (File.Exists(fileAndPath))
            {
                //Saves the drawing to the file for undo.
                string path = tempDir.GetTempPathName("HistoryBmp" + undoHistory.Count + ".undo");
                bmpCurrentDrawing.Save(path);
                undoHistory.Push(path);

                //Clears the current drawing (in case parts are transparent),
                //and draws the saved version.
                using (Bitmap redoBmp = new Bitmap(fileAndPath))
                {
                    Utils.CopyBitmapPure(redoBmp, bmpCurrentDrawing);
                }

                displayCanvas.Refresh();
            }
            else
            {
                MessageBox.Show("File could not be found for redo.");
            }

            //Handles enabling undo or disabling redo for the user's clarity.
            if (redoHistory.Count == 0)
            {
                bttnRedo.Enabled = false;
            }
            if (!bttnUndo.Enabled && undoHistory.Count > 0)
            {
                bttnUndo.Enabled = true;
            }
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void BttnRedo_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.RedoTip;
        }

        /// <summary>
        /// Reverts to a previously-saved drawing stored in a temporary file.
        /// </summary>
        private void BttnUndo_Click(object sender, EventArgs e)
        {
            //Does nothing if there is nothing to undo.
            if (undoHistory.Count == 0)
            {
                return;
            }

            //Prevents an error that would occur if undo was pressed in the
            //middle of a drawing operation by aborting it.
            isUserDrawing = false;

            //Acquires the bitmap from the file and loads it if it exists.
            string fileAndPath = undoHistory.Pop();
            if (File.Exists(fileAndPath))
            {
                //Saves the drawing to the file for redo.
                string path = tempDir.GetTempPathName("HistoryBmp" + redoHistory.Count + ".redo");
                bmpCurrentDrawing.Save(path);
                redoHistory.Push(path);

                //Clears the current drawing (in case parts are transparent),
                //and draws the saved version.
                using (Bitmap undoBmp = new Bitmap(fileAndPath))
                {
                    Utils.CopyBitmapPure(undoBmp, bmpCurrentDrawing);
                }

                displayCanvas.Refresh();
            }
            else
            {
                MessageBox.Show("File could not be found for undo.");
            }

            //Handles enabling redo or disabling undo for the user's clarity.
            if (undoHistory.Count == 0)
            {
                bttnUndo.Enabled = false;
            }
            if (!bttnRedo.Enabled && redoHistory.Count > 0)
            {
                bttnRedo.Enabled = true;
            }
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void BttnUndo_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.UndoTip;
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void BttnSymmetry_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.SymmetryTip;
        }

        /// <summary>
        /// Resets the brush to reconfigure colorization. Colorization is
        /// applied when the brush is refreshed.
        /// </summary>
        private void ChkbxColorizeBrush_CheckedChanged(object sender, EventArgs e)
        {
            UpdateBrush();
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void ChkbxColorizeBrush_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.ColorizeBrushTip;
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void ChkbxLockAlpha_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.LockAlphaTip;
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void ChkbxOrientToMouse_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.OrientToMouseTip;
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void SliderBrushAlpha_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.BrushAlphaTip;
        }

        /// <summary>
        /// Adjusts the brush alpha text when it changes.
        /// </summary>
        private void SliderBrushAlpha_ValueChanged(object sender, EventArgs e)
        {
            //Uses localized text drawn from a resource file.
            txtBrushAlpha.Text = String.Format("{0} {1}",
                Localization.Strings.Alpha,
                sliderBrushAlpha.Value);

            UpdateBrush();
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void SliderBrushSize_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.BrushSizeTip;
        }

        /// <summary>
        /// Adjusts the brush size text when it changes.
        /// </summary>
        private void SliderBrushSize_ValueChanged(object sender, EventArgs e)
        {
            //Uses localized text drawn from a resource file.
            txtBrushSize.Text = String.Format("{0} {1}",
                Localization.Strings.Size,
                sliderBrushSize.Value);

            //Updates to show changes in the brush indicator.
            displayCanvas.Refresh();
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void SliderBrushRotation_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.BrushRotationTip;
        }

        /// <summary>
        /// Adjusts the brush rotation text when it changes.
        /// </summary>
        private void SliderBrushRotation_ValueChanged(object sender, EventArgs e)
        {
            //Uses localized text drawn from a resource file.
            txtBrushRotation.Text = String.Format("{0} {1}°",
                Localization.Strings.Rotation,
                sliderBrushRotation.Value);
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void SliderCanvasZoom_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.CanvasZoomTip;
        }

        /// <summary>
        /// Zooms in and out of the drawing region.
        /// </summary>
        private void SliderCanvasZoom_ValueChanged(object sender, EventArgs e)
        {
            Zoom(0, false);
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void SliderMinDrawDistance_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.MinDrawDistanceTip;
        }

        /// <summary>
        /// Adjusts the brush minimum drawing distance text when it changes.
        /// </summary>
        private void SliderMinDrawDistance_ValueChanged(object sender, EventArgs e)
        {
            //Uses localized text drawn from a resource file.
            txtMinDrawDistance.Text = String.Format("{0} {1}",
                Localization.Strings.MinDrawDistance,
                sliderMinDrawDistance.Value);
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void SliderRandHorzShift_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.RandHorzShiftTip;
        }

        /// <summary>
        /// Adjusts the random horizontal shift text when it changes.
        /// </summary>
        private void SliderRandHorzShift_ValueChanged(object sender, EventArgs e)
        {
            //Uses localized text drawn from a resource file.
            txtRandHorzShift.Text = String.Format("{0} {1}%",
                Localization.Strings.RandHorzShift,
                sliderRandHorzShift.Value);
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void SliderRandMaxAlpha_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.RandMaxAlphaTip;
        }

        /// <summary>
        /// Adjusts the random max alpha text when it changes.
        /// </summary>
        private void SliderRandMaxAlpha_ValueChanged(object sender, EventArgs e)
        {
            //Uses localized text drawn from a resource file.
            txtRandMaxAlpha.Text = String.Format("{0} {1}",
                Localization.Strings.RandMaxAlpha,
                sliderRandMaxAlpha.Value);
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void SliderRandMaxBlue_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.RandMaxBlueTip;
        }

        /// <summary>
        /// Adjusts the random max blueness text when it changes.
        /// </summary>
        private void SliderRandMaxBlue_ValueChanged(object sender, EventArgs e)
        {
            //Uses localized text drawn from a resource file.
            txtRandMaxBlue.Text = String.Format("{0} {1}",
                Localization.Strings.RandMaxBlue,
                sliderRandMaxBlue.Value);
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void SliderRandMaxGreen_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.RandMaxGreenTip;
        }

        /// <summary>
        /// Adjusts the random max greenness text when it changes.
        /// </summary>
        private void SliderRandMaxGreen_ValueChanged(object sender, EventArgs e)
        {
            //Uses localized text drawn from a resource file.
            txtRandMaxGreen.Text = String.Format("{0} {1}",
                Localization.Strings.RandMaxGreen,
                sliderRandMaxGreen.Value);
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void SliderRandMaxRed_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.RandMaxRedTip;
        }

        /// <summary>
        /// Adjusts the random max redness text when it changes.
        /// </summary>
        private void SliderRandMaxRed_ValueChanged(object sender, EventArgs e)
        {
            //Uses localized text drawn from a resource file.
            txtRandMaxRed.Text = String.Format("{0} {1}",
                Localization.Strings.RandMaxRed,
                sliderRandMaxRed.Value);
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void SliderRandMaxSize_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.RandMaxSizeTip;
        }

        /// <summary>
        /// Adjusts the random max size text when it changes.
        /// </summary>
        private void SliderRandMaxSize_ValueChanged(object sender, EventArgs e)
        {
            //Uses localized text drawn from a resource file.
            txtRandMaxSize.Text = String.Format("{0} {1}",
                Localization.Strings.RandMaxSize,
                sliderRandMaxSize.Value);
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void SliderRandMinAlpha_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.RandMinAlphaTip;
        }

        /// <summary>
        /// Adjusts the random min alpha text when it changes.
        /// </summary>
        private void SliderRandMinAlpha_ValueChanged(object sender, EventArgs e)
        {
            //Uses localized text drawn from a resource file.
            txtRandMinAlpha.Text = String.Format("{0} {1}",
                Localization.Strings.RandMinAlpha,
                sliderRandMinAlpha.Value);
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void SliderRandMinBlue_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.RandMinBlueTip;
        }

        /// <summary>
        /// Adjusts the random min blueness text when it changes.
        /// </summary>
        private void SliderRandMinBlue_ValueChanged(object sender, EventArgs e)
        {
            //Uses localized text drawn from a resource file.
            txtRandMinBlue.Text = String.Format("{0} {1}",
                Localization.Strings.RandMinBlue,
                sliderRandMinBlue.Value);
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void SliderRandMinGreen_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.RandMinGreenTip;
        }

        /// <summary>
        /// Adjusts the random min greenness text when it changes.
        /// </summary>
        private void SliderRandMinGreen_ValueChanged(object sender, EventArgs e)
        {
            //Uses localized text drawn from a resource file.
            txtRandMinGreen.Text = String.Format("{0} {1}",
                Localization.Strings.RandMinGreen,
                sliderRandMinGreen.Value);
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void SliderRandMinRed_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.RandMinRedTip;
        }

        /// <summary>
        /// Adjusts the random min redness text when it changes.
        /// </summary>
        private void SliderRandMinRed_ValueChanged(object sender, EventArgs e)
        {
            //Uses localized text drawn from a resource file.
            txtRandMinRed.Text = String.Format("{0} {1}",
                Localization.Strings.RandMinRed,
                sliderRandMinRed.Value);
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void SliderRandMinSize_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.RandMinSizeTip;
        }

        /// <summary>
        /// Adjusts the random min size text when it changes.
        /// </summary>
        private void SliderRandMinSize_ValueChanged(object sender, EventArgs e)
        {
            //Uses localized text drawn from a resource file.
            txtRandMinSize.Text = String.Format("{0} {1}",
                Localization.Strings.RandMinSize,
                sliderRandMinSize.Value);
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void SliderRandRotLeft_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.RandRotLeftTip;
        }

        /// <summary>
        /// Adjusts the random rotation to the left text when it changes.
        /// </summary>
        private void SliderRandRotLeft_ValueChanged(object sender, EventArgs e)
        {
            //Uses localized text drawn from a resource file.
            txtRandRotLeft.Text = String.Format("{0} {1}°",
                Localization.Strings.RandRotLeft,
                sliderRandRotLeft.Value);
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void SliderRandRotRight_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.RandRotRightTip;
        }

        /// <summary>
        /// Adjusts the random rotation to the right text when it changes.
        /// </summary>
        private void SliderRandRotRight_ValueChanged(object sender, EventArgs e)
        {
            //Uses localized text drawn from a resource file.
            txtRandRotRight.Text = String.Format("{0} {1}°",
                Localization.Strings.RandRotRight,
                sliderRandRotRight.Value);
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void SliderRandVertShift_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.RandVertShiftTip;
        }

        /// <summary>
        /// Adjusts the random vertical shift text when it changes.
        /// </summary>
        private void SliderRandVertShift_ValueChanged(object sender, EventArgs e)
        {
            //Uses localized text drawn from a resource file.
            txtRandVertShift.Text = String.Format("{0} {1}%",
                Localization.Strings.RandVertShift,
                sliderRandVertShift.Value);
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void SliderShiftAlpha_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.ShiftAlphaTip;
        }

        /// <summary>
        /// Adjusts the slider shift alpha text when it changes.
        /// </summary>
        private void SliderShiftAlpha_ValueChanged(object sender, EventArgs e)
        {
            //Uses localized text drawn from a resource file.
            txtShiftAlpha.Text = String.Format("{0} {1}",
                Localization.Strings.ShiftAlpha,
                sliderShiftAlpha.Value);
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void SliderShiftRotation_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.ShiftRotationTip;
        }

        /// <summary>
        /// Adjusts the slider shift rotation text when it changes.
        /// </summary>
        private void SliderShiftRotation_ValueChanged(object sender, EventArgs e)
        {
            //Uses localized text drawn from a resource file.
            txtShiftRotation.Text = String.Format("{0} {1}°",
                Localization.Strings.ShiftRotation,
                sliderShiftRotation.Value);
        }

        /// <summary>
        /// Sets a tooltip.
        /// </summary>
        private void SliderShiftSize_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.ShiftSizeTip;
        }

        /// <summary>
        /// Adjusts the slider shift size text when it changes.
        /// </summary>
        private void SliderShiftSize_ValueChanged(object sender, EventArgs e)
        {
            //Uses localized text drawn from a resource file.
            txtShiftSize.Text = String.Format("{0} {1}",
                Localization.Strings.ShiftSize,
                sliderShiftSize.Value);
        }

        /// <summary>
        /// While drawing, moves the canvas automatically when trying to draw
        /// past the visible bounds.
        /// </summary>
        private void RepositionUpdate_Tick(object sender, EventArgs e)
        {
            /*Converts the mouse coordinates on the screen relative to the
             * background such that the top-left corner is (0, 0) up to its
             * width and height.*/
            Point mouseLocOnBG = displayCanvasBG.PointToClient(MousePosition);

            //Exits if the user isn't drawing out of the canvas boundary.
            if (!isUserDrawing ||
                displayCanvasBG.ClientRectangle.Contains(mouseLocOnBG) ||
                displayCanvasBG.ClientRectangle.Contains(displayCanvas.ClientRectangle))
            {
                return;
            }

            //Amount of space between the display canvas and background.
            Rectangle range = GetRange();

            //The amount to move the canvas while drawing.
            int nudge = (int)(displayCanvasZoom * 10);
            int canvasNewPosX, canvasNewPosY;

            //Nudges the screen horizontally when out of bounds and out of
            //the drawing region.
            if (displayCanvas.ClientRectangle.Width >=
                displayCanvasBG.ClientRectangle.Width)
            {
                if (mouseLocOnBG.X > displayCanvasBG.ClientRectangle.Width)
                {
                    canvasNewPosX = -nudge;
                }
                else if (mouseLocOnBG.X < 0)
                {
                    canvasNewPosX = nudge;
                }
                else
                {
                    canvasNewPosX = 0;
                }
            }
            else
            {
                canvasNewPosX = 0;
            }

            //Adds the left corner position to make it relative.
            if (range.Width != 0)
            {
                canvasNewPosX += displayCanvas.Left;
            }

            //Nudges the screen vertically when out of bounds and out of
            //the drawing region.
            if (displayCanvas.ClientRectangle.Height >=
                displayCanvasBG.ClientRectangle.Height)
            {
                if (mouseLocOnBG.Y > displayCanvasBG.ClientRectangle.Height)
                {
                    canvasNewPosY = -nudge;
                }
                else if (mouseLocOnBG.Y < 0)
                {
                    canvasNewPosY = nudge;
                }
                else
                {
                    canvasNewPosY = 0;
                }
            }
            else
            {
                canvasNewPosY = 0;
            }

            //Adds the top corner position to make it relative.
            if (range.Height != 0)
            {
                canvasNewPosY += displayCanvas.Top;
            }

            //Clamps all location values.
            if (canvasNewPosX <= range.Left) { canvasNewPosX = range.Left; }
            if (canvasNewPosX >= range.Right) { canvasNewPosX = range.Right; }
            if (canvasNewPosY <= range.Top) { canvasNewPosY = range.Top; }
            if (canvasNewPosY >= range.Bottom) { canvasNewPosY = range.Bottom; }

            //Updates with the new location and redraws the screen.
            displayCanvas.Location = new Point(canvasNewPosX, canvasNewPosY);
            displayCanvas.Refresh();
        }
        #endregion
    }
}