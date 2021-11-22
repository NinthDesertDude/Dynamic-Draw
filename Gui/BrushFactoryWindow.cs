using BrushFactory.Abr;
using BrushFactory.Gui;
using BrushFactory.Interop;
using BrushFactory.Localization;
using BrushFactory.Logic;
using BrushFactory.Properties;
using BrushFactory.TabletSupport;
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
        private Tool lastTool = Tool.Brush;
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
        /// Loads user's custom brush images asynchronously.
        /// </summary>
        private BackgroundWorker brushImageLoadingWorker;

        /// <summary>
        /// Stores the disposable data for the color picker cursor.
        /// </summary>
        private Cursor cursorColorPicker;

        /// <summary>
        /// The position and size of the canvas.
        /// </summary>
        private (int x, int y, int width, int height) canvas;

        /// <summary>
        /// A multiplier for zoom percent, e.g. 2 = 200% (zoomed in), 0.5 = 50% (zoomed out).
        /// </summary>
        private float canvasZoom = 1;

        /// <summary>
        /// The rotation of the canvas in degrees.
        /// </summary>
        private float canvasRotation = 0;

        /// <summary>
        /// Whether the brush loading worker should reload brushes after cancelation.
        /// </summary>
        private bool doReinitializeBrushImages;

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
        private BrushSelectorItemCollection loadedBrushImages;

        /// <summary>
        /// Stores the user's custom brush images by file and path until it can
        /// be copied to persistent settings, or ignored.
        /// </summary>
        private readonly HashSet<string> loadedBrushImagePaths =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The list of registered keyboard shortcuts.
        /// </summary>
        private readonly HashSet<KeyboardShortcut> keyboardShortcuts;

        /// <summary>
        /// The name identifying the currently loaded brush, or null if no saved brush is currently active.
        /// </summary>
        private string currentBrushName = null;

        /// <summary>
        /// The calculated minimum draw distance including factors such as pressure sensitivity.
        /// This is specially tracked as a top-level variable since it's set in the MouseMove
        /// event and must be read in the Paint event.
        /// </summary>
        private int finalMinDrawDistance = 0;

        /// <summary>
        /// Stores the current mouse location.
        /// </summary>
        private PointF mouseLoc = new PointF();

        /// <summary>
        /// Stores the mouse location at the last place a brush stroke was
        /// successfully applied. Used exclusively by minimum draw distance.
        /// </summary>
        private PointF? mouseLocBrush;

        /// <summary>
        /// Stores the previous mouse location.
        /// </summary>
        private PointF mouseLocPrev = new PointF();

        /// <summary>
        /// All user settings including custom brushes / brush image locations and the previous brush settings from
        /// the last time the effect was ran.
        /// </summary>
        private BrushFactorySettings settings;

        /// <summary>
        /// The outline of the user's selection.
        /// </summary>
        private PdnRegion selectionOutline;

        /// <summary>
        /// Contains the list of all interpolation options for applying brush
        /// strokes.
        /// </summary>
        readonly BindingList<InterpolationItem> smoothingMethods;

        /// <summary>
        /// Contains the list of all symmetry options for using brush strokes.
        /// </summary>
        readonly BindingList<Tuple<string, SymmetryMode>> symmetryOptions;

        readonly List<PointF> symmetryOrigins;

        /// <summary>
        /// The location to draw symmetry on the transformed canvas (transformations are already applied to this point).
        /// </summary>
        PointF symmetryOrigin = PointF.Empty;

        /// <summary>
        /// The tablet service instance used to detect and work with tablets.
        /// </summary>
        private readonly TabletService tabletService;

        /// <summary>
        /// The pressure ratio as a value from 0 to 1, where 0 is no pressure at all and 1 is max measurable.
        /// </summary>
        private float tabletPressureRatio;

        /// <summary>
        /// The folder used to store undo/redo images, and deleted on exit.
        /// </summary>
        private TempDirectory tempDir;

        /// <summary>
        /// The selected brush name from the effect token.
        /// </summary>
        private string tokenSelectedBrushImageName;

        private readonly Random random = new Random();

        /// <summary>
        /// List of temporary file names to load to perform redo.
        /// </summary>
        private readonly Stack<string> redoHistory = new Stack<string>();

        /// <summary>
        /// List of temporary file names to load to perform undo.
        /// </summary>
        private readonly Stack<string> undoHistory = new Stack<string>();

        /// <summary>
        /// A list of all visible items in the brush selector for thumbnails.
        /// </summary>
        private ListViewItem[] visibleBrushImages;

        /// <summary>
        /// The starting index in the brush selector cache.
        /// </summary>
        private int visibleBrushImagesIndex;
        #endregion

        #region Fields (Gui)

        /// <summary>
        /// An empty image list that allows the brush thumbnail size to be changed.
        /// </summary>
        private ImageList dummyImageList;
        private Button bttnCancel;
        private Button bttnOk;
        private Button bttnRedo;
        private Button bttnUndo;

        private IContainer components;
        internal PictureBox displayCanvas;

        /// <summary>
        /// Tracks when the user draws out-of-bounds and moves the canvas to
        /// accomodate them.
        /// </summary>
        private Timer timerRepositionUpdate;
        private FlowLayoutPanel panelUndoRedoOkCancel;
        private Button bttnColorPicker;
        private Panel panelAllSettingsContainer;
        private Panel panelDockSettingsContainer;
        private Button bttnToolBrush;
        private Button bttnToolOrigin;
        private Button BttnToolEraser;
        private FlowLayoutPanel flowLayoutPanel1;
        private FlowLayoutPanel panelSettingsContainer;
        private Accordion bttnBrushControls;
        private FlowLayoutPanel panelBrush;
        private Label txtCanvasZoom;
        private TrackBar sliderCanvasZoom;
        private DoubleBufferedListView listviewBrushImagePicker;
        private Panel panelBrushAddPickColor;
        private CheckBox chkbxColorizeBrush;
        private Button bttnAddBrushImages;
        private ProgressBar brushImageLoadProgressBar;
        private Button bttnBrushColor;
        private Label txtBrushAlpha;
        private TrackBar sliderBrushAlpha;
        private Label txtBrushRotation;
        private TrackBar sliderBrushRotation;
        private Label txtBrushSize;
        private TrackBar sliderBrushSize;
        private Accordion bttnSpecialSettings;
        private FlowLayoutPanel panelSpecialSettings;
        private Label txtMinDrawDistance;
        private TrackBar sliderMinDrawDistance;
        private Label txtBrushDensity;
        private TrackBar sliderBrushDensity;
        private CheckBox chkbxAutomaticBrushDensity;
        private ComboBox cmbxSymmetry;
        private ComboBox cmbxBrushSmoothing;
        private CheckBox chkbxOrientToMouse;
        private CheckBox chkbxLockAlpha;
        private Accordion bttnJitterBasicsControls;
        private FlowLayoutPanel panelJitterBasics;
        private Label txtRandMinSize;
        private TrackBar sliderRandMinSize;
        private Label txtRandMaxSize;
        private TrackBar sliderRandMaxSize;
        private Label txtRandRotLeft;
        private TrackBar sliderRandRotLeft;
        private Label txtRandRotRight;
        private TrackBar sliderRandRotRight;
        private Label txtRandMinAlpha;
        private TrackBar sliderRandMinAlpha;
        private Label txtRandHorzShift;
        private TrackBar sliderRandHorzShift;
        private Label txtRandVertShift;
        private TrackBar sliderRandVertShift;
        private Accordion bttnJitterColorControls;
        private FlowLayoutPanel panelJitterColor;
        private Label txtJitterRed;
        private TrackBar sliderJitterMinRed;
        private TrackBar sliderJitterMaxRed;
        private Label txtJitterGreen;
        private TrackBar sliderJitterMinGreen;
        private TrackBar sliderJitterMaxGreen;
        private Label txtJitterBlue;
        private TrackBar sliderJitterMinBlue;
        private TrackBar sliderJitterMaxBlue;
        private Label txtJitterHue;
        private TrackBar sliderJitterMinHue;
        private TrackBar sliderJitterMaxHue;
        private Label txtJitterSaturation;
        private TrackBar sliderJitterMinSat;
        private TrackBar sliderJitterMaxSat;
        private Label txtJitterValue;
        private TrackBar sliderJitterMinVal;
        private TrackBar sliderJitterMaxVal;
        private Accordion bttnShiftBasicsControls;
        private FlowLayoutPanel panelShiftBasics;
        private Label txtShiftSize;
        private TrackBar sliderShiftSize;
        private Label txtShiftRotation;
        private TrackBar sliderShiftRotation;
        private Label txtShiftAlpha;
        private TrackBar sliderShiftAlpha;
        private Accordion bttnTabAssignPressureControls;
        private FlowLayoutPanel panelTabletAssignPressure;
        private FlowLayoutPanel panelTabPressureBrushAlpha;
        private Panel panel3;
        private Label txtTabPressureBrushAlpha;
        private NumericUpDown spinTabPressureBrushAlpha;
        private CmbxTabletValueType cmbxTabPressureBrushAlpha;
        private FlowLayoutPanel panelTabPressureBrushSize;
        private Panel panel8;
        private Label txtTabPressureBrushSize;
        private NumericUpDown spinTabPressureBrushSize;
        private CmbxTabletValueType cmbxTabPressureBrushSize;
        private FlowLayoutPanel panelTabPressureBrushRotation;
        private Panel panel2;
        private Label txtTabPressureBrushRotation;
        private NumericUpDown spinTabPressureBrushRotation;
        private CmbxTabletValueType cmbxTabPressureBrushRotation;
        private FlowLayoutPanel panelTabPressureMinDrawDistance;
        private Panel panel1;
        private Label lblTabPressureMinDrawDistance;
        private NumericUpDown spinTabPressureMinDrawDistance;
        private CmbxTabletValueType cmbxTabPressureMinDrawDistance;
        private FlowLayoutPanel panelTabPressureBrushDensity;
        private Panel panel4;
        private Label lblTabPressureBrushDensity;
        private NumericUpDown spinTabPressureBrushDensity;
        private CmbxTabletValueType cmbxTabPressureBrushDensity;
        private FlowLayoutPanel panelTabPressureRandMinSize;
        private Panel panel5;
        private Label lblTabPressureRandMinSize;
        private NumericUpDown spinTabPressureRandMinSize;
        private CmbxTabletValueType cmbxTabPressureRandMinSize;
        private FlowLayoutPanel panelTabPressureRandMaxSize;
        private Panel panel6;
        private Label lblTabPressureRandMaxSize;
        private NumericUpDown spinTabPressureRandMaxSize;
        private CmbxTabletValueType cmbxTabPressureRandMaxSize;
        private FlowLayoutPanel panelTabPressureRandRotLeft;
        private Panel panel7;
        private Label lblTabPressureRandRotLeft;
        private NumericUpDown spinTabPressureRandRotLeft;
        private CmbxTabletValueType cmbxTabPressureRandRotLeft;
        private FlowLayoutPanel panelTabPressureRandRotRight;
        private Panel panel9;
        private Label lblTabPressureRandRotRight;
        private NumericUpDown spinTabPressureRandRotRight;
        private CmbxTabletValueType cmbxTabPressureRandRotRight;
        private FlowLayoutPanel panelTabPressureRandMinAlpha;
        private Panel panel10;
        private Label lblTabPressureRandMinAlpha;
        private NumericUpDown spinTabPressureRandMinAlpha;
        private CmbxTabletValueType cmbxTabPressureRandMinAlpha;
        private FlowLayoutPanel panelTabPressureRandHorShift;
        private Panel panel12;
        private Label lblTabPressureRandHorShift;
        private NumericUpDown spinTabPressureRandHorShift;
        private CmbxTabletValueType cmbxTabPressureRandHorShift;
        private FlowLayoutPanel panelTabPressureRandVerShift;
        private Panel panel11;
        private Label lblTabPressureRandVerShift;
        private NumericUpDown spinTabPressureRandVerShift;
        private CmbxTabletValueType cmbxTabPressureRandVerShift;
        private FlowLayoutPanel panelTabPressureRedJitter;
        private Panel panel13;
        private NumericUpDown spinTabPressureMinRedJitter;
        private Label lblTabPressureRedJitter;
        private NumericUpDown spinTabPressureMaxRedJitter;
        private CmbxTabletValueType cmbxTabPressureRedJitter;
        private FlowLayoutPanel panelTabPressureGreenJitter;
        private Panel panel14;
        private NumericUpDown spinTabPressureMinGreenJitter;
        private Label lblTabPressureGreenJitter;
        private NumericUpDown spinTabPressureMaxGreenJitter;
        private CmbxTabletValueType cmbxTabPressureGreenJitter;
        private FlowLayoutPanel panelTabPressureBlueJitter;
        private Panel panel15;
        private NumericUpDown spinTabPressureMinBlueJitter;
        private Label lblTabPressureBlueJitter;
        private NumericUpDown spinTabPressureMaxBlueJitter;
        private CmbxTabletValueType cmbxTabPressureBlueJitter;
        private FlowLayoutPanel panelTabPressureHueJitter;
        private Panel panel16;
        private NumericUpDown spinTabPressureMinHueJitter;
        private Label lblTabPressureHueJitter;
        private NumericUpDown spinTabPressureMaxHueJitter;
        private CmbxTabletValueType cmbxTabPressureHueJitter;
        private FlowLayoutPanel panelTabPressureSatJitter;
        private Panel panel17;
        private NumericUpDown spinTabPressureMinSatJitter;
        private Label lblTabPressureSatJitter;
        private NumericUpDown spinTabPressureMaxSatJitter;
        private CmbxTabletValueType cmbxTabPressureSatJitter;
        private FlowLayoutPanel panelTabPressureValueJitter;
        private Panel panel18;
        private NumericUpDown spinTabPressureMinValueJitter;
        private Label lblTabPressureValueJitter;
        private NumericUpDown spinTabPressureMaxValueJitter;
        private CmbxTabletValueType cmbxTabPressureValueJitter;
        private Accordion bttnSettings;
        private FlowLayoutPanel panelSettings;
        private Button bttnCustomBrushImageLocations;
        private Button bttnClearBrushImages;
        private Button bttnClearSettings;
        private ListView listviewBrushPicker;
        private Button bttnSaveBrush;
        private Button bttnDeleteBrush;
        private Label txtTooltip;
        #endregion

        #region Constructors
        /// <summary>
        /// Initializes components and brushes.
        /// </summary>
        public WinBrushFactory()
        {
            InitializeComponent();

            // Handles the application directory.
            TempDirectory.CleanupPreviousDirectories();
            tempDir = new TempDirectory();

            loadedBrushImages = new BrushSelectorItemCollection();
            keyboardShortcuts = new HashSet<KeyboardShortcut>();

            canvas = new(0, 0, 0, 0);

            //Configures items for the smoothing method combobox.
            smoothingMethods = new BindingList<InterpolationItem>
            {
                new InterpolationItem(Strings.SmoothingNormal, CmbxSmoothing.Smoothing.Normal),
                new InterpolationItem(Strings.SmoothingHigh, CmbxSmoothing.Smoothing.High),
                new InterpolationItem(Strings.SmoothingJagged, CmbxSmoothing.Smoothing.Jagged)
            };
            cmbxBrushSmoothing.DataSource = smoothingMethods;
            cmbxBrushSmoothing.DisplayMember = "Name";
            cmbxBrushSmoothing.ValueMember = "Method";

            //Configures items for the symmetry options combobox.
            symmetryOrigins = new List<PointF>();
            symmetryOptions = new BindingList<Tuple<string, SymmetryMode>>
            {
                new Tuple<string, SymmetryMode>(Strings.SymmetryNone, SymmetryMode.None),
                new Tuple<string, SymmetryMode>(Strings.SymmetryHorizontal, SymmetryMode.Horizontal),
                new Tuple<string, SymmetryMode>(Strings.SymmetryVertical, SymmetryMode.Vertical),
                new Tuple<string, SymmetryMode>(Strings.SymmetryBoth, SymmetryMode.Star2),
                new Tuple<string, SymmetryMode>(Strings.SymmetrySetPoints, SymmetryMode.SetPoints),
                new Tuple<string, SymmetryMode>(Strings.Symmetry3pt, SymmetryMode.Star3),
                new Tuple<string, SymmetryMode>(Strings.Symmetry4pt, SymmetryMode.Star4),
                new Tuple<string, SymmetryMode>(Strings.Symmetry5pt, SymmetryMode.Star5),
                new Tuple<string, SymmetryMode>(Strings.Symmetry6pt, SymmetryMode.Star6),
                new Tuple<string, SymmetryMode>(Strings.Symmetry7pt, SymmetryMode.Star7),
                new Tuple<string, SymmetryMode>(Strings.Symmetry8pt, SymmetryMode.Star8),
                new Tuple<string, SymmetryMode>(Strings.Symmetry9pt, SymmetryMode.Star9),
                new Tuple<string, SymmetryMode>(Strings.Symmetry10pt, SymmetryMode.Star10),
                new Tuple<string, SymmetryMode>(Strings.Symmetry11pt, SymmetryMode.Star11),
                new Tuple<string, SymmetryMode>(Strings.Symmetry12pt, SymmetryMode.Star12)
            };
            cmbxSymmetry.DataSource = symmetryOptions;
            cmbxSymmetry.DisplayMember = "Item1";
            cmbxSymmetry.ValueMember = "Item2";

            // Prevents sliders and comboboxes from handling mouse wheel, so the user can scroll up/down normally.
            // Winforms designer doesn't recognize this event, so it immediately strips it if placed with autogen code.
            this.cmbxTabPressureBlueJitter.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxTabPressureBrushAlpha.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxTabPressureBrushDensity.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxTabPressureBrushRotation.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxTabPressureBrushSize.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxTabPressureGreenJitter.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxTabPressureHueJitter.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxTabPressureMinDrawDistance.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxTabPressureRandHorShift.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxTabPressureRandMaxSize.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxTabPressureRandMinAlpha.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxTabPressureRandMinSize.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxTabPressureRandRotLeft.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxTabPressureRandRotRight.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxTabPressureRandVerShift.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxTabPressureRedJitter.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxTabPressureSatJitter.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxTabPressureValueJitter.MouseWheel += IgnoreMouseWheelEvent;
            this.sliderCanvasZoom.MouseWheel += IgnoreMouseWheelEvent;
            this.sliderBrushAlpha.MouseWheel += IgnoreMouseWheelEvent;
            this.sliderBrushRotation.MouseWheel += IgnoreMouseWheelEvent;
            this.sliderBrushSize.MouseWheel += IgnoreMouseWheelEvent;
            this.sliderMinDrawDistance.MouseWheel += IgnoreMouseWheelEvent;
            this.sliderBrushDensity.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxSymmetry.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxBrushSmoothing.MouseWheel += IgnoreMouseWheelEvent;
            this.sliderRandMinSize.MouseWheel += IgnoreMouseWheelEvent;
            this.sliderRandMaxSize.MouseWheel += IgnoreMouseWheelEvent;
            this.sliderRandRotLeft.MouseWheel += IgnoreMouseWheelEvent;
            this.sliderRandRotRight.MouseWheel += IgnoreMouseWheelEvent;
            this.sliderRandMinAlpha.MouseWheel += IgnoreMouseWheelEvent;
            this.sliderRandHorzShift.MouseWheel += IgnoreMouseWheelEvent;
            this.sliderRandVertShift.MouseWheel += IgnoreMouseWheelEvent;
            this.sliderJitterMinRed.MouseWheel += IgnoreMouseWheelEvent;
            this.sliderJitterMaxRed.MouseWheel += IgnoreMouseWheelEvent;
            this.sliderJitterMinGreen.MouseWheel += IgnoreMouseWheelEvent;
            this.sliderJitterMaxGreen.MouseWheel += IgnoreMouseWheelEvent;
            this.sliderJitterMinBlue.MouseWheel += IgnoreMouseWheelEvent;
            this.sliderJitterMaxBlue.MouseWheel += IgnoreMouseWheelEvent;
            this.sliderJitterMinHue.MouseWheel += IgnoreMouseWheelEvent;
            this.sliderJitterMaxHue.MouseWheel += IgnoreMouseWheelEvent;
            this.sliderJitterMinSat.MouseWheel += IgnoreMouseWheelEvent;
            this.sliderJitterMaxSat.MouseWheel += IgnoreMouseWheelEvent;
            this.sliderJitterMinVal.MouseWheel += IgnoreMouseWheelEvent;
            this.sliderJitterMaxVal.MouseWheel += IgnoreMouseWheelEvent;
            this.sliderShiftSize.MouseWheel += IgnoreMouseWheelEvent;
            this.sliderShiftRotation.MouseWheel += IgnoreMouseWheelEvent;
            this.sliderShiftAlpha.MouseWheel += IgnoreMouseWheelEvent;
            this.spinTabPressureBrushAlpha.MouseWheel += IgnoreMouseWheelEvent;
            this.spinTabPressureBrushDensity.MouseWheel += IgnoreMouseWheelEvent;
            this.spinTabPressureBrushRotation.MouseWheel += IgnoreMouseWheelEvent;
            this.spinTabPressureBrushSize.MouseWheel += IgnoreMouseWheelEvent;
            this.spinTabPressureMaxBlueJitter.MouseWheel += IgnoreMouseWheelEvent;
            this.spinTabPressureMaxGreenJitter.MouseWheel += IgnoreMouseWheelEvent;
            this.spinTabPressureMaxHueJitter.MouseWheel += IgnoreMouseWheelEvent;
            this.spinTabPressureMaxRedJitter.MouseWheel += IgnoreMouseWheelEvent;
            this.spinTabPressureMaxSatJitter.MouseWheel += IgnoreMouseWheelEvent;
            this.spinTabPressureMaxValueJitter.MouseWheel += IgnoreMouseWheelEvent;
            this.spinTabPressureMinBlueJitter.MouseWheel += IgnoreMouseWheelEvent;
            this.spinTabPressureMinDrawDistance.MouseWheel += IgnoreMouseWheelEvent;
            this.spinTabPressureMinGreenJitter.MouseWheel += IgnoreMouseWheelEvent;
            this.spinTabPressureMinHueJitter.MouseWheel += IgnoreMouseWheelEvent;
            this.spinTabPressureMinRedJitter.MouseWheel += IgnoreMouseWheelEvent;
            this.spinTabPressureMinSatJitter.MouseWheel += IgnoreMouseWheelEvent;
            this.spinTabPressureMinValueJitter.MouseWheel += IgnoreMouseWheelEvent;
            this.spinTabPressureRandHorShift.MouseWheel += IgnoreMouseWheelEvent;
            this.spinTabPressureRandMaxSize.MouseWheel += IgnoreMouseWheelEvent;
            this.spinTabPressureRandMinAlpha.MouseWheel += IgnoreMouseWheelEvent;
            this.spinTabPressureRandMinSize.MouseWheel += IgnoreMouseWheelEvent;
            this.spinTabPressureRandRotLeft.MouseWheel += IgnoreMouseWheelEvent;
            this.spinTabPressureRandRotRight.MouseWheel += IgnoreMouseWheelEvent;
            this.spinTabPressureRandVerShift.MouseWheel += IgnoreMouseWheelEvent;

            // Binds the accordion buttons containing all the options.
            bttnBrushControls.UpdateAccordion(Strings.AccordionBrush, false, new Control[] { panelBrush });
            bttnSpecialSettings.UpdateAccordion(Strings.AccordionSpecialSettings, true, new Control[] { panelSpecialSettings });
            bttnJitterBasicsControls.UpdateAccordion(Strings.AccordionJitterBasics, true, new Control[] { panelJitterBasics });
            bttnJitterColorControls.UpdateAccordion(Strings.AccordionJitterColor, true, new Control[] { panelJitterColor });
            bttnShiftBasicsControls.UpdateAccordion(Strings.AccordionShiftBasics, true, new Control[] { panelShiftBasics });
            bttnSettings.UpdateAccordion(Strings.AccordionSettingsBrush, true, new Control[] { panelSettings });
            bttnTabAssignPressureControls.UpdateAccordion(Strings.AccordionTabPressureControls, true, new Control[] { panelTabletAssignPressure });

            // Instantiates and runs the tablet service.
            tabletService = TabletService.GetTabletService();
            tabletService.TabletDataReceived += TabletUpdated;
            tabletService.Start();
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

            //Loads custom brush images if possible, but skips duplicates. This
            //method is called twice by Paint.NET for some reason, so this
            //ensures there are no duplicates. Brush names are unique.
            if (token.CustomBrushLocations.Count > 0 && !token.CustomBrushLocations.SetEquals(loadedBrushImagePaths))
            {
                loadedBrushImagePaths.UnionWith(token.CustomBrushLocations);
                importBrushesFromToken = true;
            }

            keyboardShortcuts.Clear(); // Prevents duplicate shortcut handling.
            foreach (KeyboardShortcut shortcut in token.KeyboardShortcuts)
            {
                shortcut.OnInvoke = new Action(() =>
                {
                    HandleShortcut(shortcut);
                });

                keyboardShortcuts.Add(shortcut);
            }

            // Updates brush settings and the current image.
            UpdateBrush(token.CurrentBrushSettings);
            UpdateBrushImage();
        }

        /// <summary>
        /// Overwrites the settings with the dialog's current settings so they
        /// can be reused later; i.e. this saves the settings.
        /// </summary>
        protected override void InitTokenFromDialog()
        {
            var token = (PersistentSettings)EffectToken;

            int index = listviewBrushImagePicker.SelectedIndices.Count > 0
                ? listviewBrushImagePicker.SelectedIndices[0]
                : -1;

            token.CustomBrushLocations = loadedBrushImagePaths;
            token.CurrentBrushSettings.AlphaChange = sliderShiftAlpha.Value;
            token.CurrentBrushSettings.BrushAlpha = sliderBrushAlpha.Value;
            token.CurrentBrushSettings.BrushColor = bttnBrushColor.BackColor;
            token.CurrentBrushSettings.BrushDensity = sliderBrushDensity.Value;
            token.CurrentBrushSettings.AutomaticBrushDensity = chkbxAutomaticBrushDensity.Checked;
            token.CurrentBrushSettings.BrushImageName = index >= 0 ? loadedBrushImages[index].Name : Strings.DefaultBrushCircle;
            token.CurrentBrushSettings.BrushRotation = sliderBrushRotation.Value;
            token.CurrentBrushSettings.BrushSize = sliderBrushSize.Value;
            token.CurrentBrushSettings.DoColorizeBrush = chkbxColorizeBrush.Checked;
            token.CurrentBrushSettings.DoLockAlpha = chkbxLockAlpha.Checked;
            token.CurrentBrushSettings.DoRotateWithMouse = chkbxOrientToMouse.Checked;
            token.CurrentBrushSettings.MinDrawDistance = sliderMinDrawDistance.Value;
            token.CurrentBrushSettings.RandHorzShift = sliderRandHorzShift.Value;
            token.CurrentBrushSettings.RandMaxB = sliderJitterMaxBlue.Value;
            token.CurrentBrushSettings.RandMaxG = sliderJitterMaxGreen.Value;
            token.CurrentBrushSettings.RandMaxR = sliderJitterMaxRed.Value;
            token.CurrentBrushSettings.RandMaxH = sliderJitterMaxHue.Value;
            token.CurrentBrushSettings.RandMaxS = sliderJitterMaxSat.Value;
            token.CurrentBrushSettings.RandMaxV = sliderJitterMaxVal.Value;
            token.CurrentBrushSettings.RandMaxSize = sliderRandMaxSize.Value;
            token.CurrentBrushSettings.RandMinAlpha = sliderRandMinAlpha.Value;
            token.CurrentBrushSettings.RandMinB = sliderJitterMinBlue.Value;
            token.CurrentBrushSettings.RandMinG = sliderJitterMinGreen.Value;
            token.CurrentBrushSettings.RandMinR = sliderJitterMinRed.Value;
            token.CurrentBrushSettings.RandMinH = sliderJitterMinHue.Value;
            token.CurrentBrushSettings.RandMinS = sliderJitterMinSat.Value;
            token.CurrentBrushSettings.RandMinV = sliderJitterMinVal.Value;
            token.CurrentBrushSettings.RandMinSize = sliderRandMinSize.Value;
            token.CurrentBrushSettings.RandRotLeft = sliderRandRotLeft.Value;
            token.CurrentBrushSettings.RandRotRight = sliderRandRotRight.Value;
            token.CurrentBrushSettings.RandVertShift = sliderRandVertShift.Value;
            token.CurrentBrushSettings.RotChange = sliderShiftRotation.Value;
            token.CurrentBrushSettings.SizeChange = sliderShiftSize.Value;
            token.CurrentBrushSettings.Smoothing = (CmbxSmoothing.Smoothing)cmbxBrushSmoothing.SelectedIndex;
            token.CurrentBrushSettings.Symmetry = (SymmetryMode)cmbxSymmetry.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureBrushAlpha = cmbxTabPressureBrushAlpha.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureBrushDensity = cmbxTabPressureBrushDensity.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureBrushRotation = cmbxTabPressureBrushRotation.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureBrushSize = cmbxTabPressureBrushSize.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureBlueJitter = cmbxTabPressureBlueJitter.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureGreenJitter = cmbxTabPressureGreenJitter.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureHueJitter = cmbxTabPressureHueJitter.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureMinDrawDistance = cmbxTabPressureMinDrawDistance.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureRedJitter = cmbxTabPressureRedJitter.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureSatJitter = cmbxTabPressureSatJitter.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureValueJitter = cmbxTabPressureValueJitter.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureRandHorShift = cmbxTabPressureRandHorShift.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureRandMaxSize = cmbxTabPressureRandMaxSize.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureRandMinAlpha = cmbxTabPressureRandMinAlpha.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureRandMinSize = cmbxTabPressureRandMinSize.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureRandRotLeft = cmbxTabPressureRandRotLeft.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureRandRotRight = cmbxTabPressureRandRotRight.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureRandVerShift = cmbxTabPressureRandVerShift.SelectedIndex;
            token.CurrentBrushSettings.TabPressureBrushAlpha = (int)spinTabPressureBrushAlpha.Value;
            token.CurrentBrushSettings.TabPressureBrushDensity = (int)spinTabPressureBrushDensity.Value;
            token.CurrentBrushSettings.TabPressureBrushRotation = (int)spinTabPressureBrushRotation.Value;
            token.CurrentBrushSettings.TabPressureBrushSize = (int)spinTabPressureBrushSize.Value;
            token.CurrentBrushSettings.TabPressureMaxBlueJitter = (int)spinTabPressureMaxBlueJitter.Value;
            token.CurrentBrushSettings.TabPressureMaxGreenJitter = (int)spinTabPressureMaxGreenJitter.Value;
            token.CurrentBrushSettings.TabPressureMaxHueJitter = (int)spinTabPressureMaxHueJitter.Value;
            token.CurrentBrushSettings.TabPressureMaxRedJitter = (int)spinTabPressureMaxRedJitter.Value;
            token.CurrentBrushSettings.TabPressureMaxSatJitter = (int)spinTabPressureMaxSatJitter.Value;
            token.CurrentBrushSettings.TabPressureMaxValueJitter = (int)spinTabPressureMaxValueJitter.Value;
            token.CurrentBrushSettings.TabPressureMinBlueJitter = (int)spinTabPressureMinBlueJitter.Value;
            token.CurrentBrushSettings.TabPressureMinDrawDistance = (int)spinTabPressureMinDrawDistance.Value;
            token.CurrentBrushSettings.TabPressureMinGreenJitter = (int)spinTabPressureMinGreenJitter.Value;
            token.CurrentBrushSettings.TabPressureMinHueJitter = (int)spinTabPressureMinHueJitter.Value;
            token.CurrentBrushSettings.TabPressureMinRedJitter = (int)spinTabPressureMinRedJitter.Value;
            token.CurrentBrushSettings.TabPressureMinSatJitter = (int)spinTabPressureMinSatJitter.Value;
            token.CurrentBrushSettings.TabPressureMinValueJitter = (int)spinTabPressureMinValueJitter.Value;
            token.CurrentBrushSettings.TabPressureRandHorShift = (int)spinTabPressureRandHorShift.Value;
            token.CurrentBrushSettings.TabPressureRandMaxSize = (int)spinTabPressureRandMaxSize.Value;
            token.CurrentBrushSettings.TabPressureRandMinAlpha = (int)spinTabPressureRandMinAlpha.Value;
            token.CurrentBrushSettings.TabPressureRandMinSize = (int)spinTabPressureRandMinSize.Value;
            token.CurrentBrushSettings.TabPressureRandRotLeft = (int)spinTabPressureRandRotLeft.Value;
            token.CurrentBrushSettings.TabPressureRandRotRight = (int)spinTabPressureRandRotRight.Value;
            token.CurrentBrushSettings.TabPressureRandVerShift = (int)spinTabPressureRandVerShift.Value;
        }

        /// <summary>
        /// Configures the drawing area and loads text localizations.
        /// </summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            //Sets the sizes of the canvas and drawing region.
            canvas.width = EnvironmentParameters.SourceSurface.Size.Width;
            canvas.height = EnvironmentParameters.SourceSurface.Size.Height;

            bmpCurrentDrawing = Utils.CreateBitmapFromSurface(EnvironmentParameters.SourceSurface);
            symmetryOrigin = new PointF(
                EnvironmentParameters.SourceSurface.Width / 2f,
                EnvironmentParameters.SourceSurface.Height / 2f);

            //Sets the canvas dimensions.
            canvas.x = (displayCanvas.Width - canvas.width) / 2;
            canvas.y = (displayCanvas.Height - canvas.height) / 2;

            //Adds versioning information to the window title.
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            Text = EffectPlugin.StaticName + " (version " +
                version.Major + "." +
                version.Minor + ")";

            //Loads globalization texts for regional support.
            txtBrushAlpha.Text = string.Format("{0} {1}",
                Strings.Alpha, sliderBrushAlpha.Value);

            txtBrushDensity.Text = string.Format("{0} {1}",
                Strings.BrushDensity, sliderBrushDensity.Value);

            txtBrushRotation.Text = string.Format("{0} {1}°",
                Strings.Rotation, sliderBrushRotation.Value);

            txtBrushSize.Text = string.Format("{0} {1}",
                Strings.Size, sliderBrushSize.Value);

            txtCanvasZoom.Text = string.Format("{0} {1}%",
                Strings.CanvasZoom, sliderCanvasZoom.Value);

            txtMinDrawDistance.Text = string.Format("{0} {1}",
                Strings.MinDrawDistance, sliderMinDrawDistance.Value);

            txtRandHorzShift.Text = string.Format("{0} {1}%",
                Strings.RandHorzShift, sliderRandHorzShift.Value);

            txtRandMaxSize.Text = string.Format("{0} {1}",
                Strings.RandMaxSize, sliderRandMaxSize.Value);

            txtRandMinSize.Text = string.Format("{0} {1}",
                Strings.RandMinSize, sliderRandMinSize.Value);

            txtRandRotLeft.Text = string.Format("{0} {1}°",
                Strings.RandRotLeft, sliderRandRotLeft.Value);

            txtRandRotRight.Text = string.Format("{0} {1}°",
                Strings.RandRotRight, sliderRandRotRight.Value);

            txtRandMinAlpha.Text = string.Format("{0} {1}",
                Strings.RandMinAlpha, sliderRandMinAlpha.Value);

            txtJitterBlue.Text = string.Format("{0} -{1}%, +{2}%",
                Strings.JitterBlue, sliderJitterMinBlue.Value, sliderJitterMaxBlue.Value);

            txtJitterGreen.Text = string.Format("{0} -{1}%, +{2}%",
                Strings.JitterGreen, sliderJitterMinGreen.Value, sliderJitterMaxGreen.Value);

            txtJitterRed.Text = string.Format("{0} -{1}%, +{2}%",
                Strings.JitterRed, sliderJitterMinRed.Value, sliderJitterMaxRed.Value);

            txtJitterHue.Text = string.Format("{0} -{1}%, +{2}%",
                Strings.JitterHue, sliderJitterMinHue.Value, sliderJitterMaxHue.Value);

            txtJitterSaturation.Text = string.Format("{0} -{1}%, +{2}%",
                Strings.JitterSaturation, sliderJitterMinSat.Value, sliderJitterMaxSat.Value);

            txtJitterValue.Text = string.Format("{0} -{1}%, +{2}%",
                Strings.JitterValue, sliderJitterMinVal.Value, sliderJitterMaxVal.Value);

            txtRandVertShift.Text = string.Format("{0} {1}%",
                Strings.RandVertShift, sliderRandVertShift.Value);

            txtShiftAlpha.Text = string.Format("{0} {1}",
                Strings.ShiftAlpha, sliderShiftAlpha.Value);

            txtShiftRotation.Text = string.Format("{0} {1}°",
                Strings.ShiftRotation, sliderShiftRotation.Value);

            txtShiftSize.Text = string.Format("{0} {1}",
                Strings.ShiftSize, sliderShiftSize.Value);

            UpdateTooltip(string.Empty);

            bttnAddBrushImages.Text = Strings.AddBrushImages;
            bttnBrushColor.Text = Strings.BrushColor;
            bttnCancel.Text = Strings.Cancel;
            bttnClearBrushImages.Text = Strings.ClearBrushImages;
            bttnClearSettings.Text = Strings.ClearSettings;
            bttnCustomBrushImageLocations.Text = Strings.CustomBrushImageLocations;
            bttnOk.Text = Strings.Ok;
            bttnUndo.Text = Strings.Undo;
            bttnRedo.Text = Strings.Redo;

            chkbxColorizeBrush.Text = Strings.ColorizeBrush;
            chkbxLockAlpha.Text = Strings.LockAlpha;
            chkbxOrientToMouse.Text = Strings.OrientToMouse;
            chkbxAutomaticBrushDensity.Text = Strings.AutomaticBrushDensity;

            cmbxTabPressureBlueJitter.Text = Strings.JitterBlue;
            cmbxTabPressureBrushAlpha.Text = Strings.Alpha;
            cmbxTabPressureBrushDensity.Text = Strings.BrushDensity;
            cmbxTabPressureBrushRotation.Text = Strings.Rotation;
            cmbxTabPressureBrushSize.Text = Strings.Size;
            cmbxTabPressureGreenJitter.Text = Strings.JitterGreen;
            cmbxTabPressureHueJitter.Text = Strings.JitterHue;
            cmbxTabPressureMinDrawDistance.Text = Strings.MinDrawDistance;
            cmbxTabPressureRandHorShift.Text = Strings.RandHorzShift;
            cmbxTabPressureRandMaxSize.Text = Strings.RandMaxSize;
            cmbxTabPressureRandMinAlpha.Text = Strings.RandMinAlpha;
            cmbxTabPressureRandMinSize.Text = Strings.RandMinSize;
            cmbxTabPressureRandRotLeft.Text = Strings.RandRotLeft;
            cmbxTabPressureRandRotRight.Text = Strings.RandRotRight;
            cmbxTabPressureRandVerShift.Text = Strings.RandVertShift;

            bttnDeleteBrush.Text = Strings.DeleteBrush;
            bttnSaveBrush.Text = Strings.SaveNewBrush;

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

                settings = new BrushFactorySettings(path);

                if (!File.Exists(path))
                {
                    settings.Save(true);
                }

                // Loading the settings is split into a separate method to allow the defaults
                // to be used if an error occurs.
                settings.LoadSavedSettings();

                // Populates the brush picker with saved brush names, which will be used to look up the settings later.
                if (listviewBrushPicker.Items.Count == 0)
                {
                    foreach (var keyValPair in PersistentSettings.defaultBrushes)
                    {
                        listviewBrushPicker.Items.Add(new ListViewItem(keyValPair.Key));
                    }
                    foreach (var keyValPair in settings.CustomBrushes)
                    {
                        listviewBrushPicker.Items.Add(new ListViewItem(keyValPair.Key));
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is NullReferenceException)
                {
                    MessageBox.Show(Strings.SettingsUnavailableError,
                        Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else if (ex is IOException || ex is UnauthorizedAccessException)
                {
                    MessageBox.Show(Strings.CannotLoadSettingsError,
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

            KeyShortcutManager.FireShortcuts(keyboardShortcuts, e.KeyCode, e.Control, e.Shift, e.Alt);

            //Display a hand icon while panning.
            if (e.Control)
            {
                Cursor = Cursors.Hand;
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
                if (KeyShortcutManager.IsKeyDown(Keys.R))
                {
                    sliderBrushRotation.Value = Utils.Clamp(
                        sliderBrushRotation.Value + amountChange,
                        sliderBrushRotation.Minimum,
                        sliderBrushRotation.Maximum);
                }
                else if (KeyShortcutManager.IsKeyDown(Keys.A))
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
                if (KeyShortcutManager.IsKeyDown(Keys.S))
                {
                    int changeFactor;

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
                else if (KeyShortcutManager.IsKeyDown(Keys.R))
                {
                    sliderBrushRotation.Value = Utils.Clamp(
                    sliderBrushRotation.Value + Math.Sign(e.Delta) * 20,
                    sliderBrushRotation.Minimum,
                    sliderBrushRotation.Maximum);
                }

                //Ctrl + A + Wheel: Changes the brush alpha.
                else if (KeyShortcutManager.IsKeyDown(Keys.A))
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

            // Shift + Wheel: Changes the canvas rotation.
            else if (ModifierKeys == Keys.Shift)
            {
                canvasRotation += Math.Sign(e.Delta) * 10;
                while (canvasRotation >= 360) { canvasRotation -= 360; }
                while (canvasRotation <= -360) { canvasRotation += 360; }

                displayCanvas.Refresh();
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
                canvas.x = (displayCanvas.Width - canvas.width) / 2;
                canvas.y = (displayCanvas.Height - canvas.height) / 2;
            }
        }

        /// <summary>
        /// Raises the <see cref="E:System.Windows.Forms.Form.FormClosing" /> event.
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);

            if (brushImageLoadingWorker.IsBusy)
            {
                e.Cancel = true;
                if (DialogResult == DialogResult.Cancel)
                {
                    isFormClosing = true;
                    brushImageLoadingWorker.CancelAsync();
                }
            }

            if (!e.Cancel)
            {
                try
                {
                    settings.SaveChangedSettings();
                }
                catch (Exception ex)
                {
                    if (ex is NullReferenceException)
                    {
                        // settings can't be saved. Hide the error since the user already saw it.
                    }
                    else if (ex is IOException || ex is UnauthorizedAccessException)
                    {
                        MessageBox.Show(Strings.CannotLoadSettingsError,
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
                if (loadedBrushImages != null)
                {
                    loadedBrushImages.Dispose();
                    loadedBrushImages = null;
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
        private void DrawBrush(PointF loc, int radius)
        {
            if ((radius == 1 && chkbxAutomaticBrushDensity.Checked) ||
                (CmbxSmoothing.Smoothing)cmbxBrushSmoothing.SelectedValue == CmbxSmoothing.Smoothing.Jagged)
            {
                loc.X = (int)loc.X;
                loc.Y = (int)loc.Y;
            }

            int finalRandMinSize = Utils.GetStrengthMappedValue(sliderRandMinSize.Value,
                (int)spinTabPressureRandMinSize.Value,
                sliderRandMinSize.Maximum,
                tabletPressureRatio,
                ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureRandMinSize.SelectedItem).ValueMember);

            int finalRandMaxSize = Utils.GetStrengthMappedValue(sliderRandMaxSize.Value,
                (int)spinTabPressureRandMaxSize.Value,
                sliderRandMaxSize.Maximum,
                tabletPressureRatio,
                ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureRandMaxSize.SelectedItem).ValueMember);

            int newRadius = Utils.Clamp(radius
                - random.Next(finalRandMinSize)
                + random.Next(finalRandMaxSize), 0, int.MaxValue);

            UpdateBrushDensity(newRadius);

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
            else if (tabletPressureRatio > 0 && (
                (CmbxTabletValueType.CmbxEntry)cmbxTabPressureBrushAlpha.SelectedItem).ValueMember
                != CmbxTabletValueType.ValueHandlingMethod.DoNothing)
            {
                // If not changing sliderBrushAlpha already by shifting it in the if-statement above, the brush has to
                // be manually redrawn when modifying brush alpha. This is done to avoid editing sliderBrushAlpha and
                // having to use an extra variable to mitigate the cumulative effect it would cause.
                UpdateBrushImage();
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

            int finalRandHorzShift = Utils.Clamp(Utils.GetStrengthMappedValue(sliderRandHorzShift.Value,
                (int)spinTabPressureRandHorShift.Value,
                sliderRandHorzShift.Maximum,
                tabletPressureRatio,
                ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureRandHorShift.SelectedItem).ValueMember),
                0, 100);

            int finalRandVertShift = Utils.Clamp(Utils.GetStrengthMappedValue(sliderRandVertShift.Value,
                (int)spinTabPressureRandVerShift.Value,
                sliderRandVertShift.Maximum,
                tabletPressureRatio,
                ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureRandVerShift.SelectedItem).ValueMember),
                0, 100);

            //Randomly shifts the image by some percent of the canvas size,
            //horizontally and/or vertically.
            if (finalRandHorzShift != 0 ||
                finalRandVertShift != 0)
            {
                loc.X = loc.X
                    - bmpCurrentDrawing.Width * (finalRandHorzShift / 200f)
                    + bmpCurrentDrawing.Width * (random.Next(finalRandHorzShift) / 100f);

                loc.Y = loc.Y
                    - bmpCurrentDrawing.Height * (finalRandVertShift / 200f)
                    + bmpCurrentDrawing.Height * (random.Next(finalRandVertShift) / 100f);
            }

            // Calculates the final brush rotation based on all factors. Counters canvas rotation to remain unaffected.
            int finalBrushRotation = Utils.GetStrengthMappedValue(sliderBrushRotation.Value,
                (int)spinTabPressureBrushRotation.Value,
                sliderBrushRotation.Maximum,
                tabletPressureRatio,
                ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureBrushRotation.SelectedItem).ValueMember);

            int finalRandRotLeft = Utils.GetStrengthMappedValue(sliderRandRotLeft.Value,
                (int)spinTabPressureRandRotLeft.Value,
                sliderRandRotLeft.Maximum,
                tabletPressureRatio,
                ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureRandRotLeft.SelectedItem).ValueMember);

            int finalRandRotRight = Utils.GetStrengthMappedValue(sliderRandRotRight.Value,
                (int)spinTabPressureRandRotRight.Value,
                sliderRandRotRight.Maximum,
                tabletPressureRatio,
                ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureRandRotRight.SelectedItem).ValueMember);

            int rotation = finalBrushRotation
                - (finalRandRotLeft < 1 ? 0 : random.Next(finalRandRotLeft))
                + (finalRandRotRight < 1 ? 0 : random.Next(finalRandRotRight));

            if (chkbxOrientToMouse.Checked)
            {
                //Adds to the rotation according to mouse direction. Uses the
                //original rotation as an offset.
                float deltaX = mouseLoc.X - mouseLocPrev.X;
                float deltaY = mouseLoc.Y - mouseLocPrev.Y;
                rotation += (int)(Math.Atan2(deltaY, deltaX) * 180 / Math.PI);
            }

            int finalRandMinAlpha = Utils.Clamp(Utils.GetStrengthMappedValue(sliderRandMinAlpha.Value,
                (int)spinTabPressureRandMinAlpha.Value,
                sliderRandMinAlpha.Maximum,
                tabletPressureRatio,
                ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureRandMinAlpha.SelectedItem).ValueMember),
                0, 100);

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
                g.InterpolationMode = CmbxSmoothing.SmoothingToInterpolationMode[(CmbxSmoothing.Smoothing)cmbxBrushSmoothing.SelectedValue];

                float drawingOffsetX = (bmpCurrentDrawing.Width * 0.5f);
                float drawingOffsetY = (bmpCurrentDrawing.Height * 0.5f);

                g.TranslateTransform(drawingOffsetX, drawingOffsetY);
                g.RotateTransform(-canvasRotation);
                g.TranslateTransform(-drawingOffsetX, -drawingOffsetY);

                int finalJitterMaxRed = Utils.Clamp(Utils.GetStrengthMappedValue(sliderJitterMaxRed.Value,
                    (int)spinTabPressureMaxRedJitter.Value,
                    sliderJitterMaxRed.Maximum,
                    tabletPressureRatio,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureRedJitter.SelectedItem).ValueMember),
                    0, 100);

                int finalJitterMinRed = Utils.Clamp(Utils.GetStrengthMappedValue(sliderJitterMinRed.Value,
                    (int)spinTabPressureMinRedJitter.Value,
                    sliderJitterMinRed.Maximum,
                    tabletPressureRatio,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureRedJitter.SelectedItem).ValueMember),
                    0, 100);

                int finalJitterMaxGreen = Utils.Clamp(Utils.GetStrengthMappedValue(sliderJitterMaxGreen.Value,
                    (int)spinTabPressureMaxGreenJitter.Value,
                    sliderJitterMaxGreen.Maximum,
                    tabletPressureRatio,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureGreenJitter.SelectedItem).ValueMember),
                    0, 100);

                int finalJitterMinGreen = Utils.Clamp(Utils.GetStrengthMappedValue(sliderJitterMinGreen.Value,
                    (int)spinTabPressureMinGreenJitter.Value,
                    sliderJitterMinGreen.Maximum,
                    tabletPressureRatio,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureGreenJitter.SelectedItem).ValueMember),
                    0, 100);

                int finalJitterMaxBlue = Utils.Clamp(Utils.GetStrengthMappedValue(sliderJitterMaxBlue.Value,
                    (int)spinTabPressureMaxBlueJitter.Value,
                    sliderJitterMaxBlue.Maximum,
                    tabletPressureRatio,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureBlueJitter.SelectedItem).ValueMember),
                    0, 100);

                int finalJitterMinBlue = Utils.Clamp(Utils.GetStrengthMappedValue(sliderJitterMinBlue.Value,
                    (int)spinTabPressureMinBlueJitter.Value,
                    sliderJitterMinBlue.Maximum,
                    tabletPressureRatio,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureBlueJitter.SelectedItem).ValueMember),
                    0, 100);

                int finalJitterMaxHue = Utils.Clamp(Utils.GetStrengthMappedValue(sliderJitterMaxHue.Value,
                    (int)spinTabPressureMaxHueJitter.Value,
                    sliderJitterMaxHue.Maximum,
                    tabletPressureRatio,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureHueJitter.SelectedItem).ValueMember),
                    0, 100);

                int finalJitterMinHue = Utils.Clamp(Utils.GetStrengthMappedValue(sliderJitterMinHue.Value,
                    (int)spinTabPressureMinHueJitter.Value,
                    sliderJitterMinHue.Maximum,
                    tabletPressureRatio,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureHueJitter.SelectedItem).ValueMember),
                    0, 100);

                int finalJitterMaxSat = Utils.Clamp(Utils.GetStrengthMappedValue(sliderJitterMaxSat.Value,
                    (int)spinTabPressureMaxSatJitter.Value,
                    sliderJitterMaxSat.Maximum,
                    tabletPressureRatio,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureSatJitter.SelectedItem).ValueMember),
                    0, 100);

                int finalJitterMinSat = Utils.Clamp(Utils.GetStrengthMappedValue(sliderJitterMinSat.Value,
                    (int)spinTabPressureMinSatJitter.Value,
                    sliderJitterMinSat.Maximum,
                    tabletPressureRatio,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureSatJitter.SelectedItem).ValueMember),
                    0, 100);

                int finalJitterMaxVal = Utils.Clamp(Utils.GetStrengthMappedValue(sliderJitterMaxVal.Value,
                    (int)spinTabPressureMaxValueJitter.Value,
                    sliderJitterMaxVal.Maximum,
                    tabletPressureRatio,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureValueJitter.SelectedItem).ValueMember),
                    0, 100);

                int finalJitterMinVal = Utils.Clamp(Utils.GetStrengthMappedValue(sliderJitterMinVal.Value,
                    (int)spinTabPressureMinValueJitter.Value,
                    sliderJitterMinVal.Maximum,
                    tabletPressureRatio,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureValueJitter.SelectedItem).ValueMember),
                    0, 100);

                //Draws the brush normally if color/alpha aren't randomized.
                bool jitterRgb =
                    finalJitterMaxRed != 0 ||
                    finalJitterMinRed != 0 ||
                    finalJitterMaxGreen != 0 ||
                    finalJitterMinGreen != 0 ||
                    finalJitterMaxBlue != 0 ||
                    finalJitterMinBlue != 0;

                bool jitterHsv =
                    finalJitterMaxHue != 0 ||
                    finalJitterMinHue != 0 ||
                    finalJitterMaxSat != 0 ||
                    finalJitterMinSat != 0 ||
                    finalJitterMaxVal != 0 ||
                    finalJitterMinVal != 0;

                if ((finalRandMinAlpha == 0 && !jitterRgb && !jitterHsv) ||
                    !chkbxColorizeBrush.Checked)
                {
                    // Can't draw 0 dimension images.
                    if (scaleFactor == 0)
                    {
                        return;
                    }

                    //Draws the brush for normal and non-radial symmetry.
                    if (cmbxSymmetry.SelectedIndex < 5)
                    {
                        if (activeTool == Tool.Eraser)
                        {
                            using (Bitmap bmpBrushRotScaled = Utils.ScaleImage(bmpBrushRot, new Size(scaleFactor, scaleFactor)))
                            {
                                Utils.CopyErase(
                                    this.EnvironmentParameters.SourceSurface,
                                    bmpCurrentDrawing,
                                    bmpBrushRotScaled,
                                    new Point((int)(loc.X - (scaleFactor / 2f)), (int)(loc.Y - (scaleFactor / 2f))));
                            }
                        }
                        else
                        {
                            g.DrawImage(
                                bmpBrushRot,
                                loc.X - (scaleFactor / 2f),
                                loc.Y - (scaleFactor / 2f),
                                scaleFactor,
                                scaleFactor);
                        }
                    }

                    //Draws the brush horizontally reflected.
                    if (cmbxSymmetry.SelectedIndex ==
                        (int)SymmetryMode.Horizontal)
                    {
                        g.DrawImage(
                            bmpBrushRot,
                            loc.X + scaleFactor / 2f + (symmetryOrigin.X - loc.X) * 2,
                            loc.Y - (scaleFactor / 2f),
                            scaleFactor * -1,
                            scaleFactor);
                    }

                    //Draws the brush vertically reflected.
                    else if (cmbxSymmetry.SelectedIndex ==
                        (int)SymmetryMode.Vertical)
                    {
                        g.DrawImage(
                            bmpBrushRot,
                            loc.X - (scaleFactor / 2f),
                            loc.Y + scaleFactor / 2f + (symmetryOrigin.Y - loc.Y) * 2,
                            scaleFactor,
                            scaleFactor * -1);
                    }

                    //Draws the brush horizontally and vertically reflected.
                    else if (cmbxSymmetry.SelectedIndex ==
                        (int)SymmetryMode.Star2)
                    {
                        g.DrawImage(
                            bmpBrushRot,
                            loc.X + scaleFactor / 2f + (symmetryOrigin.X - loc.X) * 2,
                            loc.Y + scaleFactor / 2f + (symmetryOrigin.Y - loc.Y) * 2,
                            scaleFactor * -1,
                            scaleFactor * -1);
                    }

                    // Draws at defined offset locations.
                    else if (cmbxSymmetry.SelectedIndex ==
                        (int)SymmetryMode.SetPoints)
                    {
                        for (int i = 0; i < symmetryOrigins.Count; i++)
                        {
                            g.DrawImage(
                                bmpBrushRot,
                                loc.X + scaleFactor / 2f + symmetryOrigins[i].X,
                                loc.Y + scaleFactor / 2f + symmetryOrigins[i].Y,
                                scaleFactor * -1,
                                scaleFactor * -1);
                        }
                    }

                    //Draws the brush with radial reflections.
                    else if (cmbxSymmetry.SelectedIndex > 3)
                    {
                        // Easier not to compute on a rotated canvas.
                        g.ResetTransform();

                        //Gets the center of the image.
                        PointF origin = new PointF(
                            symmetryOrigin.X,
                            symmetryOrigin.Y);

                        //Gets the drawn location relative to center.
                        PointF rotatedLoc = TransformPoint(loc, true, true);
                        PointF locRelativeToOrigin = new PointF(
                            rotatedLoc.X - origin.X,
                            rotatedLoc.Y - origin.Y);

                        //Gets the distance from the drawing point to center.
                        var dist = Math.Sqrt(
                            Math.Pow(locRelativeToOrigin.X, 2) +
                            Math.Pow(locRelativeToOrigin.Y, 2));

                        //Gets the angle of the drawing point.
                        var angle = Math.Atan2(
                            locRelativeToOrigin.Y,
                            locRelativeToOrigin.X);

                        //Draws an N-pt radial reflection.
                        int numPoints = cmbxSymmetry.SelectedIndex - 2;
                        double angleIncrease = (2 * Math.PI) / numPoints;
                        for (int i = 0; i < numPoints; i++)
                        {
                            g.DrawImage(
                                bmpBrushRot,
                                origin.X - (scaleFactor / 2f) + (float)(dist * Math.Cos(angle)),
                                origin.Y - (scaleFactor / 2f) + (float)(dist * Math.Sin(angle)),
                                scaleFactor,
                                scaleFactor);

                            angle += angleIncrease;
                        }
                    }
                }
                else
                {
                    // Sets random transparency jitter.
                    float newAlpha = Utils.ClampF((100 - random.Next(finalRandMinAlpha)) / 100f, 0, 1);
                    float newRed = bttnBrushColor.BackColor.R / 255f;
                    float newGreen = bttnBrushColor.BackColor.G / 255f;
                    float newBlue = bttnBrushColor.BackColor.B / 255f;

                    RgbColor colorRgb = new RgbColor(bttnBrushColor.BackColor.R, bttnBrushColor.BackColor.G, bttnBrushColor.BackColor.B);

                    //Sets RGB color jitter.
                    if (jitterRgb)
                    {
                        newBlue = Utils.ClampF((bttnBrushColor.BackColor.B / 2.55f
                            - random.Next(finalJitterMinBlue)
                            + random.Next(finalJitterMaxBlue)) / 100f, 0, 1);

                        newGreen = Utils.ClampF((bttnBrushColor.BackColor.G / 2.55f
                            - random.Next(finalJitterMinGreen)
                            + random.Next(finalJitterMaxGreen)) / 100f, 0, 1);

                        newRed = Utils.ClampF((bttnBrushColor.BackColor.R / 2.55f
                            - random.Next(finalJitterMinRed)
                            + random.Next(finalJitterMaxRed)) / 100f, 0, 1);

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
                            - random.Next((int)(finalJitterMinHue * 3.6f))
                            + random.Next((int)(finalJitterMaxHue * 3.6f)), 0, 360);

                        int newSat = (int)Utils.ClampF(colorHsv.Saturation
                            - random.Next(finalJitterMinSat)
                            + random.Next(finalJitterMaxSat), 0, 100);

                        int newVal = (int)Utils.ClampF(colorHsv.Value
                            - random.Next(finalJitterMinVal)
                            + random.Next(finalJitterMaxVal), 0, 100);
                        
                        Color finalColor = new HsvColor(newHue, newSat, newVal).ToColor();

                        newRed = finalColor.R / 255f;
                        newGreen = finalColor.G / 255f;
                        newBlue = finalColor.B / 255f;
                    }

                    //Determines the positions to draw the brush at.
                    PointF[] destination = new PointF[3];
                    float xPos = loc.X - (scaleFactor / 2f);
                    float yPos = loc.Y - (scaleFactor / 2f);

                    //Draws without reflection.
                    destination[0] = new PointF(xPos, yPos);
                    destination[1] = new PointF(xPos + scaleFactor, yPos);
                    destination[2] = new PointF(xPos, yPos + scaleFactor);

                    //Draws the whole image and applies colorations and alpha.
                    if (cmbxSymmetry.SelectedIndex < 5)
                    {
                        g.DrawImage(
                            bmpBrushRot,
                            destination,
                            new Rectangle(0, 0, bmpBrushRot.Width, bmpBrushRot.Height),
                            GraphicsUnit.Pixel,
                            Utils.ColorImageAttr(newRed, newGreen, newBlue, newAlpha));
                    }

                    //Handles drawing reflections.
                    if (cmbxSymmetry.SelectedIndex !=
                        (int)SymmetryMode.None)
                    {
                        //Draws the brush horizontally reflected
                        if (cmbxSymmetry.SelectedIndex ==
                            (int)SymmetryMode.Horizontal)
                        {
                            destination[0] = new PointF(bmpCurrentDrawing.Width - xPos, yPos);
                            destination[1] = new PointF(bmpCurrentDrawing.Width - xPos - scaleFactor, yPos);
                            destination[2] = new PointF(bmpCurrentDrawing.Width - xPos, yPos + scaleFactor);
                        }

                        //Draws the brush vertically reflected.
                        else if (cmbxSymmetry.SelectedIndex ==
                            (int)SymmetryMode.Vertical)
                        {
                            destination[0] = new PointF(xPos, bmpCurrentDrawing.Height - yPos);
                            destination[1] = new PointF(xPos + scaleFactor, bmpCurrentDrawing.Height - yPos);
                            destination[2] = new PointF(xPos, bmpCurrentDrawing.Height - yPos - scaleFactor);
                        }

                        //Draws the brush horizontally and vertically reflected.
                        else if (cmbxSymmetry.SelectedIndex ==
                            (int)SymmetryMode.Star2)
                        {
                            destination[0] = new PointF(bmpCurrentDrawing.Width - xPos,
                                bmpCurrentDrawing.Height - yPos);
                            destination[1] = new PointF(bmpCurrentDrawing.Width - xPos - scaleFactor,
                                bmpCurrentDrawing.Height - yPos);
                            destination[2] = new PointF(bmpCurrentDrawing.Width - xPos,
                                bmpCurrentDrawing.Height - yPos - scaleFactor);
                        }

                        //Draws the non-radial reflection.
                        if (cmbxSymmetry.SelectedIndex < 4)
                        {
                            //Draws the whole image and applies colorations and alpha.
                            g.DrawImage(
                                bmpBrushRot,
                                destination,
                                new Rectangle(0, 0, bmpBrushRot.Width, bmpBrushRot.Height),
                                GraphicsUnit.Pixel,
                                Utils.ColorImageAttr(newRed, newGreen, newBlue, newAlpha));
                        }

                        // Draws at defined offset locations.
                        else if (cmbxSymmetry.SelectedIndex ==
                            (int)SymmetryMode.SetPoints)
                        {
                            Rectangle bmpSizeRect = new Rectangle(0, 0, bmpBrushRot.Width, bmpBrushRot.Height);
                            ImageAttributes attr = Utils.ColorImageAttr(newRed, newGreen, newBlue, newAlpha);

                            for (int i = 0; i < symmetryOrigins.Count; i++)
                            {
                                float newXPos = xPos + symmetryOrigins[i].X;
                                float newYPos = yPos + symmetryOrigins[i].Y;
                                destination[0] = new PointF(newXPos, newYPos);
                                destination[1] = new PointF(newXPos + scaleFactor, newYPos);
                                destination[2] = new PointF(newXPos, newYPos + scaleFactor);

                                g.DrawImage(
                                    bmpBrushRot,
                                    destination,
                                    bmpSizeRect,
                                    GraphicsUnit.Pixel,
                                    attr);
                            }
                        }

                        //Draws the brush with radial reflections.
                        else if (cmbxSymmetry.SelectedIndex > 3)
                        {
                            // Easier not to compute on a rotated canvas.
                            g.ResetTransform();

                            //Gets the center of the image.
                            PointF origin = new PointF(
                                symmetryOrigin.X,
                                symmetryOrigin.Y);

                            //Gets the drawn location relative to center.
                            PointF rotatedLoc = TransformPoint(loc, true, true);
                            PointF locRelativeToOrigin = new PointF(
                                rotatedLoc.X - origin.X,
                                rotatedLoc.Y - origin.Y);

                            //Gets the distance from the drawing point to center.
                            var dist = Math.Sqrt(
                                Math.Pow(locRelativeToOrigin.X, 2) +
                                Math.Pow(locRelativeToOrigin.Y, 2));

                            //Gets the angle of the drawing point.
                            var angle = Math.Atan2(
                                locRelativeToOrigin.Y,
                                locRelativeToOrigin.X);

                            //Draws an N-pt radial reflection.
                            int numPoints = cmbxSymmetry.SelectedIndex - 2;
                            double angleIncrease = (2 * Math.PI) / numPoints;
                            float halfScaleFactor = scaleFactor / 2f;
                            Rectangle bmpBrushRotBounds = new Rectangle(0, 0, bmpBrushRot.Width, bmpBrushRot.Height);

                            for (int i = 0; i < numPoints; i++)
                            {
                                float posX = (float)(origin.X - halfScaleFactor + dist * Math.Cos(angle));
                                float posY = (float)(origin.Y - halfScaleFactor + dist * Math.Sin(angle));

                                destination[0] = new PointF(posX, posY);
                                destination[1] = new PointF(posX + scaleFactor, posY);
                                destination[2] = new PointF(posX, posY + scaleFactor);

                                g.DrawImage(
                                    bmpBrushRot,
                                    destination,
                                    bmpBrushRotBounds,
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
        /// This adjusts the brush density when it's set to be automatically adjusted based on brush size.
        /// </summary>
        private void UpdateBrushDensity(int brushSize)
        {
            if (chkbxAutomaticBrushDensity.Checked)
            {
                if (brushSize == 1 || brushSize == 2) { sliderBrushDensity.Value = brushSize; }
                else if (brushSize > 2 && brushSize < 6) { sliderBrushDensity.Value = 3; }
                else if (brushSize > 5 && brushSize < 8) { sliderBrushDensity.Value = 4; }
                else if (brushSize > 7 && brushSize < 21) { sliderBrushDensity.Value = 5; }
                else if (brushSize > 20 && brushSize < 26) { sliderBrushDensity.Value = 6; }
                else if (brushSize > 25 && brushSize < 46) { sliderBrushDensity.Value = 7; }
                else if (brushSize > 45 && brushSize < 81) { sliderBrushDensity.Value = 8; }
                else if (brushSize > 80 && brushSize < 251) { sliderBrushDensity.Value = 9; }
                else { sliderBrushDensity.Value = 10; }
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
        /// Executes actions for invoked keyboard shortcuts. This is connected to shortcuts located in persistent
        /// settings from <see cref="InitTokenFromDialog"/>.
        /// </summary>
        /// <param name="shortcut">Any shortcut invoked</param>
        private void HandleShortcut(KeyboardShortcut shortcut)
        {
            switch (shortcut.Target)
            {
                case ShortcutTarget.Alpha:
                    sliderBrushAlpha.Value = 
                        shortcut.GetDataAsInt(sliderBrushAlpha.Value,
                        sliderBrushAlpha.Minimum,
                        sliderBrushAlpha.Maximum);
                    break;
                case ShortcutTarget.AlphaShift:
                    sliderBrushAlpha.Value = 
                        shortcut.GetDataAsInt(sliderBrushAlpha.Value,
                        sliderBrushAlpha.Minimum,
                        sliderBrushAlpha.Maximum);
                    break;
                case ShortcutTarget.AutomaticBrushDensity:
                    chkbxAutomaticBrushDensity.Checked = shortcut.GetDataAsBool(chkbxAutomaticBrushDensity.Checked);
                    break;
                case ShortcutTarget.BrushStrokeDensity:
                    sliderBrushDensity.Value = 
                        shortcut.GetDataAsInt(sliderBrushDensity.Value,
                        sliderBrushDensity.Minimum,
                        sliderBrushDensity.Maximum);
                    break;
                case ShortcutTarget.CanvasZoom:
                    sliderCanvasZoom.Value = 
                        shortcut.GetDataAsInt(sliderCanvasZoom.Value,
                        sliderCanvasZoom.Minimum, sliderCanvasZoom.Maximum);
                    break;
                case ShortcutTarget.Color:
                    UpdateBrushColor(shortcut.GetDataAsColor());
                    break;
                case ShortcutTarget.ColorizeBrush:
                    chkbxColorizeBrush.Checked = shortcut.GetDataAsBool(chkbxColorizeBrush.Checked);
                    UpdateBrushImage();
                    break;
                case ShortcutTarget.JitterBlueMax:
                    sliderJitterMaxBlue.Value = 
                        shortcut.GetDataAsInt(sliderJitterMaxBlue.Value,
                        sliderJitterMaxBlue.Minimum, sliderJitterMaxBlue.Maximum);
                    break;
                case ShortcutTarget.JitterBlueMin:
                    sliderJitterMinBlue.Value = 
                        shortcut.GetDataAsInt(sliderJitterMinBlue.Value,
                        sliderJitterMinBlue.Minimum, sliderJitterMinBlue.Maximum);
                    break;
                case ShortcutTarget.JitterGreenMax:
                    sliderJitterMaxGreen.Value = 
                        shortcut.GetDataAsInt(sliderJitterMaxGreen.Value,
                        sliderJitterMaxGreen.Minimum, sliderJitterMaxGreen.Maximum);
                    break;
                case ShortcutTarget.JitterGreenMin:
                    sliderJitterMinGreen.Value = 
                        shortcut.GetDataAsInt(sliderJitterMinGreen.Value,
                        sliderJitterMinGreen.Minimum, sliderJitterMinGreen.Maximum);
                    break;
                case ShortcutTarget.JitterHorSpray:
                    sliderRandHorzShift.Value = 
                        shortcut.GetDataAsInt(sliderRandHorzShift.Value,
                        sliderRandHorzShift.Minimum, sliderRandHorzShift.Maximum);
                    break;
                case ShortcutTarget.JitterHueMax:
                    sliderJitterMaxHue.Value = 
                        shortcut.GetDataAsInt(sliderJitterMaxHue.Value,
                        sliderJitterMaxHue.Minimum, sliderJitterMaxHue.Maximum);
                    break;
                case ShortcutTarget.JitterHueMin:
                    sliderJitterMinHue.Value = 
                        shortcut.GetDataAsInt(sliderJitterMinHue.Value,
                        sliderJitterMinHue.Minimum, sliderJitterMinHue.Maximum);
                    break;
                case ShortcutTarget.JitterMaxSize:
                    sliderRandMaxSize.Value = 
                        shortcut.GetDataAsInt(sliderRandMaxSize.Value,
                        sliderRandMaxSize.Minimum, sliderRandMaxSize.Maximum);
                    break;
                case ShortcutTarget.JitterMinAlpha:
                    sliderRandMinAlpha.Value = 
                        shortcut.GetDataAsInt(sliderRandMinAlpha.Value,
                        sliderRandMinAlpha.Minimum, sliderRandMinAlpha.Maximum);
                    break;
                case ShortcutTarget.JitterMinSize:
                    sliderRandMinSize.Value = 
                        shortcut.GetDataAsInt(sliderRandMinSize.Value,
                        sliderRandMinSize.Minimum, sliderRandMinSize.Maximum);
                    break;
                case ShortcutTarget.JitterRedMax:
                    sliderJitterMaxRed.Value = 
                        shortcut.GetDataAsInt(sliderJitterMaxRed.Value,
                        sliderJitterMaxRed.Minimum, sliderJitterMaxRed.Maximum);
                    break;
                case ShortcutTarget.JitterRedMin:
                    sliderJitterMinRed.Value = 
                        shortcut.GetDataAsInt(sliderJitterMinRed.Value,
                        sliderJitterMinRed.Minimum, sliderJitterMinRed.Maximum);
                    break;
                case ShortcutTarget.JitterRotLeft:
                    sliderRandRotLeft.Value = 
                        shortcut.GetDataAsInt(sliderRandRotLeft.Value,
                        sliderRandRotLeft.Minimum, sliderRandRotLeft.Maximum);
                    break;
                case ShortcutTarget.JitterRotRight:
                    sliderRandRotRight.Value = 
                        shortcut.GetDataAsInt(sliderRandRotRight.Value,
                        sliderRandRotRight.Minimum, sliderRandRotRight.Maximum);
                    break;
                case ShortcutTarget.JitterSatMax:
                    sliderJitterMaxSat.Value = 
                        shortcut.GetDataAsInt(sliderJitterMaxSat.Value,
                        sliderJitterMaxSat.Minimum, sliderJitterMaxSat.Maximum);
                    break;
                case ShortcutTarget.JitterSatMin:
                    sliderJitterMinSat.Value =
                        shortcut.GetDataAsInt(sliderJitterMinSat.Value,
                        sliderJitterMinSat.Minimum, sliderJitterMinSat.Maximum);
                    break;
                case ShortcutTarget.JitterValMax:
                    sliderJitterMaxVal.Value =
                        shortcut.GetDataAsInt(sliderJitterMaxVal.Value,
                        sliderJitterMaxVal.Minimum, sliderJitterMaxVal.Maximum);
                    break;
                case ShortcutTarget.JitterValMin:
                    sliderJitterMinVal.Value =
                        shortcut.GetDataAsInt(sliderJitterMinVal.Value,
                        sliderJitterMinVal.Minimum, sliderJitterMinVal.Maximum);
                    break;
                case ShortcutTarget.JitterVerSpray:
                    sliderRandVertShift.Value =
                        shortcut.GetDataAsInt(sliderRandVertShift.Value,
                        sliderRandVertShift.Minimum, sliderRandVertShift.Maximum);
                    break;
                case ShortcutTarget.LockAlpha:
                    chkbxLockAlpha.Checked = shortcut.GetDataAsBool(chkbxLockAlpha.Checked);
                    break;
                case ShortcutTarget.MinDrawDistance:
                    sliderMinDrawDistance.Value =
                        shortcut.GetDataAsInt(sliderMinDrawDistance.Value,
                        sliderMinDrawDistance.Minimum, sliderMinDrawDistance.Maximum);
                    break;
                case ShortcutTarget.RotateWithMouse:
                    chkbxOrientToMouse.Checked = shortcut.GetDataAsBool(chkbxOrientToMouse.Checked);
                    break;
                case ShortcutTarget.Rotation:
                    sliderBrushRotation.Value =
                        shortcut.GetDataAsInt(sliderBrushRotation.Value,
                        sliderBrushRotation.Minimum, sliderBrushRotation.Maximum);
                    break;
                case ShortcutTarget.RotShift:
                    sliderShiftRotation.Value =
                        shortcut.GetDataAsInt(sliderShiftRotation.Value,
                        sliderShiftRotation.Minimum, sliderShiftRotation.Maximum);
                    break;
                case ShortcutTarget.SelectedBrush:
                    for (int i = 0; i < listviewBrushPicker.Items.Count; i++)
                    {
                        if (shortcut.ActionData.Equals(
                            listviewBrushImagePicker.Items[i].Text, StringComparison.CurrentCultureIgnoreCase))
                        {
                            listviewBrushImagePicker.Items[i].Selected = true;
                        }
                    }
                    break;
                case ShortcutTarget.SelectedBrushImage:
                    int selectedBrushIndex = -1;
                    for (int i = 0; i < loadedBrushImages.Count; i++)
                    {
                        //
                        if (shortcut.ActionData.Equals(loadedBrushImages[i].Name, StringComparison.CurrentCultureIgnoreCase))
                        {
                            selectedBrushIndex = i;
                            break;
                        }
                    }

                    if (selectedBrushIndex == -1)
                    {
                        for (int i = 0; i < loadedBrushImages.Count; i++)
                        {
                            if (shortcut.ActionData.Equals(loadedBrushImages[i].Location, StringComparison.CurrentCultureIgnoreCase))
                            {
                                selectedBrushIndex = i;
                                break;
                            }
                        }
                    }

                    if (selectedBrushIndex != -1)
                    {
                        listviewBrushPicker.Items[selectedBrushIndex].Selected = true;
                    }
                    break;
                case ShortcutTarget.SelectedTool:
                    Tool newTool = (Tool)shortcut.GetDataAsInt((int)activeTool, 0, Enum.GetValues(typeof(Tool)).Length);
                    SwitchTool(newTool);

                    if (newTool == Tool.SetSymmetryOrigin)
                    {
                        // The origin points could be displayed relative to the center or current mouse. It's more convenient
                        // for the user to display at the mouse position so wherever they click to set a new origin point is
                        // exactly where the origin will be when they switch back to brush mode.
                        if (cmbxSymmetry.SelectedIndex == (int)SymmetryMode.SetPoints)
                        {
                            symmetryOrigin = TransformPoint(new PointF(mouseLoc.X, mouseLoc.Y), true);
                        }

                        // Invalidate to immediately update the symmetry origin guidelines drawn in symmetry mode.
                        displayCanvas.Invalidate();
                    }
                    break;
                case ShortcutTarget.Size:
                    sliderBrushSize.Value =
                        shortcut.GetDataAsInt(sliderBrushSize.Value,
                        sliderBrushSize.Minimum, sliderBrushSize.Maximum);
                    break;
                case ShortcutTarget.SizeShift:
                    sliderShiftSize.Value =
                        shortcut.GetDataAsInt(sliderShiftSize.Value,
                        sliderShiftSize.Minimum, sliderShiftSize.Maximum);
                    break;
                case ShortcutTarget.SmoothingMode:
                    cmbxBrushSmoothing.SelectedIndex =
                        shortcut.GetDataAsInt(cmbxBrushSmoothing.SelectedIndex,
                        0, cmbxBrushSmoothing.Items.Count);
                    break;
                case ShortcutTarget.SymmetryMode:
                    cmbxSymmetry.SelectedIndex = 
                        shortcut.GetDataAsInt((int)cmbxSymmetry.SelectedIndex, 0, cmbxSymmetry.Items.Count);
                    break;
                case ShortcutTarget.UndoAction:
                    BttnUndo_Click(null, null);
                    break;
                case ShortcutTarget.RedoAction:
                    BttnRedo_Click(null, null);
                    break;
            };
        }

        /// <summary>
        /// Presents an open file dialog to the user, allowing them to select
        /// any number of brush image files to load and add as custom brush images.
        /// Returns false if the user cancels or an error occurred.
        /// </summary>
        /// <param name="doAddToSettings">
        /// If true, the brush image will be added to the settings.
        /// </param>
        private void ImportBrushImages(bool doAddToSettings)
        {
            //Configures a dialog to get the brush image(s) path(s).
            OpenFileDialog openFileDialog = new OpenFileDialog();

            string defPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            openFileDialog.InitialDirectory = defPath;
            openFileDialog.Multiselect = true;
            openFileDialog.Title = Strings.CustomBrushImagesDirectoryTitle;
            openFileDialog.Filter = Strings.CustomBrushImagesDirectoryFilter +
                "|*.png;*.bmp;*.jpg;*.gif;*.tif;*.exif*.jpeg;*.tiff;*.abr;";

            //Displays the dialog. Loads the files if it worked.
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                ImportBrushImagesFromFiles(openFileDialog.FileNames, doAddToSettings, true);
            }
        }

        /// <summary>
        /// Attempts to load any number of brush image files and add them as custom
        /// brush images. This does not interact with the user.
        /// </summary>
        /// <param name="fileAndPath">
        /// If empty, the user will be presented with a dialog to select
        /// files.
        /// </param>
        /// <param name="doAddToSettings">
        /// If true, the brush image will be added to the settings.
        /// </param>
        /// <param name="displayError">
        /// Errors should only be displayed if it's a user-initiated action.
        /// </param>
        private void ImportBrushImagesFromFiles(
            IReadOnlyCollection<string> filePaths,
            bool doAddToSettings,
            bool doDisplayErrors)
        {
            if (!brushImageLoadingWorker.IsBusy)
            {
                int listViewItemHeight = GetListViewItemHeight();
                int maxBrushSize = sliderBrushSize.Maximum;

                BrushImageLoadingSettings workerArgs = new BrushImageLoadingSettings(filePaths, doAddToSettings, doDisplayErrors, listViewItemHeight, maxBrushSize);
                bttnAddBrushImages.Visible = false;
                brushImageLoadProgressBar.Visible = true;

                brushImageLoadingWorker.RunWorkerAsync(workerArgs);
            }
        }

        /// <summary>
        /// Attempts to load any brush image files from the specified directories and add them as custom
        /// brush images. This does not interact with the user.
        /// </summary>
        /// <param name="directories">
        /// The search directories.
        /// </param>
        private void ImportBrushImagesFromDirectories(IEnumerable<string> directories)
        {
            if (!brushImageLoadingWorker.IsBusy)
            {
                int listViewItemHeight = GetListViewItemHeight();
                int maxBrushSize = sliderBrushSize.Maximum;

                BrushImageLoadingSettings workerArgs = new BrushImageLoadingSettings(directories, listViewItemHeight, maxBrushSize);
                bttnAddBrushImages.Visible = false;
                brushImageLoadProgressBar.Visible = true;

                brushImageLoadingWorker.RunWorkerAsync(workerArgs);
            }
        }

        /// <summary>
        /// Sets the brushes to be used, clearing any that already exist and
        /// removing all custom brush images as a result.
        /// </summary>
        private void InitBrushes()
        {
            if (brushImageLoadingWorker.IsBusy)
            {
                // Signal the background worker to abort and call this method when it completes.
                // This prevents a few crashes caused by race conditions when modifying the brush
                // collection from multiple threads.
                doReinitializeBrushImages = true;
                brushImageLoadingWorker.CancelAsync();
                return;
            }

            bmpBrush = new Bitmap(Resources.BrCircle);

            if (loadedBrushImages.Count > 0)
            {
                // Disposes and removes all of the existing items in the collection.
                listviewBrushImagePicker.VirtualListSize = 0;
                loadedBrushImages.Clear();
            }

            if (settings?.UseDefaultBrushes ?? true)
            {
                loadedBrushImages.Add(new BrushSelectorItem(Strings.DefaultBrushLine, Resources.BrLine));
                loadedBrushImages.Add(new BrushSelectorItem(Strings.DefaultBrushSmallDots, Resources.BrDotsTiny));
                loadedBrushImages.Add(new BrushSelectorItem(Strings.DefaultBrushBigDots, Resources.BrDotsBig));
                loadedBrushImages.Add(new BrushSelectorItem(Strings.DefaultBrushSpark, Resources.BrSpark));
                loadedBrushImages.Add(new BrushSelectorItem(Strings.DefaultBrushGravel, Resources.BrGravel));
                loadedBrushImages.Add(new BrushSelectorItem(Strings.DefaultBrushRain, Resources.BrRain));
                loadedBrushImages.Add(new BrushSelectorItem(Strings.DefaultBrushGrass, Resources.BrGrass));
                loadedBrushImages.Add(new BrushSelectorItem(Strings.DefaultBrushSmoke, Resources.BrSmoke));
                loadedBrushImages.Add(new BrushSelectorItem(Strings.DefaultBrushScales, Resources.BrScales));
                loadedBrushImages.Add(new BrushSelectorItem(Strings.DefaultBrushFractalDirt, Resources.BrFractalDirt));
                loadedBrushImages.Add(new BrushSelectorItem(Strings.DefaultBrushDirt3, Resources.BrDirt3));
                loadedBrushImages.Add(new BrushSelectorItem(Strings.DefaultBrushDirt2, Resources.BrDirt2));
                loadedBrushImages.Add(new BrushSelectorItem(Strings.DefaultBrushDirt, Resources.BrDirt));
                loadedBrushImages.Add(new BrushSelectorItem(Strings.DefaultBrushCracks, Resources.BrCracks));
                loadedBrushImages.Add(new BrushSelectorItem(Strings.DefaultBrushSpiral, Resources.BrSpiral));
                loadedBrushImages.Add(new BrushSelectorItem(Strings.DefaultBrushCircleSegmented, Resources.BrCircleSegmented));
                loadedBrushImages.Add(new BrushSelectorItem(Strings.DefaultBrushCircleSketchy, Resources.BrCircleSketchy));
                loadedBrushImages.Add(new BrushSelectorItem(Strings.DefaultBrushCircleRough, Resources.BrCircleRough));
                loadedBrushImages.Add(new BrushSelectorItem(Strings.DefaultBrushCircleHard, Resources.BrCircleHard));
                loadedBrushImages.Add(new BrushSelectorItem(Strings.DefaultBrushCircleMed, Resources.BrCircleMedium));
            }

            //Loads stored brush images.
            loadedBrushImages.Add(new BrushSelectorItem(Strings.DefaultBrushCircle, Resources.BrCircle));
            listviewBrushImagePicker.VirtualListSize = loadedBrushImages.Count;

            //Loads any custom brush images.
            if (importBrushesFromToken)
            {
                importBrushesFromToken = false;

                ImportBrushImagesFromFiles(loadedBrushImagePaths, false, false);
            }
            else
            {
                ImportBrushImagesFromDirectories(settings?.CustomBrushImageDirectories ?? new HashSet<string>());
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
        private static IReadOnlyCollection<string> FilesInDirectory(IEnumerable<string> dirs, BackgroundWorker backgroundWorker)
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
            if (listviewBrushImagePicker.VirtualListSize == 0)
            {
                // Suspend the ListView painting while the dummy item is added and removed.
                listviewBrushImagePicker.BeginUpdate();

                // Add and remove a dummy item to get the ListView item height.
                loadedBrushImages.Add(new BrushSelectorItem("Dummy", Resources.BrCircle));
                listviewBrushImagePicker.VirtualListSize = 1;

                int itemHeight = listviewBrushImagePicker.GetItemRect(0, ItemBoundsPortion.Entire).Height;

                listviewBrushImagePicker.VirtualListSize = 0;
                loadedBrushImages.Clear();

                listviewBrushImagePicker.EndUpdate();

                return itemHeight;
            }
            else
            {
                return listviewBrushImagePicker.GetItemRect(0, ItemBoundsPortion.Entire).Height;
            }
        }

        /// <summary>
        /// Returns the amount of space between the display canvas and
        /// the display canvas background.
        /// </summary>
        private Rectangle GetRange()
        {
            //Gets the full region.
            Rectangle range = new Rectangle(canvas.x, canvas.y, canvas.width, canvas.height);

            //Calculates width.
            if (canvas.width >= displayCanvas.ClientRectangle.Width)
            {
                range.X = displayCanvas.ClientRectangle.Width - canvas.width;
                range.Width = canvas.width - displayCanvas.ClientRectangle.Width;
            }
            else
            {
                range.X = (displayCanvas.ClientRectangle.Width - canvas.width) / 2;
                range.Width = 0;
            }

            //Calculates height.
            if (canvas.height >= displayCanvas.ClientRectangle.Height)
            {
                range.Y = displayCanvas.ClientRectangle.Height - canvas.height;
                range.Height = canvas.height - displayCanvas.ClientRectangle.Height;
            }
            else
            {
                range.Y = (displayCanvas.ClientRectangle.Height - canvas.height) / 2;
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
            this.displayCanvas = new System.Windows.Forms.PictureBox();
            this.bttnToolBrush = new System.Windows.Forms.Button();
            this.dummyImageList = new System.Windows.Forms.ImageList(this.components);
            this.panelUndoRedoOkCancel = new System.Windows.Forms.FlowLayoutPanel();
            this.bttnUndo = new System.Windows.Forms.Button();
            this.bttnRedo = new System.Windows.Forms.Button();
            this.bttnOk = new System.Windows.Forms.Button();
            this.bttnCancel = new System.Windows.Forms.Button();
            this.brushImageLoadingWorker = new System.ComponentModel.BackgroundWorker();
            this.bttnColorPicker = new System.Windows.Forms.Button();
            this.panelAllSettingsContainer = new System.Windows.Forms.Panel();
            this.panelDockSettingsContainer = new System.Windows.Forms.Panel();
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.BttnToolEraser = new System.Windows.Forms.Button();
            this.bttnToolOrigin = new System.Windows.Forms.Button();
            this.panelSettingsContainer = new System.Windows.Forms.FlowLayoutPanel();
            this.bttnBrushControls = new BrushFactory.Gui.Accordion();
            this.panelBrush = new System.Windows.Forms.FlowLayoutPanel();
            this.txtCanvasZoom = new System.Windows.Forms.Label();
            this.sliderCanvasZoom = new System.Windows.Forms.TrackBar();
            this.listviewBrushPicker = new System.Windows.Forms.ListView();
            this.listviewBrushImagePicker = new BrushFactory.DoubleBufferedListView();
            this.panelBrushAddPickColor = new System.Windows.Forms.Panel();
            this.chkbxColorizeBrush = new System.Windows.Forms.CheckBox();
            this.bttnAddBrushImages = new System.Windows.Forms.Button();
            this.brushImageLoadProgressBar = new System.Windows.Forms.ProgressBar();
            this.bttnBrushColor = new System.Windows.Forms.Button();
            this.txtBrushAlpha = new System.Windows.Forms.Label();
            this.sliderBrushAlpha = new System.Windows.Forms.TrackBar();
            this.txtBrushRotation = new System.Windows.Forms.Label();
            this.sliderBrushRotation = new System.Windows.Forms.TrackBar();
            this.txtBrushSize = new System.Windows.Forms.Label();
            this.sliderBrushSize = new System.Windows.Forms.TrackBar();
            this.bttnSpecialSettings = new BrushFactory.Gui.Accordion();
            this.panelSpecialSettings = new System.Windows.Forms.FlowLayoutPanel();
            this.txtMinDrawDistance = new System.Windows.Forms.Label();
            this.sliderMinDrawDistance = new System.Windows.Forms.TrackBar();
            this.txtBrushDensity = new System.Windows.Forms.Label();
            this.sliderBrushDensity = new System.Windows.Forms.TrackBar();
            this.cmbxSymmetry = new System.Windows.Forms.ComboBox();
            this.cmbxBrushSmoothing = new System.Windows.Forms.ComboBox();
            this.chkbxOrientToMouse = new System.Windows.Forms.CheckBox();
            this.chkbxLockAlpha = new System.Windows.Forms.CheckBox();
            this.bttnJitterBasicsControls = new BrushFactory.Gui.Accordion();
            this.panelJitterBasics = new System.Windows.Forms.FlowLayoutPanel();
            this.txtRandMinSize = new System.Windows.Forms.Label();
            this.sliderRandMinSize = new System.Windows.Forms.TrackBar();
            this.txtRandMaxSize = new System.Windows.Forms.Label();
            this.sliderRandMaxSize = new System.Windows.Forms.TrackBar();
            this.txtRandRotLeft = new System.Windows.Forms.Label();
            this.sliderRandRotLeft = new System.Windows.Forms.TrackBar();
            this.txtRandRotRight = new System.Windows.Forms.Label();
            this.sliderRandRotRight = new System.Windows.Forms.TrackBar();
            this.txtRandMinAlpha = new System.Windows.Forms.Label();
            this.sliderRandMinAlpha = new System.Windows.Forms.TrackBar();
            this.txtRandHorzShift = new System.Windows.Forms.Label();
            this.sliderRandHorzShift = new System.Windows.Forms.TrackBar();
            this.txtRandVertShift = new System.Windows.Forms.Label();
            this.sliderRandVertShift = new System.Windows.Forms.TrackBar();
            this.bttnJitterColorControls = new BrushFactory.Gui.Accordion();
            this.panelJitterColor = new System.Windows.Forms.FlowLayoutPanel();
            this.txtJitterRed = new System.Windows.Forms.Label();
            this.sliderJitterMinRed = new System.Windows.Forms.TrackBar();
            this.sliderJitterMaxRed = new System.Windows.Forms.TrackBar();
            this.txtJitterGreen = new System.Windows.Forms.Label();
            this.sliderJitterMinGreen = new System.Windows.Forms.TrackBar();
            this.sliderJitterMaxGreen = new System.Windows.Forms.TrackBar();
            this.txtJitterBlue = new System.Windows.Forms.Label();
            this.sliderJitterMinBlue = new System.Windows.Forms.TrackBar();
            this.sliderJitterMaxBlue = new System.Windows.Forms.TrackBar();
            this.txtJitterHue = new System.Windows.Forms.Label();
            this.sliderJitterMinHue = new System.Windows.Forms.TrackBar();
            this.sliderJitterMaxHue = new System.Windows.Forms.TrackBar();
            this.txtJitterSaturation = new System.Windows.Forms.Label();
            this.sliderJitterMinSat = new System.Windows.Forms.TrackBar();
            this.sliderJitterMaxSat = new System.Windows.Forms.TrackBar();
            this.txtJitterValue = new System.Windows.Forms.Label();
            this.sliderJitterMinVal = new System.Windows.Forms.TrackBar();
            this.sliderJitterMaxVal = new System.Windows.Forms.TrackBar();
            this.bttnShiftBasicsControls = new BrushFactory.Gui.Accordion();
            this.panelShiftBasics = new System.Windows.Forms.FlowLayoutPanel();
            this.txtShiftSize = new System.Windows.Forms.Label();
            this.sliderShiftSize = new System.Windows.Forms.TrackBar();
            this.txtShiftRotation = new System.Windows.Forms.Label();
            this.sliderShiftRotation = new System.Windows.Forms.TrackBar();
            this.txtShiftAlpha = new System.Windows.Forms.Label();
            this.sliderShiftAlpha = new System.Windows.Forms.TrackBar();
            this.bttnTabAssignPressureControls = new BrushFactory.Gui.Accordion();
            this.panelTabletAssignPressure = new System.Windows.Forms.FlowLayoutPanel();
            this.panelTabPressureBrushAlpha = new System.Windows.Forms.FlowLayoutPanel();
            this.panel3 = new System.Windows.Forms.Panel();
            this.txtTabPressureBrushAlpha = new System.Windows.Forms.Label();
            this.spinTabPressureBrushAlpha = new System.Windows.Forms.NumericUpDown();
            this.cmbxTabPressureBrushAlpha = new BrushFactory.Gui.CmbxTabletValueType();
            this.panelTabPressureBrushSize = new System.Windows.Forms.FlowLayoutPanel();
            this.panel8 = new System.Windows.Forms.Panel();
            this.txtTabPressureBrushSize = new System.Windows.Forms.Label();
            this.spinTabPressureBrushSize = new System.Windows.Forms.NumericUpDown();
            this.cmbxTabPressureBrushSize = new BrushFactory.Gui.CmbxTabletValueType();
            this.panelTabPressureBrushRotation = new System.Windows.Forms.FlowLayoutPanel();
            this.panel2 = new System.Windows.Forms.Panel();
            this.txtTabPressureBrushRotation = new System.Windows.Forms.Label();
            this.spinTabPressureBrushRotation = new System.Windows.Forms.NumericUpDown();
            this.cmbxTabPressureBrushRotation = new BrushFactory.Gui.CmbxTabletValueType();
            this.panelTabPressureMinDrawDistance = new System.Windows.Forms.FlowLayoutPanel();
            this.panel1 = new System.Windows.Forms.Panel();
            this.lblTabPressureMinDrawDistance = new System.Windows.Forms.Label();
            this.spinTabPressureMinDrawDistance = new System.Windows.Forms.NumericUpDown();
            this.cmbxTabPressureMinDrawDistance = new BrushFactory.Gui.CmbxTabletValueType();
            this.panelTabPressureBrushDensity = new System.Windows.Forms.FlowLayoutPanel();
            this.panel4 = new System.Windows.Forms.Panel();
            this.lblTabPressureBrushDensity = new System.Windows.Forms.Label();
            this.spinTabPressureBrushDensity = new System.Windows.Forms.NumericUpDown();
            this.cmbxTabPressureBrushDensity = new BrushFactory.Gui.CmbxTabletValueType();
            this.panelTabPressureRandMinSize = new System.Windows.Forms.FlowLayoutPanel();
            this.panel5 = new System.Windows.Forms.Panel();
            this.lblTabPressureRandMinSize = new System.Windows.Forms.Label();
            this.spinTabPressureRandMinSize = new System.Windows.Forms.NumericUpDown();
            this.cmbxTabPressureRandMinSize = new BrushFactory.Gui.CmbxTabletValueType();
            this.panelTabPressureRandMaxSize = new System.Windows.Forms.FlowLayoutPanel();
            this.panel6 = new System.Windows.Forms.Panel();
            this.lblTabPressureRandMaxSize = new System.Windows.Forms.Label();
            this.spinTabPressureRandMaxSize = new System.Windows.Forms.NumericUpDown();
            this.cmbxTabPressureRandMaxSize = new BrushFactory.Gui.CmbxTabletValueType();
            this.panelTabPressureRandRotLeft = new System.Windows.Forms.FlowLayoutPanel();
            this.panel7 = new System.Windows.Forms.Panel();
            this.lblTabPressureRandRotLeft = new System.Windows.Forms.Label();
            this.spinTabPressureRandRotLeft = new System.Windows.Forms.NumericUpDown();
            this.cmbxTabPressureRandRotLeft = new BrushFactory.Gui.CmbxTabletValueType();
            this.panelTabPressureRandRotRight = new System.Windows.Forms.FlowLayoutPanel();
            this.panel9 = new System.Windows.Forms.Panel();
            this.lblTabPressureRandRotRight = new System.Windows.Forms.Label();
            this.spinTabPressureRandRotRight = new System.Windows.Forms.NumericUpDown();
            this.cmbxTabPressureRandRotRight = new BrushFactory.Gui.CmbxTabletValueType();
            this.panelTabPressureRandMinAlpha = new System.Windows.Forms.FlowLayoutPanel();
            this.panel10 = new System.Windows.Forms.Panel();
            this.lblTabPressureRandMinAlpha = new System.Windows.Forms.Label();
            this.spinTabPressureRandMinAlpha = new System.Windows.Forms.NumericUpDown();
            this.cmbxTabPressureRandMinAlpha = new BrushFactory.Gui.CmbxTabletValueType();
            this.panelTabPressureRandHorShift = new System.Windows.Forms.FlowLayoutPanel();
            this.panel12 = new System.Windows.Forms.Panel();
            this.lblTabPressureRandHorShift = new System.Windows.Forms.Label();
            this.spinTabPressureRandHorShift = new System.Windows.Forms.NumericUpDown();
            this.cmbxTabPressureRandHorShift = new BrushFactory.Gui.CmbxTabletValueType();
            this.panelTabPressureRandVerShift = new System.Windows.Forms.FlowLayoutPanel();
            this.panel11 = new System.Windows.Forms.Panel();
            this.lblTabPressureRandVerShift = new System.Windows.Forms.Label();
            this.spinTabPressureRandVerShift = new System.Windows.Forms.NumericUpDown();
            this.cmbxTabPressureRandVerShift = new BrushFactory.Gui.CmbxTabletValueType();
            this.panelTabPressureRedJitter = new System.Windows.Forms.FlowLayoutPanel();
            this.panel13 = new System.Windows.Forms.Panel();
            this.spinTabPressureMinRedJitter = new System.Windows.Forms.NumericUpDown();
            this.lblTabPressureRedJitter = new System.Windows.Forms.Label();
            this.spinTabPressureMaxRedJitter = new System.Windows.Forms.NumericUpDown();
            this.cmbxTabPressureRedJitter = new BrushFactory.Gui.CmbxTabletValueType();
            this.panelTabPressureGreenJitter = new System.Windows.Forms.FlowLayoutPanel();
            this.panel14 = new System.Windows.Forms.Panel();
            this.spinTabPressureMinGreenJitter = new System.Windows.Forms.NumericUpDown();
            this.lblTabPressureGreenJitter = new System.Windows.Forms.Label();
            this.spinTabPressureMaxGreenJitter = new System.Windows.Forms.NumericUpDown();
            this.cmbxTabPressureGreenJitter = new BrushFactory.Gui.CmbxTabletValueType();
            this.panelTabPressureBlueJitter = new System.Windows.Forms.FlowLayoutPanel();
            this.panel15 = new System.Windows.Forms.Panel();
            this.spinTabPressureMinBlueJitter = new System.Windows.Forms.NumericUpDown();
            this.lblTabPressureBlueJitter = new System.Windows.Forms.Label();
            this.spinTabPressureMaxBlueJitter = new System.Windows.Forms.NumericUpDown();
            this.cmbxTabPressureBlueJitter = new BrushFactory.Gui.CmbxTabletValueType();
            this.panelTabPressureHueJitter = new System.Windows.Forms.FlowLayoutPanel();
            this.panel16 = new System.Windows.Forms.Panel();
            this.spinTabPressureMinHueJitter = new System.Windows.Forms.NumericUpDown();
            this.lblTabPressureHueJitter = new System.Windows.Forms.Label();
            this.spinTabPressureMaxHueJitter = new System.Windows.Forms.NumericUpDown();
            this.cmbxTabPressureHueJitter = new BrushFactory.Gui.CmbxTabletValueType();
            this.panelTabPressureSatJitter = new System.Windows.Forms.FlowLayoutPanel();
            this.panel17 = new System.Windows.Forms.Panel();
            this.spinTabPressureMinSatJitter = new System.Windows.Forms.NumericUpDown();
            this.lblTabPressureSatJitter = new System.Windows.Forms.Label();
            this.spinTabPressureMaxSatJitter = new System.Windows.Forms.NumericUpDown();
            this.cmbxTabPressureSatJitter = new BrushFactory.Gui.CmbxTabletValueType();
            this.panelTabPressureValueJitter = new System.Windows.Forms.FlowLayoutPanel();
            this.panel18 = new System.Windows.Forms.Panel();
            this.spinTabPressureMinValueJitter = new System.Windows.Forms.NumericUpDown();
            this.lblTabPressureValueJitter = new System.Windows.Forms.Label();
            this.spinTabPressureMaxValueJitter = new System.Windows.Forms.NumericUpDown();
            this.cmbxTabPressureValueJitter = new BrushFactory.Gui.CmbxTabletValueType();
            this.bttnSettings = new BrushFactory.Gui.Accordion();
            this.panelSettings = new System.Windows.Forms.FlowLayoutPanel();
            this.bttnCustomBrushImageLocations = new System.Windows.Forms.Button();
            this.bttnClearBrushImages = new System.Windows.Forms.Button();
            this.bttnClearSettings = new System.Windows.Forms.Button();
            this.bttnDeleteBrush = new System.Windows.Forms.Button();
            this.bttnSaveBrush = new System.Windows.Forms.Button();
            this.chkbxAutomaticBrushDensity = new System.Windows.Forms.CheckBox();
            this.displayCanvas.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.displayCanvas)).BeginInit();
            this.panelUndoRedoOkCancel.SuspendLayout();
            this.panelAllSettingsContainer.SuspendLayout();
            this.panelDockSettingsContainer.SuspendLayout();
            this.flowLayoutPanel1.SuspendLayout();
            this.panelSettingsContainer.SuspendLayout();
            this.panelBrush.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.sliderCanvasZoom)).BeginInit();
            this.panelBrushAddPickColor.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.sliderBrushAlpha)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderBrushRotation)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderBrushSize)).BeginInit();
            this.panelSpecialSettings.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.sliderMinDrawDistance)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderBrushDensity)).BeginInit();
            this.panelJitterBasics.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandMinSize)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandMaxSize)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandRotLeft)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandRotRight)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandMinAlpha)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandHorzShift)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandVertShift)).BeginInit();
            this.panelJitterColor.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMinRed)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMaxRed)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMinGreen)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMaxGreen)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMinBlue)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMaxBlue)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMinHue)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMaxHue)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMinSat)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMaxSat)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMinVal)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMaxVal)).BeginInit();
            this.panelShiftBasics.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.sliderShiftSize)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderShiftRotation)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderShiftAlpha)).BeginInit();
            this.panelTabletAssignPressure.SuspendLayout();
            this.panelTabPressureBrushAlpha.SuspendLayout();
            this.panel3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureBrushAlpha)).BeginInit();
            this.panelTabPressureBrushSize.SuspendLayout();
            this.panel8.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureBrushSize)).BeginInit();
            this.panelTabPressureBrushRotation.SuspendLayout();
            this.panel2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureBrushRotation)).BeginInit();
            this.panelTabPressureMinDrawDistance.SuspendLayout();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureMinDrawDistance)).BeginInit();
            this.panelTabPressureBrushDensity.SuspendLayout();
            this.panel4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureBrushDensity)).BeginInit();
            this.panelTabPressureRandMinSize.SuspendLayout();
            this.panel5.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureRandMinSize)).BeginInit();
            this.panelTabPressureRandMaxSize.SuspendLayout();
            this.panel6.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureRandMaxSize)).BeginInit();
            this.panelTabPressureRandRotLeft.SuspendLayout();
            this.panel7.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureRandRotLeft)).BeginInit();
            this.panelTabPressureRandRotRight.SuspendLayout();
            this.panel9.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureRandRotRight)).BeginInit();
            this.panelTabPressureRandMinAlpha.SuspendLayout();
            this.panel10.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureRandMinAlpha)).BeginInit();
            this.panelTabPressureRandHorShift.SuspendLayout();
            this.panel12.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureRandHorShift)).BeginInit();
            this.panelTabPressureRandVerShift.SuspendLayout();
            this.panel11.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureRandVerShift)).BeginInit();
            this.panelTabPressureRedJitter.SuspendLayout();
            this.panel13.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureMinRedJitter)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureMaxRedJitter)).BeginInit();
            this.panelTabPressureGreenJitter.SuspendLayout();
            this.panel14.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureMinGreenJitter)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureMaxGreenJitter)).BeginInit();
            this.panelTabPressureBlueJitter.SuspendLayout();
            this.panel15.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureMinBlueJitter)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureMaxBlueJitter)).BeginInit();
            this.panelTabPressureHueJitter.SuspendLayout();
            this.panel16.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureMinHueJitter)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureMaxHueJitter)).BeginInit();
            this.panelTabPressureSatJitter.SuspendLayout();
            this.panel17.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureMinSatJitter)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureMaxSatJitter)).BeginInit();
            this.panelTabPressureValueJitter.SuspendLayout();
            this.panel18.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureMinValueJitter)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureMaxValueJitter)).BeginInit();
            this.panelSettings.SuspendLayout();
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
            this.txtTooltip.BackColor = System.Drawing.SystemColors.ControlDarkDark;
            this.txtTooltip.ForeColor = System.Drawing.SystemColors.HighlightText;
            this.txtTooltip.Name = "txtTooltip";
            // 
            // displayCanvas
            // 
            //this.displayCanvas.BackgroundImage = global::BrushFactory.Properties.Resources.CheckeredBg;
            this.displayCanvas.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(207)))), ((int)(((byte)(207)))), ((int)(((byte)(207)))));
            resources.ApplyResources(this.displayCanvas, "displayCanvas");
            this.displayCanvas.Controls.Add(this.txtTooltip);
            this.displayCanvas.Name = "displayCanvas";
            this.displayCanvas.TabStop = false;
            this.displayCanvas.Paint += new System.Windows.Forms.PaintEventHandler(this.DisplayCanvas_Paint);
            this.displayCanvas.MouseDown += new System.Windows.Forms.MouseEventHandler(this.DisplayCanvas_MouseDown);
            this.displayCanvas.MouseEnter += new System.EventHandler(this.DisplayCanvas_MouseEnter);
            this.displayCanvas.MouseMove += new System.Windows.Forms.MouseEventHandler(this.DisplayCanvas_MouseMove);
            this.displayCanvas.MouseUp += new System.Windows.Forms.MouseEventHandler(this.DisplayCanvas_MouseUp);
            // 
            // bttnToolBrush
            // 
            this.bttnToolBrush.BackColor = System.Drawing.SystemColors.ButtonShadow;
            this.bttnToolBrush.Image = global::BrushFactory.Properties.Resources.ToolBrush;
            resources.ApplyResources(this.bttnToolBrush, "bttnToolBrush");
            this.bttnToolBrush.Name = "bttnToolBrush";
            this.bttnToolBrush.UseVisualStyleBackColor = false;
            this.bttnToolBrush.Click += new System.EventHandler(this.BttnToolBrush_Click);
            this.bttnToolBrush.MouseEnter += new System.EventHandler(this.BttnToolBrush_MouseEnter);
            // 
            // dummyImageList
            // 
            this.dummyImageList.ColorDepth = System.Windows.Forms.ColorDepth.Depth32Bit;
            resources.ApplyResources(this.dummyImageList, "dummyImageList");
            this.dummyImageList.TransparentColor = System.Drawing.Color.Transparent;
            // 
            // panelUndoRedoOkCancel
            // 
            resources.ApplyResources(this.panelUndoRedoOkCancel, "panelUndoRedoOkCancel");
            this.panelUndoRedoOkCancel.BackColor = System.Drawing.SystemColors.ControlDarkDark;
            this.panelUndoRedoOkCancel.Controls.Add(this.bttnUndo);
            this.panelUndoRedoOkCancel.Controls.Add(this.bttnRedo);
            this.panelUndoRedoOkCancel.Controls.Add(this.bttnOk);
            this.panelUndoRedoOkCancel.Controls.Add(this.bttnCancel);
            this.panelUndoRedoOkCancel.Name = "panelUndoRedoOkCancel";
            // 
            // bttnUndo
            // 
            resources.ApplyResources(this.bttnUndo, "bttnUndo");
            this.bttnUndo.Name = "bttnUndo";
            this.bttnUndo.UseVisualStyleBackColor = true;
            this.bttnUndo.Click += new System.EventHandler(this.BttnUndo_Click);
            this.bttnUndo.MouseEnter += new System.EventHandler(this.BttnUndo_MouseEnter);
            // 
            // bttnRedo
            // 
            resources.ApplyResources(this.bttnRedo, "bttnRedo");
            this.bttnRedo.Name = "bttnRedo";
            this.bttnRedo.UseVisualStyleBackColor = true;
            this.bttnRedo.Click += new System.EventHandler(this.BttnRedo_Click);
            this.bttnRedo.MouseEnter += new System.EventHandler(this.BttnRedo_MouseEnter);
            // 
            // bttnOk
            // 
            this.bttnOk.BackColor = System.Drawing.Color.Honeydew;
            resources.ApplyResources(this.bttnOk, "bttnOk");
            this.bttnOk.Name = "bttnOk";
            this.bttnOk.UseVisualStyleBackColor = false;
            this.bttnOk.Click += new System.EventHandler(this.BttnOk_Click);
            this.bttnOk.MouseEnter += new System.EventHandler(this.BttnOk_MouseEnter);
            // 
            // bttnCancel
            // 
            resources.ApplyResources(this.bttnCancel, "bttnCancel");
            this.bttnCancel.BackColor = System.Drawing.Color.MistyRose;
            this.bttnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.bttnCancel.Name = "bttnCancel";
            this.bttnCancel.UseVisualStyleBackColor = false;
            this.bttnCancel.Click += new System.EventHandler(this.BttnCancel_Click);
            this.bttnCancel.MouseEnter += new System.EventHandler(this.BttnCancel_MouseEnter);
            // 
            // brushImageLoadingWorker
            // 
            this.brushImageLoadingWorker.WorkerReportsProgress = true;
            this.brushImageLoadingWorker.WorkerSupportsCancellation = true;
            this.brushImageLoadingWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(this.BrushImageLoadingWorker_DoWork);
            this.brushImageLoadingWorker.ProgressChanged += new System.ComponentModel.ProgressChangedEventHandler(this.BrushImageLoadingWorker_ProgressChanged);
            this.brushImageLoadingWorker.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.BrushImageLoadingWorker_RunWorkerCompleted);
            // 
            // bttnColorPicker
            // 
            this.bttnColorPicker.Image = global::BrushFactory.Properties.Resources.ColorPickerIcon;
            resources.ApplyResources(this.bttnColorPicker, "bttnColorPicker");
            this.bttnColorPicker.Name = "bttnColorPicker";
            this.bttnColorPicker.UseVisualStyleBackColor = true;
            this.bttnColorPicker.Click += new System.EventHandler(this.BttnToolColorPicker_Click);
            this.bttnColorPicker.MouseEnter += new System.EventHandler(this.BttnToolColorPicker_MouseEnter);
            // 
            // panelAllSettingsContainer
            // 
            this.panelAllSettingsContainer.BackColor = System.Drawing.Color.Transparent;
            this.panelAllSettingsContainer.Controls.Add(this.panelDockSettingsContainer);
            this.panelAllSettingsContainer.Controls.Add(this.panelUndoRedoOkCancel);
            resources.ApplyResources(this.panelAllSettingsContainer, "panelAllSettingsContainer");
            this.panelAllSettingsContainer.Name = "panelAllSettingsContainer";
            // 
            // panelDockSettingsContainer
            // 
            resources.ApplyResources(this.panelDockSettingsContainer, "panelDockSettingsContainer");
            this.panelDockSettingsContainer.BackColor = System.Drawing.SystemColors.ControlDarkDark;
            this.panelDockSettingsContainer.Controls.Add(this.flowLayoutPanel1);
            this.panelDockSettingsContainer.Controls.Add(this.panelSettingsContainer);
            this.panelDockSettingsContainer.Name = "panelDockSettingsContainer";
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.Controls.Add(this.bttnToolBrush);
            this.flowLayoutPanel1.Controls.Add(this.BttnToolEraser);
            this.flowLayoutPanel1.Controls.Add(this.bttnColorPicker);
            this.flowLayoutPanel1.Controls.Add(this.bttnToolOrigin);
            resources.ApplyResources(this.flowLayoutPanel1, "flowLayoutPanel1");
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            // 
            // BttnToolEraser
            // 
            this.BttnToolEraser.Image = global::BrushFactory.Properties.Resources.ToolEraser;
            resources.ApplyResources(this.BttnToolEraser, "BttnToolEraser");
            this.BttnToolEraser.Name = "BttnToolEraser";
            this.BttnToolEraser.UseVisualStyleBackColor = true;
            this.BttnToolEraser.Click += new System.EventHandler(this.BttnToolEraser_Click);
            this.BttnToolEraser.MouseEnter += new System.EventHandler(this.BttnToolEraser_MouseEnter);
            // 
            // bttnToolOrigin
            // 
            this.bttnToolOrigin.Image = global::BrushFactory.Properties.Resources.ToolOrigin;
            resources.ApplyResources(this.bttnToolOrigin, "bttnToolOrigin");
            this.bttnToolOrigin.Name = "bttnToolOrigin";
            this.bttnToolOrigin.UseVisualStyleBackColor = true;
            this.bttnToolOrigin.Click += new System.EventHandler(this.BttnToolOrigin_Click);
            this.bttnToolOrigin.MouseEnter += new System.EventHandler(this.BttnToolOrigin_MouseEnter);
            // 
            // panelSettingsContainer
            // 
            resources.ApplyResources(this.panelSettingsContainer, "panelSettingsContainer");
            this.panelSettingsContainer.BackColor = System.Drawing.Color.Transparent;
            this.panelSettingsContainer.Controls.Add(this.bttnBrushControls);
            this.panelSettingsContainer.Controls.Add(this.panelBrush);
            this.panelSettingsContainer.Controls.Add(this.bttnSpecialSettings);
            this.panelSettingsContainer.Controls.Add(this.panelSpecialSettings);
            this.panelSettingsContainer.Controls.Add(this.bttnJitterBasicsControls);
            this.panelSettingsContainer.Controls.Add(this.panelJitterBasics);
            this.panelSettingsContainer.Controls.Add(this.bttnJitterColorControls);
            this.panelSettingsContainer.Controls.Add(this.panelJitterColor);
            this.panelSettingsContainer.Controls.Add(this.bttnShiftBasicsControls);
            this.panelSettingsContainer.Controls.Add(this.panelShiftBasics);
            this.panelSettingsContainer.Controls.Add(this.bttnTabAssignPressureControls);
            this.panelSettingsContainer.Controls.Add(this.panelTabletAssignPressure);
            this.panelSettingsContainer.Controls.Add(this.bttnSettings);
            this.panelSettingsContainer.Controls.Add(this.panelSettings);
            this.panelSettingsContainer.Name = "panelSettingsContainer";
            // 
            // bttnBrushControls
            // 
            this.bttnBrushControls.BackColor = System.Drawing.Color.Black;
            this.bttnBrushControls.ForeColor = System.Drawing.Color.WhiteSmoke;
            resources.ApplyResources(this.bttnBrushControls, "bttnBrushControls");
            this.bttnBrushControls.Name = "bttnBrushControls";
            this.bttnBrushControls.UseVisualStyleBackColor = false;
            // 
            // panelBrush
            // 
            resources.ApplyResources(this.panelBrush, "panelBrush");
            this.panelBrush.BackColor = System.Drawing.SystemColors.Control;
            this.panelBrush.Controls.Add(this.txtCanvasZoom);
            this.panelBrush.Controls.Add(this.sliderCanvasZoom);
            this.panelBrush.Controls.Add(this.listviewBrushPicker);
            this.panelBrush.Controls.Add(this.listviewBrushImagePicker);
            this.panelBrush.Controls.Add(this.panelBrushAddPickColor);
            this.panelBrush.Controls.Add(this.txtBrushAlpha);
            this.panelBrush.Controls.Add(this.sliderBrushAlpha);
            this.panelBrush.Controls.Add(this.txtBrushRotation);
            this.panelBrush.Controls.Add(this.sliderBrushRotation);
            this.panelBrush.Controls.Add(this.txtBrushSize);
            this.panelBrush.Controls.Add(this.sliderBrushSize);
            this.panelBrush.Name = "panelBrush";
            // 
            // txtCanvasZoom
            // 
            resources.ApplyResources(this.txtCanvasZoom, "txtCanvasZoom");
            this.txtCanvasZoom.BackColor = System.Drawing.Color.Transparent;
            this.txtCanvasZoom.Name = "txtCanvasZoom";
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
            // listviewBrushPicker
            // 
            this.listviewBrushPicker.HideSelection = false;
            resources.ApplyResources(this.listviewBrushPicker, "listviewBrushPicker");
            this.listviewBrushPicker.Name = "listviewBrushPicker";
            this.listviewBrushPicker.UseCompatibleStateImageBehavior = false;
            this.listviewBrushPicker.View = System.Windows.Forms.View.List;
            this.listviewBrushPicker.SelectedIndexChanged += new System.EventHandler(this.ListViewBrushPicker_SelectedIndexChanged);
            this.listviewBrushPicker.MouseEnter += new System.EventHandler(this.ListviewBrushPicker_MouseEnter);
            // 
            // listviewBrushImagePicker
            // 
            this.listviewBrushImagePicker.HideSelection = false;
            this.listviewBrushImagePicker.LargeImageList = this.dummyImageList;
            resources.ApplyResources(this.listviewBrushImagePicker, "listviewBrushImagePicker");
            this.listviewBrushImagePicker.MultiSelect = false;
            this.listviewBrushImagePicker.Name = "listviewBrushImagePicker";
            this.listviewBrushImagePicker.OwnerDraw = true;
            this.listviewBrushImagePicker.ShowItemToolTips = true;
            this.listviewBrushImagePicker.UseCompatibleStateImageBehavior = false;
            this.listviewBrushImagePicker.VirtualMode = true;
            this.listviewBrushImagePicker.CacheVirtualItems += new System.Windows.Forms.CacheVirtualItemsEventHandler(this.ListViewBrushImagePicker_CacheVirtualItems);
            this.listviewBrushImagePicker.DrawColumnHeader += new System.Windows.Forms.DrawListViewColumnHeaderEventHandler(this.ListViewBrushImagePicker_DrawColumnHeader);
            this.listviewBrushImagePicker.DrawItem += new System.Windows.Forms.DrawListViewItemEventHandler(this.ListViewBrushImagePicker_DrawItem);
            this.listviewBrushImagePicker.DrawSubItem += new System.Windows.Forms.DrawListViewSubItemEventHandler(this.ListViewBrushImagePicker_DrawSubItem);
            this.listviewBrushImagePicker.RetrieveVirtualItem += new System.Windows.Forms.RetrieveVirtualItemEventHandler(this.ListViewBrushImagePicker_RetrieveVirtualItem);
            this.listviewBrushImagePicker.SelectedIndexChanged += new System.EventHandler(this.ListViewBrushImagePicker_SelectedIndexChanged);
            this.listviewBrushImagePicker.MouseEnter += new System.EventHandler(this.ListViewBrushImagePicker_MouseEnter);
            // 
            // panelBrushAddPickColor
            // 
            resources.ApplyResources(this.panelBrushAddPickColor, "panelBrushAddPickColor");
            this.panelBrushAddPickColor.Controls.Add(this.chkbxColorizeBrush);
            this.panelBrushAddPickColor.Controls.Add(this.bttnAddBrushImages);
            this.panelBrushAddPickColor.Controls.Add(this.brushImageLoadProgressBar);
            this.panelBrushAddPickColor.Controls.Add(this.bttnBrushColor);
            this.panelBrushAddPickColor.Name = "panelBrushAddPickColor";
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
            // bttnAddBrushImages
            // 
            this.bttnAddBrushImages.Image = global::BrushFactory.Properties.Resources.AddBrushIcon;
            resources.ApplyResources(this.bttnAddBrushImages, "bttnAddBrushImages");
            this.bttnAddBrushImages.Name = "bttnAddBrushImages";
            this.bttnAddBrushImages.UseVisualStyleBackColor = true;
            this.bttnAddBrushImages.Click += new System.EventHandler(this.BttnAddBrushImages_Click);
            this.bttnAddBrushImages.MouseEnter += new System.EventHandler(this.BttnAddBrushImages_MouseEnter);
            // 
            // brushImageLoadProgressBar
            // 
            resources.ApplyResources(this.brushImageLoadProgressBar, "brushImageLoadProgressBar");
            this.brushImageLoadProgressBar.Name = "brushImageLoadProgressBar";
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
            // txtBrushAlpha
            // 
            resources.ApplyResources(this.txtBrushAlpha, "txtBrushAlpha");
            this.txtBrushAlpha.BackColor = System.Drawing.Color.Transparent;
            this.txtBrushAlpha.Name = "txtBrushAlpha";
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
            // txtBrushRotation
            // 
            resources.ApplyResources(this.txtBrushRotation, "txtBrushRotation");
            this.txtBrushRotation.BackColor = System.Drawing.Color.Transparent;
            this.txtBrushRotation.Name = "txtBrushRotation";
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
            this.sliderBrushSize.Minimum = 1;
            this.sliderBrushSize.Name = "sliderBrushSize";
            this.sliderBrushSize.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderBrushSize.Value = 10;
            this.sliderBrushSize.ValueChanged += new System.EventHandler(this.SliderBrushSize_ValueChanged);
            this.sliderBrushSize.MouseEnter += new System.EventHandler(this.SliderBrushSize_MouseEnter);
            // 
            // bttnSpecialSettings
            // 
            this.bttnSpecialSettings.BackColor = System.Drawing.Color.Black;
            this.bttnSpecialSettings.ForeColor = System.Drawing.Color.WhiteSmoke;
            resources.ApplyResources(this.bttnSpecialSettings, "bttnSpecialSettings");
            this.bttnSpecialSettings.Name = "bttnSpecialSettings";
            this.bttnSpecialSettings.UseVisualStyleBackColor = false;
            // 
            // panelSpecialSettings
            // 
            resources.ApplyResources(this.panelSpecialSettings, "panelSpecialSettings");
            this.panelSpecialSettings.BackColor = System.Drawing.SystemColors.Control;
            this.panelSpecialSettings.Controls.Add(this.txtMinDrawDistance);
            this.panelSpecialSettings.Controls.Add(this.sliderMinDrawDistance);
            this.panelSpecialSettings.Controls.Add(this.chkbxAutomaticBrushDensity);
            this.panelSpecialSettings.Controls.Add(this.txtBrushDensity);
            this.panelSpecialSettings.Controls.Add(this.sliderBrushDensity);
            this.panelSpecialSettings.Controls.Add(this.cmbxBrushSmoothing);
            this.panelSpecialSettings.Controls.Add(this.cmbxSymmetry);
            this.panelSpecialSettings.Controls.Add(this.chkbxOrientToMouse);
            this.panelSpecialSettings.Controls.Add(this.chkbxLockAlpha);
            this.panelSpecialSettings.Name = "panelSpecialSettings";
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
            // chkbxAutomaticBrushDensity
            // 
            resources.ApplyResources(this.chkbxAutomaticBrushDensity, "chkbxAutomaticBrushDensity");
            this.chkbxAutomaticBrushDensity.Text = "Manage density automatically";
            this.chkbxAutomaticBrushDensity.Checked = true;
            this.chkbxAutomaticBrushDensity.Name = "chkbxAutomaticBrushDensity";
            this.chkbxAutomaticBrushDensity.MouseEnter += new System.EventHandler(this.AutomaticBrushDensity_MouseEnter);
            this.chkbxAutomaticBrushDensity.CheckedChanged += new System.EventHandler(this.AutomaticBrushDensity_CheckedChanged);
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
            this.sliderBrushDensity.Enabled = false;
            this.sliderBrushDensity.ValueChanged += new System.EventHandler(this.SliderBrushDensity_ValueChanged);
            this.sliderBrushDensity.MouseEnter += new System.EventHandler(this.SliderBrushDensity_MouseEnter);
            // 
            // cmbxSymmetry
            // 
            resources.ApplyResources(this.cmbxSymmetry, "cmbxSymmetry");
            this.cmbxSymmetry.BackColor = System.Drawing.Color.White;
            this.cmbxSymmetry.DropDownHeight = 140;
            this.cmbxSymmetry.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbxSymmetry.DropDownWidth = 20;
            this.cmbxSymmetry.FormattingEnabled = true;
            this.cmbxSymmetry.Name = "cmbxSymmetry";
            this.cmbxSymmetry.MouseEnter += new System.EventHandler(this.BttnSymmetry_MouseEnter);
            // 
            // cmbxBrushSmoothing
            // 
            resources.ApplyResources(this.cmbxBrushSmoothing, "cmbxBrushSmoothing");
            this.cmbxBrushSmoothing.BackColor = System.Drawing.Color.White;
            this.cmbxBrushSmoothing.DropDownHeight = 140;
            this.cmbxBrushSmoothing.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbxBrushSmoothing.DropDownWidth = 20;
            this.cmbxBrushSmoothing.FormattingEnabled = true;
            this.cmbxBrushSmoothing.Name = "cmbxBrushSmoothing";
            this.cmbxBrushSmoothing.MouseEnter += new System.EventHandler(this.BttnBrushSmoothing_MouseEnter);
            // 
            // chkbxOrientToMouse
            // 
            resources.ApplyResources(this.chkbxOrientToMouse, "chkbxOrientToMouse");
            this.chkbxOrientToMouse.Name = "chkbxOrientToMouse";
            this.chkbxOrientToMouse.UseVisualStyleBackColor = true;
            this.chkbxOrientToMouse.MouseEnter += new System.EventHandler(this.ChkbxOrientToMouse_MouseEnter);
            // 
            // chkbxLockAlpha
            // 
            resources.ApplyResources(this.chkbxLockAlpha, "chkbxLockAlpha");
            this.chkbxLockAlpha.Name = "chkbxLockAlpha";
            this.chkbxLockAlpha.UseVisualStyleBackColor = true;
            this.chkbxLockAlpha.MouseEnter += new System.EventHandler(this.ChkbxLockAlpha_MouseEnter);
            // 
            // bttnJitterBasicsControls
            // 
            this.bttnJitterBasicsControls.BackColor = System.Drawing.Color.Black;
            this.bttnJitterBasicsControls.ForeColor = System.Drawing.Color.WhiteSmoke;
            resources.ApplyResources(this.bttnJitterBasicsControls, "bttnJitterBasicsControls");
            this.bttnJitterBasicsControls.Name = "bttnJitterBasicsControls";
            this.bttnJitterBasicsControls.UseVisualStyleBackColor = false;
            // 
            // panelJitterBasics
            // 
            resources.ApplyResources(this.panelJitterBasics, "panelJitterBasics");
            this.panelJitterBasics.BackColor = System.Drawing.SystemColors.Control;
            this.panelJitterBasics.Controls.Add(this.txtRandMinSize);
            this.panelJitterBasics.Controls.Add(this.sliderRandMinSize);
            this.panelJitterBasics.Controls.Add(this.txtRandMaxSize);
            this.panelJitterBasics.Controls.Add(this.sliderRandMaxSize);
            this.panelJitterBasics.Controls.Add(this.txtRandRotLeft);
            this.panelJitterBasics.Controls.Add(this.sliderRandRotLeft);
            this.panelJitterBasics.Controls.Add(this.txtRandRotRight);
            this.panelJitterBasics.Controls.Add(this.sliderRandRotRight);
            this.panelJitterBasics.Controls.Add(this.txtRandMinAlpha);
            this.panelJitterBasics.Controls.Add(this.sliderRandMinAlpha);
            this.panelJitterBasics.Controls.Add(this.txtRandHorzShift);
            this.panelJitterBasics.Controls.Add(this.sliderRandHorzShift);
            this.panelJitterBasics.Controls.Add(this.txtRandVertShift);
            this.panelJitterBasics.Controls.Add(this.sliderRandVertShift);
            this.panelJitterBasics.Name = "panelJitterBasics";
            // 
            // txtRandMinSize
            // 
            this.txtRandMinSize.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.txtRandMinSize, "txtRandMinSize");
            this.txtRandMinSize.Name = "txtRandMinSize";
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
            // txtRandMaxSize
            // 
            this.txtRandMaxSize.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.txtRandMaxSize, "txtRandMaxSize");
            this.txtRandMaxSize.Name = "txtRandMaxSize";
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
            // txtRandRotLeft
            // 
            resources.ApplyResources(this.txtRandRotLeft, "txtRandRotLeft");
            this.txtRandRotLeft.BackColor = System.Drawing.Color.Transparent;
            this.txtRandRotLeft.Name = "txtRandRotLeft";
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
            // txtRandRotRight
            // 
            resources.ApplyResources(this.txtRandRotRight, "txtRandRotRight");
            this.txtRandRotRight.BackColor = System.Drawing.Color.Transparent;
            this.txtRandRotRight.Name = "txtRandRotRight";
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
            // txtRandMinAlpha
            // 
            resources.ApplyResources(this.txtRandMinAlpha, "txtRandMinAlpha");
            this.txtRandMinAlpha.BackColor = System.Drawing.Color.Transparent;
            this.txtRandMinAlpha.Name = "txtRandMinAlpha";
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
            // txtRandHorzShift
            // 
            this.txtRandHorzShift.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.txtRandHorzShift, "txtRandHorzShift");
            this.txtRandHorzShift.Name = "txtRandHorzShift";
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
            // txtRandVertShift
            // 
            this.txtRandVertShift.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.txtRandVertShift, "txtRandVertShift");
            this.txtRandVertShift.Name = "txtRandVertShift";
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
            // bttnJitterColorControls
            // 
            this.bttnJitterColorControls.BackColor = System.Drawing.Color.Black;
            this.bttnJitterColorControls.ForeColor = System.Drawing.Color.WhiteSmoke;
            resources.ApplyResources(this.bttnJitterColorControls, "bttnJitterColorControls");
            this.bttnJitterColorControls.Name = "bttnJitterColorControls";
            this.bttnJitterColorControls.UseVisualStyleBackColor = false;
            // 
            // panelJitterColor
            // 
            resources.ApplyResources(this.panelJitterColor, "panelJitterColor");
            this.panelJitterColor.BackColor = System.Drawing.SystemColors.Control;
            this.panelJitterColor.Controls.Add(this.txtJitterRed);
            this.panelJitterColor.Controls.Add(this.sliderJitterMinRed);
            this.panelJitterColor.Controls.Add(this.sliderJitterMaxRed);
            this.panelJitterColor.Controls.Add(this.txtJitterGreen);
            this.panelJitterColor.Controls.Add(this.sliderJitterMinGreen);
            this.panelJitterColor.Controls.Add(this.sliderJitterMaxGreen);
            this.panelJitterColor.Controls.Add(this.txtJitterBlue);
            this.panelJitterColor.Controls.Add(this.sliderJitterMinBlue);
            this.panelJitterColor.Controls.Add(this.sliderJitterMaxBlue);
            this.panelJitterColor.Controls.Add(this.txtJitterHue);
            this.panelJitterColor.Controls.Add(this.sliderJitterMinHue);
            this.panelJitterColor.Controls.Add(this.sliderJitterMaxHue);
            this.panelJitterColor.Controls.Add(this.txtJitterSaturation);
            this.panelJitterColor.Controls.Add(this.sliderJitterMinSat);
            this.panelJitterColor.Controls.Add(this.sliderJitterMaxSat);
            this.panelJitterColor.Controls.Add(this.txtJitterValue);
            this.panelJitterColor.Controls.Add(this.sliderJitterMinVal);
            this.panelJitterColor.Controls.Add(this.sliderJitterMaxVal);
            this.panelJitterColor.Name = "panelJitterColor";
            // 
            // txtJitterRed
            // 
            this.txtJitterRed.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.txtJitterRed, "txtJitterRed");
            this.txtJitterRed.Name = "txtJitterRed";
            // 
            // sliderJitterMinRed
            // 
            resources.ApplyResources(this.sliderJitterMinRed, "sliderJitterMinRed");
            this.sliderJitterMinRed.BackColor = System.Drawing.SystemColors.Control;
            this.sliderJitterMinRed.LargeChange = 1;
            this.sliderJitterMinRed.Maximum = 100;
            this.sliderJitterMinRed.Name = "sliderJitterMinRed";
            this.sliderJitterMinRed.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderJitterMinRed.ValueChanged += new System.EventHandler(this.SliderJitterMinRed_ValueChanged);
            this.sliderJitterMinRed.MouseEnter += new System.EventHandler(this.SliderJitterMinRed_MouseEnter);
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
            // txtJitterGreen
            // 
            this.txtJitterGreen.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.txtJitterGreen, "txtJitterGreen");
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
            // txtJitterBlue
            // 
            this.txtJitterBlue.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.txtJitterBlue, "txtJitterBlue");
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
            // txtJitterHue
            // 
            this.txtJitterHue.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.txtJitterHue, "txtJitterHue");
            this.txtJitterHue.Name = "txtJitterHue";
            // 
            // sliderJitterMinHue
            // 
            resources.ApplyResources(this.sliderJitterMinHue, "sliderJitterMinHue");
            this.sliderJitterMinHue.LargeChange = 1;
            this.sliderJitterMinHue.Maximum = 100;
            this.sliderJitterMinHue.Name = "sliderJitterMinHue";
            this.sliderJitterMinHue.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderJitterMinHue.ValueChanged += new System.EventHandler(this.SliderJitterMinHue_ValueChanged);
            this.sliderJitterMinHue.MouseEnter += new System.EventHandler(this.SliderJitterMinHue_MouseEnter);
            // 
            // sliderJitterMaxHue
            // 
            resources.ApplyResources(this.sliderJitterMaxHue, "sliderJitterMaxHue");
            this.sliderJitterMaxHue.LargeChange = 1;
            this.sliderJitterMaxHue.Maximum = 100;
            this.sliderJitterMaxHue.Name = "sliderJitterMaxHue";
            this.sliderJitterMaxHue.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderJitterMaxHue.ValueChanged += new System.EventHandler(this.SliderJitterMaxHue_ValueChanged);
            this.sliderJitterMaxHue.MouseEnter += new System.EventHandler(this.SliderJitterMaxHue_MouseEnter);
            // 
            // txtJitterSaturation
            // 
            this.txtJitterSaturation.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.txtJitterSaturation, "txtJitterSaturation");
            this.txtJitterSaturation.Name = "txtJitterSaturation";
            // 
            // sliderJitterMinSat
            // 
            resources.ApplyResources(this.sliderJitterMinSat, "sliderJitterMinSat");
            this.sliderJitterMinSat.LargeChange = 1;
            this.sliderJitterMinSat.Maximum = 100;
            this.sliderJitterMinSat.Name = "sliderJitterMinSat";
            this.sliderJitterMinSat.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderJitterMinSat.ValueChanged += new System.EventHandler(this.SliderJitterMinSat_ValueChanged);
            this.sliderJitterMinSat.MouseEnter += new System.EventHandler(this.SliderJitterMinSat_MouseEnter);
            // 
            // sliderJitterMaxSat
            // 
            resources.ApplyResources(this.sliderJitterMaxSat, "sliderJitterMaxSat");
            this.sliderJitterMaxSat.LargeChange = 1;
            this.sliderJitterMaxSat.Maximum = 100;
            this.sliderJitterMaxSat.Name = "sliderJitterMaxSat";
            this.sliderJitterMaxSat.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderJitterMaxSat.ValueChanged += new System.EventHandler(this.SliderJitterMaxSat_ValueChanged);
            this.sliderJitterMaxSat.MouseEnter += new System.EventHandler(this.SliderJitterMaxSat_MouseEnter);
            // 
            // txtJitterValue
            // 
            this.txtJitterValue.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.txtJitterValue, "txtJitterValue");
            this.txtJitterValue.Name = "txtJitterValue";
            // 
            // sliderJitterMinVal
            // 
            resources.ApplyResources(this.sliderJitterMinVal, "sliderJitterMinVal");
            this.sliderJitterMinVal.LargeChange = 1;
            this.sliderJitterMinVal.Maximum = 100;
            this.sliderJitterMinVal.Name = "sliderJitterMinVal";
            this.sliderJitterMinVal.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderJitterMinVal.ValueChanged += new System.EventHandler(this.SliderJitterMinVal_ValueChanged);
            this.sliderJitterMinVal.MouseEnter += new System.EventHandler(this.SliderJitterMinVal_MouseEnter);
            // 
            // sliderJitterMaxVal
            // 
            resources.ApplyResources(this.sliderJitterMaxVal, "sliderJitterMaxVal");
            this.sliderJitterMaxVal.LargeChange = 1;
            this.sliderJitterMaxVal.Maximum = 100;
            this.sliderJitterMaxVal.Name = "sliderJitterMaxVal";
            this.sliderJitterMaxVal.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderJitterMaxVal.ValueChanged += new System.EventHandler(this.SliderJitterMaxVal_ValueChanged);
            this.sliderJitterMaxVal.MouseEnter += new System.EventHandler(this.SliderJitterMaxVal_MouseEnter);
            // 
            // bttnShiftBasicsControls
            // 
            this.bttnShiftBasicsControls.BackColor = System.Drawing.Color.Black;
            this.bttnShiftBasicsControls.ForeColor = System.Drawing.Color.WhiteSmoke;
            resources.ApplyResources(this.bttnShiftBasicsControls, "bttnShiftBasicsControls");
            this.bttnShiftBasicsControls.Name = "bttnShiftBasicsControls";
            this.bttnShiftBasicsControls.UseVisualStyleBackColor = false;
            // 
            // panelShiftBasics
            // 
            resources.ApplyResources(this.panelShiftBasics, "panelShiftBasics");
            this.panelShiftBasics.BackColor = System.Drawing.SystemColors.Control;
            this.panelShiftBasics.Controls.Add(this.txtShiftSize);
            this.panelShiftBasics.Controls.Add(this.sliderShiftSize);
            this.panelShiftBasics.Controls.Add(this.txtShiftRotation);
            this.panelShiftBasics.Controls.Add(this.sliderShiftRotation);
            this.panelShiftBasics.Controls.Add(this.txtShiftAlpha);
            this.panelShiftBasics.Controls.Add(this.sliderShiftAlpha);
            this.panelShiftBasics.Name = "panelShiftBasics";
            // 
            // txtShiftSize
            // 
            this.txtShiftSize.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.txtShiftSize, "txtShiftSize");
            this.txtShiftSize.Name = "txtShiftSize";
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
            // txtShiftRotation
            // 
            this.txtShiftRotation.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.txtShiftRotation, "txtShiftRotation");
            this.txtShiftRotation.Name = "txtShiftRotation";
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
            // txtShiftAlpha
            // 
            this.txtShiftAlpha.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.txtShiftAlpha, "txtShiftAlpha");
            this.txtShiftAlpha.Name = "txtShiftAlpha";
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
            // bttnTabAssignPressureControls
            // 
            this.bttnTabAssignPressureControls.BackColor = System.Drawing.Color.Black;
            this.bttnTabAssignPressureControls.ForeColor = System.Drawing.Color.WhiteSmoke;
            resources.ApplyResources(this.bttnTabAssignPressureControls, "bttnTabAssignPressureControls");
            this.bttnTabAssignPressureControls.Name = "bttnTabAssignPressureControls";
            this.bttnTabAssignPressureControls.UseVisualStyleBackColor = false;
            // 
            // panelTabletAssignPressure
            // 
            resources.ApplyResources(this.panelTabletAssignPressure, "panelTabletAssignPressure");
            this.panelTabletAssignPressure.BackColor = System.Drawing.SystemColors.Control;
            this.panelTabletAssignPressure.Controls.Add(this.panelTabPressureBrushAlpha);
            this.panelTabletAssignPressure.Controls.Add(this.panelTabPressureBrushSize);
            this.panelTabletAssignPressure.Controls.Add(this.panelTabPressureBrushRotation);
            this.panelTabletAssignPressure.Controls.Add(this.panelTabPressureMinDrawDistance);
            this.panelTabletAssignPressure.Controls.Add(this.panelTabPressureBrushDensity);
            this.panelTabletAssignPressure.Controls.Add(this.panelTabPressureRandMinSize);
            this.panelTabletAssignPressure.Controls.Add(this.panelTabPressureRandMaxSize);
            this.panelTabletAssignPressure.Controls.Add(this.panelTabPressureRandRotLeft);
            this.panelTabletAssignPressure.Controls.Add(this.panelTabPressureRandRotRight);
            this.panelTabletAssignPressure.Controls.Add(this.panelTabPressureRandMinAlpha);
            this.panelTabletAssignPressure.Controls.Add(this.panelTabPressureRandHorShift);
            this.panelTabletAssignPressure.Controls.Add(this.panelTabPressureRandVerShift);
            this.panelTabletAssignPressure.Controls.Add(this.panelTabPressureRedJitter);
            this.panelTabletAssignPressure.Controls.Add(this.panelTabPressureGreenJitter);
            this.panelTabletAssignPressure.Controls.Add(this.panelTabPressureBlueJitter);
            this.panelTabletAssignPressure.Controls.Add(this.panelTabPressureHueJitter);
            this.panelTabletAssignPressure.Controls.Add(this.panelTabPressureSatJitter);
            this.panelTabletAssignPressure.Controls.Add(this.panelTabPressureValueJitter);
            this.panelTabletAssignPressure.Name = "panelTabletAssignPressure";
            // 
            // panelTabPressureBrushAlpha
            // 
            resources.ApplyResources(this.panelTabPressureBrushAlpha, "panelTabPressureBrushAlpha");
            this.panelTabPressureBrushAlpha.Controls.Add(this.panel3);
            this.panelTabPressureBrushAlpha.Controls.Add(this.cmbxTabPressureBrushAlpha);
            this.panelTabPressureBrushAlpha.Name = "panelTabPressureBrushAlpha";
            // 
            // panel3
            // 
            this.panel3.Controls.Add(this.txtTabPressureBrushAlpha);
            this.panel3.Controls.Add(this.spinTabPressureBrushAlpha);
            resources.ApplyResources(this.panel3, "panel3");
            this.panel3.Name = "panel3";
            // 
            // txtTabPressureBrushAlpha
            // 
            resources.ApplyResources(this.txtTabPressureBrushAlpha, "txtTabPressureBrushAlpha");
            this.txtTabPressureBrushAlpha.Name = "txtTabPressureBrushAlpha";
            // 
            // spinTabPressureBrushAlpha
            // 
            resources.ApplyResources(this.spinTabPressureBrushAlpha, "spinTabPressureBrushAlpha");
            this.spinTabPressureBrushAlpha.Maximum = new decimal(new int[] {
            99,
            0,
            0,
            0});
            this.spinTabPressureBrushAlpha.Minimum = new decimal(new int[] {
            99,
            0,
            0,
            -2147483648});
            this.spinTabPressureBrushAlpha.Name = "spinTabPressureBrushAlpha";
            // 
            // cmbxTabPressureBrushAlpha
            // 
            this.cmbxTabPressureBrushAlpha.BackColor = System.Drawing.Color.White;
            this.cmbxTabPressureBrushAlpha.DisplayMember = "DisplayMember";
            this.cmbxTabPressureBrushAlpha.DropDownHeight = 140;
            this.cmbxTabPressureBrushAlpha.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbxTabPressureBrushAlpha.DropDownWidth = 20;
            resources.ApplyResources(this.cmbxTabPressureBrushAlpha, "cmbxTabPressureBrushAlpha");
            this.cmbxTabPressureBrushAlpha.FormattingEnabled = true;
            this.cmbxTabPressureBrushAlpha.Name = "cmbxTabPressureBrushAlpha";
            this.cmbxTabPressureBrushAlpha.ValueMember = "ValueMember";
            this.cmbxTabPressureBrushAlpha.MouseHover += new System.EventHandler(this.CmbxTabPressure_MouseHover);
            // 
            // panelTabPressureBrushSize
            // 
            resources.ApplyResources(this.panelTabPressureBrushSize, "panelTabPressureBrushSize");
            this.panelTabPressureBrushSize.Controls.Add(this.panel8);
            this.panelTabPressureBrushSize.Controls.Add(this.cmbxTabPressureBrushSize);
            this.panelTabPressureBrushSize.Name = "panelTabPressureBrushSize";
            // 
            // panel8
            // 
            this.panel8.Controls.Add(this.txtTabPressureBrushSize);
            this.panel8.Controls.Add(this.spinTabPressureBrushSize);
            resources.ApplyResources(this.panel8, "panel8");
            this.panel8.Name = "panel8";
            // 
            // txtTabPressureBrushSize
            // 
            resources.ApplyResources(this.txtTabPressureBrushSize, "txtTabPressureBrushSize");
            this.txtTabPressureBrushSize.Name = "txtTabPressureBrushSize";
            // 
            // spinTabPressureBrushSize
            // 
            resources.ApplyResources(this.spinTabPressureBrushSize, "spinTabPressureBrushSize");
            this.spinTabPressureBrushSize.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.spinTabPressureBrushSize.Minimum = new decimal(new int[] {
            1000,
            0,
            0,
            -2147483648});
            this.spinTabPressureBrushSize.Name = "spinTabPressureBrushSize";
            // 
            // cmbxTabPressureBrushSize
            // 
            this.cmbxTabPressureBrushSize.BackColor = System.Drawing.Color.White;
            this.cmbxTabPressureBrushSize.DisplayMember = "DisplayMember";
            this.cmbxTabPressureBrushSize.DropDownHeight = 140;
            this.cmbxTabPressureBrushSize.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbxTabPressureBrushSize.DropDownWidth = 20;
            resources.ApplyResources(this.cmbxTabPressureBrushSize, "cmbxTabPressureBrushSize");
            this.cmbxTabPressureBrushSize.FormattingEnabled = true;
            this.cmbxTabPressureBrushSize.Name = "cmbxTabPressureBrushSize";
            this.cmbxTabPressureBrushSize.ValueMember = "ValueMember";
            this.cmbxTabPressureBrushSize.MouseHover += new System.EventHandler(this.CmbxTabPressure_MouseHover);
            // 
            // panelTabPressureBrushRotation
            // 
            resources.ApplyResources(this.panelTabPressureBrushRotation, "panelTabPressureBrushRotation");
            this.panelTabPressureBrushRotation.Controls.Add(this.panel2);
            this.panelTabPressureBrushRotation.Controls.Add(this.cmbxTabPressureBrushRotation);
            this.panelTabPressureBrushRotation.Name = "panelTabPressureBrushRotation";
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.txtTabPressureBrushRotation);
            this.panel2.Controls.Add(this.spinTabPressureBrushRotation);
            resources.ApplyResources(this.panel2, "panel2");
            this.panel2.Name = "panel2";
            // 
            // txtTabPressureBrushRotation
            // 
            resources.ApplyResources(this.txtTabPressureBrushRotation, "txtTabPressureBrushRotation");
            this.txtTabPressureBrushRotation.Name = "txtTabPressureBrushRotation";
            // 
            // spinTabPressureBrushRotation
            // 
            resources.ApplyResources(this.spinTabPressureBrushRotation, "spinTabPressureBrushRotation");
            this.spinTabPressureBrushRotation.Maximum = new decimal(new int[] {
            180,
            0,
            0,
            0});
            this.spinTabPressureBrushRotation.Minimum = new decimal(new int[] {
            180,
            0,
            0,
            -2147483648});
            this.spinTabPressureBrushRotation.Name = "spinTabPressureBrushRotation";
            // 
            // cmbxTabPressureBrushRotation
            // 
            this.cmbxTabPressureBrushRotation.BackColor = System.Drawing.Color.White;
            this.cmbxTabPressureBrushRotation.DisplayMember = "DisplayMember";
            this.cmbxTabPressureBrushRotation.DropDownHeight = 140;
            this.cmbxTabPressureBrushRotation.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbxTabPressureBrushRotation.DropDownWidth = 20;
            resources.ApplyResources(this.cmbxTabPressureBrushRotation, "cmbxTabPressureBrushRotation");
            this.cmbxTabPressureBrushRotation.FormattingEnabled = true;
            this.cmbxTabPressureBrushRotation.Name = "cmbxTabPressureBrushRotation";
            this.cmbxTabPressureBrushRotation.ValueMember = "ValueMember";
            this.cmbxTabPressureBrushRotation.MouseHover += new System.EventHandler(this.CmbxTabPressure_MouseHover);
            // 
            // panelTabPressureMinDrawDistance
            // 
            resources.ApplyResources(this.panelTabPressureMinDrawDistance, "panelTabPressureMinDrawDistance");
            this.panelTabPressureMinDrawDistance.Controls.Add(this.panel1);
            this.panelTabPressureMinDrawDistance.Controls.Add(this.cmbxTabPressureMinDrawDistance);
            this.panelTabPressureMinDrawDistance.Name = "panelTabPressureMinDrawDistance";
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.lblTabPressureMinDrawDistance);
            this.panel1.Controls.Add(this.spinTabPressureMinDrawDistance);
            resources.ApplyResources(this.panel1, "panel1");
            this.panel1.Name = "panel1";
            // 
            // lblTabPressureMinDrawDistance
            // 
            resources.ApplyResources(this.lblTabPressureMinDrawDistance, "lblTabPressureMinDrawDistance");
            this.lblTabPressureMinDrawDistance.Name = "lblTabPressureMinDrawDistance";
            // 
            // spinTabPressureMinDrawDistance
            // 
            resources.ApplyResources(this.spinTabPressureMinDrawDistance, "spinTabPressureMinDrawDistance");
            this.spinTabPressureMinDrawDistance.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            this.spinTabPressureMinDrawDistance.Name = "spinTabPressureMinDrawDistance";
            // 
            // cmbxTabPressureMinDrawDistance
            // 
            this.cmbxTabPressureMinDrawDistance.BackColor = System.Drawing.Color.White;
            this.cmbxTabPressureMinDrawDistance.DisplayMember = "DisplayMember";
            this.cmbxTabPressureMinDrawDistance.DropDownHeight = 140;
            this.cmbxTabPressureMinDrawDistance.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbxTabPressureMinDrawDistance.DropDownWidth = 20;
            resources.ApplyResources(this.cmbxTabPressureMinDrawDistance, "cmbxTabPressureMinDrawDistance");
            this.cmbxTabPressureMinDrawDistance.FormattingEnabled = true;
            this.cmbxTabPressureMinDrawDistance.Name = "cmbxTabPressureMinDrawDistance";
            this.cmbxTabPressureMinDrawDistance.ValueMember = "ValueMember";
            this.cmbxTabPressureMinDrawDistance.MouseHover += new System.EventHandler(this.CmbxTabPressure_MouseHover);
            // 
            // panelTabPressureBrushDensity
            // 
            resources.ApplyResources(this.panelTabPressureBrushDensity, "panelTabPressureBrushDensity");
            this.panelTabPressureBrushDensity.Controls.Add(this.panel4);
            this.panelTabPressureBrushDensity.Controls.Add(this.cmbxTabPressureBrushDensity);
            this.panelTabPressureBrushDensity.Name = "panelTabPressureBrushDensity";
            // 
            // panel4
            // 
            this.panel4.Controls.Add(this.lblTabPressureBrushDensity);
            this.panel4.Controls.Add(this.spinTabPressureBrushDensity);
            resources.ApplyResources(this.panel4, "panel4");
            this.panel4.Name = "panel4";
            // 
            // lblTabPressureBrushDensity
            // 
            resources.ApplyResources(this.lblTabPressureBrushDensity, "lblTabPressureBrushDensity");
            this.lblTabPressureBrushDensity.Name = "lblTabPressureBrushDensity";
            // 
            // spinTabPressureBrushDensity
            // 
            resources.ApplyResources(this.spinTabPressureBrushDensity, "spinTabPressureBrushDensity");
            this.spinTabPressureBrushDensity.Maximum = new decimal(new int[] {
            50,
            0,
            0,
            0});
            this.spinTabPressureBrushDensity.Minimum = new decimal(new int[] {
            50,
            0,
            0,
            -2147483648});
            this.spinTabPressureBrushDensity.Name = "spinTabPressureBrushDensity";
            // 
            // cmbxTabPressureBrushDensity
            // 
            this.cmbxTabPressureBrushDensity.BackColor = System.Drawing.Color.White;
            this.cmbxTabPressureBrushDensity.DisplayMember = "DisplayMember";
            this.cmbxTabPressureBrushDensity.DropDownHeight = 140;
            this.cmbxTabPressureBrushDensity.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbxTabPressureBrushDensity.DropDownWidth = 20;
            resources.ApplyResources(this.cmbxTabPressureBrushDensity, "cmbxTabPressureBrushDensity");
            this.cmbxTabPressureBrushDensity.FormattingEnabled = true;
            this.cmbxTabPressureBrushDensity.Name = "cmbxTabPressureBrushDensity";
            this.cmbxTabPressureBrushDensity.ValueMember = "ValueMember";
            this.cmbxTabPressureBrushDensity.MouseHover += new System.EventHandler(this.CmbxTabPressure_MouseHover);
            // 
            // panelTabPressureRandMinSize
            // 
            resources.ApplyResources(this.panelTabPressureRandMinSize, "panelTabPressureRandMinSize");
            this.panelTabPressureRandMinSize.Controls.Add(this.panel5);
            this.panelTabPressureRandMinSize.Controls.Add(this.cmbxTabPressureRandMinSize);
            this.panelTabPressureRandMinSize.Name = "panelTabPressureRandMinSize";
            // 
            // panel5
            // 
            this.panel5.Controls.Add(this.lblTabPressureRandMinSize);
            this.panel5.Controls.Add(this.spinTabPressureRandMinSize);
            resources.ApplyResources(this.panel5, "panel5");
            this.panel5.Name = "panel5";
            // 
            // lblTabPressureRandMinSize
            // 
            resources.ApplyResources(this.lblTabPressureRandMinSize, "lblTabPressureRandMinSize");
            this.lblTabPressureRandMinSize.Name = "lblTabPressureRandMinSize";
            // 
            // spinTabPressureRandMinSize
            // 
            resources.ApplyResources(this.spinTabPressureRandMinSize, "spinTabPressureRandMinSize");
            this.spinTabPressureRandMinSize.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.spinTabPressureRandMinSize.Minimum = new decimal(new int[] {
            1000,
            0,
            0,
            -2147483648});
            this.spinTabPressureRandMinSize.Name = "spinTabPressureRandMinSize";
            // 
            // cmbxTabPressureRandMinSize
            // 
            this.cmbxTabPressureRandMinSize.BackColor = System.Drawing.Color.White;
            this.cmbxTabPressureRandMinSize.DisplayMember = "DisplayMember";
            this.cmbxTabPressureRandMinSize.DropDownHeight = 140;
            this.cmbxTabPressureRandMinSize.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbxTabPressureRandMinSize.DropDownWidth = 20;
            resources.ApplyResources(this.cmbxTabPressureRandMinSize, "cmbxTabPressureRandMinSize");
            this.cmbxTabPressureRandMinSize.FormattingEnabled = true;
            this.cmbxTabPressureRandMinSize.Name = "cmbxTabPressureRandMinSize";
            this.cmbxTabPressureRandMinSize.ValueMember = "ValueMember";
            this.cmbxTabPressureRandMinSize.MouseHover += new System.EventHandler(this.CmbxTabPressure_MouseHover);
            // 
            // panelTabPressureRandMaxSize
            // 
            resources.ApplyResources(this.panelTabPressureRandMaxSize, "panelTabPressureRandMaxSize");
            this.panelTabPressureRandMaxSize.Controls.Add(this.panel6);
            this.panelTabPressureRandMaxSize.Controls.Add(this.cmbxTabPressureRandMaxSize);
            this.panelTabPressureRandMaxSize.Name = "panelTabPressureRandMaxSize";
            // 
            // panel6
            // 
            this.panel6.Controls.Add(this.lblTabPressureRandMaxSize);
            this.panel6.Controls.Add(this.spinTabPressureRandMaxSize);
            resources.ApplyResources(this.panel6, "panel6");
            this.panel6.Name = "panel6";
            // 
            // lblTabPressureRandMaxSize
            // 
            resources.ApplyResources(this.lblTabPressureRandMaxSize, "lblTabPressureRandMaxSize");
            this.lblTabPressureRandMaxSize.Name = "lblTabPressureRandMaxSize";
            // 
            // spinTabPressureRandMaxSize
            // 
            resources.ApplyResources(this.spinTabPressureRandMaxSize, "spinTabPressureRandMaxSize");
            this.spinTabPressureRandMaxSize.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.spinTabPressureRandMaxSize.Minimum = new decimal(new int[] {
            1000,
            0,
            0,
            -2147483648});
            this.spinTabPressureRandMaxSize.Name = "spinTabPressureRandMaxSize";
            // 
            // cmbxTabPressureRandMaxSize
            // 
            this.cmbxTabPressureRandMaxSize.BackColor = System.Drawing.Color.White;
            this.cmbxTabPressureRandMaxSize.DisplayMember = "DisplayMember";
            this.cmbxTabPressureRandMaxSize.DropDownHeight = 140;
            this.cmbxTabPressureRandMaxSize.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbxTabPressureRandMaxSize.DropDownWidth = 20;
            resources.ApplyResources(this.cmbxTabPressureRandMaxSize, "cmbxTabPressureRandMaxSize");
            this.cmbxTabPressureRandMaxSize.FormattingEnabled = true;
            this.cmbxTabPressureRandMaxSize.Name = "cmbxTabPressureRandMaxSize";
            this.cmbxTabPressureRandMaxSize.ValueMember = "ValueMember";
            this.cmbxTabPressureRandMaxSize.MouseHover += new System.EventHandler(this.CmbxTabPressure_MouseHover);
            // 
            // panelTabPressureRandRotLeft
            // 
            resources.ApplyResources(this.panelTabPressureRandRotLeft, "panelTabPressureRandRotLeft");
            this.panelTabPressureRandRotLeft.Controls.Add(this.panel7);
            this.panelTabPressureRandRotLeft.Controls.Add(this.cmbxTabPressureRandRotLeft);
            this.panelTabPressureRandRotLeft.Name = "panelTabPressureRandRotLeft";
            // 
            // panel7
            // 
            this.panel7.Controls.Add(this.lblTabPressureRandRotLeft);
            this.panel7.Controls.Add(this.spinTabPressureRandRotLeft);
            resources.ApplyResources(this.panel7, "panel7");
            this.panel7.Name = "panel7";
            // 
            // lblTabPressureRandRotLeft
            // 
            resources.ApplyResources(this.lblTabPressureRandRotLeft, "lblTabPressureRandRotLeft");
            this.lblTabPressureRandRotLeft.Name = "lblTabPressureRandRotLeft";
            // 
            // spinTabPressureRandRotLeft
            // 
            resources.ApplyResources(this.spinTabPressureRandRotLeft, "spinTabPressureRandRotLeft");
            this.spinTabPressureRandRotLeft.Maximum = new decimal(new int[] {
            360,
            0,
            0,
            0});
            this.spinTabPressureRandRotLeft.Minimum = new decimal(new int[] {
            360,
            0,
            0,
            -2147483648});
            this.spinTabPressureRandRotLeft.Name = "spinTabPressureRandRotLeft";
            // 
            // cmbxTabPressureRandRotLeft
            // 
            this.cmbxTabPressureRandRotLeft.BackColor = System.Drawing.Color.White;
            this.cmbxTabPressureRandRotLeft.DisplayMember = "DisplayMember";
            this.cmbxTabPressureRandRotLeft.DropDownHeight = 140;
            this.cmbxTabPressureRandRotLeft.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbxTabPressureRandRotLeft.DropDownWidth = 20;
            resources.ApplyResources(this.cmbxTabPressureRandRotLeft, "cmbxTabPressureRandRotLeft");
            this.cmbxTabPressureRandRotLeft.FormattingEnabled = true;
            this.cmbxTabPressureRandRotLeft.Name = "cmbxTabPressureRandRotLeft";
            this.cmbxTabPressureRandRotLeft.ValueMember = "ValueMember";
            this.cmbxTabPressureRandRotLeft.MouseHover += new System.EventHandler(this.CmbxTabPressure_MouseHover);
            // 
            // panelTabPressureRandRotRight
            // 
            resources.ApplyResources(this.panelTabPressureRandRotRight, "panelTabPressureRandRotRight");
            this.panelTabPressureRandRotRight.Controls.Add(this.panel9);
            this.panelTabPressureRandRotRight.Controls.Add(this.cmbxTabPressureRandRotRight);
            this.panelTabPressureRandRotRight.Name = "panelTabPressureRandRotRight";
            // 
            // panel9
            // 
            this.panel9.Controls.Add(this.lblTabPressureRandRotRight);
            this.panel9.Controls.Add(this.spinTabPressureRandRotRight);
            resources.ApplyResources(this.panel9, "panel9");
            this.panel9.Name = "panel9";
            // 
            // lblTabPressureRandRotRight
            // 
            resources.ApplyResources(this.lblTabPressureRandRotRight, "lblTabPressureRandRotRight");
            this.lblTabPressureRandRotRight.Name = "lblTabPressureRandRotRight";
            // 
            // spinTabPressureRandRotRight
            // 
            resources.ApplyResources(this.spinTabPressureRandRotRight, "spinTabPressureRandRotRight");
            this.spinTabPressureRandRotRight.Maximum = new decimal(new int[] {
            360,
            0,
            0,
            0});
            this.spinTabPressureRandRotRight.Minimum = new decimal(new int[] {
            360,
            0,
            0,
            -2147483648});
            this.spinTabPressureRandRotRight.Name = "spinTabPressureRandRotRight";
            // 
            // cmbxTabPressureRandRotRight
            // 
            this.cmbxTabPressureRandRotRight.BackColor = System.Drawing.Color.White;
            this.cmbxTabPressureRandRotRight.DisplayMember = "DisplayMember";
            this.cmbxTabPressureRandRotRight.DropDownHeight = 140;
            this.cmbxTabPressureRandRotRight.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbxTabPressureRandRotRight.DropDownWidth = 20;
            resources.ApplyResources(this.cmbxTabPressureRandRotRight, "cmbxTabPressureRandRotRight");
            this.cmbxTabPressureRandRotRight.FormattingEnabled = true;
            this.cmbxTabPressureRandRotRight.Name = "cmbxTabPressureRandRotRight";
            this.cmbxTabPressureRandRotRight.ValueMember = "ValueMember";
            this.cmbxTabPressureRandRotRight.MouseHover += new System.EventHandler(this.CmbxTabPressure_MouseHover);
            // 
            // panelTabPressureRandMinAlpha
            // 
            resources.ApplyResources(this.panelTabPressureRandMinAlpha, "panelTabPressureRandMinAlpha");
            this.panelTabPressureRandMinAlpha.Controls.Add(this.panel10);
            this.panelTabPressureRandMinAlpha.Controls.Add(this.cmbxTabPressureRandMinAlpha);
            this.panelTabPressureRandMinAlpha.Name = "panelTabPressureRandMinAlpha";
            // 
            // panel10
            // 
            this.panel10.Controls.Add(this.lblTabPressureRandMinAlpha);
            this.panel10.Controls.Add(this.spinTabPressureRandMinAlpha);
            resources.ApplyResources(this.panel10, "panel10");
            this.panel10.Name = "panel10";
            // 
            // lblTabPressureRandMinAlpha
            // 
            resources.ApplyResources(this.lblTabPressureRandMinAlpha, "lblTabPressureRandMinAlpha");
            this.lblTabPressureRandMinAlpha.Name = "lblTabPressureRandMinAlpha";
            // 
            // spinTabPressureRandMinAlpha
            // 
            resources.ApplyResources(this.spinTabPressureRandMinAlpha, "spinTabPressureRandMinAlpha");
            this.spinTabPressureRandMinAlpha.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            this.spinTabPressureRandMinAlpha.Name = "spinTabPressureRandMinAlpha";
            // 
            // cmbxTabPressureRandMinAlpha
            // 
            this.cmbxTabPressureRandMinAlpha.BackColor = System.Drawing.Color.White;
            this.cmbxTabPressureRandMinAlpha.DisplayMember = "DisplayMember";
            this.cmbxTabPressureRandMinAlpha.DropDownHeight = 140;
            this.cmbxTabPressureRandMinAlpha.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbxTabPressureRandMinAlpha.DropDownWidth = 20;
            resources.ApplyResources(this.cmbxTabPressureRandMinAlpha, "cmbxTabPressureRandMinAlpha");
            this.cmbxTabPressureRandMinAlpha.FormattingEnabled = true;
            this.cmbxTabPressureRandMinAlpha.Name = "cmbxTabPressureRandMinAlpha";
            this.cmbxTabPressureRandMinAlpha.ValueMember = "ValueMember";
            this.cmbxTabPressureRandMinAlpha.MouseHover += new System.EventHandler(this.CmbxTabPressure_MouseHover);
            // 
            // panelTabPressureRandHorShift
            // 
            resources.ApplyResources(this.panelTabPressureRandHorShift, "panelTabPressureRandHorShift");
            this.panelTabPressureRandHorShift.Controls.Add(this.panel12);
            this.panelTabPressureRandHorShift.Controls.Add(this.cmbxTabPressureRandHorShift);
            this.panelTabPressureRandHorShift.Name = "panelTabPressureRandHorShift";
            // 
            // panel12
            // 
            this.panel12.Controls.Add(this.lblTabPressureRandHorShift);
            this.panel12.Controls.Add(this.spinTabPressureRandHorShift);
            resources.ApplyResources(this.panel12, "panel12");
            this.panel12.Name = "panel12";
            // 
            // lblTabPressureRandHorShift
            // 
            resources.ApplyResources(this.lblTabPressureRandHorShift, "lblTabPressureRandHorShift");
            this.lblTabPressureRandHorShift.Name = "lblTabPressureRandHorShift";
            // 
            // spinTabPressureRandHorShift
            // 
            resources.ApplyResources(this.spinTabPressureRandHorShift, "spinTabPressureRandHorShift");
            this.spinTabPressureRandHorShift.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            this.spinTabPressureRandHorShift.Name = "spinTabPressureRandHorShift";
            // 
            // cmbxTabPressureRandHorShift
            // 
            this.cmbxTabPressureRandHorShift.BackColor = System.Drawing.Color.White;
            this.cmbxTabPressureRandHorShift.DisplayMember = "DisplayMember";
            this.cmbxTabPressureRandHorShift.DropDownHeight = 140;
            this.cmbxTabPressureRandHorShift.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbxTabPressureRandHorShift.DropDownWidth = 20;
            resources.ApplyResources(this.cmbxTabPressureRandHorShift, "cmbxTabPressureRandHorShift");
            this.cmbxTabPressureRandHorShift.FormattingEnabled = true;
            this.cmbxTabPressureRandHorShift.Name = "cmbxTabPressureRandHorShift";
            this.cmbxTabPressureRandHorShift.ValueMember = "ValueMember";
            this.cmbxTabPressureRandHorShift.MouseHover += new System.EventHandler(this.CmbxTabPressure_MouseHover);
            // 
            // panelTabPressureRandVerShift
            // 
            resources.ApplyResources(this.panelTabPressureRandVerShift, "panelTabPressureRandVerShift");
            this.panelTabPressureRandVerShift.Controls.Add(this.panel11);
            this.panelTabPressureRandVerShift.Controls.Add(this.cmbxTabPressureRandVerShift);
            this.panelTabPressureRandVerShift.Name = "panelTabPressureRandVerShift";
            // 
            // panel11
            // 
            this.panel11.Controls.Add(this.lblTabPressureRandVerShift);
            this.panel11.Controls.Add(this.spinTabPressureRandVerShift);
            resources.ApplyResources(this.panel11, "panel11");
            this.panel11.Name = "panel11";
            // 
            // lblTabPressureRandVerShift
            // 
            resources.ApplyResources(this.lblTabPressureRandVerShift, "lblTabPressureRandVerShift");
            this.lblTabPressureRandVerShift.Name = "lblTabPressureRandVerShift";
            // 
            // spinTabPressureRandVerShift
            // 
            resources.ApplyResources(this.spinTabPressureRandVerShift, "spinTabPressureRandVerShift");
            this.spinTabPressureRandVerShift.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            this.spinTabPressureRandVerShift.Name = "spinTabPressureRandVerShift";
            // 
            // cmbxTabPressureRandVerShift
            // 
            this.cmbxTabPressureRandVerShift.BackColor = System.Drawing.Color.White;
            this.cmbxTabPressureRandVerShift.DisplayMember = "DisplayMember";
            this.cmbxTabPressureRandVerShift.DropDownHeight = 140;
            this.cmbxTabPressureRandVerShift.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbxTabPressureRandVerShift.DropDownWidth = 20;
            resources.ApplyResources(this.cmbxTabPressureRandVerShift, "cmbxTabPressureRandVerShift");
            this.cmbxTabPressureRandVerShift.FormattingEnabled = true;
            this.cmbxTabPressureRandVerShift.Name = "cmbxTabPressureRandVerShift";
            this.cmbxTabPressureRandVerShift.ValueMember = "ValueMember";
            this.cmbxTabPressureRandVerShift.MouseHover += new System.EventHandler(this.CmbxTabPressure_MouseHover);
            // 
            // panelTabPressureRedJitter
            // 
            resources.ApplyResources(this.panelTabPressureRedJitter, "panelTabPressureRedJitter");
            this.panelTabPressureRedJitter.Controls.Add(this.panel13);
            this.panelTabPressureRedJitter.Controls.Add(this.cmbxTabPressureRedJitter);
            this.panelTabPressureRedJitter.Name = "panelTabPressureRedJitter";
            // 
            // panel13
            // 
            this.panel13.Controls.Add(this.spinTabPressureMinRedJitter);
            this.panel13.Controls.Add(this.lblTabPressureRedJitter);
            this.panel13.Controls.Add(this.spinTabPressureMaxRedJitter);
            resources.ApplyResources(this.panel13, "panel13");
            this.panel13.Name = "panel13";
            // 
            // spinTabPressureMinRedJitter
            // 
            resources.ApplyResources(this.spinTabPressureMinRedJitter, "spinTabPressureMinRedJitter");
            this.spinTabPressureMinRedJitter.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            this.spinTabPressureMinRedJitter.Name = "spinTabPressureMinRedJitter";
            // 
            // lblTabPressureRedJitter
            // 
            resources.ApplyResources(this.lblTabPressureRedJitter, "lblTabPressureRedJitter");
            this.lblTabPressureRedJitter.Name = "lblTabPressureRedJitter";
            // 
            // spinTabPressureMaxRedJitter
            // 
            resources.ApplyResources(this.spinTabPressureMaxRedJitter, "spinTabPressureMaxRedJitter");
            this.spinTabPressureMaxRedJitter.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            this.spinTabPressureMaxRedJitter.Name = "spinTabPressureMaxRedJitter";
            // 
            // cmbxTabPressureRedJitter
            // 
            this.cmbxTabPressureRedJitter.BackColor = System.Drawing.Color.White;
            this.cmbxTabPressureRedJitter.DisplayMember = "DisplayMember";
            this.cmbxTabPressureRedJitter.DropDownHeight = 140;
            this.cmbxTabPressureRedJitter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbxTabPressureRedJitter.DropDownWidth = 20;
            resources.ApplyResources(this.cmbxTabPressureRedJitter, "cmbxTabPressureRedJitter");
            this.cmbxTabPressureRedJitter.FormattingEnabled = true;
            this.cmbxTabPressureRedJitter.Name = "cmbxTabPressureRedJitter";
            this.cmbxTabPressureRedJitter.ValueMember = "ValueMember";
            this.cmbxTabPressureRedJitter.MouseHover += new System.EventHandler(this.CmbxTabPressure_MouseHover);
            // 
            // panelTabPressureGreenJitter
            // 
            resources.ApplyResources(this.panelTabPressureGreenJitter, "panelTabPressureGreenJitter");
            this.panelTabPressureGreenJitter.Controls.Add(this.panel14);
            this.panelTabPressureGreenJitter.Controls.Add(this.cmbxTabPressureGreenJitter);
            this.panelTabPressureGreenJitter.Name = "panelTabPressureGreenJitter";
            // 
            // panel14
            // 
            this.panel14.Controls.Add(this.spinTabPressureMinGreenJitter);
            this.panel14.Controls.Add(this.lblTabPressureGreenJitter);
            this.panel14.Controls.Add(this.spinTabPressureMaxGreenJitter);
            resources.ApplyResources(this.panel14, "panel14");
            this.panel14.Name = "panel14";
            // 
            // spinTabPressureMinGreenJitter
            // 
            resources.ApplyResources(this.spinTabPressureMinGreenJitter, "spinTabPressureMinGreenJitter");
            this.spinTabPressureMinGreenJitter.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            this.spinTabPressureMinGreenJitter.Name = "spinTabPressureMinGreenJitter";
            // 
            // lblTabPressureGreenJitter
            // 
            resources.ApplyResources(this.lblTabPressureGreenJitter, "lblTabPressureGreenJitter");
            this.lblTabPressureGreenJitter.Name = "lblTabPressureGreenJitter";
            // 
            // spinTabPressureMaxGreenJitter
            // 
            resources.ApplyResources(this.spinTabPressureMaxGreenJitter, "spinTabPressureMaxGreenJitter");
            this.spinTabPressureMaxGreenJitter.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            this.spinTabPressureMaxGreenJitter.Name = "spinTabPressureMaxGreenJitter";
            // 
            // cmbxTabPressureGreenJitter
            // 
            this.cmbxTabPressureGreenJitter.BackColor = System.Drawing.Color.White;
            this.cmbxTabPressureGreenJitter.DisplayMember = "DisplayMember";
            this.cmbxTabPressureGreenJitter.DropDownHeight = 140;
            this.cmbxTabPressureGreenJitter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbxTabPressureGreenJitter.DropDownWidth = 20;
            resources.ApplyResources(this.cmbxTabPressureGreenJitter, "cmbxTabPressureGreenJitter");
            this.cmbxTabPressureGreenJitter.FormattingEnabled = true;
            this.cmbxTabPressureGreenJitter.Name = "cmbxTabPressureGreenJitter";
            this.cmbxTabPressureGreenJitter.ValueMember = "ValueMember";
            this.cmbxTabPressureGreenJitter.MouseHover += new System.EventHandler(this.CmbxTabPressure_MouseHover);
            // 
            // panelTabPressureBlueJitter
            // 
            resources.ApplyResources(this.panelTabPressureBlueJitter, "panelTabPressureBlueJitter");
            this.panelTabPressureBlueJitter.Controls.Add(this.panel15);
            this.panelTabPressureBlueJitter.Controls.Add(this.cmbxTabPressureBlueJitter);
            this.panelTabPressureBlueJitter.Name = "panelTabPressureBlueJitter";
            // 
            // panel15
            // 
            this.panel15.Controls.Add(this.spinTabPressureMinBlueJitter);
            this.panel15.Controls.Add(this.lblTabPressureBlueJitter);
            this.panel15.Controls.Add(this.spinTabPressureMaxBlueJitter);
            resources.ApplyResources(this.panel15, "panel15");
            this.panel15.Name = "panel15";
            // 
            // spinTabPressureMinBlueJitter
            // 
            resources.ApplyResources(this.spinTabPressureMinBlueJitter, "spinTabPressureMinBlueJitter");
            this.spinTabPressureMinBlueJitter.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            this.spinTabPressureMinBlueJitter.Name = "spinTabPressureMinBlueJitter";
            // 
            // lblTabPressureBlueJitter
            // 
            resources.ApplyResources(this.lblTabPressureBlueJitter, "lblTabPressureBlueJitter");
            this.lblTabPressureBlueJitter.Name = "lblTabPressureBlueJitter";
            // 
            // spinTabPressureMaxBlueJitter
            // 
            resources.ApplyResources(this.spinTabPressureMaxBlueJitter, "spinTabPressureMaxBlueJitter");
            this.spinTabPressureMaxBlueJitter.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            this.spinTabPressureMaxBlueJitter.Name = "spinTabPressureMaxBlueJitter";
            // 
            // cmbxTabPressureBlueJitter
            // 
            this.cmbxTabPressureBlueJitter.BackColor = System.Drawing.Color.White;
            this.cmbxTabPressureBlueJitter.DisplayMember = "DisplayMember";
            this.cmbxTabPressureBlueJitter.DropDownHeight = 140;
            this.cmbxTabPressureBlueJitter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbxTabPressureBlueJitter.DropDownWidth = 20;
            resources.ApplyResources(this.cmbxTabPressureBlueJitter, "cmbxTabPressureBlueJitter");
            this.cmbxTabPressureBlueJitter.FormattingEnabled = true;
            this.cmbxTabPressureBlueJitter.Name = "cmbxTabPressureBlueJitter";
            this.cmbxTabPressureBlueJitter.ValueMember = "ValueMember";
            this.cmbxTabPressureBlueJitter.MouseHover += new System.EventHandler(this.CmbxTabPressure_MouseHover);
            // 
            // panelTabPressureHueJitter
            // 
            resources.ApplyResources(this.panelTabPressureHueJitter, "panelTabPressureHueJitter");
            this.panelTabPressureHueJitter.Controls.Add(this.panel16);
            this.panelTabPressureHueJitter.Controls.Add(this.cmbxTabPressureHueJitter);
            this.panelTabPressureHueJitter.Name = "panelTabPressureHueJitter";
            // 
            // panel16
            // 
            this.panel16.Controls.Add(this.spinTabPressureMinHueJitter);
            this.panel16.Controls.Add(this.lblTabPressureHueJitter);
            this.panel16.Controls.Add(this.spinTabPressureMaxHueJitter);
            resources.ApplyResources(this.panel16, "panel16");
            this.panel16.Name = "panel16";
            // 
            // spinTabPressureMinHueJitter
            // 
            resources.ApplyResources(this.spinTabPressureMinHueJitter, "spinTabPressureMinHueJitter");
            this.spinTabPressureMinHueJitter.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            this.spinTabPressureMinHueJitter.Name = "spinTabPressureMinHueJitter";
            // 
            // lblTabPressureHueJitter
            // 
            resources.ApplyResources(this.lblTabPressureHueJitter, "lblTabPressureHueJitter");
            this.lblTabPressureHueJitter.Name = "lblTabPressureHueJitter";
            // 
            // spinTabPressureMaxHueJitter
            // 
            resources.ApplyResources(this.spinTabPressureMaxHueJitter, "spinTabPressureMaxHueJitter");
            this.spinTabPressureMaxHueJitter.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            this.spinTabPressureMaxHueJitter.Name = "spinTabPressureMaxHueJitter";
            // 
            // cmbxTabPressureHueJitter
            // 
            this.cmbxTabPressureHueJitter.BackColor = System.Drawing.Color.White;
            this.cmbxTabPressureHueJitter.DisplayMember = "DisplayMember";
            this.cmbxTabPressureHueJitter.DropDownHeight = 140;
            this.cmbxTabPressureHueJitter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbxTabPressureHueJitter.DropDownWidth = 20;
            resources.ApplyResources(this.cmbxTabPressureHueJitter, "cmbxTabPressureHueJitter");
            this.cmbxTabPressureHueJitter.FormattingEnabled = true;
            this.cmbxTabPressureHueJitter.Name = "cmbxTabPressureHueJitter";
            this.cmbxTabPressureHueJitter.ValueMember = "ValueMember";
            this.cmbxTabPressureHueJitter.MouseHover += new System.EventHandler(this.CmbxTabPressure_MouseHover);
            // 
            // panelTabPressureSatJitter
            // 
            resources.ApplyResources(this.panelTabPressureSatJitter, "panelTabPressureSatJitter");
            this.panelTabPressureSatJitter.Controls.Add(this.panel17);
            this.panelTabPressureSatJitter.Controls.Add(this.cmbxTabPressureSatJitter);
            this.panelTabPressureSatJitter.Name = "panelTabPressureSatJitter";
            // 
            // panel17
            // 
            this.panel17.Controls.Add(this.spinTabPressureMinSatJitter);
            this.panel17.Controls.Add(this.lblTabPressureSatJitter);
            this.panel17.Controls.Add(this.spinTabPressureMaxSatJitter);
            resources.ApplyResources(this.panel17, "panel17");
            this.panel17.Name = "panel17";
            // 
            // spinTabPressureMinSatJitter
            // 
            resources.ApplyResources(this.spinTabPressureMinSatJitter, "spinTabPressureMinSatJitter");
            this.spinTabPressureMinSatJitter.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            this.spinTabPressureMinSatJitter.Name = "spinTabPressureMinSatJitter";
            // 
            // lblTabPressureSatJitter
            // 
            resources.ApplyResources(this.lblTabPressureSatJitter, "lblTabPressureSatJitter");
            this.lblTabPressureSatJitter.Name = "lblTabPressureSatJitter";
            // 
            // spinTabPressureMaxSatJitter
            // 
            resources.ApplyResources(this.spinTabPressureMaxSatJitter, "spinTabPressureMaxSatJitter");
            this.spinTabPressureMaxSatJitter.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            this.spinTabPressureMaxSatJitter.Name = "spinTabPressureMaxSatJitter";
            // 
            // cmbxTabPressureSatJitter
            // 
            this.cmbxTabPressureSatJitter.BackColor = System.Drawing.Color.White;
            this.cmbxTabPressureSatJitter.DisplayMember = "DisplayMember";
            this.cmbxTabPressureSatJitter.DropDownHeight = 140;
            this.cmbxTabPressureSatJitter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbxTabPressureSatJitter.DropDownWidth = 20;
            resources.ApplyResources(this.cmbxTabPressureSatJitter, "cmbxTabPressureSatJitter");
            this.cmbxTabPressureSatJitter.FormattingEnabled = true;
            this.cmbxTabPressureSatJitter.Name = "cmbxTabPressureSatJitter";
            this.cmbxTabPressureSatJitter.ValueMember = "ValueMember";
            this.cmbxTabPressureSatJitter.MouseHover += new System.EventHandler(this.CmbxTabPressure_MouseHover);
            // 
            // panelTabPressureValueJitter
            // 
            resources.ApplyResources(this.panelTabPressureValueJitter, "panelTabPressureValueJitter");
            this.panelTabPressureValueJitter.Controls.Add(this.panel18);
            this.panelTabPressureValueJitter.Controls.Add(this.cmbxTabPressureValueJitter);
            this.panelTabPressureValueJitter.Name = "panelTabPressureValueJitter";
            // 
            // panel18
            // 
            this.panel18.Controls.Add(this.spinTabPressureMinValueJitter);
            this.panel18.Controls.Add(this.lblTabPressureValueJitter);
            this.panel18.Controls.Add(this.spinTabPressureMaxValueJitter);
            resources.ApplyResources(this.panel18, "panel18");
            this.panel18.Name = "panel18";
            // 
            // spinTabPressureMinValueJitter
            // 
            resources.ApplyResources(this.spinTabPressureMinValueJitter, "spinTabPressureMinValueJitter");
            this.spinTabPressureMinValueJitter.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            this.spinTabPressureMinValueJitter.Name = "spinTabPressureMinValueJitter";
            // 
            // lblTabPressureValueJitter
            // 
            resources.ApplyResources(this.lblTabPressureValueJitter, "lblTabPressureValueJitter");
            this.lblTabPressureValueJitter.Name = "lblTabPressureValueJitter";
            // 
            // spinTabPressureMaxValueJitter
            // 
            resources.ApplyResources(this.spinTabPressureMaxValueJitter, "spinTabPressureMaxValueJitter");
            this.spinTabPressureMaxValueJitter.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            this.spinTabPressureMaxValueJitter.Name = "spinTabPressureMaxValueJitter";
            // 
            // cmbxTabPressureValueJitter
            // 
            this.cmbxTabPressureValueJitter.BackColor = System.Drawing.Color.White;
            this.cmbxTabPressureValueJitter.DisplayMember = "DisplayMember";
            this.cmbxTabPressureValueJitter.DropDownHeight = 140;
            this.cmbxTabPressureValueJitter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbxTabPressureValueJitter.DropDownWidth = 20;
            resources.ApplyResources(this.cmbxTabPressureValueJitter, "cmbxTabPressureValueJitter");
            this.cmbxTabPressureValueJitter.FormattingEnabled = true;
            this.cmbxTabPressureValueJitter.Name = "cmbxTabPressureValueJitter";
            this.cmbxTabPressureValueJitter.ValueMember = "ValueMember";
            this.cmbxTabPressureValueJitter.MouseHover += new System.EventHandler(this.CmbxTabPressure_MouseHover);
            // 
            // bttnSettings
            // 
            this.bttnSettings.BackColor = System.Drawing.Color.Black;
            this.bttnSettings.ForeColor = System.Drawing.Color.WhiteSmoke;
            resources.ApplyResources(this.bttnSettings, "bttnSettings");
            this.bttnSettings.Name = "bttnSettings";
            this.bttnSettings.UseVisualStyleBackColor = false;
            // 
            // panelSettings
            // 
            resources.ApplyResources(this.panelSettings, "panelSettings");
            this.panelSettings.BackColor = System.Drawing.SystemColors.Control;
            this.panelSettings.Controls.Add(this.bttnCustomBrushImageLocations);
            this.panelSettings.Controls.Add(this.bttnClearBrushImages);
            this.panelSettings.Controls.Add(this.bttnClearSettings);
            this.panelSettings.Controls.Add(this.bttnDeleteBrush);
            this.panelSettings.Controls.Add(this.bttnSaveBrush);
            this.panelSettings.Name = "panelSettings";
            // 
            // bttnCustomBrushImageLocations
            // 
            resources.ApplyResources(this.bttnCustomBrushImageLocations, "bttnCustomBrushImageLocations");
            this.bttnCustomBrushImageLocations.Name = "bttnCustomBrushImageLocations";
            this.bttnCustomBrushImageLocations.UseVisualStyleBackColor = true;
            this.bttnCustomBrushImageLocations.Click += new System.EventHandler(this.BttnPreferences_Click);
            this.bttnCustomBrushImageLocations.MouseEnter += new System.EventHandler(this.BttnPreferences_MouseEnter);
            // 
            // bttnClearBrushImages
            // 
            resources.ApplyResources(this.bttnClearBrushImages, "bttnClearBrushImages");
            this.bttnClearBrushImages.Name = "bttnClearBrushImages";
            this.bttnClearBrushImages.UseVisualStyleBackColor = true;
            this.bttnClearBrushImages.Click += new System.EventHandler(this.BttnClearBrushImages_Click);
            this.bttnClearBrushImages.MouseEnter += new System.EventHandler(this.BttnClearBrushImages_MouseEnter);
            // 
            // bttnClearSettings
            // 
            resources.ApplyResources(this.bttnClearSettings, "bttnClearSettings");
            this.bttnClearSettings.Name = "bttnClearSettings";
            this.bttnClearSettings.UseVisualStyleBackColor = true;
            this.bttnClearSettings.Click += new System.EventHandler(this.BttnClearSettings_Click);
            this.bttnClearSettings.MouseEnter += new System.EventHandler(this.BttnClearSettings_MouseEnter);
            // 
            // bttnDeleteBrush
            // 
            resources.ApplyResources(this.bttnDeleteBrush, "bttnDeleteBrush");
            this.bttnDeleteBrush.Name = "bttnDeleteBrush";
            this.bttnDeleteBrush.UseVisualStyleBackColor = true;
            this.bttnDeleteBrush.Click += new System.EventHandler(this.BttnDeleteBrush_Click);
            this.bttnDeleteBrush.MouseEnter += new System.EventHandler(this.BttnDeleteBrush_MouseEnter);
            // 
            // bttnSaveBrush
            // 
            resources.ApplyResources(this.bttnSaveBrush, "bttnSaveBrush");
            this.bttnSaveBrush.Name = "bttnSaveBrush";
            this.bttnSaveBrush.UseVisualStyleBackColor = true;
            this.bttnSaveBrush.Click += new System.EventHandler(this.BttnSaveBrush_Click);
            this.bttnSaveBrush.MouseEnter += new System.EventHandler(this.BttnSaveBrush_MouseEnter);
            // 
            // WinBrushFactory
            // 
            this.AcceptButton = this.bttnOk;
            resources.ApplyResources(this, "$this");
            this.BackColor = System.Drawing.SystemColors.ControlLight;
            this.CancelButton = this.bttnCancel;
            this.Controls.Add(this.panelAllSettingsContainer);
            this.Controls.Add(this.displayCanvas);
            this.DoubleBuffered = true;
            this.KeyPreview = true;
            this.MaximizeBox = true;
            this.Name = "WinBrushFactory";
            this.displayCanvas.ResumeLayout(false);
            this.displayCanvas.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.displayCanvas)).EndInit();
            this.panelUndoRedoOkCancel.ResumeLayout(false);
            this.panelAllSettingsContainer.ResumeLayout(false);
            this.panelDockSettingsContainer.ResumeLayout(false);
            this.panelDockSettingsContainer.PerformLayout();
            this.flowLayoutPanel1.ResumeLayout(false);
            this.panelSettingsContainer.ResumeLayout(false);
            this.panelSettingsContainer.PerformLayout();
            this.panelBrush.ResumeLayout(false);
            this.panelBrush.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.sliderCanvasZoom)).EndInit();
            this.panelBrushAddPickColor.ResumeLayout(false);
            this.panelBrushAddPickColor.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.sliderBrushAlpha)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderBrushRotation)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderBrushSize)).EndInit();
            this.panelSpecialSettings.ResumeLayout(false);
            this.panelSpecialSettings.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.sliderMinDrawDistance)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderBrushDensity)).EndInit();
            this.panelJitterBasics.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandMinSize)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandMaxSize)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandRotLeft)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandRotRight)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandMinAlpha)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandHorzShift)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderRandVertShift)).EndInit();
            this.panelJitterColor.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMinRed)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMaxRed)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMinGreen)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMaxGreen)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMinBlue)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMaxBlue)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMinHue)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMaxHue)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMinSat)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMaxSat)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMinVal)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderJitterMaxVal)).EndInit();
            this.panelShiftBasics.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.sliderShiftSize)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderShiftRotation)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.sliderShiftAlpha)).EndInit();
            this.panelTabletAssignPressure.ResumeLayout(false);
            this.panelTabletAssignPressure.PerformLayout();
            this.panelTabPressureBrushAlpha.ResumeLayout(false);
            this.panel3.ResumeLayout(false);
            this.panel3.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureBrushAlpha)).EndInit();
            this.panelTabPressureBrushSize.ResumeLayout(false);
            this.panel8.ResumeLayout(false);
            this.panel8.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureBrushSize)).EndInit();
            this.panelTabPressureBrushRotation.ResumeLayout(false);
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureBrushRotation)).EndInit();
            this.panelTabPressureMinDrawDistance.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureMinDrawDistance)).EndInit();
            this.panelTabPressureBrushDensity.ResumeLayout(false);
            this.panel4.ResumeLayout(false);
            this.panel4.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureBrushDensity)).EndInit();
            this.panelTabPressureRandMinSize.ResumeLayout(false);
            this.panel5.ResumeLayout(false);
            this.panel5.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureRandMinSize)).EndInit();
            this.panelTabPressureRandMaxSize.ResumeLayout(false);
            this.panel6.ResumeLayout(false);
            this.panel6.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureRandMaxSize)).EndInit();
            this.panelTabPressureRandRotLeft.ResumeLayout(false);
            this.panel7.ResumeLayout(false);
            this.panel7.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureRandRotLeft)).EndInit();
            this.panelTabPressureRandRotRight.ResumeLayout(false);
            this.panel9.ResumeLayout(false);
            this.panel9.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureRandRotRight)).EndInit();
            this.panelTabPressureRandMinAlpha.ResumeLayout(false);
            this.panel10.ResumeLayout(false);
            this.panel10.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureRandMinAlpha)).EndInit();
            this.panelTabPressureRandHorShift.ResumeLayout(false);
            this.panel12.ResumeLayout(false);
            this.panel12.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureRandHorShift)).EndInit();
            this.panelTabPressureRandVerShift.ResumeLayout(false);
            this.panel11.ResumeLayout(false);
            this.panel11.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureRandVerShift)).EndInit();
            this.panelTabPressureRedJitter.ResumeLayout(false);
            this.panel13.ResumeLayout(false);
            this.panel13.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureMinRedJitter)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureMaxRedJitter)).EndInit();
            this.panelTabPressureGreenJitter.ResumeLayout(false);
            this.panel14.ResumeLayout(false);
            this.panel14.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureMinGreenJitter)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureMaxGreenJitter)).EndInit();
            this.panelTabPressureBlueJitter.ResumeLayout(false);
            this.panel15.ResumeLayout(false);
            this.panel15.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureMinBlueJitter)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureMaxBlueJitter)).EndInit();
            this.panelTabPressureHueJitter.ResumeLayout(false);
            this.panel16.ResumeLayout(false);
            this.panel16.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureMinHueJitter)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureMaxHueJitter)).EndInit();
            this.panelTabPressureSatJitter.ResumeLayout(false);
            this.panel17.ResumeLayout(false);
            this.panel17.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureMinSatJitter)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureMaxSatJitter)).EndInit();
            this.panelTabPressureValueJitter.ResumeLayout(false);
            this.panel18.ResumeLayout(false);
            this.panel18.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureMinValueJitter)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.spinTabPressureMaxValueJitter)).EndInit();
            this.panelSettings.ResumeLayout(false);
            this.ResumeLayout(false);

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

                    graphics.DrawImage(clipboardImage, 0, 0, background.Width, background.Height);
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
            ContextMenuStrip contextMenu = new ContextMenuStrip();

            //Options to set the background colors / image.
            contextMenu.Items.Add(new ToolStripMenuItem("Use transparent background",
                null,
                new EventHandler((a, b) =>
                {
                    displayCanvas.BackColor = Color.Transparent;
                    displayCanvas.BackgroundImageLayout = ImageLayout.Tile;
                    displayCanvas.BackgroundImage = Resources.CheckeredBg;
                })));
            contextMenu.Items.Add(new ToolStripMenuItem("Use white background",
                null,
                new EventHandler((a, b) =>
                {
                    displayCanvas.BackColor = Color.White;
                    displayCanvas.BackgroundImage = null;
                })));
            contextMenu.Items.Add(new ToolStripMenuItem("Use black background",
                null,
                new EventHandler((a, b) =>
                {
                    displayCanvas.BackColor = Color.Black;
                    displayCanvas.BackgroundImage = null;
                })));
            if (Clipboard.ContainsData("PNG"))
            {
                contextMenu.Items.Add(new ToolStripMenuItem("Use clipboard as background",
                    null,
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
                                MessageBox.Show(Strings.ClipboardErrorUnusable,
                                    Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            bttnToolBrush.BackColor = SystemColors.ButtonFace;
            BttnToolEraser.BackColor = SystemColors.ButtonFace;
            bttnColorPicker.BackColor = SystemColors.ButtonFace;
            bttnToolOrigin.BackColor = SystemColors.ButtonFace;

            switch (toolToSwitchTo)
            {
                case Tool.Eraser:
                    BttnToolEraser.BackColor = SystemColors.ButtonShadow;
                    displayCanvas.Cursor = Cursors.Default;
                    break;
                case Tool.Brush:
                    bttnToolBrush.BackColor = SystemColors.ButtonShadow;
                    displayCanvas.Cursor = Cursors.Default;
                    break;
                case Tool.SetSymmetryOrigin:
                    bttnToolOrigin.BackColor = SystemColors.ButtonShadow;
                    displayCanvas.Cursor = Cursors.Hand;
                    break;
                case Tool.ColorPicker:
                    bttnColorPicker.BackColor = SystemColors.ButtonShadow;

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

            // Prevents repeated switching to the same tool from changing the last tool (shouldn't match active tool).
            if (activeTool != toolToSwitchTo)
            {
                lastTool = activeTool;
            }

            activeTool = toolToSwitchTo;
        }

        /// <summary>
        /// Translates a point on the translated, rotated & scaled canvas into its coordinates relative to the untransformed
        /// canvas if it was displayed with its top-left corner at {0,0}.
        /// </summary>
        private PointF TransformPoint(
            PointF location,
            bool isAlreadyTranslated = false,
            bool isAlreadyZoomed = false,
            bool doClamp = true)
        {
            // Given a point relative to the canvas, such that {0,0} is the top-left corner and
            // {canvas width, canvas height} is the bottom right, first subtracts canvas x/y translation. This is for
            // when the mouse is clicked in the center of the canvas, where it's displayed. Subtracting the canvas x,y
            // repositions the coordinate range to fit {0, 0, canvas width, canvas height}.
            float locX = isAlreadyTranslated ? location.X : location.X - canvas.x;
            float locY = isAlreadyTranslated ? location.Y : location.Y - canvas.y;

            // Divides by canvas zoom factor.
            if (!isAlreadyZoomed)
            {
                locX /= canvasZoom;
                locY /= canvasZoom;
            }

            // Determines the point's offset from the center of the un-rotated image, then gets its representing vector.
            // Note that if a rotation origin other than center image is set, change width/2 and height/2 to match it.
            double deltaX = locX - bmpCurrentDrawing.Width / 2;
            double deltaY = locY - bmpCurrentDrawing.Height / 2;
            double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            double angle = Math.Atan2(deltaY, deltaX);

            // subtracts canvas rotation angle to get the coordinates rotated w.r.t. the canvas.
            angle -= canvasRotation * Math.PI / 180;

            if (doClamp)
            {
                return new PointF(
                    Utils.ClampF((float)(
                        bmpCurrentDrawing.Width / 2 + Math.Cos(angle) * distance), 0, bmpCurrentDrawing.Width),
                    Utils.ClampF((float)(
                        bmpCurrentDrawing.Height / 2 + Math.Sin(angle) * distance), 0, bmpCurrentDrawing.Height));
            }

            return new PointF(
                (float)(bmpCurrentDrawing.Width / 2 + Math.Cos(angle) * distance),
                (float)(bmpCurrentDrawing.Height / 2 + Math.Sin(angle) * distance));
        }
 
        /// <summary>
        /// Updates all settings based on the currently selected brush.
        /// </summary>
        private void UpdateBrush(BrushSettings settings)
        {
            // Whether the delete brush button is enabled or not.
            bttnDeleteBrush.Enabled = currentBrushName != null &&
                !PersistentSettings.defaultBrushes.ContainsKey(currentBrushName);

            //Copies GUI values from the settings.
            sliderBrushSize.Value = settings.BrushSize;
            tokenSelectedBrushImageName = settings.BrushImageName;

            //Sets all other fields.
            sliderBrushAlpha.Value = settings.BrushAlpha;
            sliderBrushDensity.Value = settings.BrushDensity;
            sliderBrushRotation.Value = settings.BrushRotation;
            sliderRandHorzShift.Value = settings.RandHorzShift;
            sliderRandMaxSize.Value = settings.RandMaxSize;
            sliderRandMinAlpha.Value = settings.RandMinAlpha;
            sliderRandMinSize.Value = settings.RandMinSize;
            sliderRandRotLeft.Value = settings.RandRotLeft;
            sliderRandRotRight.Value = settings.RandRotRight;
            sliderRandVertShift.Value = settings.RandVertShift;
            chkbxAutomaticBrushDensity.Checked = settings.AutomaticBrushDensity;
            chkbxOrientToMouse.Checked = settings.DoRotateWithMouse;
            chkbxColorizeBrush.Checked = settings.DoColorizeBrush;
            chkbxLockAlpha.Checked = settings.DoLockAlpha;
            sliderMinDrawDistance.Value = settings.MinDrawDistance;
            sliderJitterMaxRed.Value = settings.RandMaxR;
            sliderJitterMaxGreen.Value = settings.RandMaxG;
            sliderJitterMaxBlue.Value = settings.RandMaxB;
            sliderJitterMinRed.Value = settings.RandMinR;
            sliderJitterMinGreen.Value = settings.RandMinG;
            sliderJitterMinBlue.Value = settings.RandMinB;
            sliderJitterMaxHue.Value = settings.RandMaxH;
            sliderJitterMaxSat.Value = settings.RandMaxS;
            sliderJitterMaxVal.Value = settings.RandMaxV;
            sliderJitterMinHue.Value = settings.RandMinH;
            sliderJitterMinSat.Value = settings.RandMinS;
            sliderJitterMinVal.Value = settings.RandMinV;
            sliderShiftSize.Value = settings.SizeChange;
            sliderShiftRotation.Value = settings.RotChange;
            sliderShiftAlpha.Value = settings.AlphaChange;
            cmbxTabPressureBrushAlpha.SelectedIndex = settings.CmbxTabPressureBrushAlpha;
            cmbxTabPressureBrushDensity.SelectedIndex = settings.CmbxTabPressureBrushDensity;
            cmbxTabPressureBrushRotation.SelectedIndex = settings.CmbxTabPressureBrushRotation;
            cmbxTabPressureBrushSize.SelectedIndex = settings.CmbxTabPressureBrushSize;
            cmbxTabPressureBlueJitter.SelectedIndex = settings.CmbxTabPressureBlueJitter;
            cmbxTabPressureGreenJitter.SelectedIndex = settings.CmbxTabPressureGreenJitter;
            cmbxTabPressureHueJitter.SelectedIndex = settings.CmbxTabPressureHueJitter;
            cmbxTabPressureMinDrawDistance.SelectedIndex = settings.CmbxTabPressureMinDrawDistance;
            cmbxTabPressureRedJitter.SelectedIndex = settings.CmbxTabPressureRedJitter;
            cmbxTabPressureSatJitter.SelectedIndex = settings.CmbxTabPressureSatJitter;
            cmbxTabPressureValueJitter.SelectedIndex = settings.CmbxTabPressureValueJitter;
            cmbxTabPressureRandHorShift.SelectedIndex = settings.CmbxTabPressureRandHorShift;
            cmbxTabPressureRandMaxSize.SelectedIndex = settings.CmbxTabPressureRandMaxSize;
            cmbxTabPressureRandMinAlpha.SelectedIndex = settings.CmbxTabPressureRandMinAlpha;
            cmbxTabPressureRandMinSize.SelectedIndex = settings.CmbxTabPressureRandMinSize;
            cmbxTabPressureRandRotLeft.SelectedIndex = settings.CmbxTabPressureRandRotLeft;
            cmbxTabPressureRandRotRight.SelectedIndex = settings.CmbxTabPressureRandRotRight;
            cmbxTabPressureRandVerShift.SelectedIndex = settings.CmbxTabPressureRandVerShift;
            spinTabPressureBrushAlpha.Value = settings.TabPressureBrushAlpha;
            spinTabPressureBrushDensity.Value = settings.TabPressureBrushDensity;
            spinTabPressureBrushRotation.Value = settings.TabPressureBrushRotation;
            spinTabPressureBrushSize.Value = settings.TabPressureBrushSize;
            spinTabPressureMaxBlueJitter.Value = settings.TabPressureMaxBlueJitter;
            spinTabPressureMaxGreenJitter.Value = settings.TabPressureMaxGreenJitter;
            spinTabPressureMaxHueJitter.Value = settings.TabPressureMaxHueJitter;
            spinTabPressureMaxRedJitter.Value = settings.TabPressureMaxRedJitter;
            spinTabPressureMaxSatJitter.Value = settings.TabPressureMaxSatJitter;
            spinTabPressureMaxValueJitter.Value = settings.TabPressureMaxValueJitter;
            spinTabPressureMinBlueJitter.Value = settings.TabPressureMinBlueJitter;
            spinTabPressureMinDrawDistance.Value = settings.TabPressureMinDrawDistance;
            spinTabPressureMinGreenJitter.Value = settings.TabPressureMinGreenJitter;
            spinTabPressureMinHueJitter.Value = settings.TabPressureMinHueJitter;
            spinTabPressureMinRedJitter.Value = settings.TabPressureMinRedJitter;
            spinTabPressureMinSatJitter.Value = settings.TabPressureMinSatJitter;
            spinTabPressureMinValueJitter.Value = settings.TabPressureMinValueJitter;
            spinTabPressureRandHorShift.Value = settings.TabPressureRandHorShift;
            spinTabPressureRandMaxSize.Value = settings.TabPressureRandMaxSize;
            spinTabPressureRandMinAlpha.Value = settings.TabPressureRandMinAlpha;
            spinTabPressureRandMinSize.Value = settings.TabPressureRandMinSize;
            spinTabPressureRandRotLeft.Value = settings.TabPressureRandRotLeft;
            spinTabPressureRandRotRight.Value = settings.TabPressureRandRotRight;
            spinTabPressureRandVerShift.Value = settings.TabPressureRandVerShift;
            cmbxBrushSmoothing.SelectedIndex = (int)settings.Smoothing;
            cmbxSymmetry.SelectedIndex = (int)settings.Symmetry;

            UpdateBrushColor(settings.BrushColor);
        }

        /// <summary>
        /// Recreates the brush image with color and alpha effects applied.
        /// </summary>
        private void UpdateBrushImage()
        {
            int finalBrushAlpha = Utils.Clamp(Utils.GetStrengthMappedValue(sliderBrushAlpha.Value,
                (int)spinTabPressureBrushAlpha.Value,
                sliderBrushAlpha.Maximum,
                tabletPressureRatio,
                ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureBrushAlpha.SelectedItem).ValueMember), 0, 100);

            //Sets the color and alpha.
            Color setColor = bttnBrushColor.BackColor;
            float multAlpha = 1 - (finalBrushAlpha / 100f);

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
                    Utils.ColorImage(bmpBrushEffects, null, multAlpha);
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
            UpdateBrushImage();
        }

        /// <summary>
        /// Updates the brush ListView item count.
        /// </summary>
        private void UpdateListViewVirtualItemCount(int count)
        {
            if (listviewBrushImagePicker.InvokeRequired)
            {
                listviewBrushImagePicker.Invoke(new Action<int>((int value) => listviewBrushImagePicker.VirtualListSize = value), count);
            }
            else
            {
                listviewBrushImagePicker.VirtualListSize = count;
            }
        }

        /// <summary>
        /// Updates the tooltip popup (reused for all tooltips) and its visibility. It's visible
        /// when non-null and non empty.
        /// </summary>
        /// <param name="newTooltip"></param>
        private void UpdateTooltip(string newTooltip)
        {
            if (string.IsNullOrEmpty(newTooltip))
            {
                txtTooltip.Visible = false;
            } else
            {
                txtTooltip.Visible = true;
                txtTooltip.Text = newTooltip;
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
            canvasZoom = newZoomFactor;
            txtCanvasZoom.Text = string.Format(
                "{0} {1:p0}", Strings.CanvasZoom, newZoomFactor);

            //Gets the new width and height, adjusted for zooming.
            float zoomWidth = bmpCurrentDrawing.Width * newZoomFactor;
            float zoomHeight = bmpCurrentDrawing.Height * newZoomFactor;

            PointF zoomingPoint = isWheelZooming
                ? mouseLoc
                : new PointF(
                    displayCanvas.ClientSize.Width - canvas.x,
                    displayCanvas.ClientSize.Height - canvas.y);

            int zoomX = (int)(canvas.x + zoomingPoint.X -
                zoomingPoint.X * zoomWidth / canvas.width);
            int zoomY = (int)(canvas.y + zoomingPoint.Y -
                zoomingPoint.Y * zoomHeight / canvas.height);

            isWheelZooming = false;

            // Adjusts the canvas position after zooming, and the mouse location since it includes canvas position.
            (int x, int y) canvasOffset = new(canvas.x - zoomX, canvas.y - zoomY);
            canvas = new(zoomX, zoomY, (int)zoomWidth, (int)zoomHeight);
            mouseLoc = new PointF(mouseLoc.X + canvasOffset.x, mouseLoc.Y + canvasOffset.y);
            displayCanvas.Refresh();
        }
        #endregion

        #region Methods (event handlers)
        private void BrushImageLoadingWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker backgroundWorker = (BackgroundWorker)sender;
            BrushImageLoadingSettings args = (BrushImageLoadingSettings)e.Argument;

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

                int brushImagesLoadedCount = 0;
                int brushImagesDetectedCount = filePaths.Count;

                int maxThumbnailHeight = args.ListViewItemHeight;
                int maxBrushImageSize = args.MaxBrushSize;

                //Attempts to load a bitmap from a file to use as a brush image.
                foreach (string file in filePaths)
                {
                    if (backgroundWorker.CancellationPending)
                    {
                        e.Cancel = true;
                        return;
                    }

                    backgroundWorker.ReportProgress(GetProgressPercentage(brushImagesLoadedCount, brushImagesDetectedCount));
                    brushImagesLoadedCount++;

                    try
                    {
                        if (file.EndsWith(".abr", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                using (AbrBrushCollection brushImages = AbrReader.LoadBrushes(file))
                                {
                                    string location = Path.GetFileName(file);

                                    brushImagesDetectedCount += brushImages.Count;

                                    for (int i = 0; i < brushImages.Count; i++)
                                    {
                                        if (backgroundWorker.CancellationPending)
                                        {
                                            e.Cancel = true;
                                            return;
                                        }

                                        backgroundWorker.ReportProgress(GetProgressPercentage(brushImagesLoadedCount, brushImagesDetectedCount));
                                        brushImagesLoadedCount++;

                                        AbrBrush item = brushImages[i];

                                        // Creates the brush image space.
                                        int size = Math.Max(item.Image.Width, item.Image.Height);

                                        Bitmap scaledBrushImage = null;

                                        if (size > maxBrushImageSize)
                                        {
                                            size = maxBrushImageSize;
                                            Size newImageSize = Utils.ComputeBrushSize(item.Image.Width, item.Image.Height, maxBrushImageSize);
                                            scaledBrushImage = Utils.ScaleImage(item.Image, newImageSize);
                                        }

                                        Bitmap brushImage = new Bitmap(size, size, PixelFormat.Format32bppPArgb);

                                        //Pads the image to be square if needed, makes fully
                                        //opaque images use intensity for alpha, and draws the
                                        //altered loaded bitmap to the brush image.
                                        Utils.OverwriteBits(Utils.MakeBitmapSquare(
                                            Utils.MakeTransparent(scaledBrushImage ?? item.Image)), brushImage);

                                        if (scaledBrushImage != null)
                                        {
                                            scaledBrushImage.Dispose();
                                            scaledBrushImage = null;
                                        }

                                        string filename = item.Name;

                                        if (string.IsNullOrEmpty(filename))
                                        {
                                            filename = string.Format(
                                                System.Globalization.CultureInfo.CurrentCulture,
                                                Strings.AbrBrushNameFallbackFormat,
                                                i);
                                        }

                                        //Appends invisible spaces to files with the same name
                                        //until they're unique.
                                        while (loadedBrushImages.Any(brush => brush.Name.Equals(filename, StringComparison.Ordinal)))
                                        {
                                            filename += " ";
                                        }

                                        // Add the brush image to the list and generate the ListView thumbnail.

                                        loadedBrushImages.Add(
                                            new BrushSelectorItem(filename, location, brushImage, tempDir.GetRandomFileName(), maxThumbnailHeight));

                                        if ((i % 2) == 0)
                                        {
                                            UpdateListViewVirtualItemCount(loadedBrushImages.Count);
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
                                if (size > maxBrushImageSize)
                                {
                                    size = maxBrushImageSize;

                                    Size newImageSize = Utils.ComputeBrushSize(bmp.Width, bmp.Height, maxBrushImageSize);

                                    scaledBrush = Utils.ScaleImage(bmp, newImageSize);
                                }

                                brushImage = new Bitmap(size, size, PixelFormat.Format32bppPArgb);

                                //Pads the image to be square if needed, makes fully
                                //opaque images use intensity for alpha, and draws the
                                //altered loaded bitmap to the brush.
                                Utils.OverwriteBits(Utils.MakeBitmapSquare(
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
                            while (loadedBrushImages.Any(a =>
                            { return (a.Name.Equals(filename)); }))
                            {
                                filename += " ";
                            }

                            string location = Path.GetDirectoryName(file);

                            //Adds the brush without the period at the end.
                            loadedBrushImages.Add(
                                new BrushSelectorItem(filename, location, brushImage, tempDir.GetRandomFileName(), maxThumbnailHeight));

                            if ((brushImagesLoadedCount % 2) == 0)
                            {
                                UpdateListViewVirtualItemCount(loadedBrushImages.Count);
                            }
                        }

                        if (args.AddtoSettings)
                        {
                            //Adds the brush image location into settings.
                            loadedBrushImagePaths.Add(file);
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

            static int GetProgressPercentage(double done, double total)
            {
                return (int)(done / total * 100.0).Clamp(0.0, 100.0);
            }
        }

        private void BrushImageLoadingWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            brushImageLoadProgressBar.Value = e.ProgressPercentage;
        }

        private void BrushImageLoadingWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                if (isFormClosing)
                {
                    Close();
                }
                else if (doReinitializeBrushImages)
                {
                    InitBrushes();
                }
            }
            else
            {
                BrushImageLoadingSettings workerArgs = (BrushImageLoadingSettings)e.Result;

                if (e.Error != null && workerArgs.DisplayErrors)
                {
                    MessageBox.Show(this, e.Error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    listviewBrushImagePicker.VirtualListSize = loadedBrushImages.Count;

                    if (loadedBrushImages.Count > 0)
                    {
                        // Select the user's previous brush if it is present, otherwise select the last added brush.
                        int selectedItemIndex = loadedBrushImages.Count - 1;

                        if (!string.IsNullOrEmpty(tokenSelectedBrushImageName))
                        {
                            int index = loadedBrushImages.FindIndex(brush => brush.Name.Equals(tokenSelectedBrushImageName));

                            if (index >= 0)
                            {
                                selectedItemIndex = index;
                            }
                        }

                        listviewBrushImagePicker.SelectedIndices.Clear();
                        listviewBrushImagePicker.SelectedIndices.Add(selectedItemIndex);
                        listviewBrushImagePicker.EnsureVisible(selectedItemIndex);
                    }
                }

                brushImageLoadProgressBar.Value = 0;
                brushImageLoadProgressBar.Visible = false;
                bttnAddBrushImages.Visible = true;
            }
        }

        private void CmbxTabPressure_MouseHover(object sender, EventArgs e)
        {
            UpdateTooltip(
                Strings.ValueInfluenceTip + "\n\n"
                + Strings.ValueTypeNothingTip + "\n"
                + Strings.ValueTypeAddTip + "\n"
                + Strings.ValueTypeAddPercentTip + "\n"
                + Strings.ValueTypeAddPercentCurrentTip + "\n"
                + Strings.ValueTypeMatchValueTip + "\n"
                + Strings.ValueTypeMatchPercentTip);
        }

        /// <summary>
        /// Sets up image panning and drawing to occur with mouse movement.
        /// </summary>
        private void DisplayCanvas_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                // Removes points near the mouse.
                if (activeTool == Tool.SetSymmetryOrigin && cmbxSymmetry.SelectedIndex == (int)SymmetryMode.SetPoints)
                {
                    // Deletes all points.
                    if (KeyShortcutManager.IsKeyDown(Keys.ControlKey))
                    {
                        symmetryOrigins.Clear();
                    }
                    // Deletes points within a small radius of the mouse.
                    else
                    {
                        for (int i = 0; i < symmetryOrigins.Count; i++)
                        {
                            double radius = Math.Sqrt(
                                Math.Pow(mouseLoc.X / canvasZoom - (symmetryOrigin.X + symmetryOrigins[i].X), 2) +
                                Math.Pow(mouseLoc.Y / canvasZoom - (symmetryOrigin.Y + symmetryOrigins[i].Y), 2));

                            if (radius <= 15 / canvasZoom)
                            {
                                symmetryOrigins.RemoveAt(i);
                                i--;
                            }
                        }
                    }
                }
                //Displays a context menu for the background.
                else
                {
                    ShowBgContextMenu(displayCanvas, e.Location);
                }
            }

            //Pans the image.
            else if (e.Button == MouseButtons.Middle ||
                (e.Button == MouseButtons.Left && KeyShortcutManager.IsKeyDown(Keys.ControlKey)))
            {
                isUserPanning = true;
                mouseLocPrev = new Point(e.Location.X - canvas.x, e.Location.Y - canvas.y);
            }

            else if (e.Button == MouseButtons.Left)
            {
                mouseLocPrev = new Point(e.Location.X - canvas.x, e.Location.Y - canvas.y);

                //Draws with the brush.
                if (activeTool == Tool.Brush || activeTool == Tool.Eraser)
                {
                    isUserDrawing = true;

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

                    //Draws the brush on the first canvas click. Lines aren't drawn at a single point.
                    //Doesn't draw for tablets, since the user hasn't exerted full pressure yet.
                    if (!chkbxOrientToMouse.Checked && tabletPressureRatio == 0)
                    {
                        int finalBrushSize = Utils.GetStrengthMappedValue(sliderBrushSize.Value,
                            (int)spinTabPressureBrushSize.Value,
                            sliderBrushSize.Maximum,
                            tabletPressureRatio,
                            ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureBrushSize.SelectedItem).ValueMember);

                        DrawBrush(new PointF(
                            mouseLocPrev.X / canvasZoom,
                            mouseLocPrev.Y / canvasZoom),
                            finalBrushSize);
                    }
                }
                // Grabs the color under the mouse.
                else if (activeTool == Tool.ColorPicker)
                {
                    GetColorFromCanvas(new Point(
                        (int)(mouseLocPrev.X / canvasZoom),
                        (int)(mouseLocPrev.Y / canvasZoom)));

                    SwitchTool(lastTool);
                }
                // Changes the symmetry origin or adds a new origin (if in SetPoints symmetry mode).
                else if (activeTool == Tool.SetSymmetryOrigin)
                {
                    if (cmbxSymmetry.SelectedIndex == (int)SymmetryMode.SetPoints)
                    {
                        PointF newPoint = TransformPoint(new PointF(e.Location.X, e.Location.Y));
                        newPoint = new PointF(
                            newPoint.X - symmetryOrigin.X,
                            newPoint.Y - symmetryOrigin.Y);

                        symmetryOrigins.Add(newPoint);
                    }
                    else
                    {
                        symmetryOrigin = TransformPoint(new PointF(e.Location.X, e.Location.Y));
                    }
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

            UpdateTooltip(string.Empty);
        }

        /// <summary>
        /// Sets up for drawing and handles panning.
        /// </summary>
        private void DisplayCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            //Updates the new location.
            mouseLoc = new Point(e.Location.X - canvas.x, e.Location.Y - canvas.y);

            //Handles panning the screen.
            if (isUserPanning && (
                displayCanvas.ClientRectangle.X > canvas.x ||
                displayCanvas.ClientRectangle.Width < canvas.x + canvas.width ||
                displayCanvas.ClientRectangle.Y > canvas.y ||
                displayCanvas.ClientRectangle.Height < canvas.y + canvas.height))
            {
                Rectangle range = GetRange();

                //Moves the drawing region.
                double rotMaxSizeFactor = Math.Sqrt(2);
                double width = range.Right * rotMaxSizeFactor / 2;
                double height = range.Bottom * rotMaxSizeFactor / 2;
                int locx = Utils.Clamp(canvas.x + (int)Math.Round(mouseLoc.X - mouseLocPrev.X),
                    (int)(range.Left - width), (int)(range.Right + width));
                int locy = Utils.Clamp(canvas.y + (int)Math.Round(mouseLoc.Y - mouseLocPrev.Y),
                    (int)Math.Ceiling(range.Top - height), (int)Math.Ceiling(range.Bottom + height));

                //Updates the position of the canvas.
                canvas.x = locx;
                canvas.y = locy;
            }

            else if (isUserDrawing)
            {
                finalMinDrawDistance = Utils.Clamp(Utils.GetStrengthMappedValue(sliderMinDrawDistance.Value,
                        (int)spinTabPressureMinDrawDistance.Value,
                        sliderMinDrawDistance.Maximum,
                        tabletPressureRatio,
                        ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureMinDrawDistance.SelectedItem).ValueMember),
                        0, sliderMinDrawDistance.Maximum);

                // Doesn't draw unless the minimum drawing distance is met.
                if (finalMinDrawDistance != 0)
                {
                    if (mouseLocBrush.HasValue)
                    {
                        float deltaX = mouseLocBrush.Value.X - mouseLoc.X;
                        float deltaY = mouseLocBrush.Value.Y - mouseLoc.Y;

                        if (Math.Sqrt(deltaX * deltaX + deltaY * deltaY) <
                            finalMinDrawDistance * canvasZoom)
                        {
                            displayCanvas.Refresh();
                            return;
                        }
                    }

                    mouseLocBrush = mouseLoc;
                }

                if (activeTool == Tool.Brush || activeTool == Tool.Eraser)
                {
                    int finalBrushDensity = Utils.Clamp(Utils.GetStrengthMappedValue(sliderBrushDensity.Value,
                        (int)spinTabPressureBrushDensity.Value,
                        sliderBrushDensity.Maximum,
                        tabletPressureRatio,
                        ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureBrushDensity.SelectedItem).ValueMember),
                        0, sliderBrushDensity.Maximum);

                    // Draws without speed control. Messier, but faster.
                    if (finalBrushDensity == 0)
                    {
                        int finalBrushSize = Utils.GetStrengthMappedValue(sliderBrushSize.Value,
                            (int)spinTabPressureBrushSize.Value,
                            sliderBrushSize.Maximum,
                            tabletPressureRatio,
                            ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureBrushSize.SelectedItem).ValueMember);

                        if (finalBrushSize > 0)
                        {
                            DrawBrush(new PointF(
                                mouseLoc.X / canvasZoom,
                                mouseLoc.Y / canvasZoom),
                                finalBrushSize);
                        }

                        mouseLocPrev = new Point(e.Location.X - canvas.x, e.Location.Y - canvas.y);
                    }

                    // Draws at intervals of brush width between last and current mouse position,
                    // tracking remainder by changing final mouse position.
                    else
                    {
                        int finalBrushSize = Utils.GetStrengthMappedValue(sliderBrushSize.Value,
                            (int)spinTabPressureBrushSize.Value,
                            sliderBrushSize.Maximum,
                            tabletPressureRatio,
                            ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureBrushSize.SelectedItem).ValueMember);

                        if (finalBrushSize <= 0)
                        {
                            return;
                        }

                        double deltaX = (mouseLoc.X - mouseLocPrev.X) / canvasZoom;
                        double deltaY = (mouseLoc.Y - mouseLocPrev.Y) / canvasZoom;
                        double brushWidthFrac = finalBrushSize / (double)finalBrushDensity;
                        double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                        double angle = Math.Atan2(deltaY, deltaX);
                        double xDist = Math.Cos(angle);
                        double yDist = Math.Sin(angle);
                        double numIntervals = distance / (Double.IsNaN(brushWidthFrac) ? 1 : brushWidthFrac);

                        for (int i = 1; i <= (int)numIntervals; i++)
                        {
                            DrawBrush(new PointF(
                                (float)(mouseLocPrev.X / canvasZoom + xDist * brushWidthFrac * i),
                                (float)(mouseLocPrev.Y / canvasZoom + yDist * brushWidthFrac * i)),
                                finalBrushSize);
                        }

                        double extraDist = brushWidthFrac * (numIntervals - (int)numIntervals);

                        // Same as mouse position except for remainder.
                        mouseLoc = new PointF(
                            (float)((e.Location.X - canvas.x) - xDist * extraDist * canvasZoom),
                            (float)((e.Location.Y - canvas.y) - yDist * extraDist * canvasZoom));
                        mouseLocPrev = mouseLoc;
                    }
                }
            }
            else if (e.Button == MouseButtons.Left
                && activeTool == Tool.SetSymmetryOrigin
                && cmbxSymmetry.SelectedIndex != (int)SymmetryMode.SetPoints)
            {
                symmetryOrigin = TransformPoint(new PointF(e.Location.X, e.Location.Y));
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
                        Utils.OverwriteBits(prevStroke, bmpCurrentDrawing, true);
                    }

                    displayCanvas.Refresh();
                }
            }
        }

        /// <summary>
        /// Redraws the canvas and draws shapes to illustrate the current tool.
        /// </summary>
        private void DisplayCanvas_Paint(object sender, PaintEventArgs e)
        {
            //Draws the whole canvas with smoothing at lower zooms.
            if (canvasZoom < 0.5f) { e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic; }
            else if (canvasZoom < 0.75f) { e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear; }
            else if (canvasZoom < 1) { e.Graphics.InterpolationMode = InterpolationMode.Bilinear; }
            else { e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor; }

            e.Graphics.SmoothingMode = SmoothingMode.None;

            float drawingOffsetX = (bmpCurrentDrawing.Width * 0.5f * canvasZoom);
            float drawingOffsetY = (bmpCurrentDrawing.Height * 0.5f * canvasZoom);

            e.Graphics.TranslateTransform(canvas.x + drawingOffsetX, canvas.y + drawingOffsetY);
            e.Graphics.RotateTransform(canvasRotation);
            e.Graphics.TranslateTransform(-drawingOffsetX, -drawingOffsetY);

            //Draws the image.
            e.Graphics.DrawImage(bmpCurrentDrawing, 0, 0, canvas.width, canvas.height);

            //Draws the selection.
            var selection = EnvironmentParameters.GetSelectionAsPdnRegion();

            if (selection?.GetRegionReadOnly() != null)
            {
                //Calculates the outline once the selection becomes valid.
                if (selectionOutline == null)
                {
                    selectionOutline = selection.ConstructOutline(
                        new RectangleF(0, 0,
                        bmpCurrentDrawing.Width,
                        bmpCurrentDrawing.Height),
                        canvasZoom);
                }

                //Scales to zoom so the drawing region accounts for scale.
                e.Graphics.ScaleTransform(canvasZoom, canvasZoom);

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
            }

            e.Graphics.ResetTransform();

            if (activeTool == Tool.Brush || activeTool == Tool.Eraser)
            {
                //Draws the brush as a rectangle when not drawing by mouse.
                if (!isUserDrawing)
                {
                    int radius = (int)(sliderBrushSize.Value * canvasZoom);

                    e.Graphics.DrawRectangle(
                        Pens.Black,
                        mouseLoc.X + canvas.x - (radius / 2f),
                        mouseLoc.Y + canvas.y - (radius / 2f),
                        radius,
                        radius);

                    e.Graphics.DrawRectangle(
                        Pens.White,
                        mouseLoc.X + canvas.x - (radius / 2f) - 1,
                        mouseLoc.Y + canvas.y - (radius / 2f) - 1,
                        radius + 2,
                        radius + 2);
                }

                // Draws the minimum distance circle if min distance is in use.
                else if (finalMinDrawDistance > 0)
                {
                    e.Graphics.TranslateTransform(canvas.x, canvas.y);

                    int radius = (int)(finalMinDrawDistance * 2 * canvasZoom);

                    e.Graphics.DrawEllipse(
                        Pens.Red,
                        (mouseLocBrush.HasValue ? mouseLocBrush.Value.X : mouseLoc.X) - (radius / 2f) - 1,
                        (mouseLocBrush.HasValue ? mouseLocBrush.Value.Y : mouseLoc.Y) - (radius / 2f) - 1,
                        radius + 2,
                        radius + 2);

                    e.Graphics.TranslateTransform(-canvas.x, -canvas.y);
                }
            }

            e.Graphics.TranslateTransform(canvas.x + drawingOffsetX, canvas.y + drawingOffsetY);
            e.Graphics.RotateTransform(canvasRotation);
            e.Graphics.TranslateTransform(-drawingOffsetX, -drawingOffsetY);

            // Draws the symmetry origins for symmetry modes when it's enabled.
            if (activeTool == Tool.SetSymmetryOrigin || cmbxSymmetry.SelectedIndex != (int)SymmetryMode.None)
            {
                // Draws indicators to see where the user-defined offsets are in SetPoints mode.
                if (cmbxSymmetry.SelectedIndex == (int)SymmetryMode.SetPoints)
                {
                    // Draws indication lines for the symmetry origin, which in SetPoints mode should be wherever the
                    // mouse was when the user switched to the tool.
                    if (activeTool == Tool.SetSymmetryOrigin)
                    {
                        e.Graphics.DrawLine(
                            Pens.Red,
                            new PointF(0, symmetryOrigin.Y * canvasZoom),
                            new PointF(bmpCurrentDrawing.Width * canvasZoom, symmetryOrigin.Y * canvasZoom));
                        e.Graphics.DrawLine(
                            Pens.Red,
                            new PointF(symmetryOrigin.X * canvasZoom, 0),
                            new PointF(symmetryOrigin.X * canvasZoom, bmpCurrentDrawing.Height * canvasZoom));
                    }

                    e.Graphics.ScaleTransform(canvasZoom, canvasZoom);

                    // Draws a rectangle for each origin point, either relative to the symmetry origin (in
                    // SetSymmetryOrigin tool) or the mouse (with the Brush tool)
                    if (!isUserDrawing)
                    {
                        float pointsDrawnX, pointsDrawnY;

                        if (activeTool == Tool.SetSymmetryOrigin)
                        {
                            pointsDrawnX = symmetryOrigin.X;
                            pointsDrawnY = symmetryOrigin.Y;

                            for (int i = 0; i < symmetryOrigins.Count; i++)
                            {
                                e.Graphics.DrawRectangle(
                                    Pens.Red,
                                    (float)(pointsDrawnX + symmetryOrigins[i].X - 1),
                                    (float)(pointsDrawnY + symmetryOrigins[i].Y - 1),
                                    2, 2);
                            }
                        }
                        else
                        {
                            pointsDrawnX = (mouseLoc.X / canvasZoom - bmpCurrentDrawing.Width / 2);
                            pointsDrawnY = (mouseLoc.Y / canvasZoom - bmpCurrentDrawing.Height / 2);

                            for (int i = 0; i < symmetryOrigins.Count; i++)
                            {
                                float offsetX = pointsDrawnX + symmetryOrigins[i].X;
                                float offsetY = pointsDrawnY + symmetryOrigins[i].Y;
                                double dist = Math.Sqrt(offsetX * offsetX + offsetY * offsetY);
                                double angle = Math.Atan2(offsetY, offsetX);
                                angle -= canvasRotation * Math.PI / 180;
                                e.Graphics.DrawRectangle(
                                    Pens.Red,
                                    (float)(bmpCurrentDrawing.Width / 2 + dist * Math.Cos(angle) - 1),
                                    (float)(bmpCurrentDrawing.Height / 2 + dist * Math.Sin(angle) - 1),
                                    2, 2);
                            }
                        }
                    }
                }
                else
                {
                    e.Graphics.DrawLine(
                        Pens.Red,
                        new PointF(0, symmetryOrigin.Y * canvasZoom),
                        new PointF(bmpCurrentDrawing.Width * canvasZoom, symmetryOrigin.Y * canvasZoom));
                    e.Graphics.DrawLine(
                        Pens.Red,
                        new PointF(symmetryOrigin.X * canvasZoom, 0),
                        new PointF(symmetryOrigin.X * canvasZoom, bmpCurrentDrawing.Height * canvasZoom));
                }
            }

            e.Graphics.ResetTransform();
        }

        /// <summary>
        /// Displays a dialog allowing the user to add new brushes.
        /// </summary>
        private void BttnAddBrushImages_Click(object sender, EventArgs e)
        {
            ImportBrushImages(true);
        }

        private void BttnAddBrushImages_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.AddBrushImagesTip);
        }

        /// <summary>
        /// Sets the new color of the brush.
        /// </summary>
        private void BttnBrushColor_Click(object sender, EventArgs e)
        {
            //Creates and configures a color dialog to display.
            ColorDialog dialog = new ColorDialog
            {
                FullOpen = true,
                Color = bttnBrushColor.BackColor
            };

            //If the user successfully chooses a color.
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                UpdateBrushColor(dialog.Color);
            }
        }

        private void BttnBrushColor_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.BrushColorTip);
        }

        private void BttnBrushSmoothing_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.BrushSmoothingTip);
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
            UpdateTooltip(Strings.CancelTip);
        }

        /// <summary>
        /// Removes all brush images added by the user.
        /// </summary>
        private void BttnClearBrushImages_Click(object sender, EventArgs e)
        {
            InitBrushes();
        }

        private void BttnClearBrushImages_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.ClearBrushImagesTip);
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
            UpdateTooltip(Strings.ClearSettingsTip);
        }

        /// <summary>
        /// Deletes the current brush without changing the brush settings.
        /// </summary>
        private void BttnDeleteBrush_Click(object sender, EventArgs e)
        {
            settings.CustomBrushes.Remove(currentBrushName);
            listviewBrushPicker.Items.RemoveAt(listviewBrushPicker.SelectedIndices[0]);
            settings.MarkSettingsChanged();
            currentBrushName = null;
            bttnDeleteBrush.Enabled = false;
        }

        private void BttnDeleteBrush_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.DeleteBrushTip);
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
            UpdateTooltip(Strings.OkTip);
        }

        /// <summary>
        /// Opens the preferences dialog to define persistent settings.
        /// </summary>
        private void BttnPreferences_Click(object sender, EventArgs e)
        {
            if (settings != null)
            {
                new Gui.BrushFactoryPreferences(settings).ShowDialog();
            }
            else
            {
                MessageBox.Show(Strings.SettingsUnavailableError,
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BttnPreferences_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.CustomBrushImageLocationsTip);
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
                    Utils.OverwriteBits(redoBmp, bmpCurrentDrawing);
                }

                displayCanvas.Refresh();
            }
            else
            {
                MessageBox.Show(Strings.RedoFileNotFoundError,
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            UpdateTooltip(Strings.RedoTip);
        }

        /// <summary>
        /// Saves the current brush settings as a separate brush.
        /// </summary>
        private void BttnSaveBrush_Click(object sender, EventArgs e)
        {
            int index = listviewBrushImagePicker.SelectedIndices.Count > 0
                ? listviewBrushImagePicker.SelectedIndices[0]
                : -1;

            // Opens a textbox dialog to name the custom brush. Brush names must be unique, so the brush will have
            // spaces appended to the end of the name until naming conflicts are resolved.
            TextboxDialog dlg = new TextboxDialog(
                Strings.CustomBrushDialogTitle,
                Strings.CustomBrushDialogDescription,
                Strings.Ok,
                (txt) => string.IsNullOrWhiteSpace(txt) ? Strings.CustomBrushDialogErrorName : null);

            DialogResult result = dlg.ShowDialog();
            if (result == DialogResult.OK)
            {
                string inputText = dlg.GetSubmittedText();
                while (
                    PersistentSettings.defaultBrushes.ContainsKey(inputText) ||
                    settings.CustomBrushes.ContainsKey(inputText))
                {
                    inputText += " ";
                }

                BrushSettings newSettings = new BrushSettings()
                {
                    AutomaticBrushDensity = chkbxAutomaticBrushDensity.Checked,
                    AlphaChange = sliderShiftAlpha.Value,
                    BrushAlpha = sliderBrushAlpha.Value,
                    BrushColor = bttnBrushColor.BackColor,
                    BrushDensity = sliderBrushDensity.Value,
                    BrushImageName = index >= 0 ? loadedBrushImages[index].Name : string.Empty,
                    BrushRotation = sliderBrushRotation.Value,
                    BrushSize = sliderBrushSize.Value,
                    DoColorizeBrush = chkbxColorizeBrush.Checked,
                    DoLockAlpha = chkbxLockAlpha.Checked,
                    DoRotateWithMouse = chkbxOrientToMouse.Checked,
                    MinDrawDistance = sliderMinDrawDistance.Value,
                    RandHorzShift = sliderRandHorzShift.Value,
                    RandMaxB = sliderJitterMaxBlue.Value,
                    RandMaxG = sliderJitterMaxGreen.Value,
                    RandMaxR = sliderJitterMaxRed.Value,
                    RandMaxH = sliderJitterMaxHue.Value,
                    RandMaxS = sliderJitterMaxSat.Value,
                    RandMaxV = sliderJitterMaxVal.Value,
                    RandMaxSize = sliderRandMaxSize.Value,
                    RandMinAlpha = sliderRandMinAlpha.Value,
                    RandMinB = sliderJitterMinBlue.Value,
                    RandMinG = sliderJitterMinGreen.Value,
                    RandMinR = sliderJitterMinRed.Value,
                    RandMinH = sliderJitterMinHue.Value,
                    RandMinS = sliderJitterMinSat.Value,
                    RandMinV = sliderJitterMinVal.Value,
                    RandMinSize = sliderRandMinSize.Value,
                    RandRotLeft = sliderRandRotLeft.Value,
                    RandRotRight = sliderRandRotRight.Value,
                    RandVertShift = sliderRandVertShift.Value,
                    RotChange = sliderShiftRotation.Value,
                    SizeChange = sliderShiftSize.Value,
                    Smoothing = (CmbxSmoothing.Smoothing)cmbxBrushSmoothing.SelectedIndex,
                    Symmetry = (SymmetryMode)cmbxSymmetry.SelectedIndex,
                    CmbxTabPressureBrushAlpha = cmbxTabPressureBrushAlpha.SelectedIndex,
                    CmbxTabPressureBrushDensity = cmbxTabPressureBrushDensity.SelectedIndex,
                    CmbxTabPressureBrushRotation = cmbxTabPressureBrushRotation.SelectedIndex,
                    CmbxTabPressureBrushSize = cmbxTabPressureBrushSize.SelectedIndex,
                    CmbxTabPressureBlueJitter = cmbxTabPressureBlueJitter.SelectedIndex,
                    CmbxTabPressureGreenJitter = cmbxTabPressureGreenJitter.SelectedIndex,
                    CmbxTabPressureHueJitter = cmbxTabPressureHueJitter.SelectedIndex,
                    CmbxTabPressureMinDrawDistance = cmbxTabPressureMinDrawDistance.SelectedIndex,
                    CmbxTabPressureRedJitter = cmbxTabPressureRedJitter.SelectedIndex,
                    CmbxTabPressureSatJitter = cmbxTabPressureSatJitter.SelectedIndex,
                    CmbxTabPressureValueJitter = cmbxTabPressureValueJitter.SelectedIndex,
                    CmbxTabPressureRandHorShift = cmbxTabPressureRandHorShift.SelectedIndex,
                    CmbxTabPressureRandMaxSize = cmbxTabPressureRandMaxSize.SelectedIndex,
                    CmbxTabPressureRandMinAlpha = cmbxTabPressureRandMinAlpha.SelectedIndex,
                    CmbxTabPressureRandMinSize = cmbxTabPressureRandMinSize.SelectedIndex,
                    CmbxTabPressureRandRotLeft = cmbxTabPressureRandRotLeft.SelectedIndex,
                    CmbxTabPressureRandRotRight = cmbxTabPressureRandRotRight.SelectedIndex,
                    CmbxTabPressureRandVerShift = cmbxTabPressureRandVerShift.SelectedIndex,
                    TabPressureBrushAlpha = (int)spinTabPressureBrushAlpha.Value,
                    TabPressureBrushDensity = (int)spinTabPressureBrushDensity.Value,
                    TabPressureBrushRotation = (int)spinTabPressureBrushRotation.Value,
                    TabPressureBrushSize = (int)spinTabPressureBrushSize.Value,
                    TabPressureMaxBlueJitter = (int)spinTabPressureMaxBlueJitter.Value,
                    TabPressureMaxGreenJitter = (int)spinTabPressureMaxGreenJitter.Value,
                    TabPressureMaxHueJitter = (int)spinTabPressureMaxHueJitter.Value,
                    TabPressureMaxRedJitter = (int)spinTabPressureMaxRedJitter.Value,
                    TabPressureMaxSatJitter = (int)spinTabPressureMaxSatJitter.Value,
                    TabPressureMaxValueJitter = (int)spinTabPressureMaxValueJitter.Value,
                    TabPressureMinBlueJitter = (int)spinTabPressureMinBlueJitter.Value,
                    TabPressureMinDrawDistance = (int)spinTabPressureMinDrawDistance.Value,
                    TabPressureMinGreenJitter = (int)spinTabPressureMinGreenJitter.Value,
                    TabPressureMinHueJitter = (int)spinTabPressureMinHueJitter.Value,
                    TabPressureMinRedJitter = (int)spinTabPressureMinRedJitter.Value,
                    TabPressureMinSatJitter = (int)spinTabPressureMinSatJitter.Value,
                    TabPressureMinValueJitter = (int)spinTabPressureMinValueJitter.Value,
                    TabPressureRandHorShift = (int)spinTabPressureRandHorShift.Value,
                    TabPressureRandMaxSize = (int)spinTabPressureRandMaxSize.Value,
                    TabPressureRandMinAlpha = (int)spinTabPressureRandMinAlpha.Value,
                    TabPressureRandMinSize = (int)spinTabPressureRandMinSize.Value,
                    TabPressureRandRotLeft = (int)spinTabPressureRandRotLeft.Value,
                    TabPressureRandRotRight = (int)spinTabPressureRandRotRight.Value,
                    TabPressureRandVerShift = (int)spinTabPressureRandVerShift.Value
                };

                settings.CustomBrushes.Add(inputText, newSettings);
                settings.MarkSettingsChanged();

                // Deselect whatever's selected and add a brush as selected.
                foreach (ListViewItem item in listviewBrushPicker.Items)
                {
                    item.Selected = false;
                }

                listviewBrushPicker.Items.Add(new ListViewItem(inputText) { Selected = true });
                currentBrushName = inputText;
                bttnDeleteBrush.Enabled = true;
            }
        }

        private void BttnSaveBrush_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.SaveNewBrushTip);
        }

        private void BttnToolBrush_Click(object sender, EventArgs e)
        {
            SwitchTool(Tool.Brush);
        }

        private void BttnToolBrush_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.ToolBrushTip);
        }

        private void BttnToolColorPicker_Click(object sender, EventArgs e)
        {
            SwitchTool(Tool.ColorPicker);
        }

        private void BttnToolColorPicker_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.ColorPickerTip);
        }

        private void BttnToolEraser_Click(object sender, EventArgs e)
        {
            SwitchTool(Tool.Eraser);
        }

        private void BttnToolEraser_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.ToolEraserTip);
        }

        private void BttnToolOrigin_Click(object sender, EventArgs e)
        {
            SwitchTool(Tool.SetSymmetryOrigin);
        }

        private void BttnToolOrigin_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.ToolOriginTip);
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
                    Utils.OverwriteBits(undoBmp, bmpCurrentDrawing);
                }

                displayCanvas.Refresh();
            }
            else
            {
                MessageBox.Show(Strings.RedoFileNotFoundError,
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            UpdateTooltip(Strings.UndoTip);
        }

        private void BttnSymmetry_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.SymmetryTip);
        }

        private void AutomaticBrushDensity_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.AutomaticBrushDensityTip);
        }

        private void AutomaticBrushDensity_CheckedChanged(object sender, EventArgs e) {
            sliderBrushDensity.Enabled = !chkbxAutomaticBrushDensity.Checked;
        }

        /// <summary>
        /// Resets the brush to reconfigure colorization. Colorization is
        /// applied when the brush is refreshed.
        /// </summary>
        private void ChkbxColorizeBrush_CheckedChanged(object sender, EventArgs e)
        {
            UpdateBrushImage();
        }

        private void ChkbxColorizeBrush_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.ColorizeBrushTip);
        }

        private void ChkbxLockAlpha_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.LockAlphaTip);
        }

        private void ChkbxOrientToMouse_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.OrientToMouseTip);
        }

        /// <summary>
        /// Trackbars and comboboxes (among others) handle the mouse wheel event, which makes it hard to scroll across
        /// the controls in the right-side pane. Since these controls are more traditionally used with left-clicking,
        /// being able to scroll the window with the mouse wheel is more important. This prevents handling.
        /// </summary>
        private void IgnoreMouseWheelEvent(object sender, MouseEventArgs e)
        {
            var scroll = panelDockSettingsContainer.VerticalScroll;
            var mouseEvent = (HandledMouseEventArgs)e;
            mouseEvent.Handled = true;

            // Stopping the mouse wheel event prevents scrolling the parent container too. This does it manually.
            scroll.Value = Utils.Clamp(scroll.Value - mouseEvent.Delta, scroll.Minimum, scroll.Maximum);
            panelDockSettingsContainer.PerformLayout(); // visually update scrollbar since it won't always.
        }

        /// <summary>
        /// Handles the CacheVirtualItems event of the listviewBrushPicker control.
        /// </summary>
        private void ListViewBrushImagePicker_CacheVirtualItems(object sender, CacheVirtualItemsEventArgs e)
        {
            // Check if the cache needs to be refreshed.
            if (visibleBrushImages != null && e.StartIndex >= visibleBrushImagesIndex && e.EndIndex <= visibleBrushImagesIndex + visibleBrushImages.Length)
            {
                // If the newly requested cache is a subset of the old cache,
                // no need to rebuild everything, so do nothing.
                return;
            }

            visibleBrushImagesIndex = e.StartIndex;
            // The indexes are inclusive.
            int length = e.EndIndex - e.StartIndex + 1;
            visibleBrushImages = new ListViewItem[length];

            // Fill the cache with the appropriate ListViewItems.
            for (int i = 0; i < length; i++)
            {
                int itemIndex = visibleBrushImagesIndex + i;

                BrushSelectorItem brushImage = loadedBrushImages[itemIndex];
                string name = brushImage.Name;
                string tooltipText;

                if (!string.IsNullOrEmpty(brushImage.Location))
                {
                    tooltipText = name + "\n" + brushImage.Location;
                }
                else
                {
                    tooltipText = name;
                }

                visibleBrushImages[i] = new ListViewItem
                {
                    // When the text is an empty string it will not
                    // be included in ListViewItem size calculation.
                    Text = string.Empty,
                    ImageIndex = itemIndex,
                    ToolTipText = tooltipText
                };
            }
        }

        /// <summary>
        /// Handles the DrawColumnHeader event of the listviewBrushPicker control.
        /// </summary>
        private void ListViewBrushImagePicker_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            e.DrawDefault = true;
        }

        /// <summary>
        /// Handles the DrawItem event of the listviewBrushPicker control.
        /// </summary>
        private void ListViewBrushImagePicker_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            BrushSelectorItem item = loadedBrushImages[e.ItemIndex];

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
        /// Handles the DrawSubItem event of the listviewBrushPicker control.
        /// </summary>
        private void ListViewBrushImagePicker_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            e.DrawDefault = true;
        }

        private void ListViewBrushImagePicker_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.BrushImageSelectorTip);
        }

        /// <summary>
        /// Handles the RetrieveVirtualItem event of the listviewBrushPicker control.
        /// </summary>
        private void ListViewBrushImagePicker_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
        {
            if (visibleBrushImages != null && e.ItemIndex >= visibleBrushImagesIndex && e.ItemIndex < visibleBrushImagesIndex + visibleBrushImages.Length)
            {
                e.Item = visibleBrushImages[e.ItemIndex - visibleBrushImagesIndex];
            }
            else
            {
                BrushSelectorItem brush = loadedBrushImages[e.ItemIndex];
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
        /// Sets the brush image when the user changes it with the selector.
        /// </summary>
        private void ListViewBrushImagePicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listviewBrushImagePicker.SelectedIndices.Count > 0)
            {
                int index = listviewBrushImagePicker.SelectedIndices[0];

                if (index >= 0)
                {
                    int previousItemIndex = listviewBrushImagePicker.PreviousItemIndex;

                    // Unloads virtualized item thumbnails to save memory.
                    if (previousItemIndex >= 0)
                    {
                        BrushSelectorItem previousItem = loadedBrushImages[previousItemIndex];
                        if (previousItem.State == BrushSelectorItemState.Memory)
                        {
                            previousItem.ToDisk();
                        }
                    }

                    // Loads item thumbnails that should be displayed.
                    BrushSelectorItem currentItem = loadedBrushImages[index];
                    if (currentItem.State == BrushSelectorItemState.Disk)
                    {
                        currentItem.ToMemory();
                    }

                    bmpBrush?.Dispose();
                    bmpBrush = Utils.FormatImage(
                        currentItem.Brush,
                        PixelFormat.Format32bppPArgb);

                    UpdateBrushImage();
                }
            }
        }

        private void ListviewBrushPicker_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.BrushSelectorTip);
        }

        /// <summary>
        /// Sets the brush when the user changes it with the selector.
        /// </summary>
        private void ListViewBrushPicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listviewBrushPicker.SelectedIndices.Count > 0)
            {
                ListViewItem selection = listviewBrushPicker.SelectedItems[0];

                if (selection != null)
                {
                    currentBrushName = selection.Text;

                    if (PersistentSettings.defaultBrushes.ContainsKey(selection.Text))
                    {
                        UpdateBrush(PersistentSettings.defaultBrushes[selection.Text]);
                    } else
                    {
                        UpdateBrush(settings.CustomBrushes[selection.Text]);
                    }
                }

                // Updates which brush image is active for the newly-selected brush
                if (listviewBrushImagePicker?.Items != null && selection?.Text != null)
                {
                    int index = loadedBrushImages.FindIndex((entry) =>
                    {
                        if (!string.IsNullOrEmpty(currentBrushName))
                        {
                            if (PersistentSettings.defaultBrushes.ContainsKey(currentBrushName))
                            {
                                return entry.Name.Equals(PersistentSettings.defaultBrushes[currentBrushName].BrushImageName);
                            }

                            if (settings.CustomBrushes.ContainsKey(currentBrushName))
                            {
                                return entry.Name.Equals(settings.CustomBrushes[currentBrushName].BrushImageName);
                            }
                        }

                        return entry.Name.Equals(Strings.DefaultBrushCircle);
                    });

                    if (index >= 0)
                    {
                        listviewBrushImagePicker.Items[index].Selected = true;
                        ListViewBrushImagePicker_SelectedIndexChanged(null, null);
                    }
                }
            }
        }

        private void SliderBrushAlpha_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.BrushAlphaTip);
        }

        private void SliderBrushAlpha_ValueChanged(object sender, EventArgs e)
        {
            txtBrushAlpha.Text = String.Format("{0} {1}",
                Strings.Alpha,
                sliderBrushAlpha.Value);

            UpdateBrushImage();
        }

        private void SliderBrushDensity_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.BrushDensityTip);
        }

        private void SliderBrushDensity_ValueChanged(object sender, EventArgs e)
        {
            txtBrushDensity.Text = String.Format("{0} {1}",
                Strings.BrushDensity,
                sliderBrushDensity.Value);
        }

        private void SliderBrushSize_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.BrushSizeTip);
        }

        private void SliderBrushSize_ValueChanged(object sender, EventArgs e)
        {
            txtBrushSize.Text = String.Format("{0} {1}",
                Strings.Size,
                sliderBrushSize.Value);

            //Updates to show changes in the brush indicator.
            displayCanvas.Refresh();
        }

        private void SliderBrushRotation_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.BrushRotationTip);
        }

        private void SliderBrushRotation_ValueChanged(object sender, EventArgs e)
        {
            txtBrushRotation.Text = String.Format("{0} {1}°",
                Strings.Rotation,
                sliderBrushRotation.Value);
        }

        private void SliderCanvasZoom_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.CanvasZoomTip);
        }

        private void SliderCanvasZoom_ValueChanged(object sender, EventArgs e)
        {
            Zoom(0, false);
        }

        private void SliderMinDrawDistance_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.MinDrawDistanceTip);
        }

        private void SliderMinDrawDistance_ValueChanged(object sender, EventArgs e)
        {
            txtMinDrawDistance.Text = String.Format("{0} {1}",
                Strings.MinDrawDistance,
                sliderMinDrawDistance.Value);
        }

        private void SliderRandHorzShift_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.RandHorzShiftTip);
        }

        private void SliderRandHorzShift_ValueChanged(object sender, EventArgs e)
        {
            txtRandHorzShift.Text = String.Format("{0} {1}%",
                Strings.RandHorzShift,
                sliderRandHorzShift.Value);
        }

        private void SliderJitterMaxBlue_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.JitterBlueTip);
        }

        private void SliderJitterMaxBlue_ValueChanged(object sender, EventArgs e)
        {
            txtJitterBlue.Text = String.Format("{0} -{1}%, +{2}%",
                Strings.JitterBlue,
                sliderJitterMinBlue.Value,
                sliderJitterMaxBlue.Value);
        }

        private void SliderJitterMaxGreen_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.JitterGreenTip);
        }

        private void SliderJitterMaxGreen_ValueChanged(object sender, EventArgs e)
        {
            txtJitterGreen.Text = String.Format("{0} -{1}%, +{2}%",
                Strings.JitterGreen,
                sliderJitterMinGreen.Value,
                sliderJitterMaxGreen.Value);
        }

        private void SliderJitterMaxRed_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.JitterRedTip);
        }

        private void SliderJitterMaxRed_ValueChanged(object sender, EventArgs e)
        {
            txtJitterRed.Text = String.Format("{0} -{1}%, +{2}%",
                Strings.JitterRed,
                sliderJitterMinRed.Value,
                sliderJitterMaxRed.Value);
        }

        private void SliderRandMaxSize_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.RandMaxSizeTip);
        }

        private void SliderRandMaxSize_ValueChanged(object sender, EventArgs e)
        {
            txtRandMaxSize.Text = String.Format("{0} {1}",
                Strings.RandMaxSize,
                sliderRandMaxSize.Value);
        }

        private void SliderRandMinAlpha_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.RandMinAlphaTip);
        }

        private void SliderRandMinAlpha_ValueChanged(object sender, EventArgs e)
        {
            txtRandMinAlpha.Text = String.Format("{0} {1}",
                Strings.RandMinAlpha,
                sliderRandMinAlpha.Value);
        }

        private void SliderJitterMinBlue_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.JitterBlueTip);
        }

        private void SliderJitterMinBlue_ValueChanged(object sender, EventArgs e)
        {
            txtJitterBlue.Text = String.Format("{0} -{1}%, +{2}%",
                Strings.JitterBlue,
                sliderJitterMinBlue.Value,
                sliderJitterMaxBlue.Value);
        }

        private void SliderJitterMinGreen_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.JitterGreenTip);
        }

        private void SliderJitterMinGreen_ValueChanged(object sender, EventArgs e)
        {
            txtJitterGreen.Text = String.Format("{0} -{1}%, +{2}%",
                Strings.JitterGreen,
                sliderJitterMinGreen.Value,
                sliderJitterMaxGreen.Value);
        }

        private void SliderJitterMaxHue_ValueChanged(object sender, EventArgs e)
        {
            txtJitterHue.Text = String.Format("{0} -{1}%, +{2}%",
                Strings.JitterHue,
                sliderJitterMinHue.Value,
                sliderJitterMaxHue.Value);
        }

        private void SliderJitterMaxHue_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.JitterHueTip);
        }

        private void SliderJitterMinHue_ValueChanged(object sender, EventArgs e)
        {
            txtJitterHue.Text = String.Format("{0} -{1}%, +{2}%",
                Strings.JitterHue,
                sliderJitterMinHue.Value,
                sliderJitterMaxHue.Value);
        }

        private void SliderJitterMinHue_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.JitterHueTip);
        }

        private void SliderJitterMinRed_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.JitterRedTip);
        }

        private void SliderJitterMinRed_ValueChanged(object sender, EventArgs e)
        {
            txtJitterRed.Text = String.Format("{0} -{1}%, +{2}%",
                Strings.JitterRed,
                sliderJitterMinRed.Value,
                sliderJitterMaxRed.Value);
        }

        private void SliderJitterMaxSat_ValueChanged(object sender, EventArgs e)
        {
            txtJitterSaturation.Text = String.Format("{0} -{1}%, +{2}%",
                Strings.JitterSaturation,
                sliderJitterMinSat.Value,
                sliderJitterMaxSat.Value);
        }

        private void SliderJitterMaxSat_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.JitterSaturationTip);
        }

        private void SliderJitterMinSat_ValueChanged(object sender, EventArgs e)
        {
            txtJitterSaturation.Text = String.Format("{0} -{1}%, +{2}%",
                Strings.JitterSaturation,
                sliderJitterMinSat.Value,
                sliderJitterMaxSat.Value);
        }

        private void SliderJitterMinSat_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.JitterSaturationTip);
        }

        private void SliderJitterMaxVal_ValueChanged(object sender, EventArgs e)
        {
            txtJitterValue.Text = String.Format("{0} -{1}%, +{2}%",
                Strings.JitterValue,
                sliderJitterMinVal.Value,
                sliderJitterMaxVal.Value);
        }

        private void SliderJitterMaxVal_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.JitterValueTip);
        }

        private void SliderJitterMinVal_ValueChanged(object sender, EventArgs e)
        {
            txtJitterValue.Text = String.Format("{0} -{1}%, +{2}%",
                Strings.JitterValue,
                sliderJitterMinVal.Value,
                sliderJitterMaxVal.Value);
        }

        private void SliderJitterMinVal_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.JitterValueTip);
        }

        private void SliderRandMinSize_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.RandMinSizeTip);
        }

        private void SliderRandMinSize_ValueChanged(object sender, EventArgs e)
        {
            txtRandMinSize.Text = String.Format("{0} {1}",
                Strings.RandMinSize,
                sliderRandMinSize.Value);
        }

        private void SliderRandRotLeft_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.RandRotLeftTip);
        }

        private void SliderRandRotLeft_ValueChanged(object sender, EventArgs e)
        {
            txtRandRotLeft.Text = String.Format("{0} {1}°",
                Strings.RandRotLeft,
                sliderRandRotLeft.Value);
        }

        private void SliderRandRotRight_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.RandRotRightTip);
        }

        private void SliderRandRotRight_ValueChanged(object sender, EventArgs e)
        {
            txtRandRotRight.Text = String.Format("{0} {1}°",
                Strings.RandRotRight,
                sliderRandRotRight.Value);
        }

        private void SliderRandVertShift_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.RandVertShiftTip);
        }

        private void SliderRandVertShift_ValueChanged(object sender, EventArgs e)
        {
            txtRandVertShift.Text = String.Format("{0} {1}%",
                Strings.RandVertShift,
                sliderRandVertShift.Value);
        }

        private void SliderShiftAlpha_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.ShiftAlphaTip);
        }

        private void SliderShiftAlpha_ValueChanged(object sender, EventArgs e)
        {
            txtShiftAlpha.Text = String.Format("{0} {1}",
                Strings.ShiftAlpha,
                sliderShiftAlpha.Value);
        }

        private void SliderShiftRotation_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.ShiftRotationTip);
        }

        private void SliderShiftRotation_ValueChanged(object sender, EventArgs e)
        {
            txtShiftRotation.Text = String.Format("{0} {1}°",
                Strings.ShiftRotation,
                sliderShiftRotation.Value);
        }

        private void SliderShiftSize_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.ShiftSizeTip);
        }

        private void SliderShiftSize_ValueChanged(object sender, EventArgs e)
        {
            txtShiftSize.Text = String.Format("{0} {1}",
                Strings.ShiftSize,
                sliderShiftSize.Value);
        }

        private void TabletUpdated(WintabDN.WintabPacket packet)
        {
            // Move cursor to stylus. This works since packets are only sent for touch or hover events.
            Cursor.Position = new Point(packet.pkX, packet.pkY);

            // Gets the current pressure.
            int maxPressure = WintabDN.CWintabInfo.GetMaxPressure();
            float newPressureRatio = (packet.pkNormalPressure == 0) ? 0 : (float)packet.pkNormalPressure / maxPressure;
            float deadzone = 0.01f; // Represents the 0 to 1% range of pressure.

            // Simulates the left mouse based on pressure. It must be simulated to avoid special handling for each
            // button since winforms doesn't support touch events.
            if (tabletPressureRatio < deadzone && newPressureRatio >= deadzone)
            {
                SafeNativeMethods.SimulateClick(SafeNativeMethods.MouseEvents.LeftDown);
            }
            else if (tabletPressureRatio > deadzone && newPressureRatio <= deadzone)
            {
                SafeNativeMethods.SimulateClick(SafeNativeMethods.MouseEvents.LeftUp);
            }

            // Updates the pressure.
            tabletPressureRatio = newPressureRatio;
        }

        /// <summary>
        /// While drawing, moves the canvas automatically when trying to draw
        /// past the visible bounds.
        /// </summary>
        private void RepositionUpdate_Tick(object sender, EventArgs e)
        {
            if (displayCanvas.IsDisposed)
            {
                return;
            }

            // Converts the mouse coordinates on the screen relative to the
            // background such that the top-left corner is (0, 0) up to its
            // width and height.
            Point mouseLocOnBG = displayCanvas.PointToClient(MousePosition);

            //Exits if the user isn't drawing out of the canvas boundary.
            if (!isUserDrawing || displayCanvas.ClientRectangle.Contains(mouseLocOnBG))
            {
                return;
            }

            //Amount of space between the display canvas and background.
            Rectangle range = GetRange();

            //The amount to move the canvas while drawing.
            int nudge = (int)(canvasZoom * 10);
            int canvasNewPosX, canvasNewPosY;

            //Nudges the screen horizontally when out of bounds and out of
            //the drawing region.
            if (canvas.width >=
                displayCanvas.ClientRectangle.Width)
            {
                if (mouseLocOnBG.X > displayCanvas.ClientRectangle.Width)
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
                canvasNewPosX += canvas.x;
            }

            //Nudges the screen vertically when out of bounds and out of
            //the drawing region.
            if (canvas.height >=
                displayCanvas.ClientRectangle.Height)
            {
                if (mouseLocOnBG.Y > displayCanvas.ClientRectangle.Height)
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
                canvasNewPosY += canvas.y;
            }

            PointF minBounds = TransformPoint(new PointF(0, 0));

            //Clamps all location values.
            if (canvasNewPosX <= range.Left) { canvasNewPosX = range.Left; }
            if (canvasNewPosX >= range.Right) { canvasNewPosX = range.Right; }
            if (canvasNewPosY <= range.Top) { canvasNewPosY = range.Top; }
            if (canvasNewPosY >= range.Bottom) { canvasNewPosY = range.Bottom; }

            //Updates with the new location and redraws the screen.
            canvas.x = canvasNewPosX;
            canvas.y = canvasNewPosY;
            displayCanvas.Refresh();
        }
        #endregion
    }
}