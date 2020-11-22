using BrushFactory.Abr;
using BrushFactory.Logic;
using BrushFactory.Properties;
using PaintDotNet;
using PaintDotNet.AppModel;
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
using System.Security;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace BrushFactory
{
    /// <summary>
    /// The dialog used for working with the effect.
    /// </summary>
    public class WinBrushFactory : EffectConfigDialog
    {
        #region Fields (Non Gui)
        private Tool activeTool = Tool.Brush;

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
        private Bitmap bmpCurrentDrawing = new Bitmap(1, 1, PixelFormat.Format32bppPArgb);

        /// <summary>
        /// Loads user's custom brushes asynchronously.
        /// </summary>
        private BackgroundWorker brushLoadingWorker;

        /// <summary>
        /// Stores the disposable data for the color picker cursor.
        /// </summary>
        private Cursor cursorColorPicker;

        /// <summary>
        /// Stores the zoom percentage for the drawing region.
        /// </summary>
        private float displayCanvasZoom = 1;

        /// <summary>
        /// Whether the brush loading worker should reload brushes after cancelation.
        /// </summary>
        private bool doReinitializeBrushes;

        /// <summary>
        /// Indicates whether the brushes need to be imported from the
        /// collection of loaded brush paths stored in the effect token.
        /// </summary>
        private bool importBrushesFromToken;

        private bool isFormClosing;

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

        private bool isUserDrawing = false;
        private bool isUserPanning = false;

        private bool isWheelZooming = false;

        /// <summary>
        /// Creates the list of brushes used by the brush selector.
        /// </summary>
        private BrushSelectorItemCollection loadedBrushes;

        /// <summary>
        /// Stores the user's custom brushes by file and path until it can
        /// be copied to persistent settings, or ignored.
        /// </summary>
        private HashSet<string> loadedBrushPaths =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

        private BrushFactorySettings settings;

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
        /// The folder used to store undo/redo images, and deleted on exit.
        /// </summary>
        private TempDirectory tempDir;

        /// <summary>
        /// The selected brush name from the effect token.
        /// </summary>
        private string tokenSelectedBrushName;

        private Random random = new Random();

        /// <summary>
        /// List of temporary file names to load to perform redo.
        /// </summary>
        private Stack<string> redoHistory = new Stack<string>();

        /// <summary>
        /// List of temporary file names to load to perform undo.
        /// </summary>
        private Stack<string> undoHistory = new Stack<string>();

        /// <summary>
        /// A list of all visible items in the brush selector for thumbnails.
        /// </summary>
        private ListViewItem[] visibleBrushes;

        /// <summary>
        /// The starting index in the brush selector cache.
        /// </summary>
        private int visibleBrushesIndex;
        #endregion

        #region Fields (Gui)
        private Button bttnBrushColor;
        private DoubleBufferedListView bttnBrushSelector;

        /// <summary>
        /// An empty image list that allows the brush thumbnail size to be changed.
        /// </summary>
        private ImageList dummyImageList;

        private Button bttnAddBrushes;
        private ComboBox bttnBrushSmoothing;
        private Button bttnCancel;
        private Button bttnClearBrushes;
        private Button bttnClearSettings;
        private Button bttnColorPicker;
        private Button bttnCustomBrushLocations;
        private Button bttnOk;
        private Button bttnRedo;
        private ComboBox bttnSymmetry;
        private Button bttnUndo;
        private ProgressBar brushLoadProgressBar;

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

        private IContainer components;
        internal PictureBox displayCanvas;
        private Panel displayCanvasBG;
        private GroupBox grpbxBrushOptions;
        private TrackBar sliderBrushAlpha;
        private TrackBar sliderBrushDensity;
        private TrackBar sliderBrushRotation;
        private TrackBar sliderBrushSize;
        private TrackBar sliderCanvasZoom;
        private TrackBar sliderJitterMaxBlue;
        private TrackBar sliderJitterMaxGreen;
        private TrackBar sliderJitterMaxHue;
        private TrackBar sliderJitterMaxRed;
        private TrackBar sliderJitterMaxSat;
        private TrackBar sliderJitterMaxVal;
        private TrackBar sliderJitterMinBlue;
        private TrackBar sliderJitterMinGreen;
        private TrackBar sliderJitterMinHue;
        private TrackBar sliderJitterMinRed;
        private TrackBar sliderJitterMinSat;
        private TrackBar sliderJitterMinVal;

        /// <summary>
        /// The mouse must be at least this far away from its last successful
        /// brush image application to create another brush image. Used for
        /// spacing between brush images during a stroke.
        /// </summary>
        private TrackBar sliderMinDrawDistance;

        private TrackBar sliderRandHorzShift;
        private TrackBar sliderRandMaxSize;
        private TrackBar sliderRandMinAlpha;
        private TrackBar sliderRandMinSize;
        private TrackBar sliderRandRotLeft;
        private TrackBar sliderRandRotRight;
        private TrackBar sliderRandVertShift;
        private TrackBar sliderShiftAlpha;
        private TrackBar sliderShiftRotation;
        private TrackBar sliderShiftSize;
        private TabControl tabBar;
        private TabPage tabControls;
        private TabPage tabJitter;
        private TabPage tabColor;
        private TabPage tabOther;

        /// <summary>
        /// Tracks when the user draws out-of-bounds and moves the canvas to
        /// accomodate them.
        /// </summary>
        private Timer timerRepositionUpdate;

        private Label txtBrushAlpha;
        private Label txtBrushDensity;
        private Label txtBrushRotation;
        private Label txtBrushSize;
        private Label txtCanvasZoom;
        private Label txtJitterBlue;
        private Label txtJitterGreen;
        private Label txtJitterHue;
        private Label txtJitterRed;
        private Label txtJitterSaturation;
        private Label txtJitterValue;
        private Label txtMinDrawDistance;
        private Label txtRandHorzShift;
        private Label txtRandRotRight;
        private Label txtRandRotLeft;
        private Label txtRandMaxSize;
        private Label txtRandMinAlpha;
        private Label txtRandMinSize;
        private Label txtRandVertShift;
        private Label txtShiftAlpha;
        private Label txtShiftRotation;
        private Label txtShiftSize;
        private Label txtTooltip;
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

            loadedBrushes = new BrushSelectorItemCollection();

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
        }
        #endregion

        #region Methods (overridden)
        /// <summary>
        /// Configures settings so they can be stored between consecutive
        /// calls of the effect.
        /// </summary>
        protected override void InitialInitToken()
        {
            theEffectToken = new PersistentSettings();
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
                loadedBrushPaths.UnionWith(token.CustomBrushLocations);
                importBrushesFromToken = true;
            }

            tokenSelectedBrushName = token.BrushName;

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
            sliderBrushAlpha.Value = token.BrushAlpha;
            sliderBrushDensity.Value = token.BrushDensity;
            sliderBrushRotation.Value = token.BrushRotation;
            sliderRandHorzShift.Value = token.RandHorzShift;
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
            sliderJitterMaxRed.Value = token.RandMaxR;
            sliderJitterMaxGreen.Value = token.RandMaxG;
            sliderJitterMaxBlue.Value = token.RandMaxB;
            sliderJitterMinRed.Value = token.RandMinR;
            sliderJitterMinGreen.Value = token.RandMinG;
            sliderJitterMinBlue.Value = token.RandMinB;
            sliderJitterMaxHue.Value = token.RandMaxH;
            sliderJitterMaxSat.Value = token.RandMaxS;
            sliderJitterMaxVal.Value = token.RandMaxV;
            sliderJitterMinHue.Value = token.RandMinH;
            sliderJitterMinSat.Value = token.RandMinS;
            sliderJitterMinVal.Value = token.RandMinV;
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

            int index = bttnBrushSelector.SelectedIndices.Count > 0
                ? bttnBrushSelector.SelectedIndices[0]
                : -1;

            token.AlphaChange = sliderShiftAlpha.Value;
            token.BrushAlpha = sliderBrushAlpha.Value;
            token.BrushColor = bttnBrushColor.BackColor;
            token.BrushDensity = sliderBrushDensity.Value;
            token.BrushName = index >= 0 ? loadedBrushes[index].Name : string.Empty;
            token.BrushRotation = sliderBrushRotation.Value;
            token.BrushSize = sliderBrushSize.Value;
            token.CustomBrushLocations = loadedBrushPaths;
            token.DoColorizeBrush = chkbxColorizeBrush.Checked;
            token.DoLockAlpha = chkbxLockAlpha.Checked;
            token.DoRotateWithMouse = chkbxOrientToMouse.Checked;
            token.MinDrawDistance = sliderMinDrawDistance.Value;
            token.RandHorzShift = sliderRandHorzShift.Value;
            token.RandMaxB = sliderJitterMaxBlue.Value;
            token.RandMaxG = sliderJitterMaxGreen.Value;
            token.RandMaxR = sliderJitterMaxRed.Value;
            token.RandMaxH = sliderJitterMaxHue.Value;
            token.RandMaxS = sliderJitterMaxSat.Value;
            token.RandMaxV = sliderJitterMaxVal.Value;
            token.RandMaxSize = sliderRandMaxSize.Value;
            token.RandMinAlpha = sliderRandMinAlpha.Value;
            token.RandMinB = sliderJitterMinBlue.Value;
            token.RandMinG = sliderJitterMinGreen.Value;
            token.RandMinR = sliderJitterMinRed.Value;
            token.RandMinH = sliderJitterMinHue.Value;
            token.RandMinS = sliderJitterMinSat.Value;
            token.RandMinV = sliderJitterMinVal.Value;
            token.RandMinSize = sliderRandMinSize.Value;
            token.RandRotLeft = sliderRandRotLeft.Value;
            token.RandRotRight = sliderRandRotRight.Value;
            token.RandVertShift = sliderRandVertShift.Value;
            token.RotChange = sliderShiftRotation.Value;
            token.SizeChange = sliderShiftSize.Value;
            token.Symmetry = (SymmetryMode)bttnSymmetry.SelectedIndex;
        }

        /// <summary>
        /// Configures the drawing area and loads text localizations.
        /// </summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            //Sets the sizes of the canvas and drawing region.
            displayCanvas.Size = EnvironmentParameters.SourceSurface.Size;
            bmpCurrentDrawing = Utils.CreateBitmapFromSurface(EnvironmentParameters.SourceSurface);

            //Sets the canvas dimensions.
            displayCanvas.Left = (displayCanvasBG.Width - displayCanvas.Width) / 2;
            displayCanvas.Top = (displayCanvasBG.Height - displayCanvas.Height) / 2;

            //Adds versioning information to the window title.
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            Text = EffectPlugin.StaticName + " (version " +
                version.Major + "." +
                version.Minor + ")";

            //Loads globalization texts for regional support.
            txtBrushAlpha.Text = string.Format("{0} {1}",
                Localization.Strings.Alpha, sliderBrushAlpha.Value);

            txtBrushDensity.Text = string.Format("{0} {1}",
                Localization.Strings.BrushDensity, sliderBrushDensity.Value);

            txtBrushRotation.Text = string.Format("{0} {1}°",
                Localization.Strings.Rotation, sliderBrushRotation.Value);

            txtBrushSize.Text = string.Format("{0} {1}",
                Localization.Strings.Size, sliderBrushSize.Value);

            txtCanvasZoom.Text = string.Format("{0} {1}%",
                Localization.Strings.CanvasZoom, sliderCanvasZoom.Value);

            txtMinDrawDistance.Text = string.Format("{0} {1}",
                Localization.Strings.MinDrawDistance, sliderMinDrawDistance.Value);

            txtRandHorzShift.Text = string.Format("{0} {1}%",
                Localization.Strings.RandHorzShift, sliderRandHorzShift.Value);

            txtRandMaxSize.Text = string.Format("{0} {1}",
                Localization.Strings.RandMaxSize, sliderRandMaxSize.Value);

            txtRandMinSize.Text = string.Format("{0} {1}",
                Localization.Strings.RandMinSize, sliderRandMinSize.Value);

            txtRandRotLeft.Text = string.Format("{0} {1}°",
                Localization.Strings.RandRotLeft, sliderRandRotLeft.Value);

            txtRandRotRight.Text = string.Format("{0} {1}°",
                Localization.Strings.RandRotRight, sliderRandRotRight.Value);

            txtRandMinAlpha.Text = string.Format("{0} {1}",
                Localization.Strings.RandMinAlpha, sliderRandMinAlpha.Value);

            txtJitterBlue.Text = string.Format("{0} -{1}%, +{2}%",
                Localization.Strings.JitterBlue, sliderJitterMinBlue.Value, sliderJitterMaxBlue.Value);

            txtJitterGreen.Text = string.Format("{0} -{1}%, +{2}%",
                Localization.Strings.JitterGreen, sliderJitterMinGreen.Value, sliderJitterMaxGreen.Value);

            txtJitterRed.Text = string.Format("{0} -{1}%, +{2}%",
                Localization.Strings.JitterRed, sliderJitterMinRed.Value, sliderJitterMaxRed.Value);

            txtJitterHue.Text = string.Format("{0} -{1}%, +{2}%",
                Localization.Strings.JitterHue, sliderJitterMinHue.Value, sliderJitterMaxHue.Value);

            txtJitterSaturation.Text = string.Format("{0} -{1}%, +{2}%",
                Localization.Strings.JitterSaturation, sliderJitterMinSat.Value, sliderJitterMaxSat.Value);

            txtJitterValue.Text = string.Format("{0} -{1}%, +{2}%",
                Localization.Strings.JitterValue, sliderJitterMinVal.Value, sliderJitterMaxVal.Value);

            txtRandVertShift.Text = string.Format("{0} {1}%",
                Localization.Strings.RandVertShift, sliderRandVertShift.Value);

            txtShiftAlpha.Text = string.Format("{0} {1}",
                Localization.Strings.ShiftAlpha, sliderShiftAlpha.Value);

            txtShiftRotation.Text = string.Format("{0} {1}°",
                Localization.Strings.ShiftRotation, sliderShiftRotation.Value);

            txtShiftSize.Text = string.Format("{0} {1}",
                Localization.Strings.ShiftSize, sliderShiftSize.Value);

            txtTooltip.Text = Localization.Strings.GeneralTooltip;

            tabColor.Text = Localization.Strings.TabColor;
            tabControls.Text = Localization.Strings.TabControls;
            tabJitter.Text = Localization.Strings.TabJitter;
            tabOther.Text = Localization.Strings.TabOther;

            bttnAddBrushes.Text = Localization.Strings.AddBrushes;
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

            //Forces the window to cover the screen without being maximized.
            Rectangle workingArea = Screen.FromControl(this).WorkingArea;

            Left = workingArea.Left;
            Top = workingArea.Top;
            Width = workingArea.Width;
            Height = workingArea.Height;
        }

        /// <summary>
        /// Sets the form resize restrictions.
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            try
            {
                IUserFilesService userFilesService =
                (IUserFilesService)Services.GetService(typeof(IUserFilesService));

                // userFilesService.UserFilesPath has been reported null before, so if it is, try to guess at the path.
                string basePath = userFilesService.UserFilesPath ??
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "paint.net User Files");

                string path = Path.Combine(basePath, "BrushFactorySettings.xml");

                if (!Directory.Exists(path))
                {
                    throw new IOException();
                }

                settings = new BrushFactorySettings(path);

                // Loading the settings is split into a separate method to allow the defaults
                // to be used if an error occurs.
                settings.LoadSavedSettings();
            }
            catch (Exception ex)
            {
                if (ex is IOException || ex is UnauthorizedAccessException)
                {
                    MessageBox.Show(Localization.Strings.CannotLoadSettingsError,
                        Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    throw;
                }
            }

            InitBrushes();

            MinimumSize = new Size(835, 580);
            MaximumSize = Size;
        }

        /// <summary>
        /// Handles keypresses for global commands.
        /// </summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            //B: Switches to the brush tool.
            if (e.KeyCode == Keys.B)
            {
                SwitchTool(Tool.Brush);
            }

            //K: Switches to the color picker tool.
            if (e.KeyCode == Keys.K)
            {
                SwitchTool(Tool.ColorPicker);
            }

            //Display a hand icon while panning.
            if (e.Control)
            {
                Cursor = Cursors.Hand;
            }

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
        /// Display an arrow icon while not panning.
        /// </summary>
        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (!e.Control)
            {
                Cursor = Cursors.Default;
            }
        }

        /// <summary>
        /// Zooms in and out of the drawing region.
        /// </summary>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

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
                    isWheelZooming = true;
                    Zoom(e.Delta, true);
                }
            }
        }

        /// <summary>
        /// Recalculates the drawing region to maintain accuracy on resize.
        /// </summary>
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            if (displayCanvas != null)
            {
                displayCanvas.Left = (displayCanvasBG.Width - displayCanvas.Width) / 2;
                displayCanvas.Top = (displayCanvasBG.Height - displayCanvas.Height) / 2;
            }
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
                ClientRectangle.Height));
        }

        /// <summary>
        /// Raises the <see cref="E:System.Windows.Forms.Form.FormClosing" /> event.
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            if (brushLoadingWorker.IsBusy)
            {
                e.Cancel = true;
                if (DialogResult == DialogResult.Cancel)
                {
                    isFormClosing = true;
                    brushLoadingWorker.CancelAsync();
                }
            }

            if (!e.Cancel)
            {
                try
                {
                    settings?.SaveChangedSettings();
                }
                catch (Exception ex)
                {
                    if (ex is IOException || ex is UnauthorizedAccessException)
                    {
                        MessageBox.Show(Localization.Strings.CannotLoadSettingsError,
                            Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing">
        /// <c>true</c> to release both managed and unmanaged resources;
        /// <c>false</c> to release only unmanaged resources.
        /// </param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (loadedBrushes != null)
                {
                    loadedBrushes.Dispose();
                    loadedBrushes = null;
                }
                if (tempDir != null)
                {
                    tempDir.Dispose();
                    tempDir = null;
                }

                //Disposes all form bitmaps.
                bmpBrush?.Dispose();
                bmpBrushEffects?.Dispose();
                bmpCurrentDrawing?.Dispose();

                //Disposes all cursors.
                displayCanvas.Cursor = Cursors.Default;
                cursorColorPicker?.Dispose();
            }

            base.Dispose(disposing);
        }
        #endregion

        #region Methods (not event handlers)
        /// <summary>
        /// Applies the brush to the drawing region at the given location
        /// with the given radius. The brush is assumed square.
        /// </summary>
        /// <param name="loc">The location to apply the brush.</param>
        /// <param name="radius">The size to draw the brush at.</param>
        private void DrawBrush(Point loc, int radius)
        {
            int newRadius = Utils.Clamp(radius
                - random.Next(sliderRandMinSize.Value)
                + random.Next(sliderRandMaxSize.Value), 0, int.MaxValue);

            if (bmpBrushEffects == null)
            {
                return;
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
                int deltaX = mouseLoc.X - mouseLocPrev.X;
                int deltaY = mouseLoc.Y - mouseLocPrev.Y;
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
                int scaleFactor = (int)(newRadius * rotScaleFactor);

                //Sets the interpolation mode based on preferences.
                g.InterpolationMode = (InterpolationMode)bttnBrushSmoothing.SelectedValue;

                //Draws the brush normally if color/alpha aren't randomized.
                bool jitterRgb =
                    sliderJitterMaxRed.Value != 0 ||
                    sliderJitterMinRed.Value != 0 ||
                    sliderJitterMaxGreen.Value != 0 ||
                    sliderJitterMinGreen.Value != 0 ||
                    sliderJitterMaxBlue.Value != 0 ||
                    sliderJitterMinBlue.Value != 0;

                bool jitterHsv =
                    sliderJitterMaxHue.Value != 0 ||
                    sliderJitterMinHue.Value != 0 ||
                    sliderJitterMaxSat.Value != 0 ||
                    sliderJitterMinSat.Value != 0 ||
                    sliderJitterMaxVal.Value != 0 ||
                    sliderJitterMinVal.Value != 0;

                if ((sliderRandMinAlpha.Value == 0 && !jitterRgb && !jitterHsv) ||
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
                            (bmpCurrentDrawing.Width / 2) - (newRadius / 2),
                            (bmpCurrentDrawing.Height / 2) - (newRadius / 2));

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
                    // Sets random transparency jitter.
                    float newAlpha = Utils.ClampF((100 - random.Next(sliderRandMinAlpha.Value)) / 100f, 0, 1);
                    float newRed = 0f;
                    float newGreen = 0f;
                    float newBlue = 0f;

                    RgbColor colorRgb = new RgbColor(bttnBrushColor.BackColor.R, bttnBrushColor.BackColor.G, bttnBrushColor.BackColor.B);

                    //Sets RGB color jitter.
                    if (jitterRgb)
                    {
                        newBlue = Utils.ClampF((bttnBrushColor.BackColor.B / 2.55f
                            - random.Next(sliderJitterMinBlue.Value)
                            + random.Next(sliderJitterMaxBlue.Value)) / 100f, 0, 1);

                        newGreen = Utils.ClampF((bttnBrushColor.BackColor.G / 2.55f
                            - random.Next(sliderJitterMinGreen.Value)
                            + random.Next(sliderJitterMaxGreen.Value)) / 100f, 0, 1);

                        newRed = Utils.ClampF((bttnBrushColor.BackColor.R / 2.55f
                            - random.Next(sliderJitterMinRed.Value)
                            + random.Next(sliderJitterMaxRed.Value)) / 100f, 0, 1);

                        if (jitterHsv)
                        {
                            colorRgb = new RgbColor((int)(newRed * 255), (int)(newGreen * 255), (int)(newBlue * 255));
                        }
                    }

                    // Sets HSV color jitter.
                    if (jitterHsv)
                    {
                        HsvColor colorHsv = colorRgb.ToHsv();

                        int newHue = (int)Utils.ClampF(colorHsv.Hue
                            - random.Next((int)(sliderJitterMinHue.Value * 3.6f))
                            + random.Next((int)(sliderJitterMaxHue.Value * 3.6f)), 0, 360);

                        int newSat = (int)Utils.ClampF(colorHsv.Saturation
                            - random.Next(sliderJitterMinSat.Value)
                            + random.Next(sliderJitterMaxSat.Value), 0, 100);

                        int newVal = (int)Utils.ClampF(colorHsv.Value
                            - random.Next(sliderJitterMinVal.Value)
                            + random.Next(sliderJitterMaxVal.Value), 0, 100);
                        
                        Color finalColor = new HsvColor(newHue, newSat, newVal).ToColor();

                        newRed = finalColor.R / 255f;
                        newGreen = finalColor.G / 255f;
                        newBlue = finalColor.B / 255f;
                    }

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
                            Utils.ColorImageAttr(newRed, newGreen, newBlue, newAlpha));
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
                                Utils.ColorImageAttr(newRed, newGreen, newBlue, newAlpha));
                        }

                        //Draws the brush with radial reflections.
                        else if (bttnSymmetry.SelectedIndex > 3)
                        {
                            //Gets the center of the image.
                            Point center = new Point(
                                (bmpCurrentDrawing.Width / 2) - (newRadius / 2),
                                (bmpCurrentDrawing.Height / 2) - (newRadius / 2));

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
                                    Utils.ColorImageAttr(newRed, newGreen, newBlue, newAlpha));

                                angle += angleIncrease;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Sets the active color based on the color from the canvas at the given point.
        /// </summary>
        /// <param name="loc">The point to get the color from.</param>
        private void GetColorFromCanvas(Point loc)
        {
            UpdateBrushColor(bmpCurrentDrawing.GetPixel(loc.X, loc.Y));
        }

        /// <summary>
        /// Presents an open file dialog to the user, allowing them to select
        /// any number of brush files to load and add as custom brushes.
        /// Returns false if the user cancels or an error occurred.
        /// </summary>
        /// <param name="doAddToSettings">
        /// If true, the brush will be added to the settings.
        /// </param>
        private void ImportBrushes(bool doAddToSettings)
        {
            //Configures a dialog to get the brush(es) path(s).
            OpenFileDialog openFileDialog = new OpenFileDialog();

            string defPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            openFileDialog.InitialDirectory = defPath;
            openFileDialog.Multiselect = true;
            openFileDialog.Title = "Load custom brushes";
            openFileDialog.Filter = "Images and abr brushes|" +
                "*.png;*.bmp;*.jpg;*.gif;*.tif;*.exif*.jpeg;*.tiff;*.abr;";

            //Displays the dialog. Loads the files if it worked.
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                ImportBrushesFromFiles(openFileDialog.FileNames, doAddToSettings, true);
            }
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
        private void ImportBrushesFromFiles(
            IReadOnlyCollection<string> filePaths,
            bool doAddToSettings,
            bool doDisplayErrors)
        {
            if (!brushLoadingWorker.IsBusy)
            {
                int listViewItemHeight = GetListViewItemHeight();
                int maxBrushSize = sliderBrushSize.Maximum;

                BrushLoadingSettings workerArgs = new BrushLoadingSettings(filePaths, doAddToSettings, doDisplayErrors, listViewItemHeight, maxBrushSize);
                bttnAddBrushes.Visible = false;
                brushLoadProgressBar.Visible = true;

                brushLoadingWorker.RunWorkerAsync(workerArgs);
            }
        }

        /// <summary>
        /// Attempts to load any brush files from the specified directories and add them as custom
        /// brushes. This does not interact with the user.
        /// </summary>
        /// <param name="directories">
        /// The search directories.
        /// </param>
        private void ImportBrushesFromDirectories(IEnumerable<string> directories)
        {
            if (!brushLoadingWorker.IsBusy)
            {
                int listViewItemHeight = GetListViewItemHeight();
                int maxBrushSize = sliderBrushSize.Maximum;

                BrushLoadingSettings workerArgs = new BrushLoadingSettings(directories, listViewItemHeight, maxBrushSize);
                bttnAddBrushes.Visible = false;
                brushLoadProgressBar.Visible = true;

                brushLoadingWorker.RunWorkerAsync(workerArgs);
            }
        }

        /// <summary>
        /// Sets the brushes to be used, clearing any that already exist and
        /// removing all custom brushes as a result.
        /// </summary>
        private void InitBrushes()
        {
            if (brushLoadingWorker.IsBusy)
            {
                // Signal the background worker to abort and call this method when it completes.
                // This prevents a few crashes caused by race conditions when modifying the brush
                // collection from multiple threads.
                doReinitializeBrushes = true;
                brushLoadingWorker.CancelAsync();
                return;
            }

            bmpBrush = new Bitmap(Resources.BrCircle);

            if (loadedBrushes.Count > 0)
            {
                // Disposes and removes all of the existing items in the collection.
                bttnBrushSelector.VirtualListSize = 0;
                loadedBrushes.Clear();
            }

            if (settings.UseDefaultBrushes)
            {
                loadedBrushes.Add(new BrushSelectorItem("Line", Resources.BrLine));
                loadedBrushes.Add(new BrushSelectorItem("Tiny Dots", Resources.BrDotsTiny));
                loadedBrushes.Add(new BrushSelectorItem("Big Dots", Resources.BrDotsBig));
                loadedBrushes.Add(new BrushSelectorItem("Spark", Resources.BrSpark));
                loadedBrushes.Add(new BrushSelectorItem("Gravel", Resources.BrGravel));
                loadedBrushes.Add(new BrushSelectorItem("Rain", Resources.BrRain));
                loadedBrushes.Add(new BrushSelectorItem("Grass", Resources.BrGrass));
                loadedBrushes.Add(new BrushSelectorItem("Smoke", Resources.BrSmoke));
                loadedBrushes.Add(new BrushSelectorItem("Scales", Resources.BrScales));
                loadedBrushes.Add(new BrushSelectorItem("Dirt 4", Resources.BrFractalDirt));
                loadedBrushes.Add(new BrushSelectorItem("Dirt 3", Resources.BrDirt3));
                loadedBrushes.Add(new BrushSelectorItem("Dirt 2", Resources.BrDirt2));
                loadedBrushes.Add(new BrushSelectorItem("Dirt 1", Resources.BrDirt));
                loadedBrushes.Add(new BrushSelectorItem("Cracks", Resources.BrCracks));
                loadedBrushes.Add(new BrushSelectorItem("Spiral", Resources.BrSpiral));
                loadedBrushes.Add(new BrushSelectorItem("Segments", Resources.BrCircleSegmented));
                loadedBrushes.Add(new BrushSelectorItem("Sketchy", Resources.BrCircleSketchy));
                loadedBrushes.Add(new BrushSelectorItem("Rough", Resources.BrCircleRough));
                loadedBrushes.Add(new BrushSelectorItem("Circle 3", Resources.BrCircleHard));
                loadedBrushes.Add(new BrushSelectorItem("Circle 2", Resources.BrCircleMedium));
            }

            //Loads stored brushes.
            loadedBrushes.Add(new BrushSelectorItem("Circle 1", Resources.BrCircle));
            bttnBrushSelector.VirtualListSize = loadedBrushes.Count;

            //Loads any custom brushes.
            if (importBrushesFromToken)
            {
                importBrushesFromToken = false;

                ImportBrushesFromFiles(loadedBrushPaths, false, false);
            }
            else
            {
                ImportBrushesFromDirectories(settings.CustomBrushDirectories);
            }
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
        /// Returns a list of files in the given directories. Any invalid
        /// or non-directory path is ignored.
        /// </summary>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        private IReadOnlyCollection<string> FilesInDirectory(IEnumerable<string> dirs, BackgroundWorker backgroundWorker)
        {
            List<string> pathsToReturn = new List<string>();

            foreach (string directory in dirs)
            {
                try
                {
                    //Excludes all non-image files.
                    foreach (string str in Directory.EnumerateFiles(directory))
                    {
                        if (backgroundWorker.CancellationPending)
                        {
                            throw new OperationCanceledException();
                        }

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
                catch (Exception ex)
                {
                    if (!(ex is ArgumentException ||
                        ex is IOException ||
                        ex is SecurityException ||
                        ex is UnauthorizedAccessException))
                    {
                        throw;
                    }
                }
            }

            return pathsToReturn;
        }

        /// <summary>
        /// Returns the height of one thumbnail in the list view, which is
        /// used to compute the number on screen.
        /// </summary>
        private int GetListViewItemHeight()
        {
            if (bttnBrushSelector.VirtualListSize == 0)
            {
                // Suspend the ListView painting while the dummy item is added and removed.
                bttnBrushSelector.BeginUpdate();

                // Add and remove a dummy item to get the ListView item height.
                loadedBrushes.Add(new BrushSelectorItem("Dummy", Resources.BrCircle));
                bttnBrushSelector.VirtualListSize = 1;

                int itemHeight = bttnBrushSelector.GetItemRect(0, ItemBoundsPortion.Entire).Height;

                bttnBrushSelector.VirtualListSize = 0;
                loadedBrushes.Clear();

                bttnBrushSelector.EndUpdate();

                return itemHeight;
            }
            else
            {
                return bttnBrushSelector.GetItemRect(0, ItemBoundsPortion.Entire).Height;
            }
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
            this.bttnColorPicker = new System.Windows.Forms.Button();
            this.bttnAddBrushes = new System.Windows.Forms.Button();
            this.bttnBrushSelector = new BrushFactory.DoubleBufferedListView();
            this.dummyImageList = new System.Windows.Forms.ImageList(this.components);
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
            this.brushLoadProgressBar = new System.Windows.Forms.ProgressBar();
            this.grpbxBrushOptions = new System.Windows.Forms.GroupBox();
            this.bttnSymmetry = new System.Windows.Forms.ComboBox();
            this.chkbxLockAlpha = new System.Windows.Forms.CheckBox();
            this.chkbxOrientToMouse = new System.Windows.Forms.CheckBox();
            this.tabBar = new System.Windows.Forms.TabControl();
            this.tabColor = new System.Windows.Forms.TabPage();
            this.sliderJitterMaxVal = new System.Windows.Forms.TrackBar();
            this.sliderJitterMaxSat = new System.Windows.Forms.TrackBar();
            this.sliderJitterMaxHue = new System.Windows.Forms.TrackBar();
            this.txtJitterValue = new System.Windows.Forms.Label();
            this.sliderJitterMinVal = new System.Windows.Forms.TrackBar();
            this.txtJitterSaturation = new System.Windows.Forms.Label();
            this.sliderJitterMinSat = new System.Windows.Forms.TrackBar();
            this.txtJitterHue = new System.Windows.Forms.Label();
            this.sliderJitterMinHue = new System.Windows.Forms.TrackBar();
            this.sliderJitterMaxBlue = new System.Windows.Forms.TrackBar();
            this.sliderJitterMaxGreen = new System.Windows.Forms.TrackBar();
            this.sliderJitterMaxRed = new System.Windows.Forms.TrackBar();
            this.txtJitterBlue = new System.Windows.Forms.Label();
            this.sliderJitterMinBlue = new System.Windows.Forms.TrackBar();
            this.txtJitterGreen = new System.Windows.Forms.Label();
            this.sliderJitterMinGreen = new System.Windows.Forms.TrackBar();
            this.txtJitterRed = new System.Windows.Forms.Label();
            this.sliderJitterMinRed = new System.Windows.Forms.TrackBar();
            this.tabOther = new System.Windows.Forms.TabPage();
            this.txtBrushDensity = new System.Windows.Forms.Label();
            this.sliderBrushDensity = new System.Windows.Forms.TrackBar();
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
            this.brushLoadingWorker = new System.ComponentModel.BackgroundWorker();
            this.displayCanvasBG.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.displayCanvas)).BeginInit();
            this.tabJitter.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandVertShift)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandHorzShift)).BeginInit();
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
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMaxVal)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMaxSat)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMaxHue)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMinVal)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMinSat)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMinHue)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMaxBlue)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMaxGreen)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMaxRed)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMinBlue)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMinGreen)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMinRed)).BeginInit();
            this.tabOther.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.sliderBrushDensity)).BeginInit();
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
            this.sliderRandMaxSize.Maximum = 1000;
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
            this.sliderRandMinSize.Maximum = 1000;
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
            this.tabControls.Controls.Add(this.bttnColorPicker);
            this.tabControls.Controls.Add(this.bttnAddBrushes);
            this.tabControls.Controls.Add(this.bttnBrushSelector);
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
            this.tabControls.Controls.Add(this.brushLoadProgressBar);
            resources.ApplyResources(this.tabControls, "tabControls");
            this.tabControls.Name = "tabControls";
            // 
            // bttnColorPicker
            // 
            this.bttnColorPicker.Image = global::BrushFactory.Properties.Resources.ColorPickerIcon;
            resources.ApplyResources(this.bttnColorPicker, "bttnColorPicker");
            this.bttnColorPicker.Name = "bttnColorPicker";
            this.bttnColorPicker.UseVisualStyleBackColor = true;
            this.bttnColorPicker.Click += new System.EventHandler(this.BttnColorPicker_Click);
            this.bttnColorPicker.MouseEnter += new System.EventHandler(this.BttnColorPicker_MouseEnter);
            // 
            // bttnAddBrushes
            // 
            this.bttnAddBrushes.Image = global::BrushFactory.Properties.Resources.AddBrushIcon;
            resources.ApplyResources(this.bttnAddBrushes, "bttnAddBrushes");
            this.bttnAddBrushes.Name = "bttnAddBrushes";
            this.bttnAddBrushes.UseVisualStyleBackColor = true;
            this.bttnAddBrushes.Click += new System.EventHandler(this.BttnAddBrushes_Click);
            this.bttnAddBrushes.MouseEnter += new System.EventHandler(this.BttnAddBrushes_MouseEnter);
            // 
            // bttnBrushSelector
            // 
            this.bttnBrushSelector.HideSelection = false;
            this.bttnBrushSelector.LargeImageList = this.dummyImageList;
            resources.ApplyResources(this.bttnBrushSelector, "bttnBrushSelector");
            this.bttnBrushSelector.MultiSelect = false;
            this.bttnBrushSelector.Name = "bttnBrushSelector";
            this.bttnBrushSelector.OwnerDraw = true;
            this.bttnBrushSelector.ShowItemToolTips = true;
            this.bttnBrushSelector.UseCompatibleStateImageBehavior = false;
            this.bttnBrushSelector.VirtualMode = true;
            this.bttnBrushSelector.CacheVirtualItems += new System.Windows.Forms.CacheVirtualItemsEventHandler(this.BttnBrushSelector_CacheVirtualItems);
            this.bttnBrushSelector.DrawColumnHeader += new System.Windows.Forms.DrawListViewColumnHeaderEventHandler(this.BttnBrushSelector_DrawColumnHeader);
            this.bttnBrushSelector.DrawItem += new System.Windows.Forms.DrawListViewItemEventHandler(this.BttnBrushSelector_DrawItem);
            this.bttnBrushSelector.DrawSubItem += new System.Windows.Forms.DrawListViewSubItemEventHandler(this.BttnBrushSelector_DrawSubItem);
            this.bttnBrushSelector.RetrieveVirtualItem += new System.Windows.Forms.RetrieveVirtualItemEventHandler(this.BttnBrushSelector_RetrieveVirtualItem);
            this.bttnBrushSelector.SelectedIndexChanged += new System.EventHandler(this.BttnBrushSelector_SelectedIndexChanged);
            this.bttnBrushSelector.MouseEnter += new System.EventHandler(this.BttnBrushSelector_MouseEnter);
            // 
            // dummyImageList
            // 
            this.dummyImageList.ColorDepth = System.Windows.Forms.ColorDepth.Depth32Bit;
            resources.ApplyResources(this.dummyImageList, "dummyImageList");
            this.dummyImageList.TransparentColor = System.Drawing.Color.Transparent;
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
            this.sliderBrushAlpha.Maximum = 99;
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
            this.sliderBrushSize.Maximum = 1000;
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
            // brushLoadProgressBar
            // 
            resources.ApplyResources(this.brushLoadProgressBar, "brushLoadProgressBar");
            this.brushLoadProgressBar.Name = "brushLoadProgressBar";
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
            this.tabColor.Controls.Add(this.sliderJitterMaxVal);
            this.tabColor.Controls.Add(this.sliderJitterMaxSat);
            this.tabColor.Controls.Add(this.sliderJitterMaxHue);
            this.tabColor.Controls.Add(this.txtJitterValue);
            this.tabColor.Controls.Add(this.sliderJitterMinVal);
            this.tabColor.Controls.Add(this.txtJitterSaturation);
            this.tabColor.Controls.Add(this.sliderJitterMinSat);
            this.tabColor.Controls.Add(this.txtJitterHue);
            this.tabColor.Controls.Add(this.sliderJitterMinHue);
            this.tabColor.Controls.Add(this.sliderJitterMaxBlue);
            this.tabColor.Controls.Add(this.sliderJitterMaxGreen);
            this.tabColor.Controls.Add(this.sliderJitterMaxRed);
            this.tabColor.Controls.Add(this.txtJitterBlue);
            this.tabColor.Controls.Add(this.sliderJitterMinBlue);
            this.tabColor.Controls.Add(this.txtJitterGreen);
            this.tabColor.Controls.Add(this.sliderJitterMinGreen);
            this.tabColor.Controls.Add(this.txtJitterRed);
            this.tabColor.Controls.Add(this.sliderJitterMinRed);
            this.tabColor.Name = "tabColor";
            // 
            // sliderJitterMaxVal
            // 
            resources.ApplyResources(this.sliderJitterMaxVal, "sliderJitterMaxVal");
            this.sliderJitterMaxVal.LargeChange = 1;
            this.sliderJitterMaxVal.Maximum = 100;
            this.sliderJitterMaxVal.Name = "sliderJitterMaxVal";
            this.sliderJitterMaxVal.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderJitterMaxVal.ValueChanged += new System.EventHandler(this.sliderJitterMaxVal_ValueChanged);
            this.sliderJitterMaxVal.MouseEnter += new System.EventHandler(this.sliderJitterMaxVal_MouseEnter);
            // 
            // sliderJitterMaxSat
            // 
            resources.ApplyResources(this.sliderJitterMaxSat, "sliderJitterMaxSat");
            this.sliderJitterMaxSat.LargeChange = 1;
            this.sliderJitterMaxSat.Maximum = 100;
            this.sliderJitterMaxSat.Name = "sliderJitterMaxSat";
            this.sliderJitterMaxSat.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderJitterMaxSat.ValueChanged += new System.EventHandler(this.sliderJitterMaxSat_ValueChanged);
            this.sliderJitterMaxSat.MouseEnter += new System.EventHandler(this.sliderJitterMaxSat_MouseEnter);
            // 
            // sliderJitterMaxHue
            // 
            resources.ApplyResources(this.sliderJitterMaxHue, "sliderJitterMaxHue");
            this.sliderJitterMaxHue.LargeChange = 1;
            this.sliderJitterMaxHue.Maximum = 100;
            this.sliderJitterMaxHue.Name = "sliderJitterMaxHue";
            this.sliderJitterMaxHue.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderJitterMaxHue.ValueChanged += new System.EventHandler(this.sliderJitterMaxHue_ValueChanged);
            this.sliderJitterMaxHue.MouseEnter += new System.EventHandler(this.sliderJitterMaxHue_MouseEnter);
            // 
            // txtJitterValue
            // 
            resources.ApplyResources(this.txtJitterValue, "txtJitterValue");
            this.txtJitterValue.BackColor = System.Drawing.Color.Transparent;
            this.txtJitterValue.Name = "txtJitterValue";
            // 
            // sliderJitterMinVal
            // 
            resources.ApplyResources(this.sliderJitterMinVal, "sliderJitterMinVal");
            this.sliderJitterMinVal.LargeChange = 1;
            this.sliderJitterMinVal.Maximum = 100;
            this.sliderJitterMinVal.Name = "sliderJitterMinVal";
            this.sliderJitterMinVal.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderJitterMinVal.ValueChanged += new System.EventHandler(this.sliderJitterMinVal_ValueChanged);
            this.sliderJitterMinVal.MouseEnter += new System.EventHandler(this.sliderJitterMinVal_MouseEnter);
            // 
            // txtJitterSaturation
            // 
            resources.ApplyResources(this.txtJitterSaturation, "txtJitterSaturation");
            this.txtJitterSaturation.BackColor = System.Drawing.Color.Transparent;
            this.txtJitterSaturation.Name = "txtJitterSaturation";
            // 
            // sliderJitterMinSat
            // 
            resources.ApplyResources(this.sliderJitterMinSat, "sliderJitterMinSat");
            this.sliderJitterMinSat.LargeChange = 1;
            this.sliderJitterMinSat.Maximum = 100;
            this.sliderJitterMinSat.Name = "sliderJitterMinSat";
            this.sliderJitterMinSat.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderJitterMinSat.ValueChanged += new System.EventHandler(this.sliderJitterMinSat_ValueChanged);
            this.sliderJitterMinSat.MouseEnter += new System.EventHandler(this.sliderJitterMinSat_MouseEnter);
            // 
            // txtJitterHue
            // 
            resources.ApplyResources(this.txtJitterHue, "txtJitterHue");
            this.txtJitterHue.BackColor = System.Drawing.Color.Transparent;
            this.txtJitterHue.Name = "txtJitterHue";
            // 
            // sliderJitterMinHue
            // 
            resources.ApplyResources(this.sliderJitterMinHue, "sliderJitterMinHue");
            this.sliderJitterMinHue.LargeChange = 1;
            this.sliderJitterMinHue.Maximum = 100;
            this.sliderJitterMinHue.Name = "sliderJitterMinHue";
            this.sliderJitterMinHue.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderJitterMinHue.ValueChanged += new System.EventHandler(this.sliderJitterMinHue_ValueChanged);
            this.sliderJitterMinHue.MouseEnter += new System.EventHandler(this.sliderJitterMinHue_MouseEnter);
            // 
            // sliderJitterMaxBlue
            // 
            resources.ApplyResources(this.sliderJitterMaxBlue, "sliderJitterMaxBlue");
            this.sliderJitterMaxBlue.LargeChange = 1;
            this.sliderJitterMaxBlue.Maximum = 100;
            this.sliderJitterMaxBlue.Name = "sliderJitterMaxBlue";
            this.sliderJitterMaxBlue.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderJitterMaxBlue.ValueChanged += new System.EventHandler(this.SliderJitterMaxBlue_ValueChanged);
            this.sliderJitterMaxBlue.MouseEnter += new System.EventHandler(this.SliderJitterMaxBlue_MouseEnter);
            // 
            // sliderJitterMaxGreen
            // 
            resources.ApplyResources(this.sliderJitterMaxGreen, "sliderJitterMaxGreen");
            this.sliderJitterMaxGreen.LargeChange = 1;
            this.sliderJitterMaxGreen.Maximum = 100;
            this.sliderJitterMaxGreen.Name = "sliderJitterMaxGreen";
            this.sliderJitterMaxGreen.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderJitterMaxGreen.ValueChanged += new System.EventHandler(this.SliderJitterMaxGreen_ValueChanged);
            this.sliderJitterMaxGreen.MouseEnter += new System.EventHandler(this.SliderJitterMaxGreen_MouseEnter);
            // 
            // sliderJitterMaxRed
            // 
            resources.ApplyResources(this.sliderJitterMaxRed, "sliderJitterMaxRed");
            this.sliderJitterMaxRed.LargeChange = 1;
            this.sliderJitterMaxRed.Maximum = 100;
            this.sliderJitterMaxRed.Name = "sliderJitterMaxRed";
            this.sliderJitterMaxRed.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderJitterMaxRed.ValueChanged += new System.EventHandler(this.SliderJitterMaxRed_ValueChanged);
            this.sliderJitterMaxRed.MouseEnter += new System.EventHandler(this.SliderJitterMaxRed_MouseEnter);
            // 
            // txtJitterBlue
            // 
            resources.ApplyResources(this.txtJitterBlue, "txtJitterBlue");
            this.txtJitterBlue.BackColor = System.Drawing.Color.Transparent;
            this.txtJitterBlue.Name = "txtJitterBlue";
            // 
            // sliderJitterMinBlue
            // 
            resources.ApplyResources(this.sliderJitterMinBlue, "sliderJitterMinBlue");
            this.sliderJitterMinBlue.LargeChange = 1;
            this.sliderJitterMinBlue.Maximum = 100;
            this.sliderJitterMinBlue.Name = "sliderJitterMinBlue";
            this.sliderJitterMinBlue.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderJitterMinBlue.ValueChanged += new System.EventHandler(this.SliderJitterMinBlue_ValueChanged);
            this.sliderJitterMinBlue.MouseEnter += new System.EventHandler(this.SliderJitterMinBlue_MouseEnter);
            // 
            // txtJitterGreen
            // 
            resources.ApplyResources(this.txtJitterGreen, "txtJitterGreen");
            this.txtJitterGreen.BackColor = System.Drawing.Color.Transparent;
            this.txtJitterGreen.Name = "txtJitterGreen";
            // 
            // sliderJitterMinGreen
            // 
            resources.ApplyResources(this.sliderJitterMinGreen, "sliderJitterMinGreen");
            this.sliderJitterMinGreen.LargeChange = 1;
            this.sliderJitterMinGreen.Maximum = 100;
            this.sliderJitterMinGreen.Name = "sliderJitterMinGreen";
            this.sliderJitterMinGreen.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderJitterMinGreen.ValueChanged += new System.EventHandler(this.SliderJitterMinGreen_ValueChanged);
            this.sliderJitterMinGreen.MouseEnter += new System.EventHandler(this.SliderJitterMinGreen_MouseEnter);
            // 
            // txtJitterRed
            // 
            resources.ApplyResources(this.txtJitterRed, "txtJitterRed");
            this.txtJitterRed.BackColor = System.Drawing.Color.Transparent;
            this.txtJitterRed.Name = "txtJitterRed";
            // 
            // sliderJitterMinRed
            // 
            resources.ApplyResources(this.sliderJitterMinRed, "sliderJitterMinRed");
            this.sliderJitterMinRed.LargeChange = 1;
            this.sliderJitterMinRed.Maximum = 100;
            this.sliderJitterMinRed.Name = "sliderJitterMinRed";
            this.sliderJitterMinRed.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderJitterMinRed.ValueChanged += new System.EventHandler(this.SliderJitterMinRed_ValueChanged);
            this.sliderJitterMinRed.MouseEnter += new System.EventHandler(this.SliderJitterMinRed_MouseEnter);
            // 
            // tabOther
            // 
            this.tabOther.BackColor = System.Drawing.Color.Transparent;
            this.tabOther.Controls.Add(this.txtBrushDensity);
            this.tabOther.Controls.Add(this.sliderBrushDensity);
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
            // txtBrushDensity
            // 
            resources.ApplyResources(this.txtBrushDensity, "txtBrushDensity");
            this.txtBrushDensity.BackColor = System.Drawing.Color.Transparent;
            this.txtBrushDensity.Name = "txtBrushDensity";
            // 
            // sliderBrushDensity
            // 
            resources.ApplyResources(this.sliderBrushDensity, "sliderBrushDensity");
            this.sliderBrushDensity.LargeChange = 1;
            this.sliderBrushDensity.Maximum = 50;
            this.sliderBrushDensity.Name = "sliderBrushDensity";
            this.sliderBrushDensity.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderBrushDensity.Value = 10;
            this.sliderBrushDensity.ValueChanged += new System.EventHandler(this.SliderBrushDensity_ValueChanged);
            this.sliderBrushDensity.MouseEnter += new System.EventHandler(this.SliderBrushDensity_MouseEnter);
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
            this.sliderShiftSize.Maximum = 1000;
            this.sliderShiftSize.Minimum = -1000;
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
            // brushLoadingWorker
            // 
            this.brushLoadingWorker.WorkerReportsProgress = true;
            this.brushLoadingWorker.WorkerSupportsCancellation = true;
            this.brushLoadingWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(this.BrushLoadingWorker_DoWork);
            this.brushLoadingWorker.ProgressChanged += new System.ComponentModel.ProgressChangedEventHandler(this.BrushLoadingWorker_ProgressChanged);
            this.brushLoadingWorker.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.BrushLoadingWorker_RunWorkerCompleted);
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
            this.displayCanvasBG.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.displayCanvas)).EndInit();
            this.tabJitter.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandVertShift)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandHorzShift)).EndInit();
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
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMaxVal)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMaxSat)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMaxHue)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMinVal)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMinSat)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMinHue)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMaxBlue)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMaxGreen)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMaxRed)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMinBlue)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMinGreen)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMinRed)).EndInit();
            this.tabOther.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.sliderBrushDensity)).EndInit();
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
        /// Renders the clipboard image with the checkerboard pattern under the transparent areas.
        /// </summary>
        /// <param name="stream">The stream containing the clipboard image.</param>
        /// <exception cref="ArgumentException">The clipboard image is not a valid format.</exception>
        /// <returns>The rendered image.</returns>
        private static Bitmap RenderClipboardImageWithCheckerboard(Stream stream)
        {
            Bitmap background = null;

            using (Image clipboardImage = Image.FromStream(stream))
            {
                background = new Bitmap(clipboardImage.Width, clipboardImage.Height, PixelFormat.Format32bppPArgb);

                using (Graphics graphics = Graphics.FromImage(background))
                {
                    // Fill the entire image with the checkerboard background
                    // to ensure that transparent areas are displayed correctly.
                    using (TextureBrush backgroundBrush = new TextureBrush(Resources.CheckeredBg, WrapMode.Tile))
                    {
                        graphics.FillRectangle(backgroundBrush, 0, 0, background.Width, background.Height);
                    }

                    graphics.DrawImage(clipboardImage, 0, 0);
                }
            }

            return background;
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
            if (Clipboard.ContainsData("PNG"))
            {
                contextMenu.MenuItems.Add(new MenuItem("Use clipboard as background",
                    new EventHandler((a, b) =>
                    {
                        if (Clipboard.ContainsData("PNG"))
                        {
                            Stream stream = null;
                            try
                            {
                                stream = Clipboard.GetData("PNG") as Stream;

                                if (stream != null)
                                {
                                    displayCanvas.BackgroundImage = RenderClipboardImageWithCheckerboard(stream);
                                    displayCanvas.BackgroundImageLayout = ImageLayout.Stretch;
                                    displayCanvas.BackColor = Color.Transparent;
                                }
                            }
                            catch
                            {
                                MessageBox.Show("Could not use clipboard image.");
                            }
                            finally
                            {
                                stream?.Dispose();
                            }
                        }
                    })));
            }

            contextMenu.Show(sender, location);
        }

        /// <summary>
        /// Switches to the provided tool.
        /// </summary>
        /// <param name="toolToSwitchTo">The tool to switch to.</param>
        private void SwitchTool(Tool toolToSwitchTo)
        {
            switch (toolToSwitchTo)
            {
                case (Tool.Brush):
                    displayCanvas.Cursor = Cursors.Default;
                    break;
                case (Tool.ColorPicker):
                    // Lazy-loads the color picker cursor.
                    if (cursorColorPicker == null)
                    {
                        using (MemoryStream ms = new MemoryStream(Resources.ColorPickerCursor))
                        {
                            cursorColorPicker = new Cursor(ms);
                        }
                    }

                    displayCanvas.Cursor = cursorColorPicker;
                    Cursor.Current = cursorColorPicker;
                    break;
            }

            activeTool = toolToSwitchTo;
        }

        /// <summary>
        /// Recreates the brush with color and alpha effects applied.
        /// </summary>
        private void UpdateBrush()
        {
            //Sets the color and alpha.
            Color setColor = bttnBrushColor.BackColor;
            float multAlpha = 1 - (sliderBrushAlpha.Value / 100f);

            if (bmpBrush != null)
            {
                //Applies the color and alpha changes.
                bmpBrushEffects = Utils.FormatImage(bmpBrush, PixelFormat.Format32bppPArgb);

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
        }

        /// <summary>
        /// Updates the current brush color to the desired color.
        /// </summary>
        /// <param name="newColor">The new color to set the brush to.</param>
        private void UpdateBrushColor(Color newColor)
        {
            //Makes the text that says 'colors' almost always legible.
            Color oppositeColor = Color.FromArgb(
            (byte)(255 - newColor.R),
            (byte)(255 - newColor.G),
            (byte)(255 - newColor.B));
            bttnBrushColor.ForeColor = oppositeColor;

            //Sets the back color and updates the brushes.
            bttnBrushColor.BackColor = newColor;
            UpdateBrush();
        }

        /// <summary>
        /// Updates the brush ListView item count.
        /// </summary>
        private void UpdateListViewVirtualItemCount(int count)
        {
            if (bttnBrushSelector.InvokeRequired)
            {
                bttnBrushSelector.Invoke(new Action<int>((int value) => bttnBrushSelector.VirtualListSize = value), count);
            }
            else
            {
                bttnBrushSelector.VirtualListSize = count;
            }
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
            txtCanvasZoom.Text = string.Format(
                "{0} {1:p0}", Localization.Strings.CanvasZoom, newZoomFactor);

            //Gets the new width and height, adjusted for zooming.
            int zoomWidth = (int)(bmpCurrentDrawing.Width * newZoomFactor);
            int zoomHeight = (int)(bmpCurrentDrawing.Height * newZoomFactor);

            Point zoomingPoint = isWheelZooming
                ? mouseLoc
                : new Point(
                    displayCanvasBG.ClientSize.Width / 2 - displayCanvas.Location.X,
                    displayCanvasBG.ClientSize.Height / 2 - displayCanvas.Location.Y);

            int zoomX = displayCanvas.Location.X + zoomingPoint.X -
                zoomingPoint.X * zoomWidth / displayCanvas.Width;
            int zoomY = displayCanvas.Location.Y + zoomingPoint.Y -
                zoomingPoint.Y * zoomHeight / displayCanvas.Height;

            isWheelZooming = false;

            //Sets the new canvas position (center) and size using zoom.
            displayCanvas.Bounds = new Rectangle(
                zoomX, zoomY,
                zoomWidth, zoomHeight);
        }
        #endregion

        #region Methods (event handlers)
        private void BrushLoadingWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker backgroundWorker = (BackgroundWorker)sender;
            BrushLoadingSettings args = (BrushLoadingSettings)e.Argument;

            try
            {
                IReadOnlyCollection<string> filePaths;

                if (args.SearchDirectories != null)
                {
                    filePaths = FilesInDirectory(args.SearchDirectories, backgroundWorker);
                }
                else
                {
                    filePaths = args.FilePaths;
                }

                int brushesLoadedCount = 0;
                int brushesDetectedCount = filePaths.Count;

                int maxThumbnailHeight = args.ListViewItemHeight;
                int maxBrushSize = args.MaxBrushSize;

                //Attempts to load a bitmap from a file to use as a brush.
                foreach (string file in filePaths)
                {
                    if (backgroundWorker.CancellationPending)
                    {
                        e.Cancel = true;
                        return;
                    }

                    backgroundWorker.ReportProgress(GetProgressPercentage(brushesLoadedCount, brushesDetectedCount));
                    brushesLoadedCount++;

                    try
                    {
                        if (file.EndsWith(".abr", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                using (AbrBrushCollection brushes = AbrReader.LoadBrushes(file))
                                {
                                    string location = Path.GetFileName(file);

                                    brushesDetectedCount += brushes.Count;

                                    for (int i = 0; i < brushes.Count; i++)
                                    {
                                        if (backgroundWorker.CancellationPending)
                                        {
                                            e.Cancel = true;
                                            return;
                                        }

                                        backgroundWorker.ReportProgress(GetProgressPercentage(brushesLoadedCount, brushesDetectedCount));
                                        brushesLoadedCount++;

                                        AbrBrush item = brushes[i];

                                        // Creates the brush space.
                                        int size = Math.Max(item.Image.Width, item.Image.Height);

                                        Bitmap scaledBrush = null;

                                        if (size > maxBrushSize)
                                        {
                                            size = maxBrushSize;
                                            Size newImageSize = Utils.ComputeBrushSize(item.Image.Width, item.Image.Height, maxBrushSize);
                                            scaledBrush = Utils.ScaleImage(item.Image, newImageSize);
                                        }

                                        Bitmap brushImage = new Bitmap(size, size, PixelFormat.Format32bppPArgb);

                                        //Pads the image to be square if needed, makes fully
                                        //opaque images use intensity for alpha, and draws the
                                        //altered loaded bitmap to the brush.
                                        Utils.CopyBitmapPure(Utils.MakeBitmapSquare(
                                            Utils.MakeTransparent(scaledBrush ?? item.Image)), brushImage);

                                        if (scaledBrush != null)
                                        {
                                            scaledBrush.Dispose();
                                            scaledBrush = null;
                                        }

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
                                        while (loadedBrushes.Any(brush => brush.Name.Equals(filename, StringComparison.Ordinal)))
                                        {
                                            filename += " ";
                                        }

                                        // Add the brush to the list and generate the ListView thumbnail.

                                        loadedBrushes.Add(
                                            new BrushSelectorItem(filename, location, brushImage, tempDir.GetRandomFileName(), maxThumbnailHeight));

                                        if ((i % 2) == 0)
                                        {
                                            UpdateListViewVirtualItemCount(loadedBrushes.Count);
                                        }
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
                            Bitmap brushImage = null;

                            using (Bitmap bmp = (Bitmap)Image.FromFile(file))
                            {
                                //Creates the brush space.
                                int size = Math.Max(bmp.Width, bmp.Height);

                                Bitmap scaledBrush = null;
                                if (size > maxBrushSize)
                                {
                                    size = maxBrushSize;

                                    Size newImageSize = Utils.ComputeBrushSize(bmp.Width, bmp.Height, maxBrushSize);

                                    scaledBrush = Utils.ScaleImage(bmp, newImageSize);
                                }

                                brushImage = new Bitmap(size, size, PixelFormat.Format32bppPArgb);

                                //Pads the image to be square if needed, makes fully
                                //opaque images use intensity for alpha, and draws the
                                //altered loaded bitmap to the brush.
                                Utils.CopyBitmapPure(Utils.MakeBitmapSquare(
                                    Utils.MakeTransparent(scaledBrush ?? bmp)), brushImage);

                                if (scaledBrush != null)
                                {
                                    scaledBrush.Dispose();
                                    scaledBrush = null;
                                }
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

                            string location = Path.GetDirectoryName(file);

                            //Adds the brush without the period at the end.
                            loadedBrushes.Add(
                                new BrushSelectorItem(filename, location, brushImage, tempDir.GetRandomFileName(), maxThumbnailHeight));

                            if ((brushesLoadedCount % 2) == 0)
                            {
                                UpdateListViewVirtualItemCount(loadedBrushes.Count);
                            }
                        }

                        if (args.AddtoSettings)
                        {
                            //Adds the brush location into settings.
                            loadedBrushPaths.Add(file);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (!(ex is ArgumentException ||
                            ex is DirectoryNotFoundException ||
                            ex is FileNotFoundException))
                        {
                            throw;
                        }
                    }
                }

                e.Result = args;
            }
            catch (OperationCanceledException)
            {
                e.Cancel = true;
            }

            int GetProgressPercentage(double done, double total)
            {
                return (int)(done / total * 100.0).Clamp(0.0, 100.0);
            }
        }

        private void BrushLoadingWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            brushLoadProgressBar.Value = e.ProgressPercentage;
        }

        private void BrushLoadingWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                if (isFormClosing)
                {
                    Close();
                }
                else if (doReinitializeBrushes)
                {
                    InitBrushes();
                }
            }
            else
            {
                BrushLoadingSettings workerArgs = (BrushLoadingSettings)e.Result;

                if (e.Error != null && workerArgs.DisplayErrors)
                {
                    MessageBox.Show(this, e.Error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    bttnBrushSelector.VirtualListSize = loadedBrushes.Count;

                    if (loadedBrushes.Count > 0)
                    {
                        // Select the user's previous brush if it is present, otherwise select the last added brush.
                        int selectedItemIndex = loadedBrushes.Count - 1;

                        if (!string.IsNullOrEmpty(tokenSelectedBrushName))
                        {
                            int index = loadedBrushes.FindIndex(brush => brush.Name.Equals(tokenSelectedBrushName));

                            if (index >= 0)
                            {
                                selectedItemIndex = index;
                            }
                        }

                        bttnBrushSelector.SelectedIndices.Clear();
                        bttnBrushSelector.SelectedIndices.Add(selectedItemIndex);
                        bttnBrushSelector.EnsureVisible(selectedItemIndex);
                    }
                }

                brushLoadProgressBar.Value = 0;
                brushLoadProgressBar.Visible = false;
                bttnAddBrushes.Visible = true;
            }
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

            //Pans the image.
            else if (e.Button == MouseButtons.Middle ||
                (e.Button == MouseButtons.Left && IsKeyDown(Keys.ControlKey)))
            {
                isUserPanning = true;
                mouseLocPrev = e.Location;
            }

            //Draws with the brush.
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
                if (activeTool == Tool.Brush && !chkbxOrientToMouse.Checked)
                {
                    DrawBrush(new Point(
                        (int)(mouseLocPrev.X / displayCanvasZoom),
                        (int)(mouseLocPrev.Y / displayCanvasZoom)),
                        sliderBrushSize.Value);
                }
                else if (activeTool == Tool.ColorPicker)
                {
                    GetColorFromCanvas(new Point(
                        (int)(mouseLocPrev.X / displayCanvasZoom),
                        (int)(mouseLocPrev.Y / displayCanvasZoom)));

                    SwitchTool(Tool.Brush);
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
            }

            else if (isUserDrawing)
            {
                // Doesn't draw unless the minimum drawing distance is met.
                if (sliderMinDrawDistance.Value != 0)
                {
                    if (mouseLocBrush.HasValue)
                    {
                        int deltaX = mouseLocBrush.Value.X - mouseLoc.X;
                        int deltaY = mouseLocBrush.Value.Y - mouseLoc.Y;

                        if (Math.Sqrt(deltaX * deltaX + deltaY * deltaY) <
                            sliderMinDrawDistance.Value * displayCanvasZoom)
                        {
                            displayCanvas.Refresh();
                            return;
                        }
                    }

                    mouseLocBrush = mouseLoc;
                }

                if (activeTool == Tool.Brush)
                {
                    // Draws without speed control. Messier, but faster.
                    if (sliderBrushDensity.Value == 0)
                    {
                        DrawBrush(new Point(
                            (int)(mouseLoc.X / displayCanvasZoom),
                            (int)(mouseLoc.Y / displayCanvasZoom)),
                            sliderBrushSize.Value);

                        mouseLocPrev = e.Location;
                    }

                    // Draws at intervals of brush width between last and current mouse position,
                    // tracking remainder by changing final mouse position.
                    else
                    {
                        double deltaX = (mouseLoc.X - mouseLocPrev.X) / displayCanvasZoom;
                        double deltaY = (mouseLoc.Y - mouseLocPrev.Y) / displayCanvasZoom;
                        double brushWidthFrac = sliderBrushSize.Value / (double)sliderBrushDensity.Value;
                        double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                        double angle = Math.Atan2(deltaY, deltaX);
                        double xDist = Math.Cos(angle);
                        double yDist = Math.Sin(angle);
                        double numIntervals = distance / brushWidthFrac;

                        for (int i = 1; i <= (int)numIntervals; i++)
                        {
                            DrawBrush(new Point(
                                (int)(mouseLocPrev.X / displayCanvasZoom + xDist * brushWidthFrac * i),
                                (int)(mouseLocPrev.Y / displayCanvasZoom + yDist * brushWidthFrac * i)),
                                sliderBrushSize.Value);
                        }

                        double extraDist = brushWidthFrac * (numIntervals - (int)numIntervals);

                        // Same as mouse position except for remainder.
                        mouseLoc = new Point(
                            (int)(e.Location.X - xDist * extraDist * displayCanvasZoom),
                            (int)(e.Location.Y - yDist * extraDist * displayCanvasZoom));
                        mouseLocPrev = mouseLoc;
                    }
                }
            }

            displayCanvas.Refresh();
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
            var selection = EnvironmentParameters.GetSelectionAsPdnRegion();

            if (selection != null && selection.GetRegionReadOnly() != null)
            {
                //Calculates the outline once the selection becomes valid.
                if (selectionOutline == null)
                {
                    selectionOutline = selection.ConstructOutline(
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
                drawingArea.Exclude(selection.GetRegionReadOnly());

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
            if (activeTool == Tool.Brush && !isUserDrawing)
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
        /// Displays a dialog allowing the user to add new brushes.
        /// </summary>
        private void BttnAddBrushes_Click(object sender, EventArgs e)
        {
            ImportBrushes(true);
        }

        private void BttnAddBrushes_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.AddBrushesTip;
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
                UpdateBrushColor(dialog.Color);
            }
        }

        private void BttnBrushColor_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.BrushColorTip;
        }

        /// <summary>
        /// Handles the CacheVirtualItems event of the bttnBrushSelector control.
        /// </summary>
        private void BttnBrushSelector_CacheVirtualItems(object sender, CacheVirtualItemsEventArgs e)
        {
            // Check if the cache needs to be refreshed.
            if (visibleBrushes != null && e.StartIndex >= visibleBrushesIndex && e.EndIndex <= visibleBrushesIndex + visibleBrushes.Length)
            {
                // If the newly requested cache is a subset of the old cache,
                // no need to rebuild everything, so do nothing.
                return;
            }

            visibleBrushesIndex = e.StartIndex;
            // The indexes are inclusive.
            int length = e.EndIndex - e.StartIndex + 1;
            visibleBrushes = new ListViewItem[length];

            // Fill the cache with the appropriate ListViewItems.
            for (int i = 0; i < length; i++)
            {
                int itemIndex = visibleBrushesIndex + i;

                BrushSelectorItem brush = loadedBrushes[itemIndex];
                string name = brush.Name;
                string tooltipText;

                if (!string.IsNullOrEmpty(brush.Location))
                {
                    tooltipText = name + "\n" + brush.Location;
                }
                else
                {
                    tooltipText = name;
                }

                visibleBrushes[i] = new ListViewItem
                {
                    // When the text is an empty string it will not
                    // be included ListViewItem size calculation.
                    Text = string.Empty,
                    ImageIndex = itemIndex,
                    ToolTipText = tooltipText
                };
            }
        }

        /// <summary>
        /// Handles the DrawColumnHeader event of the bttnBrushSelector control.
        /// </summary>
        private void BttnBrushSelector_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            e.DrawDefault = true;
        }

        /// <summary>
        /// Handles the DrawItem event of the bttnBrushSelector control.
        /// </summary>
        private void BttnBrushSelector_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            BrushSelectorItem item = loadedBrushes[e.ItemIndex];

            Rectangle drawRect = new Rectangle(e.Bounds.Left, e.Bounds.Top, item.BrushWidth, item.BrushHeight);

            // The brush image is always square.
            if (item.BrushHeight > e.Bounds.Height)
            {
                drawRect.Width = item.BrushWidth * e.Bounds.Height / item.BrushHeight;
                drawRect.Height = e.Bounds.Height;
            }

            // Center the image.
            if (drawRect.Width < e.Bounds.Width)
            {
                drawRect.X = e.Bounds.X + ((e.Bounds.Width - drawRect.Width) / 2);
            }
            if (drawRect.Height < e.Bounds.Height)
            {
                drawRect.Y = e.Bounds.Y + ((e.Bounds.Height - drawRect.Height) / 2);
            }

            Bitmap thumbnail = item.Thumbnail;

            if (thumbnail == null ||
                drawRect.Width != thumbnail.Width ||
                drawRect.Height != thumbnail.Height)
            {
                item.GenerateListViewThumbnail(e.Bounds.Height, e.Item.Selected);
                thumbnail = item.Thumbnail;
            }

            if (e.Item.Selected)
            {
                e.Graphics.FillRectangle(SystemBrushes.Highlight, e.Bounds);
                e.DrawFocusRectangle();
            }
            else
            {
                e.DrawBackground();
            }

            e.Graphics.DrawImage(thumbnail, drawRect);
        }

        /// <summary>
        /// Handles the DrawSubItem event of the bttnBrushSelector control.
        /// </summary>
        private void BttnBrushSelector_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            e.DrawDefault = true;
        }

        private void BttnBrushSelector_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.BrushSelectorTip;
        }

        /// <summary>
        /// Handles the RetrieveVirtualItem event of the bttnBrushSelector control.
        /// </summary>
        private void BttnBrushSelector_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            if (visibleBrushes != null && e.ItemIndex >= visibleBrushesIndex && e.ItemIndex < visibleBrushesIndex + visibleBrushes.Length)
            {
                e.Item = visibleBrushes[e.ItemIndex - visibleBrushesIndex];
            }
            else
            {
                BrushSelectorItem brush = loadedBrushes[e.ItemIndex];
                string name = brush.Name;
                string tooltipText;

                if (!string.IsNullOrEmpty(brush.Location))
                {
                    tooltipText = name + "\n" + brush.Location;
                }
                else
                {
                    tooltipText = name;
                }

                e.Item = new ListViewItem
                {
                    // When the text is an empty string it will not
                    // be included ListViewItem size calculation.
                    Text = string.Empty,
                    ImageIndex = e.ItemIndex,
                    ToolTipText = tooltipText
                };
            }
        }

        /// <summary>
        /// Sets the brush when the user changes it with the selector.
        /// </summary>
        private void BttnBrushSelector_SelectedIndexChanged(object sender, EventArgs e)
        {
            //Gets the currently selected item.

            if (bttnBrushSelector.SelectedIndices.Count > 0)
            {
                int index = bttnBrushSelector.SelectedIndices[0];

                if (index >= 0)
                {
                    int previousItemIndex = bttnBrushSelector.PreviousItemIndex;

                    if (previousItemIndex >= 0)
                    {
                        BrushSelectorItem previousItem = loadedBrushes[previousItemIndex];
                        if (previousItem.State == BrushSelectorItemState.Memory)
                        {
                            previousItem.ToDisk();
                        }
                    }

                    BrushSelectorItem currentItem = loadedBrushes[index];

                    if (currentItem.State == BrushSelectorItemState.Disk)
                    {
                        currentItem.ToMemory();
                    }

                    bmpBrush?.Dispose();
                    bmpBrush = Utils.FormatImage(
                        currentItem.Brush,
                        PixelFormat.Format32bppPArgb);

                    UpdateBrush();
                }
            }
        }

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

        private void BttnClearSettings_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.ClearSettingsTip;
        }

        /// <summary>
        /// Switches to the color picker tool.
        /// </summary>
        private void BttnColorPicker_Click(object sender, EventArgs e)
        {
            SwitchTool(Tool.ColorPicker);
        }

        private void BttnColorPicker_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.ColorPickerTip;
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
            lock (RenderSettings.SurfaceToRender)
            {
                RenderSettings.SurfaceToRender = Surface.CopyFromBitmap(bmpCurrentDrawing);
            }

            //Updates the saved effect settings and OKs the effect.
            RenderSettings.DoApplyEffect = true;
            FinishTokenUpdate();

            this.Close();
        }

        private void BttnOk_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.OkTip;
        }

        /// <summary>
        /// Opens the preferences dialog to define persistent settings.
        /// </summary>
        private void BttnPreferences_Click(object sender, EventArgs e)
        {
            new Gui.BrushFactoryPreferences(settings).ShowDialog();
        }

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

        private void BttnUndo_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.UndoTip;
        }

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

        private void ChkbxColorizeBrush_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.ColorizeBrushTip;
        }

        private void ChkbxLockAlpha_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.LockAlphaTip;
        }

        private void ChkbxOrientToMouse_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.OrientToMouseTip;
        }

        private void SliderBrushAlpha_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.BrushAlphaTip;
        }

        private void SliderBrushAlpha_ValueChanged(object sender, EventArgs e)
        {
            txtBrushAlpha.Text = String.Format("{0} {1}",
                Localization.Strings.Alpha,
                sliderBrushAlpha.Value);

            UpdateBrush();
        }

        private void SliderBrushDensity_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.BrushDensityTip;
        }

        private void SliderBrushDensity_ValueChanged(object sender, EventArgs e)
        {
            txtBrushDensity.Text = String.Format("{0} {1}",
                Localization.Strings.BrushDensity,
                sliderBrushDensity.Value);
        }

        private void SliderBrushSize_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.BrushSizeTip;
        }

        private void SliderBrushSize_ValueChanged(object sender, EventArgs e)
        {
            txtBrushSize.Text = String.Format("{0} {1}",
                Localization.Strings.Size,
                sliderBrushSize.Value);

            //Updates to show changes in the brush indicator.
            displayCanvas.Refresh();
        }

        private void SliderBrushRotation_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.BrushRotationTip;
        }

        private void SliderBrushRotation_ValueChanged(object sender, EventArgs e)
        {
            txtBrushRotation.Text = String.Format("{0} {1}°",
                Localization.Strings.Rotation,
                sliderBrushRotation.Value);
        }

        private void SliderCanvasZoom_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.CanvasZoomTip;
        }

        private void SliderCanvasZoom_ValueChanged(object sender, EventArgs e)
        {
            Zoom(0, false);
        }

        private void SliderMinDrawDistance_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.MinDrawDistanceTip;
        }

        private void SliderMinDrawDistance_ValueChanged(object sender, EventArgs e)
        {
            txtMinDrawDistance.Text = String.Format("{0} {1}",
                Localization.Strings.MinDrawDistance,
                sliderMinDrawDistance.Value);
        }

        private void SliderRandHorzShift_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.RandHorzShiftTip;
        }

        private void SliderRandHorzShift_ValueChanged(object sender, EventArgs e)
        {
            txtRandHorzShift.Text = String.Format("{0} {1}%",
                Localization.Strings.RandHorzShift,
                sliderRandHorzShift.Value);
        }

        private void SliderJitterMaxBlue_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.JitterBlueTip;
        }

        private void SliderJitterMaxBlue_ValueChanged(object sender, EventArgs e)
        {
            txtJitterBlue.Text = String.Format("{0} -{1}%, +{2}%",
                Localization.Strings.JitterBlue,
                sliderJitterMinBlue.Value,
                sliderJitterMaxBlue.Value);
        }

        private void SliderJitterMaxGreen_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.JitterGreenTip;
        }

        private void SliderJitterMaxGreen_ValueChanged(object sender, EventArgs e)
        {
            txtJitterGreen.Text = String.Format("{0} -{1}%, +{2}%",
                Localization.Strings.JitterGreen,
                sliderJitterMinGreen.Value,
                sliderJitterMaxGreen.Value);
        }

        private void SliderJitterMaxRed_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.JitterRedTip;
        }

        private void SliderJitterMaxRed_ValueChanged(object sender, EventArgs e)
        {
            txtJitterRed.Text = String.Format("{0} -{1}%, +{2}%",
                Localization.Strings.JitterRed,
                sliderJitterMinRed.Value,
                sliderJitterMaxRed.Value);
        }

        private void SliderRandMaxSize_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.RandMaxSizeTip;
        }

        private void SliderRandMaxSize_ValueChanged(object sender, EventArgs e)
        {
            txtRandMaxSize.Text = String.Format("-{0}%, +{1}%",
                Localization.Strings.RandMaxSize,
                sliderRandMaxSize.Value);
        }

        private void SliderRandMinAlpha_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.RandMinAlphaTip;
        }

        private void SliderRandMinAlpha_ValueChanged(object sender, EventArgs e)
        {
            txtRandMinAlpha.Text = String.Format("{0} {1}",
                Localization.Strings.RandMinAlpha,
                sliderRandMinAlpha.Value);
        }

        private void SliderJitterMinBlue_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.JitterBlueTip;
        }

        private void SliderJitterMinBlue_ValueChanged(object sender, EventArgs e)
        {
            txtJitterBlue.Text = String.Format("{0} -{1}%, +{2}%",
                Localization.Strings.JitterBlue,
                sliderJitterMinBlue.Value,
                sliderJitterMaxBlue.Value);
        }

        private void SliderJitterMinGreen_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.JitterGreenTip;
        }

        private void SliderJitterMinGreen_ValueChanged(object sender, EventArgs e)
        {
            txtJitterGreen.Text = String.Format("{0} -{1}%, +{2}%",
                Localization.Strings.JitterGreen,
                sliderJitterMinGreen.Value,
                sliderJitterMaxGreen.Value);
        }

        private void sliderJitterMaxHue_ValueChanged(object sender, EventArgs e)
        {
            txtJitterHue.Text = String.Format("{0} -{1}%, +{2}%",
                Localization.Strings.JitterHue,
                sliderJitterMinHue.Value,
                sliderJitterMaxHue.Value);
        }

        private void sliderJitterMaxHue_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.JitterHueTip;
        }

        private void sliderJitterMinHue_ValueChanged(object sender, EventArgs e)
        {
            txtJitterHue.Text = String.Format("{0} -{1}%, +{2}%",
                Localization.Strings.JitterHue,
                sliderJitterMinHue.Value,
                sliderJitterMaxHue.Value);
        }

        private void sliderJitterMinHue_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.JitterHueTip;
        }

        private void SliderJitterMinRed_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.JitterRedTip;
        }

        private void SliderJitterMinRed_ValueChanged(object sender, EventArgs e)
        {
            txtJitterRed.Text = String.Format("{0} -{1}%, +{2}%",
                Localization.Strings.JitterRed,
                sliderJitterMinRed.Value,
                sliderJitterMaxRed.Value);
        }

        private void sliderJitterMaxSat_ValueChanged(object sender, EventArgs e)
        {
            txtJitterSaturation.Text = String.Format("{0} -{1}%, +{2}%",
                Localization.Strings.JitterSaturation,
                sliderJitterMinSat.Value,
                sliderJitterMaxSat.Value);
        }

        private void sliderJitterMaxSat_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.JitterSaturationTip;
        }

        private void sliderJitterMinSat_ValueChanged(object sender, EventArgs e)
        {
            txtJitterSaturation.Text = String.Format("{0} -{1}%, +{2}%",
                Localization.Strings.JitterSaturation,
                sliderJitterMinSat.Value,
                sliderJitterMaxSat.Value);
        }

        private void sliderJitterMinSat_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.JitterSaturationTip;
        }

        private void sliderJitterMaxVal_ValueChanged(object sender, EventArgs e)
        {
            txtJitterValue.Text = String.Format("{0} -{1}%, +{2}%",
                Localization.Strings.JitterValue,
                sliderJitterMinVal.Value,
                sliderJitterMaxVal.Value);
        }

        private void sliderJitterMaxVal_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.JitterValueTip;
        }

        private void sliderJitterMinVal_ValueChanged(object sender, EventArgs e)
        {
            txtJitterValue.Text = String.Format("{0} -{1}%, +{2}%",
                Localization.Strings.JitterValue,
                sliderJitterMinVal.Value,
                sliderJitterMaxVal.Value);
        }

        private void sliderJitterMinVal_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.JitterValueTip;
        }

        private void SliderRandMinSize_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.RandMinSizeTip;
        }

        private void SliderRandMinSize_ValueChanged(object sender, EventArgs e)
        {
            txtRandMinSize.Text = String.Format("{0} {1}",
                Localization.Strings.RandMinSize,
                sliderRandMinSize.Value);
        }

        private void SliderRandRotLeft_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.RandRotLeftTip;
        }

        private void SliderRandRotLeft_ValueChanged(object sender, EventArgs e)
        {
            txtRandRotLeft.Text = String.Format("{0} {1}°",
                Localization.Strings.RandRotLeft,
                sliderRandRotLeft.Value);
        }

        private void SliderRandRotRight_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.RandRotRightTip;
        }

        private void SliderRandRotRight_ValueChanged(object sender, EventArgs e)
        {
            txtRandRotRight.Text = String.Format("{0} {1}°",
                Localization.Strings.RandRotRight,
                sliderRandRotRight.Value);
        }

        private void SliderRandVertShift_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.RandVertShiftTip;
        }

        private void SliderRandVertShift_ValueChanged(object sender, EventArgs e)
        {
            txtRandVertShift.Text = String.Format("{0} {1}%",
                Localization.Strings.RandVertShift,
                sliderRandVertShift.Value);
        }

        private void SliderShiftAlpha_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.ShiftAlphaTip;
        }

        private void SliderShiftAlpha_ValueChanged(object sender, EventArgs e)
        {
            txtShiftAlpha.Text = String.Format("{0} {1}",
                Localization.Strings.ShiftAlpha,
                sliderShiftAlpha.Value);
        }

        private void SliderShiftRotation_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.ShiftRotationTip;
        }

        private void SliderShiftRotation_ValueChanged(object sender, EventArgs e)
        {
            txtShiftRotation.Text = String.Format("{0} {1}°",
                Localization.Strings.ShiftRotation,
                sliderShiftRotation.Value);
        }

        private void SliderShiftSize_MouseEnter(object sender, EventArgs e)
        {
            txtTooltip.Text = Localization.Strings.ShiftSizeTip;
        }

        private void SliderShiftSize_ValueChanged(object sender, EventArgs e)
        {
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
            if (displayCanvasBG.IsDisposed)
            {
                return;
            }

            // Converts the mouse coordinates on the screen relative to the
            // background such that the top-left corner is (0, 0) up to its
            // width and height.
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