using DynamicDraw.Abr;
using DynamicDraw.Gui;
using DynamicDraw.Interop;
using DynamicDraw.Localization;
using DynamicDraw.Logic;
using DynamicDraw.Properties;
using DynamicDraw.TabletSupport;
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

namespace DynamicDraw
{
    /// <summary>
    /// The dialog used for working with the effect.
    /// </summary>
    public class WinDynamicDraw : EffectConfigDialog
    {
        #region Fields (Non Gui)
        private Tool lastTool = Tool.Brush;
        private Tool activeTool = Tool.Brush;

        /// <summary>
        /// Determines how to fill the transparent area under the user's image, if any.
        /// </summary>
        private BackgroundDisplayMode backgroundDisplayMode = BackgroundDisplayMode.Transparent;

        /// <summary>
        /// Contains the list of all blend mode options for brush strokes.
        /// </summary>
        readonly BindingList<Tuple<string, BlendMode>> blendModeOptions;

        /// <summary>
        /// If <see cref="BackgroundDisplayMode.Clipboard"/> is used, this will contain the image that was copied to
        /// the clipboard.
        /// </summary>
        private Bitmap bmpBackgroundClipboard;

        /// <summary>
        /// Contains the current brush (without modifications like alpha via flow).
        /// </summary>
        private Bitmap bmpBrush;

        /// <summary>
        /// Contains the current brush resized to be as small as the maximum possible brush size through randomization
        /// and the current brush. If the max possible size is larger than the current brush image, this is the same as
        /// bmpBrush.
        /// </summary>
        private Bitmap bmpBrushDownsized;

        /// <summary>
        /// Contains the current brush with all modifications applied. This is
        /// overwritten by the original brush to apply new changes so changes
        /// are not cumulative. For example, applying 25% alpha via flow repeatedly
        /// would otherwise make the brush unusable.
        /// </summary>
        private Bitmap bmpBrushEffects;

        /// <summary>
        /// Stores the current drawing in full.
        /// </summary>
        private Bitmap bmpCommitted = new Bitmap(1, 1, PixelFormat.Format32bppPArgb);

        /// <summary>
        /// Stores the current brush stroke. This bitmap is drawn to for all brush strokes, then
        /// merged down to the committed bitmap and cleared on finishing a brush stroke. Keeping
        /// a staging layer enables layer-like opacity and all the blend modes that aren't normal
        /// or overwrite mode. These effects are performed during draw & when committing.
        /// </summary>
        private Bitmap bmpStaged = new Bitmap(1, 1, PixelFormat.Format32bppPArgb);

        /// <summary>
        /// Stores the merged image of the staged + committed bitmaps. This is used only when drawing the canvas
        /// visually for the user, because freeing & allocating this memory repeatedly would be extremely slow.
        /// </summary>
        private Bitmap bmpMerged = new Bitmap(1, 1, PixelFormat.Format32bppPArgb);

        /// <summary>
        /// A performance optimization when using a merge layer. It's easier to add rectangles to
        /// this list and merge them down only once, vs. merging the entire viewport or image.
        /// </summary>
        private readonly List<Rectangle> mergeRegions = new List<Rectangle>();

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
        /// Whether the brush loading worker should reload brushes after cancelation.
        /// </summary>
        private bool doReinitializeBrushImages;

        /// <summary>
        /// While drawing the canvas, a half-pixel offset avoids visual edge boundary discrepancies. When zoomed in,
        /// this offset causes the position the user draws at to be incongruent with their mouse location. Subtracting
        /// half a pixel solves this schism.
        /// </summary>
        private const float halfPixelOffset = 0.5f;

        private bool isFormClosing = false;

        /// <summary>
        /// Determines the direction of flow shifting, which can be growing
        /// (true) or shrinking (false). Used by the flow shift slider.
        /// </summary>
        private bool isGrowingFlow = true;

        /// <summary>
        /// Determines the direction of size shifting, which can be growing
        /// (true) or shrinking (false). Used by the size shift slider.
        /// </summary>
        private bool isGrowingSize = true;

        /// <summary>
        /// Tracks when the user has begun drawing by pressing down the mouse or applying pressure, and if they
        /// actually affected the image (which might not happen based on the dynamic brush settings), and whether
        /// changes were made to the staged layer or not (there's a major performance boost for not having to use or
        /// draw the staged layer because that involves merging two layers in realtime as you draw).
        /// </summary>
        private (bool started, bool canvasChanged, bool stagedChanged) isUserDrawing = new(false, false, false);

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
        /// The file path identifying the currently loaded brush, or name for built-in brushes, or null if no saved brush is currently active.
        /// </summary>
        private string currentBrushPath = null;

        /// <summary>
        /// If set and using the brush tool (not eraser), the effect and its settings will be
        /// applied to the staged bmp when first starting to draw, and copied to committed bmp as
        /// the user draws.
        /// </summary>
        (Effect effect, EffectConfigToken settings) effectToDraw = (null, null);

        /// <summary>
        /// Contains the list of all available effects to use while drawing.
        /// </summary>
        BindingList<Tuple<string, IEffectInfo>> effectOptions;

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
        /// Stores the previous mouse location. This is used for calculating mouse direction, speed, etc.
        /// </summary>
        private PointF mouseLocPrev = new PointF();

        /// <summary>
        /// All user settings including custom brushes / brush image locations and the previous brush settings from
        /// the last time the effect was ran.
        /// </summary>
        private DynamicDrawSettings settings;

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
        /// The previous pressure ratio so lerping the sensitivity can be done for nonzero brush density.
        /// </summary>
        private float tabletPressureRatioPrev;

        /// <summary>
        /// The tablet reading service is async to this plugin, so any time this plugin runs faster, it might read the
        /// same packets multiple times. Tracking the serial number of the last packet ensures unique reads.
        /// </summary>
        private uint tabletLastPacketId;

        /// <summary>
        /// The folder used to store undo/redo images, and deleted on exit.
        /// </summary>
        private TempDirectory tempDir;

        /// <summary>
        /// The selected brush image path from the effect token.
        /// </summary>
        private string tokenSelectedBrushImagePath;

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
        private Label txtCanvasAngle;
        private TrackBar sliderCanvasAngle;
        private DoubleBufferedListView listviewBrushImagePicker;
        private Panel panelBrushAddPickColor;
        private CheckBox chkbxColorizeBrush;
        private Button bttnAddBrushImages;
        private ProgressBar brushImageLoadProgressBar;
        private Label txtColorInfluence;
        private TrackBar sliderColorInfluence;
        private FlowLayoutPanel panelColorInfluenceHSV;
        private CheckBox chkbxColorInfluenceHue;
        private CheckBox chkbxColorInfluenceSat;
        private CheckBox chkbxColorInfluenceVal;
        private Button bttnBrushColor;
        private Panel panelChosenEffect;
        private ComboBox cmbxChosenEffect;
        private Button bttnChooseEffectSettings;
        private ComboBox cmbxBlendMode;
        private Label txtBrushOpacity;
        private TrackBar sliderBrushOpacity;
        private Label txtBrushFlow;
        private TrackBar sliderBrushFlow;
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
        private CheckBox chkbxSeamlessDrawing;
        private CheckBox chkbxDitherDraw;
        private CheckBox chkbxLockAlpha;
        private Panel panelRGBLocks;
        private CheckBox chkbxLockR;
        private CheckBox chkbxLockG;
        private CheckBox chkbxLockB;
        private Panel panelHSVLocks;
        private CheckBox chkbxLockHue;
        private CheckBox chkbxLockSat;
        private CheckBox chkbxLockVal;
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
        private Label txtRandFlowLoss;
        private TrackBar sliderRandFlowLoss;
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
        private Label txtShiftFlow;
        private TrackBar sliderShiftFlow;
        private Accordion bttnTabAssignPressureControls;
        private FlowLayoutPanel panelTabletAssignPressure;
        private FlowLayoutPanel panelTabPressureBrushOpacity;
        private Panel panel19;
        private Label txtTabPressureBrushOpacity;
        private NumericUpDown spinTabPressureBrushOpacity;
        private CmbxTabletValueType cmbxTabPressureBrushOpacity;
        private FlowLayoutPanel panelTabPressureBrushFlow;
        private Panel panel3;
        private Label txtTabPressureBrushFlow;
        private NumericUpDown spinTabPressureBrushFlow;
        private CmbxTabletValueType cmbxTabPressureBrushFlow;
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
        private FlowLayoutPanel panelTabPressureRandFlowLoss;
        private Panel panel10;
        private Label lblTabPressureRandFlowLoss;
        private NumericUpDown spinTabPressureRandFlowLoss;
        private CmbxTabletValueType cmbxTabPressureRandFlowLoss;
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
        public WinDynamicDraw()
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

            // Fetches and populates the available effects for the effect chooser combobox.
            effectOptions = new BindingList<Tuple<string, IEffectInfo>>()
            {
                new Tuple<string, IEffectInfo>("None", null) // TODO: localize
            };

            cmbxChosenEffect.DataSource = effectOptions;
            cmbxChosenEffect.DisplayMember = "Item1";
            cmbxChosenEffect.ValueMember = "Item2";

            // Configures items the blend mode options combobox.
            blendModeOptions = new BindingList<Tuple<string, BlendMode>>
            {
                new Tuple<string, BlendMode>(Strings.BlendModeNormal, BlendMode.Normal),
                new Tuple<string, BlendMode>(Strings.BlendModeMultiply, BlendMode.Multiply),
                new Tuple<string, BlendMode>(Strings.BlendModeAdditive, BlendMode.Additive),
                new Tuple<string, BlendMode>(Strings.BlendModeColorBurn, BlendMode.ColorBurn),
                new Tuple<string, BlendMode>(Strings.BlendModeColorDodge, BlendMode.ColorDodge),
                new Tuple<string, BlendMode>(Strings.BlendModeReflect, BlendMode.Reflect),
                new Tuple<string, BlendMode>(Strings.BlendModeGlow, BlendMode.Glow),
                new Tuple<string, BlendMode>(Strings.BlendModeOverlay, BlendMode.Overlay),
                new Tuple<string, BlendMode>(Strings.BlendModeDifference, BlendMode.Difference),
                new Tuple<string, BlendMode>(Strings.BlendModeNegation, BlendMode.Negation),
                new Tuple<string, BlendMode>(Strings.BlendModeLighten, BlendMode.Lighten),
                new Tuple<string, BlendMode>(Strings.BlendModeDarken, BlendMode.Darken),
                new Tuple<string, BlendMode>(Strings.BlendModeScreen, BlendMode.Screen),
                new Tuple<string, BlendMode>(Strings.BlendModeXor, BlendMode.Xor),
                new Tuple<string, BlendMode>(Strings.BlendModeOverwrite, BlendMode.Overwrite)
            };
            cmbxBlendMode.DataSource = blendModeOptions;
            cmbxBlendMode.DisplayMember = "Item1";
            cmbxBlendMode.ValueMember = "Item2";

            // Prevents sliders and comboboxes from handling mouse wheel, so the user can scroll up/down normally.
            // Winforms designer doesn't recognize this event, so it immediately strips it if placed with autogen code.
            this.cmbxTabPressureBlueJitter.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxTabPressureBrushDensity.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxTabPressureBrushFlow.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxTabPressureBrushOpacity.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxTabPressureBrushRotation.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxTabPressureBrushSize.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxTabPressureGreenJitter.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxTabPressureHueJitter.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxTabPressureMinDrawDistance.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxTabPressureRandFlowLoss.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxTabPressureRandHorShift.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxTabPressureRandMaxSize.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxTabPressureRandMinSize.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxTabPressureRandRotLeft.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxTabPressureRandRotRight.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxTabPressureRandVerShift.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxTabPressureRedJitter.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxTabPressureSatJitter.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxTabPressureValueJitter.MouseWheel += IgnoreMouseWheelEvent;
            this.sliderCanvasZoom.MouseWheel += IgnoreMouseWheelEvent;
            this.sliderCanvasAngle.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxBlendMode.MouseWheel += IgnoreMouseWheelEvent;
            this.cmbxChosenEffect.MouseWheel += IgnoreMouseWheelEvent;
            this.sliderColorInfluence.MouseWheel += IgnoreMouseWheelEvent;
            this.sliderBrushFlow.MouseWheel += IgnoreMouseWheelEvent;
            this.sliderBrushOpacity.MouseWheel += IgnoreMouseWheelEvent;
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
            this.sliderRandFlowLoss.MouseWheel += IgnoreMouseWheelEvent;
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
            this.sliderShiftFlow.MouseWheel += IgnoreMouseWheelEvent;
            this.spinTabPressureBrushOpacity.MouseWheel += IgnoreMouseWheelEvent;
            this.spinTabPressureBrushFlow.MouseWheel += IgnoreMouseWheelEvent;
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
            this.spinTabPressureRandFlowLoss.MouseWheel += IgnoreMouseWheelEvent;
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

            // Loads custom brush images if possible, but skips duplicates. This method is called twice by Paint.NET,
            // so this ensures there are no duplicates.
            if (token.CustomBrushLocations.Count > 0 && !token.CustomBrushLocations.SetEquals(loadedBrushImagePaths))
            {
                loadedBrushImagePaths.UnionWith(token.CustomBrushLocations);
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
            token.CurrentBrushSettings.BrushFlow = UserSettings.userPrimaryColor.A;
            token.CurrentBrushSettings.BrushColor = UserSettings.userPrimaryColor;
            UpdateBrush(token.CurrentBrushSettings);
            UpdateBrushImage();

            // Fetches and populates the available effects for the effect chooser combobox.
            IEffectsService effectsService = this.Services?.GetService<IEffectsService>();
            if (effectOptions.Count == 1)
            {
                for (int i = 0; i < effectsService?.EffectInfos.Count; i++)
                {
                    if (effectsService.EffectInfos[i].Category != EffectCategory.DoNotDisplay)
                    {
                        effectOptions.Add(new Tuple<string, IEffectInfo>(
                            effectsService.EffectInfos[i].Name,
                            effectsService.EffectInfos[i]));
                    }
                }
            }
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
            token.CurrentBrushSettings.FlowChange = sliderShiftFlow.Value;
            token.CurrentBrushSettings.BlendMode = (BlendMode)cmbxBlendMode.SelectedIndex;
            // Skipping BrushFlow and BrushColor since they'll be overridden anyway
            token.CurrentBrushSettings.BrushDensity = sliderBrushDensity.Value;
            token.CurrentBrushSettings.AutomaticBrushDensity = chkbxAutomaticBrushDensity.Checked;
            token.CurrentBrushSettings.BrushImagePath = index >= 0
                ? loadedBrushImages[index].Location ?? loadedBrushImages[index].Name
                : Strings.DefaultBrushCircle;
            token.CurrentBrushSettings.BrushRotation = sliderBrushRotation.Value;
            token.CurrentBrushSettings.BrushSize = sliderBrushSize.Value;
            token.CurrentBrushSettings.ColorInfluence = sliderColorInfluence.Value;
            token.CurrentBrushSettings.ColorInfluenceHue = chkbxColorInfluenceHue.Checked;
            token.CurrentBrushSettings.ColorInfluenceSat = chkbxColorInfluenceSat.Checked;
            token.CurrentBrushSettings.ColorInfluenceVal = chkbxColorInfluenceVal.Checked;
            token.CurrentBrushSettings.DoColorizeBrush = chkbxColorizeBrush.Checked;
            token.CurrentBrushSettings.DoDitherDraw = chkbxDitherDraw.Checked;
            token.CurrentBrushSettings.DoLockAlpha = chkbxLockAlpha.Checked;
            token.CurrentBrushSettings.DoLockR = chkbxLockR.Checked;
            token.CurrentBrushSettings.DoLockG = chkbxLockG.Checked;
            token.CurrentBrushSettings.DoLockB = chkbxLockB.Checked;
            token.CurrentBrushSettings.DoLockHue = chkbxLockHue.Checked;
            token.CurrentBrushSettings.DoLockSat = chkbxLockSat.Checked;
            token.CurrentBrushSettings.DoLockVal = chkbxLockVal.Checked;
            token.CurrentBrushSettings.SeamlessDrawing = chkbxSeamlessDrawing.Checked;
            token.CurrentBrushSettings.DoRotateWithMouse = chkbxOrientToMouse.Checked;
            token.CurrentBrushSettings.MinDrawDistance = sliderMinDrawDistance.Value;
            token.CurrentBrushSettings.RandFlowLoss = sliderRandFlowLoss.Value;
            token.CurrentBrushSettings.RandHorzShift = sliderRandHorzShift.Value;
            token.CurrentBrushSettings.RandMaxB = sliderJitterMaxBlue.Value;
            token.CurrentBrushSettings.RandMaxG = sliderJitterMaxGreen.Value;
            token.CurrentBrushSettings.RandMaxR = sliderJitterMaxRed.Value;
            token.CurrentBrushSettings.RandMaxH = sliderJitterMaxHue.Value;
            token.CurrentBrushSettings.RandMaxS = sliderJitterMaxSat.Value;
            token.CurrentBrushSettings.RandMaxV = sliderJitterMaxVal.Value;
            token.CurrentBrushSettings.RandMaxSize = sliderRandMaxSize.Value;
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
            token.CurrentBrushSettings.CmbxChosenEffect = cmbxChosenEffect.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureBrushDensity = cmbxTabPressureBrushDensity.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureBrushFlow = cmbxTabPressureBrushFlow.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureBrushOpacity = cmbxTabPressureBrushOpacity.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureBrushRotation = cmbxTabPressureBrushRotation.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureBrushSize = cmbxTabPressureBrushSize.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureBlueJitter = cmbxTabPressureBlueJitter.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureGreenJitter = cmbxTabPressureGreenJitter.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureHueJitter = cmbxTabPressureHueJitter.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureMinDrawDistance = cmbxTabPressureMinDrawDistance.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureRedJitter = cmbxTabPressureRedJitter.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureSatJitter = cmbxTabPressureSatJitter.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureValueJitter = cmbxTabPressureValueJitter.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureRandFlowLoss = cmbxTabPressureRandFlowLoss.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureRandHorShift = cmbxTabPressureRandHorShift.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureRandMaxSize = cmbxTabPressureRandMaxSize.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureRandMinSize = cmbxTabPressureRandMinSize.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureRandRotLeft = cmbxTabPressureRandRotLeft.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureRandRotRight = cmbxTabPressureRandRotRight.SelectedIndex;
            token.CurrentBrushSettings.CmbxTabPressureRandVerShift = cmbxTabPressureRandVerShift.SelectedIndex;
            token.CurrentBrushSettings.TabPressureBrushDensity = (int)spinTabPressureBrushDensity.Value;
            token.CurrentBrushSettings.TabPressureBrushFlow = (int)spinTabPressureBrushFlow.Value;
            token.CurrentBrushSettings.TabPressureBrushOpacity = (int)spinTabPressureBrushOpacity.Value;
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
            token.CurrentBrushSettings.TabPressureRandFlowLoss = (int)spinTabPressureRandFlowLoss.Value;
            token.CurrentBrushSettings.TabPressureRandHorShift = (int)spinTabPressureRandHorShift.Value;
            token.CurrentBrushSettings.TabPressureRandMaxSize = (int)spinTabPressureRandMaxSize.Value;
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

            bmpCommitted = Utils.CreateBitmapFromSurface(EnvironmentParameters.SourceSurface);
            bmpStaged = new Bitmap(bmpCommitted.Width, bmpCommitted.Height, PixelFormat.Format32bppPArgb);
            bmpMerged = new Bitmap(bmpCommitted.Width, bmpCommitted.Height, PixelFormat.Format32bppPArgb);

            symmetryOrigin = new PointF(
                EnvironmentParameters.SourceSurface.Width / 2f,
                EnvironmentParameters.SourceSurface.Height / 2f);

            //Sets the canvas dimensions.
            canvas.x = (displayCanvas.Width - canvas.width) / 2;
            canvas.y = (displayCanvas.Height - canvas.height) / 2;

            // Sets the tooltip's maximum allowed dimensions.
            txtTooltip.MaximumSize = new Size(displayCanvas.Width, displayCanvas.Height);

            //Adds versioning information to the window title.
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            Text = EffectPlugin.StaticName + " (version " +
                version.Major + "." +
                version.Minor + ")";

            //Loads globalization texts for regional support.
            txtBrushOpacity.Text = string.Format("{0} {1}",
                Strings.BrushOpacity, sliderBrushOpacity.Value);

            txtBrushFlow.Text = string.Format("{0} {1}",
                Strings.BrushFlow, sliderBrushFlow.Value);

            txtBrushDensity.Text = string.Format("{0} {1}",
                Strings.BrushDensity, sliderBrushDensity.Value);

            txtBrushRotation.Text = string.Format("{0} {1}°",
                Strings.Rotation, sliderBrushRotation.Value);

            txtBrushSize.Text = string.Format("{0} {1}",
                Strings.Size, sliderBrushSize.Value);

            txtCanvasZoom.Text = string.Format("{0} {1}%",
                Strings.CanvasZoom, sliderCanvasZoom.Value);

            txtCanvasAngle.Text = string.Format("{0} {1}°",
                Strings.CanvasAngle, sliderCanvasAngle.Value);

            txtColorInfluence.Text = String.Format("{0} {1}%",
                Strings.ColorInfluence, sliderColorInfluence.Value);

            txtMinDrawDistance.Text = string.Format("{0} {1}",
                Strings.MinDrawDistance, sliderMinDrawDistance.Value);

            txtRandFlowLoss.Text = string.Format("{0} {1}",
                Strings.RandFlowLoss, sliderRandFlowLoss.Value);

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

            txtShiftFlow.Text = string.Format("{0} {1}",
                Strings.ShiftFlow, sliderShiftFlow.Value);

            txtShiftRotation.Text = string.Format("{0} {1}°",
                Strings.ShiftRotation, sliderShiftRotation.Value);

            txtShiftSize.Text = string.Format("{0} {1}",
                Strings.ShiftSize, sliderShiftSize.Value);

            UpdateTooltip(string.Empty);

            bttnAddBrushImages.Text = Strings.AddBrushImages;
            bttnBrushColor.Text = Strings.BrushColor;
            bttnCancel.Text = Strings.Cancel;
            bttnClearSettings.Text = Strings.ClearSettings;
            bttnCustomBrushImageLocations.Text = Strings.CustomBrushImageLocations;
            bttnOk.Text = Strings.Ok;
            bttnUndo.Text = Strings.Undo;
            bttnRedo.Text = Strings.Redo;

            chkbxColorizeBrush.Text = Strings.ColorizeBrush;
            chkbxColorInfluenceHue.Text = Strings.HueAbbr;
            chkbxColorInfluenceSat.Text = Strings.SatAbbr;
            chkbxColorInfluenceVal.Text = Strings.ValAbbr;
            chkbxLockAlpha.Text = Strings.LockAlpha;
            chkbxLockR.Text = Strings.LockR;
            chkbxLockG.Text = Strings.LockG;
            chkbxLockB.Text = Strings.LockB;
            chkbxLockHue.Text = Strings.LockHue;
            chkbxLockSat.Text = Strings.LockSat;
            chkbxLockVal.Text = Strings.LockVal;
            chkbxDitherDraw.Text = Strings.DitherDraw;
            chkbxSeamlessDrawing.Text = Strings.SeamlessDrawing;
            chkbxOrientToMouse.Text = Strings.OrientToMouse;
            chkbxAutomaticBrushDensity.Text = Strings.AutomaticBrushDensity;

            cmbxTabPressureBlueJitter.Text = Strings.JitterBlue;
            cmbxTabPressureBrushDensity.Text = Strings.BrushDensity;
            cmbxTabPressureBrushFlow.Text = Strings.BrushFlow;
            cmbxTabPressureBrushOpacity.Text = Strings.BrushOpacity;
            cmbxTabPressureBrushRotation.Text = Strings.Rotation;
            cmbxTabPressureBrushSize.Text = Strings.Size;
            cmbxTabPressureGreenJitter.Text = Strings.JitterGreen;
            cmbxTabPressureHueJitter.Text = Strings.JitterHue;
            cmbxTabPressureMinDrawDistance.Text = Strings.MinDrawDistance;
            cmbxTabPressureRandFlowLoss.Text = Strings.RandFlowLoss;
            cmbxTabPressureRandHorShift.Text = Strings.RandHorzShift;
            cmbxTabPressureRandMaxSize.Text = Strings.RandMaxSize;
            cmbxTabPressureRandMinSize.Text = Strings.RandMinSize;
            cmbxTabPressureRandRotLeft.Text = Strings.RandRotLeft;
            cmbxTabPressureRandRotRight.Text = Strings.RandRotRight;
            cmbxTabPressureRandVerShift.Text = Strings.RandVertShift;

            bttnDeleteBrush.Text = Strings.DeleteBrush;
            bttnSaveBrush.Text = Strings.SaveNewBrush;
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

                string path = Path.Combine(basePath, "DynamicDrawSettings.xml");
                settings = new DynamicDrawSettings(path);

                if (!File.Exists(path))
                {
                    // Migrate settings from the old settings filepath.
                    string legacyPath = Path.Combine(basePath, "BrushFactorySettings.xml");
                    if (File.Exists(legacyPath))
                    {
                        var legacySettings = new DynamicDrawSettings(legacyPath);
                        legacySettings.LoadSavedSettings();
                        settings.CustomBrushImageDirectories = legacySettings.CustomBrushImageDirectories;
                        settings.UseDefaultBrushes = legacySettings.UseDefaultBrushes;
                    }

                    settings.Save(true);

                    // Delete the old settings file if present, after migration.
                    if (File.Exists(legacyPath))
                    {
                        File.Delete(legacyPath);
                    }
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

                    listviewBrushPicker.AutoResizeColumn(0, ColumnHeaderAutoResizeStyle.ColumnContent);
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

            HashSet<ShortcutContext> contexts = new HashSet<ShortcutContext>();
            if (displayCanvas.Focused) { contexts.Add(ShortcutContext.OnCanvas); }
            else { contexts.Add(ShortcutContext.OnSidebar); }

            KeyShortcutManager.FireShortcuts(keyboardShortcuts, e.KeyCode, e.Control, e.Shift, e.Alt, contexts);

            //Display a hand icon while panning.
            if (e.Control || e.KeyCode == Keys.Space)
            {
                Cursor = Cursors.Hand;
            }

            // [, Ctrl + [ increase rotation, flow, size. ], Ctrl + ] decrease.
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
                    sliderBrushRotation.Value = Math.Clamp(
                        sliderBrushRotation.Value + amountChange,
                        sliderBrushRotation.Minimum,
                        sliderBrushRotation.Maximum);
                }
                else if (KeyShortcutManager.IsKeyDown(Keys.O))
                {
                    sliderBrushOpacity.Value = Math.Clamp(
                        sliderBrushOpacity.Value + amountChange,
                        sliderBrushOpacity.Minimum,
                        sliderBrushOpacity.Maximum);
                }
                else if (KeyShortcutManager.IsKeyDown(Keys.F))
                {
                    sliderBrushFlow.Value = Math.Clamp(
                        sliderBrushFlow.Value + amountChange,
                        sliderBrushFlow.Minimum,
                        sliderBrushFlow.Maximum);
                }
                else
                {
                    sliderBrushSize.Value = Math.Clamp(
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
            if (!e.Control && e.KeyCode != Keys.Space)
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

                    sliderBrushSize.Value = Math.Clamp(
                    sliderBrushSize.Value + Math.Sign(e.Delta) * changeFactor,
                    sliderBrushSize.Minimum,
                    sliderBrushSize.Maximum);
                }

                //Ctrl + R + Wheel: Changes the brush rotation.
                else if (KeyShortcutManager.IsKeyDown(Keys.R))
                {
                    int newValue = sliderBrushRotation.Value + Math.Sign(e.Delta) * 20;
                    while (newValue < -sliderBrushRotation.Maximum) { newValue += sliderBrushRotation.Maximum * 2; }
                    while (newValue >= sliderBrushRotation.Maximum) { newValue -= sliderBrushRotation.Maximum * 2; }
                    sliderBrushRotation.Value = newValue;
                }

                //Ctrl + O + Wheel: Changes the brush opacity.
                else if (KeyShortcutManager.IsKeyDown(Keys.O))
                {
                    sliderBrushOpacity.Value = Math.Clamp(
                    sliderBrushOpacity.Value + Math.Sign(e.Delta) * 10,
                    sliderBrushOpacity.Minimum,
                    sliderBrushOpacity.Maximum);
                }

                //Ctrl + F + Wheel: Changes the brush flow.
                else if (KeyShortcutManager.IsKeyDown(Keys.F))
                {
                    sliderBrushFlow.Value = Math.Clamp(
                    sliderBrushFlow.Value + Math.Sign(e.Delta) * 10,
                    sliderBrushFlow.Minimum,
                    sliderBrushFlow.Maximum);
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
                int newValue = sliderCanvasAngle.Value + Math.Sign(e.Delta) * 10;
                while (newValue < 0) { newValue += 360; }
                while (newValue >= 360) { newValue -= 360; }
                sliderCanvasAngle.Value = newValue;

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
            if (DialogResult == DialogResult.None)
            {
                e.Cancel = true;
            }

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

                bmpBrush?.Dispose();
                bmpBrushDownsized?.Dispose();
                bmpBrushEffects?.Dispose();
                bmpCommitted?.Dispose();
                bmpStaged?.Dispose();
                bmpMerged?.Dispose();
                bmpBackgroundClipboard?.Dispose();

                displayCanvas.Cursor = Cursors.Default;
                cursorColorPicker?.Dispose();
                selectionOutline?.Dispose();

                tabletService?.Dispose();
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
        private void DrawBrush(PointF loc, int radius, float pressure)
        {
            // It's possible to try to draw before the plugin has initialized (indicated by not having the bitmap set).
            if (bmpBrushEffects == null)
            {
                return;
            }

            // Updates undo stack on first brush stroke.
            if (!isUserDrawing.canvasChanged)
            {
                isUserDrawing.canvasChanged = true;

                //Adds to the list of undo operations.
                string path = tempDir.GetTempPathName("HistoryBmp" + undoHistory.Count + ".undo");

                //Saves the drawing to the file and saves the file path.
                bmpCommitted.Save(path);
                undoHistory.Push(path);
                if (!bttnUndo.Enabled)
                {
                    bttnUndo.Enabled = true;
                }

                //Removes all redo history.
                redoHistory.Clear();
            }

            #region apply size jitter
            // Change the brush size based on settings.
            int finalRandMinSize = Utils.GetStrengthMappedValue(sliderRandMinSize.Value,
                (int)spinTabPressureRandMinSize.Value,
                sliderRandMinSize.Maximum,
                pressure,
                ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureRandMinSize.SelectedItem).ValueMember);

            int finalRandMaxSize = Utils.GetStrengthMappedValue(sliderRandMaxSize.Value,
                (int)spinTabPressureRandMaxSize.Value,
                sliderRandMaxSize.Maximum,
                pressure,
                ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureRandMaxSize.SelectedItem).ValueMember);

            int newRadius = Math.Clamp(radius
                - random.Next(finalRandMinSize)
                + random.Next(finalRandMaxSize), 0, int.MaxValue);
            #endregion

            #region update brush smoothing mode
            // Update the brush draw rate based on the size.
            UpdateBrushDensity(newRadius);
            #endregion

            //Sets the new brush location because the brush stroke succeeded.
            mouseLocBrush = mouseLoc;

            #region apply size/flow/rotation shift
            // Updates the brush size slider (doesn't affect this brush stroke).
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

                sliderBrushSize.Value = Math.Clamp(tempSize,
                    sliderBrushSize.Minimum, sliderBrushSize.Maximum);
            }

            // Updates the brush flow (doesn't affect this brush stroke).
            if (sliderShiftFlow.Value != 0)
            {
                int tempFlow = sliderBrushFlow.Value;
                if (isGrowingFlow)
                {
                    tempFlow += sliderShiftFlow.Value;
                }
                else
                {
                    tempFlow -= sliderShiftFlow.Value;
                }
                if (tempFlow > sliderBrushFlow.Maximum)
                {
                    tempFlow = sliderBrushFlow.Maximum;
                    isGrowingFlow = !isGrowingFlow; //handles values < 0.
                }
                else if (tempFlow < sliderBrushFlow.Minimum)
                {
                    tempFlow = sliderBrushFlow.Minimum;
                    isGrowingFlow = !isGrowingFlow;
                }

                sliderBrushFlow.Value = Math.Clamp(tempFlow,
                    sliderBrushFlow.Minimum, sliderBrushFlow.Maximum);
            }
            else if (pressure > 0 && (
                (CmbxTabletValueType.CmbxEntry)cmbxTabPressureBrushFlow.SelectedItem).ValueMember
                != ConstraintValueHandlingMethod.DoNothing)
            {
                // If not changing sliderBrushFlow already by shifting it in the if-statement above, the brush has to
                // be manually redrawn when modifying brush alpha. This is done to avoid editing sliderBrushFlow and
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

                sliderBrushRotation.Value = Math.Clamp(tempRot,
                    sliderBrushRotation.Minimum, sliderBrushRotation.Maximum);
            }
            #endregion

            #region apply position jitter
            int finalRandHorzShift = Math.Clamp(Utils.GetStrengthMappedValue(sliderRandHorzShift.Value,
                (int)spinTabPressureRandHorShift.Value,
                sliderRandHorzShift.Maximum,
                pressure,
                ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureRandHorShift.SelectedItem).ValueMember),
                0, 100);

            int finalRandVertShift = Math.Clamp(Utils.GetStrengthMappedValue(sliderRandVertShift.Value,
                (int)spinTabPressureRandVerShift.Value,
                sliderRandVertShift.Maximum,
                pressure,
                ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureRandVerShift.SelectedItem).ValueMember),
                0, 100);

            //Randomly shifts the image by some percent of the canvas size,
            //horizontally and/or vertically.
            if (finalRandHorzShift != 0 ||
                finalRandVertShift != 0)
            {
                loc.X = loc.X
                    - bmpCommitted.Width * (finalRandHorzShift / 200f)
                    + bmpCommitted.Width * (random.Next(finalRandHorzShift) / 100f);

                loc.Y = loc.Y
                    - bmpCommitted.Height * (finalRandVertShift / 200f)
                    + bmpCommitted.Height * (random.Next(finalRandVertShift) / 100f);
            }
            #endregion

            // Avoid subpixel rendering at 1px width since it'd make the line entirely antialiasing.
            if (radius == 1 &&
                chkbxAutomaticBrushDensity.Checked &&
                (CmbxSmoothing.Smoothing)cmbxBrushSmoothing.SelectedValue != CmbxSmoothing.Smoothing.Jagged)
            {
                loc.X = (int)Math.Round(loc.X);
                loc.Y = (int)Math.Round(loc.Y);
            }

            #region apply rotation jitter + rotate with mouse option
            // Calculates the final brush rotation based on all factors. Counters canvas rotation to remain unaffected.
            int finalBrushRotation = Utils.GetStrengthMappedValue(sliderBrushRotation.Value,
                (int)spinTabPressureBrushRotation.Value,
                sliderBrushRotation.Maximum,
                pressure,
                ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureBrushRotation.SelectedItem).ValueMember);

            int finalRandRotLeft = Utils.GetStrengthMappedValue(sliderRandRotLeft.Value,
                (int)spinTabPressureRandRotLeft.Value,
                sliderRandRotLeft.Maximum,
                pressure,
                ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureRandRotLeft.SelectedItem).ValueMember);

            int finalRandRotRight = Utils.GetStrengthMappedValue(sliderRandRotRight.Value,
                (int)spinTabPressureRandRotRight.Value,
                sliderRandRotRight.Maximum,
                pressure,
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
            #endregion

            #region apply alpha jitter (via flow)
            int finalRandFlowLoss = Math.Clamp(Utils.GetStrengthMappedValue(sliderRandFlowLoss.Value,
                (int)spinTabPressureRandFlowLoss.Value,
                sliderRandFlowLoss.Maximum,
                pressure,
                ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureRandFlowLoss.SelectedItem).ValueMember),
                0, 255);
            #endregion

            #region apply color jitter
            ImageAttributes recolorMatrix = null;
            ColorBgra adjustedColor = bttnBrushColor.BackColor;
            int newFlowLoss = random.Next(finalRandFlowLoss);
            adjustedColor.A = (byte)Math.Round(Math.Clamp(sliderBrushFlow.Value - newFlowLoss, 0f, 255f));

            if (activeTool == Tool.Eraser)
            {
                if (newFlowLoss != 0)
                {
                    recolorMatrix = Utils.ColorImageAttr(1, 1, 1, (255 - newFlowLoss) / 255f);
                }
            }
            else if (chkbxColorizeBrush.Checked || sliderColorInfluence.Value != 0 || cmbxBlendMode.SelectedIndex == (int)BlendMode.Overwrite)
            {
                int finalJitterMaxRed = Math.Clamp(Utils.GetStrengthMappedValue(sliderJitterMaxRed.Value,
                    (int)spinTabPressureMaxRedJitter.Value,
                    sliderJitterMaxRed.Maximum,
                    pressure,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureRedJitter.SelectedItem).ValueMember),
                    0, 100);

                int finalJitterMinRed = Math.Clamp(Utils.GetStrengthMappedValue(sliderJitterMinRed.Value,
                    (int)spinTabPressureMinRedJitter.Value,
                    sliderJitterMinRed.Maximum,
                    pressure,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureRedJitter.SelectedItem).ValueMember),
                    0, 100);

                int finalJitterMaxGreen = Math.Clamp(Utils.GetStrengthMappedValue(sliderJitterMaxGreen.Value,
                    (int)spinTabPressureMaxGreenJitter.Value,
                    sliderJitterMaxGreen.Maximum,
                    pressure,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureGreenJitter.SelectedItem).ValueMember),
                    0, 100);

                int finalJitterMinGreen = Math.Clamp(Utils.GetStrengthMappedValue(sliderJitterMinGreen.Value,
                    (int)spinTabPressureMinGreenJitter.Value,
                    sliderJitterMinGreen.Maximum,
                    pressure,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureGreenJitter.SelectedItem).ValueMember),
                    0, 100);

                int finalJitterMaxBlue = Math.Clamp(Utils.GetStrengthMappedValue(sliderJitterMaxBlue.Value,
                    (int)spinTabPressureMaxBlueJitter.Value,
                    sliderJitterMaxBlue.Maximum,
                    pressure,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureBlueJitter.SelectedItem).ValueMember),
                    0, 100);

                int finalJitterMinBlue = Math.Clamp(Utils.GetStrengthMappedValue(sliderJitterMinBlue.Value,
                    (int)spinTabPressureMinBlueJitter.Value,
                    sliderJitterMinBlue.Maximum,
                    pressure,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureBlueJitter.SelectedItem).ValueMember),
                    0, 100);

                int finalJitterMaxHue = Math.Clamp(Utils.GetStrengthMappedValue(sliderJitterMaxHue.Value,
                    (int)spinTabPressureMaxHueJitter.Value,
                    sliderJitterMaxHue.Maximum,
                    pressure,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureHueJitter.SelectedItem).ValueMember),
                    0, 100);

                int finalJitterMinHue = Math.Clamp(Utils.GetStrengthMappedValue(sliderJitterMinHue.Value,
                    (int)spinTabPressureMinHueJitter.Value,
                    sliderJitterMinHue.Maximum,
                    pressure,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureHueJitter.SelectedItem).ValueMember),
                    0, 100);

                int finalJitterMaxSat = Math.Clamp(Utils.GetStrengthMappedValue(sliderJitterMaxSat.Value,
                    (int)spinTabPressureMaxSatJitter.Value,
                    sliderJitterMaxSat.Maximum,
                    pressure,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureSatJitter.SelectedItem).ValueMember),
                    0, 100);

                int finalJitterMinSat = Math.Clamp(Utils.GetStrengthMappedValue(sliderJitterMinSat.Value,
                    (int)spinTabPressureMinSatJitter.Value,
                    sliderJitterMinSat.Maximum,
                    pressure,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureSatJitter.SelectedItem).ValueMember),
                    0, 100);

                int finalJitterMaxVal = Math.Clamp(Utils.GetStrengthMappedValue(sliderJitterMaxVal.Value,
                    (int)spinTabPressureMaxValueJitter.Value,
                    sliderJitterMaxVal.Maximum,
                    pressure,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureValueJitter.SelectedItem).ValueMember),
                    0, 100);

                int finalJitterMinVal = Math.Clamp(Utils.GetStrengthMappedValue(sliderJitterMinVal.Value,
                    (int)spinTabPressureMinValueJitter.Value,
                    sliderJitterMinVal.Maximum,
                    pressure,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureValueJitter.SelectedItem).ValueMember),
                    0, 100);

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

                if (newFlowLoss != 0 || jitterRgb || jitterHsv)
                {
                    // the brush image is already alpha multiplied for speed when the user sets brush flow, so
                    // newAlpha just mixes the remainder from jitter, which is why it does 255 minus the jitter.
                    // Non-standard blend modes need to use the brush as an alpha mask, so they don't multiply brush
                    // transparency into the brush early on.
                    float newAlpha = (activeTool == Tool.Eraser || cmbxBlendMode.SelectedIndex != (int)BlendMode.Overwrite)
                        ? Math.Clamp((255 - newFlowLoss) / 255f, 0f, 1f)
                        : adjustedColor.A / 255f;

                    float newRed = bttnBrushColor.BackColor.R / 255f;
                    float newGreen = bttnBrushColor.BackColor.G / 255f;
                    float newBlue = bttnBrushColor.BackColor.B / 255f;

                    //Sets RGB color jitter.
                    if (jitterRgb)
                    {
                        newBlue = Math.Clamp((bttnBrushColor.BackColor.B / 2.55f
                            - random.Next(finalJitterMinBlue)
                            + random.Next(finalJitterMaxBlue)) / 100f, 0, 1);

                        newGreen = Math.Clamp((bttnBrushColor.BackColor.G / 2.55f
                            - random.Next(finalJitterMinGreen)
                            + random.Next(finalJitterMaxGreen)) / 100f, 0, 1);

                        newRed = Math.Clamp((bttnBrushColor.BackColor.R / 2.55f
                            - random.Next(finalJitterMinRed)
                            + random.Next(finalJitterMaxRed)) / 100f, 0, 1);
                    }

                    // Sets HSV color jitter.
                    if (jitterHsv)
                    {
                        HsvColor colorHsv = new RgbColor((int)(newRed * 255f), (int)(newGreen * 255f), (int)(newBlue * 255f))
                            .ToHsv();

                        int newHue = Math.Clamp(colorHsv.Hue
                            - random.Next((int)(finalJitterMinHue * 3.6f))
                            + random.Next((int)(finalJitterMaxHue * 3.6f)), 0, 360);

                        int newSat = Math.Clamp(colorHsv.Saturation
                            - random.Next(finalJitterMinSat)
                            + random.Next(finalJitterMaxSat), 0, 100);

                        int newVal = Math.Clamp(colorHsv.Value
                            - random.Next(finalJitterMinVal)
                            + random.Next(finalJitterMaxVal), 0, 100);

                        Color finalColor = new HsvColor(newHue, newSat, newVal).ToColor();

                        newRed = finalColor.R / 255f;
                        newGreen = finalColor.G / 255f;
                        newBlue = finalColor.B / 255f;
                    }

                    recolorMatrix = Utils.ColorImageAttr(newRed, newGreen, newBlue, newAlpha);
                    adjustedColor = ColorBgra.FromBgra(
                        (byte)Math.Round(newBlue * 255),
                        (byte)Math.Round(newGreen * 255),
                        (byte)Math.Round(newRed * 255),
                        (byte)Math.Round(newAlpha * 255));
                }
            }
            #endregion

            byte finalOpacity = (byte)Math.Clamp(Utils.GetStrengthMappedValue(sliderBrushOpacity.Value,
                (int)spinTabPressureBrushOpacity.Value,
                sliderBrushOpacity.Maximum,
                pressure,
                ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureBrushOpacity.SelectedItem).ValueMember),
                0, 255);

            // The staged bitmap is only needed for layer opacity and layer blend modes, because the brush stroke
            // opacity becomes important in calculating separately from the regular drawing. The staged bitmap will
            // be drawn the whole time and when done with the brush stroke, so it's tracked to know when committing to
            // it is necessary, to save speed. Note that the eraser tool always erases on the committed bitmap.
            bool drawToStagedBitmap = activeTool != Tool.Eraser &&
                (isUserDrawing.stagedChanged
                || BlendModeUtils.BlendModeToUserBlendOp((BlendMode)cmbxBlendMode.SelectedIndex) != null
                || finalOpacity != sliderBrushOpacity.Maximum);

            if (drawToStagedBitmap && !isUserDrawing.stagedChanged)
            {
                isUserDrawing.stagedChanged = true;
                Utils.OverwriteBits(bmpCommitted, bmpMerged);
            }

            Bitmap bmpToDrawOn = drawToStagedBitmap ? bmpStaged : bmpCommitted;

            // Draws the brush.
            using (Graphics g = Graphics.FromImage(bmpToDrawOn))
            {
                // Lockbits is needed for overwrite blend mode, channel locks, and seamless drawing.
                bool useLockbitsDrawing = activeTool == Tool.Eraser
                    || cmbxBlendMode.SelectedIndex == (int)BlendMode.Overwrite
                    || finalOpacity != sliderBrushOpacity.Maximum
                    || chkbxSeamlessDrawing.Checked
                    || chkbxLockAlpha.Checked
                    || chkbxLockR.Checked || chkbxLockG.Checked || chkbxLockB.Checked
                    || chkbxLockHue.Checked || chkbxLockSat.Checked || chkbxLockVal.Checked
                    || chkbxDitherDraw.Checked
                    || !chkbxColorizeBrush.Checked
                    || (CmbxSmoothing.Smoothing)cmbxBrushSmoothing.SelectedIndex == CmbxSmoothing.Smoothing.Jagged;

                // Adjust to center for pixel perfect drawing.
                if (useLockbitsDrawing)
                {
                    loc.X += halfPixelOffset;
                    loc.Y += halfPixelOffset;
                }

                // Overwrite blend mode and uncolorized images don't use the recolor matrix.
                if (activeTool != Tool.Eraser)
                {
                    if (useLockbitsDrawing && cmbxBlendMode.SelectedIndex == (int)BlendMode.Overwrite)
                    {
                        recolorMatrix = null;
                    }
                    else if (!chkbxColorizeBrush.Checked)
                    {
                        recolorMatrix = null;
                    }
                }

                #region create intermediate rotated brush (as needed)
                // Manually rotates to account for canvas angle (lockbits doesn't use matrices).
                if (useLockbitsDrawing && sliderCanvasAngle.Value != 0)
                {
                    rotation -= sliderCanvasAngle.Value;
                }

                Bitmap bmpBrushRot = bmpBrushEffects;
                bool isJagged = cmbxBrushSmoothing.SelectedIndex == (int)CmbxSmoothing.Smoothing.Jagged;
                if (rotation != 0 || adjustedColor.A != 255)
                {
                    bmpBrushRot = isJagged
                        ? Utils.RotateImage(bmpBrushEffects, rotation, adjustedColor.A)
                        : Utils.RotateImage(bmpBrushEffects, rotation);
                }
                else if (isJagged)
                {
                    bmpBrushRot = Utils.AliasImageCopy(bmpBrushEffects, 255);
                }

                //Rotating the brush increases image bounds, so brush space
                //must increase to avoid making it visually shrink.
                double radAngle = (Math.Abs(rotation) % 90) * Math.PI / 180;
                float rotScaleFactor = (float)(Math.Cos(radAngle) + Math.Sin(radAngle));
                #endregion

                // The final computed brush radius.
                int scaleFactor = (int)(newRadius * rotScaleFactor);

                // Sets the interpolation mode based on preferences.
                g.InterpolationMode = CmbxSmoothing.SmoothingToInterpolationMode[(CmbxSmoothing.Smoothing)cmbxBrushSmoothing.SelectedValue];

                // Compensate for brush origin being top-left corner instead of center.
                float drawingOffsetX = bmpCommitted.Width * 0.5f;
                float drawingOffsetY = bmpCommitted.Height * 0.5f;

                // Moves where the brush stroke is applied to match the user's canvas rotation settings.
                if (!useLockbitsDrawing)
                {
                    g.TranslateTransform(drawingOffsetX, drawingOffsetY);
                    g.RotateTransform(-sliderCanvasAngle.Value);
                    g.TranslateTransform(-drawingOffsetX, -drawingOffsetY);
                }

                #region Draw the brush with blend modes or without a recolor matrix, considering symmetry
                if (recolorMatrix == null || useLockbitsDrawing)
                {
                    // Can't draw 0 dimension images.
                    if (scaleFactor == 0)
                    {
                        return;
                    }

                    //Draws the brush for normal and non-radial symmetry.
                    if (cmbxSymmetry.SelectedIndex < 5)
                    {
                        if (useLockbitsDrawing)
                        {
                            PointF rotatedLoc = loc;
                            if (sliderCanvasAngle.Value != 0)
                            {
                                rotatedLoc = TransformPoint(loc, true, true, false);
                            }

                            using (Bitmap bmpBrushRotScaled = Utils.ScaleImage(bmpBrushRot, new Size(scaleFactor, scaleFactor), false, false, recolorMatrix,
                                (CmbxSmoothing.Smoothing)cmbxBrushSmoothing.SelectedIndex))
                            {
                                if (activeTool == Tool.Eraser)
                                {
                                    Utils.OverwriteMasked(
                                        this.EnvironmentParameters.SourceSurface,
                                        bmpCommitted,
                                        bmpBrushRotScaled,
                                        new Point(
                                            (int)Math.Round(rotatedLoc.X - (scaleFactor / 2f)),
                                            (int)Math.Round(rotatedLoc.Y - (scaleFactor / 2f))),
                                        (chkbxLockR.Checked, chkbxLockG.Checked, chkbxLockB.Checked,
                                        chkbxLockHue.Checked, chkbxLockSat.Checked, chkbxLockVal.Checked),
                                        chkbxSeamlessDrawing.Checked,
                                        chkbxDitherDraw.Checked);
                                }
                                else
                                {
                                    Utils.DrawMasked(
                                        bmpToDrawOn,
                                        bmpBrushRotScaled,
                                        new Point(
                                            (int)Math.Round(rotatedLoc.X - (scaleFactor / 2f)),
                                            (int)Math.Round(rotatedLoc.Y - (scaleFactor / 2f))),
                                        (adjustedColor, newFlowLoss, finalOpacity),
                                        chkbxColorizeBrush.Checked ? null : sliderColorInfluence.Value == 0 ? (100, false, false, false) : (
                                            sliderColorInfluence.Value, chkbxColorInfluenceHue.Checked,
                                            chkbxColorInfluenceSat.Checked, chkbxColorInfluenceVal.Checked),
                                        (BlendMode)cmbxBlendMode.SelectedIndex,
                                        drawToStagedBitmap ? (false, false, false, false, false, false, false) :
                                        (chkbxLockAlpha.Checked,
                                        chkbxLockR.Checked, chkbxLockG.Checked, chkbxLockB.Checked, 
                                        chkbxLockHue.Checked, chkbxLockSat.Checked, chkbxLockVal.Checked),
                                        chkbxSeamlessDrawing.Checked,
                                        chkbxDitherDraw.Checked,
                                        mergeRegions);
                                }
                            }
                        }
                        else
                        {
                            if (drawToStagedBitmap)
                            {
                                PointF rotatedLoc = loc;
                                if (sliderCanvasAngle.Value != 0)
                                {
                                    rotatedLoc = TransformPoint(loc, true, true, false);
                                }

                                mergeRegions.Add(new Rectangle(
                                    (int)Math.Round(rotatedLoc.X - (scaleFactor / 2f)),
                                    (int)Math.Round(rotatedLoc.Y - (scaleFactor / 2f)),
                                    scaleFactor, scaleFactor
                                ));
                            }

                            g.DrawImage(
                                bmpBrushRot,
                                loc.X - (scaleFactor / 2f),
                                loc.Y - (scaleFactor / 2f),
                                scaleFactor,
                                scaleFactor);
                        }
                    }

                    //Draws the brush horizontally reflected.
                    if (cmbxSymmetry.SelectedIndex == (int)SymmetryMode.Horizontal ||
                        cmbxSymmetry.SelectedIndex == (int)SymmetryMode.Vertical ||
                        cmbxSymmetry.SelectedIndex == (int)SymmetryMode.Star2)
                    {
                        bool symmetryX = cmbxSymmetry.SelectedIndex == (int)SymmetryMode.Vertical;
                        bool symmetryY = cmbxSymmetry.SelectedIndex == (int)SymmetryMode.Horizontal;

                        // Easier not to compute on a rotated canvas.
                        g.ResetTransform();

                        //Gets the symmetry origin.
                        PointF origin = new PointF(
                            symmetryOrigin.X,
                            symmetryOrigin.Y);

                        //Gets the drawn location relative to symmetry origin.
                        PointF rotatedLoc = TransformPoint(loc, true, true, false);
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

                        float halfScaleFactor = (scaleFactor / 2f);
                        float xDist = (float)(dist * Math.Cos(angle));
                        float yDist = (float)(dist * Math.Sin(angle));

                        if (useLockbitsDrawing)
                        {
                            using (Bitmap bmpBrushRotScaled = Utils.ScaleImage(
                                bmpBrushRot, new Size(scaleFactor, scaleFactor), !symmetryX, !symmetryY, null, (CmbxSmoothing.Smoothing)cmbxBrushSmoothing.SelectedIndex))
                            {
                                if (activeTool == Tool.Eraser)
                                {
                                    Utils.OverwriteMasked(
                                        this.EnvironmentParameters.SourceSurface,
                                        bmpCommitted,
                                        bmpBrushRotScaled,
                                        new Point(
                                            (int)Math.Round(origin.X - halfScaleFactor + (symmetryX ? xDist : -xDist)),
                                            (int)Math.Round(origin.Y - halfScaleFactor + (symmetryY ? yDist : -yDist))),
                                        (chkbxLockR.Checked, chkbxLockG.Checked, chkbxLockB.Checked, 
                                        chkbxLockHue.Checked, chkbxLockSat.Checked, chkbxLockVal.Checked),
                                        chkbxSeamlessDrawing.Checked,
                                        chkbxDitherDraw.Checked);
                                }
                                else
                                {
                                    Utils.DrawMasked(
                                        bmpToDrawOn,
                                        bmpBrushRotScaled,
                                        new Point(
                                            (int)Math.Round(origin.X - halfScaleFactor + (symmetryX ? xDist : -xDist)),
                                            (int)Math.Round(origin.Y - halfScaleFactor + (symmetryY ? yDist : -yDist))),
                                        (adjustedColor, newFlowLoss, finalOpacity),
                                        chkbxColorizeBrush.Checked ? null : sliderColorInfluence.Value == 0 ? (100, false, false, false) : (
                                            sliderColorInfluence.Value, chkbxColorInfluenceHue.Checked,
                                            chkbxColorInfluenceSat.Checked, chkbxColorInfluenceVal.Checked),
                                        (BlendMode)cmbxBlendMode.SelectedIndex,
                                        drawToStagedBitmap ? (false, false, false, false, false, false, false) :
                                        (chkbxLockAlpha.Checked,
                                        chkbxLockR.Checked, chkbxLockG.Checked, chkbxLockB.Checked,
                                        chkbxLockHue.Checked, chkbxLockSat.Checked, chkbxLockVal.Checked),
                                        chkbxSeamlessDrawing.Checked,
                                        chkbxDitherDraw.Checked,
                                        mergeRegions);
                                }
                            }
                        }
                        else
                        {
                            if (drawToStagedBitmap)
                            {
                                mergeRegions.Add(new Rectangle(
                                    (int)(origin.X + (symmetryX ? -halfScaleFactor + xDist : halfScaleFactor - scaleFactor - xDist)),
                                    (int)(origin.Y + (symmetryY ? -halfScaleFactor + yDist : halfScaleFactor - scaleFactor - yDist)),
                                    scaleFactor,
                                    scaleFactor));
                            }

                            g.DrawImage(
                                bmpBrushRot,
                                origin.X + (symmetryX ? -halfScaleFactor + xDist : halfScaleFactor - xDist),
                                origin.Y + (symmetryY ? -halfScaleFactor + yDist : halfScaleFactor - yDist),
                                symmetryX ? scaleFactor : -scaleFactor,
                                symmetryY ? scaleFactor : -scaleFactor);
                        }
                    }

                    // Draws at defined offset locations.
                    else if (cmbxSymmetry.SelectedIndex ==
                        (int)SymmetryMode.SetPoints)
                    {
                        if (useLockbitsDrawing)
                        {
                            using (Bitmap bmpBrushRotScaled = Utils.ScaleImage(bmpBrushRot, new Size(scaleFactor, scaleFactor), false, false, null,
                                (CmbxSmoothing.Smoothing)cmbxBrushSmoothing.SelectedIndex))
                            {
                                float halfScaleFactor = scaleFactor / 2f;

                                for (int i = 0; i < symmetryOrigins.Count; i++)
                                {
                                    PointF transformedPoint = new PointF(
                                        loc.X + symmetryOrigins[i].X,
                                        loc.Y + symmetryOrigins[i].Y);

                                    if (sliderCanvasAngle.Value != 0)
                                    {
                                        transformedPoint = TransformPoint(transformedPoint, true, true, false);
                                    }

                                    if (activeTool == Tool.Eraser)
                                    {
                                        Utils.OverwriteMasked(
                                            this.EnvironmentParameters.SourceSurface,
                                            bmpCommitted,
                                            bmpBrushRotScaled,
                                            new Point(
                                                (int)Math.Round(transformedPoint.X - halfScaleFactor),
                                                (int)Math.Round(transformedPoint.Y - halfScaleFactor)),
                                            (chkbxLockR.Checked, chkbxLockG.Checked, chkbxLockB.Checked, 
                                            chkbxLockHue.Checked, chkbxLockSat.Checked, chkbxLockVal.Checked),
                                            chkbxSeamlessDrawing.Checked,
                                            chkbxDitherDraw.Checked);
                                    }
                                    else
                                    {
                                        Utils.DrawMasked(
                                            bmpToDrawOn,
                                            bmpBrushRotScaled,
                                            new Point(
                                                (int)Math.Round(transformedPoint.X - halfScaleFactor),
                                                (int)Math.Round(transformedPoint.Y - halfScaleFactor)),
                                            (adjustedColor, newFlowLoss, finalOpacity),
                                            chkbxColorizeBrush.Checked ? null : sliderColorInfluence.Value == 0 ? (100, false, false, false) : (
                                                sliderColorInfluence.Value, chkbxColorInfluenceHue.Checked,
                                                chkbxColorInfluenceSat.Checked, chkbxColorInfluenceVal.Checked),
                                            (BlendMode)cmbxBlendMode.SelectedIndex,
                                            drawToStagedBitmap ? (false, false, false, false, false, false, false) :
                                            (chkbxLockAlpha.Checked,
                                            chkbxLockR.Checked, chkbxLockG.Checked, chkbxLockB.Checked, 
                                            chkbxLockHue.Checked, chkbxLockSat.Checked, chkbxLockVal.Checked),
                                            chkbxSeamlessDrawing.Checked,
                                            chkbxDitherDraw.Checked,
                                        mergeRegions);
                                    }
                                }
                            }
                        }
                        else
                        {
                            for (int i = 0; i < symmetryOrigins.Count; i++)
                            {
                                if (drawToStagedBitmap)
                                {
                                    mergeRegions.Add(new Rectangle(
                                        (int)(loc.X - scaleFactor / 2f + symmetryOrigins[i].X),
                                        (int)(loc.Y - scaleFactor / 2f + symmetryOrigins[i].Y),
                                        scaleFactor,
                                        scaleFactor));
                                }

                                g.DrawImage(
                                    bmpBrushRot,
                                    loc.X - scaleFactor / 2f + symmetryOrigins[i].X,
                                    loc.Y - scaleFactor / 2f + symmetryOrigins[i].Y,
                                    scaleFactor,
                                    scaleFactor);
                            }
                        }
                    }

                    //Draws the brush with radial reflections.
                    else if (cmbxSymmetry.SelectedIndex != (int)SymmetryMode.None)
                    {
                        // Easier not to compute on a rotated canvas.
                        g.ResetTransform();

                        //Gets the center of the image.
                        PointF origin = new PointF(
                            symmetryOrigin.X,
                            symmetryOrigin.Y);

                        //Gets the drawn location relative to center.
                        PointF rotatedLoc = TransformPoint(loc, true, true, false);
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

                        if (useLockbitsDrawing)
                        {
                            using (Bitmap bmpBrushRotScaled = Utils.ScaleImage(
                                bmpBrushRot, new Size(scaleFactor, scaleFactor), false, false, null,
                                (CmbxSmoothing.Smoothing)cmbxBrushSmoothing.SelectedIndex))
                            {
                                for (int i = 0; i < numPoints; i++)
                                {
                                    if (activeTool == Tool.Eraser)
                                    {
                                        Utils.OverwriteMasked(
                                            this.EnvironmentParameters.SourceSurface,
                                            bmpCommitted,
                                            bmpBrushRotScaled,
                                            new Point(
                                                (int)Math.Round(origin.X - (scaleFactor / 2f) + (float)(dist * Math.Cos(angle))),
                                                (int)Math.Round(origin.Y - (scaleFactor / 2f) + (float)(dist * Math.Sin(angle)))),
                                            (chkbxLockR.Checked, chkbxLockG.Checked, chkbxLockB.Checked, 
                                            chkbxLockHue.Checked, chkbxLockSat.Checked, chkbxLockVal.Checked),
                                            chkbxSeamlessDrawing.Checked,
                                            chkbxDitherDraw.Checked);
                                    }
                                    else
                                    {
                                        Utils.DrawMasked(
                                            bmpToDrawOn,
                                            bmpBrushRotScaled,
                                            new Point(
                                                (int)Math.Round(origin.X - (scaleFactor / 2f) + (float)(dist * Math.Cos(angle))),
                                                (int)Math.Round(origin.Y - (scaleFactor / 2f) + (float)(dist * Math.Sin(angle)))),
                                            (adjustedColor, newFlowLoss, finalOpacity),
                                            chkbxColorizeBrush.Checked ? null : sliderColorInfluence.Value == 0 ? (100, false, false, false) : (
                                                sliderColorInfluence.Value, chkbxColorInfluenceHue.Checked,
                                                chkbxColorInfluenceSat.Checked, chkbxColorInfluenceVal.Checked),
                                            (BlendMode)cmbxBlendMode.SelectedIndex,
                                            drawToStagedBitmap ? (false, false, false, false, false, false, false) :
                                            (chkbxLockAlpha.Checked,
                                            chkbxLockR.Checked, chkbxLockG.Checked, chkbxLockB.Checked, 
                                            chkbxLockHue.Checked, chkbxLockSat.Checked, chkbxLockVal.Checked),
                                            chkbxSeamlessDrawing.Checked,
                                            chkbxDitherDraw.Checked,
                                            mergeRegions);
                                    }

                                    angle += angleIncrease;
                                }
                            }
                        }
                        else
                        {
                            for (int i = 0; i < numPoints; i++)
                            {
                                if (drawToStagedBitmap)
                                {
                                    mergeRegions.Add(new Rectangle(
                                        (int)(origin.X - (scaleFactor / 2f) + (float)(dist * Math.Cos(angle))),
                                        (int)(origin.Y - (scaleFactor / 2f) + (float)(dist * Math.Sin(angle))),
                                        scaleFactor,
                                        scaleFactor));
                                }

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
                }
                #endregion

                #region Draw the brush without blend modes but with a recolor matrix, considering symmetry
                else
                {
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
                        if (drawToStagedBitmap)
                        {
                            mergeRegions.Add(new Rectangle((int)xPos, (int)yPos, scaleFactor, scaleFactor));
                        }

                        g.DrawImage(bmpBrushRot, destination, bmpBrushRot.GetBounds(), GraphicsUnit.Pixel, recolorMatrix);
                    }

                    //Handles drawing reflections.
                    if (cmbxSymmetry.SelectedIndex !=
                        (int)SymmetryMode.None)
                    {
                        //Draws the brush horizontally reflected.
                        if (cmbxSymmetry.SelectedIndex == (int)SymmetryMode.Horizontal ||
                            cmbxSymmetry.SelectedIndex == (int)SymmetryMode.Vertical ||
                            cmbxSymmetry.SelectedIndex == (int)SymmetryMode.Star2)
                        {
                            bool symmetryX = cmbxSymmetry.SelectedIndex == (int)SymmetryMode.Vertical;
                            bool symmetryY = cmbxSymmetry.SelectedIndex == (int)SymmetryMode.Horizontal;

                            // Easier not to compute on a rotated canvas.
                            g.ResetTransform();

                            //Gets the symmetry origin.
                            PointF origin = new PointF(
                                symmetryOrigin.X,
                                symmetryOrigin.Y);

                            //Gets the drawn location relative to symmetry origin.
                            PointF rotatedLoc = TransformPoint(loc, true, true, false);
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

                            float halfScaleFactor = (scaleFactor / 2f);
                            float xDist = (float)(dist * Math.Cos(angle));
                            float yDist = (float)(dist * Math.Sin(angle));
                            float posX = origin.X - halfScaleFactor + (symmetryX ? xDist : -xDist);
                            float posY = origin.Y - halfScaleFactor + (symmetryY ? yDist : -yDist);

                            destination[0] = new PointF(posX, posY);
                            destination[1] = new PointF(posX + scaleFactor, posY);
                            destination[2] = new PointF(posX, posY + scaleFactor);

                            if (drawToStagedBitmap)
                            {
                                mergeRegions.Add(new Rectangle((int)posX, (int)posY, scaleFactor, scaleFactor));
                            }

                            g.DrawImage(bmpBrushRot, destination, bmpBrushRot.GetBounds(), GraphicsUnit.Pixel, recolorMatrix);
                        }

                        // Draws at defined offset locations.
                        else if (cmbxSymmetry.SelectedIndex ==
                            (int)SymmetryMode.SetPoints)
                        {
                            for (int i = 0; i < symmetryOrigins.Count; i++)
                            {
                                float newXPos = xPos + symmetryOrigins[i].X;
                                float newYPos = yPos + symmetryOrigins[i].Y;
                                destination[0] = new PointF(newXPos, newYPos);
                                destination[1] = new PointF(newXPos + scaleFactor, newYPos);
                                destination[2] = new PointF(newXPos, newYPos + scaleFactor);

                                if (drawToStagedBitmap)
                                {
                                    mergeRegions.Add(new Rectangle((int)newXPos, (int)newYPos, scaleFactor, scaleFactor));
                                }

                                g.DrawImage(bmpBrushRot, destination, bmpBrushRot.GetBounds(), GraphicsUnit.Pixel, recolorMatrix);
                            }
                        }

                        //Draws the brush with radial reflections.
                        else
                        {
                            // Easier not to compute on a rotated canvas.
                            g.ResetTransform();

                            //Gets the center of the image.
                            PointF origin = new PointF(
                                symmetryOrigin.X,
                                symmetryOrigin.Y);

                            //Gets the drawn location relative to center.
                            PointF rotatedLoc = TransformPoint(loc, true, true, false);
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

                            for (int i = 0; i < numPoints; i++)
                            {
                                float posX = (float)(origin.X - halfScaleFactor + dist * Math.Cos(angle));
                                float posY = (float)(origin.Y - halfScaleFactor + dist * Math.Sin(angle));

                                destination[0] = new PointF(posX, posY);
                                destination[1] = new PointF(posX + scaleFactor, posY);
                                destination[2] = new PointF(posX, posY + scaleFactor);

                                if (drawToStagedBitmap)
                                {
                                    mergeRegions.Add(new Rectangle((int)posX, (int)posY, scaleFactor, scaleFactor));
                                }

                                g.DrawImage(bmpBrushRot, destination, bmpBrushRot.GetBounds(), GraphicsUnit.Pixel, recolorMatrix);
                                angle += angleIncrease;
                            }
                        }
                    }
                }
                #endregion

                if (bmpBrushRot != bmpBrushEffects)
                {
                    bmpBrushRot?.Dispose();
                }
            }

            recolorMatrix?.Dispose();
        }

        /// <summary>
        /// Sets the active color based on the color from the canvas at the given point.
        /// </summary>
        /// <param name="loc">The point to get the color from.</param>
        private void GetColorFromCanvas(Point loc)
        {
            PointF rotatedLoc = TransformPoint(
                new PointF(loc.X - halfPixelOffset, loc.Y - halfPixelOffset),
                true, true, false);

            if (rotatedLoc.X >= 0 && rotatedLoc.Y >= 0 &&
                rotatedLoc.X <= bmpCommitted.Width - 1 &&
                rotatedLoc.Y <= bmpCommitted.Height - 1)
            {
                UpdateBrushColor(bmpCommitted.GetPixel(
                    (int)Math.Round(rotatedLoc.X),
                    (int)Math.Round(rotatedLoc.Y)));
            }
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
                case ShortcutTarget.Flow:
                    sliderBrushFlow.Value =
                        shortcut.GetDataAsInt(sliderBrushFlow.Value,
                        sliderBrushFlow.Minimum,
                        sliderBrushFlow.Maximum);
                    break;
                case ShortcutTarget.FlowShift:
                    sliderBrushFlow.Value =
                        shortcut.GetDataAsInt(sliderBrushFlow.Value,
                        sliderBrushFlow.Minimum,
                        sliderBrushFlow.Maximum);
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
                case ShortcutTarget.ColorInfluence:
                    sliderColorInfluence.Value = shortcut.GetDataAsInt(sliderColorInfluence.Value,
                        sliderColorInfluence.Minimum, sliderColorInfluence.Maximum);
                    UpdateBrushImage();
                    break;
                case ShortcutTarget.ColorInfluenceHue:
                    chkbxColorInfluenceHue.Checked = shortcut.GetDataAsBool(chkbxColorInfluenceHue.Checked);
                    break;
                case ShortcutTarget.ColorInfluenceSat:
                    chkbxColorInfluenceSat.Checked = shortcut.GetDataAsBool(chkbxColorInfluenceSat.Checked);
                    break;
                case ShortcutTarget.ColorInfluenceVal:
                    chkbxColorInfluenceVal.Checked = shortcut.GetDataAsBool(chkbxColorInfluenceVal.Checked);
                    break;
                case ShortcutTarget.DitherDraw:
                    chkbxDitherDraw.Checked = shortcut.GetDataAsBool(chkbxDitherDraw.Checked);
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
                case ShortcutTarget.JitterFlowLoss:
                    sliderRandFlowLoss.Value =
                        shortcut.GetDataAsInt(sliderRandFlowLoss.Value,
                        sliderRandFlowLoss.Minimum, sliderRandFlowLoss.Maximum);
                    break;
                case ShortcutTarget.JitterMaxSize:
                    sliderRandMaxSize.Value =
                        shortcut.GetDataAsInt(sliderRandMaxSize.Value,
                        sliderRandMaxSize.Minimum, sliderRandMaxSize.Maximum);
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
                case ShortcutTarget.DoLockAlpha:
                    chkbxLockAlpha.Checked = shortcut.GetDataAsBool(chkbxLockAlpha.Checked);
                    break;
                case ShortcutTarget.DoLockR:
                    chkbxLockR.Checked = shortcut.GetDataAsBool(chkbxLockR.Checked);
                    break;
                case ShortcutTarget.DoLockG:
                    chkbxLockG.Checked = shortcut.GetDataAsBool(chkbxLockG.Checked);
                    break;
                case ShortcutTarget.DoLockB:
                    chkbxLockB.Checked = shortcut.GetDataAsBool(chkbxLockB.Checked);
                    break;
                case ShortcutTarget.DoLockHue:
                    chkbxLockHue.Checked = shortcut.GetDataAsBool(chkbxLockHue.Checked);
                    break;
                case ShortcutTarget.DoLockSat:
                    chkbxLockSat.Checked = shortcut.GetDataAsBool(chkbxLockSat.Checked);
                    break;
                case ShortcutTarget.DoLockVal:
                    chkbxLockVal.Checked = shortcut.GetDataAsBool(chkbxLockVal.Checked);
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
                        if (shortcut.ActionData.Equals(loadedBrushImages[i].Location, StringComparison.CurrentCultureIgnoreCase))
                        {
                            selectedBrushIndex = i;
                            break;
                        }
                    }

                    if (selectedBrushIndex == -1)
                    {
                        for (int i = 0; i < loadedBrushImages.Count; i++)
                        {
                            if (shortcut.ActionData.Equals(loadedBrushImages[i].Name, StringComparison.CurrentCultureIgnoreCase))
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

                    if (newTool == Tool.PreviousTool) { newTool = lastTool; }

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
                case ShortcutTarget.ResetCanvasTransforms:
                    canvas.width = bmpCommitted.Width;
                    canvas.height = bmpCommitted.Height;
                    canvas.x = (displayCanvas.Width - canvas.width) / 2;
                    canvas.y = (displayCanvas.Height - canvas.height) / 2;
                    sliderCanvasAngle.Value = 0;
                    sliderCanvasZoom.Value = 100;
                    break;
                case ShortcutTarget.CanvasX:
                    canvas.x -= (int)((shortcut.GetDataAsInt(canvas.x, int.MinValue, int.MaxValue) - canvas.x) * canvasZoom);
                    displayCanvas.Refresh();
                    break;
                case ShortcutTarget.CanvasY:
                    canvas.y -= (int)((shortcut.GetDataAsInt(canvas.y, int.MinValue, int.MaxValue) - canvas.y) * canvasZoom);
                    displayCanvas.Refresh();
                    break;
                case ShortcutTarget.CanvasRotation:
                    var newValue = shortcut.GetDataAsInt(sliderCanvasAngle.Value, int.MinValue, int.MaxValue);
                    while (newValue < 0) { newValue += 360; }
                    while (newValue >= 360) { newValue -= 360; }
                    sliderCanvasAngle.Value = newValue;
                    break;
                case ShortcutTarget.BlendMode:
                    cmbxBlendMode.SelectedIndex =
                        shortcut.GetDataAsInt(cmbxBlendMode.SelectedIndex, 0, cmbxBlendMode.Items.Count);
                    break;
                case ShortcutTarget.SeamlessDrawing:
                    chkbxSeamlessDrawing.Checked = shortcut.GetDataAsBool(chkbxSeamlessDrawing.Checked);
                    break;
                case ShortcutTarget.BrushOpacity:
                    sliderBrushOpacity.Value =
                        shortcut.GetDataAsInt(sliderBrushOpacity.Value,
                        sliderBrushOpacity.Minimum,
                        sliderBrushOpacity.Maximum);
                    break;
                case ShortcutTarget.ChosenEffect:
                    cmbxChosenEffect.SelectedIndex =
                        shortcut.GetDataAsInt(cmbxChosenEffect.SelectedIndex, 0, cmbxChosenEffect.Items.Count);
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
        private void ImportBrushImages()
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
                ImportBrushImagesFromFiles(openFileDialog.FileNames, true);

                // Permanently adds brushes.
                if (settings != null)
                {
                    foreach (string filename in openFileDialog.FileNames)
                    {
                        if (!settings.CustomBrushImageDirectories.Contains(filename))
                        {
                            settings.CustomBrushImageDirectories.Add(filename);
                        }
                    }

                    settings.Save(true);
                }
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
        /// <param name="displayError">
        /// Errors should only be displayed if it's a user-initiated action.
        /// </param>
        private void ImportBrushImagesFromFiles(IReadOnlyCollection<string> filePaths, bool doDisplayErrors)
        {
            if (!brushImageLoadingWorker.IsBusy)
            {
                int listViewItemHeight = GetListViewItemHeight();
                int maxBrushSize = sliderBrushSize.Maximum;

                BrushImageLoadingSettings workerArgs = new BrushImageLoadingSettings(filePaths, true, doDisplayErrors, listViewItemHeight, maxBrushSize);
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

            bmpBrush?.Dispose();
            bmpBrushDownsized?.Dispose();
            bmpBrushDownsized = null;
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
                loadedBrushImages.Add(new BrushSelectorItem(Strings.DefaultBrushSquare, Resources.BrSquare));
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
            ImportBrushImagesFromDirectories(settings?.CustomBrushImageDirectories ?? new HashSet<string>());
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
        private static IReadOnlyCollection<string> FilesInDirectory(IEnumerable<string> localUris, BackgroundWorker backgroundWorker)
        {
            List<string> pathsToReturn = new List<string>();

            foreach (string pathFromUser in localUris)
            {
                try
                {
                    // Gets and verifies the file uri is valid.
                    string localUri = Path.GetFullPath(new Uri(pathFromUser).LocalPath)
                       ?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                       ?.ToLower();

                    bool isDirectory = Directory.Exists(localUri);
                    bool isFile = File.Exists(localUri);

                    if (isFile)
                    {
                        if (localUri.EndsWith("png") || localUri.EndsWith("bmp") || localUri.EndsWith("jpg") ||
                            localUri.EndsWith("gif") || localUri.EndsWith("tif") || localUri.EndsWith("exif") ||
                            localUri.EndsWith("jpeg") || localUri.EndsWith("tiff") || localUri.EndsWith(".abr"))
                        {
                            if (!pathsToReturn.Contains(localUri))
                            {
                                pathsToReturn.Add(localUri);
                            }
                        }
                    }
                    else if (isDirectory)
                    {
                        foreach (string str in Directory.GetFiles(localUri))
                        {
                            if (backgroundWorker.CancellationPending)
                            {
                                throw new OperationCanceledException();
                            }

                            if (str.EndsWith("png") || str.EndsWith("bmp") || str.EndsWith("jpg") ||
                                str.EndsWith("gif") || str.EndsWith("tif") || str.EndsWith("exif") ||
                                str.EndsWith("jpeg") || str.EndsWith("tiff") || str.EndsWith(".abr"))
                            {
                                if (!pathsToReturn.Contains(str))
                                {
                                    pathsToReturn.Add(str);
                                }
                            }
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
            this.components = new Container();
            ComponentResourceManager resources = new ComponentResourceManager(typeof(WinDynamicDraw));
            this.timerRepositionUpdate = new Timer(this.components);
            this.txtTooltip = new Label();
            this.displayCanvas = new PictureBox();
            this.bttnToolBrush = new Button();
            this.dummyImageList = new ImageList(this.components);
            this.panelUndoRedoOkCancel = new FlowLayoutPanel();
            this.bttnUndo = new Button();
            this.bttnRedo = new Button();
            this.bttnOk = new Button();
            this.bttnCancel = new Button();
            this.brushImageLoadingWorker = new BackgroundWorker();
            this.bttnColorPicker = new Button();
            this.panelAllSettingsContainer = new Panel();
            this.panelDockSettingsContainer = new Panel();
            this.flowLayoutPanel1 = new FlowLayoutPanel();
            this.BttnToolEraser = new Button();
            this.bttnToolOrigin = new Button();
            this.panelSettingsContainer = new FlowLayoutPanel();
            this.bttnBrushControls = new Accordion();
            this.panelBrush = new FlowLayoutPanel();
            this.txtCanvasZoom = new Label();
            this.sliderCanvasZoom = new TrackBar();
            this.txtCanvasAngle = new Label();
            this.sliderCanvasAngle = new TrackBar();
            this.listviewBrushPicker = new ListView();
            this.listviewBrushImagePicker = new DoubleBufferedListView();
            this.panelBrushAddPickColor = new Panel();
            this.chkbxColorizeBrush = new CheckBox();
            this.txtColorInfluence = new Label();
            this.sliderColorInfluence = new TrackBar();
            this.panelColorInfluenceHSV = new FlowLayoutPanel();
            this.chkbxColorInfluenceHue = new CheckBox();
            this.chkbxColorInfluenceSat = new CheckBox();
            this.chkbxColorInfluenceVal = new CheckBox();
            this.bttnAddBrushImages = new Button();
            this.brushImageLoadProgressBar = new ProgressBar();
            this.bttnBrushColor = new Button();
            this.cmbxBlendMode = new ComboBox();
            this.txtBrushOpacity = new Label();
            this.sliderBrushOpacity = new TrackBar();
            this.txtBrushFlow = new Label();
            this.sliderBrushFlow = new TrackBar();
            this.txtBrushRotation = new Label();
            this.sliderBrushRotation = new TrackBar();
            this.txtBrushSize = new Label();
            this.sliderBrushSize = new TrackBar();
            this.bttnSpecialSettings = new Accordion();
            this.panelSpecialSettings = new FlowLayoutPanel();
            this.panelChosenEffect = new Panel();
            this.cmbxChosenEffect = new ComboBox();
            this.bttnChooseEffectSettings = new Button();
            this.txtMinDrawDistance = new Label();
            this.sliderMinDrawDistance = new TrackBar();
            this.txtBrushDensity = new Label();
            this.sliderBrushDensity = new TrackBar();
            this.cmbxSymmetry = new ComboBox();
            this.cmbxBrushSmoothing = new ComboBox();
            this.chkbxSeamlessDrawing = new CheckBox();
            this.chkbxOrientToMouse = new CheckBox();
            this.chkbxDitherDraw = new CheckBox();
            this.chkbxLockAlpha = new CheckBox();
            this.panelRGBLocks = new Panel();
            this.chkbxLockR = new CheckBox();
            this.chkbxLockG = new CheckBox();
            this.chkbxLockB = new CheckBox();
            this.panelHSVLocks = new Panel();
            this.chkbxLockHue = new CheckBox();
            this.chkbxLockSat = new CheckBox();
            this.chkbxLockVal = new CheckBox();
            this.bttnJitterBasicsControls = new Accordion();
            this.panelJitterBasics = new FlowLayoutPanel();
            this.txtRandMinSize = new Label();
            this.sliderRandMinSize = new TrackBar();
            this.txtRandMaxSize = new Label();
            this.sliderRandMaxSize = new TrackBar();
            this.txtRandRotLeft = new Label();
            this.sliderRandRotLeft = new TrackBar();
            this.txtRandRotRight = new Label();
            this.sliderRandRotRight = new TrackBar();
            this.txtRandFlowLoss = new Label();
            this.sliderRandFlowLoss = new TrackBar();
            this.txtRandHorzShift = new Label();
            this.sliderRandHorzShift = new TrackBar();
            this.txtRandVertShift = new Label();
            this.sliderRandVertShift = new TrackBar();
            this.bttnJitterColorControls = new Accordion();
            this.panelJitterColor = new FlowLayoutPanel();
            this.txtJitterRed = new Label();
            this.sliderJitterMinRed = new TrackBar();
            this.sliderJitterMaxRed = new TrackBar();
            this.txtJitterGreen = new Label();
            this.sliderJitterMinGreen = new TrackBar();
            this.sliderJitterMaxGreen = new TrackBar();
            this.txtJitterBlue = new Label();
            this.sliderJitterMinBlue = new TrackBar();
            this.sliderJitterMaxBlue = new TrackBar();
            this.txtJitterHue = new Label();
            this.sliderJitterMinHue = new TrackBar();
            this.sliderJitterMaxHue = new TrackBar();
            this.txtJitterSaturation = new Label();
            this.sliderJitterMinSat = new TrackBar();
            this.sliderJitterMaxSat = new TrackBar();
            this.txtJitterValue = new Label();
            this.sliderJitterMinVal = new TrackBar();
            this.sliderJitterMaxVal = new TrackBar();
            this.bttnShiftBasicsControls = new Accordion();
            this.panelShiftBasics = new FlowLayoutPanel();
            this.txtShiftSize = new Label();
            this.sliderShiftSize = new TrackBar();
            this.txtShiftRotation = new Label();
            this.sliderShiftRotation = new TrackBar();
            this.txtShiftFlow = new Label();
            this.sliderShiftFlow = new TrackBar();
            this.bttnTabAssignPressureControls = new Accordion();
            this.panelTabletAssignPressure = new FlowLayoutPanel();
            this.panelTabPressureBrushOpacity = new FlowLayoutPanel();
            this.panel19 = new Panel();
            this.txtTabPressureBrushOpacity = new Label();
            this.spinTabPressureBrushOpacity = new NumericUpDown();
            this.cmbxTabPressureBrushOpacity = new CmbxTabletValueType();
            this.panelTabPressureBrushFlow = new FlowLayoutPanel();
            this.panel3 = new Panel();
            this.txtTabPressureBrushFlow = new Label();
            this.spinTabPressureBrushFlow = new NumericUpDown();
            this.cmbxTabPressureBrushFlow = new CmbxTabletValueType();
            this.panelTabPressureBrushSize = new FlowLayoutPanel();
            this.panel8 = new Panel();
            this.txtTabPressureBrushSize = new Label();
            this.spinTabPressureBrushSize = new NumericUpDown();
            this.cmbxTabPressureBrushSize = new CmbxTabletValueType();
            this.panelTabPressureBrushRotation = new FlowLayoutPanel();
            this.panel2 = new Panel();
            this.txtTabPressureBrushRotation = new Label();
            this.spinTabPressureBrushRotation = new NumericUpDown();
            this.cmbxTabPressureBrushRotation = new CmbxTabletValueType();
            this.panelTabPressureMinDrawDistance = new FlowLayoutPanel();
            this.panel1 = new Panel();
            this.lblTabPressureMinDrawDistance = new Label();
            this.spinTabPressureMinDrawDistance = new NumericUpDown();
            this.cmbxTabPressureMinDrawDistance = new CmbxTabletValueType();
            this.panelTabPressureBrushDensity = new FlowLayoutPanel();
            this.panel4 = new Panel();
            this.lblTabPressureBrushDensity = new Label();
            this.spinTabPressureBrushDensity = new NumericUpDown();
            this.cmbxTabPressureBrushDensity = new CmbxTabletValueType();
            this.panelTabPressureRandMinSize = new FlowLayoutPanel();
            this.panel5 = new Panel();
            this.lblTabPressureRandMinSize = new Label();
            this.spinTabPressureRandMinSize = new NumericUpDown();
            this.cmbxTabPressureRandMinSize = new CmbxTabletValueType();
            this.panelTabPressureRandMaxSize = new FlowLayoutPanel();
            this.panel6 = new Panel();
            this.lblTabPressureRandMaxSize = new Label();
            this.spinTabPressureRandMaxSize = new NumericUpDown();
            this.cmbxTabPressureRandMaxSize = new CmbxTabletValueType();
            this.panelTabPressureRandRotLeft = new FlowLayoutPanel();
            this.panel7 = new Panel();
            this.lblTabPressureRandRotLeft = new Label();
            this.spinTabPressureRandRotLeft = new NumericUpDown();
            this.cmbxTabPressureRandRotLeft = new CmbxTabletValueType();
            this.panelTabPressureRandRotRight = new FlowLayoutPanel();
            this.panel9 = new Panel();
            this.lblTabPressureRandRotRight = new Label();
            this.spinTabPressureRandRotRight = new NumericUpDown();
            this.cmbxTabPressureRandRotRight = new CmbxTabletValueType();
            this.panelTabPressureRandFlowLoss = new FlowLayoutPanel();
            this.panel10 = new Panel();
            this.lblTabPressureRandFlowLoss = new Label();
            this.spinTabPressureRandFlowLoss = new NumericUpDown();
            this.cmbxTabPressureRandFlowLoss = new CmbxTabletValueType();
            this.panelTabPressureRandHorShift = new FlowLayoutPanel();
            this.panel12 = new Panel();
            this.lblTabPressureRandHorShift = new Label();
            this.spinTabPressureRandHorShift = new NumericUpDown();
            this.cmbxTabPressureRandHorShift = new CmbxTabletValueType();
            this.panelTabPressureRandVerShift = new FlowLayoutPanel();
            this.panel11 = new Panel();
            this.lblTabPressureRandVerShift = new Label();
            this.spinTabPressureRandVerShift = new NumericUpDown();
            this.cmbxTabPressureRandVerShift = new CmbxTabletValueType();
            this.panelTabPressureRedJitter = new FlowLayoutPanel();
            this.panel13 = new Panel();
            this.spinTabPressureMinRedJitter = new NumericUpDown();
            this.lblTabPressureRedJitter = new Label();
            this.spinTabPressureMaxRedJitter = new NumericUpDown();
            this.cmbxTabPressureRedJitter = new CmbxTabletValueType();
            this.panelTabPressureGreenJitter = new FlowLayoutPanel();
            this.panel14 = new Panel();
            this.spinTabPressureMinGreenJitter = new NumericUpDown();
            this.lblTabPressureGreenJitter = new Label();
            this.spinTabPressureMaxGreenJitter = new NumericUpDown();
            this.cmbxTabPressureGreenJitter = new CmbxTabletValueType();
            this.panelTabPressureBlueJitter = new FlowLayoutPanel();
            this.panel15 = new Panel();
            this.spinTabPressureMinBlueJitter = new NumericUpDown();
            this.lblTabPressureBlueJitter = new Label();
            this.spinTabPressureMaxBlueJitter = new NumericUpDown();
            this.cmbxTabPressureBlueJitter = new CmbxTabletValueType();
            this.panelTabPressureHueJitter = new FlowLayoutPanel();
            this.panel16 = new Panel();
            this.spinTabPressureMinHueJitter = new NumericUpDown();
            this.lblTabPressureHueJitter = new Label();
            this.spinTabPressureMaxHueJitter = new NumericUpDown();
            this.cmbxTabPressureHueJitter = new CmbxTabletValueType();
            this.panelTabPressureSatJitter = new FlowLayoutPanel();
            this.panel17 = new Panel();
            this.spinTabPressureMinSatJitter = new NumericUpDown();
            this.lblTabPressureSatJitter = new Label();
            this.spinTabPressureMaxSatJitter = new NumericUpDown();
            this.cmbxTabPressureSatJitter = new CmbxTabletValueType();
            this.panelTabPressureValueJitter = new FlowLayoutPanel();
            this.panel18 = new Panel();
            this.spinTabPressureMinValueJitter = new NumericUpDown();
            this.lblTabPressureValueJitter = new Label();
            this.spinTabPressureMaxValueJitter = new NumericUpDown();
            this.cmbxTabPressureValueJitter = new CmbxTabletValueType();
            this.bttnSettings = new Accordion();
            this.panelSettings = new FlowLayoutPanel();
            this.bttnCustomBrushImageLocations = new Button();
            this.bttnClearSettings = new Button();
            this.bttnDeleteBrush = new Button();
            this.bttnSaveBrush = new Button();
            this.chkbxAutomaticBrushDensity = new CheckBox();
            this.displayCanvas.SuspendLayout();
            ((ISupportInitialize)(this.displayCanvas)).BeginInit();
            this.panelUndoRedoOkCancel.SuspendLayout();
            this.panelAllSettingsContainer.SuspendLayout();
            this.panelDockSettingsContainer.SuspendLayout();
            this.flowLayoutPanel1.SuspendLayout();
            this.panelSettingsContainer.SuspendLayout();
            this.panelBrush.SuspendLayout();
            ((ISupportInitialize)(this.sliderCanvasZoom)).BeginInit();
            ((ISupportInitialize)(this.sliderCanvasAngle)).BeginInit();
            this.panelBrushAddPickColor.SuspendLayout();
            ((ISupportInitialize)(this.sliderColorInfluence)).BeginInit();
            this.panelColorInfluenceHSV.SuspendLayout();
            ((ISupportInitialize)(this.sliderBrushOpacity)).BeginInit();
            ((ISupportInitialize)(this.sliderBrushFlow)).BeginInit();
            ((ISupportInitialize)(this.sliderBrushRotation)).BeginInit();
            ((ISupportInitialize)(this.sliderBrushSize)).BeginInit();
            this.panelSpecialSettings.SuspendLayout();
            this.panelChosenEffect.SuspendLayout();
            this.panelRGBLocks.SuspendLayout();
            this.panelHSVLocks.SuspendLayout();
            ((ISupportInitialize)(this.sliderMinDrawDistance)).BeginInit();
            ((ISupportInitialize)(this.sliderBrushDensity)).BeginInit();
            this.panelJitterBasics.SuspendLayout();
            ((ISupportInitialize)(this.sliderRandMinSize)).BeginInit();
            ((ISupportInitialize)(this.sliderRandMaxSize)).BeginInit();
            ((ISupportInitialize)(this.sliderRandRotLeft)).BeginInit();
            ((ISupportInitialize)(this.sliderRandRotRight)).BeginInit();
            ((ISupportInitialize)(this.sliderRandFlowLoss)).BeginInit();
            ((ISupportInitialize)(this.sliderRandHorzShift)).BeginInit();
            ((ISupportInitialize)(this.sliderRandVertShift)).BeginInit();
            this.panelJitterColor.SuspendLayout();
            ((ISupportInitialize)(this.sliderJitterMinRed)).BeginInit();
            ((ISupportInitialize)(this.sliderJitterMaxRed)).BeginInit();
            ((ISupportInitialize)(this.sliderJitterMinGreen)).BeginInit();
            ((ISupportInitialize)(this.sliderJitterMaxGreen)).BeginInit();
            ((ISupportInitialize)(this.sliderJitterMinBlue)).BeginInit();
            ((ISupportInitialize)(this.sliderJitterMaxBlue)).BeginInit();
            ((ISupportInitialize)(this.sliderJitterMinHue)).BeginInit();
            ((ISupportInitialize)(this.sliderJitterMaxHue)).BeginInit();
            ((ISupportInitialize)(this.sliderJitterMinSat)).BeginInit();
            ((ISupportInitialize)(this.sliderJitterMaxSat)).BeginInit();
            ((ISupportInitialize)(this.sliderJitterMinVal)).BeginInit();
            ((ISupportInitialize)(this.sliderJitterMaxVal)).BeginInit();
            this.panelShiftBasics.SuspendLayout();
            ((ISupportInitialize)(this.sliderShiftSize)).BeginInit();
            ((ISupportInitialize)(this.sliderShiftRotation)).BeginInit();
            ((ISupportInitialize)(this.sliderShiftFlow)).BeginInit();
            this.panelTabletAssignPressure.SuspendLayout();
            this.panelTabPressureBrushOpacity.SuspendLayout();
            this.panel19.SuspendLayout();
            ((ISupportInitialize)(this.spinTabPressureBrushOpacity)).BeginInit();
            this.panelTabPressureBrushFlow.SuspendLayout();
            this.panel3.SuspendLayout();
            ((ISupportInitialize)(this.spinTabPressureBrushFlow)).BeginInit();
            this.panelTabPressureBrushSize.SuspendLayout();
            this.panel8.SuspendLayout();
            ((ISupportInitialize)(this.spinTabPressureBrushSize)).BeginInit();
            this.panelTabPressureBrushRotation.SuspendLayout();
            this.panel2.SuspendLayout();
            ((ISupportInitialize)(this.spinTabPressureBrushRotation)).BeginInit();
            this.panelTabPressureMinDrawDistance.SuspendLayout();
            this.panel1.SuspendLayout();
            ((ISupportInitialize)(this.spinTabPressureMinDrawDistance)).BeginInit();
            this.panelTabPressureBrushDensity.SuspendLayout();
            this.panel4.SuspendLayout();
            ((ISupportInitialize)(this.spinTabPressureBrushDensity)).BeginInit();
            this.panelTabPressureRandMinSize.SuspendLayout();
            this.panel5.SuspendLayout();
            ((ISupportInitialize)(this.spinTabPressureRandMinSize)).BeginInit();
            this.panelTabPressureRandMaxSize.SuspendLayout();
            this.panel6.SuspendLayout();
            ((ISupportInitialize)(this.spinTabPressureRandMaxSize)).BeginInit();
            this.panelTabPressureRandRotLeft.SuspendLayout();
            this.panel7.SuspendLayout();
            ((ISupportInitialize)(this.spinTabPressureRandRotLeft)).BeginInit();
            this.panelTabPressureRandRotRight.SuspendLayout();
            this.panel9.SuspendLayout();
            ((ISupportInitialize)(this.spinTabPressureRandRotRight)).BeginInit();
            this.panelTabPressureRandFlowLoss.SuspendLayout();
            this.panel10.SuspendLayout();
            ((ISupportInitialize)(this.spinTabPressureRandFlowLoss)).BeginInit();
            this.panelTabPressureRandHorShift.SuspendLayout();
            this.panel12.SuspendLayout();
            ((ISupportInitialize)(this.spinTabPressureRandHorShift)).BeginInit();
            this.panelTabPressureRandVerShift.SuspendLayout();
            this.panel11.SuspendLayout();
            ((ISupportInitialize)(this.spinTabPressureRandVerShift)).BeginInit();
            this.panelTabPressureRedJitter.SuspendLayout();
            this.panel13.SuspendLayout();
            ((ISupportInitialize)(this.spinTabPressureMinRedJitter)).BeginInit();
            ((ISupportInitialize)(this.spinTabPressureMaxRedJitter)).BeginInit();
            this.panelTabPressureGreenJitter.SuspendLayout();
            this.panel14.SuspendLayout();
            ((ISupportInitialize)(this.spinTabPressureMinGreenJitter)).BeginInit();
            ((ISupportInitialize)(this.spinTabPressureMaxGreenJitter)).BeginInit();
            this.panelTabPressureBlueJitter.SuspendLayout();
            this.panel15.SuspendLayout();
            ((ISupportInitialize)(this.spinTabPressureMinBlueJitter)).BeginInit();
            ((ISupportInitialize)(this.spinTabPressureMaxBlueJitter)).BeginInit();
            this.panelTabPressureHueJitter.SuspendLayout();
            this.panel16.SuspendLayout();
            ((ISupportInitialize)(this.spinTabPressureMinHueJitter)).BeginInit();
            ((ISupportInitialize)(this.spinTabPressureMaxHueJitter)).BeginInit();
            this.panelTabPressureSatJitter.SuspendLayout();
            this.panel17.SuspendLayout();
            ((ISupportInitialize)(this.spinTabPressureMinSatJitter)).BeginInit();
            ((ISupportInitialize)(this.spinTabPressureMaxSatJitter)).BeginInit();
            this.panelTabPressureValueJitter.SuspendLayout();
            this.panel18.SuspendLayout();
            ((ISupportInitialize)(this.spinTabPressureMinValueJitter)).BeginInit();
            ((ISupportInitialize)(this.spinTabPressureMaxValueJitter)).BeginInit();
            this.panelSettings.SuspendLayout();
            this.SuspendLayout();
            // 
            // timerRepositionUpdate
            // 
            this.timerRepositionUpdate.Interval = 5;
            this.timerRepositionUpdate.Tick += new EventHandler(this.RepositionUpdate_Tick);
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
            this.displayCanvas.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(207)))), ((int)(((byte)(207)))), ((int)(((byte)(207)))));
            resources.ApplyResources(this.displayCanvas, "displayCanvas");
            this.displayCanvas.Controls.Add(this.txtTooltip);
            this.displayCanvas.Name = "displayCanvas";
            this.displayCanvas.TabStop = false;
            this.displayCanvas.Paint += new PaintEventHandler(this.DisplayCanvas_Paint);
            this.displayCanvas.MouseDown += new MouseEventHandler(this.DisplayCanvas_MouseDown);
            this.displayCanvas.MouseEnter += new EventHandler(this.DisplayCanvas_MouseEnter);
            this.displayCanvas.MouseMove += new MouseEventHandler(this.DisplayCanvas_MouseMove);
            this.displayCanvas.MouseUp += new MouseEventHandler(this.DisplayCanvas_MouseUp);
            // 
            // bttnToolBrush
            // 
            this.bttnToolBrush.BackColor = System.Drawing.SystemColors.ButtonShadow;
            this.bttnToolBrush.Image = global::DynamicDraw.Properties.Resources.ToolBrush;
            resources.ApplyResources(this.bttnToolBrush, "bttnToolBrush");
            this.bttnToolBrush.Name = "bttnToolBrush";
            this.bttnToolBrush.UseVisualStyleBackColor = false;
            this.bttnToolBrush.Click += new EventHandler(this.BttnToolBrush_Click);
            this.bttnToolBrush.MouseEnter += new EventHandler(this.BttnToolBrush_MouseEnter);
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
            this.bttnUndo.Click += new EventHandler(this.BttnUndo_Click);
            this.bttnUndo.MouseEnter += new EventHandler(this.BttnUndo_MouseEnter);
            // 
            // bttnRedo
            // 
            resources.ApplyResources(this.bttnRedo, "bttnRedo");
            this.bttnRedo.Name = "bttnRedo";
            this.bttnRedo.UseVisualStyleBackColor = true;
            this.bttnRedo.Click += new EventHandler(this.BttnRedo_Click);
            this.bttnRedo.MouseEnter += new EventHandler(this.BttnRedo_MouseEnter);
            // 
            // bttnOk
            // 
            this.bttnOk.BackColor = System.Drawing.Color.Honeydew;
            resources.ApplyResources(this.bttnOk, "bttnOk");
            this.bttnOk.Name = "bttnOk";
            this.bttnOk.UseVisualStyleBackColor = false;
            this.bttnOk.Click += new EventHandler(this.BttnOk_Click);
            this.bttnOk.MouseEnter += new EventHandler(this.BttnOk_MouseEnter);
            // 
            // bttnCancel
            // 
            resources.ApplyResources(this.bttnCancel, "bttnCancel");
            this.bttnCancel.BackColor = System.Drawing.Color.MistyRose;
            this.bttnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.bttnCancel.Name = "bttnCancel";
            this.bttnCancel.UseVisualStyleBackColor = false;
            this.bttnCancel.Click += new EventHandler(this.BttnCancel_Click);
            this.bttnCancel.MouseEnter += new EventHandler(this.BttnCancel_MouseEnter);
            // 
            // brushImageLoadingWorker
            // 
            this.brushImageLoadingWorker.WorkerReportsProgress = true;
            this.brushImageLoadingWorker.WorkerSupportsCancellation = true;
            this.brushImageLoadingWorker.DoWork += new DoWorkEventHandler(this.BrushImageLoadingWorker_DoWork);
            this.brushImageLoadingWorker.ProgressChanged += new ProgressChangedEventHandler(this.BrushImageLoadingWorker_ProgressChanged);
            this.brushImageLoadingWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(this.BrushImageLoadingWorker_RunWorkerCompleted);
            // 
            // bttnColorPicker
            // 
            this.bttnColorPicker.Image = global::DynamicDraw.Properties.Resources.ColorPickerIcon;
            resources.ApplyResources(this.bttnColorPicker, "bttnColorPicker");
            this.bttnColorPicker.Name = "bttnColorPicker";
            this.bttnColorPicker.UseVisualStyleBackColor = true;
            this.bttnColorPicker.Click += new EventHandler(this.BttnToolColorPicker_Click);
            this.bttnColorPicker.MouseEnter += new EventHandler(this.BttnToolColorPicker_MouseEnter);
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
            this.BttnToolEraser.Image = global::DynamicDraw.Properties.Resources.ToolEraser;
            resources.ApplyResources(this.BttnToolEraser, "BttnToolEraser");
            this.BttnToolEraser.Name = "BttnToolEraser";
            this.BttnToolEraser.UseVisualStyleBackColor = true;
            this.BttnToolEraser.Click += new EventHandler(this.BttnToolEraser_Click);
            this.BttnToolEraser.MouseEnter += new EventHandler(this.BttnToolEraser_MouseEnter);
            // 
            // bttnToolOrigin
            // 
            this.bttnToolOrigin.Image = global::DynamicDraw.Properties.Resources.ToolOrigin;
            resources.ApplyResources(this.bttnToolOrigin, "bttnToolOrigin");
            this.bttnToolOrigin.Name = "bttnToolOrigin";
            this.bttnToolOrigin.UseVisualStyleBackColor = true;
            this.bttnToolOrigin.Click += new EventHandler(this.BttnToolOrigin_Click);
            this.bttnToolOrigin.MouseEnter += new EventHandler(this.BttnToolOrigin_MouseEnter);
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
            this.panelBrush.Controls.Add(this.txtCanvasAngle);
            this.panelBrush.Controls.Add(this.sliderCanvasAngle);
            this.panelBrush.Controls.Add(this.listviewBrushPicker);
            this.panelBrush.Controls.Add(this.listviewBrushImagePicker);
            this.panelBrush.Controls.Add(this.panelBrushAddPickColor);
            this.panelBrush.Controls.Add(this.txtColorInfluence);
            this.panelBrush.Controls.Add(this.sliderColorInfluence);
            this.panelBrush.Controls.Add(this.panelColorInfluenceHSV);
            this.panelBrush.Controls.Add(this.cmbxBlendMode);
            this.panelBrush.Controls.Add(this.txtBrushOpacity);
            this.panelBrush.Controls.Add(this.sliderBrushOpacity);
            this.panelBrush.Controls.Add(this.txtBrushFlow);
            this.panelBrush.Controls.Add(this.sliderBrushFlow);
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
            this.sliderCanvasZoom.Maximum = 6400;
            this.sliderCanvasZoom.Minimum = 1;
            this.sliderCanvasZoom.Name = "sliderCanvasZoom";
            this.sliderCanvasZoom.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderCanvasZoom.Value = 100;
            this.sliderCanvasZoom.ValueChanged += new EventHandler(this.SliderCanvasZoom_ValueChanged);
            this.sliderCanvasZoom.MouseEnter += new EventHandler(this.SliderCanvasZoom_MouseEnter);
            // 
            // txtCanvasAngle
            // 
            resources.ApplyResources(this.txtCanvasAngle, "txtCanvasAngle");
            this.txtCanvasAngle.BackColor = System.Drawing.Color.Transparent;
            this.txtCanvasAngle.Name = "txtCanvasAngle";
            // 
            // sliderCanvasAngle
            // 
            resources.ApplyResources(this.sliderCanvasAngle, "sliderCanvasAngle");
            this.sliderCanvasAngle.LargeChange = 1;
            this.sliderCanvasAngle.Maximum = 360;
            this.sliderCanvasAngle.Minimum = 0;
            this.sliderCanvasAngle.Name = "sliderCanvasAngle";
            this.sliderCanvasAngle.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderCanvasAngle.Value = 0;
            this.sliderCanvasAngle.ValueChanged += new EventHandler(this.SliderCanvasAngle_ValueChanged);
            this.sliderCanvasAngle.MouseEnter += new EventHandler(this.SliderCanvasAngle_MouseEnter);
            // 
            // listviewBrushPicker
            // 
            this.listviewBrushPicker.HideSelection = false;
            this.listviewBrushPicker.Columns.Add("_"); // name hidden and unimportant
            this.listviewBrushPicker.HeaderStyle = ColumnHeaderStyle.None;
            resources.ApplyResources(this.listviewBrushPicker, "listviewBrushPicker");
            this.listviewBrushPicker.Name = "listviewBrushPicker";
            this.listviewBrushPicker.UseCompatibleStateImageBehavior = false;
            this.listviewBrushPicker.View = System.Windows.Forms.View.Details;
            this.listviewBrushPicker.SelectedIndexChanged += new EventHandler(this.ListViewBrushPicker_SelectedIndexChanged);
            this.listviewBrushPicker.MouseEnter += new EventHandler(this.ListviewBrushPicker_MouseEnter);
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
            this.listviewBrushImagePicker.CacheVirtualItems += new CacheVirtualItemsEventHandler(this.ListViewBrushImagePicker_CacheVirtualItems);
            this.listviewBrushImagePicker.DrawColumnHeader += new DrawListViewColumnHeaderEventHandler(this.ListViewBrushImagePicker_DrawColumnHeader);
            this.listviewBrushImagePicker.DrawItem += new DrawListViewItemEventHandler(this.ListViewBrushImagePicker_DrawItem);
            this.listviewBrushImagePicker.DrawSubItem += new DrawListViewSubItemEventHandler(this.ListViewBrushImagePicker_DrawSubItem);
            this.listviewBrushImagePicker.RetrieveVirtualItem += new RetrieveVirtualItemEventHandler(this.ListViewBrushImagePicker_RetrieveVirtualItem);
            this.listviewBrushImagePicker.SelectedIndexChanged += new EventHandler(this.ListViewBrushImagePicker_SelectedIndexChanged);
            this.listviewBrushImagePicker.MouseEnter += new EventHandler(this.ListViewBrushImagePicker_MouseEnter);
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
            // bttnChooseEffectSettings
            // 
            this.bttnChooseEffectSettings.Text = "⚙";
            resources.ApplyResources(this.bttnChooseEffectSettings, "bttnChooseEffectSettings");
            this.bttnChooseEffectSettings.Name = "bttnChooseEffectSettings";
            this.bttnChooseEffectSettings.UseVisualStyleBackColor = true;
            this.bttnChooseEffectSettings.Click += new EventHandler(this.BttnChooseEffectSettings_Click);
            this.bttnChooseEffectSettings.MouseEnter += new EventHandler(this.BttnChooseEffectSettings_MouseEnter);
            // 
            // chkbxColorizeBrush
            // 
            resources.ApplyResources(this.chkbxColorizeBrush, "chkbxColorizeBrush");
            this.chkbxColorizeBrush.Checked = true;
            this.chkbxColorizeBrush.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkbxColorizeBrush.Name = "chkbxColorizeBrush";
            this.chkbxColorizeBrush.UseVisualStyleBackColor = true;
            this.chkbxColorizeBrush.CheckedChanged += new EventHandler(this.ChkbxColorizeBrush_CheckedChanged);
            this.chkbxColorizeBrush.MouseEnter += new EventHandler(this.ChkbxColorizeBrush_MouseEnter);
            // 
            // bttnAddBrushImages
            // 
            this.bttnAddBrushImages.Image = global::DynamicDraw.Properties.Resources.AddBrushIcon;
            resources.ApplyResources(this.bttnAddBrushImages, "bttnAddBrushImages");
            this.bttnAddBrushImages.Name = "bttnAddBrushImages";
            this.bttnAddBrushImages.UseVisualStyleBackColor = true;
            this.bttnAddBrushImages.Click += new EventHandler(this.BttnAddBrushImages_Click);
            this.bttnAddBrushImages.MouseEnter += new EventHandler(this.BttnAddBrushImages_MouseEnter);
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
            this.bttnBrushColor.Click += new EventHandler(this.BttnBrushColor_Click);
            this.bttnBrushColor.MouseEnter += new EventHandler(this.BttnBrushColor_MouseEnter);
            // 
            // txtColorInfluence
            // 
            resources.ApplyResources(this.txtColorInfluence, "txtColorInfluence");
            this.txtColorInfluence.BackColor = System.Drawing.Color.Transparent;
            this.txtColorInfluence.Name = "txtColorInfluence";
            // 
            // sliderColorInfluence
            // 
            resources.ApplyResources(this.sliderColorInfluence, "sliderColorInfluence");
            this.sliderColorInfluence.LargeChange = 1;
            this.sliderColorInfluence.Maximum = 100;
            this.sliderColorInfluence.Minimum = 0;
            this.sliderColorInfluence.Name = "sliderColorInfluence";
            this.sliderColorInfluence.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderColorInfluence.ValueChanged += new EventHandler(this.SliderColorInfluence_ValueChanged);
            this.sliderColorInfluence.MouseEnter += new EventHandler(this.SliderColorInfluence_MouseEnter);
            // 
            // panelColorInfluenceHSV
            // 
            resources.ApplyResources(this.panelColorInfluenceHSV, "panelColorInfluenceHSV");
            this.panelColorInfluenceHSV.BackColor = System.Drawing.Color.Transparent;
            this.panelColorInfluenceHSV.Controls.Add(this.chkbxColorInfluenceHue);
            this.panelColorInfluenceHSV.Controls.Add(this.chkbxColorInfluenceSat);
            this.panelColorInfluenceHSV.Controls.Add(this.chkbxColorInfluenceVal);
            this.panelColorInfluenceHSV.Name = "panelColorInfluenceHSV";
            // 
            // chkbxColorInfluenceHue
            // 
            resources.ApplyResources(this.chkbxColorInfluenceHue, "chkbxColorInfluenceHue");
            this.chkbxColorInfluenceHue.Checked = true;
            this.chkbxColorInfluenceHue.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkbxColorInfluenceHue.Name = "chkbxColorInfluenceHue";
            this.chkbxColorInfluenceHue.UseVisualStyleBackColor = true;
            this.chkbxColorInfluenceHue.MouseEnter += new EventHandler(this.ChkbxColorInfluenceHue_MouseEnter);
            // 
            // chkbxColorInfluenceSat
            // 
            resources.ApplyResources(this.chkbxColorInfluenceSat, "chkbxColorInfluenceSat");
            this.chkbxColorInfluenceSat.Checked = true;
            this.chkbxColorInfluenceSat.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkbxColorInfluenceSat.Name = "chkbxColorInfluenceSat";
            this.chkbxColorInfluenceSat.UseVisualStyleBackColor = true;
            this.chkbxColorInfluenceSat.MouseEnter += new EventHandler(this.ChkbxColorInfluenceSat_MouseEnter);
            // 
            // chkbxColorInfluenceVal
            // 
            resources.ApplyResources(this.chkbxColorInfluenceVal, "chkbxColorInfluenceVal");
            this.chkbxColorInfluenceVal.Name = "chkbxColorInfluenceVal";
            this.chkbxColorInfluenceVal.UseVisualStyleBackColor = true;
            this.chkbxColorInfluenceVal.MouseEnter += new EventHandler(this.ChkbxColorInfluenceVal_MouseEnter);
            // 
            // cmbxBlendMode
            // 
            resources.ApplyResources(this.cmbxBlendMode, "cmbxBlendMode");
            this.cmbxBlendMode.BackColor = System.Drawing.Color.White;
            this.cmbxBlendMode.DropDownHeight = 140;
            this.cmbxBlendMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbxBlendMode.DropDownWidth = 20;
            this.cmbxBlendMode.FormattingEnabled = true;
            this.cmbxBlendMode.Name = "cmbxBlendMode";
            this.cmbxBlendMode.SelectedIndexChanged += new EventHandler(this.BttnBlendMode_SelectedIndexChanged);
            this.cmbxBlendMode.MouseEnter += new EventHandler(this.BttnBlendMode_MouseEnter);
            // 
            // txtBrushOpacity
            // 
            resources.ApplyResources(this.txtBrushOpacity, "txtBrushOpacity");
            this.txtBrushOpacity.BackColor = System.Drawing.Color.Transparent;
            this.txtBrushOpacity.Name = "txtBrushOpacity";
            // 
            // sliderBrushOpacity
            // 
            resources.ApplyResources(this.sliderBrushOpacity, "sliderBrushOpacity");
            this.sliderBrushOpacity.LargeChange = 1;
            this.sliderBrushOpacity.Maximum = 255;
            this.sliderBrushOpacity.Name = "sliderBrushOpacity";
            this.sliderBrushOpacity.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderBrushOpacity.Value = 255;
            this.sliderBrushOpacity.ValueChanged += new EventHandler(this.SliderBrushOpacity_ValueChanged);
            this.sliderBrushOpacity.MouseEnter += new EventHandler(this.SliderBrushOpacity_MouseEnter);
            // 
            // txtBrushFlow
            // 
            resources.ApplyResources(this.txtBrushFlow, "txtBrushFlow");
            this.txtBrushFlow.BackColor = System.Drawing.Color.Transparent;
            this.txtBrushFlow.Name = "txtBrushFlow";
            // 
            // sliderBrushFlow
            // 
            resources.ApplyResources(this.sliderBrushFlow, "sliderBrushFlow");
            this.sliderBrushFlow.LargeChange = 1;
            this.sliderBrushFlow.Maximum = 255;
            this.sliderBrushFlow.Name = "sliderBrushFlow";
            this.sliderBrushFlow.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderBrushFlow.Value = 255;
            this.sliderBrushFlow.ValueChanged += new EventHandler(this.SliderBrushFlow_ValueChanged);
            this.sliderBrushFlow.MouseEnter += new EventHandler(this.SliderBrushFlow_MouseEnter);
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
            this.sliderBrushRotation.ValueChanged += new EventHandler(this.SliderBrushRotation_ValueChanged);
            this.sliderBrushRotation.MouseEnter += new EventHandler(this.SliderBrushRotation_MouseEnter);
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
            this.sliderBrushSize.ValueChanged += new EventHandler(this.SliderBrushSize_ValueChanged);
            this.sliderBrushSize.MouseEnter += new EventHandler(this.SliderBrushSize_MouseEnter);
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
            this.panelSpecialSettings.Controls.Add(this.panelChosenEffect);
            this.panelSpecialSettings.Controls.Add(this.txtMinDrawDistance);
            this.panelSpecialSettings.Controls.Add(this.sliderMinDrawDistance);
            this.panelSpecialSettings.Controls.Add(this.chkbxAutomaticBrushDensity);
            this.panelSpecialSettings.Controls.Add(this.txtBrushDensity);
            this.panelSpecialSettings.Controls.Add(this.sliderBrushDensity);
            this.panelSpecialSettings.Controls.Add(this.cmbxBrushSmoothing);
            this.panelSpecialSettings.Controls.Add(this.cmbxSymmetry);
            this.panelSpecialSettings.Controls.Add(this.chkbxSeamlessDrawing);
            this.panelSpecialSettings.Controls.Add(this.chkbxOrientToMouse);
            this.panelSpecialSettings.Controls.Add(this.chkbxDitherDraw);
            this.panelSpecialSettings.Controls.Add(this.chkbxLockAlpha);
            this.panelSpecialSettings.Controls.Add(this.panelRGBLocks);
            this.panelSpecialSettings.Controls.Add(this.panelHSVLocks);
            this.panelSpecialSettings.Name = "panelSpecialSettings";
            // 
            // panelChosenEffect
            // 
            this.panelChosenEffect.Controls.Add(this.cmbxChosenEffect);
            this.panelChosenEffect.Controls.Add(this.bttnChooseEffectSettings);
            resources.ApplyResources(this.panelChosenEffect, "panelChosenEffect");
            this.panelChosenEffect.Name = "panelChosenEffect";
            // 
            // cmbxChosenEffect
            // 
            resources.ApplyResources(this.cmbxChosenEffect, "cmbxChosenEffect");
            this.cmbxChosenEffect.BackColor = System.Drawing.Color.White;
            this.cmbxChosenEffect.DropDownHeight = 140;
            this.cmbxChosenEffect.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbxChosenEffect.DropDownWidth = 20;
            this.cmbxChosenEffect.FormattingEnabled = true;
            this.cmbxChosenEffect.Name = "cmbxChosenEffect";
            this.cmbxChosenEffect.MouseEnter += new EventHandler(this.CmbxChosenEffect_MouseEnter);
            this.cmbxChosenEffect.SelectedIndexChanged += new EventHandler(this.CmbxChosenEffect_SelectedIndexChanged);
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
            this.sliderMinDrawDistance.Maximum = 500;
            this.sliderMinDrawDistance.Name = "sliderMinDrawDistance";
            this.sliderMinDrawDistance.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderMinDrawDistance.ValueChanged += new EventHandler(this.SliderMinDrawDistance_ValueChanged);
            this.sliderMinDrawDistance.MouseEnter += new EventHandler(this.SliderMinDrawDistance_MouseEnter);
            // 
            // chkbxAutomaticBrushDensity
            // 
            resources.ApplyResources(this.chkbxAutomaticBrushDensity, "chkbxAutomaticBrushDensity");
            this.chkbxAutomaticBrushDensity.Text = "Manage density automatically";
            this.chkbxAutomaticBrushDensity.Checked = true;
            this.chkbxAutomaticBrushDensity.Name = "chkbxAutomaticBrushDensity";
            this.chkbxAutomaticBrushDensity.MouseEnter += new EventHandler(this.AutomaticBrushDensity_MouseEnter);
            this.chkbxAutomaticBrushDensity.CheckedChanged += new EventHandler(this.AutomaticBrushDensity_CheckedChanged);
            this.chkbxAutomaticBrushDensity.TabIndex = 20;
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
            this.sliderBrushDensity.ValueChanged += new EventHandler(this.SliderBrushDensity_ValueChanged);
            this.sliderBrushDensity.MouseEnter += new EventHandler(this.SliderBrushDensity_MouseEnter);
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
            this.cmbxSymmetry.MouseEnter += new EventHandler(this.BttnSymmetry_MouseEnter);
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
            this.cmbxBrushSmoothing.MouseEnter += new EventHandler(this.BttnBrushSmoothing_MouseEnter);
            // 
            // chkbxSeamlessDrawing
            // 
            resources.ApplyResources(this.chkbxSeamlessDrawing, "chkbxSeamlessDrawing");
            this.chkbxSeamlessDrawing.Name = "chkbxSeamlessDrawing";
            this.chkbxSeamlessDrawing.UseVisualStyleBackColor = true;
            this.chkbxSeamlessDrawing.MouseEnter += new EventHandler(this.ChkbxSeamlessDrawing_MouseEnter);
            // 
            // chkbxOrientToMouse
            // 
            resources.ApplyResources(this.chkbxOrientToMouse, "chkbxOrientToMouse");
            this.chkbxOrientToMouse.Name = "chkbxOrientToMouse";
            this.chkbxOrientToMouse.UseVisualStyleBackColor = true;
            this.chkbxOrientToMouse.MouseEnter += new EventHandler(this.ChkbxOrientToMouse_MouseEnter);
            // 
            // chkbxDitherDraw
            // 
            resources.ApplyResources(this.chkbxDitherDraw, "chkbxDitherDraw");
            this.chkbxDitherDraw.Name = "chkbxDitherDraw";
            this.chkbxDitherDraw.UseVisualStyleBackColor = true;
            this.chkbxDitherDraw.MouseEnter += new EventHandler(this.ChkbxDitherDraw_MouseEnter);
            // 
            // panelRGBLocks
            // 
            this.panelRGBLocks.Controls.Add(this.chkbxLockR);
            this.panelRGBLocks.Controls.Add(this.chkbxLockG);
            this.panelRGBLocks.Controls.Add(this.chkbxLockB);
            resources.ApplyResources(this.panelRGBLocks, "panelRGBLocks");
            this.panelRGBLocks.Name = "panelRGBLocks";
            // 
            // panelHSVLocks
            // 
            this.panelHSVLocks.Controls.Add(this.chkbxLockHue);
            this.panelHSVLocks.Controls.Add(this.chkbxLockSat);
            this.panelHSVLocks.Controls.Add(this.chkbxLockVal);
            resources.ApplyResources(this.panelHSVLocks, "panelHSVLocks");
            this.panelHSVLocks.Name = "panelHSVLocks";
            // 
            // chkbxLockAlpha
            // 
            resources.ApplyResources(this.chkbxLockAlpha, "chkbxLockAlpha");
            this.chkbxLockAlpha.Name = "chkbxLockAlpha";
            this.chkbxLockAlpha.UseVisualStyleBackColor = true;
            this.chkbxLockAlpha.MouseEnter += new EventHandler(this.ChkbxLockAlpha_MouseEnter);
            // 
            // chkbxLockR
            // 
            resources.ApplyResources(this.chkbxLockR, "chkbxLockR");
            this.chkbxLockR.Name = "chkbxLockR";
            this.chkbxLockR.UseVisualStyleBackColor = true;
            this.chkbxLockR.MouseEnter += new EventHandler(this.ChkbxLockR_MouseEnter);
            // 
            // chkbxLockG
            // 
            resources.ApplyResources(this.chkbxLockG, "chkbxLockG");
            this.chkbxLockG.Name = "chkbxLockG";
            this.chkbxLockG.UseVisualStyleBackColor = true;
            this.chkbxLockG.MouseEnter += new EventHandler(this.ChkbxLockG_MouseEnter);
            // 
            // chkbxLockB
            // 
            resources.ApplyResources(this.chkbxLockB, "chkbxLockB");
            this.chkbxLockB.Name = "chkbxLockB";
            this.chkbxLockB.UseVisualStyleBackColor = true;
            this.chkbxLockB.MouseEnter += new EventHandler(this.ChkbxLockB_MouseEnter);
            // 
            // chkbxLockHue
            // 
            resources.ApplyResources(this.chkbxLockHue, "chkbxLockHue");
            this.chkbxLockHue.Name = "chkbxLockHue";
            this.chkbxLockHue.UseVisualStyleBackColor = true;
            this.chkbxLockHue.MouseEnter += new EventHandler(this.ChkbxLockHue_MouseEnter);
            // 
            // chkbxLockSat
            // 
            resources.ApplyResources(this.chkbxLockSat, "chkbxLockSat");
            this.chkbxLockSat.Name = "chkbxLockSat";
            this.chkbxLockSat.UseVisualStyleBackColor = true;
            this.chkbxLockSat.MouseEnter += new EventHandler(this.ChkbxLockSat_MouseEnter);
            // 
            // chkbxLockVal
            // 
            resources.ApplyResources(this.chkbxLockVal, "chkbxLockVal");
            this.chkbxLockVal.Name = "chkbxLockVal";
            this.chkbxLockVal.UseVisualStyleBackColor = true;
            this.chkbxLockVal.MouseEnter += new EventHandler(this.ChkbxLockVal_MouseEnter);
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
            this.panelJitterBasics.Controls.Add(this.txtRandFlowLoss);
            this.panelJitterBasics.Controls.Add(this.sliderRandFlowLoss);
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
            this.sliderRandMinSize.ValueChanged += new EventHandler(this.SliderRandMinSize_ValueChanged);
            this.sliderRandMinSize.MouseEnter += new EventHandler(this.SliderRandMinSize_MouseEnter);
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
            this.sliderRandMaxSize.ValueChanged += new EventHandler(this.SliderRandMaxSize_ValueChanged);
            this.sliderRandMaxSize.MouseEnter += new EventHandler(this.SliderRandMaxSize_MouseEnter);
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
            this.sliderRandRotLeft.ValueChanged += new EventHandler(this.SliderRandRotLeft_ValueChanged);
            this.sliderRandRotLeft.MouseEnter += new EventHandler(this.SliderRandRotLeft_MouseEnter);
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
            this.sliderRandRotRight.ValueChanged += new EventHandler(this.SliderRandRotRight_ValueChanged);
            this.sliderRandRotRight.MouseEnter += new EventHandler(this.SliderRandRotRight_MouseEnter);
            // 
            // txtRandFlowLoss
            // 
            resources.ApplyResources(this.txtRandFlowLoss, "txtRandFlowLoss");
            this.txtRandFlowLoss.BackColor = System.Drawing.Color.Transparent;
            this.txtRandFlowLoss.Name = "txtRandMinFlowLoss";
            // 
            // sliderRandFlowLoss
            // 
            resources.ApplyResources(this.sliderRandFlowLoss, "sliderRandFlowLoss");
            this.sliderRandFlowLoss.LargeChange = 1;
            this.sliderRandFlowLoss.Maximum = 255;
            this.sliderRandFlowLoss.Name = "sliderRandFlowLoss";
            this.sliderRandFlowLoss.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderRandFlowLoss.ValueChanged += new EventHandler(this.SliderRandFlowLoss_ValueChanged);
            this.sliderRandFlowLoss.MouseEnter += new EventHandler(this.SliderRandFlowLoss_MouseEnter);
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
            this.sliderRandHorzShift.ValueChanged += new EventHandler(this.SliderRandHorzShift_ValueChanged);
            this.sliderRandHorzShift.MouseEnter += new EventHandler(this.SliderRandHorzShift_MouseEnter);
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
            this.sliderRandVertShift.ValueChanged += new EventHandler(this.SliderRandVertShift_ValueChanged);
            this.sliderRandVertShift.MouseEnter += new EventHandler(this.SliderRandVertShift_MouseEnter);
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
            this.sliderJitterMinRed.ValueChanged += new EventHandler(this.SliderJitterMinRed_ValueChanged);
            this.sliderJitterMinRed.MouseEnter += new EventHandler(this.SliderJitterMinRed_MouseEnter);
            // 
            // sliderJitterMaxRed
            // 
            resources.ApplyResources(this.sliderJitterMaxRed, "sliderJitterMaxRed");
            this.sliderJitterMaxRed.LargeChange = 1;
            this.sliderJitterMaxRed.Maximum = 100;
            this.sliderJitterMaxRed.Name = "sliderJitterMaxRed";
            this.sliderJitterMaxRed.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderJitterMaxRed.ValueChanged += new EventHandler(this.SliderJitterMaxRed_ValueChanged);
            this.sliderJitterMaxRed.MouseEnter += new EventHandler(this.SliderJitterMaxRed_MouseEnter);
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
            this.sliderJitterMinGreen.ValueChanged += new EventHandler(this.SliderJitterMinGreen_ValueChanged);
            this.sliderJitterMinGreen.MouseEnter += new EventHandler(this.SliderJitterMinGreen_MouseEnter);
            // 
            // sliderJitterMaxGreen
            // 
            resources.ApplyResources(this.sliderJitterMaxGreen, "sliderJitterMaxGreen");
            this.sliderJitterMaxGreen.LargeChange = 1;
            this.sliderJitterMaxGreen.Maximum = 100;
            this.sliderJitterMaxGreen.Name = "sliderJitterMaxGreen";
            this.sliderJitterMaxGreen.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderJitterMaxGreen.ValueChanged += new EventHandler(this.SliderJitterMaxGreen_ValueChanged);
            this.sliderJitterMaxGreen.MouseEnter += new EventHandler(this.SliderJitterMaxGreen_MouseEnter);
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
            this.sliderJitterMinBlue.ValueChanged += new EventHandler(this.SliderJitterMinBlue_ValueChanged);
            this.sliderJitterMinBlue.MouseEnter += new EventHandler(this.SliderJitterMinBlue_MouseEnter);
            // 
            // sliderJitterMaxBlue
            // 
            resources.ApplyResources(this.sliderJitterMaxBlue, "sliderJitterMaxBlue");
            this.sliderJitterMaxBlue.LargeChange = 1;
            this.sliderJitterMaxBlue.Maximum = 100;
            this.sliderJitterMaxBlue.Name = "sliderJitterMaxBlue";
            this.sliderJitterMaxBlue.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderJitterMaxBlue.ValueChanged += new EventHandler(this.SliderJitterMaxBlue_ValueChanged);
            this.sliderJitterMaxBlue.MouseEnter += new EventHandler(this.SliderJitterMaxBlue_MouseEnter);
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
            this.sliderJitterMinHue.ValueChanged += new EventHandler(this.SliderJitterMinHue_ValueChanged);
            this.sliderJitterMinHue.MouseEnter += new EventHandler(this.SliderJitterMinHue_MouseEnter);
            // 
            // sliderJitterMaxHue
            // 
            resources.ApplyResources(this.sliderJitterMaxHue, "sliderJitterMaxHue");
            this.sliderJitterMaxHue.LargeChange = 1;
            this.sliderJitterMaxHue.Maximum = 100;
            this.sliderJitterMaxHue.Name = "sliderJitterMaxHue";
            this.sliderJitterMaxHue.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderJitterMaxHue.ValueChanged += new EventHandler(this.SliderJitterMaxHue_ValueChanged);
            this.sliderJitterMaxHue.MouseEnter += new EventHandler(this.SliderJitterMaxHue_MouseEnter);
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
            this.sliderJitterMinSat.ValueChanged += new EventHandler(this.SliderJitterMinSat_ValueChanged);
            this.sliderJitterMinSat.MouseEnter += new EventHandler(this.SliderJitterMinSat_MouseEnter);
            // 
            // sliderJitterMaxSat
            // 
            resources.ApplyResources(this.sliderJitterMaxSat, "sliderJitterMaxSat");
            this.sliderJitterMaxSat.LargeChange = 1;
            this.sliderJitterMaxSat.Maximum = 100;
            this.sliderJitterMaxSat.Name = "sliderJitterMaxSat";
            this.sliderJitterMaxSat.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderJitterMaxSat.ValueChanged += new EventHandler(this.SliderJitterMaxSat_ValueChanged);
            this.sliderJitterMaxSat.MouseEnter += new EventHandler(this.SliderJitterMaxSat_MouseEnter);
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
            this.sliderJitterMinVal.ValueChanged += new EventHandler(this.SliderJitterMinVal_ValueChanged);
            this.sliderJitterMinVal.MouseEnter += new EventHandler(this.SliderJitterMinVal_MouseEnter);
            // 
            // sliderJitterMaxVal
            // 
            resources.ApplyResources(this.sliderJitterMaxVal, "sliderJitterMaxVal");
            this.sliderJitterMaxVal.LargeChange = 1;
            this.sliderJitterMaxVal.Maximum = 100;
            this.sliderJitterMaxVal.Name = "sliderJitterMaxVal";
            this.sliderJitterMaxVal.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderJitterMaxVal.ValueChanged += new EventHandler(this.SliderJitterMaxVal_ValueChanged);
            this.sliderJitterMaxVal.MouseEnter += new EventHandler(this.SliderJitterMaxVal_MouseEnter);
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
            this.panelShiftBasics.Controls.Add(this.txtShiftFlow);
            this.panelShiftBasics.Controls.Add(this.sliderShiftFlow);
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
            this.sliderShiftSize.ValueChanged += new EventHandler(this.SliderShiftSize_ValueChanged);
            this.sliderShiftSize.MouseEnter += new EventHandler(this.SliderShiftSize_MouseEnter);
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
            this.sliderShiftRotation.ValueChanged += new EventHandler(this.SliderShiftRotation_ValueChanged);
            this.sliderShiftRotation.MouseEnter += new EventHandler(this.SliderShiftRotation_MouseEnter);
            // 
            // txtShiftFlow
            // 
            this.txtShiftFlow.BackColor = System.Drawing.Color.Transparent;
            resources.ApplyResources(this.txtShiftFlow, "txtShiftFlow");
            this.txtShiftFlow.Name = "txtShiftFlow";
            // 
            // sliderShiftFlow
            // 
            resources.ApplyResources(this.sliderShiftFlow, "sliderShiftFlow");
            this.sliderShiftFlow.LargeChange = 1;
            this.sliderShiftFlow.Maximum = 255;
            this.sliderShiftFlow.Minimum = -255;
            this.sliderShiftFlow.Name = "sliderShiftFlow";
            this.sliderShiftFlow.TickStyle = System.Windows.Forms.TickStyle.None;
            this.sliderShiftFlow.ValueChanged += new EventHandler(this.SliderShiftFlow_ValueChanged);
            this.sliderShiftFlow.MouseEnter += new EventHandler(this.SliderShiftFlow_MouseEnter);
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
            this.panelTabletAssignPressure.Controls.Add(this.panelTabPressureBrushOpacity);
            this.panelTabletAssignPressure.Controls.Add(this.panelTabPressureBrushFlow);
            this.panelTabletAssignPressure.Controls.Add(this.panelTabPressureBrushSize);
            this.panelTabletAssignPressure.Controls.Add(this.panelTabPressureBrushRotation);
            this.panelTabletAssignPressure.Controls.Add(this.panelTabPressureMinDrawDistance);
            this.panelTabletAssignPressure.Controls.Add(this.panelTabPressureBrushDensity);
            this.panelTabletAssignPressure.Controls.Add(this.panelTabPressureRandMinSize);
            this.panelTabletAssignPressure.Controls.Add(this.panelTabPressureRandMaxSize);
            this.panelTabletAssignPressure.Controls.Add(this.panelTabPressureRandRotLeft);
            this.panelTabletAssignPressure.Controls.Add(this.panelTabPressureRandRotRight);
            this.panelTabletAssignPressure.Controls.Add(this.panelTabPressureRandFlowLoss);
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
            // panelTabPressureBrushOpacity
            // 
            resources.ApplyResources(this.panelTabPressureBrushOpacity, "panelTabPressureBrushOpacity");
            this.panelTabPressureBrushOpacity.Controls.Add(this.panel19);
            this.panelTabPressureBrushOpacity.Controls.Add(this.cmbxTabPressureBrushOpacity);
            this.panelTabPressureBrushOpacity.Name = "panelTabPressureBrushOpacity";
            // 
            // panel19
            //
            this.panel19.Controls.Add(this.txtTabPressureBrushOpacity);
            this.panel19.Controls.Add(this.spinTabPressureBrushOpacity);
            resources.ApplyResources(this.panel19, "panel19");
            this.panel19.Name = "panel19";
            // 
            // txtTabPressureBrushOpacity
            // 
            resources.ApplyResources(this.txtTabPressureBrushOpacity, "txtTabPressureBrushOpacity");
            this.txtTabPressureBrushOpacity.Name = "txtTabPressureBrushOpacity";
            // 
            // spinTabPressureBrushOpacity
            // 
            resources.ApplyResources(this.spinTabPressureBrushOpacity, "spinTabPressureBrushOpacity");
            this.spinTabPressureBrushOpacity.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.spinTabPressureBrushOpacity.Minimum = new decimal(new int[] {
            255,
            0,
            0,
            -2147483648});
            this.spinTabPressureBrushOpacity.Name = "spinTabPressureBrushOpacity";
            // 
            // cmbxTabPressureBrushOpacity
            // 
            this.cmbxTabPressureBrushOpacity.BackColor = System.Drawing.Color.White;
            this.cmbxTabPressureBrushOpacity.DisplayMember = "DisplayMember";
            this.cmbxTabPressureBrushOpacity.DropDownHeight = 140;
            this.cmbxTabPressureBrushOpacity.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbxTabPressureBrushOpacity.DropDownWidth = 20;
            resources.ApplyResources(this.cmbxTabPressureBrushOpacity, "cmbxTabPressureBrushOpacity");
            this.cmbxTabPressureBrushOpacity.FormattingEnabled = true;
            this.cmbxTabPressureBrushOpacity.Name = "cmbxTabPressureBrushOpacity";
            this.cmbxTabPressureBrushOpacity.ValueMember = "ValueMember";
            this.cmbxTabPressureBrushOpacity.MouseHover += new EventHandler(this.CmbxTabPressure_MouseHover);
            //
            // panelTabPressureBrushFlow
            // 
            resources.ApplyResources(this.panelTabPressureBrushFlow, "panelTabPressureBrushFlow");
            this.panelTabPressureBrushFlow.Controls.Add(this.panel3);
            this.panelTabPressureBrushFlow.Controls.Add(this.cmbxTabPressureBrushFlow);
            this.panelTabPressureBrushFlow.Name = "panelTabPressureBrushFlow";
            // 
            // panel3
            // 
            this.panel3.Controls.Add(this.txtTabPressureBrushFlow);
            this.panel3.Controls.Add(this.spinTabPressureBrushFlow);
            resources.ApplyResources(this.panel3, "panel3");
            this.panel3.Name = "panel3";
            // 
            // txtTabPressureBrushFlow
            // 
            resources.ApplyResources(this.txtTabPressureBrushFlow, "txtTabPressureBrushFlow");
            this.txtTabPressureBrushFlow.Name = "txtTabPressureBrushFlow";
            // 
            // spinTabPressureBrushFlow
            // 
            resources.ApplyResources(this.spinTabPressureBrushFlow, "spinTabPressureBrushFlow");
            this.spinTabPressureBrushFlow.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            this.spinTabPressureBrushFlow.Minimum = new decimal(new int[] {
            255,
            0,
            0,
            -2147483648});
            this.spinTabPressureBrushFlow.Name = "spinTabPressureBrushFlow";
            // 
            // cmbxTabPressureBrushFlow
            // 
            this.cmbxTabPressureBrushFlow.BackColor = System.Drawing.Color.White;
            this.cmbxTabPressureBrushFlow.DisplayMember = "DisplayMember";
            this.cmbxTabPressureBrushFlow.DropDownHeight = 140;
            this.cmbxTabPressureBrushFlow.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbxTabPressureBrushFlow.DropDownWidth = 20;
            resources.ApplyResources(this.cmbxTabPressureBrushFlow, "cmbxTabPressureBrushFlow");
            this.cmbxTabPressureBrushFlow.FormattingEnabled = true;
            this.cmbxTabPressureBrushFlow.Name = "cmbxTabPressureBrushFlow";
            this.cmbxTabPressureBrushFlow.ValueMember = "ValueMember";
            this.cmbxTabPressureBrushFlow.MouseHover += new EventHandler(this.CmbxTabPressure_MouseHover);
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
            this.spinTabPressureBrushSize.LostFocus += SpinTabPressureBrushSize_LostFocus;
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
            this.cmbxTabPressureBrushSize.MouseHover += new EventHandler(this.CmbxTabPressure_MouseHover);
            this.cmbxTabPressureBrushSize.SelectedIndexChanged += CmbxTabPressureBrushSize_SelectedIndexChanged;
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
            this.cmbxTabPressureBrushRotation.MouseHover += new EventHandler(this.CmbxTabPressure_MouseHover);
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
            this.cmbxTabPressureMinDrawDistance.MouseHover += new EventHandler(this.CmbxTabPressure_MouseHover);
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
            this.cmbxTabPressureBrushDensity.MouseHover += new EventHandler(this.CmbxTabPressure_MouseHover);
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
            this.cmbxTabPressureRandMinSize.MouseHover += new EventHandler(this.CmbxTabPressure_MouseHover);
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
            this.cmbxTabPressureRandMaxSize.MouseHover += new EventHandler(this.CmbxTabPressure_MouseHover);
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
            this.cmbxTabPressureRandRotLeft.MouseHover += new EventHandler(this.CmbxTabPressure_MouseHover);
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
            this.cmbxTabPressureRandRotRight.MouseHover += new EventHandler(this.CmbxTabPressure_MouseHover);
            // 
            // panelTabPressureRandFlowLoss
            // 
            resources.ApplyResources(this.panelTabPressureRandFlowLoss, "panelTabPressureRandFlowLoss");
            this.panelTabPressureRandFlowLoss.Controls.Add(this.panel10);
            this.panelTabPressureRandFlowLoss.Controls.Add(this.cmbxTabPressureRandFlowLoss);
            this.panelTabPressureRandFlowLoss.Name = "panelTabPressureRandFlowLoss";
            // 
            // panel10
            // 
            this.panel10.Controls.Add(this.lblTabPressureRandFlowLoss);
            this.panel10.Controls.Add(this.spinTabPressureRandFlowLoss);
            resources.ApplyResources(this.panel10, "panel10");
            this.panel10.Name = "panel10";
            // 
            // lblTabPressureRandFlowLoss
            // 
            resources.ApplyResources(this.lblTabPressureRandFlowLoss, "lblTabPressureRandFlowLoss");
            this.lblTabPressureRandFlowLoss.Name = "lblTabPressureRandFlowLoss";
            // 
            // spinTabPressureRandFlowLoss
            // 
            resources.ApplyResources(this.spinTabPressureRandFlowLoss, "spinTabPressureRandFlowLoss");
            this.spinTabPressureRandFlowLoss.Minimum = new decimal(new int[] {
            255,
            0,
            0,
            -2147483648});
            this.spinTabPressureRandFlowLoss.Name = "spinTabPressureRandFlowLoss";
            // 
            // cmbxTabPressureRandFlowLoss
            // 
            this.cmbxTabPressureRandFlowLoss.BackColor = System.Drawing.Color.White;
            this.cmbxTabPressureRandFlowLoss.DisplayMember = "DisplayMember";
            this.cmbxTabPressureRandFlowLoss.DropDownHeight = 140;
            this.cmbxTabPressureRandFlowLoss.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbxTabPressureRandFlowLoss.DropDownWidth = 20;
            resources.ApplyResources(this.cmbxTabPressureRandFlowLoss, "cmbxTabPressureRandFlowLoss");
            this.cmbxTabPressureRandFlowLoss.FormattingEnabled = true;
            this.cmbxTabPressureRandFlowLoss.Name = "cmbxTabPressureRandFlowLoss";
            this.cmbxTabPressureRandFlowLoss.ValueMember = "ValueMember";
            this.cmbxTabPressureRandFlowLoss.MouseHover += new EventHandler(this.CmbxTabPressure_MouseHover);
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
            this.cmbxTabPressureRandHorShift.MouseHover += new EventHandler(this.CmbxTabPressure_MouseHover);
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
            this.cmbxTabPressureRandVerShift.MouseHover += new EventHandler(this.CmbxTabPressure_MouseHover);
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
            this.cmbxTabPressureRedJitter.MouseHover += new EventHandler(this.CmbxTabPressure_MouseHover);
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
            this.cmbxTabPressureGreenJitter.MouseHover += new EventHandler(this.CmbxTabPressure_MouseHover);
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
            this.cmbxTabPressureBlueJitter.MouseHover += new EventHandler(this.CmbxTabPressure_MouseHover);
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
            this.cmbxTabPressureHueJitter.MouseHover += new EventHandler(this.CmbxTabPressure_MouseHover);
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
            this.cmbxTabPressureSatJitter.MouseHover += new EventHandler(this.CmbxTabPressure_MouseHover);
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
            this.cmbxTabPressureValueJitter.MouseHover += new EventHandler(this.CmbxTabPressure_MouseHover);
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
            this.bttnCustomBrushImageLocations.Click += new EventHandler(this.BttnPreferences_Click);
            this.bttnCustomBrushImageLocations.MouseEnter += new EventHandler(this.BttnPreferences_MouseEnter);
            // 
            // bttnClearSettings
            // 
            resources.ApplyResources(this.bttnClearSettings, "bttnClearSettings");
            this.bttnClearSettings.Name = "bttnClearSettings";
            this.bttnClearSettings.UseVisualStyleBackColor = true;
            this.bttnClearSettings.Click += new EventHandler(this.BttnClearSettings_Click);
            this.bttnClearSettings.MouseEnter += new EventHandler(this.BttnClearSettings_MouseEnter);
            // 
            // bttnDeleteBrush
            // 
            resources.ApplyResources(this.bttnDeleteBrush, "bttnDeleteBrush");
            this.bttnDeleteBrush.Name = "bttnDeleteBrush";
            this.bttnDeleteBrush.UseVisualStyleBackColor = true;
            this.bttnDeleteBrush.Click += new EventHandler(this.BttnDeleteBrush_Click);
            this.bttnDeleteBrush.MouseEnter += new EventHandler(this.BttnDeleteBrush_MouseEnter);
            // 
            // bttnSaveBrush
            // 
            resources.ApplyResources(this.bttnSaveBrush, "bttnSaveBrush");
            this.bttnSaveBrush.Name = "bttnSaveBrush";
            this.bttnSaveBrush.UseVisualStyleBackColor = true;
            this.bttnSaveBrush.Click += new EventHandler(this.BttnSaveBrush_Click);
            this.bttnSaveBrush.MouseEnter += new EventHandler(this.BttnSaveBrush_MouseEnter);
            // 
            // WinDynamicDraw
            // 
            this.AcceptButton = this.bttnOk;
            resources.ApplyResources(this, "$this");
            this.BackColor = System.Drawing.SystemColors.ControlLight;
            this.CancelButton = this.bttnCancel;
            this.Controls.Add(this.panelAllSettingsContainer);
            this.Controls.Add(this.displayCanvas);
            this.DoubleBuffered = true;
            this.KeyPreview = true;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.Load += new System.EventHandler(this.DynamicDrawWindow_Load);
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.Name = "WinDynamicDraw";
            this.Resize += WinDynamicDraw_Resize;
            this.SizeGripStyle = SizeGripStyle.Auto;
            this.displayCanvas.ResumeLayout(false);
            this.displayCanvas.PerformLayout();
            ((ISupportInitialize)(this.displayCanvas)).EndInit();
            this.panelUndoRedoOkCancel.ResumeLayout(false);
            this.panelAllSettingsContainer.ResumeLayout(false);
            this.panelDockSettingsContainer.ResumeLayout(false);
            this.panelDockSettingsContainer.PerformLayout();
            this.flowLayoutPanel1.ResumeLayout(false);
            this.panelSettingsContainer.ResumeLayout(false);
            this.panelSettingsContainer.PerformLayout();
            this.panelBrush.ResumeLayout(false);
            this.panelBrush.PerformLayout();
            ((ISupportInitialize)(this.sliderCanvasZoom)).EndInit();
            ((ISupportInitialize)(this.sliderCanvasAngle)).EndInit();
            this.panelBrushAddPickColor.ResumeLayout(false);
            this.panelBrushAddPickColor.PerformLayout();
            ((ISupportInitialize)(this.sliderColorInfluence)).EndInit();
            this.panelColorInfluenceHSV.ResumeLayout(false);
            this.panelColorInfluenceHSV.PerformLayout();
            ((ISupportInitialize)(this.sliderBrushOpacity)).EndInit();
            ((ISupportInitialize)(this.sliderBrushFlow)).EndInit();
            ((ISupportInitialize)(this.sliderBrushRotation)).EndInit();
            ((ISupportInitialize)(this.sliderBrushSize)).EndInit();
            this.panelSpecialSettings.ResumeLayout(false);
            this.panelSpecialSettings.PerformLayout();
            this.panelChosenEffect.ResumeLayout(false);
            this.panelChosenEffect.PerformLayout();
            this.panelRGBLocks.ResumeLayout(false);
            this.panelRGBLocks.PerformLayout();
            this.panelHSVLocks.ResumeLayout(false);
            this.panelHSVLocks.PerformLayout();
            ((ISupportInitialize)(this.sliderMinDrawDistance)).EndInit();
            ((ISupportInitialize)(this.sliderBrushDensity)).EndInit();
            this.panelJitterBasics.ResumeLayout(false);
            ((ISupportInitialize)(this.sliderRandMinSize)).EndInit();
            ((ISupportInitialize)(this.sliderRandMaxSize)).EndInit();
            ((ISupportInitialize)(this.sliderRandRotLeft)).EndInit();
            ((ISupportInitialize)(this.sliderRandRotRight)).EndInit();
            ((ISupportInitialize)(this.sliderRandFlowLoss)).EndInit();
            ((ISupportInitialize)(this.sliderRandHorzShift)).EndInit();
            ((ISupportInitialize)(this.sliderRandVertShift)).EndInit();
            this.panelJitterColor.ResumeLayout(false);
            ((ISupportInitialize)(this.sliderJitterMinRed)).EndInit();
            ((ISupportInitialize)(this.sliderJitterMaxRed)).EndInit();
            ((ISupportInitialize)(this.sliderJitterMinGreen)).EndInit();
            ((ISupportInitialize)(this.sliderJitterMaxGreen)).EndInit();
            ((ISupportInitialize)(this.sliderJitterMinBlue)).EndInit();
            ((ISupportInitialize)(this.sliderJitterMaxBlue)).EndInit();
            ((ISupportInitialize)(this.sliderJitterMinHue)).EndInit();
            ((ISupportInitialize)(this.sliderJitterMaxHue)).EndInit();
            ((ISupportInitialize)(this.sliderJitterMinSat)).EndInit();
            ((ISupportInitialize)(this.sliderJitterMaxSat)).EndInit();
            ((ISupportInitialize)(this.sliderJitterMinVal)).EndInit();
            ((ISupportInitialize)(this.sliderJitterMaxVal)).EndInit();
            this.panelShiftBasics.ResumeLayout(false);
            ((ISupportInitialize)(this.sliderShiftSize)).EndInit();
            ((ISupportInitialize)(this.sliderShiftRotation)).EndInit();
            ((ISupportInitialize)(this.sliderShiftFlow)).EndInit();
            this.panelTabletAssignPressure.ResumeLayout(false);
            this.panelTabletAssignPressure.PerformLayout();
            this.panelTabPressureBrushOpacity.ResumeLayout(false);
            this.panel19.ResumeLayout(false);
            this.panel19.PerformLayout();
            ((ISupportInitialize)(this.spinTabPressureBrushOpacity)).EndInit();
            this.panelTabPressureBrushFlow.ResumeLayout(false);
            this.panel3.ResumeLayout(false);
            this.panel3.PerformLayout();
            ((ISupportInitialize)(this.spinTabPressureBrushFlow)).EndInit();
            this.panelTabPressureBrushSize.ResumeLayout(false);
            this.panel8.ResumeLayout(false);
            this.panel8.PerformLayout();
            ((ISupportInitialize)(this.spinTabPressureBrushSize)).EndInit();
            this.panelTabPressureBrushRotation.ResumeLayout(false);
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            ((ISupportInitialize)(this.spinTabPressureBrushRotation)).EndInit();
            this.panelTabPressureMinDrawDistance.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            ((ISupportInitialize)(this.spinTabPressureMinDrawDistance)).EndInit();
            this.panelTabPressureBrushDensity.ResumeLayout(false);
            this.panel4.ResumeLayout(false);
            this.panel4.PerformLayout();
            ((ISupportInitialize)(this.spinTabPressureBrushDensity)).EndInit();
            this.panelTabPressureRandMinSize.ResumeLayout(false);
            this.panel5.ResumeLayout(false);
            this.panel5.PerformLayout();
            ((ISupportInitialize)(this.spinTabPressureRandMinSize)).EndInit();
            this.panelTabPressureRandMaxSize.ResumeLayout(false);
            this.panel6.ResumeLayout(false);
            this.panel6.PerformLayout();
            ((ISupportInitialize)(this.spinTabPressureRandMaxSize)).EndInit();
            this.panelTabPressureRandRotLeft.ResumeLayout(false);
            this.panel7.ResumeLayout(false);
            this.panel7.PerformLayout();
            ((ISupportInitialize)(this.spinTabPressureRandRotLeft)).EndInit();
            this.panelTabPressureRandRotRight.ResumeLayout(false);
            this.panel9.ResumeLayout(false);
            this.panel9.PerformLayout();
            ((ISupportInitialize)(this.spinTabPressureRandRotRight)).EndInit();
            this.panelTabPressureRandFlowLoss.ResumeLayout(false);
            this.panel10.ResumeLayout(false);
            this.panel10.PerformLayout();
            ((ISupportInitialize)(this.spinTabPressureRandFlowLoss)).EndInit();
            this.panelTabPressureRandHorShift.ResumeLayout(false);
            this.panel12.ResumeLayout(false);
            this.panel12.PerformLayout();
            ((ISupportInitialize)(this.spinTabPressureRandHorShift)).EndInit();
            this.panelTabPressureRandVerShift.ResumeLayout(false);
            this.panel11.ResumeLayout(false);
            this.panel11.PerformLayout();
            ((ISupportInitialize)(this.spinTabPressureRandVerShift)).EndInit();
            this.panelTabPressureRedJitter.ResumeLayout(false);
            this.panel13.ResumeLayout(false);
            this.panel13.PerformLayout();
            ((ISupportInitialize)(this.spinTabPressureMinRedJitter)).EndInit();
            ((ISupportInitialize)(this.spinTabPressureMaxRedJitter)).EndInit();
            this.panelTabPressureGreenJitter.ResumeLayout(false);
            this.panel14.ResumeLayout(false);
            this.panel14.PerformLayout();
            ((ISupportInitialize)(this.spinTabPressureMinGreenJitter)).EndInit();
            ((ISupportInitialize)(this.spinTabPressureMaxGreenJitter)).EndInit();
            this.panelTabPressureBlueJitter.ResumeLayout(false);
            this.panel15.ResumeLayout(false);
            this.panel15.PerformLayout();
            ((ISupportInitialize)(this.spinTabPressureMinBlueJitter)).EndInit();
            ((ISupportInitialize)(this.spinTabPressureMaxBlueJitter)).EndInit();
            this.panelTabPressureHueJitter.ResumeLayout(false);
            this.panel16.ResumeLayout(false);
            this.panel16.PerformLayout();
            ((ISupportInitialize)(this.spinTabPressureMinHueJitter)).EndInit();
            ((ISupportInitialize)(this.spinTabPressureMaxHueJitter)).EndInit();
            this.panelTabPressureSatJitter.ResumeLayout(false);
            this.panel17.ResumeLayout(false);
            this.panel17.PerformLayout();
            ((ISupportInitialize)(this.spinTabPressureMinSatJitter)).EndInit();
            ((ISupportInitialize)(this.spinTabPressureMaxSatJitter)).EndInit();
            this.panelTabPressureValueJitter.ResumeLayout(false);
            this.panel18.ResumeLayout(false);
            this.panel18.PerformLayout();
            ((ISupportInitialize)(this.spinTabPressureMinValueJitter)).EndInit();
            ((ISupportInitialize)(this.spinTabPressureMaxValueJitter)).EndInit();
            this.panelSettings.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        /// <summary>
        /// Positions the window according to the paint.net window.
        /// </summary>
        private void DynamicDrawWindow_Load(object sender, EventArgs e)
        {
            this.DesktopLocation = Owner.PointToScreen(new Point(0, 30));
            this.Size = new Size(Owner.ClientSize.Width, Owner.ClientSize.Height - 30);
            this.WindowState = Owner.WindowState;
        }

        /// <summary>
        /// Handles manual resizing of any element that requires it.
        /// </summary>
        private void WinDynamicDraw_Resize(object sender, EventArgs e)
        {
            this.txtTooltip.MaximumSize = new Size(displayCanvas.Width, displayCanvas.Height);
            displayCanvas.Refresh();
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

            if (Clipboard.ContainsData("PNG"))
            {
                contextMenu.Items.Add(new ToolStripMenuItem(Strings.BackgroundImage,
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
                                    bmpBackgroundClipboard?.Dispose();

                                    // Sets the clipboard background image.
                                    using (Image clipboardImage = Image.FromStream(stream))
                                    {
                                        bmpBackgroundClipboard = new Bitmap(bmpCommitted.Width, bmpCommitted.Height, PixelFormat.Format32bppPArgb);
                                        using (Graphics graphics = Graphics.FromImage(bmpBackgroundClipboard))
                                        {
                                            graphics.CompositingMode = CompositingMode.SourceCopy;
                                            graphics.DrawImage(clipboardImage, 0, 0, bmpBackgroundClipboard.Width, bmpBackgroundClipboard.Height);
                                        }
                                    }

                                    backgroundDisplayMode = BackgroundDisplayMode.Clipboard;
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

            //Options to set the background colors / image.
            contextMenu.Items.Add(new ToolStripMenuItem(Strings.BackgroundTransparent,
                null,
                new EventHandler((a, b) =>
                {
                    backgroundDisplayMode = BackgroundDisplayMode.Transparent;
                    bmpBackgroundClipboard?.Dispose();
                })));
            contextMenu.Items.Add(new ToolStripMenuItem(Strings.BackgroundNone,
                null,
                new EventHandler((a, b) =>
                {
                    backgroundDisplayMode = BackgroundDisplayMode.Gray;
                    bmpBackgroundClipboard?.Dispose();
                })));
            contextMenu.Items.Add(new ToolStripMenuItem(Strings.BackgroundWhite,
                null,
                new EventHandler((a, b) =>
                {
                    backgroundDisplayMode = BackgroundDisplayMode.White;
                    bmpBackgroundClipboard?.Dispose();
                })));
            contextMenu.Items.Add(new ToolStripMenuItem(Strings.BackgroundBlack,
                null,
                new EventHandler((a, b) =>
                {
                    backgroundDisplayMode = BackgroundDisplayMode.Black;
                    bmpBackgroundClipboard?.Dispose();
                })));

            contextMenu.Show(sender, location);
        }

        /// <summary>
        /// Switches to the provided tool.
        /// </summary>
        /// <param name="toolToSwitchTo">The tool to switch to.</param>
        private void SwitchTool(Tool toolToSwitchTo)
        {
            if (toolToSwitchTo == Tool.PreviousTool)
            {
                toolToSwitchTo = lastTool;
            }

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

            // End any brush stroke in progress.
            if (isUserDrawing.started)
            {
                DisplayCanvas_MouseUp(this, null);
            }

            UpdateEnabledControls();
            UpdateBrushImage();
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
            double deltaX = locX - bmpCommitted.Width / 2;
            double deltaY = locY - bmpCommitted.Height / 2;
            double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            double angle = Math.Atan2(deltaY, deltaX);

            // subtracts canvas rotation angle to get the coordinates rotated w.r.t. the canvas.
            angle -= sliderCanvasAngle.Value * Math.PI / 180;

            if (doClamp)
            {
                return new PointF(
                    (float)Math.Clamp(
                        bmpCommitted.Width / 2f + Math.Cos(angle) * distance, 0, bmpCommitted.Width),
                    (float)Math.Clamp(
                        bmpCommitted.Height / 2f + Math.Sin(angle) * distance, 0, bmpCommitted.Height));
            }

            return new PointF(
                (float)(bmpCommitted.Width / 2f + Math.Cos(angle) * distance),
                (float)(bmpCommitted.Height / 2f + Math.Sin(angle) * distance));
        }

        /// <summary>
        /// Updates all settings based on the currently selected brush.
        /// </summary>
        private void UpdateBrush(BrushSettings settings)
        {
            // Whether the delete brush button is enabled or not.
            bttnDeleteBrush.Enabled = currentBrushPath != null &&
                !PersistentSettings.defaultBrushes.ContainsKey(currentBrushPath);

            //Copies GUI values from the settings.
            sliderBrushSize.Value = settings.BrushSize;
            tokenSelectedBrushImagePath = settings.BrushImagePath;

            //Sets all other fields.
            sliderBrushOpacity.Value = settings.BrushOpacity;
            sliderBrushDensity.Value = settings.BrushDensity;
            sliderBrushFlow.Value = settings.BrushFlow;
            sliderBrushRotation.Value = settings.BrushRotation;
            sliderRandFlowLoss.Value = settings.RandFlowLoss;
            sliderRandHorzShift.Value = settings.RandHorzShift;
            sliderRandMaxSize.Value = settings.RandMaxSize;
            sliderRandMinSize.Value = settings.RandMinSize;
            sliderRandRotLeft.Value = settings.RandRotLeft;
            sliderRandRotRight.Value = settings.RandRotRight;
            sliderRandVertShift.Value = settings.RandVertShift;
            chkbxAutomaticBrushDensity.Checked = settings.AutomaticBrushDensity;
            chkbxSeamlessDrawing.Checked = settings.SeamlessDrawing;
            chkbxOrientToMouse.Checked = settings.DoRotateWithMouse;
            chkbxColorizeBrush.Checked = settings.DoColorizeBrush;
            sliderColorInfluence.Value = settings.ColorInfluence;
            chkbxColorInfluenceHue.Checked = settings.ColorInfluenceHue;
            chkbxColorInfluenceSat.Checked = settings.ColorInfluenceSat;
            chkbxColorInfluenceVal.Checked = settings.ColorInfluenceVal;
            chkbxDitherDraw.Checked = settings.DoDitherDraw;
            chkbxLockAlpha.Checked = settings.DoLockAlpha;
            chkbxLockR.Checked = settings.DoLockR;
            chkbxLockG.Checked = settings.DoLockG;
            chkbxLockB.Checked = settings.DoLockB;
            chkbxLockHue.Checked = settings.DoLockHue;
            chkbxLockSat.Checked = settings.DoLockSat;
            chkbxLockVal.Checked = settings.DoLockVal;
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
            sliderShiftFlow.Value = settings.FlowChange;
            sliderShiftSize.Value = settings.SizeChange;
            sliderShiftRotation.Value = settings.RotChange;
            cmbxChosenEffect.SelectedIndex = settings.CmbxChosenEffect;
            cmbxTabPressureBrushDensity.SelectedIndex = settings.CmbxTabPressureBrushDensity;
            cmbxTabPressureBrushFlow.SelectedIndex = settings.CmbxTabPressureBrushFlow;
            cmbxTabPressureBrushOpacity.SelectedIndex = settings.CmbxTabPressureBrushOpacity;
            cmbxTabPressureBrushRotation.SelectedIndex = settings.CmbxTabPressureBrushRotation;
            cmbxTabPressureBrushSize.SelectedIndex = settings.CmbxTabPressureBrushSize;
            cmbxTabPressureBlueJitter.SelectedIndex = settings.CmbxTabPressureBlueJitter;
            cmbxTabPressureGreenJitter.SelectedIndex = settings.CmbxTabPressureGreenJitter;
            cmbxTabPressureHueJitter.SelectedIndex = settings.CmbxTabPressureHueJitter;
            cmbxTabPressureMinDrawDistance.SelectedIndex = settings.CmbxTabPressureMinDrawDistance;
            cmbxTabPressureRedJitter.SelectedIndex = settings.CmbxTabPressureRedJitter;
            cmbxTabPressureSatJitter.SelectedIndex = settings.CmbxTabPressureSatJitter;
            cmbxTabPressureValueJitter.SelectedIndex = settings.CmbxTabPressureValueJitter;
            cmbxTabPressureRandFlowLoss.SelectedIndex = settings.CmbxTabPressureRandFlowLoss;
            cmbxTabPressureRandHorShift.SelectedIndex = settings.CmbxTabPressureRandHorShift;
            cmbxTabPressureRandMaxSize.SelectedIndex = settings.CmbxTabPressureRandMaxSize;
            cmbxTabPressureRandMinSize.SelectedIndex = settings.CmbxTabPressureRandMinSize;
            cmbxTabPressureRandRotLeft.SelectedIndex = settings.CmbxTabPressureRandRotLeft;
            cmbxTabPressureRandRotRight.SelectedIndex = settings.CmbxTabPressureRandRotRight;
            cmbxTabPressureRandVerShift.SelectedIndex = settings.CmbxTabPressureRandVerShift;
            spinTabPressureBrushDensity.Value = settings.TabPressureBrushDensity;
            spinTabPressureBrushFlow.Value = settings.TabPressureBrushFlow;
            spinTabPressureBrushOpacity.Value = settings.TabPressureBrushOpacity;
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
            spinTabPressureRandFlowLoss.Value = settings.TabPressureRandFlowLoss;
            spinTabPressureRandHorShift.Value = settings.TabPressureRandHorShift;
            spinTabPressureRandMaxSize.Value = settings.TabPressureRandMaxSize;
            spinTabPressureRandMinSize.Value = settings.TabPressureRandMinSize;
            spinTabPressureRandRotLeft.Value = settings.TabPressureRandRotLeft;
            spinTabPressureRandRotRight.Value = settings.TabPressureRandRotRight;
            spinTabPressureRandVerShift.Value = settings.TabPressureRandVerShift;
            cmbxBlendMode.SelectedIndex = (int)settings.BlendMode;
            cmbxBrushSmoothing.SelectedIndex = (int)settings.Smoothing;
            cmbxSymmetry.SelectedIndex = (int)settings.Symmetry;

            UpdateEnabledControls();
            UpdateBrushColor(settings.BrushColor);
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
            bttnBrushColor.BackColor = Color.FromArgb(newColor.R, newColor.G, newColor.B);
            UpdateBrushImage();
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
        /// Recreates the brush image with color and alpha effects applied.
        /// </summary>
        private void UpdateBrushImage()
        {
            if (bmpBrush == null)
            {
                return;
            }

            int finalBrushFlow = Math.Clamp(Utils.GetStrengthMappedValue(sliderBrushFlow.Value,
                (int)spinTabPressureBrushFlow.Value,
                sliderBrushFlow.Maximum,
                tabletPressureRatio,
                ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureBrushFlow.SelectedItem).ValueMember), 0, 255);

            //Sets the color and alpha.
            Color setColor = bttnBrushColor.BackColor;
            float multAlpha = (activeTool == Tool.Eraser || (BlendMode)cmbxBlendMode.SelectedIndex != BlendMode.Overwrite)
                ? finalBrushFlow / 255f
                : 1;

            int maxPossibleSize = sliderRandMaxSize.Value
                + Math.Max(sliderBrushSize.Value, Utils.GetStrengthMappedValue(sliderBrushSize.Value,
                    (int)spinTabPressureBrushSize.Value, sliderBrushSize.Maximum, 1,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureBrushSize.SelectedItem).ValueMember));

            // Creates a downsized intermediate bmp for faster transformations and blitting. Brush assumed square.
            if (bmpBrushDownsized != null && maxPossibleSize > bmpBrushDownsized.Width)
            {
                bmpBrushDownsized.Dispose();
                bmpBrushDownsized = null;
            }
            if (maxPossibleSize < bmpBrush.Width && (bmpBrushDownsized == null || bmpBrushDownsized.Width != maxPossibleSize))
            {
                bmpBrushDownsized?.Dispose();
                bmpBrushDownsized = Utils.ScaleImage(
                    bmpBrush,
                    new Size(maxPossibleSize, maxPossibleSize),
                    false, false, null,
                    (CmbxSmoothing.Smoothing)cmbxBrushSmoothing.SelectedIndex);
            }

            if (bmpBrushDownsized != null || bmpBrush != null)
            {
                //Applies the color and alpha changes.
                bmpBrushEffects?.Dispose();
                bmpBrushEffects = Utils.FormatImage(bmpBrushDownsized ?? bmpBrush, PixelFormat.Format32bppPArgb);

                // Replaces RGB entirely with the active color preemptive to drawing when possible, for performance.
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
        /// Updates which controls are enabled or not based on current settings.
        /// </summary>
        private void UpdateEnabledControls()
        {
            bool enableColorInfluence = !chkbxColorizeBrush.Checked && activeTool != Tool.Eraser;
            bool enableColorJitter = activeTool != Tool.Eraser && (chkbxColorizeBrush.Checked || sliderColorInfluence.Value != 0);

            sliderBrushOpacity.Enabled = ((BlendMode)cmbxBlendMode.SelectedIndex) != BlendMode.Overwrite && activeTool != Tool.Eraser;

            chkbxColorizeBrush.Enabled = activeTool != Tool.Eraser;
            txtColorInfluence.Visible = enableColorInfluence;
            sliderColorInfluence.Visible = enableColorInfluence;
            panelColorInfluenceHSV.Visible = enableColorInfluence && sliderColorInfluence.Value != 0;
            chkbxLockAlpha.Enabled = activeTool != Tool.Eraser;
            cmbxBlendMode.Enabled = activeTool != Tool.Eraser;

            sliderJitterMaxRed.Enabled = enableColorJitter;
            sliderJitterMinRed.Enabled = enableColorJitter;
            sliderJitterMaxGreen.Enabled = enableColorJitter;
            sliderJitterMinGreen.Enabled = enableColorJitter;
            sliderJitterMaxBlue.Enabled = enableColorJitter;
            sliderJitterMinBlue.Enabled = enableColorJitter;
            sliderJitterMaxHue.Enabled = enableColorJitter;
            sliderJitterMinHue.Enabled = enableColorJitter;
            sliderJitterMaxSat.Enabled = enableColorJitter;
            sliderJitterMinSat.Enabled = enableColorJitter;
            sliderJitterMaxVal.Enabled = enableColorJitter;
            sliderJitterMinVal.Enabled = enableColorJitter;
            panelTabPressureRedJitter.Enabled = enableColorJitter;
            panelTabPressureBlueJitter.Enabled = enableColorJitter;
            panelTabPressureGreenJitter.Enabled = enableColorJitter;
            panelTabPressureHueJitter.Enabled = enableColorJitter;
            panelTabPressureSatJitter.Enabled = enableColorJitter;
            panelTabPressureValueJitter.Enabled = enableColorJitter;

            bttnBrushColor.Visible = (chkbxColorizeBrush.Checked || sliderColorInfluence.Value != 0) && activeTool != Tool.Eraser;
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
            }
            else
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
                sliderCanvasZoom.Value = Math.Clamp(
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

            // Prevent losing the canvas due to nudging off-screen prior to zoom.
            if (canvas.x + canvas.width < 0 || canvas.x > displayCanvas.Width)
            {
                isWheelZooming = false;
                canvas.x = (displayCanvas.Width - canvas.width) / 2;
            }
            if (canvas.y + canvas.height < 0 || canvas.y > displayCanvas.Height)
            {
                isWheelZooming = false;
                canvas.y = (displayCanvas.Height - canvas.height) / 2;
            }

            //Gets the new width and height, adjusted for zooming.
            float zoomWidth = bmpCommitted.Width * newZoomFactor;
            float zoomHeight = bmpCommitted.Height * newZoomFactor;

            PointF zoomingPoint = isWheelZooming
                ? mouseLoc
                : new PointF(
                    displayCanvas.ClientSize.Width / 2f - canvas.x,
                    displayCanvas.ClientSize.Height / 2f - canvas.y);

            // Clamp the zooming point at the edges of the canvas.
            zoomingPoint.X = Math.Clamp(zoomingPoint.X, 0, canvas.width);
            zoomingPoint.Y = Math.Clamp(zoomingPoint.Y, 0, canvas.height);

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
                                        using Bitmap newBmp = Utils.MakeTransparent(scaledBrushImage ?? item.Image);
                                        using Bitmap newBmp2 = Utils.MakeBitmapSquare(newBmp);
                                        Utils.OverwriteBits(newBmp2, brushImage);

                                        if (scaledBrushImage != null)
                                        {
                                            scaledBrushImage.Dispose();
                                            scaledBrushImage = null;
                                        }

                                        string brushName = item.Name;

                                        if (string.IsNullOrEmpty(brushName))
                                        {
                                            brushName = string.Format(
                                                System.Globalization.CultureInfo.CurrentCulture,
                                                Strings.AbrBrushNameFallbackFormat,
                                                i);
                                        }

                                        // Brush images with the same location need unique names. Append spaces until unique.
                                        while (brushImages.Any(brush => brush.Name.Equals(brushName, StringComparison.Ordinal)))
                                        {
                                            brushName += " ";
                                        }

                                        // Add the brush image to the list and generate the ListView thumbnail.
                                        loadedBrushImages.Add(
                                            new BrushSelectorItem(brushName, location, brushImage, tempDir.GetRandomFileName(), maxThumbnailHeight));

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
                                //Creates the brush image space.
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
                                using Bitmap newBmp = Utils.MakeTransparent(scaledBrush ?? bmp);
                                using Bitmap newBmp2 = Utils.MakeBitmapSquare(newBmp);
                                Utils.OverwriteBits(newBmp2, brushImage);

                                if (scaledBrush != null)
                                {
                                    scaledBrush.Dispose();
                                    scaledBrush = null;
                                }
                            }

                            //Gets the last word in the filename without the path.
                            Regex getOnlyFilename = new Regex(@"[\w-]+\.");
                            string brushName = getOnlyFilename.Match(file).Value;

                            //Removes the file extension dot.
                            if (brushName.EndsWith("."))
                            {
                                brushName = brushName.Remove(brushName.Length - 1);
                            }

                            // Brush images with the same location need unique names. Append spaces until unique.
                            if (loadedBrushImages.Any(a => a.Location != null && a.Location.Equals(file) && a.Name != null && a.Name.Equals(brushName)))
                            {
                                brushName += " ";
                            }

                            //Adds the brush image.
                            loadedBrushImages.Add(
                                new BrushSelectorItem(brushName, file, brushImage, tempDir.GetRandomFileName(), maxThumbnailHeight));

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
                    brushImageLoadProgressBar.Visible = false;
                    bttnAddBrushImages.Visible = true;
                }
                else
                {
                    listviewBrushImagePicker.VirtualListSize = loadedBrushImages.Count;

                    if (loadedBrushImages.Count > 0)
                    {
                        // Select the user's previous brush if it is present, otherwise select the last added brush.
                        int selectedItemIndex = loadedBrushImages.Count - 1;

                        if (!string.IsNullOrEmpty(tokenSelectedBrushImagePath))
                        {
                            int index = loadedBrushImages.FindIndex(brush => brush.Location?.Equals(tokenSelectedBrushImagePath) ?? false);

                            if (index == -1)
                            {
                                index = loadedBrushImages.FindIndex(brush => brush.Name?.Equals(tokenSelectedBrushImagePath) ?? false);
                            }
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
                            var rotatedPoint = TransformPoint(mouseLoc, true);
                            double radius = Math.Sqrt(
                                Math.Pow(rotatedPoint.X - (symmetryOrigin.X + symmetryOrigins[i].X), 2) +
                                Math.Pow(rotatedPoint.Y - (symmetryOrigin.Y + symmetryOrigins[i].Y), 2));

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
                (e.Button == MouseButtons.Left && KeyShortcutManager.IsKeyDown(Keys.ControlKey)) ||
                KeyShortcutManager.IsKeyDown(Keys.Space))
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
                    isUserDrawing.started = true;
                    timerRepositionUpdate.Enabled = true;

                    //Draws the brush on the first canvas click. Lines aren't drawn at a single point.
                    //Doesn't draw for tablets, since the user hasn't exerted full pressure yet.
                    if (!chkbxOrientToMouse.Checked)
                    {
                        int finalBrushSize = Utils.GetStrengthMappedValue(sliderBrushSize.Value,
                            (int)spinTabPressureBrushSize.Value,
                            sliderBrushSize.Maximum,
                            tabletPressureRatio,
                            ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureBrushSize.SelectedItem).ValueMember);

                        DrawBrush(new PointF(
                            mouseLocPrev.X / canvasZoom - halfPixelOffset,
                            mouseLocPrev.Y / canvasZoom - halfPixelOffset),
                            finalBrushSize, tabletPressureRatio);
                    }
                }
                // Grabs the color under the mouse.
                else if (activeTool == Tool.ColorPicker)
                {
                    GetColorFromCanvas(new Point(
                        (int)Math.Round(mouseLocPrev.X / canvasZoom),
                        (int)Math.Round(mouseLocPrev.Y / canvasZoom)));

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

                GraphicsPath path = new GraphicsPath();
                path.AddRectangle(range);
                Matrix m = new Matrix();
                m.RotateAt(sliderCanvasAngle.Value, new PointF(range.X + range.Width / 2f, range.Y + range.Height / 2f));
                path.Transform(m);

                //Moves the drawing region.
                int locx = canvas.x + (int)Math.Round(mouseLoc.X - mouseLocPrev.X);
                int locy = canvas.y + (int)Math.Round(mouseLoc.Y - mouseLocPrev.Y);

                //Updates the position of the canvas.
                canvas.x = locx;
                canvas.y = locy;
            }

            else if (isUserDrawing.started)
            {
                finalMinDrawDistance = Math.Clamp(Utils.GetStrengthMappedValue(sliderMinDrawDistance.Value,
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
                    int finalBrushDensity = Math.Clamp(Utils.GetStrengthMappedValue(sliderBrushDensity.Value,
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
                                mouseLoc.X / canvasZoom - halfPixelOffset,
                                mouseLoc.Y / canvasZoom - halfPixelOffset),
                                finalBrushSize, tabletPressureRatio);
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

                        double deltaX = (mouseLoc.X - mouseLocPrev.X) / canvasZoom;
                        double deltaY = (mouseLoc.Y - mouseLocPrev.Y) / canvasZoom;
                        double brushWidthFrac = finalBrushSize / (double)finalBrushDensity;
                        double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                        double angle = Math.Atan2(deltaY, deltaX);
                        double xDist = Math.Cos(angle);
                        double yDist = Math.Sin(angle);
                        double numIntervals = distance / (double.IsNaN(brushWidthFrac) ? 1 : brushWidthFrac);
                        float tabletPressure = tabletPressureRatio;

                        for (int i = 1; i <= (int)numIntervals; i++)
                        {
                            // lerp between the last and current tablet pressure for smoother lines
                            if (tabletPressureRatioPrev != tabletPressureRatio && spinTabPressureBrushSize.Value != 0)
                            {
                                tabletPressure = (float)(tabletPressureRatioPrev + i / numIntervals * (tabletPressureRatio - tabletPressureRatioPrev));
                                finalBrushSize = Utils.GetStrengthMappedValue(sliderBrushSize.Value,
                                    (int)spinTabPressureBrushSize.Value,
                                    sliderBrushSize.Maximum,
                                    tabletPressure,
                                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureBrushSize.SelectedItem).ValueMember);
                            }

                            DrawBrush(new PointF(
                                (float)(mouseLocPrev.X / canvasZoom + xDist * brushWidthFrac * i - halfPixelOffset),
                                (float)(mouseLocPrev.Y / canvasZoom + yDist * brushWidthFrac * i - halfPixelOffset)),
                                finalBrushSize, tabletPressure);
                        }

                        double extraDist = brushWidthFrac * (numIntervals - (int)numIntervals);

                        // Same as mouse position except for remainder.
                        mouseLoc = new PointF(
                            (float)(e.Location.X - canvas.x - xDist * extraDist * canvasZoom),
                            (float)(e.Location.Y - canvas.y - yDist * extraDist * canvasZoom));
                        mouseLocPrev = mouseLoc;

                        tabletPressureRatioPrev = tabletPressureRatio;
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
            if (isUserDrawing.started && !isUserDrawing.canvasChanged)
            {
                DrawBrush(new PointF(
                    mouseLocPrev.X / canvasZoom - halfPixelOffset,
                    mouseLocPrev.Y / canvasZoom - halfPixelOffset),
                    sliderBrushSize.Value, 1); // opinionated: pass as full pressure because this is usually desired.
            }

            // Merge the staged layer down to committed, then clear the staged layer.
            if (isUserDrawing.stagedChanged)
            {
                Utils.MergeImage(bmpStaged, bmpCommitted, bmpCommitted,
                    bmpCommitted.GetBounds(),
                    (BlendMode)cmbxBlendMode.SelectedIndex,
                    (chkbxLockAlpha.Checked,
                    chkbxLockR.Checked, chkbxLockG.Checked, chkbxLockB.Checked,
                    chkbxLockHue.Checked, chkbxLockSat.Checked, chkbxLockVal.Checked));

                Utils.ColorImage(bmpStaged, ColorBgra.Black, 0);
            }

            isUserPanning = false;
            isUserDrawing.started = false;
            isUserDrawing.canvasChanged = false;
            isUserDrawing.stagedChanged = false;
            timerRepositionUpdate.Enabled = false;

            //Lets the user click anywhere to draw again.
            mouseLocBrush = null;
        }

        /// <summary>
        /// Redraws the canvas and draws shapes to illustrate the current tool.
        /// </summary>
        private void DisplayCanvas_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            e.Graphics.SmoothingMode = SmoothingMode.None;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;

            float drawingOffsetX = EnvironmentParameters.SourceSurface.Width * 0.5f * canvasZoom;
            float drawingOffsetY = EnvironmentParameters.SourceSurface.Height * 0.5f * canvasZoom;

            e.Graphics.TranslateTransform(canvas.x + drawingOffsetX, canvas.y + drawingOffsetY);
            e.Graphics.RotateTransform(sliderCanvasAngle.Value);
            e.Graphics.TranslateTransform(-drawingOffsetX, -drawingOffsetY);

            // Computes the visible region of the image. This speed optimization is only used for unrotated canvases
            // due to complexity in figuring out the math for the rotated canvas at the moment.
            int leftCutoff = -Math.Min(canvas.x, 0);
            int topCutoff = -Math.Min(canvas.y, 0);
            int overshootX = -Math.Min(displayCanvas.Width - (canvas.x + canvas.width), 0);
            int overshootY = -Math.Min(displayCanvas.Height - (canvas.y + canvas.height), 0);
            float lCutoffUnzoomed = leftCutoff / canvasZoom;
            float tCutoffUnzoomed = topCutoff / canvasZoom;

            Rectangle visibleBounds = (sliderCanvasAngle.Value == 0)
                ? new Rectangle(
                    leftCutoff, topCutoff,
                    canvas.width - overshootX - leftCutoff,
                    canvas.height - overshootY - topCutoff)
                : new Rectangle(0, 0, canvas.width, canvas.height);

            // Draws the background according to the selected background display mode. Note it will
            // be drawn with nearest neighbor interpolation, for speed.
            if (backgroundDisplayMode == BackgroundDisplayMode.Transparent ||
                backgroundDisplayMode == BackgroundDisplayMode.Clipboard)
            {
                HatchBrush hatchBrush = new HatchBrush(HatchStyle.LargeCheckerBoard, Color.White, Color.FromArgb(191, 191, 191));
                e.Graphics.FillRectangle(hatchBrush, visibleBounds);
            }
            if (backgroundDisplayMode == BackgroundDisplayMode.Clipboard)
            {
                if (sliderCanvasAngle.Value == 0)
                {
                    e.Graphics.DrawImage(
                        bmpBackgroundClipboard,
                        visibleBounds,
                        lCutoffUnzoomed,
                        tCutoffUnzoomed,
                        EnvironmentParameters.SourceSurface.Width - overshootX / canvasZoom - lCutoffUnzoomed,
                        EnvironmentParameters.SourceSurface.Height - overshootY / canvasZoom - tCutoffUnzoomed,
                        GraphicsUnit.Pixel);
                }
                else
                {
                    e.Graphics.DrawImage(bmpBackgroundClipboard, 0, 0, canvas.width, canvas.height);
                }
            }
            else if (backgroundDisplayMode == BackgroundDisplayMode.White)
            {
                e.Graphics.FillRectangle(Brushes.White, visibleBounds);
            }
            else if (backgroundDisplayMode == BackgroundDisplayMode.Black)
            {
                e.Graphics.FillRectangle(Brushes.Black, visibleBounds);
            }

            // Sets the smoothing mode based on zoom for the canvas.
            if (canvasZoom < 0.5f) { e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic; }
            else if (canvasZoom < 0.75f) { e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear; }
            else if (canvasZoom < 1) { e.Graphics.InterpolationMode = InterpolationMode.Bilinear; }
            else { e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor; }

            // Draws all merge region rectangles.
            if (isUserDrawing.stagedChanged)
            {
                for (int i = mergeRegions.Count - 1; i >= 0; i--)
                {
                    Rectangle rect = Rectangle.Intersect(bmpCommitted.GetBounds(), mergeRegions[i]);
                    mergeRegions.RemoveAt(i);

                    if (rect.Width > 0 && rect.Height > 0)
                    {
                        Utils.MergeImage(bmpStaged, bmpCommitted, bmpMerged,
                            rect,
                            (BlendMode)cmbxBlendMode.SelectedIndex,
                            (chkbxLockAlpha.Checked,
                            chkbxLockR.Checked, chkbxLockG.Checked, chkbxLockB.Checked,
                            chkbxLockHue.Checked, chkbxLockSat.Checked, chkbxLockVal.Checked));
                    }
                }
            }

            //Draws only the visible portion of the image.
            Bitmap bmpToDraw = 
                effectToDraw.effect != null ? bmpStaged :
                isUserDrawing.stagedChanged ? bmpMerged : bmpCommitted;

            if (sliderCanvasAngle.Value == 0)
            {
                e.Graphics.DrawImage(
                    bmpToDraw,
                    visibleBounds,
                    lCutoffUnzoomed,
                    tCutoffUnzoomed,
                    EnvironmentParameters.SourceSurface.Width - overshootX / canvasZoom - lCutoffUnzoomed,
                    EnvironmentParameters.SourceSurface.Height - overshootY / canvasZoom - tCutoffUnzoomed,
                    GraphicsUnit.Pixel);
            }
            else
            {
                e.Graphics.DrawImage(bmpToDraw, 0, 0, canvas.width, canvas.height);
            }

            //Draws the selection.
            PdnRegion selection = EnvironmentParameters.GetSelectionAsPdnRegion();
            Region selectionRegion = selection?.GetRegionReadOnly();

            long area = selection.GetArea64();
            if (selectionRegion != null)
            {
                //Calculates the outline once the selection becomes valid.
                if (selectionOutline == null)
                {
                    if (area != EnvironmentParameters.SourceSurface.Width * EnvironmentParameters.SourceSurface.Height)
                    {
                        selectionOutline = selection.ConstructOutline(
                            new RectangleF(0, 0,
                            EnvironmentParameters.SourceSurface.Width,
                            EnvironmentParameters.SourceSurface.Height),
                            canvasZoom);
                    }
                }

                //Scales to zoom so the drawing region accounts for scale.
                e.Graphics.ScaleTransform(canvasZoom, canvasZoom);

                //Creates the inverted region of the selection.
                using var drawingArea = new Region(new Rectangle
                    (0, 0, EnvironmentParameters.SourceSurface.Width, EnvironmentParameters.SourceSurface.Height));
                drawingArea.Exclude(selectionRegion);

                //Draws the region as a darkening over unselected pixels.
                e.Graphics.FillRegion(
                    new SolidBrush(Color.FromArgb(63, 0, 0, 0)), drawingArea);

                //Draws the outline of the selection.
                if (selectionOutline?.GetRegionData() != null)
                {
                    e.Graphics.FillRegion(
                        SystemBrushes.Highlight,
                        selectionRegion);
                }
            }

            e.Graphics.ResetTransform();

            if (activeTool == Tool.Brush || activeTool == Tool.Eraser)
            {
                //Draws the brush as a rectangle when not drawing by mouse.
                if (!isUserDrawing.started)
                {
                    float halfSize = sliderBrushSize.Value / 2f;
                    int radius = (int)(sliderBrushSize.Value * canvasZoom);

                    float x = sliderBrushSize.Value == 1
                        ? (int)(mouseLoc.X / canvasZoom) * canvasZoom + canvas.x
                        : (int)(mouseLoc.X / canvasZoom - halfSize + halfPixelOffset) * canvasZoom + canvas.x;

                    float y = sliderBrushSize.Value == 1
                        ? (int)(mouseLoc.Y / canvasZoom) * canvasZoom + canvas.y
                        : (int)(mouseLoc.Y / canvasZoom - halfSize + halfPixelOffset) * canvasZoom + canvas.y;

                    e.Graphics.TranslateTransform(x + radius / 2f, y + radius / 2f);
                    e.Graphics.RotateTransform(sliderBrushRotation.Value);
                    e.Graphics.TranslateTransform(-radius / 2f, -radius / 2f);

                    e.Graphics.DrawRectangle(Pens.Black, 0, 0, radius, radius);
                    e.Graphics.DrawRectangle(Pens.White, -1, -1, radius + 2, radius + 2);

                    e.Graphics.ResetTransform();
                }

                // Draws the minimum distance circle if min distance is in use.
                else if (finalMinDrawDistance > 0)
                {
                    e.Graphics.TranslateTransform(canvas.x, canvas.y);

                    int diameter = (int)(finalMinDrawDistance * 2 * canvasZoom);

                    e.Graphics.DrawEllipse(
                        Pens.Red,
                        (mouseLocBrush.HasValue ? mouseLocBrush.Value.X : mouseLoc.X) - (diameter / 2f) - 1,
                        (mouseLocBrush.HasValue ? mouseLocBrush.Value.Y : mouseLoc.Y) - (diameter / 2f) - 1,
                        diameter + 2,
                        diameter + 2);

                    e.Graphics.TranslateTransform(-canvas.x, -canvas.y);
                }
            }

            e.Graphics.TranslateTransform(canvas.x + drawingOffsetX, canvas.y + drawingOffsetY);
            e.Graphics.RotateTransform(sliderCanvasAngle.Value);
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
                            new PointF(EnvironmentParameters.SourceSurface.Width * canvasZoom, symmetryOrigin.Y * canvasZoom));
                        e.Graphics.DrawLine(
                            Pens.Red,
                            new PointF(symmetryOrigin.X * canvasZoom, 0),
                            new PointF(symmetryOrigin.X * canvasZoom, EnvironmentParameters.SourceSurface.Height * canvasZoom));
                    }

                    e.Graphics.ScaleTransform(canvasZoom, canvasZoom);

                    // Draws a rectangle for each origin point, either relative to the symmetry origin (in
                    // SetSymmetryOrigin tool) or the mouse (with the Brush tool)
                    if (!isUserDrawing.started)
                    {
                        float pointsDrawnX, pointsDrawnY;

                        if (activeTool == Tool.SetSymmetryOrigin)
                        {
                            var transformedMouseLoc = TransformPoint(mouseLoc, true);
                            Pen transparentRed = new Pen(Color.FromArgb(128, 128, 0, 0), 1);

                            e.Graphics.DrawRectangle(
                                transparentRed,
                                symmetryOrigin.X - (sliderBrushSize.Value / 2f),
                                symmetryOrigin.Y - (sliderBrushSize.Value / 2f),
                                sliderBrushSize.Value,
                                sliderBrushSize.Value);

                            e.Graphics.DrawRectangle(
                                Pens.Black,
                                transformedMouseLoc.X - (sliderBrushSize.Value / 2f),
                                transformedMouseLoc.Y - (sliderBrushSize.Value / 2f),
                                sliderBrushSize.Value,
                                sliderBrushSize.Value);

                            for (int i = 0; i < symmetryOrigins.Count; i++)
                            {
                                e.Graphics.DrawRectangle(
                                    transparentRed,
                                    symmetryOrigin.X + symmetryOrigins[i].X - (sliderBrushSize.Value / 2f),
                                    symmetryOrigin.Y + symmetryOrigins[i].Y - (sliderBrushSize.Value / 2f),
                                    sliderBrushSize.Value,
                                    sliderBrushSize.Value);

                                e.Graphics.DrawRectangle(
                                    Pens.Red,
                                    (float)(symmetryOrigin.X + symmetryOrigins[i].X - 1),
                                    (float)(symmetryOrigin.Y + symmetryOrigins[i].Y - 1),
                                    2, 2);
                            }
                        }
                        else
                        {
                            pointsDrawnX = (mouseLoc.X / canvasZoom - EnvironmentParameters.SourceSurface.Width / 2);
                            pointsDrawnY = (mouseLoc.Y / canvasZoom - EnvironmentParameters.SourceSurface.Height / 2);

                            for (int i = 0; i < symmetryOrigins.Count; i++)
                            {
                                float offsetX = pointsDrawnX + symmetryOrigins[i].X;
                                float offsetY = pointsDrawnY + symmetryOrigins[i].Y;
                                double dist = Math.Sqrt(offsetX * offsetX + offsetY * offsetY);
                                double angle = Math.Atan2(offsetY, offsetX);
                                angle -= sliderCanvasAngle.Value * Math.PI / 180;
                                e.Graphics.DrawRectangle(
                                    Pens.Red,
                                    (float)(EnvironmentParameters.SourceSurface.Width / 2 + dist * Math.Cos(angle) - 1),
                                    (float)(EnvironmentParameters.SourceSurface.Height / 2 + dist * Math.Sin(angle) - 1),
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
                        new PointF(EnvironmentParameters.SourceSurface.Width * canvasZoom, symmetryOrigin.Y * canvasZoom));
                    e.Graphics.DrawLine(
                        Pens.Red,
                        new PointF(symmetryOrigin.X * canvasZoom, 0),
                        new PointF(symmetryOrigin.X * canvasZoom, EnvironmentParameters.SourceSurface.Height * canvasZoom));
                }
            }

            e.Graphics.ResetTransform();
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
            scroll.Value = Math.Clamp(scroll.Value - mouseEvent.Delta, scroll.Minimum, scroll.Maximum);
            panelDockSettingsContainer.PerformLayout(); // visually update scrollbar since it won't always.
        }

        /// <summary>
        /// Called by the tablet drawing service. Reads the packet for x/y and pressure info.
        /// </summary>
        /// <param name="packet"></param>
        private void TabletUpdated(WintabDN.WintabPacket packet)
        {
            // Move cursor to stylus. This works since packets are only sent for touch or hover events.
            Cursor.Position = new Point(packet.pkX, packet.pkY);

            if (packet.pkSerialNumber == tabletLastPacketId) { return; }

            tabletLastPacketId = packet.pkSerialNumber;

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
            // canvas such that the top-left corner is (0, 0) up to its
            // width and height.
            Point mouseLocOnBG = displayCanvas.PointToClient(MousePosition);

            //Exits if the user isn't drawing out of the canvas boundary.
            if (!isUserDrawing.started || displayCanvas.ClientRectangle.Contains(mouseLocOnBG))
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

        #region Methods (button event handlers)
        private void AutomaticBrushDensity_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.AutomaticBrushDensityTip);
        }

        private void AutomaticBrushDensity_CheckedChanged(object sender, EventArgs e)
        {
            sliderBrushDensity.Enabled = !chkbxAutomaticBrushDensity.Checked;
        }

        /// <summary>
        /// Displays a dialog allowing the user to add new brushes.
        /// </summary>
        private void BttnAddBrushImages_Click(object sender, EventArgs e)
        {
            ImportBrushImages();
        }

        private void BttnAddBrushImages_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.AddBrushImagesTip);
        }

        private void StageEffect()
        {
            if (effectToDraw.effect == null)
            {
                return;
            }

            // Runs the effect.
            using Surface fromStagedSurf = Surface.CopyFromBitmap(bmpStaged);
            using Surface fromCommittedSurf = Surface.CopyFromBitmap(bmpCommitted);
            using RenderArgs fromStaged = new RenderArgs(fromStagedSurf);
            using RenderArgs fromCommitted = new RenderArgs(fromCommittedSurf);

            effectToDraw.effect.Render(
                effectToDraw.settings,
                fromStaged,
                fromCommitted,
                Utils.GetRois(bmpCommitted.Width, bmpCommitted.Height));

            Utils.OverwriteBits(fromStagedSurf, bmpStaged);
        }

        /// <summary>
        /// Displays a dialog allowing the user to edit effect settings.
        /// </summary>
        private void BttnChooseEffectSettings_Click(object sender, EventArgs e)
        {
            var effect = effectOptions[cmbxChosenEffect.SelectedIndex];

            if (effectToDraw.effect != null && effectToDraw.settings != null)
            {
                using EffectConfigDialog dialog = effectToDraw.effect.CreateConfigDialog();
                using Timer repaintTimer = new Timer() { Interval = 100, Enabled = false };
                dialog.EffectToken = effectToDraw.settings;

                dialog.EffectTokenChanged += (a, b) =>
                {
                    repaintTimer.Stop(); // resets
                    repaintTimer.Start();
                };
                repaintTimer.Tick += (a, b) =>
                {
                    repaintTimer.Stop();
                    StageEffect();
                };

                var dlgResult = dialog.ShowDialog();
                if (dlgResult == DialogResult.OK || dlgResult == DialogResult.Yes || dlgResult == DialogResult.Continue)
                {
                    StageEffect();
                }
            }
        }

        private void BttnChooseEffectSettings_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.ChooseEffectSettingsTip);
        }

        private void BttnBlendMode_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.BlendModeTip);
        }

        private void BttnBlendMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateEnabledControls();
            UpdateBrushImage();
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

        private void CmbxChosenEffect_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.ChosenEffectTip);
        }

        private void CmbxChosenEffect_SelectedIndexChanged(object sender, EventArgs e)
        {
            var effect = effectOptions[cmbxChosenEffect.SelectedIndex];
            if (effect.Item2 == null)
            {
                bttnChooseEffectSettings.Enabled = false;
                effectToDraw = new(null, null);
            }
            else
            {
                using Effect effectInstance = effect.Item2.CreateInstance();

                if (effect.Item2.Options.Flags.HasFlag(EffectFlags.Configurable))
                {
                    bttnChooseEffectSettings.Enabled = true;
                    using EffectConfigDialog dialog = effectInstance.CreateConfigDialog();
                    effectToDraw = new(effectInstance, dialog?.EffectToken);
                }
                else
                {
                    bttnChooseEffectSettings.Enabled = false;
                    effectToDraw = new(effectInstance, null);
                }
            }

            // Open the configuration dialog when changing the chosen effect.
            bttnChooseEffectSettings.Enabled = effect.Item2?.Options.Flags == EffectFlags.Configurable;
            if (bttnChooseEffectSettings.Enabled)
            {
                BttnChooseEffectSettings_Click(this, null);
            }

            Utils.ColorImage(bmpStaged, ColorBgra.Black, 0f);
            StageEffect();
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
            DialogResult = DialogResult.Cancel;

            // It's easy to hit the escape key on accident, especially when toggling other controls.
            // If any changes were made and the OK button was indirectly invoked, ask for confirmation first.
            if (undoHistory.Count > 0 && !bttnCancel.Focused &&
                MessageBox.Show(Strings.ConfirmCancel, Strings.Confirm, MessageBoxButtons.YesNo) != DialogResult.Yes)
            {
                DialogResult = DialogResult.None;
                return;
            }

            //Disables the button so it can't accidentally be called twice.
            bttnCancel.Enabled = false;

            this.Close();
        }

        private void BttnCancel_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.CancelTip);
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
            settings.CustomBrushes.Remove(currentBrushPath);
            listviewBrushPicker.Items.RemoveAt(listviewBrushPicker.SelectedIndices[0]);
            listviewBrushPicker.AutoResizeColumn(0, ColumnHeaderAutoResizeStyle.ColumnContent);
            settings.MarkSettingsChanged();
            currentBrushPath = null;
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

            // It's easy to hit the enter key on accident, especially when toggling other controls.
            // If any changes were made and the OK button was indirectly invoked, ask for confirmation first.
            if (undoHistory.Count > 0 && !bttnOk.Focused &&
                MessageBox.Show(Strings.ConfirmChanges, Strings.Confirm, MessageBoxButtons.YesNo) != DialogResult.Yes)
            {
                DialogResult = DialogResult.None;
                return;
            }

            //Disables the button so it can't accidentally be called twice.
            //Ensures settings will be saved.
            bttnOk.Enabled = false;

            //Sets the bitmap to draw. Locks to prevent concurrency.
            lock (RenderSettings.SurfaceToRender)
            {
                RenderSettings.SurfaceToRender = Surface.CopyFromBitmap(bmpCommitted);
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
                if (new DynamicDrawPreferences(settings).ShowDialog() == DialogResult.OK)
                {
                    InitBrushes();
                }
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
            isUserDrawing.started = false;
            isUserDrawing.canvasChanged = false;

            //Acquires the bitmap from the file and loads it if it exists.
            string fileAndPath = redoHistory.Pop();
            if (File.Exists(fileAndPath))
            {
                //Saves the drawing to the file for undo.
                string path = tempDir.GetTempPathName("HistoryBmp" + undoHistory.Count + ".undo");
                bmpCommitted.Save(path);
                undoHistory.Push(path);

                //Clears the current drawing (in case parts are transparent),
                //and draws the saved version.
                using (Bitmap redoBmp = new Bitmap(fileAndPath))
                {
                    Utils.OverwriteBits(redoBmp, bmpCommitted);
                    Utils.ColorImage(bmpStaged, ColorBgra.Black, 0);
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
                    BlendMode = (BlendMode)cmbxBlendMode.SelectedIndex,
                    BrushColor = bttnBrushColor.BackColor,
                    BrushDensity = sliderBrushDensity.Value,
                    BrushFlow = sliderBrushFlow.Value,
                    BrushImagePath = index >= 0 ? loadedBrushImages[index].Location ?? loadedBrushImages[index].Name : string.Empty,
                    BrushOpacity = sliderBrushOpacity.Value,
                    BrushRotation = sliderBrushRotation.Value,
                    BrushSize = sliderBrushSize.Value,
                    ColorInfluence = sliderColorInfluence.Value,
                    DoColorizeBrush = chkbxColorizeBrush.Checked,
                    ColorInfluenceHue = chkbxColorInfluenceHue.Checked,
                    ColorInfluenceSat = chkbxColorInfluenceSat.Checked,
                    ColorInfluenceVal = chkbxColorInfluenceVal.Checked,
                    DoDitherDraw = chkbxDitherDraw.Checked,
                    DoLockAlpha = chkbxLockAlpha.Checked,
                    DoLockR = chkbxLockR.Checked,
                    DoLockG = chkbxLockG.Checked,
                    DoLockB = chkbxLockB.Checked,
                    DoLockHue = chkbxLockHue.Checked,
                    DoLockSat = chkbxLockSat.Checked,
                    DoLockVal = chkbxLockVal.Checked,
                    FlowChange = sliderShiftFlow.Value,
                    SeamlessDrawing = chkbxSeamlessDrawing.Checked,
                    DoRotateWithMouse = chkbxOrientToMouse.Checked,
                    MinDrawDistance = sliderMinDrawDistance.Value,
                    RandFlowLoss = sliderRandFlowLoss.Value,
                    RandHorzShift = sliderRandHorzShift.Value,
                    RandMaxB = sliderJitterMaxBlue.Value,
                    RandMaxG = sliderJitterMaxGreen.Value,
                    RandMaxR = sliderJitterMaxRed.Value,
                    RandMaxH = sliderJitterMaxHue.Value,
                    RandMaxS = sliderJitterMaxSat.Value,
                    RandMaxV = sliderJitterMaxVal.Value,
                    RandMaxSize = sliderRandMaxSize.Value,
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
                    CmbxChosenEffect = cmbxChosenEffect.SelectedIndex,
                    CmbxTabPressureBrushDensity = cmbxTabPressureBrushDensity.SelectedIndex,
                    CmbxTabPressureBrushFlow = cmbxTabPressureBrushFlow.SelectedIndex,
                    CmbxTabPressureBrushOpacity = cmbxTabPressureBrushOpacity.SelectedIndex,
                    CmbxTabPressureBrushRotation = cmbxTabPressureBrushRotation.SelectedIndex,
                    CmbxTabPressureBrushSize = cmbxTabPressureBrushSize.SelectedIndex,
                    CmbxTabPressureBlueJitter = cmbxTabPressureBlueJitter.SelectedIndex,
                    CmbxTabPressureGreenJitter = cmbxTabPressureGreenJitter.SelectedIndex,
                    CmbxTabPressureHueJitter = cmbxTabPressureHueJitter.SelectedIndex,
                    CmbxTabPressureMinDrawDistance = cmbxTabPressureMinDrawDistance.SelectedIndex,
                    CmbxTabPressureRedJitter = cmbxTabPressureRedJitter.SelectedIndex,
                    CmbxTabPressureSatJitter = cmbxTabPressureSatJitter.SelectedIndex,
                    CmbxTabPressureValueJitter = cmbxTabPressureValueJitter.SelectedIndex,
                    CmbxTabPressureRandFlowLoss = cmbxTabPressureRandFlowLoss.SelectedIndex,
                    CmbxTabPressureRandHorShift = cmbxTabPressureRandHorShift.SelectedIndex,
                    CmbxTabPressureRandMaxSize = cmbxTabPressureRandMaxSize.SelectedIndex,
                    CmbxTabPressureRandMinSize = cmbxTabPressureRandMinSize.SelectedIndex,
                    CmbxTabPressureRandRotLeft = cmbxTabPressureRandRotLeft.SelectedIndex,
                    CmbxTabPressureRandRotRight = cmbxTabPressureRandRotRight.SelectedIndex,
                    CmbxTabPressureRandVerShift = cmbxTabPressureRandVerShift.SelectedIndex,
                    TabPressureBrushDensity = (int)spinTabPressureBrushDensity.Value,
                    TabPressureBrushFlow = (int)spinTabPressureBrushFlow.Value,
                    TabPressureBrushOpacity = (int)spinTabPressureBrushOpacity.Value,
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
                    TabPressureRandFlowLoss = (int)spinTabPressureRandFlowLoss.Value,
                    TabPressureRandHorShift = (int)spinTabPressureRandHorShift.Value,
                    TabPressureRandMaxSize = (int)spinTabPressureRandMaxSize.Value,
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
                listviewBrushPicker.AutoResizeColumn(0, ColumnHeaderAutoResizeStyle.ColumnContent);
                currentBrushPath = inputText;
                bttnDeleteBrush.Enabled = true;
            }
        }

        private void BttnSaveBrush_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.SaveNewBrushTip);
        }

        private void BttnSymmetry_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.SymmetryTip);
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
            isUserDrawing.started = false;
            isUserDrawing.canvasChanged = false;

            //Acquires the bitmap from the file and loads it if it exists.
            string fileAndPath = undoHistory.Pop();
            if (File.Exists(fileAndPath))
            {
                //Saves the drawing to the file for redo.
                string path = tempDir.GetTempPathName("HistoryBmp" + redoHistory.Count + ".redo");
                bmpCommitted.Save(path);
                redoHistory.Push(path);

                //Clears the current drawing (in case parts are transparent),
                //and draws the saved version.
                using (Bitmap undoBmp = new Bitmap(fileAndPath))
                {
                    Utils.OverwriteBits(undoBmp, bmpCommitted);
                    Utils.ColorImage(bmpStaged, ColorBgra.Black, 0);
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

        /// <summary>
        /// Resets the brush to reconfigure colorization. Colorization is
        /// applied when the brush is refreshed.
        /// </summary>
        private void ChkbxColorizeBrush_CheckedChanged(object sender, EventArgs e)
        {
            UpdateEnabledControls();
            UpdateBrushImage();
        }

        private void ChkbxColorizeBrush_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.ColorizeBrushTip);
        }

        private void ChkbxColorInfluenceHue_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.ColorInfluenceHTip);
        }

        private void ChkbxColorInfluenceSat_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.ColorInfluenceSTip);
        }

        private void ChkbxColorInfluenceVal_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.ColorInfluenceVTip);
        }

        private void ChkbxDitherDraw_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.DitherDrawTip);
        }

        private void ChkbxLockAlpha_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.LockAlphaTip);
        }

        private void ChkbxLockR_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.LockRTip);
        }

        private void ChkbxLockG_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.LockGTip);
        }

        private void ChkbxLockB_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.LockBTip);
        }

        private void ChkbxLockHue_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.LockHueTip);
        }

        private void ChkbxLockSat_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.LockSatTip);
        }

        private void ChkbxLockVal_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.LockValTip);
        }

        private void ChkbxOrientToMouse_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.OrientToMouseTip);
        }

        private void ChkbxSeamlessDrawing_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.SeamlessDrawingTip);
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

        private void CmbxTabPressureBrushSize_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Included in brush size calculation in this function, so needs to be recalculated.
            UpdateBrushImage();
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
                string tooltipText = name + Environment.NewLine + brushImage.BrushWidth + "x" + brushImage.BrushHeight + Environment.NewLine;

                tooltipText += !string.IsNullOrEmpty(brushImage.Location)
                    ? brushImage.Location
                    : Strings.BuiltIn;

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
                    tooltipText = name + Environment.NewLine + brush.BrushWidth + 'x' + brush.BrushHeight + Environment.NewLine + brush.Location;
                }
                else
                {
                    tooltipText = name + Environment.NewLine + brush.BrushWidth + 'x' + brush.BrushHeight + Environment.NewLine + Strings.BuiltIn;
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
                    bmpBrushDownsized = null;

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
                    currentBrushPath = selection.Text;

                    if (PersistentSettings.defaultBrushes.ContainsKey(selection.Text))
                    {
                        UpdateBrush(PersistentSettings.defaultBrushes[selection.Text]);
                    }
                    else
                    {
                        UpdateBrush(settings.CustomBrushes[selection.Text]);
                    }
                }

                // Updates which brush image is active for the newly-selected brush
                if (listviewBrushImagePicker?.Items != null && selection?.Text != null)
                {
                    int index = -1;

                    if (!string.IsNullOrEmpty(currentBrushPath))
                    {
                        index = loadedBrushImages.FindIndex((entry) =>
                        {
                            if (PersistentSettings.defaultBrushes.ContainsKey(currentBrushPath))
                            {
                                return entry.Location != null && entry.Location.Equals(PersistentSettings.defaultBrushes[currentBrushPath].BrushImagePath);
                            }

                            if (settings.CustomBrushes.ContainsKey(currentBrushPath))
                            {
                                return entry.Location != null && entry.Location.Equals(settings.CustomBrushes[currentBrushPath].BrushImagePath);
                            }

                            return false;
                        });

                        if (index == -1)
                        {
                            index = loadedBrushImages.FindIndex((entry) =>
                            {
                                if (PersistentSettings.defaultBrushes.ContainsKey(currentBrushPath))
                                {
                                    return entry.Name.Equals(PersistentSettings.defaultBrushes[currentBrushPath].BrushImagePath);
                                }

                                if (settings.CustomBrushes.ContainsKey(currentBrushPath))
                                {
                                    return entry.Name.Equals(settings.CustomBrushes[currentBrushPath].BrushImagePath);
                                }

                                return false;
                            });
                        }
                    }
                    else
                    {
                        index = loadedBrushImages.FindIndex((entry) => entry.Name.Equals(Strings.DefaultBrushCircle));
                    }

                    if (index >= 0)
                    {
                        listviewBrushImagePicker.Items[index].Selected = true;
                        ListViewBrushImagePicker_SelectedIndexChanged(null, null);
                    }
                }
            }
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

        private void SliderBrushFlow_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.BrushFlowTip);
        }

        private void SliderBrushFlow_ValueChanged(object sender, EventArgs e)
        {
            txtBrushFlow.Text = String.Format("{0} {1}",
                Strings.BrushFlow,
                sliderBrushFlow.Value);

            UpdateBrushImage();
        }

        private void SliderBrushOpacity_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.BrushOpacityTip);
        }

        private void SliderBrushOpacity_ValueChanged(object sender, EventArgs e)
        {
            txtBrushOpacity.Text = String.Format("{0} {1}",
                Strings.BrushOpacity,
                sliderBrushOpacity.Value);

            UpdateBrushImage();
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
            UpdateBrushImage();
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

            // Refreshes the brush indicator (ignore if the indicator wouldn't be shown).
            if (!isUserDrawing.started)
            {
                displayCanvas.Refresh();
            }
        }

        private void SliderCanvasZoom_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.CanvasZoomTip);
        }

        private void SliderCanvasZoom_ValueChanged(object sender, EventArgs e)
        {
            Zoom(0, false);
        }

        private void SliderCanvasAngle_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.CanvasAngleTip);
        }

        private void SliderCanvasAngle_ValueChanged(object sender, EventArgs e)
        {
            txtCanvasAngle.Text = String.Format("{0} {1}°",
                Strings.CanvasAngle,
                sliderCanvasAngle.Value);

            displayCanvas.Refresh();
        }

        /// <summary>
        /// Resets the brush to reconfigure colorization. Colorization is
        /// applied when the brush is refreshed.
        /// </summary>
        private void SliderColorInfluence_ValueChanged(object sender, EventArgs e)
        {
            txtColorInfluence.Text = String.Format("{0} {1}%",
                Strings.ColorInfluence,
                sliderColorInfluence.Value);

            UpdateEnabledControls();
            UpdateBrushImage();
        }

        private void SliderColorInfluence_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.ColorInfluenceTip);
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

        private void SliderRandFlowLoss_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.RandFlowLossTip);
        }

        private void SliderRandFlowLoss_ValueChanged(object sender, EventArgs e)
        {
            txtRandFlowLoss.Text = String.Format("{0} {1}",
                Strings.RandFlowLoss,
                sliderRandFlowLoss.Value);
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

        private void SliderShiftFlow_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.ShiftFlowTip);
        }

        private void SliderShiftFlow_ValueChanged(object sender, EventArgs e)
        {
            txtShiftFlow.Text = String.Format("{0} {1}",
                Strings.ShiftFlow,
                sliderShiftFlow.Value);
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

        private void SpinTabPressureBrushSize_LostFocus(object sender, EventArgs e)
        {
            // Included in brush size calculation in this function when on.
            if (((CmbxTabletValueType.CmbxEntry)cmbxTabPressureBrushSize.SelectedItem).ValueMember
                != ConstraintValueHandlingMethod.DoNothing)
            {
                UpdateBrushImage();
            }
        }
        #endregion
    }
}