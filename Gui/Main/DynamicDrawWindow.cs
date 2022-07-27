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
        /// Contains the list of all blend mode options for brush strokes.
        /// </summary>
        readonly BindingList<Tuple<string, BlendMode>> blendModeOptions;

        /// <summary>
        /// If <see cref="BackgroundDisplayMode.ClipboardFit"/> or
        /// <see cref="BackgroundDisplayMode.ClipboardOnlyIfFits"/> is used, this will contain the image that was
        /// copied to the clipboard.
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
        /// The last directory that brushes were imported from via the add brushes button, by the user during this run
        /// of the plugin.
        /// </summary>
        private string importBrushesLastDirectory = null;

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
        /// Indicates whether the plugin has finished loading basic elements. This includes the form component init,
        /// the token initialization by paint.net, the canvas/bitmap elements and the default brush being set, as well
        /// as the chosen effect being applied (if any was active from before). It does not guarantee that all brushes
        /// have finished loading, both default and custom ones.
        /// </summary>
        private bool pluginHasLoaded = false;

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
        /// List of keys currently pressed. The last item in the list is always the most recently pressed key.
        /// </summary>
        private readonly HashSet<Keys> currentKeysPressed;

        /// <summary>
        /// The file path identifying the currently loaded brush, or name for built-in brushes, or null if no saved brush is currently active.
        /// </summary>
        private string currentBrushPath = null;

        /// <summary>
        /// If set and using the brush tool (not eraser), the effect and its settings will be
        /// applied to the staged bmp when first starting to draw, and copied to committed bmp as
        /// the user draws. <see cref="settings"/> is used for non-property based effects, while
        /// <see cref="propertySettings"/> is used for property-based effects.
        /// </summary>
        private CustomEffect effectToDraw = new CustomEffect();

        /// <summary>
        /// Contains the list of all available effects to use while drawing.
        /// </summary>
        private readonly BindingList<Tuple<string, IEffectInfo>> effectOptions;

        /// <summary>
        /// True when the user sets an effect like blur, and is currently modifying the settings.
        /// </summary>
        private (bool settingsOpen, bool hoverPreview) isPreviewingEffect = (false, false);

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
        private SettingsSerialization settings;

        /// <summary>
        /// Settings deserialize asynchronously (and can fail sometimes). This returns that object if it exists, else
        /// it returns a copy of the program defaults (so any changes are discarded).
        /// </summary>
        private UserSettings UserSettings
        {
            get
            {
                return settings?.Preferences ?? new UserSettings();
            }
            set
            {
                if (settings?.Preferences != null)
                {
                    settings.Preferences = value;
                }
            }
        }

        /// <summary>
        /// Shortcuts deserialize asynchronously (and can fail sometimes). This returns that object if it exists, else
        /// it returns a copy of the shortcut defaults (so any changes are discarded).
        /// </summary>
        private HashSet<KeyboardShortcut> KeyboardShortcuts
        {
            get
            {
                return settings?.KeyboardShortcuts ?? new PersistentSettings().KeyboardShortcuts;
            }
            set
            {
                if (settings?.KeyboardShortcuts != null)
                {
                    settings.KeyboardShortcuts = value;
                }
            }
        }

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

        private FlowLayoutPanel topMenu;
        private Button menuOptions;
        private ToolStripMenuItem menuResetCanvas, menuSetCanvasBackground, menuDisplaySettings;
        private ToolStripMenuItem menuSetCanvasBgImage, menuSetCanvasBgImageFit, menuSetCanvasBgImageOnlyIfFits;
        private ToolStripMenuItem menuSetCanvasBgTransparent, menuSetCanvasBgGray, menuSetCanvasBgWhite, menuSetCanvasBgBlack;
        private ToolStripMenuItem menuBrushIndicator, menuBrushIndicatorSquare, menuBrushIndicatorPreview;
        private ToolStripMenuItem menuShowSymmetryLinesInUse, menuShowMinDistanceInUse;
        private ToolStripMenuItem menuBrushImageDirectories, menuKeyboardShortcutsDialog;
        private ToolStripMenuItem menuColorPickerIncludesAlpha, menuColorPickerSwitchesToPrevTool;
        private ToolStripMenuItem menuRemoveUnfoundImagePaths, menuConfirmCloseSave;
        private Button menuCanvasZoomBttn, menuCanvasAngleBttn;
        private Slider sliderCanvasZoom, sliderCanvasAngle;
        private ToolStripMenuItem menuCanvasZoomReset, menuCanvasZoomFit, menuCanvasZoomTo;
        private ToolStripMenuItem menuCanvasAngleReset, menuCanvasAngle90, menuCanvasAngle180, menuCanvasAngle270, menuCanvasAngleTo;
        private FlowLayoutPanel panelTools;

        /// <summary>
        /// Tracks when the user draws out-of-bounds and moves the canvas to
        /// accomodate them.
        /// </summary>
        private Timer timerRepositionUpdate;

        /// <summary>
        /// Periodically checks if the user has PNG data on the clipboard and updates the bitmap copy used by this
        /// plugin when it changes, if the user is set to use the clipboard image when available.
        /// </summary>
        private Timer timerClipboardDataCheck;

        private FlowLayoutPanel panelUndoRedoOkCancel;
        private Button bttnColorPicker;
        private Panel panelAllSettingsContainer;
        private Panel panelDockSettingsContainer;
        private Button bttnToolBrush;
        private Button bttnToolOrigin;
        private Button bttnToolEraser;
        private FlowLayoutPanel panelSettingsContainer;
        private Accordion bttnBrushControls;
        private FlowLayoutPanel panelBrush;
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
        private Button bttnUpdateCurrentBrush;
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

            // The temp directory is used to store undo/redo images.
            TempDirectory.CleanupPreviousDirectories();
            tempDir = new TempDirectory();

            loadedBrushImages = new BrushSelectorItemCollection();
            KeyboardShortcuts = new HashSet<KeyboardShortcut>();
            currentKeysPressed = new HashSet<Keys>();

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
                new Tuple<string, IEffectInfo>(Strings.EffectDefaultNone, null)
            };

            cmbxChosenEffect.DisplayMember = "Item1";
            cmbxChosenEffect.ValueMember = "Item2";
            cmbxChosenEffect.DataSource = effectOptions;

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
            cmbxTabPressureBlueJitter.MouseWheel += IgnoreMouseWheelEvent;
            cmbxTabPressureBrushDensity.MouseWheel += IgnoreMouseWheelEvent;
            cmbxTabPressureBrushFlow.MouseWheel += IgnoreMouseWheelEvent;
            cmbxTabPressureBrushOpacity.MouseWheel += IgnoreMouseWheelEvent;
            cmbxTabPressureBrushRotation.MouseWheel += IgnoreMouseWheelEvent;
            cmbxTabPressureBrushSize.MouseWheel += IgnoreMouseWheelEvent;
            cmbxTabPressureGreenJitter.MouseWheel += IgnoreMouseWheelEvent;
            cmbxTabPressureHueJitter.MouseWheel += IgnoreMouseWheelEvent;
            cmbxTabPressureMinDrawDistance.MouseWheel += IgnoreMouseWheelEvent;
            cmbxTabPressureRandFlowLoss.MouseWheel += IgnoreMouseWheelEvent;
            cmbxTabPressureRandHorShift.MouseWheel += IgnoreMouseWheelEvent;
            cmbxTabPressureRandMaxSize.MouseWheel += IgnoreMouseWheelEvent;
            cmbxTabPressureRandMinSize.MouseWheel += IgnoreMouseWheelEvent;
            cmbxTabPressureRandRotLeft.MouseWheel += IgnoreMouseWheelEvent;
            cmbxTabPressureRandRotRight.MouseWheel += IgnoreMouseWheelEvent;
            cmbxTabPressureRandVerShift.MouseWheel += IgnoreMouseWheelEvent;
            cmbxTabPressureRedJitter.MouseWheel += IgnoreMouseWheelEvent;
            cmbxTabPressureSatJitter.MouseWheel += IgnoreMouseWheelEvent;
            cmbxTabPressureValueJitter.MouseWheel += IgnoreMouseWheelEvent;
            cmbxBlendMode.MouseWheel += IgnoreMouseWheelEvent;
            cmbxChosenEffect.MouseWheel += IgnoreMouseWheelEvent;
            sliderColorInfluence.MouseWheel += IgnoreMouseWheelEvent;
            sliderBrushFlow.MouseWheel += IgnoreMouseWheelEvent;
            sliderBrushOpacity.MouseWheel += IgnoreMouseWheelEvent;
            sliderBrushRotation.MouseWheel += IgnoreMouseWheelEvent;
            sliderBrushSize.MouseWheel += IgnoreMouseWheelEvent;
            sliderMinDrawDistance.MouseWheel += IgnoreMouseWheelEvent;
            sliderBrushDensity.MouseWheel += IgnoreMouseWheelEvent;
            cmbxSymmetry.MouseWheel += IgnoreMouseWheelEvent;
            cmbxBrushSmoothing.MouseWheel += IgnoreMouseWheelEvent;
            sliderRandMinSize.MouseWheel += IgnoreMouseWheelEvent;
            sliderRandMaxSize.MouseWheel += IgnoreMouseWheelEvent;
            sliderRandRotLeft.MouseWheel += IgnoreMouseWheelEvent;
            sliderRandRotRight.MouseWheel += IgnoreMouseWheelEvent;
            sliderRandFlowLoss.MouseWheel += IgnoreMouseWheelEvent;
            sliderRandHorzShift.MouseWheel += IgnoreMouseWheelEvent;
            sliderRandVertShift.MouseWheel += IgnoreMouseWheelEvent;
            sliderJitterMinRed.MouseWheel += IgnoreMouseWheelEvent;
            sliderJitterMaxRed.MouseWheel += IgnoreMouseWheelEvent;
            sliderJitterMinGreen.MouseWheel += IgnoreMouseWheelEvent;
            sliderJitterMaxGreen.MouseWheel += IgnoreMouseWheelEvent;
            sliderJitterMinBlue.MouseWheel += IgnoreMouseWheelEvent;
            sliderJitterMaxBlue.MouseWheel += IgnoreMouseWheelEvent;
            sliderJitterMinHue.MouseWheel += IgnoreMouseWheelEvent;
            sliderJitterMaxHue.MouseWheel += IgnoreMouseWheelEvent;
            sliderJitterMinSat.MouseWheel += IgnoreMouseWheelEvent;
            sliderJitterMaxSat.MouseWheel += IgnoreMouseWheelEvent;
            sliderJitterMinVal.MouseWheel += IgnoreMouseWheelEvent;
            sliderJitterMaxVal.MouseWheel += IgnoreMouseWheelEvent;
            sliderShiftSize.MouseWheel += IgnoreMouseWheelEvent;
            sliderShiftRotation.MouseWheel += IgnoreMouseWheelEvent;
            sliderShiftFlow.MouseWheel += IgnoreMouseWheelEvent;
            spinTabPressureBrushOpacity.MouseWheel += IgnoreMouseWheelEvent;
            spinTabPressureBrushFlow.MouseWheel += IgnoreMouseWheelEvent;
            spinTabPressureBrushDensity.MouseWheel += IgnoreMouseWheelEvent;
            spinTabPressureBrushRotation.MouseWheel += IgnoreMouseWheelEvent;
            spinTabPressureBrushSize.MouseWheel += IgnoreMouseWheelEvent;
            spinTabPressureMaxBlueJitter.MouseWheel += IgnoreMouseWheelEvent;
            spinTabPressureMaxGreenJitter.MouseWheel += IgnoreMouseWheelEvent;
            spinTabPressureMaxHueJitter.MouseWheel += IgnoreMouseWheelEvent;
            spinTabPressureMaxRedJitter.MouseWheel += IgnoreMouseWheelEvent;
            spinTabPressureMaxSatJitter.MouseWheel += IgnoreMouseWheelEvent;
            spinTabPressureMaxValueJitter.MouseWheel += IgnoreMouseWheelEvent;
            spinTabPressureMinBlueJitter.MouseWheel += IgnoreMouseWheelEvent;
            spinTabPressureMinDrawDistance.MouseWheel += IgnoreMouseWheelEvent;
            spinTabPressureMinGreenJitter.MouseWheel += IgnoreMouseWheelEvent;
            spinTabPressureMinHueJitter.MouseWheel += IgnoreMouseWheelEvent;
            spinTabPressureMinRedJitter.MouseWheel += IgnoreMouseWheelEvent;
            spinTabPressureMinSatJitter.MouseWheel += IgnoreMouseWheelEvent;
            spinTabPressureMinValueJitter.MouseWheel += IgnoreMouseWheelEvent;
            spinTabPressureRandHorShift.MouseWheel += IgnoreMouseWheelEvent;
            spinTabPressureRandMaxSize.MouseWheel += IgnoreMouseWheelEvent;
            spinTabPressureRandFlowLoss.MouseWheel += IgnoreMouseWheelEvent;
            spinTabPressureRandMinSize.MouseWheel += IgnoreMouseWheelEvent;
            spinTabPressureRandRotLeft.MouseWheel += IgnoreMouseWheelEvent;
            spinTabPressureRandRotRight.MouseWheel += IgnoreMouseWheelEvent;
            spinTabPressureRandVerShift.MouseWheel += IgnoreMouseWheelEvent;

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

                timerRepositionUpdate?.Dispose();
                timerClipboardDataCheck?.Dispose();

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
                effectToDraw.Effect?.Dispose();

                tabletService?.Dispose();
            }

            base.Dispose(disposing);
        }

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
            // Copies GUI values from the settings.
            PersistentSettings token = (PersistentSettings)effectToken;

            // Loads custom brush images if possible, but skips duplicates. This method is called twice by Paint.NET,
            // so this ensures there are no duplicates.
            if (token.CustomBrushLocations.Count > 0 && !token.CustomBrushLocations.SetEquals(loadedBrushImagePaths))
            {
                loadedBrushImagePaths.UnionWith(token.CustomBrushLocations);
            }

            // Registers all current keyboard shortcuts.
            InitKeyboardShortcuts(token.KeyboardShortcuts);

            token.CurrentBrushSettings.BrushFlow = PdnUserSettings.userPrimaryColor.A;
            token.CurrentBrushSettings.BrushColor = PdnUserSettings.userPrimaryColor;

            // Fetches and populates the available effects for the effect chooser combobox.
            if (effectOptions.Count == 1)
            {
                IEffectsService effectsService = Services?.GetService<IEffectsService>();

                if (effectsService != null)
                {
                    // Sorts effects by built-in status, then name.
                    var effectInfos = effectsService?.EffectInfos
                        .OrderByDescending((effect) => effect.IsBuiltIn)
                        .ThenBy((effect) => effect.Name);

                    foreach (IEffectInfo effectInfo in effectInfos)
                    {
                        // No reason to use this plugin inside itself and reusing shared state causes it to crash.
                        if (effectInfo.Name == EffectPlugin.StaticName)
                        {
                            continue;
                        }

                        // Blacklists known problematic effects.
                        if (KnownEffectCompatibilities.KnownCustomEffects.ContainsKey(effectInfo.Name) &&
                            effectInfo.AssemblyLocation.EndsWith(KnownEffectCompatibilities.KnownCustomEffects[effectInfo.Name].effectAssembly))
                        {
                            continue;
                        }

                        if (effectInfo.Category != EffectCategory.DoNotDisplay)
                        {
                            effectOptions.Add(new Tuple<string, IEffectInfo>(effectInfo.Name, effectInfo));
                        }
                    }
                }
            }

            cmbxChosenEffect.SelectedIndex = token.ActiveEffect.index;
            effectToDraw = token.ActiveEffect.effect;

            UpdateBrush(token.CurrentBrushSettings);
            UpdateBrushImage();
        }

        /// <summary>
        /// Overwrites the settings with the dialog's current settings so they
        /// can be reused later; i.e. this saves the settings.
        /// </summary>
        protected override void InitTokenFromDialog()
        {
            PersistentSettings token = (PersistentSettings)EffectToken;
            token.UserSettings = UserSettings;
            token.KeyboardShortcuts = KeyboardShortcuts;
            token.CustomBrushLocations = loadedBrushImagePaths;
            token.CurrentBrushSettings = CreateSettingsObjectFromCurrentSettings(true);
            token.ActiveEffect = new(cmbxChosenEffect.SelectedIndex, new CustomEffect(effectToDraw, false));
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
                        MessageBox.Show(Strings.CannotSaveSettingsError,
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

            // Applies the effect chosen to be used if the user set one the last time they used the plugin.
            if (effectToDraw?.Effect != null)
            {
                ActiveEffectPrepareAndPreview(effectToDraw, false);
            }

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
            Text = string.Format(Strings.VersionString, EffectPlugin.StaticName, version.Major, version.Minor);

            //Loads globalization texts for regional support.
            txtBrushOpacity.Text = string.Format("{0} {1}",
                Strings.BrushOpacity, sliderBrushOpacity.Value);

            if ((BlendMode)cmbxBlendMode.SelectedIndex == BlendMode.Overwrite &&
                activeTool == Tool.Brush && effectToDraw.Effect == null)
            {
                txtBrushFlow.Text = String.Format("{0} {1}",
                    Strings.BrushFlowAlpha,
                    sliderBrushFlow.Value);
            }
            else
            {
                txtBrushFlow.Text = String.Format("{0} {1}",
                    Strings.BrushFlow,
                    sliderBrushFlow.Value);
            }

            txtBrushDensity.Text = string.Format("{0} {1}",
                Strings.BrushDensity, sliderBrushDensity.Value);

            txtBrushRotation.Text = string.Format("{0} {1}°",
                Strings.Rotation, sliderBrushRotation.Value);

            txtBrushSize.Text = string.Format("{0} {1}",
                Strings.Size, sliderBrushSize.Value);

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
            bttnOk.Text = Strings.Ok;
            bttnUndo.Text = Strings.Undo;
            bttnUpdateCurrentBrush.Text = Strings.UpdateCurrentBrush;
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

            pluginHasLoaded = true;
        }

        /// <summary>
        /// Handles keypresses for global commands.
        /// </summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            currentKeysPressed.Add(e.KeyCode & Keys.KeyCode);

            base.OnKeyDown(e);

            // Fires any shortcuts that don't require the mouse wheel.
            HashSet<ShortcutContext> contexts = new HashSet<ShortcutContext>();
            if (displayCanvas.Focused) { contexts.Add(ShortcutContext.OnCanvas); }
            else { contexts.Add(ShortcutContext.OnSidebar); }

            KeyShortcutManager.FireShortcuts(KeyboardShortcuts, currentKeysPressed, false, false, contexts);

            // Display a hand icon while panning.
            if (e.KeyCode == Keys.Control || e.KeyCode == Keys.Space)
            {
                Cursor = Cursors.Hand;
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
            currentKeysPressed.Remove(e.KeyCode & Keys.KeyCode);

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

            // Fires any shortcuts that require the mouse wheel.
            HashSet<ShortcutContext> contexts = new HashSet<ShortcutContext>();
            if (displayCanvas.Focused) { contexts.Add(ShortcutContext.OnCanvas); }
            else { contexts.Add(ShortcutContext.OnSidebar); }

            bool wheelDirectionUp = Math.Sign(e.Delta) > 0;

            KeyShortcutManager.FireShortcuts(
                KeyboardShortcuts,
                currentKeysPressed,
                wheelDirectionUp,
                !wheelDirectionUp,
                contexts);
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
        /// Sets the form resize restrictions.
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            try
            {
                MigrateLegacySettings();

                // Loading the settings is split into a separate method to allow the defaults
                // to be used if an error occurs.
                settings.LoadSavedSettings();
                InitKeyboardShortcuts(KeyboardShortcuts);
                UpdateTopMenuState();

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
        #endregion

        #region Methods (not event handlers)
        /// <summary>
        /// (Re)registers the keyboard shortcuts for the app.
        /// </summary>
        private void InitKeyboardShortcuts(HashSet<KeyboardShortcut> shortcutsToApply)
        {
            KeyboardShortcuts = new HashSet<KeyboardShortcut>(); // Prevents duplicate shortcut handling.

            foreach (KeyboardShortcut shortcut in shortcutsToApply)
            {
                shortcut.OnInvoke = new Action(() =>
                {
                    HandleShortcut(shortcut);
                });

                KeyboardShortcuts.Add(shortcut);
            }
        }

        /// <summary>
        /// Renders a chosen effect, returning whether an error occurred or not. The effect is rendered on the
        /// bmpStaged bitmap. See <see cref="ActiveEffectPrepareAndPreview"/> for context.
        /// </summary>
        /// <param name="changedToken">
        /// Uses the given token for effect config settings instead of reading the active effect. This allows the live
        /// preview dialog shown by <see cref="ActiveEffectPrepareAndPreview"/> to render without changing the active
        /// effect settings, in case the user cancels.
        /// </param>
        private bool ActiveEffectRender(EffectConfigToken changedToken = null)
        {
            // Clears the staging surface if the effect fails and continues to call render.
            if (effectToDraw.Effect == null)
            {
                Utils.ColorImage(bmpStaged, ColorBgra.Black, 0f);
                return false;
            }

            // Sets some surfaces & ensures the source surface is exact.
            if (effectToDraw.SrcArgs == null)
            {
                Surface fromStagedSurf = Surface.CopyFromBitmap(bmpCommitted);
                Surface fromCommittedSurf = Surface.CopyFromBitmap(bmpCommitted);
                RenderArgs fromStaged = new RenderArgs(fromStagedSurf);
                RenderArgs fromCommitted = new RenderArgs(fromCommittedSurf);
                effectToDraw.SrcArgs = fromCommitted;
                effectToDraw.DstArgs = fromStaged;
            }
            else
            {
                effectToDraw.DstArgs.Surface.CopyFromGdipBitmap(bmpCommitted);
                effectToDraw.SrcArgs.Surface.CopyFromGdipBitmap(bmpCommitted);
            }

            var envParams = effectToDraw.Effect.EnvironmentParameters;
            effectToDraw.Effect.EnvironmentParameters = effectToDraw.Effect.EnvironmentParameters
                .CloneWithDifferentSourceSurface(effectToDraw.SrcArgs.Surface);
            envParams?.Dispose();

            // Runs the effect.
            try
            {
                effectToDraw.Effect.SetRenderInfo(changedToken ?? effectToDraw.Settings, effectToDraw.DstArgs, effectToDraw.SrcArgs);
                effectToDraw.Effect.Render(
                    changedToken ?? effectToDraw.Settings,
                    effectToDraw.DstArgs,
                    effectToDraw.SrcArgs,
                    Utils.GetRois(bmpCommitted.Width, bmpCommitted.Height));
            }
            catch
            {
                MessageBox.Show(Strings.EffectFailedToWorkError);
                cmbxChosenEffect.SelectedIndex = 0; // corresponds to no effect chosen.
                isPreviewingEffect = (false, false);
                return false;
            }

            Utils.OverwriteBits(effectToDraw.DstArgs.Bitmap, bmpStaged);
            return true;
        }

        /// <summary>
        /// It's possible for a user to apply an effect on the bmpStaged surface and draw it using a brush stroke,
        /// similar to how erasing to the original surface works. This instantiates and sets up the metadata for a
        /// chosen effect, then creates the dialog for it (if the effect has configuration options), maintaining a live
        /// preview until the user applied or cancels.
        /// 
        /// Effect settings are persisted for when the user wants to change the effect config, or when the user reopens
        /// the plugin if it had an effect active last time they used it. In both cases, the effect info is passed. In
        /// the first case, the user invokes the dialog (so previewRestoredEffect is true) while in the latter case,
        /// it's a poor experience to show the dialog as soon as the plugin starts, so it's hidden
        /// (previewRestoredEffect is false).
        /// </summary>
        /// <param name="restoreEffect">
        /// When passed in, the settings will be copied to the new effect instance so it has the same config options.
        /// </param>
        /// <param name="previewRestoredEffect">
        /// When passing in effect info, this determines whether a live preview dialog will be shown.
        /// </param>
        private void ActiveEffectPrepareAndPreview(CustomEffect restoreEffect = null, bool previewRestoredEffect = false)
        {
            // Effect options haven't populated yet, so the effect can't be prepared.
            if (cmbxChosenEffect.SelectedIndex >= effectOptions.Count)
            {
                return;
            }

            if (effectOptions[cmbxChosenEffect.SelectedIndex] == null)
            {
                return;
            }

            // Clears the active effect if it lacks effectInfo, e.g. the special "no effect selected" option.
            if (effectOptions[cmbxChosenEffect.SelectedIndex].Item2 == null)
            {
                if (effectToDraw != restoreEffect)
                {
                    effectToDraw?.Dispose();
                }
                effectToDraw = new CustomEffect();
                bttnChooseEffectSettings.Enabled = false;
                UpdateEnabledControls();
                return;
            }

            // Instantiates the effect and prepares all metadata for it.
            var effectInfo = effectOptions[cmbxChosenEffect.SelectedIndex].Item2;
            effectToDraw.Effect = effectInfo.CreateInstance();
            effectToDraw.SrcArgs = null;
            effectToDraw.DstArgs = null;

            if (restoreEffect != null)
            {
                effectToDraw.Settings = restoreEffect.Settings;
                effectToDraw.PropertySettings = restoreEffect.PropertySettings;
            }

            if (effectToDraw.Effect == null)
            {
                MessageBox.Show(Strings.EffectFailedToWorkError);
                UpdateEnabledControls();
                return;
            }

            bool isEffectPropertyBased = effectToDraw.Effect is PropertyBasedEffect;
            bool isEffectConfigurable = effectToDraw.Effect.Options.Flags.HasFlag(EffectFlags.Configurable);

            bttnChooseEffectSettings.Enabled = isEffectConfigurable;
            UpdateEnabledControls();

            effectToDraw.Effect.Services = Services;
            effectToDraw.Effect.EnvironmentParameters = new EffectEnvironmentParameters(
                bttnBrushColor.BackColor,
                Color.Black,
                sliderBrushSize.Value,
                EnvironmentParameters.DocumentResolution,
                new PdnRegion(EnvironmentParameters.GetSelectionAsPdnRegion().GetRegionData()),
                Surface.CopyFromBitmap(bmpCommitted));

            // Applies the restored effect immediately under current settings without preview ing.
            if (restoreEffect != null && !previewRestoredEffect)
            {
                ActiveEffectRender();
                displayCanvas.Refresh();
                return;
            }

            if (restoreEffect == null)
            {
                effectToDraw.PropertySettings = isEffectPropertyBased
                    ? ((PropertyBasedEffect)effectToDraw.Effect).CreatePropertyCollection()
                    : null;
            }

            // For effects with no dialog, apply the effect immediately.
            if (!isEffectConfigurable)
            {
                effectToDraw.Settings = null;
                ActiveEffectRender();
                displayCanvas.Refresh();
                return;
            }

            // Opens the effect dialog, updating a live preview of it every 200ms, and applies when done.
            try
            {
                using Timer repaintTimer = new Timer() { Interval = 150, Enabled = false };
                using EffectConfigDialog dialog = effectToDraw.Effect.CreateConfigDialog();

                if (dialog == null)
                {
                    MessageBox.Show(Strings.EffectFailedToWorkError);
                    return;
                }

                dialog.Effect = effectToDraw.Effect;

                if (isEffectPropertyBased)
                {
                    dialog.EffectToken = new PropertyBasedEffectConfigToken(effectToDraw.PropertySettings);
                }

                effectToDraw.Settings = dialog.EffectToken;

                // Reset/start a short delay before refreshing the effect preview.
                // Delays until a short duration passes without changing the UI.
                dialog.EffectTokenChanged += (a, b) =>
                {
                    repaintTimer.Stop();
                    repaintTimer.Start();
                };

                // Refresh the effect preview after a short delay.
                repaintTimer.Tick += (a, b) =>
                {
                    repaintTimer.Stop();

                    bool effectSuccessfullyExecuted = ActiveEffectRender(dialog.EffectToken);
                    if (!effectSuccessfullyExecuted)
                    {
                        MessageBox.Show(Strings.EffectFailedToWorkError);
                        dialog.Close();
                    }

                    displayCanvas.Refresh();
                };

                isPreviewingEffect.settingsOpen = true;

                dialog.Owner = this;
                var dlgResult = dialog.ShowDialog();

                if (repaintTimer.Enabled)
                {
                    repaintTimer.Stop();
                    bool effectSuccessfullyExecuted = ActiveEffectRender(dialog.EffectToken);
                    if (!effectSuccessfullyExecuted)
                    {
                        MessageBox.Show(Strings.EffectFailedToWorkError);
                        dialog.Close();
                    }
                }

                if (isEffectPropertyBased)
                {
                    effectToDraw.PropertySettings = (dialog.EffectToken as PropertyBasedEffectConfigToken)?.Properties;
                }

                effectToDraw.Settings = dialog.EffectToken;

                isPreviewingEffect = (false, false);
                displayCanvas.Refresh();
            }
            catch
            {
                MessageBox.Show(Strings.EffectFailedToWorkError);
            }
        }

        /// <summary>
        /// Returns a new brush settings object using all current settings values.
        /// </summary>
        private BrushSettings CreateSettingsObjectFromCurrentSettings(bool fallbackToCircleBrushPath = false)
        {
            int index = listviewBrushImagePicker.SelectedIndices.Count > 0
                ? listviewBrushImagePicker.SelectedIndices[0]
                : -1;

            BrushSettings newSettings = new BrushSettings()
            {
                AutomaticBrushDensity = chkbxAutomaticBrushDensity.Checked,
                BlendMode = (BlendMode)cmbxBlendMode.SelectedIndex,
                BrushColor = bttnBrushColor.BackColor,
                BrushDensity = sliderBrushDensity.Value,
                BrushFlow = sliderBrushFlow.Value,
                BrushImagePath = index >= 0
                    ? loadedBrushImages[index].Location ?? loadedBrushImages[index].Name
                    : fallbackToCircleBrushPath ? Strings.DefaultBrushCircle : string.Empty,
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

            return newSettings;
        }

        /// <summary>
        /// Applies the brush to the drawing region at the given location
        /// with the given radius. The brush is assumed square.
        /// </summary>
        /// <param name="loc">The location to apply the brush.</param>
        /// <param name="radius">The size to draw the brush at.</param>
        private void DrawBrush(PointF loc, int radius, float pressure)
        {
            if (!pluginHasLoaded || bmpBrushEffects == null)
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

            if (activeTool == Tool.Eraser || effectToDraw.Effect != null)
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
                    float newAlpha = (activeTool == Tool.Eraser || effectToDraw.Effect != null || cmbxBlendMode.SelectedIndex != (int)BlendMode.Overwrite)
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
            bool drawToStagedBitmap = activeTool != Tool.Eraser
                && effectToDraw.Effect == null
                && ((BlendMode)cmbxBlendMode.SelectedIndex != BlendMode.Overwrite)
                && (isUserDrawing.stagedChanged
                    || BlendModeUtils.BlendModeToUserBlendOp((BlendMode)cmbxBlendMode.SelectedIndex) != null
                    || finalOpacity != sliderBrushOpacity.Maximum);

            if (drawToStagedBitmap && !isUserDrawing.stagedChanged)
            {
                isUserDrawing.stagedChanged = true;
                Utils.OverwriteBits(bmpCommitted, bmpMerged);
            }

            Bitmap bmpToDrawOn = drawToStagedBitmap ? bmpStaged : bmpCommitted;

            // Erasing uses a surface while drawing with an effect uses a bitmap, but both use the same function.
            (Surface surface, Bitmap bmp) eraserAndEffectSrc = (null, null);
            if (activeTool == Tool.Eraser)
            {
                eraserAndEffectSrc.surface = EnvironmentParameters.SourceSurface;
            }
            else if (effectToDraw.Effect != null)
            {
                eraserAndEffectSrc.bmp = bmpStaged;
            }

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
                    || (CmbxSmoothing.Smoothing)cmbxBrushSmoothing.SelectedIndex == CmbxSmoothing.Smoothing.Jagged
                    || effectToDraw.Effect != null;

                // Adjust to center for pixel perfect drawing.
                if (useLockbitsDrawing)
                {
                    loc.X += halfPixelOffset;
                    loc.Y += halfPixelOffset;
                }

                // Overwrite blend mode and uncolorized images don't use the recolor matrix.
                if (activeTool != Tool.Eraser && effectToDraw.Effect == null)
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
                if (useLockbitsDrawing && sliderCanvasAngle.ValueInt != 0)
                {
                    rotation -= sliderCanvasAngle.ValueInt;
                }

                Bitmap bmpBrushRot = bmpBrushEffects;
                bool isJagged = cmbxBrushSmoothing.SelectedIndex == (int)CmbxSmoothing.Smoothing.Jagged;
                if (rotation != 0 || (isJagged && adjustedColor.A != 255))
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
                    g.RotateTransform(-sliderCanvasAngle.ValueInt);
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
                            if (sliderCanvasAngle.ValueInt != 0)
                            {
                                rotatedLoc = TransformPoint(loc, true, true, false);
                            }

                            using (Bitmap bmpBrushRotScaled = Utils.ScaleImage(bmpBrushRot, new Size(scaleFactor, scaleFactor), false, false, recolorMatrix,
                                (CmbxSmoothing.Smoothing)cmbxBrushSmoothing.SelectedIndex))
                            {
                                if (activeTool == Tool.Eraser || effectToDraw.Effect != null)
                                {
                                    Utils.OverwriteMasked(
                                        eraserAndEffectSrc,
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
                                if (sliderCanvasAngle.ValueInt != 0)
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
                                if (activeTool == Tool.Eraser || effectToDraw.Effect != null)
                                {
                                    Utils.OverwriteMasked(
                                        eraserAndEffectSrc,
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

                                    if (sliderCanvasAngle.ValueInt != 0)
                                    {
                                        transformedPoint = TransformPoint(transformedPoint, true, true, false);
                                    }

                                    if (activeTool == Tool.Eraser || effectToDraw.Effect != null)
                                    {
                                        Utils.OverwriteMasked(
                                            eraserAndEffectSrc,
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
                                    if (activeTool == Tool.Eraser || effectToDraw.Effect != null)
                                    {
                                        Utils.OverwriteMasked(
                                            eraserAndEffectSrc,
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
        /// Returns a list of files in the given directories. Any invalid
        /// or non-directory path is ignored.
        /// </summary>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        private IReadOnlyCollection<string> FilesInDirectory(IEnumerable<string> localUris, BackgroundWorker backgroundWorker)
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
                    else if (UserSettings.RemoveBrushImagePathsWhenNotFound)
                    {
                        settings.CustomBrushImageDirectories.Remove(pathFromUser);
                    }
                }
                catch (Exception ex)
                {
                    // Remove gibberish entries (according to user preference).
                    if (ex is UriFormatException)
                    {
                        if (UserSettings.RemoveBrushImagePathsWhenNotFound)
                        {
                            settings.CustomBrushImageDirectories.Remove(pathFromUser);
                        }
                    }

                    // All these exceptions are run-of-the-mill issues with File I/O and can just be swallowed.
                    else if (!(ex is ArgumentException ||
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
                var pixel = bmpCommitted.GetPixel(
                    (int)Math.Round(rotatedLoc.X),
                    (int)Math.Round(rotatedLoc.Y));

                if (UserSettings.ColorPickerIncludesAlpha)
                {
                    sliderBrushOpacity.Value = pixel.A;
                }

                UpdateBrushColor(pixel);
            }
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
                    sliderCanvasZoom.ValueInt =
                        shortcut.GetDataAsInt(sliderCanvasZoom.ValueInt,
                        sliderCanvasZoom.MinimumInt, sliderCanvasZoom.MaximumInt);
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
                    sliderCanvasAngle.ValueInt = 0;
                    sliderCanvasZoom.ValueInt = 100;
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
                    var newValue = shortcut.GetDataAsInt(sliderCanvasAngle.ValueInt, int.MinValue, int.MaxValue);
                    while (newValue <= -180) { newValue += 360; }
                    while (newValue > 180) { newValue -= 360; }
                    sliderCanvasAngle.ValueInt = newValue;
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
                case ShortcutTarget.CanvasZoomToMouse:
                    isWheelZooming = true;
                    sliderCanvasZoom.ValueInt =
                        shortcut.GetDataAsInt(sliderCanvasZoom.ValueInt,
                        sliderCanvasZoom.MinimumInt, sliderCanvasZoom.MaximumInt);
                    break;
                case ShortcutTarget.CanvasZoomFit:
                    canvas.width = bmpCommitted.Width;
                    canvas.height = bmpCommitted.Height;
                    canvas.x = (displayCanvas.Width - canvas.width) / 2;
                    canvas.y = (displayCanvas.Height - canvas.height) / 2;

                    float newZoom = 100 * Math.Min(
                        displayCanvas.ClientSize.Width / (float)bmpCommitted.Width,
                        displayCanvas.ClientSize.Height / (float)bmpCommitted.Height);

                    int result = (int)Math.Clamp(newZoom, sliderCanvasZoom.MinimumInt, sliderCanvasZoom.MaximumInt);
                    sliderCanvasZoom.ValueInt = result > 1 ? result : 1;
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
            openFileDialog.InitialDirectory = importBrushesLastDirectory ?? defPath;
            openFileDialog.Multiselect = true;
            openFileDialog.Title = Strings.CustomBrushImagesDirectoryTitle;
            openFileDialog.Filter = Strings.CustomBrushImagesDirectoryFilter +
                "|*.png;*.bmp;*.jpg;*.gif;*.tif;*.exif*.jpeg;*.tiff;*.abr;";

            //Displays the dialog. Loads the files if it worked.
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                importBrushesLastDirectory = Path.GetDirectoryName(openFileDialog.FileName);
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
        /// Initializes all components. This used to be auto-generated, but is now editable as auto-generation tools
        /// don't work on this file anymore due to a history of temporary & long incompatibility in this repo.
        /// </summary>
        private void InitializeComponent()
        {
            components = new Container();
            ComponentResourceManager resources = new ComponentResourceManager(typeof(WinDynamicDraw));

            Font detailsFont = new Font("Microsoft Sans Serif", 8.25f);

            #region initialize every component at once
            timerRepositionUpdate = new Timer(components);
            timerClipboardDataCheck = new Timer(components);
            txtTooltip = new Label();
            displayCanvas = new PictureBox();
            topMenu = new FlowLayoutPanel();
            bttnToolBrush = new Button();
            dummyImageList = new ImageList(components);
            panelUndoRedoOkCancel = new FlowLayoutPanel();
            bttnUndo = new Button();
            bttnRedo = new Button();
            bttnOk = new Button();
            bttnCancel = new Button();
            brushImageLoadingWorker = new BackgroundWorker();
            bttnColorPicker = new Button();
            panelAllSettingsContainer = new Panel();
            panelDockSettingsContainer = new Panel();
            bttnToolEraser = new Button();
            bttnToolOrigin = new Button();
            panelSettingsContainer = new FlowLayoutPanel();
            bttnBrushControls = new Accordion();
            panelBrush = new FlowLayoutPanel();
            listviewBrushPicker = new ListView();
            listviewBrushImagePicker = new DoubleBufferedListView();
            panelBrushAddPickColor = new Panel();
            chkbxColorizeBrush = new CheckBox();
            txtColorInfluence = new Label();
            sliderColorInfluence = new TrackBar();
            panelColorInfluenceHSV = new FlowLayoutPanel();
            chkbxColorInfluenceHue = new CheckBox();
            chkbxColorInfluenceSat = new CheckBox();
            chkbxColorInfluenceVal = new CheckBox();
            bttnAddBrushImages = new Button();
            brushImageLoadProgressBar = new ProgressBar();
            bttnBrushColor = new Button();
            cmbxBlendMode = new ComboBox();
            txtBrushOpacity = new Label();
            sliderBrushOpacity = new TrackBar();
            txtBrushFlow = new Label();
            sliderBrushFlow = new TrackBar();
            txtBrushRotation = new Label();
            sliderBrushRotation = new TrackBar();
            txtBrushSize = new Label();
            sliderBrushSize = new TrackBar();
            bttnSpecialSettings = new Accordion();
            panelSpecialSettings = new FlowLayoutPanel();
            panelChosenEffect = new Panel();
            cmbxChosenEffect = new ComboBox();
            bttnChooseEffectSettings = new Button();
            txtMinDrawDistance = new Label();
            sliderMinDrawDistance = new TrackBar();
            txtBrushDensity = new Label();
            sliderBrushDensity = new TrackBar();
            cmbxSymmetry = new ComboBox();
            cmbxBrushSmoothing = new ComboBox();
            chkbxSeamlessDrawing = new CheckBox();
            chkbxOrientToMouse = new CheckBox();
            chkbxDitherDraw = new CheckBox();
            chkbxLockAlpha = new CheckBox();
            panelRGBLocks = new Panel();
            chkbxLockR = new CheckBox();
            chkbxLockG = new CheckBox();
            chkbxLockB = new CheckBox();
            panelHSVLocks = new Panel();
            chkbxLockHue = new CheckBox();
            chkbxLockSat = new CheckBox();
            chkbxLockVal = new CheckBox();
            bttnJitterBasicsControls = new Accordion();
            panelJitterBasics = new FlowLayoutPanel();
            txtRandMinSize = new Label();
            sliderRandMinSize = new TrackBar();
            txtRandMaxSize = new Label();
            sliderRandMaxSize = new TrackBar();
            txtRandRotLeft = new Label();
            sliderRandRotLeft = new TrackBar();
            txtRandRotRight = new Label();
            sliderRandRotRight = new TrackBar();
            txtRandFlowLoss = new Label();
            sliderRandFlowLoss = new TrackBar();
            txtRandHorzShift = new Label();
            sliderRandHorzShift = new TrackBar();
            txtRandVertShift = new Label();
            sliderRandVertShift = new TrackBar();
            bttnJitterColorControls = new Accordion();
            panelJitterColor = new FlowLayoutPanel();
            txtJitterRed = new Label();
            sliderJitterMinRed = new TrackBar();
            sliderJitterMaxRed = new TrackBar();
            txtJitterGreen = new Label();
            sliderJitterMinGreen = new TrackBar();
            sliderJitterMaxGreen = new TrackBar();
            txtJitterBlue = new Label();
            sliderJitterMinBlue = new TrackBar();
            sliderJitterMaxBlue = new TrackBar();
            txtJitterHue = new Label();
            sliderJitterMinHue = new TrackBar();
            sliderJitterMaxHue = new TrackBar();
            txtJitterSaturation = new Label();
            sliderJitterMinSat = new TrackBar();
            sliderJitterMaxSat = new TrackBar();
            txtJitterValue = new Label();
            sliderJitterMinVal = new TrackBar();
            sliderJitterMaxVal = new TrackBar();
            bttnShiftBasicsControls = new Accordion();
            panelShiftBasics = new FlowLayoutPanel();
            txtShiftSize = new Label();
            sliderShiftSize = new TrackBar();
            txtShiftRotation = new Label();
            sliderShiftRotation = new TrackBar();
            txtShiftFlow = new Label();
            sliderShiftFlow = new TrackBar();
            bttnTabAssignPressureControls = new Accordion();
            panelTabletAssignPressure = new FlowLayoutPanel();
            panelTabPressureBrushOpacity = new FlowLayoutPanel();
            panel19 = new Panel();
            txtTabPressureBrushOpacity = new Label();
            spinTabPressureBrushOpacity = new NumericUpDown();
            cmbxTabPressureBrushOpacity = new CmbxTabletValueType();
            panelTabPressureBrushFlow = new FlowLayoutPanel();
            panel3 = new Panel();
            txtTabPressureBrushFlow = new Label();
            spinTabPressureBrushFlow = new NumericUpDown();
            cmbxTabPressureBrushFlow = new CmbxTabletValueType();
            panelTabPressureBrushSize = new FlowLayoutPanel();
            panel8 = new Panel();
            txtTabPressureBrushSize = new Label();
            spinTabPressureBrushSize = new NumericUpDown();
            cmbxTabPressureBrushSize = new CmbxTabletValueType();
            panelTabPressureBrushRotation = new FlowLayoutPanel();
            panel2 = new Panel();
            txtTabPressureBrushRotation = new Label();
            spinTabPressureBrushRotation = new NumericUpDown();
            cmbxTabPressureBrushRotation = new CmbxTabletValueType();
            panelTabPressureMinDrawDistance = new FlowLayoutPanel();
            panel1 = new Panel();
            lblTabPressureMinDrawDistance = new Label();
            spinTabPressureMinDrawDistance = new NumericUpDown();
            cmbxTabPressureMinDrawDistance = new CmbxTabletValueType();
            panelTabPressureBrushDensity = new FlowLayoutPanel();
            panel4 = new Panel();
            lblTabPressureBrushDensity = new Label();
            spinTabPressureBrushDensity = new NumericUpDown();
            cmbxTabPressureBrushDensity = new CmbxTabletValueType();
            panelTabPressureRandMinSize = new FlowLayoutPanel();
            panel5 = new Panel();
            lblTabPressureRandMinSize = new Label();
            spinTabPressureRandMinSize = new NumericUpDown();
            cmbxTabPressureRandMinSize = new CmbxTabletValueType();
            panelTabPressureRandMaxSize = new FlowLayoutPanel();
            panel6 = new Panel();
            lblTabPressureRandMaxSize = new Label();
            spinTabPressureRandMaxSize = new NumericUpDown();
            cmbxTabPressureRandMaxSize = new CmbxTabletValueType();
            panelTabPressureRandRotLeft = new FlowLayoutPanel();
            panel7 = new Panel();
            lblTabPressureRandRotLeft = new Label();
            spinTabPressureRandRotLeft = new NumericUpDown();
            cmbxTabPressureRandRotLeft = new CmbxTabletValueType();
            panelTabPressureRandRotRight = new FlowLayoutPanel();
            panel9 = new Panel();
            lblTabPressureRandRotRight = new Label();
            spinTabPressureRandRotRight = new NumericUpDown();
            cmbxTabPressureRandRotRight = new CmbxTabletValueType();
            panelTabPressureRandFlowLoss = new FlowLayoutPanel();
            panel10 = new Panel();
            lblTabPressureRandFlowLoss = new Label();
            spinTabPressureRandFlowLoss = new NumericUpDown();
            cmbxTabPressureRandFlowLoss = new CmbxTabletValueType();
            panelTabPressureRandHorShift = new FlowLayoutPanel();
            panel12 = new Panel();
            lblTabPressureRandHorShift = new Label();
            spinTabPressureRandHorShift = new NumericUpDown();
            cmbxTabPressureRandHorShift = new CmbxTabletValueType();
            panelTabPressureRandVerShift = new FlowLayoutPanel();
            panel11 = new Panel();
            lblTabPressureRandVerShift = new Label();
            spinTabPressureRandVerShift = new NumericUpDown();
            cmbxTabPressureRandVerShift = new CmbxTabletValueType();
            panelTabPressureRedJitter = new FlowLayoutPanel();
            panel13 = new Panel();
            spinTabPressureMinRedJitter = new NumericUpDown();
            lblTabPressureRedJitter = new Label();
            spinTabPressureMaxRedJitter = new NumericUpDown();
            cmbxTabPressureRedJitter = new CmbxTabletValueType();
            panelTabPressureGreenJitter = new FlowLayoutPanel();
            panel14 = new Panel();
            spinTabPressureMinGreenJitter = new NumericUpDown();
            lblTabPressureGreenJitter = new Label();
            spinTabPressureMaxGreenJitter = new NumericUpDown();
            cmbxTabPressureGreenJitter = new CmbxTabletValueType();
            panelTabPressureBlueJitter = new FlowLayoutPanel();
            panel15 = new Panel();
            spinTabPressureMinBlueJitter = new NumericUpDown();
            lblTabPressureBlueJitter = new Label();
            spinTabPressureMaxBlueJitter = new NumericUpDown();
            cmbxTabPressureBlueJitter = new CmbxTabletValueType();
            panelTabPressureHueJitter = new FlowLayoutPanel();
            panel16 = new Panel();
            spinTabPressureMinHueJitter = new NumericUpDown();
            lblTabPressureHueJitter = new Label();
            spinTabPressureMaxHueJitter = new NumericUpDown();
            cmbxTabPressureHueJitter = new CmbxTabletValueType();
            panelTabPressureSatJitter = new FlowLayoutPanel();
            panel17 = new Panel();
            spinTabPressureMinSatJitter = new NumericUpDown();
            lblTabPressureSatJitter = new Label();
            spinTabPressureMaxSatJitter = new NumericUpDown();
            cmbxTabPressureSatJitter = new CmbxTabletValueType();
            panelTabPressureValueJitter = new FlowLayoutPanel();
            panel18 = new Panel();
            spinTabPressureMinValueJitter = new NumericUpDown();
            lblTabPressureValueJitter = new Label();
            spinTabPressureMaxValueJitter = new NumericUpDown();
            cmbxTabPressureValueJitter = new CmbxTabletValueType();
            bttnSettings = new Accordion();
            panelSettings = new FlowLayoutPanel();
            bttnUpdateCurrentBrush = new Button();
            bttnClearSettings = new Button();
            bttnDeleteBrush = new Button();
            bttnSaveBrush = new Button();
            chkbxAutomaticBrushDensity = new CheckBox();
            #endregion

            #region suspend them all, order is VERY delicate
            topMenu.SuspendLayout();
            displayCanvas.SuspendLayout();
            ((ISupportInitialize)(displayCanvas)).BeginInit();
            panelUndoRedoOkCancel.SuspendLayout();
            panelAllSettingsContainer.SuspendLayout();
            panelDockSettingsContainer.SuspendLayout();
            panelSettingsContainer.SuspendLayout();
            panelBrush.SuspendLayout();
            panelBrushAddPickColor.SuspendLayout();
            ((ISupportInitialize)(sliderColorInfluence)).BeginInit();
            panelColorInfluenceHSV.SuspendLayout();
            ((ISupportInitialize)(sliderBrushOpacity)).BeginInit();
            ((ISupportInitialize)(sliderBrushFlow)).BeginInit();
            ((ISupportInitialize)(sliderBrushRotation)).BeginInit();
            ((ISupportInitialize)(sliderBrushSize)).BeginInit();
            panelSpecialSettings.SuspendLayout();
            panelChosenEffect.SuspendLayout();
            panelRGBLocks.SuspendLayout();
            panelHSVLocks.SuspendLayout();
            ((ISupportInitialize)(sliderMinDrawDistance)).BeginInit();
            ((ISupportInitialize)(sliderBrushDensity)).BeginInit();
            panelJitterBasics.SuspendLayout();
            ((ISupportInitialize)(sliderRandMinSize)).BeginInit();
            ((ISupportInitialize)(sliderRandMaxSize)).BeginInit();
            ((ISupportInitialize)(sliderRandRotLeft)).BeginInit();
            ((ISupportInitialize)(sliderRandRotRight)).BeginInit();
            ((ISupportInitialize)(sliderRandFlowLoss)).BeginInit();
            ((ISupportInitialize)(sliderRandHorzShift)).BeginInit();
            ((ISupportInitialize)(sliderRandVertShift)).BeginInit();
            panelJitterColor.SuspendLayout();
            ((ISupportInitialize)(sliderJitterMinRed)).BeginInit();
            ((ISupportInitialize)(sliderJitterMaxRed)).BeginInit();
            ((ISupportInitialize)(sliderJitterMinGreen)).BeginInit();
            ((ISupportInitialize)(sliderJitterMaxGreen)).BeginInit();
            ((ISupportInitialize)(sliderJitterMinBlue)).BeginInit();
            ((ISupportInitialize)(sliderJitterMaxBlue)).BeginInit();
            ((ISupportInitialize)(sliderJitterMinHue)).BeginInit();
            ((ISupportInitialize)(sliderJitterMaxHue)).BeginInit();
            ((ISupportInitialize)(sliderJitterMinSat)).BeginInit();
            ((ISupportInitialize)(sliderJitterMaxSat)).BeginInit();
            ((ISupportInitialize)(sliderJitterMinVal)).BeginInit();
            ((ISupportInitialize)(sliderJitterMaxVal)).BeginInit();
            panelShiftBasics.SuspendLayout();
            ((ISupportInitialize)(sliderShiftSize)).BeginInit();
            ((ISupportInitialize)(sliderShiftRotation)).BeginInit();
            ((ISupportInitialize)(sliderShiftFlow)).BeginInit();
            panelTabletAssignPressure.SuspendLayout();
            panelTabPressureBrushOpacity.SuspendLayout();
            panel19.SuspendLayout();
            ((ISupportInitialize)(spinTabPressureBrushOpacity)).BeginInit();
            panelTabPressureBrushFlow.SuspendLayout();
            panel3.SuspendLayout();
            ((ISupportInitialize)(spinTabPressureBrushFlow)).BeginInit();
            panelTabPressureBrushSize.SuspendLayout();
            panel8.SuspendLayout();
            ((ISupportInitialize)(spinTabPressureBrushSize)).BeginInit();
            panelTabPressureBrushRotation.SuspendLayout();
            panel2.SuspendLayout();
            ((ISupportInitialize)(spinTabPressureBrushRotation)).BeginInit();
            panelTabPressureMinDrawDistance.SuspendLayout();
            panel1.SuspendLayout();
            ((ISupportInitialize)(spinTabPressureMinDrawDistance)).BeginInit();
            panelTabPressureBrushDensity.SuspendLayout();
            panel4.SuspendLayout();
            ((ISupportInitialize)(spinTabPressureBrushDensity)).BeginInit();
            panelTabPressureRandMinSize.SuspendLayout();
            panel5.SuspendLayout();
            ((ISupportInitialize)(spinTabPressureRandMinSize)).BeginInit();
            panelTabPressureRandMaxSize.SuspendLayout();
            panel6.SuspendLayout();
            ((ISupportInitialize)(spinTabPressureRandMaxSize)).BeginInit();
            panelTabPressureRandRotLeft.SuspendLayout();
            panel7.SuspendLayout();
            ((ISupportInitialize)(spinTabPressureRandRotLeft)).BeginInit();
            panelTabPressureRandRotRight.SuspendLayout();
            panel9.SuspendLayout();
            ((ISupportInitialize)(spinTabPressureRandRotRight)).BeginInit();
            panelTabPressureRandFlowLoss.SuspendLayout();
            panel10.SuspendLayout();
            ((ISupportInitialize)(spinTabPressureRandFlowLoss)).BeginInit();
            panelTabPressureRandHorShift.SuspendLayout();
            panel12.SuspendLayout();
            ((ISupportInitialize)(spinTabPressureRandHorShift)).BeginInit();
            panelTabPressureRandVerShift.SuspendLayout();
            panel11.SuspendLayout();
            ((ISupportInitialize)(spinTabPressureRandVerShift)).BeginInit();
            panelTabPressureRedJitter.SuspendLayout();
            panel13.SuspendLayout();
            ((ISupportInitialize)(spinTabPressureMinRedJitter)).BeginInit();
            ((ISupportInitialize)(spinTabPressureMaxRedJitter)).BeginInit();
            panelTabPressureGreenJitter.SuspendLayout();
            panel14.SuspendLayout();
            ((ISupportInitialize)(spinTabPressureMinGreenJitter)).BeginInit();
            ((ISupportInitialize)(spinTabPressureMaxGreenJitter)).BeginInit();
            panelTabPressureBlueJitter.SuspendLayout();
            panel15.SuspendLayout();
            ((ISupportInitialize)(spinTabPressureMinBlueJitter)).BeginInit();
            ((ISupportInitialize)(spinTabPressureMaxBlueJitter)).BeginInit();
            panelTabPressureHueJitter.SuspendLayout();
            panel16.SuspendLayout();
            ((ISupportInitialize)(spinTabPressureMinHueJitter)).BeginInit();
            ((ISupportInitialize)(spinTabPressureMaxHueJitter)).BeginInit();
            panelTabPressureSatJitter.SuspendLayout();
            panel17.SuspendLayout();
            ((ISupportInitialize)(spinTabPressureMinSatJitter)).BeginInit();
            ((ISupportInitialize)(spinTabPressureMaxSatJitter)).BeginInit();
            panelTabPressureValueJitter.SuspendLayout();
            panel18.SuspendLayout();
            ((ISupportInitialize)(spinTabPressureMinValueJitter)).BeginInit();
            ((ISupportInitialize)(spinTabPressureMaxValueJitter)).BeginInit();
            panelSettings.SuspendLayout();
            SuspendLayout();
            #endregion

            #region timerRepositionUpdate
            timerRepositionUpdate.Interval = 5;
            timerRepositionUpdate.Tick += new EventHandler(RepositionUpdate_Tick);
            #endregion

            #region timerClipboardDataCheck
            timerClipboardDataCheck.Interval = 1000;
            timerClipboardDataCheck.Tick += new EventHandler(ClipboardDataCheck_Tick);
            timerClipboardDataCheck.Enabled = true;
            #endregion

            #region txtTooltip
            txtTooltip.BackColor = SystemColors.ControlDarkDark;
            txtTooltip.ForeColor = SystemColors.HighlightText;
            txtTooltip.AutoSize = true;
            txtTooltip.Dock = DockStyle.Top;
            txtTooltip.Font = new Font("Microsoft Sans Serif", 10);
            txtTooltip.Size = new Size(76, 17);
            txtTooltip.TabIndex = 0;
            #endregion

            #region displayCanvas
            displayCanvas.BackColor = Color.FromArgb(207, 207, 207);
            displayCanvas.Controls.Add(txtTooltip);
            displayCanvas.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            displayCanvas.Location = new Point(0, 29);
            displayCanvas.Margin = Padding.Empty;
            displayCanvas.Size = new Size(656, 512);
            displayCanvas.TabStop = false;
            displayCanvas.Paint += new PaintEventHandler(DisplayCanvas_Paint);
            displayCanvas.MouseDown += new MouseEventHandler(DisplayCanvas_MouseDown);
            displayCanvas.MouseEnter += new EventHandler(DisplayCanvas_MouseEnter);
            displayCanvas.MouseMove += new MouseEventHandler(DisplayCanvas_MouseMove);
            displayCanvas.MouseUp += new MouseEventHandler(DisplayCanvas_MouseUp);
            #endregion

            #region topMenu
            topMenu.FlowDirection = FlowDirection.LeftToRight;
            topMenu.Width = displayCanvas.Width;
            topMenu.Height = 32;
            topMenu.Margin = Padding.Empty;

            // The options button
            menuOptions = new Button
            {
                Text = Strings.MenuOptions,
                Height = 32,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            ContextMenuStrip preferencesContextMenu = new ContextMenuStrip();

            // Options -> custom brush images...
            menuBrushImageDirectories = new ToolStripMenuItem(Strings.MenuCustomBrushImages);
            menuBrushImageDirectories.Click += (a, b) =>
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
            };
            preferencesContextMenu.Items.Add(menuBrushImageDirectories);

            // Options -> keyboard shortcuts...
            menuKeyboardShortcutsDialog = new ToolStripMenuItem(Strings.MenuKeyboardShortcuts);
            menuKeyboardShortcutsDialog.Click += (a, b) =>
            {
                var shortcutsDialog = new EditKeyboardShortcuts(KeyboardShortcuts);
                if (shortcutsDialog.ShowDialog() == DialogResult.OK)
                {
                    InitKeyboardShortcuts(shortcutsDialog.GetShortcutsAfterDialogOK());
                }
            };
            preferencesContextMenu.Items.Add(menuKeyboardShortcutsDialog);

            // Separator
            preferencesContextMenu.Items.Add(new ToolStripSeparator());

            // Options -> reset canvas
            menuResetCanvas = new ToolStripMenuItem(Strings.MenuRecenterTheCanvas, null, (a, b) =>
            {
                HandleShortcut(new KeyboardShortcut() { Target = ShortcutTarget.ResetCanvasTransforms });
            });

            preferencesContextMenu.Items.Add(menuResetCanvas);

            // Options -> set canvas background
            menuSetCanvasBackground = new ToolStripMenuItem(Strings.MenuSetCanvasBackground);

            menuSetCanvasBgImageFit = new ToolStripMenuItem(Strings.MenuCanvasBgStretchToFit);
            menuSetCanvasBgImageOnlyIfFits = new ToolStripMenuItem(Strings.MenuCanvasBgUseOnlyIfSameSize);
            menuSetCanvasBgImage = new ToolStripMenuItem(Strings.BackgroundImage);
            menuSetCanvasBgImage.DropDown.Items.Add(menuSetCanvasBgImageFit);
            menuSetCanvasBgImage.DropDown.Items.Add(menuSetCanvasBgImageOnlyIfFits);

            menuSetCanvasBgTransparent = new ToolStripMenuItem(Strings.BackgroundTransparent);
            menuSetCanvasBgGray = new ToolStripMenuItem(Strings.BackgroundNone);
            menuSetCanvasBgWhite = new ToolStripMenuItem(Strings.BackgroundWhite);
            menuSetCanvasBgBlack = new ToolStripMenuItem(Strings.BackgroundBlack);

            menuSetCanvasBgImageFit.Click += (a, b) =>
            {
                UserSettings.BackgroundDisplayMode = BackgroundDisplayMode.ClipboardFit;
                UpdateTopMenuState();
                UpdateBackgroundFromClipboard(true);
            };
            menuSetCanvasBgImageOnlyIfFits.Click += (a, b) =>
            {
                UserSettings.BackgroundDisplayMode = BackgroundDisplayMode.ClipboardOnlyIfFits;
                UpdateTopMenuState();
                UpdateBackgroundFromClipboard(true);
            };
            menuSetCanvasBgTransparent.Click += (a, b) =>
            {
                UserSettings.BackgroundDisplayMode = BackgroundDisplayMode.Transparent;
                UpdateTopMenuState();
                bmpBackgroundClipboard?.Dispose();
            };
            menuSetCanvasBgGray.Click += (a, b) =>
            {
                UserSettings.BackgroundDisplayMode = BackgroundDisplayMode.Gray;
                UpdateTopMenuState();
                bmpBackgroundClipboard?.Dispose();
            };
            menuSetCanvasBgWhite.Click += (a, b) =>
            {
                UserSettings.BackgroundDisplayMode = BackgroundDisplayMode.White;
                UpdateTopMenuState();
                bmpBackgroundClipboard?.Dispose();
            };
            menuSetCanvasBgBlack.Click += (a, b) =>
            {
                UserSettings.BackgroundDisplayMode = BackgroundDisplayMode.Black;
                UpdateTopMenuState();
                bmpBackgroundClipboard?.Dispose();
            };

            menuSetCanvasBackground.DropDown.Items.Add(menuSetCanvasBgImage);
            menuSetCanvasBackground.DropDown.Items.Add(menuSetCanvasBgTransparent);
            menuSetCanvasBackground.DropDown.Items.Add(menuSetCanvasBgGray);
            menuSetCanvasBackground.DropDown.Items.Add(menuSetCanvasBgWhite);
            menuSetCanvasBackground.DropDown.Items.Add(menuSetCanvasBgBlack);

            preferencesContextMenu.Items.Add(menuSetCanvasBackground);

            // Options -> display settings -> brush indicator
            menuDisplaySettings = new ToolStripMenuItem(Strings.MenuDisplaySettings);
            menuBrushIndicator = new ToolStripMenuItem(Strings.MenuDisplayBrushIndicator);
            menuBrushIndicatorSquare = new ToolStripMenuItem(Strings.MenuDisplayBrushIndicatorSquare);
            menuBrushIndicatorPreview = new ToolStripMenuItem(Strings.MenuDisplayBrushIndicatorPreview);

            menuBrushIndicatorSquare.Click += (a, b) =>
            {
                UserSettings.BrushCursorPreview = BrushCursorPreview.Square;
                UpdateTopMenuState();
            };
            menuBrushIndicatorPreview.Click += (a, b) =>
            {
                UserSettings.BrushCursorPreview = BrushCursorPreview.Preview;
                UpdateTopMenuState();
            };

            menuBrushIndicator.DropDown.Items.Add(menuBrushIndicatorSquare);
            menuBrushIndicator.DropDown.Items.Add(menuBrushIndicatorPreview);

            menuDisplaySettings.DropDown.Items.Add(menuBrushIndicator);
            preferencesContextMenu.Items.Add(menuDisplaySettings);

            // Options -> display settings -> show symmetry lines when in use
            menuShowSymmetryLinesInUse = new ToolStripMenuItem(Strings.MenuDisplayShowSymmetryLines);
            menuShowSymmetryLinesInUse.Click += (a, b) =>
            {
                UserSettings.ShowSymmetryLinesWhenUsingSymmetry = !UserSettings.ShowSymmetryLinesWhenUsingSymmetry;
                UpdateTopMenuState();
            };
            menuDisplaySettings.DropDown.Items.Add(menuShowSymmetryLinesInUse);

            // Options -> display settings -> show the circle for minimum distance when in use
            menuShowMinDistanceInUse = new ToolStripMenuItem(Strings.MenuDisplayShowMinDistCircle);
                        menuShowMinDistanceInUse.Click += (a, b) =>
            {
                UserSettings.ShowCircleRadiusWhenUsingMinDistance = !UserSettings.ShowCircleRadiusWhenUsingMinDistance;
                UpdateTopMenuState();
            };
            menuDisplaySettings.DropDown.Items.Add(menuShowMinDistanceInUse);

            // Separator
            preferencesContextMenu.Items.Add(new ToolStripSeparator());

            // Options -> color picker includes alpha
            menuColorPickerIncludesAlpha = new ToolStripMenuItem(Strings.MenuColorPickerCopiesTransparency);
            menuColorPickerIncludesAlpha.Click += (a, b) =>
            {
                UserSettings.ColorPickerIncludesAlpha = !UserSettings.ColorPickerIncludesAlpha;
                UpdateTopMenuState();
            };
            preferencesContextMenu.Items.Add(menuColorPickerIncludesAlpha);

            // Options -> color picker switches to last tool when used
            menuColorPickerSwitchesToPrevTool = new ToolStripMenuItem(Strings.MenuColorPickerSwitches);
            menuColorPickerSwitchesToPrevTool.Click += (a, b) =>
            {
                UserSettings.ColorPickerSwitchesToLastTool = !UserSettings.ColorPickerSwitchesToLastTool;
                UpdateTopMenuState();
            };
            preferencesContextMenu.Items.Add(menuColorPickerSwitchesToPrevTool);

            // Options -> remove brush image paths when not found
            menuRemoveUnfoundImagePaths = new ToolStripMenuItem(Strings.MenuRemoveUnfoundImagePaths);
            menuRemoveUnfoundImagePaths.Click += (a, b) =>
            {
                UserSettings.RemoveBrushImagePathsWhenNotFound = !UserSettings.RemoveBrushImagePathsWhenNotFound;
                UpdateTopMenuState();
            };
            preferencesContextMenu.Items.Add(menuRemoveUnfoundImagePaths);

            // Options -> don't ask to confirm when closing/saving
            menuConfirmCloseSave = new ToolStripMenuItem(Strings.MenuDontConfirmCloseSave);
            menuConfirmCloseSave.Click += (a, b) =>
            {
                UserSettings.DisableConfirmationOnCloseOrSave = !UserSettings.DisableConfirmationOnCloseOrSave;
                UpdateTopMenuState();
            };
            preferencesContextMenu.Items.Add(menuConfirmCloseSave);

            menuOptions.Click += (a, b) => {
                preferencesContextMenu.Show(menuOptions.PointToScreen(new Point(0, menuOptions.Height)));
            };

            topMenu.Controls.Add(menuOptions);

            // sets up the canvas zoom button
            menuCanvasZoomBttn = new Button
            {
                Image = Resources.MenuZoom,
                Height = 32,
                Width = 32,
                Margin = Padding.Empty
            };

            ContextMenuStrip CanvasZoomButtonContextMenu = new ContextMenuStrip();

            menuCanvasZoomBttn.Click += (a, b) => {
                CanvasZoomButtonContextMenu.Show(menuCanvasZoomBttn.PointToScreen(new Point(0, menuCanvasZoomBttn.Height)));
            };

            menuCanvasAngleBttn = new Button();

            // canvas zoom -> reset zoom
            menuCanvasZoomReset = new ToolStripMenuItem(Strings.MenuZoomReset);
            menuCanvasZoomReset.Click += (a, b) =>
            {
                HandleShortcut(new KeyboardShortcut() { Target = ShortcutTarget.CanvasZoom, ActionData = "100|set" });
            };
            CanvasZoomButtonContextMenu.Items.Add(menuCanvasZoomReset);

            // canvas zoom -> fit to window
            menuCanvasZoomFit = new ToolStripMenuItem(Strings.MenuZoomFit);
            menuCanvasZoomFit.Click += (a, b) =>
            {
                HandleShortcut(new KeyboardShortcut() { Target = ShortcutTarget.CanvasZoomFit });
            };
            CanvasZoomButtonContextMenu.Items.Add(menuCanvasZoomFit);

            // canvas zoom -> zoom to...
            menuCanvasZoomTo = new ToolStripMenuItem(Strings.MenuZoomTo);
            menuCanvasZoomTo.Click += (a, b) =>
            {
                TextboxDialog dlg = new TextboxDialog(
                    Strings.ShortcutCanvasZoom,
                    null,
                    Strings.Ok, (txt) =>
                    {
                        if (!int.TryParse(txt, out int value))
                        {
                            return " ";
                        }

                        if (!Setting.AllSettings[ShortcutTarget.CanvasZoom].ValidateNumberValue(value))
                        {
                            return string.Format(Strings.TextboxDialogRangeInvalid,
                                Setting.AllSettings[ShortcutTarget.CanvasZoom].MinMaxRange.Item1,
                                Setting.AllSettings[ShortcutTarget.CanvasZoom].MinMaxRange.Item2);
                        }

                        return "";
                    });

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    HandleShortcut(new KeyboardShortcut() {
                        Target = ShortcutTarget.CanvasZoom,
                        ActionData = int.Parse(dlg.GetSubmittedText()) + "|set"
                    });
                }
            };
            CanvasZoomButtonContextMenu.Items.Add(menuCanvasZoomTo);

            topMenu.Controls.Add(menuCanvasZoomBttn);

            // canvas zoom slider
            sliderCanvasZoom = new Slider(
                new float[] { 1, 5, 10, 13, 17, 20, 25, 33, 50, 67, 100, 150, 200, 300, 400, 500, 600, 800, 1000, 1200, 1400, 1600, 2000, 2400, 2800, 3200, 4000, 4800, 5600, 6400 },
                100)
            {
                DiscreteStops = true,
                Width = 128,
                Height = 32,
                Margin = Padding.Empty,
                ComputeText = (value) => { return $"{value}%"; }
            };
            sliderCanvasZoom.MouseEnter += SliderCanvasZoom_MouseEnter;
            sliderCanvasZoom.ValueChanged += SliderCanvasZoom_ValueChanged;

            topMenu.Controls.Add(sliderCanvasZoom);

            // sets up the canvas angle button
            menuCanvasAngleBttn = new Button
            {
                Image = Resources.MenuAngle,
                Height = 32,
                Width = 32,
                Margin = Padding.Empty
            };

            ContextMenuStrip CanvasAngleButtonContextMenu = new ContextMenuStrip();

            menuCanvasAngleBttn.Click += (a, b) => {
                CanvasAngleButtonContextMenu.Show(menuCanvasAngleBttn.PointToScreen(new Point(0, menuCanvasAngleBttn.Height)));
            };

            // canvas angle -> reset angle
            menuCanvasAngleReset = new ToolStripMenuItem(Strings.MenuRotateReset);
            menuCanvasAngleReset.Click += (a, b) =>
            {
                HandleShortcut(new KeyboardShortcut() { Target = ShortcutTarget.CanvasRotation, ActionData = "0|set" });
            };
            CanvasAngleButtonContextMenu.Items.Add(menuCanvasAngleReset);

            // canvas angle -> rotate to 90
            menuCanvasAngle90 = new ToolStripMenuItem(Strings.MenuRotate90);
            menuCanvasAngle90.Click += (a, b) =>
            {
                HandleShortcut(new KeyboardShortcut() { Target = ShortcutTarget.CanvasRotation, ActionData = "90|set" });
            };
            CanvasAngleButtonContextMenu.Items.Add(menuCanvasAngle90);

            // canvas angle -> rotate to 180
            menuCanvasAngle180 = new ToolStripMenuItem(Strings.MenuRotate180);
            menuCanvasAngle180.Click += (a, b) =>
            {
                HandleShortcut(new KeyboardShortcut() { Target = ShortcutTarget.CanvasRotation, ActionData = "180|set" });
            };
            CanvasAngleButtonContextMenu.Items.Add(menuCanvasAngle180);

            // canvas angle -> rotate to 270
            menuCanvasAngle270 = new ToolStripMenuItem(Strings.MenuRotate270);
            menuCanvasAngle270.Click += (a, b) =>
            {
                HandleShortcut(new KeyboardShortcut() { Target = ShortcutTarget.CanvasRotation, ActionData = "270|set" });
            };
            CanvasAngleButtonContextMenu.Items.Add(menuCanvasAngle270);

            UpdateTopMenuState();

            // canvas angle -> rotate to...
            menuCanvasAngleTo = new ToolStripMenuItem(Strings.MenuRotateTo);
            menuCanvasAngleTo.Click += (a, b) =>
            {
                TextboxDialog dlg = new TextboxDialog(
                    Strings.ShortcutCanvasAngle,
                    null,
                    Strings.Ok, (txt) =>
                    {
                        if (!int.TryParse(txt, out int value))
                        {
                            return " ";
                        }

                        return null;
                    });

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    HandleShortcut(new KeyboardShortcut()
                    {
                        Target = ShortcutTarget.CanvasRotation,
                        ActionData = int.Parse(dlg.GetSubmittedText()) + "|set"
                    });
                }
            };
            CanvasAngleButtonContextMenu.Items.Add(menuCanvasAngleTo);

            topMenu.Controls.Add(menuCanvasAngleBttn);

            // canvas angle slider
            sliderCanvasAngle = new Slider(-180f, 180f, 0)
            {
                IntegerOnly = true,
                Width = 128,
                Height = 32,
                Margin = Padding.Empty,
                ComputeText = (value) => { return $"{value}°"; }
            };
            sliderCanvasAngle.MouseEnter += SliderCanvasAngle_MouseEnter;
            sliderCanvasAngle.ValueChanged += SliderCanvasAngle_ValueChanged;

            topMenu.Controls.Add(sliderCanvasAngle);

            panelTools = new FlowLayoutPanel();
            panelTools.Controls.Add(bttnToolBrush);
            panelTools.Controls.Add(bttnToolEraser);
            panelTools.Controls.Add(bttnColorPicker);
            panelTools.Controls.Add(bttnToolOrigin);
            panelTools.Margin = Padding.Empty;
            panelTools.Padding = Padding.Empty;
            panelTools.Size = new Size(155, 38);
            panelTools.TabIndex = 30;

            topMenu.Controls.Add(panelTools);

            #endregion

            #region bttnToolBrush
            bttnToolBrush.BackColor = SystemColors.ButtonShadow;
            bttnToolBrush.Image = Resources.ToolBrush;
            bttnToolBrush.UseVisualStyleBackColor = false;
            bttnToolBrush.Click += new EventHandler(BttnToolBrush_Click);
            bttnToolBrush.MouseEnter += new EventHandler(BttnToolBrush_MouseEnter);
            bttnToolBrush.Location = new Point(8, 3);
            bttnToolBrush.Margin = Padding.Empty;
            bttnToolBrush.Size = new Size(32, 32);
            bttnToolBrush.TabIndex = 1;
            #endregion

            #region dummyImageList
            dummyImageList.ColorDepth = System.Windows.Forms.ColorDepth.Depth32Bit;
            dummyImageList.TransparentColor = Color.Transparent;
            dummyImageList.ImageSize = new Size(24, 24);
            #endregion

            #region panelUndoRedoOkCancel
            panelUndoRedoOkCancel.BackColor = SystemColors.ControlDarkDark;
            panelUndoRedoOkCancel.Controls.Add(bttnUndo);
            panelUndoRedoOkCancel.Controls.Add(bttnRedo);
            panelUndoRedoOkCancel.Controls.Add(bttnOk);
            panelUndoRedoOkCancel.Controls.Add(bttnCancel);
            panelUndoRedoOkCancel.Dock = DockStyle.Bottom;
            panelUndoRedoOkCancel.Location = new Point(0, 484);
            panelUndoRedoOkCancel.Margin = new Padding(0, 3, 0, 3);
            panelUndoRedoOkCancel.Size = new Size(173, 57);
            panelUndoRedoOkCancel.TabIndex = 145;
            #endregion

            #region bttnUndo
            bttnUndo.Enabled = false;
            bttnUndo.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            bttnUndo.Margin = new Padding(3, 3, 13, 3);
            bttnUndo.Size = new Size(77, 23);
            bttnUndo.TabIndex = 141;
            bttnUndo.UseVisualStyleBackColor = true;
            bttnUndo.Click += new EventHandler(BttnUndo_Click);
            bttnUndo.MouseEnter += new EventHandler(BttnUndo_MouseEnter);
            #endregion

            #region bttnRedo
            bttnRedo.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            bttnRedo.Enabled = false;
            bttnRedo.Location = new Point(93, 3);
            bttnRedo.Margin = new Padding(0, 3, 0, 3);
            bttnRedo.Size = new Size(77, 23);
            bttnRedo.TabIndex = 142;
            bttnRedo.UseVisualStyleBackColor = true;
            bttnRedo.Click += new EventHandler(BttnRedo_Click);
            bttnRedo.MouseEnter += new EventHandler(BttnRedo_MouseEnter);
            #endregion

            #region bttnOk
            bttnOk.BackColor = Color.Honeydew;
            bttnOk.Location = new Point(3, 32);
            bttnOk.Margin = new Padding(3, 3, 13, 3);
            bttnOk.Size = new Size(77, 23);
            bttnOk.TabIndex = 143;
            bttnOk.UseVisualStyleBackColor = false;
            bttnOk.Click += new EventHandler(BttnOk_Click);
            bttnOk.MouseEnter += new EventHandler(BttnOk_MouseEnter);
            #endregion

            #region bttnCancel
            bttnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            bttnCancel.BackColor = Color.MistyRose;
            bttnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            bttnCancel.Location = new Point(93, 32);
            bttnCancel.Margin = new Padding(0, 3, 0, 3);
            bttnCancel.Size = new Size(77, 23);
            bttnCancel.TabIndex = 144;
            bttnCancel.UseVisualStyleBackColor = false;
            bttnCancel.Click += new EventHandler(BttnCancel_Click);
            bttnCancel.MouseEnter += new EventHandler(BttnCancel_MouseEnter);
            #endregion

            #region brushImageLoadingWorker
            brushImageLoadingWorker.WorkerReportsProgress = true;
            brushImageLoadingWorker.WorkerSupportsCancellation = true;
            brushImageLoadingWorker.DoWork += new DoWorkEventHandler(BrushImageLoadingWorker_DoWork);
            brushImageLoadingWorker.ProgressChanged += new ProgressChangedEventHandler(BrushImageLoadingWorker_ProgressChanged);
            brushImageLoadingWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(BrushImageLoadingWorker_RunWorkerCompleted);
            #endregion

            #region bttnColorPicker
            bttnColorPicker.Image = Resources.ColorPickerIcon;
            bttnColorPicker.Location = new Point(78, 3);
            bttnColorPicker.Margin = Padding.Empty;
            bttnColorPicker.Size = new Size(32, 32);
            bttnColorPicker.TabIndex = 3;
            bttnColorPicker.UseVisualStyleBackColor = true;
            bttnColorPicker.Click += new EventHandler(BttnToolColorPicker_Click);
            bttnColorPicker.MouseEnter += new EventHandler(BttnToolColorPicker_MouseEnter);
            #endregion

            #region panelAllSettingsContainer
            panelAllSettingsContainer.BackColor = Color.Transparent;
            panelAllSettingsContainer.Dock = DockStyle.Right;
            panelAllSettingsContainer.Location = new Point(656, 0);
            panelAllSettingsContainer.Size = new Size(173, 541);
            panelAllSettingsContainer.TabIndex = 140;
            panelAllSettingsContainer.Controls.Add(panelDockSettingsContainer);
            panelAllSettingsContainer.Controls.Add(panelUndoRedoOkCancel);
            #endregion

            #region panelDockSettingsContainer
            panelDockSettingsContainer.AutoScroll = true;
            panelDockSettingsContainer.BackColor = SystemColors.ControlDarkDark;
            panelDockSettingsContainer.Dock = DockStyle.Fill;
            panelDockSettingsContainer.Location = new Point(0, 0);
            panelDockSettingsContainer.Size = new Size(173, 484);
            panelDockSettingsContainer.TabIndex = 139;
            panelDockSettingsContainer.Controls.Add(panelSettingsContainer);
            #endregion

            #region BttnToolEraser
            bttnToolEraser.Image = Resources.ToolEraser;
            bttnToolEraser.Location = new Point(43, 3);
            bttnToolEraser.Margin = Padding.Empty;
            bttnToolEraser.Size = new Size(32, 32);
            bttnToolEraser.TabIndex = 2;
            bttnToolEraser.UseVisualStyleBackColor = true;
            bttnToolEraser.Click += new EventHandler(BttnToolEraser_Click);
            bttnToolEraser.MouseEnter += new EventHandler(BttnToolEraser_MouseEnter);
            #endregion

            #region bttnToolOrigin
            bttnToolOrigin.Image = Resources.ToolOrigin;
            bttnToolOrigin.Location = new Point(113, 3);
            bttnToolOrigin.Margin = Padding.Empty;
            bttnToolOrigin.Size = new Size(32, 32);
            bttnToolOrigin.TabIndex = 4;
            bttnToolOrigin.UseVisualStyleBackColor = true;
            bttnToolOrigin.Click += new EventHandler(BttnToolOrigin_Click);
            bttnToolOrigin.MouseEnter += new EventHandler(BttnToolOrigin_MouseEnter);
            #endregion

            #region panelSettingsContainer
            panelSettingsContainer.AutoSize = true;
            panelSettingsContainer.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelSettingsContainer.BackColor = Color.Transparent;
            panelSettingsContainer.FlowDirection = FlowDirection.TopDown;
            panelSettingsContainer.Location = new Point(0, 35);
            panelSettingsContainer.Margin = new Padding(0, 3, 0, 3);
            panelSettingsContainer.Size = new Size(156, 3073);
            panelSettingsContainer.TabIndex = 138;
            panelSettingsContainer.Controls.Add(bttnBrushControls);
            panelSettingsContainer.Controls.Add(panelBrush);
            panelSettingsContainer.Controls.Add(bttnSpecialSettings);
            panelSettingsContainer.Controls.Add(panelSpecialSettings);
            panelSettingsContainer.Controls.Add(bttnJitterBasicsControls);
            panelSettingsContainer.Controls.Add(panelJitterBasics);
            panelSettingsContainer.Controls.Add(bttnJitterColorControls);
            panelSettingsContainer.Controls.Add(panelJitterColor);
            panelSettingsContainer.Controls.Add(bttnShiftBasicsControls);
            panelSettingsContainer.Controls.Add(panelShiftBasics);
            panelSettingsContainer.Controls.Add(bttnTabAssignPressureControls);
            panelSettingsContainer.Controls.Add(panelTabletAssignPressure);
            panelSettingsContainer.Controls.Add(bttnSettings);
            panelSettingsContainer.Controls.Add(panelSettings);
            #endregion

            #region bttnBrushControls
            bttnBrushControls.BackColor = Color.Black;
            bttnBrushControls.ForeColor = Color.WhiteSmoke;
            bttnBrushControls.Location = new Point(0, 3);
            bttnBrushControls.Margin = new Padding(0, 3, 0, 3);
            bttnBrushControls.Size = new Size(155, 23);
            bttnBrushControls.TabIndex = 5;
            bttnBrushControls.TextAlign = ContentAlignment.MiddleLeft;
            bttnBrushControls.UseVisualStyleBackColor = false;
            #endregion

            #region panelBrush
            panelBrush.AutoSize = true;
            panelBrush.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelBrush.BackColor = SystemColors.Control;
            panelBrush.FlowDirection = FlowDirection.TopDown;
            panelBrush.Location = new Point(0, 32);
            panelBrush.Margin = new Padding(0, 3, 0, 3);
            panelBrush.Size = new Size(156, 546);
            panelBrush.TabIndex = 5;
            panelBrush.Controls.Add(listviewBrushPicker);
            panelBrush.Controls.Add(listviewBrushImagePicker);
            panelBrush.Controls.Add(panelBrushAddPickColor);
            panelBrush.Controls.Add(txtColorInfluence);
            panelBrush.Controls.Add(sliderColorInfluence);
            panelBrush.Controls.Add(panelColorInfluenceHSV);
            panelBrush.Controls.Add(cmbxBlendMode);
            panelBrush.Controls.Add(txtBrushOpacity);
            panelBrush.Controls.Add(sliderBrushOpacity);
            panelBrush.Controls.Add(txtBrushFlow);
            panelBrush.Controls.Add(sliderBrushFlow);
            panelBrush.Controls.Add(txtBrushRotation);
            panelBrush.Controls.Add(sliderBrushRotation);
            panelBrush.Controls.Add(txtBrushSize);
            panelBrush.Controls.Add(sliderBrushSize);
            #endregion

            #region listviewBrushPicker
            listviewBrushPicker.HideSelection = false;
            listviewBrushPicker.Location = new Point(0, 98);
            listviewBrushPicker.Margin = new Padding(0, 0, 0, 3);
            listviewBrushPicker.Size = new Size(156, 92);
            listviewBrushPicker.TabIndex = 8;
            listviewBrushPicker.Columns.Add("_"); // name hidden and unimportant
            listviewBrushPicker.HeaderStyle = ColumnHeaderStyle.None;
            listviewBrushPicker.UseCompatibleStateImageBehavior = false;
            listviewBrushPicker.View = System.Windows.Forms.View.Details;
            listviewBrushPicker.SelectedIndexChanged += new EventHandler(ListViewBrushPicker_SelectedIndexChanged);
            listviewBrushPicker.MouseEnter += new EventHandler(ListviewBrushPicker_MouseEnter);
            #endregion

            #region listviewBrushImagePicker
            listviewBrushImagePicker.HideSelection = false;
            listviewBrushImagePicker.Location = new Point(0, 193);
            listviewBrushImagePicker.Margin = new Padding(0, 0, 0, 3);
            listviewBrushImagePicker.Size = new Size(156, 175);
            listviewBrushImagePicker.TabIndex = 9;
            listviewBrushImagePicker.LargeImageList = dummyImageList;
            listviewBrushImagePicker.MultiSelect = false;
            listviewBrushImagePicker.OwnerDraw = true;
            listviewBrushImagePicker.ShowItemToolTips = true;
            listviewBrushImagePicker.UseCompatibleStateImageBehavior = false;
            listviewBrushImagePicker.VirtualMode = true;
            listviewBrushImagePicker.CacheVirtualItems += new CacheVirtualItemsEventHandler(ListViewBrushImagePicker_CacheVirtualItems);
            listviewBrushImagePicker.DrawColumnHeader += new DrawListViewColumnHeaderEventHandler(ListViewBrushImagePicker_DrawColumnHeader);
            listviewBrushImagePicker.DrawItem += new DrawListViewItemEventHandler(ListViewBrushImagePicker_DrawItem);
            listviewBrushImagePicker.DrawSubItem += new DrawListViewSubItemEventHandler(ListViewBrushImagePicker_DrawSubItem);
            listviewBrushImagePicker.RetrieveVirtualItem += new RetrieveVirtualItemEventHandler(ListViewBrushImagePicker_RetrieveVirtualItem);
            listviewBrushImagePicker.SelectedIndexChanged += new EventHandler(ListViewBrushImagePicker_SelectedIndexChanged);
            listviewBrushImagePicker.MouseEnter += new EventHandler(ListViewBrushImagePicker_MouseEnter);
            #endregion

            #region panelBrushAddPickColor
            panelBrushAddPickColor.Location = new Point(0, 374);
            panelBrushAddPickColor.Margin = new Padding(0, 3, 0, 3);
            panelBrushAddPickColor.Size = new Size(156, 72);
            panelBrushAddPickColor.TabIndex = 10;
            panelBrushAddPickColor.AutoSize = true;
            panelBrushAddPickColor.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelBrushAddPickColor.Controls.Add(chkbxColorizeBrush);
            panelBrushAddPickColor.Controls.Add(bttnAddBrushImages);
            panelBrushAddPickColor.Controls.Add(brushImageLoadProgressBar);
            panelBrushAddPickColor.Controls.Add(bttnBrushColor);
            #endregion

            #region bttnChooseEffectSettings
            bttnChooseEffectSettings.Image = Resources.EffectSettingsIcon;
            bttnChooseEffectSettings.Location = new Point(123, 0);
            bttnChooseEffectSettings.Margin = new Padding(0, 0, 0, 0);
            bttnChooseEffectSettings.Size = new Size(30, 30);
            bttnChooseEffectSettings.TabIndex = 1;
            bttnChooseEffectSettings.UseVisualStyleBackColor = true;
            bttnChooseEffectSettings.Click += new EventHandler(BttnChooseEffectSettings_Click);
            bttnChooseEffectSettings.MouseEnter += new EventHandler(BttnChooseEffectSettings_MouseEnter);
            bttnChooseEffectSettings.MouseLeave += new EventHandler(BttnChooseEffectSettings_MouseLeave);
            #endregion

            #region chkbxColorizeBrush
            chkbxColorizeBrush.AutoSize = true;
            chkbxColorizeBrush.Checked = true;
            chkbxColorizeBrush.CheckState = System.Windows.Forms.CheckState.Checked;
            chkbxColorizeBrush.Location = new Point(10, 48);
            chkbxColorizeBrush.Size = new Size(93, 17);
            chkbxColorizeBrush.TabIndex = 12;
            chkbxColorizeBrush.UseVisualStyleBackColor = true;
            chkbxColorizeBrush.CheckedChanged += new EventHandler(ChkbxColorizeBrush_CheckedChanged);
            chkbxColorizeBrush.MouseEnter += new EventHandler(ChkbxColorizeBrush_MouseEnter);
            #endregion

            #region bttnAddBrushImages
            bttnAddBrushImages.Image = Resources.AddBrushIcon;
            bttnAddBrushImages.TextImageRelation = TextImageRelation.ImageBeforeText;
            bttnAddBrushImages.Location = new Point(0, 3);
            bttnAddBrushImages.Size = new Size(153, 32);
            bttnAddBrushImages.TabIndex = 11;
            bttnAddBrushImages.UseVisualStyleBackColor = true;
            bttnAddBrushImages.Click += new EventHandler(BttnAddBrushImages_Click);
            bttnAddBrushImages.MouseEnter += new EventHandler(BttnAddBrushImages_MouseEnter);
            #endregion

            #region brushImageLoadProgressBar
            brushImageLoadProgressBar.Location = new Point(0, 12);
            brushImageLoadProgressBar.Margin = new Padding(0, 3, 0, 3);
            brushImageLoadProgressBar.Size = new Size(153, 23);
            #endregion

            #region bttnBrushColor
            bttnBrushColor.Anchor = AnchorStyles.Top;
            bttnBrushColor.BackColor = Color.Black;
            bttnBrushColor.ForeColor = Color.White;
            bttnBrushColor.Location = new Point(109, 41);
            bttnBrushColor.Margin = new Padding(3, 3, 0, 3);
            bttnBrushColor.Size = new Size(47, 28);
            bttnBrushColor.TabIndex = 13;
            bttnBrushColor.UseVisualStyleBackColor = false;
            bttnBrushColor.Click += new EventHandler(BttnBrushColor_Click);
            bttnBrushColor.MouseEnter += new EventHandler(BttnBrushColor_MouseEnter);
            #endregion

            #region txtColorInfluence
            txtColorInfluence.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            txtColorInfluence.BackColor = Color.Transparent;
            txtColorInfluence.Location = new Point(10, 449);
            txtColorInfluence.Margin = new Padding(0, 0, 0, 0);
            txtColorInfluence.Size = new Size(149, 17);
            txtColorInfluence.TabIndex = 0;
            txtColorInfluence.TextAlign = ContentAlignment.MiddleCenter;
            #endregion

            #region sliderColorInfluence
            sliderColorInfluence.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            sliderColorInfluence.LargeChange = 1;
            sliderColorInfluence.Maximum = 100;
            sliderColorInfluence.Minimum = 0;
            sliderColorInfluence.TickStyle = TickStyle.None;
            sliderColorInfluence.ValueChanged += new EventHandler(SliderColorInfluence_ValueChanged);
            sliderColorInfluence.MouseEnter += new EventHandler(SliderColorInfluence_MouseEnter);
            #endregion

            #region panelColorInfluenceHSV
            panelColorInfluenceHSV.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelColorInfluenceHSV.BackColor = Color.Transparent;
            panelColorInfluenceHSV.FlowDirection = FlowDirection.LeftToRight;
            panelColorInfluenceHSV.Location = new Point(0, 497);
            panelColorInfluenceHSV.Margin = new Padding(0, 3, 0, 3);
            panelColorInfluenceHSV.Size = new Size(155, 28);
            panelColorInfluenceHSV.TabIndex = 15;
            panelColorInfluenceHSV.Controls.Add(chkbxColorInfluenceHue);
            panelColorInfluenceHSV.Controls.Add(chkbxColorInfluenceSat);
            panelColorInfluenceHSV.Controls.Add(chkbxColorInfluenceVal);
            #endregion

            #region chkbxColorInfluenceHue
            chkbxColorInfluenceHue.AutoSize = true;
            chkbxColorInfluenceHue.Location = new Point(0, 0);
            chkbxColorInfluenceHue.Size = new Size(32, 17);
            chkbxColorInfluenceHue.TabIndex = 16;
            chkbxColorInfluenceHue.Checked = true;
            chkbxColorInfluenceHue.CheckState = System.Windows.Forms.CheckState.Checked;
            chkbxColorInfluenceHue.UseVisualStyleBackColor = true;
            chkbxColorInfluenceHue.MouseEnter += new EventHandler(ChkbxColorInfluenceHue_MouseEnter);
            #endregion

            #region chkbxColorInfluenceSat
            chkbxColorInfluenceSat.AutoSize = true;
            chkbxColorInfluenceSat.Location = new Point(0, 0);
            chkbxColorInfluenceSat.Size = new Size(32, 17);
            chkbxColorInfluenceSat.TabIndex = 17;
            chkbxColorInfluenceSat.Checked = true;
            chkbxColorInfluenceSat.CheckState = System.Windows.Forms.CheckState.Checked;
            chkbxColorInfluenceSat.UseVisualStyleBackColor = true;
            chkbxColorInfluenceSat.MouseEnter += new EventHandler(ChkbxColorInfluenceSat_MouseEnter);
            #endregion

            #region chkbxColorInfluenceVal
            chkbxColorInfluenceVal.AutoSize = true;
            chkbxColorInfluenceVal.Location = new Point(0, 0);
            chkbxColorInfluenceVal.Size = new Size(32, 17);
            chkbxColorInfluenceVal.TabIndex = 18;
            chkbxColorInfluenceVal.UseVisualStyleBackColor = true;
            chkbxColorInfluenceVal.MouseEnter += new EventHandler(ChkbxColorInfluenceVal_MouseEnter);
            #endregion

            #region cmbxBlendMode
            cmbxBlendMode.Font = detailsFont;
            cmbxBlendMode.IntegralHeight = false;
            cmbxBlendMode.ItemHeight = 13;
            cmbxBlendMode.Location = new Point(3, 477);
            cmbxBlendMode.Margin = new Padding(0, 3, 0, 3);
            cmbxBlendMode.MaxDropDownItems = 3;
            cmbxBlendMode.Size = new Size(153, 21);
            cmbxBlendMode.TabIndex = 19;
            cmbxBlendMode.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            cmbxBlendMode.BackColor = Color.White;
            cmbxBlendMode.DropDownHeight = 140;
            cmbxBlendMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbxBlendMode.DropDownWidth = 20;
            cmbxBlendMode.FormattingEnabled = true;
            cmbxBlendMode.SelectedIndexChanged += new EventHandler(BttnBlendMode_SelectedIndexChanged);
            cmbxBlendMode.MouseEnter += new EventHandler(BttnBlendMode_MouseEnter);
            #endregion

            #region txtBrushOpacity
            txtBrushOpacity.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            txtBrushOpacity.BackColor = Color.Transparent;
            txtBrushOpacity.Location = new Point(7, 522);
            txtBrushOpacity.Margin = new Padding(0, 0, 0, 0);
            txtBrushOpacity.Size = new Size(149, 17);
            txtBrushOpacity.TabIndex = 0;
            txtBrushOpacity.TextAlign = ContentAlignment.MiddleCenter;
            #endregion

            #region sliderBrushOpacity
            sliderBrushOpacity.AutoSize = false;
            sliderBrushOpacity.Location = new Point(6, 542);
            sliderBrushOpacity.Margin = new Padding(0, 3, 0, 3);
            sliderBrushOpacity.Size = new Size(150, 25);
            sliderBrushOpacity.TabIndex = 20;
            sliderBrushOpacity.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            sliderBrushOpacity.LargeChange = 1;
            sliderBrushOpacity.Maximum = 255;
            sliderBrushOpacity.TickStyle = TickStyle.None;
            sliderBrushOpacity.Value = 255;
            sliderBrushOpacity.ValueChanged += new EventHandler(SliderBrushOpacity_ValueChanged);
            sliderBrushOpacity.MouseEnter += new EventHandler(SliderBrushOpacity_MouseEnter);
            #endregion

            #region txtBrushFlow
            txtBrushFlow.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            txtBrushFlow.BackColor = Color.Transparent;
            txtBrushFlow.Location = new Point(7, 570);
            txtBrushFlow.Margin = new Padding(0, 0, 0, 0);
            txtBrushFlow.Size = new Size(149, 17);
            txtBrushFlow.TabIndex = 0;
            txtBrushFlow.TextAlign = ContentAlignment.MiddleCenter;
            #endregion

            #region sliderBrushFlow
            sliderBrushFlow.AutoSize = false;
            sliderBrushFlow.Location = new Point(6, 590);
            sliderBrushFlow.Margin = new Padding(0, 3, 0, 3);
            sliderBrushFlow.Size = new Size(150, 25);
            sliderBrushFlow.TabIndex = 21;
            sliderBrushFlow.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            sliderBrushFlow.LargeChange = 1;
            sliderBrushFlow.Maximum = 255;
            sliderBrushFlow.TickStyle = TickStyle.None;
            sliderBrushFlow.Value = 255;
            sliderBrushFlow.ValueChanged += new EventHandler(SliderBrushFlow_ValueChanged);
            sliderBrushFlow.MouseEnter += new EventHandler(SliderBrushFlow_MouseEnter);
            #endregion

            #region txtBrushRotation
            txtBrushRotation.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            txtBrushRotation.BackColor = Color.Transparent;
            txtBrushRotation.Location = new Point(7, 618);
            txtBrushRotation.Margin = new Padding(0, 0, 0, 0);
            txtBrushRotation.Size = new Size(149, 17);
            txtBrushRotation.TabIndex = 0;
            txtBrushRotation.TextAlign = ContentAlignment.MiddleCenter;
            #endregion

            #region sliderBrushRotation
            sliderBrushRotation.AutoSize = false;
            sliderBrushRotation.Location = new Point(6, 638);
            sliderBrushRotation.Margin = new Padding(0, 3, 0, 3);
            sliderBrushRotation.Size = new Size(150, 25);
            sliderBrushRotation.TabIndex = 22;
            sliderBrushRotation.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            sliderBrushRotation.LargeChange = 1;
            sliderBrushRotation.Maximum = 180;
            sliderBrushRotation.Minimum = -180;
            sliderBrushRotation.TickStyle = TickStyle.None;
            sliderBrushRotation.ValueChanged += new EventHandler(SliderBrushRotation_ValueChanged);
            sliderBrushRotation.MouseEnter += new EventHandler(SliderBrushRotation_MouseEnter);
            #endregion

            #region txtBrushSize
            txtBrushSize.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            txtBrushSize.BackColor = Color.Transparent;
            txtBrushSize.Location = new Point(7, 666);
            txtBrushSize.Margin = new Padding(0, 0, 0, 0);
            txtBrushSize.Size = new Size(149, 17);
            txtBrushSize.TabIndex = 0;
            txtBrushSize.TextAlign = ContentAlignment.MiddleCenter;
            #endregion

            #region sliderBrushSize
            sliderBrushSize.AutoSize = false;
            sliderBrushSize.Location = new Point(6, 686);
            sliderBrushSize.Margin = new Padding(0, 3, 0, 3);
            sliderBrushSize.Size = new Size(150, 25);
            sliderBrushSize.TabIndex = 23;
            sliderBrushSize.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            sliderBrushSize.LargeChange = 1;
            sliderBrushSize.Maximum = 1000;
            sliderBrushSize.Minimum = 1;
            sliderBrushSize.TickStyle = TickStyle.None;
            sliderBrushSize.Value = 10;
            sliderBrushSize.ValueChanged += new EventHandler(SliderBrushSize_ValueChanged);
            sliderBrushSize.MouseEnter += new EventHandler(SliderBrushSize_MouseEnter);
            #endregion

            #region bttnSpecialSettings
            bttnSpecialSettings.Location = new Point(0, 584);
            bttnSpecialSettings.Margin = new Padding(0, 3, 0, 3);
            bttnSpecialSettings.Size = new Size(155, 23);
            bttnSpecialSettings.TabIndex = 18;
            bttnSpecialSettings.TextAlign = ContentAlignment.MiddleLeft;
            bttnSpecialSettings.BackColor = Color.Black;
            bttnSpecialSettings.ForeColor = Color.WhiteSmoke;
            bttnSpecialSettings.UseVisualStyleBackColor = false;
            #endregion

            #region panelSpecialSettings
            panelSpecialSettings.BackColor = SystemColors.Control;
            panelSpecialSettings.FlowDirection = FlowDirection.TopDown;
            panelSpecialSettings.Location = new Point(0, 613);
            panelSpecialSettings.Margin = new Padding(0, 3, 0, 3);
            panelSpecialSettings.Size = new Size(156, 196);
            panelSpecialSettings.TabIndex = 19;
            panelSpecialSettings.AutoSize = true;
            panelSpecialSettings.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelSpecialSettings.Controls.Add(panelChosenEffect);
            panelSpecialSettings.Controls.Add(txtMinDrawDistance);
            panelSpecialSettings.Controls.Add(sliderMinDrawDistance);
            panelSpecialSettings.Controls.Add(chkbxAutomaticBrushDensity);
            panelSpecialSettings.Controls.Add(txtBrushDensity);
            panelSpecialSettings.Controls.Add(sliderBrushDensity);
            panelSpecialSettings.Controls.Add(cmbxBrushSmoothing);
            panelSpecialSettings.Controls.Add(cmbxSymmetry);
            panelSpecialSettings.Controls.Add(chkbxSeamlessDrawing);
            panelSpecialSettings.Controls.Add(chkbxOrientToMouse);
            panelSpecialSettings.Controls.Add(chkbxDitherDraw);
            panelSpecialSettings.Controls.Add(chkbxLockAlpha);
            panelSpecialSettings.Controls.Add(panelRGBLocks);
            panelSpecialSettings.Controls.Add(panelHSVLocks);
            #endregion

            #region panelChosenEffect
            panelChosenEffect.Location = new Point(0, 3);
            panelChosenEffect.Margin = new Padding(0, 3, 0, 0);
            panelChosenEffect.Size = new Size(156, 33);
            panelChosenEffect.TabIndex = 20;
            panelChosenEffect.Controls.Add(bttnChooseEffectSettings);
            panelChosenEffect.Controls.Add(cmbxChosenEffect);
            #endregion

            #region cmbxChosenEffect
            cmbxChosenEffect.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            cmbxChosenEffect.BackColor = Color.White;
            cmbxChosenEffect.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
            cmbxChosenEffect.DropDownHeight = 140;
            cmbxChosenEffect.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbxChosenEffect.DropDownWidth = 20;
            cmbxChosenEffect.FormattingEnabled = true;
            cmbxChosenEffect.Font = detailsFont;
            cmbxChosenEffect.IntegralHeight = false;
            cmbxChosenEffect.ItemHeight = 24;
            cmbxChosenEffect.Location = new Point(3, 0);
            cmbxChosenEffect.Margin = new Padding(0, 3, 0, 3);
            cmbxChosenEffect.MaxDropDownItems = 100;
            cmbxChosenEffect.Size = new Size(121, 21);
            cmbxChosenEffect.TabIndex = 0;
            cmbxChosenEffect.DrawItem += new System.Windows.Forms.DrawItemEventHandler(CmbxChosenEffect_DrawItem);
            cmbxChosenEffect.MouseEnter += new EventHandler(CmbxChosenEffect_MouseEnter);
            cmbxChosenEffect.MouseLeave += new EventHandler(CmbxChosenEffect_MouseLeave);
            cmbxChosenEffect.SelectedIndexChanged += new EventHandler(CmbxChosenEffect_SelectedIndexChanged);
            #endregion

            #region txtMinDrawDistance
            txtMinDrawDistance.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            txtMinDrawDistance.BackColor = Color.Transparent;
            txtMinDrawDistance.Location = new Point(4, 32);
            txtMinDrawDistance.Size = new Size(149, 17);
            txtMinDrawDistance.TabIndex = 0;
            txtMinDrawDistance.TextAlign = ContentAlignment.MiddleCenter;

            #endregion

            #region sliderMinDrawDistance
            sliderMinDrawDistance.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            sliderMinDrawDistance.LargeChange = 1;
            sliderMinDrawDistance.Maximum = 500;
            sliderMinDrawDistance.TickStyle = TickStyle.None;
            sliderMinDrawDistance.AutoSize = false;
            sliderMinDrawDistance.Location = new Point(3, 52);
            sliderMinDrawDistance.Size = new Size(150, 25);
            sliderMinDrawDistance.TabIndex = 21;
            sliderMinDrawDistance.ValueChanged += new EventHandler(SliderMinDrawDistance_ValueChanged);
            sliderMinDrawDistance.MouseEnter += new EventHandler(SliderMinDrawDistance_MouseEnter);
            #endregion

            #region chkbxAutomaticBrushDensity
            chkbxAutomaticBrushDensity.Text = "Manage density automatically";
            chkbxAutomaticBrushDensity.Checked = true;
            chkbxAutomaticBrushDensity.MouseEnter += new EventHandler(AutomaticBrushDensity_MouseEnter);
            chkbxAutomaticBrushDensity.CheckedChanged += new EventHandler(AutomaticBrushDensity_CheckedChanged);
            chkbxAutomaticBrushDensity.TabIndex = 20;
            #endregion

            #region txtBrushDensity
            txtBrushDensity.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            txtBrushDensity.BackColor = Color.Transparent;
            txtBrushDensity.Location = new Point(4, 48);
            txtBrushDensity.Size = new Size(149, 17);
            txtBrushDensity.TabIndex = 0;
            txtBrushDensity.TextAlign = ContentAlignment.MiddleCenter;
            #endregion

            #region sliderBrushDensity
            sliderBrushDensity.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            sliderBrushDensity.LargeChange = 1;
            sliderBrushDensity.Maximum = 50;
            sliderBrushDensity.TickStyle = TickStyle.None;
            sliderBrushDensity.Value = 10;
            sliderBrushDensity.Enabled = false;
            sliderBrushDensity.AutoSize = false;
            sliderBrushDensity.Location = new Point(3, 68);
            sliderBrushDensity.Size = new Size(150, 25);
            sliderBrushDensity.TabIndex = 22;
            sliderBrushDensity.ValueChanged += new EventHandler(SliderBrushDensity_ValueChanged);
            sliderBrushDensity.MouseEnter += new EventHandler(SliderBrushDensity_MouseEnter);
            #endregion

            #region cmbxSymmetry
            cmbxSymmetry.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            cmbxSymmetry.BackColor = Color.White;
            cmbxSymmetry.DropDownHeight = 140;
            cmbxSymmetry.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbxSymmetry.DropDownWidth = 20;
            cmbxSymmetry.FormattingEnabled = true;
            cmbxSymmetry.Font = detailsFont;
            cmbxSymmetry.IntegralHeight = false;
            cmbxSymmetry.ItemHeight = 13;
            cmbxSymmetry.Location = new Point(3, 99);
            cmbxSymmetry.Margin = new Padding(0, 3, 0, 3);
            cmbxSymmetry.MaxDropDownItems = 9;
            cmbxSymmetry.Size = new Size(153, 21);
            cmbxSymmetry.TabIndex = 24;
            cmbxSymmetry.MouseEnter += new EventHandler(BttnSymmetry_MouseEnter);
            #endregion

            #region cmbxBrushSmoothing
            cmbxBrushSmoothing.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            cmbxBrushSmoothing.BackColor = Color.White;
            cmbxBrushSmoothing.DropDownHeight = 140;
            cmbxBrushSmoothing.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbxBrushSmoothing.DropDownWidth = 20;
            cmbxBrushSmoothing.FormattingEnabled = true;
            cmbxBrushSmoothing.Font = detailsFont;
            cmbxBrushSmoothing.IntegralHeight = false;
            cmbxBrushSmoothing.ItemHeight = 13;
            cmbxBrushSmoothing.Location = new Point(3, 126);
            cmbxBrushSmoothing.Margin = new Padding(0, 3, 0, 3);
            cmbxBrushSmoothing.MaxDropDownItems = 9;
            cmbxBrushSmoothing.Size = new Size(153, 21);
            cmbxBrushSmoothing.TabIndex = 23;
            cmbxBrushSmoothing.MouseEnter += new EventHandler(BttnBrushSmoothing_MouseEnter);
            #endregion

            #region chkbxSeamlessDrawing
            chkbxSeamlessDrawing.AutoSize = true;
            chkbxSeamlessDrawing.Location = new Point(3, 153);
            chkbxSeamlessDrawing.Size = new Size(118, 17);
            chkbxSeamlessDrawing.TabIndex = 25;
            chkbxSeamlessDrawing.UseVisualStyleBackColor = true;
            chkbxSeamlessDrawing.MouseEnter += new EventHandler(ChkbxSeamlessDrawing_MouseEnter);
            #endregion

            #region chkbxOrientToMouse
            chkbxOrientToMouse.AutoSize = true;
            chkbxOrientToMouse.Location = new Point(3, 176);
            chkbxOrientToMouse.Size = new Size(118, 17);
            chkbxOrientToMouse.TabIndex = 26;
            chkbxOrientToMouse.UseVisualStyleBackColor = true;
            chkbxOrientToMouse.MouseEnter += new EventHandler(ChkbxOrientToMouse_MouseEnter);
            #endregion

            #region chkbxDitherDraw
            chkbxDitherDraw.AutoSize = true;
            chkbxDitherDraw.Location = new Point(3, 199);
            chkbxDitherDraw.Size = new Size(80, 17);
            chkbxDitherDraw.TabIndex = 27;
            chkbxDitherDraw.UseVisualStyleBackColor = true;
            chkbxDitherDraw.MouseEnter += new EventHandler(ChkbxDitherDraw_MouseEnter);
            #endregion

            #region panelRGBLocks
            panelRGBLocks.Location = new Point(0, 255);
            panelRGBLocks.Margin = new Padding(0, 3, 0, 0);
            panelRGBLocks.Size = new Size(156, 22);
            panelRGBLocks.TabIndex = 29;
            panelRGBLocks.Controls.Add(chkbxLockR);
            panelRGBLocks.Controls.Add(chkbxLockG);
            panelRGBLocks.Controls.Add(chkbxLockB);
            #endregion

            #region panelHSVLocks
            panelHSVLocks.Location = new Point(0, 277);
            panelHSVLocks.Margin = new Padding(0, 3, 0, 0);
            panelHSVLocks.Size = new Size(156, 22);
            panelHSVLocks.TabIndex = 30;
            panelHSVLocks.Controls.Add(chkbxLockHue);
            panelHSVLocks.Controls.Add(chkbxLockSat);
            panelHSVLocks.Controls.Add(chkbxLockVal);
            #endregion

            #region chkbxLockAlpha
            chkbxLockAlpha.AutoSize = true;
            chkbxLockAlpha.Location = new Point(3, 222);
            chkbxLockAlpha.Size = new Size(80, 17);
            chkbxLockAlpha.TabIndex = 28;
            chkbxLockAlpha.UseVisualStyleBackColor = true;
            chkbxLockAlpha.MouseEnter += new EventHandler(ChkbxLockAlpha_MouseEnter);
            #endregion

            #region chkbxLockR
            chkbxLockR.AutoSize = true;
            chkbxLockR.Location = new Point(3, 0);
            chkbxLockR.Size = new Size(80, 17);
            chkbxLockR.TabIndex = 1;
            chkbxLockR.UseVisualStyleBackColor = true;
            chkbxLockR.MouseEnter += new EventHandler(ChkbxLockR_MouseEnter);
            #endregion

            #region chkbxLockG
            chkbxLockG.AutoSize = true;
            chkbxLockG.Location = new Point(44, 0);
            chkbxLockG.Size = new Size(80, 17);
            chkbxLockG.TabIndex = 2;
            chkbxLockG.UseVisualStyleBackColor = true;
            chkbxLockG.MouseEnter += new EventHandler(ChkbxLockG_MouseEnter);
            #endregion

            #region chkbxLockB
            chkbxLockB.AutoSize = true;
            chkbxLockB.Location = new Point(82, 0);
            chkbxLockB.Size = new Size(80, 17);
            chkbxLockB.TabIndex = 3;
            chkbxLockB.UseVisualStyleBackColor = true;
            chkbxLockB.MouseEnter += new EventHandler(ChkbxLockB_MouseEnter);
            #endregion

            #region chkbxLockHue
            chkbxLockHue.AutoSize = true;
            chkbxLockHue.Location = new Point(3, 0);
            chkbxLockHue.Size = new Size(80, 17);
            chkbxLockHue.TabIndex = 1;
            chkbxLockHue.UseVisualStyleBackColor = true;
            chkbxLockHue.MouseEnter += new EventHandler(ChkbxLockHue_MouseEnter);
            #endregion

            #region chkbxLockSat
            chkbxLockSat.AutoSize = true;
            chkbxLockSat.Location = new Point(44, 0);
            chkbxLockSat.Size = new Size(80, 17);
            chkbxLockSat.TabIndex = 2;
            chkbxLockSat.UseVisualStyleBackColor = true;
            chkbxLockSat.MouseEnter += new EventHandler(ChkbxLockSat_MouseEnter);
            #endregion

            #region chkbxLockVal
            chkbxLockVal.AutoSize = true;
            chkbxLockVal.Location = new Point(82, 0);
            chkbxLockVal.Size = new Size(80, 17);
            chkbxLockVal.TabIndex = 3;
            chkbxLockVal.UseVisualStyleBackColor = true;
            chkbxLockVal.MouseEnter += new EventHandler(ChkbxLockVal_MouseEnter);
            #endregion

            #region bttnJitterBasicsControls
            bttnJitterBasicsControls.BackColor = Color.Black;
            bttnJitterBasicsControls.ForeColor = Color.WhiteSmoke;
            bttnJitterBasicsControls.Location = new Point(0, 815);
            bttnJitterBasicsControls.Margin = new Padding(0, 3, 0, 3);
            bttnJitterBasicsControls.Size = new Size(155, 23);
            bttnJitterBasicsControls.TabIndex = 27;
            bttnJitterBasicsControls.TextAlign = ContentAlignment.MiddleLeft;
            bttnJitterBasicsControls.UseVisualStyleBackColor = false;
            #endregion

            #region panelJitterBasics
            panelJitterBasics.BackColor = SystemColors.Control;
            panelJitterBasics.AutoSize = true;
            panelJitterBasics.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelJitterBasics.FlowDirection = FlowDirection.TopDown;
            panelJitterBasics.Location = new Point(0, 844);
            panelJitterBasics.Margin = new Padding(0, 3, 0, 3);
            panelJitterBasics.Size = new Size(156, 336);
            panelJitterBasics.TabIndex = 28;
            panelJitterBasics.Controls.Add(txtRandMinSize);
            panelJitterBasics.Controls.Add(sliderRandMinSize);
            panelJitterBasics.Controls.Add(txtRandMaxSize);
            panelJitterBasics.Controls.Add(sliderRandMaxSize);
            panelJitterBasics.Controls.Add(txtRandRotLeft);
            panelJitterBasics.Controls.Add(sliderRandRotLeft);
            panelJitterBasics.Controls.Add(txtRandRotRight);
            panelJitterBasics.Controls.Add(sliderRandRotRight);
            panelJitterBasics.Controls.Add(txtRandFlowLoss);
            panelJitterBasics.Controls.Add(sliderRandFlowLoss);
            panelJitterBasics.Controls.Add(txtRandHorzShift);
            panelJitterBasics.Controls.Add(sliderRandHorzShift);
            panelJitterBasics.Controls.Add(txtRandVertShift);
            panelJitterBasics.Controls.Add(sliderRandVertShift);
            #endregion

            #region txtRandMinSize
            txtRandMinSize.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            txtRandMinSize.BackColor = Color.Transparent;
            txtRandMinSize.Location = new Point(3, 0);
            txtRandMinSize.Size = new Size(149, 17);
            txtRandMinSize.TabIndex = 0;
            txtRandMinSize.TextAlign = ContentAlignment.MiddleCenter;
            #endregion

            #region sliderRandMinSize
            sliderRandMinSize.AutoSize = false;
            sliderRandMinSize.Location = new Point(3, 20);
            sliderRandMinSize.Size = new Size(150, 25);
            sliderRandMinSize.TabIndex = 29;
            sliderRandMinSize.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            sliderRandMinSize.LargeChange = 1;
            sliderRandMinSize.Maximum = 1000;
            sliderRandMinSize.TickStyle = TickStyle.None;
            sliderRandMinSize.ValueChanged += new EventHandler(SliderRandMinSize_ValueChanged);
            sliderRandMinSize.MouseEnter += new EventHandler(SliderRandMinSize_MouseEnter);
            #endregion

            #region txtRandMaxSize
            txtRandMaxSize.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            txtRandMaxSize.BackColor = Color.Transparent;
            txtRandMaxSize.Location = new Point(3, 48);
            txtRandMaxSize.Size = new Size(149, 17);
            txtRandMaxSize.TabIndex = 0;
            txtRandMaxSize.TextAlign = ContentAlignment.MiddleCenter;
            #endregion

            #region sliderRandMaxSize
            sliderRandMaxSize.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            sliderRandMaxSize.AutoSize = false;
            sliderRandMaxSize.Location = new Point(3, 68);
            sliderRandMaxSize.Size = new Size(150, 25);
            sliderRandMaxSize.TabIndex = 30;
            sliderRandMaxSize.LargeChange = 1;
            sliderRandMaxSize.Maximum = 1000;
            sliderRandMaxSize.TickStyle = TickStyle.None;
            sliderRandMaxSize.ValueChanged += new EventHandler(SliderRandMaxSize_ValueChanged);
            sliderRandMaxSize.MouseEnter += new EventHandler(SliderRandMaxSize_MouseEnter);
            #endregion

            #region txtRandRotLeft
            txtRandRotLeft.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            txtRandRotLeft.BackColor = Color.Transparent;
            txtRandRotLeft.Location = new Point(4, 96);
            txtRandRotLeft.Size = new Size(149, 17);
            txtRandRotLeft.TabIndex = 0;
            txtRandRotLeft.TextAlign = ContentAlignment.MiddleCenter;
            #endregion

            #region sliderRandRotLeft
            sliderRandRotLeft.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            sliderRandRotLeft.AutoSize = false;
            sliderRandRotLeft.Location = new Point(3, 116);
            sliderRandRotLeft.Size = new Size(150, 25);
            sliderRandRotLeft.TabIndex = 31;
            sliderRandRotLeft.LargeChange = 1;
            sliderRandRotLeft.Maximum = 180;
            sliderRandRotLeft.TickStyle = TickStyle.None;
            sliderRandRotLeft.ValueChanged += new EventHandler(SliderRandRotLeft_ValueChanged);
            sliderRandRotLeft.MouseEnter += new EventHandler(SliderRandRotLeft_MouseEnter);
            #endregion

            #region txtRandRotRight
            txtRandRotRight.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            txtRandRotRight.BackColor = Color.Transparent;
            txtRandRotRight.Location = new Point(4, 144);
            txtRandRotRight.Size = new Size(149, 17);
            txtRandRotRight.TabIndex = 0;
            txtRandRotRight.TextAlign = ContentAlignment.MiddleCenter;
            #endregion

            #region sliderRandRotRight
            sliderRandRotRight.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            sliderRandRotRight.AutoSize = false;
            sliderRandRotRight.Location = new Point(3, 164);
            sliderRandRotRight.Size = new Size(150, 25);
            sliderRandRotRight.TabIndex = 32;
            sliderRandRotRight.LargeChange = 1;
            sliderRandRotRight.Maximum = 180;
            sliderRandRotRight.TickStyle = TickStyle.None;
            sliderRandRotRight.ValueChanged += new EventHandler(SliderRandRotRight_ValueChanged);
            sliderRandRotRight.MouseEnter += new EventHandler(SliderRandRotRight_MouseEnter);
            #endregion

            #region txtRandFlowLoss
            txtRandFlowLoss.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            txtRandFlowLoss.BackColor = Color.Transparent;
            txtRandFlowLoss.Location = new Point(4, 192);
            txtRandFlowLoss.Size = new Size(149, 17);
            txtRandFlowLoss.TabIndex = 0;
            txtRandFlowLoss.TextAlign = ContentAlignment.MiddleCenter;
            #endregion

            #region sliderRandFlowLoss
            sliderRandFlowLoss.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            sliderRandFlowLoss.AutoSize = false;
            sliderRandFlowLoss.Location = new Point(3, 212);
            sliderRandFlowLoss.Size = new Size(150, 25);
            sliderRandFlowLoss.TabIndex = 33;
            sliderRandFlowLoss.LargeChange = 1;
            sliderRandFlowLoss.Maximum = 255;
            sliderRandFlowLoss.TickStyle = TickStyle.None;
            sliderRandFlowLoss.ValueChanged += new EventHandler(SliderRandFlowLoss_ValueChanged);
            sliderRandFlowLoss.MouseEnter += new EventHandler(SliderRandFlowLoss_MouseEnter);
            #endregion

            #region txtRandHorzShift
            txtRandHorzShift.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            txtRandHorzShift.BackColor = Color.Transparent;
            txtRandHorzShift.Location = new Point(3, 240);
            txtRandHorzShift.Size = new Size(149, 17);
            txtRandHorzShift.TabIndex = 0;
            txtRandHorzShift.TextAlign = ContentAlignment.MiddleCenter;
            #endregion

            #region sliderRandHorzShift
            sliderRandHorzShift.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            sliderRandHorzShift.AutoSize = false;
            sliderRandHorzShift.Location = new Point(3, 260);
            sliderRandHorzShift.Size = new Size(150, 25);
            sliderRandHorzShift.TabIndex = 34;
            sliderRandHorzShift.LargeChange = 1;
            sliderRandHorzShift.Maximum = 100;
            sliderRandHorzShift.TickStyle = TickStyle.None;
            sliderRandHorzShift.ValueChanged += new EventHandler(SliderRandHorzShift_ValueChanged);
            sliderRandHorzShift.MouseEnter += new EventHandler(SliderRandHorzShift_MouseEnter);
            #endregion

            #region txtRandVertShift
            txtRandVertShift.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            txtRandVertShift.BackColor = Color.Transparent;
            txtRandVertShift.Location = new Point(3, 288);
            txtRandVertShift.Size = new Size(149, 17);
            txtRandVertShift.TabIndex = 0;
            txtRandVertShift.TextAlign = ContentAlignment.MiddleCenter;
            #endregion

            #region sliderRandVertShift
            sliderRandVertShift.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            sliderRandVertShift.AutoSize = false;
            sliderRandVertShift.Location = new Point(3, 308);
            sliderRandVertShift.Size = new Size(150, 25);
            sliderRandVertShift.TabIndex = 35;
            sliderRandVertShift.LargeChange = 1;
            sliderRandVertShift.Maximum = 100;
            sliderRandVertShift.TickStyle = TickStyle.None;
            sliderRandVertShift.ValueChanged += new EventHandler(SliderRandVertShift_ValueChanged);
            sliderRandVertShift.MouseEnter += new EventHandler(SliderRandVertShift_MouseEnter);
            #endregion

            #region bttnJitterColorControls
            bttnJitterColorControls.BackColor = Color.Black;
            bttnJitterColorControls.ForeColor = Color.WhiteSmoke;
            bttnJitterColorControls.Location = new Point(0, 1186);
            bttnJitterColorControls.Margin = new Padding(0, 3, 0, 3);
            bttnJitterColorControls.Size = new Size(155, 23);
            bttnJitterColorControls.TabIndex = 36;
            bttnJitterColorControls.TextAlign = ContentAlignment.MiddleLeft;
            bttnJitterColorControls.UseVisualStyleBackColor = false;
            #endregion

            #region panelJitterColor
            panelJitterColor.BackColor = SystemColors.Control;
            panelJitterColor.AutoSize = true;
            panelJitterColor.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelJitterColor.FlowDirection = FlowDirection.TopDown;
            panelJitterColor.Location = new Point(0, 1215);
            panelJitterColor.Margin = new Padding(0, 3, 0, 3);
            panelJitterColor.Size = new Size(156, 474);
            panelJitterColor.TabIndex = 37;
            panelJitterColor.Controls.Add(txtJitterRed);
            panelJitterColor.Controls.Add(sliderJitterMinRed);
            panelJitterColor.Controls.Add(sliderJitterMaxRed);
            panelJitterColor.Controls.Add(txtJitterGreen);
            panelJitterColor.Controls.Add(sliderJitterMinGreen);
            panelJitterColor.Controls.Add(sliderJitterMaxGreen);
            panelJitterColor.Controls.Add(txtJitterBlue);
            panelJitterColor.Controls.Add(sliderJitterMinBlue);
            panelJitterColor.Controls.Add(sliderJitterMaxBlue);
            panelJitterColor.Controls.Add(txtJitterHue);
            panelJitterColor.Controls.Add(sliderJitterMinHue);
            panelJitterColor.Controls.Add(sliderJitterMaxHue);
            panelJitterColor.Controls.Add(txtJitterSaturation);
            panelJitterColor.Controls.Add(sliderJitterMinSat);
            panelJitterColor.Controls.Add(sliderJitterMaxSat);
            panelJitterColor.Controls.Add(txtJitterValue);
            panelJitterColor.Controls.Add(sliderJitterMinVal);
            panelJitterColor.Controls.Add(sliderJitterMaxVal);
            #endregion

            #region txtJitterRed
            txtJitterRed.BackColor = Color.Transparent;
            txtJitterRed.Location = new Point(3, 0);
            txtJitterRed.Size = new Size(149, 17);
            txtJitterRed.TabIndex = 0;
            txtJitterRed.TextAlign = ContentAlignment.MiddleCenter;
            #endregion

            #region sliderJitterMinRed
            sliderJitterMinRed.AutoSize = false;
            sliderJitterMinRed.Location = new Point(3, 20);
            sliderJitterMinRed.Size = new Size(150, 25);
            sliderJitterMinRed.TabIndex = 38;
            sliderJitterMinRed.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            sliderJitterMinRed.BackColor = SystemColors.Control;
            sliderJitterMinRed.LargeChange = 1;
            sliderJitterMinRed.Maximum = 100;
            sliderJitterMinRed.TickStyle = TickStyle.None;
            sliderJitterMinRed.ValueChanged += new EventHandler(SliderJitterMinRed_ValueChanged);
            sliderJitterMinRed.MouseEnter += new EventHandler(SliderJitterMinRed_MouseEnter);
            #endregion

            #region sliderJitterMaxRed
            sliderJitterMaxRed.AutoSize = false;
            sliderJitterMaxRed.Location = new Point(3, 51);
            sliderJitterMaxRed.Size = new Size(150, 25);
            sliderJitterMaxRed.TabIndex = 39;
            sliderJitterMaxRed.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            sliderJitterMaxRed.LargeChange = 1;
            sliderJitterMaxRed.Maximum = 100;
            sliderJitterMaxRed.TickStyle = TickStyle.None;
            sliderJitterMaxRed.ValueChanged += new EventHandler(SliderJitterMaxRed_ValueChanged);
            sliderJitterMaxRed.MouseEnter += new EventHandler(SliderJitterMaxRed_MouseEnter);
            #endregion

            #region txtJitterGreen
            txtJitterGreen.BackColor = Color.Transparent;
            txtJitterGreen.Location = new Point(3, 79);
            txtJitterGreen.Size = new Size(149, 17);
            txtJitterGreen.TabIndex = 0;
            txtJitterGreen.TextAlign = ContentAlignment.MiddleCenter;
            #endregion

            #region sliderJitterMinGreen
            sliderJitterMinGreen.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            sliderJitterMinGreen.AutoSize = false;
            sliderJitterMinGreen.Location = new Point(3, 99);
            sliderJitterMinGreen.Size = new Size(150, 25);
            sliderJitterMinGreen.TabIndex = 40;
            sliderJitterMinGreen.LargeChange = 1;
            sliderJitterMinGreen.Maximum = 100;
            sliderJitterMinGreen.TickStyle = TickStyle.None;
            sliderJitterMinGreen.ValueChanged += new EventHandler(SliderJitterMinGreen_ValueChanged);
            sliderJitterMinGreen.MouseEnter += new EventHandler(SliderJitterMinGreen_MouseEnter);
            #endregion

            #region sliderJitterMaxGreen
            sliderJitterMaxGreen.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            sliderJitterMaxGreen.AutoSize = false;
            sliderJitterMaxGreen.Location = new Point(3, 130);
            sliderJitterMaxGreen.Size = new Size(150, 25);
            sliderJitterMaxGreen.TabIndex = 41;
            sliderJitterMaxGreen.LargeChange = 1;
            sliderJitterMaxGreen.Maximum = 100;
            sliderJitterMaxGreen.TickStyle = TickStyle.None;
            sliderJitterMaxGreen.ValueChanged += new EventHandler(SliderJitterMaxGreen_ValueChanged);
            sliderJitterMaxGreen.MouseEnter += new EventHandler(SliderJitterMaxGreen_MouseEnter);
            #endregion

            #region txtJitterBlue
            txtJitterBlue.BackColor = Color.Transparent;
            txtJitterBlue.Location = new Point(3, 158);
            txtJitterBlue.Size = new Size(149, 17);
            txtJitterBlue.TabIndex = 0;
            txtJitterBlue.TextAlign = ContentAlignment.MiddleCenter;
            #endregion

            #region sliderJitterMinBlue
            sliderJitterMinBlue.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            sliderJitterMinBlue.AutoSize = false;
            sliderJitterMinBlue.Location = new Point(3, 178);
            sliderJitterMinBlue.Size = new Size(150, 25);
            sliderJitterMinBlue.TabIndex = 42;
            sliderJitterMinBlue.LargeChange = 1;
            sliderJitterMinBlue.Maximum = 100;
            sliderJitterMinBlue.TickStyle = TickStyle.None;
            sliderJitterMinBlue.ValueChanged += new EventHandler(SliderJitterMinBlue_ValueChanged);
            sliderJitterMinBlue.MouseEnter += new EventHandler(SliderJitterMinBlue_MouseEnter);
            #endregion

            #region sliderJitterMaxBlue
            sliderJitterMaxBlue.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            sliderJitterMaxBlue.AutoSize = false;
            sliderJitterMaxBlue.Location = new Point(3, 209);
            sliderJitterMaxBlue.Size = new Size(150, 25);
            sliderJitterMaxBlue.TabIndex = 43;
            sliderJitterMaxBlue.LargeChange = 1;
            sliderJitterMaxBlue.Maximum = 100;
            sliderJitterMaxBlue.TickStyle = TickStyle.None;
            sliderJitterMaxBlue.ValueChanged += new EventHandler(SliderJitterMaxBlue_ValueChanged);
            sliderJitterMaxBlue.MouseEnter += new EventHandler(SliderJitterMaxBlue_MouseEnter);
            #endregion

            #region txtJitterHue
            txtJitterHue.BackColor = Color.Transparent;
            txtJitterHue.Location = new Point(3, 237);
            txtJitterHue.Size = new Size(149, 17);
            txtJitterHue.TabIndex = 0;
            txtJitterHue.TextAlign = ContentAlignment.MiddleCenter;
            #endregion

            #region sliderJitterMinHue
            sliderJitterMinHue.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            sliderJitterMinHue.AutoSize = false;
            sliderJitterMinHue.Location = new Point(3, 257);
            sliderJitterMinHue.Size = new Size(150, 25);
            sliderJitterMinHue.TabIndex = 44;
            sliderJitterMinHue.LargeChange = 1;
            sliderJitterMinHue.Maximum = 100;
            sliderJitterMinHue.TickStyle = TickStyle.None;
            sliderJitterMinHue.ValueChanged += new EventHandler(SliderJitterMinHue_ValueChanged);
            sliderJitterMinHue.MouseEnter += new EventHandler(SliderJitterMinHue_MouseEnter);
            #endregion

            #region sliderJitterMaxHue
            sliderJitterMaxHue.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            sliderJitterMaxHue.AutoSize = false;
            sliderJitterMaxHue.Location = new Point(3, 288);
            sliderJitterMaxHue.Size = new Size(150, 25);
            sliderJitterMaxHue.TabIndex = 45;
            sliderJitterMaxHue.LargeChange = 1;
            sliderJitterMaxHue.Maximum = 100;
            sliderJitterMaxHue.TickStyle = TickStyle.None;
            sliderJitterMaxHue.ValueChanged += new EventHandler(SliderJitterMaxHue_ValueChanged);
            sliderJitterMaxHue.MouseEnter += new EventHandler(SliderJitterMaxHue_MouseEnter);
            #endregion

            #region txtJitterSaturation
            txtJitterSaturation.BackColor = Color.Transparent;
            txtJitterSaturation.Location = new Point(3, 316);
            txtJitterSaturation.Size = new Size(149, 17);
            txtJitterSaturation.TabIndex = 0;
            txtJitterSaturation.TextAlign = ContentAlignment.MiddleCenter;
            #endregion

            #region sliderJitterMinSat
            sliderJitterMinSat.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            sliderJitterMinSat.AutoSize = false;
            sliderJitterMinSat.Location = new Point(3, 336);
            sliderJitterMinSat.Size = new Size(150, 25);
            sliderJitterMinSat.TabIndex = 46;
            sliderJitterMinSat.LargeChange = 1;
            sliderJitterMinSat.Maximum = 100;
            sliderJitterMinSat.TickStyle = TickStyle.None;
            sliderJitterMinSat.ValueChanged += new EventHandler(SliderJitterMinSat_ValueChanged);
            sliderJitterMinSat.MouseEnter += new EventHandler(SliderJitterMinSat_MouseEnter);
            #endregion

            #region sliderJitterMaxSat
            sliderJitterMaxSat.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            sliderJitterMaxSat.AutoSize = false;
            sliderJitterMaxSat.Location = new Point(3, 367);
            sliderJitterMaxSat.Size = new Size(150, 25);
            sliderJitterMaxSat.TabIndex = 47;
            sliderJitterMaxSat.LargeChange = 1;
            sliderJitterMaxSat.Maximum = 100;
            sliderJitterMaxSat.TickStyle = TickStyle.None;
            sliderJitterMaxSat.ValueChanged += new EventHandler(SliderJitterMaxSat_ValueChanged);
            sliderJitterMaxSat.MouseEnter += new EventHandler(SliderJitterMaxSat_MouseEnter);
            #endregion

            #region txtJitterValue
            txtJitterValue.BackColor = Color.Transparent;
            txtJitterValue.Location = new Point(3, 395);
            txtJitterValue.Size = new Size(149, 17);
            txtJitterValue.TabIndex = 0;
            txtJitterValue.TextAlign = ContentAlignment.MiddleCenter;
            #endregion

            #region sliderJitterMinVal
            sliderJitterMinVal.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            sliderJitterMinVal.AutoSize = false;
            sliderJitterMinVal.Location = new Point(3, 415);
            sliderJitterMinVal.Size = new Size(150, 25);
            sliderJitterMinVal.TabIndex = 48;
            sliderJitterMinVal.LargeChange = 1;
            sliderJitterMinVal.Maximum = 100;
            sliderJitterMinVal.TickStyle = TickStyle.None;
            sliderJitterMinVal.ValueChanged += new EventHandler(SliderJitterMinVal_ValueChanged);
            sliderJitterMinVal.MouseEnter += new EventHandler(SliderJitterMinVal_MouseEnter);
            #endregion

            #region sliderJitterMaxVal
            sliderJitterMaxVal.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            sliderJitterMaxVal.AutoSize = false;
            sliderJitterMaxVal.Location = new Point(3, 446);
            sliderJitterMaxVal.Size = new Size(150, 25);
            sliderJitterMaxVal.TabIndex = 49;
            sliderJitterMaxVal.LargeChange = 1;
            sliderJitterMaxVal.Maximum = 100;
            sliderJitterMaxVal.TickStyle = TickStyle.None;
            sliderJitterMaxVal.ValueChanged += new EventHandler(SliderJitterMaxVal_ValueChanged);
            sliderJitterMaxVal.MouseEnter += new EventHandler(SliderJitterMaxVal_MouseEnter);
            #endregion

            #region bttnShiftBasicsControls
            bttnShiftBasicsControls.BackColor = Color.Black;
            bttnShiftBasicsControls.ForeColor = Color.WhiteSmoke;
            bttnShiftBasicsControls.Location = new Point(0, 1695);
            bttnShiftBasicsControls.Margin = new Padding(0, 3, 0, 3);
            bttnShiftBasicsControls.Size = new Size(155, 23);
            bttnShiftBasicsControls.TabIndex = 50;
            bttnShiftBasicsControls.TextAlign = ContentAlignment.MiddleLeft;
            bttnShiftBasicsControls.UseVisualStyleBackColor = false;
            #endregion

            #region panelShiftBasics
            panelShiftBasics.BackColor = SystemColors.Control;
            panelShiftBasics.AutoSize = true;
            panelShiftBasics.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelShiftBasics.FlowDirection = FlowDirection.TopDown;
            panelShiftBasics.Location = new Point(0, 1724);
            panelShiftBasics.Margin = new Padding(0, 3, 0, 3);
            panelShiftBasics.Size = new Size(156, 144);
            panelShiftBasics.TabIndex = 51;
            panelShiftBasics.Controls.Add(txtShiftSize);
            panelShiftBasics.Controls.Add(sliderShiftSize);
            panelShiftBasics.Controls.Add(txtShiftRotation);
            panelShiftBasics.Controls.Add(sliderShiftRotation);
            panelShiftBasics.Controls.Add(txtShiftFlow);
            panelShiftBasics.Controls.Add(sliderShiftFlow);
            #endregion

            #region txtShiftSize
            txtShiftSize.BackColor = Color.Transparent;
            txtShiftSize.Location = new Point(3, 0);
            txtShiftSize.Size = new Size(149, 17);
            txtShiftSize.TabIndex = 0;
            txtShiftSize.TextAlign = ContentAlignment.MiddleCenter;
            #endregion

            #region sliderShiftSize
            sliderShiftSize.AutoSize = false;
            sliderShiftSize.Location = new Point(3, 20);
            sliderShiftSize.Size = new Size(150, 25);
            sliderShiftSize.TabIndex = 52;
            sliderShiftSize.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            sliderShiftSize.LargeChange = 1;
            sliderShiftSize.Maximum = 1000;
            sliderShiftSize.Minimum = -1000;
            sliderShiftSize.TickStyle = TickStyle.None;
            sliderShiftSize.ValueChanged += new EventHandler(SliderShiftSize_ValueChanged);
            sliderShiftSize.MouseEnter += new EventHandler(SliderShiftSize_MouseEnter);
            #endregion

            #region txtShiftRotation
            txtShiftRotation.BackColor = Color.Transparent;
            txtShiftRotation.Location = new Point(3, 48);
            txtShiftRotation.Size = new Size(149, 17);
            txtShiftRotation.TabIndex = 0;
            txtShiftRotation.TextAlign = ContentAlignment.MiddleCenter;
            #endregion

            #region sliderShiftRotation
            sliderShiftRotation.AutoSize = false;
            sliderShiftRotation.Location = new Point(3, 68);
            sliderShiftRotation.Size = new Size(150, 25);
            sliderShiftRotation.TabIndex = 53;
            sliderShiftRotation.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            sliderShiftRotation.LargeChange = 1;
            sliderShiftRotation.Maximum = 180;
            sliderShiftRotation.Minimum = -180;
            sliderShiftRotation.TickStyle = TickStyle.None;
            sliderShiftRotation.ValueChanged += new EventHandler(SliderShiftRotation_ValueChanged);
            sliderShiftRotation.MouseEnter += new EventHandler(SliderShiftRotation_MouseEnter);
            #endregion

            #region txtShiftFlow
            txtShiftFlow.BackColor = Color.Transparent;
            txtShiftFlow.Location = new Point(3, 96);
            txtShiftFlow.Size = new Size(149, 17);
            txtShiftFlow.TabIndex = 0;
            txtShiftFlow.TextAlign = ContentAlignment.MiddleCenter;
            #endregion

            #region sliderShiftFlow
            sliderShiftFlow.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            sliderShiftFlow.AutoSize = false;
            sliderShiftFlow.Location = new Point(3, 116);
            sliderShiftFlow.Size = new Size(150, 25);
            sliderShiftFlow.TabIndex = 54;
            sliderShiftFlow.LargeChange = 1;
            sliderShiftFlow.Maximum = 255;
            sliderShiftFlow.Minimum = -255;
            sliderShiftFlow.TickStyle = TickStyle.None;
            sliderShiftFlow.ValueChanged += new EventHandler(SliderShiftFlow_ValueChanged);
            sliderShiftFlow.MouseEnter += new EventHandler(SliderShiftFlow_MouseEnter);
            #endregion

            #region bttnTabAssignPressureControls
            bttnTabAssignPressureControls.BackColor = Color.Black;
            bttnTabAssignPressureControls.ForeColor = Color.WhiteSmoke;
            bttnTabAssignPressureControls.Location = new Point(0, 1874);
            bttnTabAssignPressureControls.Margin = new Padding(0, 3, 0, 3);
            bttnTabAssignPressureControls.Size = new Size(155, 23);
            bttnTabAssignPressureControls.TabIndex = 55;
            bttnTabAssignPressureControls.TextAlign = ContentAlignment.MiddleLeft;
            bttnTabAssignPressureControls.UseVisualStyleBackColor = false;
            #endregion

            #region panelTabletAssignPressure
            panelTabletAssignPressure.AutoSize = true;
            panelTabletAssignPressure.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelTabletAssignPressure.BackColor = SystemColors.Control;
            panelTabletAssignPressure.FlowDirection = FlowDirection.TopDown;
            panelTabletAssignPressure.Location = new Point(0, 1903);
            panelTabletAssignPressure.Margin = new Padding(0, 3, 0, 3);
            panelTabletAssignPressure.Size = new Size(156, 990);
            panelTabletAssignPressure.TabIndex = 55;
            panelTabletAssignPressure.Controls.Add(panelTabPressureBrushOpacity);
            panelTabletAssignPressure.Controls.Add(panelTabPressureBrushFlow);
            panelTabletAssignPressure.Controls.Add(panelTabPressureBrushSize);
            panelTabletAssignPressure.Controls.Add(panelTabPressureBrushRotation);
            panelTabletAssignPressure.Controls.Add(panelTabPressureMinDrawDistance);
            panelTabletAssignPressure.Controls.Add(panelTabPressureBrushDensity);
            panelTabletAssignPressure.Controls.Add(panelTabPressureRandMinSize);
            panelTabletAssignPressure.Controls.Add(panelTabPressureRandMaxSize);
            panelTabletAssignPressure.Controls.Add(panelTabPressureRandRotLeft);
            panelTabletAssignPressure.Controls.Add(panelTabPressureRandRotRight);
            panelTabletAssignPressure.Controls.Add(panelTabPressureRandFlowLoss);
            panelTabletAssignPressure.Controls.Add(panelTabPressureRandHorShift);
            panelTabletAssignPressure.Controls.Add(panelTabPressureRandVerShift);
            panelTabletAssignPressure.Controls.Add(panelTabPressureRedJitter);
            panelTabletAssignPressure.Controls.Add(panelTabPressureGreenJitter);
            panelTabletAssignPressure.Controls.Add(panelTabPressureBlueJitter);
            panelTabletAssignPressure.Controls.Add(panelTabPressureHueJitter);
            panelTabletAssignPressure.Controls.Add(panelTabPressureSatJitter);
            panelTabletAssignPressure.Controls.Add(panelTabPressureValueJitter);
            #endregion

            #region panelTabPressureBrushOpacity
            panelTabPressureBrushOpacity.FlowDirection = FlowDirection.TopDown;
            panelTabPressureBrushOpacity.Location = new Point(0, 3);
            panelTabPressureBrushOpacity.Margin = new Padding(0, 3, 0, 3);
            panelTabPressureBrushOpacity.Size = new Size(156, 49);
            panelTabPressureBrushOpacity.TabIndex = 60;
            panelTabPressureBrushOpacity.Controls.Add(panel19);
            panelTabPressureBrushOpacity.Controls.Add(cmbxTabPressureBrushOpacity);
            #endregion

            #region panel19
            panel19.Location = new Point(0, 3);
            panel19.Margin = new Padding(0, 3, 0, 0);
            panel19.Size = new Size(156, 22);
            panel19.TabIndex = 57;
            panel19.Controls.Add(txtTabPressureBrushOpacity);
            panel19.Controls.Add(spinTabPressureBrushOpacity);
            #endregion

            #region txtTabPressureBrushOpacity
            txtTabPressureBrushOpacity.AutoSize = true;
            txtTabPressureBrushOpacity.Dock = DockStyle.Left;
            txtTabPressureBrushOpacity.Font = detailsFont;
            txtTabPressureBrushOpacity.Location = new Point(0, 0);
            txtTabPressureBrushOpacity.Margin = new Padding(3, 3, 3, 3);
            txtTabPressureBrushOpacity.Size = new Size(105, 13);
            txtTabPressureBrushOpacity.TabIndex = 0;
            txtTabPressureBrushOpacity.Text = Strings.BrushOpacity;
            #endregion

            #region spinTabPressureBrushOpacity
            spinTabPressureBrushOpacity.Dock = DockStyle.Right;
            spinTabPressureBrushOpacity.Location = new Point(105, 0);
            spinTabPressureBrushOpacity.Margin = new Padding(3, 3, 0, 3);
            spinTabPressureBrushOpacity.Size = new Size(51, 20);
            spinTabPressureBrushOpacity.TabIndex = 58;
            spinTabPressureBrushOpacity.Maximum = 255;
            spinTabPressureBrushOpacity.Minimum = 0;
            #endregion

            #region cmbxTabPressureBrushOpacity
            cmbxTabPressureBrushOpacity.Font = detailsFont;
            cmbxTabPressureBrushOpacity.IntegralHeight = false;
            cmbxTabPressureBrushOpacity.ItemHeight = 13;
            cmbxTabPressureBrushOpacity.Location = new Point(0, 25);
            cmbxTabPressureBrushOpacity.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureBrushOpacity.MaxDropDownItems = 9;
            cmbxTabPressureBrushOpacity.Size = new Size(156, 21);
            cmbxTabPressureBrushOpacity.TabIndex = 59;
            cmbxTabPressureBrushOpacity.BackColor = Color.White;
            cmbxTabPressureBrushOpacity.DisplayMember = "DisplayMember";
            cmbxTabPressureBrushOpacity.DropDownHeight = 140;
            cmbxTabPressureBrushOpacity.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbxTabPressureBrushOpacity.DropDownWidth = 20;
            cmbxTabPressureBrushOpacity.FormattingEnabled = true;
            cmbxTabPressureBrushOpacity.ValueMember = "ValueMember";
            cmbxTabPressureBrushOpacity.MouseHover += new EventHandler(CmbxTabPressure_MouseHover);
            #endregion

            #region panelTabPressureBrushFlow
            panelTabPressureBrushFlow.AutoSize = true;
            panelTabPressureBrushFlow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelTabPressureBrushFlow.FlowDirection = FlowDirection.TopDown;
            panelTabPressureBrushFlow.Location = new Point(0, 58);
            panelTabPressureBrushFlow.Margin = new Padding(0, 3, 0, 3);
            panelTabPressureBrushFlow.Size = new Size(156, 49);
            panelTabPressureBrushFlow.TabIndex = 60;
            panelTabPressureBrushFlow.Controls.Add(panel3);
            panelTabPressureBrushFlow.Controls.Add(cmbxTabPressureBrushFlow);
            #endregion

            #region panel3
            panel3.Location = new Point(0, 3);
            panel3.Margin = new Padding(0, 3, 0, 0);
            panel3.Size = new Size(156, 22);
            panel3.TabIndex = 57;
            panel3.Controls.Add(txtTabPressureBrushFlow);
            panel3.Controls.Add(spinTabPressureBrushFlow);
            #endregion

            #region txtTabPressureBrushFlow
            txtTabPressureBrushFlow.Text = Strings.BrushFlow;
            txtTabPressureBrushFlow.AutoSize = true;
            txtTabPressureBrushFlow.Dock = DockStyle.Left;
            txtTabPressureBrushFlow.Font = detailsFont;
            txtTabPressureBrushFlow.Location = new Point(0, 0);
            txtTabPressureBrushFlow.Margin = new Padding(3, 3, 3, 3);
            txtTabPressureBrushFlow.Size = new Size(105, 13);
            txtTabPressureBrushFlow.TabIndex = 0;
            #endregion

            #region spinTabPressureBrushFlow
            spinTabPressureBrushFlow.Dock = DockStyle.Right;
            spinTabPressureBrushFlow.Location = new Point(105, 0);
            spinTabPressureBrushFlow.Margin = new Padding(3, 3, 0, 3);
            spinTabPressureBrushFlow.Size = new Size(51, 20);
            spinTabPressureBrushFlow.TabIndex = 58;
            spinTabPressureBrushFlow.Maximum = new decimal(new int[] {
            255,
            0,
            0,
            0});
            spinTabPressureBrushFlow.Minimum = new decimal(new int[] {
            255,
            0,
            0,
            -2147483648});
            #endregion

            #region cmbxTabPressureBrushFlow
            cmbxTabPressureBrushFlow.Font = detailsFont;
            cmbxTabPressureBrushFlow.IntegralHeight = false;
            cmbxTabPressureBrushFlow.ItemHeight = 13;
            cmbxTabPressureBrushFlow.Location = new Point(0, 25);
            cmbxTabPressureBrushFlow.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureBrushFlow.MaxDropDownItems = 9;
            cmbxTabPressureBrushFlow.Size = new Size(156, 21);
            cmbxTabPressureBrushFlow.TabIndex = 59;
            cmbxTabPressureBrushFlow.BackColor = Color.White;
            cmbxTabPressureBrushFlow.DisplayMember = "DisplayMember";
            cmbxTabPressureBrushFlow.DropDownHeight = 140;
            cmbxTabPressureBrushFlow.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbxTabPressureBrushFlow.DropDownWidth = 20;
            cmbxTabPressureBrushFlow.FormattingEnabled = true;
            cmbxTabPressureBrushFlow.ValueMember = "ValueMember";
            cmbxTabPressureBrushFlow.MouseHover += new EventHandler(CmbxTabPressure_MouseHover);
            #endregion

            #region panelTabPressureBrushSize
            panelTabPressureBrushSize.AutoSize = true;
            panelTabPressureBrushSize.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelTabPressureBrushSize.FlowDirection = FlowDirection.TopDown;
            panelTabPressureBrushSize.Location = new Point(0, 113);
            panelTabPressureBrushSize.Margin = new Padding(0, 3, 0, 3);
            panelTabPressureBrushSize.Size = new Size(156, 49);
            panelTabPressureBrushSize.TabIndex = 62;
            panelTabPressureBrushSize.Controls.Add(panel8);
            panelTabPressureBrushSize.Controls.Add(cmbxTabPressureBrushSize);
            #endregion

            #region panel8
            panel8.Location = new Point(0, 3);
            panel8.Margin = new Padding(0, 3, 0, 0);
            panel8.Size = new Size(156, 22);
            panel8.TabIndex = 61;
            panel8.Controls.Add(txtTabPressureBrushSize);
            panel8.Controls.Add(spinTabPressureBrushSize);
            #endregion

            #region txtTabPressureBrushSize
            txtTabPressureBrushSize.Text = Strings.Size;
            txtTabPressureBrushSize.AutoSize = true;
            txtTabPressureBrushSize.Dock = DockStyle.Left;
            txtTabPressureBrushSize.Font = detailsFont;
            txtTabPressureBrushSize.Location = new Point(0, 0);
            txtTabPressureBrushSize.Margin = new Padding(3, 3, 3, 3);
            txtTabPressureBrushSize.Size = new Size(60, 13);
            txtTabPressureBrushSize.TabIndex = 0;
            #endregion

            #region spinTabPressureBrushSize
            spinTabPressureBrushSize.Dock = DockStyle.Right;
            spinTabPressureBrushSize.Location = new Point(105, 0);
            spinTabPressureBrushSize.Margin = new Padding(3, 3, 0, 3);
            spinTabPressureBrushSize.Size = new Size(51, 20);
            spinTabPressureBrushSize.TabIndex = 62;
            spinTabPressureBrushSize.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            spinTabPressureBrushSize.Minimum = new decimal(new int[] {
            1000,
            0,
            0,
            -2147483648});
            spinTabPressureBrushSize.LostFocus += SpinTabPressureBrushSize_LostFocus;
            #endregion

            #region cmbxTabPressureBrushSize
            cmbxTabPressureBrushSize.BackColor = Color.White;
            cmbxTabPressureBrushSize.DisplayMember = "DisplayMember";
            cmbxTabPressureBrushSize.DropDownHeight = 140;
            cmbxTabPressureBrushSize.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbxTabPressureBrushSize.DropDownWidth = 20;
            cmbxTabPressureBrushSize.FormattingEnabled = true;
            cmbxTabPressureBrushSize.ValueMember = "ValueMember";
            cmbxTabPressureBrushSize.Font = detailsFont;
            cmbxTabPressureBrushSize.IntegralHeight = false;
            cmbxTabPressureBrushSize.ItemHeight = 13;
            cmbxTabPressureBrushSize.Location = new Point(0, 25);
            cmbxTabPressureBrushSize.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureBrushSize.MaxDropDownItems = 9;
            cmbxTabPressureBrushSize.Size = new Size(156, 21);
            cmbxTabPressureBrushSize.TabIndex = 63;
            cmbxTabPressureBrushSize.MouseHover += new EventHandler(CmbxTabPressure_MouseHover);
            cmbxTabPressureBrushSize.SelectedIndexChanged += CmbxTabPressureBrushSize_SelectedIndexChanged;
            #endregion

            #region panelTabPressureBrushRotation
            panelTabPressureBrushRotation.AutoSize = true;
            panelTabPressureBrushRotation.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelTabPressureBrushRotation.FlowDirection = FlowDirection.TopDown;
            panelTabPressureBrushRotation.Location = new Point(0, 168);
            panelTabPressureBrushRotation.Margin = new Padding(0, 3, 0, 3);
            panelTabPressureBrushRotation.Size = new Size(156, 49);
            panelTabPressureBrushRotation.TabIndex = 67;
            panelTabPressureBrushRotation.Controls.Add(panel2);
            panelTabPressureBrushRotation.Controls.Add(cmbxTabPressureBrushRotation);
            #endregion

            #region panel2
            panel2.Location = new Point(0, 3);
            panel2.Margin = new Padding(0, 3, 0, 0);
            panel2.Size = new Size(156, 22);
            panel2.TabIndex = 65;
            panel2.Controls.Add(txtTabPressureBrushRotation);
            panel2.Controls.Add(spinTabPressureBrushRotation);
            #endregion

            #region txtTabPressureBrushRotation
            txtTabPressureBrushRotation.Text = Strings.Rotation;
            txtTabPressureBrushRotation.AutoSize = true;
            txtTabPressureBrushRotation.Dock = DockStyle.Left;
            txtTabPressureBrushRotation.Font = detailsFont;
            txtTabPressureBrushRotation.Location = new Point(0, 0);
            txtTabPressureBrushRotation.Margin = new Padding(3, 3, 3, 3);
            txtTabPressureBrushRotation.Size = new Size(80, 13);
            txtTabPressureBrushRotation.TabIndex = 0;
            #endregion

            #region spinTabPressureBrushRotation
            spinTabPressureBrushRotation.Dock = DockStyle.Right;
            spinTabPressureBrushRotation.Location = new Point(105, 0);
            spinTabPressureBrushRotation.Margin = new Padding(3, 3, 0, 3);
            spinTabPressureBrushRotation.Size = new Size(51, 20);
            spinTabPressureBrushRotation.TabIndex = 66;
            spinTabPressureBrushRotation.Maximum = new decimal(new int[] {
            180,
            0,
            0,
            0});
            spinTabPressureBrushRotation.Minimum = new decimal(new int[] {
            180,
            0,
            0,
            -2147483648});
            #endregion

            #region cmbxTabPressureBrushRotation
            cmbxTabPressureBrushRotation.Font = detailsFont;
            cmbxTabPressureBrushRotation.IntegralHeight = false;
            cmbxTabPressureBrushRotation.ItemHeight = 13;
            cmbxTabPressureBrushRotation.Location = new Point(0, 25);
            cmbxTabPressureBrushRotation.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureBrushRotation.MaxDropDownItems = 9;
            cmbxTabPressureBrushRotation.Size = new Size(156, 21);
            cmbxTabPressureBrushRotation.TabIndex = 68;
            cmbxTabPressureBrushRotation.BackColor = Color.White;
            cmbxTabPressureBrushRotation.DisplayMember = "DisplayMember";
            cmbxTabPressureBrushRotation.DropDownHeight = 140;
            cmbxTabPressureBrushRotation.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbxTabPressureBrushRotation.DropDownWidth = 20;
            cmbxTabPressureBrushRotation.FormattingEnabled = true;
            cmbxTabPressureBrushRotation.ValueMember = "ValueMember";
            cmbxTabPressureBrushRotation.MouseHover += new EventHandler(CmbxTabPressure_MouseHover);
            #endregion

            #region panelTabPressureMinDrawDistance
            panelTabPressureMinDrawDistance.AutoSize = true;
            panelTabPressureMinDrawDistance.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelTabPressureMinDrawDistance.FlowDirection = FlowDirection.TopDown;
            panelTabPressureMinDrawDistance.Location = new Point(0, 223);
            panelTabPressureMinDrawDistance.Margin = new Padding(0, 3, 0, 3);
            panelTabPressureMinDrawDistance.Size = new Size(156, 49);
            panelTabPressureMinDrawDistance.TabIndex = 71;
            panelTabPressureMinDrawDistance.Controls.Add(panel1);
            panelTabPressureMinDrawDistance.Controls.Add(cmbxTabPressureMinDrawDistance);
            #endregion

            #region panel1
            panel1.Location = new Point(0, 3);
            panel1.Margin = new Padding(0, 3, 0, 0);
            panel1.Size = new Size(156, 22);
            panel1.TabIndex = 70;
            panel1.Controls.Add(lblTabPressureMinDrawDistance);
            panel1.Controls.Add(spinTabPressureMinDrawDistance);
            #endregion

            #region lblTabPressureMinDrawDistance
            lblTabPressureMinDrawDistance.Text = Strings.MinDrawDistance;
            lblTabPressureMinDrawDistance.AutoSize = true;
            lblTabPressureMinDrawDistance.Dock = DockStyle.Left;
            lblTabPressureMinDrawDistance.Font = detailsFont;
            lblTabPressureMinDrawDistance.Location = new Point(0, 0);
            lblTabPressureMinDrawDistance.Margin = new Padding(3, 3, 3, 3);
            lblTabPressureMinDrawDistance.Size = new Size(103, 13);
            lblTabPressureMinDrawDistance.TabIndex = 0;
            #endregion

            #region spinTabPressureMinDrawDistance
            spinTabPressureMinDrawDistance.Dock = DockStyle.Right;
            spinTabPressureMinDrawDistance.Location = new Point(105, 0);
            spinTabPressureMinDrawDistance.Margin = new Padding(3, 3, 0, 3);
            spinTabPressureMinDrawDistance.Size = new Size(51, 20);
            spinTabPressureMinDrawDistance.TabIndex = 69;
            spinTabPressureMinDrawDistance.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            #endregion

            #region cmbxTabPressureMinDrawDistance
            cmbxTabPressureMinDrawDistance.Font = detailsFont;
            cmbxTabPressureMinDrawDistance.IntegralHeight = false;
            cmbxTabPressureMinDrawDistance.ItemHeight = 13;
            cmbxTabPressureMinDrawDistance.Location = new Point(0, 25);
            cmbxTabPressureMinDrawDistance.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureMinDrawDistance.MaxDropDownItems = 9;
            cmbxTabPressureMinDrawDistance.Size = new Size(156, 21);
            cmbxTabPressureMinDrawDistance.TabIndex = 72;
            cmbxTabPressureMinDrawDistance.BackColor = Color.White;
            cmbxTabPressureMinDrawDistance.DisplayMember = "DisplayMember";
            cmbxTabPressureMinDrawDistance.DropDownHeight = 140;
            cmbxTabPressureMinDrawDistance.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbxTabPressureMinDrawDistance.DropDownWidth = 20;
            cmbxTabPressureMinDrawDistance.FormattingEnabled = true;
            cmbxTabPressureMinDrawDistance.ValueMember = "ValueMember";
            cmbxTabPressureMinDrawDistance.MouseHover += new EventHandler(CmbxTabPressure_MouseHover);
            #endregion

            #region panelTabPressureBrushDensity
            panelTabPressureBrushDensity.AutoSize = true;
            panelTabPressureBrushDensity.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelTabPressureBrushDensity.FlowDirection = FlowDirection.TopDown;
            panelTabPressureBrushDensity.Location = new Point(0, 278);
            panelTabPressureBrushDensity.Margin = new Padding(0, 3, 0, 3);
            panelTabPressureBrushDensity.Size = new Size(156, 49);
            panelTabPressureBrushDensity.TabIndex = 75;
            panelTabPressureBrushDensity.Controls.Add(panel4);
            panelTabPressureBrushDensity.Controls.Add(cmbxTabPressureBrushDensity);
            #endregion

            #region panel4
            panel4.Location = new Point(0, 3);
            panel4.Margin = new Padding(0, 3, 0, 0);
            panel4.Size = new Size(156, 22);
            panel4.TabIndex = 73;
            panel4.Controls.Add(lblTabPressureBrushDensity);
            panel4.Controls.Add(spinTabPressureBrushDensity);
            #endregion

            #region lblTabPressureBrushDensity
            lblTabPressureBrushDensity.Text = Strings.BrushDensity;
            lblTabPressureBrushDensity.AutoSize = true;
            lblTabPressureBrushDensity.Dock = DockStyle.Left;
            lblTabPressureBrushDensity.Font = detailsFont;
            lblTabPressureBrushDensity.Location = new Point(0, 0);
            lblTabPressureBrushDensity.Margin = new Padding(3, 3, 3, 3);
            lblTabPressureBrushDensity.Size = new Size(75, 13);
            lblTabPressureBrushDensity.TabIndex = 0;
            #endregion

            #region spinTabPressureBrushDensity
            spinTabPressureBrushDensity.Dock = DockStyle.Right;
            spinTabPressureBrushDensity.Location = new Point(105, 0);
            spinTabPressureBrushDensity.Margin = new Padding(3, 3, 0, 3);
            spinTabPressureBrushDensity.Size = new Size(51, 20);
            spinTabPressureBrushDensity.TabIndex = 74;
            spinTabPressureBrushDensity.Maximum = new decimal(new int[] {
            50,
            0,
            0,
            0});
            spinTabPressureBrushDensity.Minimum = new decimal(new int[] {
            50,
            0,
            0,
            -2147483648});
            #endregion

            #region cmbxTabPressureBrushDensity
            cmbxTabPressureBrushDensity.Font = detailsFont;
            cmbxTabPressureBrushDensity.IntegralHeight = false;
            cmbxTabPressureBrushDensity.ItemHeight = 13;
            cmbxTabPressureBrushDensity.Location = new Point(0, 25);
            cmbxTabPressureBrushDensity.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureBrushDensity.MaxDropDownItems = 9;
            cmbxTabPressureBrushDensity.Size = new Size(156, 21);
            cmbxTabPressureBrushDensity.TabIndex = 76;
            cmbxTabPressureBrushDensity.BackColor = Color.White;
            cmbxTabPressureBrushDensity.DisplayMember = "DisplayMember";
            cmbxTabPressureBrushDensity.DropDownHeight = 140;
            cmbxTabPressureBrushDensity.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbxTabPressureBrushDensity.DropDownWidth = 20;
            cmbxTabPressureBrushDensity.FormattingEnabled = true;
            cmbxTabPressureBrushDensity.ValueMember = "ValueMember";
            cmbxTabPressureBrushDensity.MouseHover += new EventHandler(CmbxTabPressure_MouseHover);
            #endregion

            #region panelTabPressureRandMinSize
            panelTabPressureRandMinSize.AutoSize = true;
            panelTabPressureRandMinSize.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelTabPressureRandMinSize.FlowDirection = FlowDirection.TopDown;
            panelTabPressureRandMinSize.Location = new Point(0, 333);
            panelTabPressureRandMinSize.Margin = new Padding(0, 3, 0, 3);
            panelTabPressureRandMinSize.Size = new Size(156, 49);
            panelTabPressureRandMinSize.TabIndex = 79;
            panelTabPressureRandMinSize.Controls.Add(panel5);
            panelTabPressureRandMinSize.Controls.Add(cmbxTabPressureRandMinSize);
            #endregion

            #region panel5
            panel5.Location = new Point(0, 3);
            panel5.Margin = new Padding(0, 3, 0, 0);
            panel5.Size = new Size(156, 22);
            panel5.TabIndex = 77;
            panel5.Controls.Add(lblTabPressureRandMinSize);
            panel5.Controls.Add(spinTabPressureRandMinSize);
            #endregion

            #region lblTabPressureRandMinSize
            lblTabPressureRandMinSize.Text = Strings.RandMinSize;
            lblTabPressureRandMinSize.AutoSize = true;
            lblTabPressureRandMinSize.Dock = DockStyle.Left;
            lblTabPressureRandMinSize.Font = detailsFont;
            lblTabPressureRandMinSize.Location = new Point(0, 0);
            lblTabPressureRandMinSize.Margin = new Padding(3, 3, 3, 3);
            lblTabPressureRandMinSize.Size = new Size(93, 13);
            lblTabPressureRandMinSize.TabIndex = 0;
            #endregion

            #region spinTabPressureRandMinSize
            spinTabPressureRandMinSize.Dock = DockStyle.Right;
            spinTabPressureRandMinSize.Location = new Point(105, 0);
            spinTabPressureRandMinSize.Margin = new Padding(3, 3, 0, 3);
            spinTabPressureRandMinSize.Size = new Size(51, 20);
            spinTabPressureRandMinSize.TabIndex = 78;
            spinTabPressureRandMinSize.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            spinTabPressureRandMinSize.Minimum = new decimal(new int[] {
            1000,
            0,
            0,
            -2147483648});
            #endregion

            #region cmbxTabPressureRandMinSize
            cmbxTabPressureRandMinSize.BackColor = Color.White;
            cmbxTabPressureRandMinSize.Font = detailsFont;
            cmbxTabPressureRandMinSize.IntegralHeight = false;
            cmbxTabPressureRandMinSize.ItemHeight = 13;
            cmbxTabPressureRandMinSize.Location = new Point(0, 25);
            cmbxTabPressureRandMinSize.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureRandMinSize.MaxDropDownItems = 9;
            cmbxTabPressureRandMinSize.Size = new Size(156, 21);
            cmbxTabPressureRandMinSize.TabIndex = 80;
            cmbxTabPressureRandMinSize.DisplayMember = "DisplayMember";
            cmbxTabPressureRandMinSize.DropDownHeight = 140;
            cmbxTabPressureRandMinSize.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbxTabPressureRandMinSize.DropDownWidth = 20;
            cmbxTabPressureRandMinSize.FormattingEnabled = true;
            cmbxTabPressureRandMinSize.ValueMember = "ValueMember";
            cmbxTabPressureRandMinSize.MouseHover += new EventHandler(CmbxTabPressure_MouseHover);
            #endregion

            #region panelTabPressureRandMaxSize
            panelTabPressureRandMaxSize.AutoSize = true;
            panelTabPressureRandMaxSize.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelTabPressureRandMaxSize.FlowDirection = FlowDirection.TopDown;
            panelTabPressureRandMaxSize.Location = new Point(0, 388);
            panelTabPressureRandMaxSize.Margin = new Padding(0, 3, 0, 3);
            panelTabPressureRandMaxSize.Size = new Size(156, 49);
            panelTabPressureRandMaxSize.TabIndex = 83;
            panelTabPressureRandMaxSize.Controls.Add(panel6);
            panelTabPressureRandMaxSize.Controls.Add(cmbxTabPressureRandMaxSize);
            #endregion

            #region panel6
            panel6.Location = new Point(0, 3);
            panel6.Margin = new Padding(0, 3, 0, 0);
            panel6.Size = new Size(156, 22);
            panel6.TabIndex = 81;
            panel6.Controls.Add(lblTabPressureRandMaxSize);
            panel6.Controls.Add(spinTabPressureRandMaxSize);
            #endregion

            #region lblTabPressureRandMaxSize
            lblTabPressureRandMaxSize.Text = Strings.RandMaxSize;
            lblTabPressureRandMaxSize.AutoSize = true;
            lblTabPressureRandMaxSize.Dock = DockStyle.Left;
            lblTabPressureRandMaxSize.Font = detailsFont;
            lblTabPressureRandMaxSize.Location = new Point(0, 0);
            lblTabPressureRandMaxSize.Margin = new Padding(3, 3, 3, 3);
            lblTabPressureRandMaxSize.Size = new Size(96, 13);
            lblTabPressureRandMaxSize.TabIndex = 0;
            #endregion

            #region spinTabPressureRandMaxSize
            spinTabPressureRandMaxSize.Dock = DockStyle.Right;
            spinTabPressureRandMaxSize.Location = new Point(105, 0);
            spinTabPressureRandMaxSize.Margin = new Padding(3, 3, 0, 3);
            spinTabPressureRandMaxSize.Size = new Size(51, 20);
            spinTabPressureRandMaxSize.TabIndex = 82;
            spinTabPressureRandMaxSize.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            spinTabPressureRandMaxSize.Minimum = new decimal(new int[] {
            1000,
            0,
            0,
            -2147483648});
            #endregion

            #region cmbxTabPressureRandMaxSize
            cmbxTabPressureRandMaxSize.Font = detailsFont;
            cmbxTabPressureRandMaxSize.IntegralHeight = false;
            cmbxTabPressureRandMaxSize.ItemHeight = 13;
            cmbxTabPressureRandMaxSize.Location = new Point(0, 25);
            cmbxTabPressureRandMaxSize.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureRandMaxSize.MaxDropDownItems = 9;
            cmbxTabPressureRandMaxSize.Size = new Size(156, 21);
            cmbxTabPressureRandMaxSize.TabIndex = 84;
            cmbxTabPressureRandMaxSize.BackColor = Color.White;
            cmbxTabPressureRandMaxSize.DisplayMember = "DisplayMember";
            cmbxTabPressureRandMaxSize.DropDownHeight = 140;
            cmbxTabPressureRandMaxSize.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbxTabPressureRandMaxSize.DropDownWidth = 20;
            cmbxTabPressureRandMaxSize.FormattingEnabled = true;
            cmbxTabPressureRandMaxSize.ValueMember = "ValueMember";
            cmbxTabPressureRandMaxSize.MouseHover += new EventHandler(CmbxTabPressure_MouseHover);
            #endregion

            #region panelTabPressureRandRotLeft
            panelTabPressureRandRotLeft.AutoSize = true;
            panelTabPressureRandRotLeft.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelTabPressureRandRotLeft.FlowDirection = FlowDirection.TopDown;
            panelTabPressureRandRotLeft.Location = new Point(0, 443);
            panelTabPressureRandRotLeft.Margin = new Padding(0, 3, 0, 3);
            panelTabPressureRandRotLeft.Size = new Size(156, 49);
            panelTabPressureRandRotLeft.TabIndex = 86;
            panelTabPressureRandRotLeft.Controls.Add(panel7);
            panelTabPressureRandRotLeft.Controls.Add(cmbxTabPressureRandRotLeft);
            #endregion

            #region panel7
            panel7.Location = new Point(0, 3);
            panel7.Margin = new Padding(0, 3, 0, 0);
            panel7.Size = new Size(156, 22);
            panel7.TabIndex = 84;
            panel7.Controls.Add(lblTabPressureRandRotLeft);
            panel7.Controls.Add(spinTabPressureRandRotLeft);
            #endregion

            #region lblTabPressureRandRotLeft
            lblTabPressureRandRotLeft.Text = Strings.RandRotLeft;
            lblTabPressureRandRotLeft.AutoSize = true;
            lblTabPressureRandRotLeft.Dock = DockStyle.Left;
            lblTabPressureRandRotLeft.Font = detailsFont;
            lblTabPressureRandRotLeft.Location = new Point(0, 0);
            lblTabPressureRandRotLeft.Margin = new Padding(0, 3, 0, 3);
            lblTabPressureRandRotLeft.Size = new Size(91, 13);
            lblTabPressureRandRotLeft.TabIndex = 0;
            #endregion

            #region spinTabPressureRandRotLeft
            spinTabPressureRandRotLeft.Dock = DockStyle.Right;
            spinTabPressureRandRotLeft.Location = new Point(105, 0);
            spinTabPressureRandRotLeft.Margin = new Padding(3, 3, 0, 3);
            spinTabPressureRandRotLeft.Size = new Size(51, 20);
            spinTabPressureRandRotLeft.TabIndex = 85;
            spinTabPressureRandRotLeft.Maximum = new decimal(new int[] {
            360,
            0,
            0,
            0});
            spinTabPressureRandRotLeft.Minimum = new decimal(new int[] {
            360,
            0,
            0,
            -2147483648});
            #endregion

            #region cmbxTabPressureRandRotLeft
            cmbxTabPressureRandRotLeft.Font = detailsFont;
            cmbxTabPressureRandRotLeft.IntegralHeight = false;
            cmbxTabPressureRandRotLeft.ItemHeight = 13;
            cmbxTabPressureRandRotLeft.Location = new Point(0, 25);
            cmbxTabPressureRandRotLeft.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureRandRotLeft.MaxDropDownItems = 9;
            cmbxTabPressureRandRotLeft.Size = new Size(156, 21);
            cmbxTabPressureRandRotLeft.TabIndex = 87;
            cmbxTabPressureRandRotLeft.BackColor = Color.White;
            cmbxTabPressureRandRotLeft.DisplayMember = "DisplayMember";
            cmbxTabPressureRandRotLeft.DropDownHeight = 140;
            cmbxTabPressureRandRotLeft.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbxTabPressureRandRotLeft.DropDownWidth = 20;
            cmbxTabPressureRandRotLeft.FormattingEnabled = true;
            cmbxTabPressureRandRotLeft.ValueMember = "ValueMember";
            cmbxTabPressureRandRotLeft.MouseHover += new EventHandler(CmbxTabPressure_MouseHover);
            #endregion

            #region panelTabPressureRandRotRight
            panelTabPressureRandRotRight.AutoSize = true;
            panelTabPressureRandRotRight.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelTabPressureRandRotRight.FlowDirection = FlowDirection.TopDown;
            panelTabPressureRandRotRight.Location = new Point(0, 498);
            panelTabPressureRandRotRight.Margin = new Padding(0, 3, 0, 3);
            panelTabPressureRandRotRight.Size = new Size(156, 49);
            panelTabPressureRandRotRight.TabIndex = 90;
            panelTabPressureRandRotRight.Controls.Add(panel9);
            panelTabPressureRandRotRight.Controls.Add(cmbxTabPressureRandRotRight);
            #endregion

            #region panel9
            panel9.Location = new Point(0, 3);
            panel9.Margin = new Padding(0, 3, 0, 0);
            panel9.Size = new Size(156, 22);
            panel9.TabIndex = 88;
            panel9.Controls.Add(lblTabPressureRandRotRight);
            panel9.Controls.Add(spinTabPressureRandRotRight);
            #endregion

            #region lblTabPressureRandRotRight
            lblTabPressureRandRotRight.Text = Strings.RandRotRight;
            lblTabPressureRandRotRight.AutoSize = true;
            lblTabPressureRandRotRight.Dock = DockStyle.Left;
            lblTabPressureRandRotRight.Font = detailsFont;
            lblTabPressureRandRotRight.Location = new Point(0, 0);
            lblTabPressureRandRotRight.Margin = new Padding(0, 3, 0, 3);
            lblTabPressureRandRotRight.Size = new Size(98, 13);
            lblTabPressureRandRotRight.TabIndex = 0;
            #endregion

            #region spinTabPressureRandRotRight
            spinTabPressureRandRotRight.Dock = DockStyle.Right;
            spinTabPressureRandRotRight.Location = new Point(105, 0);
            spinTabPressureRandRotRight.Margin = new Padding(3, 3, 0, 3);
            spinTabPressureRandRotRight.Size = new Size(51, 20);
            spinTabPressureRandRotRight.TabIndex = 89;
            spinTabPressureRandRotRight.Maximum = new decimal(new int[] {
            360,
            0,
            0,
            0});
            spinTabPressureRandRotRight.Minimum = new decimal(new int[] {
            360,
            0,
            0,
            -2147483648});
            #endregion

            #region cmbxTabPressureRandRotRight
            cmbxTabPressureRandRotRight.Font = detailsFont;
            cmbxTabPressureRandRotRight.IntegralHeight = false;
            cmbxTabPressureRandRotRight.ItemHeight = 13;
            cmbxTabPressureRandRotRight.Location = new Point(0, 25);
            cmbxTabPressureRandRotRight.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureRandRotRight.MaxDropDownItems = 9;
            cmbxTabPressureRandRotRight.Size = new Size(156, 21);
            cmbxTabPressureRandRotRight.TabIndex = 91;
            cmbxTabPressureRandRotRight.BackColor = Color.White;
            cmbxTabPressureRandRotRight.DisplayMember = "DisplayMember";
            cmbxTabPressureRandRotRight.DropDownHeight = 140;
            cmbxTabPressureRandRotRight.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbxTabPressureRandRotRight.DropDownWidth = 20;
            cmbxTabPressureRandRotRight.FormattingEnabled = true;
            cmbxTabPressureRandRotRight.ValueMember = "ValueMember";
            cmbxTabPressureRandRotRight.MouseHover += new EventHandler(CmbxTabPressure_MouseHover);
            #endregion

            #region panelTabPressureRandFlowLoss
            panelTabPressureRandFlowLoss.AutoSize = true;
            panelTabPressureRandFlowLoss.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelTabPressureRandFlowLoss.FlowDirection = FlowDirection.TopDown;
            panelTabPressureRandFlowLoss.Location = new Point(0, 553);
            panelTabPressureRandFlowLoss.Margin = new Padding(0, 3, 0, 3);
            panelTabPressureRandFlowLoss.Size = new Size(156, 49);
            panelTabPressureRandFlowLoss.TabIndex = 94;
            panelTabPressureRandFlowLoss.Controls.Add(panel10);
            panelTabPressureRandFlowLoss.Controls.Add(cmbxTabPressureRandFlowLoss);
            #endregion

            #region panel10
            panel10.Location = new Point(0, 3);
            panel10.Margin = new Padding(0, 3, 0, 0);
            panel10.Size = new Size(156, 22);
            panel10.TabIndex = 92;
            panel10.Controls.Add(lblTabPressureRandFlowLoss);
            panel10.Controls.Add(spinTabPressureRandFlowLoss);
            #endregion

            #region lblTabPressureRandFlowLoss
            lblTabPressureRandFlowLoss.Text = Strings.RandFlowLoss;
            lblTabPressureRandFlowLoss.AutoSize = true;
            lblTabPressureRandFlowLoss.Dock = DockStyle.Left;
            lblTabPressureRandFlowLoss.Font = detailsFont;
            lblTabPressureRandFlowLoss.Location = new Point(0, 0);
            lblTabPressureRandFlowLoss.Margin = new Padding(0, 3, 0, 3);
            lblTabPressureRandFlowLoss.Size = new Size(100, 13);
            lblTabPressureRandFlowLoss.TabIndex = 0;
            #endregion

            #region spinTabPressureRandFlowLoss
            spinTabPressureRandFlowLoss.Dock = DockStyle.Right;
            spinTabPressureRandFlowLoss.Location = new Point(105, 0);
            spinTabPressureRandFlowLoss.Margin = new Padding(3, 3, 0, 3);
            spinTabPressureRandFlowLoss.Size = new Size(51, 20);
            spinTabPressureRandFlowLoss.TabIndex = 93;
            spinTabPressureRandFlowLoss.Minimum = new decimal(new int[] {
            255,
            0,
            0,
            -2147483648});
            #endregion

            #region cmbxTabPressureRandFlowLoss
            cmbxTabPressureRandFlowLoss.Font = detailsFont;
            cmbxTabPressureRandFlowLoss.IntegralHeight = false;
            cmbxTabPressureRandFlowLoss.ItemHeight = 13;
            cmbxTabPressureRandFlowLoss.Location = new Point(0, 25);
            cmbxTabPressureRandFlowLoss.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureRandFlowLoss.MaxDropDownItems = 9;
            cmbxTabPressureRandFlowLoss.Size = new Size(156, 21);
            cmbxTabPressureRandFlowLoss.TabIndex = 95;
            cmbxTabPressureRandFlowLoss.BackColor = Color.White;
            cmbxTabPressureRandFlowLoss.DisplayMember = "DisplayMember";
            cmbxTabPressureRandFlowLoss.DropDownHeight = 140;
            cmbxTabPressureRandFlowLoss.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbxTabPressureRandFlowLoss.DropDownWidth = 20;
            cmbxTabPressureRandFlowLoss.FormattingEnabled = true;
            cmbxTabPressureRandFlowLoss.ValueMember = "ValueMember";
            cmbxTabPressureRandFlowLoss.MouseHover += new EventHandler(CmbxTabPressure_MouseHover);
            #endregion

            #region panelTabPressureRandHorShift
            panelTabPressureRandHorShift.AutoSize = true;
            panelTabPressureRandHorShift.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelTabPressureRandHorShift.FlowDirection = FlowDirection.TopDown;
            panelTabPressureRandHorShift.Location = new Point(0, 608);
            panelTabPressureRandHorShift.Margin = new Padding(0, 3, 0, 3);
            panelTabPressureRandHorShift.Size = new Size(156, 49);
            panelTabPressureRandHorShift.TabIndex = 98;
            panelTabPressureRandHorShift.Controls.Add(panel12);
            panelTabPressureRandHorShift.Controls.Add(cmbxTabPressureRandHorShift);
            #endregion

            #region panel12
            panel12.Location = new Point(0, 3);
            panel12.Margin = new Padding(0, 3, 0, 0);
            panel12.Size = new Size(156, 22);
            panel12.TabIndex = 96;
            panel12.Controls.Add(lblTabPressureRandHorShift);
            panel12.Controls.Add(spinTabPressureRandHorShift);
            #endregion

            #region lblTabPressureRandHorShift
            lblTabPressureRandHorShift.Text = Strings.RandHorzShift;
            lblTabPressureRandHorShift.AutoSize = true;
            lblTabPressureRandHorShift.Dock = DockStyle.Left;
            lblTabPressureRandHorShift.Font = detailsFont;
            lblTabPressureRandHorShift.Location = new Point(0, 0);
            lblTabPressureRandHorShift.Margin = new Padding(0, 3, 0, 3);
            lblTabPressureRandHorShift.Size = new Size(97, 13);
            lblTabPressureRandHorShift.TabIndex = 0;
            #endregion

            #region spinTabPressureRandHorShift
            spinTabPressureRandHorShift.Dock = DockStyle.Right;
            spinTabPressureRandHorShift.Location = new Point(105, 0);
            spinTabPressureRandHorShift.Margin = new Padding(3, 3, 0, 3);
            spinTabPressureRandHorShift.Size = new Size(51, 20);
            spinTabPressureRandHorShift.TabIndex = 97;
            spinTabPressureRandHorShift.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            #endregion

            #region cmbxTabPressureRandHorShift
            cmbxTabPressureRandHorShift.Font = detailsFont;
            cmbxTabPressureRandHorShift.IntegralHeight = false;
            cmbxTabPressureRandHorShift.ItemHeight = 13;
            cmbxTabPressureRandHorShift.Location = new Point(0, 25);
            cmbxTabPressureRandHorShift.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureRandHorShift.MaxDropDownItems = 9;
            cmbxTabPressureRandHorShift.Size = new Size(156, 21);
            cmbxTabPressureRandHorShift.TabIndex = 99;
            cmbxTabPressureRandHorShift.BackColor = Color.White;
            cmbxTabPressureRandHorShift.DisplayMember = "DisplayMember";
            cmbxTabPressureRandHorShift.DropDownHeight = 140;
            cmbxTabPressureRandHorShift.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbxTabPressureRandHorShift.DropDownWidth = 20;
            cmbxTabPressureRandHorShift.FormattingEnabled = true;
            cmbxTabPressureRandHorShift.ValueMember = "ValueMember";
            cmbxTabPressureRandHorShift.MouseHover += new EventHandler(CmbxTabPressure_MouseHover);
            #endregion

            #region panelTabPressureRandVerShift
            panelTabPressureRandVerShift.AutoSize = true;
            panelTabPressureRandVerShift.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelTabPressureRandVerShift.FlowDirection = FlowDirection.TopDown;
            panelTabPressureRandVerShift.Location = new Point(0, 663);
            panelTabPressureRandVerShift.Margin = new Padding(0, 3, 0, 3);
            panelTabPressureRandVerShift.Size = new Size(156, 49);
            panelTabPressureRandVerShift.TabIndex = 102;
            panelTabPressureRandVerShift.Controls.Add(panel11);
            panelTabPressureRandVerShift.Controls.Add(cmbxTabPressureRandVerShift);
            #endregion

            #region panel11
            panel11.Location = new Point(0, 3);
            panel11.Margin = new Padding(0, 3, 0, 0);
            panel11.Size = new Size(156, 22);
            panel11.TabIndex = 100;
            panel11.Controls.Add(lblTabPressureRandVerShift);
            panel11.Controls.Add(spinTabPressureRandVerShift);
            #endregion

            #region lblTabPressureRandVerShift
            lblTabPressureRandVerShift.Text = Strings.RandVertShift;
            lblTabPressureRandVerShift.AutoSize = true;
            lblTabPressureRandVerShift.Dock = DockStyle.Left;
            lblTabPressureRandVerShift.Font = detailsFont;
            lblTabPressureRandVerShift.Location = new Point(0, 0);
            lblTabPressureRandVerShift.Margin = new Padding(0, 3, 0, 3);
            lblTabPressureRandVerShift.Size = new Size(96, 13);
            lblTabPressureRandVerShift.TabIndex = 0;
            #endregion

            #region spinTabPressureRandVerShift
            spinTabPressureRandVerShift.Dock = DockStyle.Right;
            spinTabPressureRandVerShift.Location = new Point(105, 0);
            spinTabPressureRandVerShift.Margin = new Padding(3, 3, 0, 3);
            spinTabPressureRandVerShift.Size = new Size(51, 20);
            spinTabPressureRandVerShift.TabIndex = 101;
            spinTabPressureRandVerShift.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            #endregion

            #region cmbxTabPressureRandVerShift
            cmbxTabPressureRandVerShift.Font = detailsFont;
            cmbxTabPressureRandVerShift.IntegralHeight = false;
            cmbxTabPressureRandVerShift.ItemHeight = 13;
            cmbxTabPressureRandVerShift.Location = new Point(0, 25);
            cmbxTabPressureRandVerShift.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureRandVerShift.MaxDropDownItems = 9;
            cmbxTabPressureRandVerShift.Size = new Size(156, 21);
            cmbxTabPressureRandVerShift.TabIndex = 103;
            cmbxTabPressureRandVerShift.BackColor = Color.White;
            cmbxTabPressureRandVerShift.DisplayMember = "DisplayMember";
            cmbxTabPressureRandVerShift.DropDownHeight = 140;
            cmbxTabPressureRandVerShift.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbxTabPressureRandVerShift.DropDownWidth = 20;
            cmbxTabPressureRandVerShift.FormattingEnabled = true;
            cmbxTabPressureRandVerShift.ValueMember = "ValueMember";
            cmbxTabPressureRandVerShift.MouseHover += new EventHandler(CmbxTabPressure_MouseHover);
            #endregion

            #region panelTabPressureRedJitter
            panelTabPressureRedJitter.AutoSize = true;
            panelTabPressureRedJitter.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelTabPressureRedJitter.FlowDirection = FlowDirection.TopDown;
            panelTabPressureRedJitter.Location = new Point(0, 718);
            panelTabPressureRedJitter.Margin = new Padding(0, 3, 0, 3);
            panelTabPressureRedJitter.Size = new Size(156, 49);
            panelTabPressureRedJitter.TabIndex = 106;
            panelTabPressureRedJitter.Controls.Add(panel13);
            panelTabPressureRedJitter.Controls.Add(cmbxTabPressureRedJitter);
            #endregion

            #region panel13
            panel13.Location = new Point(0, 3);
            panel13.Margin = new Padding(0, 3, 0, 0);
            panel13.Size = new Size(156, 22);
            panel13.TabIndex = 103;
            panel13.Controls.Add(spinTabPressureMinRedJitter);
            panel13.Controls.Add(lblTabPressureRedJitter);
            panel13.Controls.Add(spinTabPressureMaxRedJitter);
            #endregion

            #region spinTabPressureMinRedJitter
            spinTabPressureMinRedJitter.Dock = DockStyle.Right;
            spinTabPressureMinRedJitter.Location = new Point(74, 0);
            spinTabPressureMinRedJitter.Margin = new Padding(3, 3, 0, 3);
            spinTabPressureMinRedJitter.Size = new Size(41, 20);
            spinTabPressureMinRedJitter.TabIndex = 104;
            spinTabPressureMinRedJitter.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            #endregion

            #region lblTabPressureRedJitter
            lblTabPressureRedJitter.Text = Strings.JitterRed;
            lblTabPressureRedJitter.AutoSize = true;
            lblTabPressureRedJitter.Dock = DockStyle.Left;
            lblTabPressureRedJitter.Font = detailsFont;
            lblTabPressureRedJitter.Location = new Point(0, 0);
            lblTabPressureRedJitter.Margin = new Padding(0, 3, 0, 3);
            lblTabPressureRedJitter.Size = new Size(55, 13);
            lblTabPressureRedJitter.TabIndex = 0;
            #endregion

            #region spinTabPressureMaxRedJitter
            spinTabPressureMaxRedJitter.Dock = DockStyle.Right;
            spinTabPressureMaxRedJitter.Location = new Point(115, 0);
            spinTabPressureMaxRedJitter.Margin = new Padding(3, 3, 0, 3);
            spinTabPressureMaxRedJitter.Size = new Size(41, 20);
            spinTabPressureMaxRedJitter.TabIndex = 105;
            spinTabPressureMaxRedJitter.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            #endregion

            #region cmbxTabPressureRedJitter
            cmbxTabPressureRedJitter.Font = detailsFont;
            cmbxTabPressureRedJitter.IntegralHeight = false;
            cmbxTabPressureRedJitter.ItemHeight = 13;
            cmbxTabPressureRedJitter.Location = new Point(0, 25);
            cmbxTabPressureRedJitter.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureRedJitter.MaxDropDownItems = 9;
            cmbxTabPressureRedJitter.Size = new Size(156, 21);
            cmbxTabPressureRedJitter.TabIndex = 107;
            cmbxTabPressureRedJitter.BackColor = Color.White;
            cmbxTabPressureRedJitter.DisplayMember = "DisplayMember";
            cmbxTabPressureRedJitter.DropDownHeight = 140;
            cmbxTabPressureRedJitter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbxTabPressureRedJitter.DropDownWidth = 20;
            cmbxTabPressureRedJitter.FormattingEnabled = true;
            cmbxTabPressureRedJitter.ValueMember = "ValueMember";
            cmbxTabPressureRedJitter.MouseHover += new EventHandler(CmbxTabPressure_MouseHover);
            #endregion

            #region panelTabPressureGreenJitter
            panelTabPressureGreenJitter.AutoSize = true;
            panelTabPressureGreenJitter.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelTabPressureGreenJitter.FlowDirection = FlowDirection.TopDown;
            panelTabPressureGreenJitter.Location = new Point(0, 773);
            panelTabPressureGreenJitter.Margin = new Padding(0, 3, 0, 3);
            panelTabPressureGreenJitter.Size = new Size(156, 49);
            panelTabPressureGreenJitter.TabIndex = 110;
            panelTabPressureGreenJitter.Controls.Add(panel14);
            panelTabPressureGreenJitter.Controls.Add(cmbxTabPressureGreenJitter);
            #endregion

            #region panel14
            panel14.Location = new Point(0, 3);
            panel14.Margin = new Padding(0, 3, 0, 0);
            panel14.Size = new Size(156, 22);
            panel14.TabIndex = 107;
            panel14.Controls.Add(spinTabPressureMinGreenJitter);
            panel14.Controls.Add(lblTabPressureGreenJitter);
            panel14.Controls.Add(spinTabPressureMaxGreenJitter);
            #endregion

            #region spinTabPressureMinGreenJitter
            spinTabPressureMinGreenJitter.Dock = DockStyle.Right;
            spinTabPressureMinGreenJitter.Location = new Point(74, 0);
            spinTabPressureMinGreenJitter.Margin = new Padding(3, 3, 0, 3);
            spinTabPressureMinGreenJitter.Size = new Size(41, 20);
            spinTabPressureMinGreenJitter.TabIndex = 108;
            spinTabPressureMinGreenJitter.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            #endregion

            #region lblTabPressureGreenJitter
            lblTabPressureGreenJitter.AutoSize = true;
            lblTabPressureGreenJitter.Dock = DockStyle.Left;
            lblTabPressureGreenJitter.Font = detailsFont;
            lblTabPressureGreenJitter.Location = new Point(0, 0);
            lblTabPressureGreenJitter.Margin = new Padding(0, 3, 0, 3);
            lblTabPressureGreenJitter.Size = new Size(64, 13);
            lblTabPressureGreenJitter.TabIndex = 0;
            lblTabPressureGreenJitter.Text = Strings.JitterGreen;
            #endregion

            #region spinTabPressureMaxGreenJitter
            spinTabPressureMaxGreenJitter.Dock = DockStyle.Right;
            spinTabPressureMaxGreenJitter.Location = new Point(115, 0);
            spinTabPressureMaxGreenJitter.Margin = new Padding(3, 3, 0, 3);
            spinTabPressureMaxGreenJitter.Size = new Size(41, 20);
            spinTabPressureMaxGreenJitter.TabIndex = 109;
            spinTabPressureMaxGreenJitter.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            #endregion

            #region cmbxTabPressureGreenJitter
            cmbxTabPressureGreenJitter.Font = detailsFont;
            cmbxTabPressureGreenJitter.IntegralHeight = false;
            cmbxTabPressureGreenJitter.ItemHeight = 13;
            cmbxTabPressureGreenJitter.Location = new Point(0, 25);
            cmbxTabPressureGreenJitter.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureGreenJitter.MaxDropDownItems = 9;
            cmbxTabPressureGreenJitter.Size = new Size(156, 21);
            cmbxTabPressureGreenJitter.TabIndex = 111;
            cmbxTabPressureGreenJitter.BackColor = Color.White;
            cmbxTabPressureGreenJitter.DisplayMember = "DisplayMember";
            cmbxTabPressureGreenJitter.DropDownHeight = 140;
            cmbxTabPressureGreenJitter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbxTabPressureGreenJitter.DropDownWidth = 20;
            cmbxTabPressureGreenJitter.FormattingEnabled = true;
            cmbxTabPressureGreenJitter.ValueMember = "ValueMember";
            cmbxTabPressureGreenJitter.MouseHover += new EventHandler(CmbxTabPressure_MouseHover);
            #endregion

            #region panelTabPressureBlueJitter
            panelTabPressureBlueJitter.AutoSize = true;
            panelTabPressureBlueJitter.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelTabPressureBlueJitter.FlowDirection = FlowDirection.TopDown;
            panelTabPressureBlueJitter.Location = new Point(0, 828);
            panelTabPressureBlueJitter.Margin = new Padding(0, 3, 0, 3);
            panelTabPressureBlueJitter.Size = new Size(156, 49);
            panelTabPressureBlueJitter.TabIndex = 115;
            panelTabPressureBlueJitter.Controls.Add(panel15);
            panelTabPressureBlueJitter.Controls.Add(cmbxTabPressureBlueJitter);
            #endregion

            #region panel15
            panel15.Location = new Point(0, 3);
            panel15.Margin = new Padding(0, 3, 0, 0);
            panel15.Size = new Size(156, 22);
            panel15.TabIndex = 112;
            panel15.Controls.Add(spinTabPressureMinBlueJitter);
            panel15.Controls.Add(lblTabPressureBlueJitter);
            panel15.Controls.Add(spinTabPressureMaxBlueJitter);
            #endregion

            #region spinTabPressureMinBlueJitter
            spinTabPressureMinBlueJitter.Dock = DockStyle.Right;
            spinTabPressureMinBlueJitter.Location = new Point(74, 0);
            spinTabPressureMinBlueJitter.Margin = new Padding(3, 3, 0, 3);
            spinTabPressureMinBlueJitter.Size = new Size(41, 20);
            spinTabPressureMinBlueJitter.TabIndex = 113;
            spinTabPressureMinBlueJitter.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            #endregion

            #region lblTabPressureBlueJitter
            lblTabPressureBlueJitter.AutoSize = true;
            lblTabPressureBlueJitter.Dock = DockStyle.Left;
            lblTabPressureBlueJitter.Font = detailsFont;
            lblTabPressureBlueJitter.Location = new Point(0, 0);
            lblTabPressureBlueJitter.Margin = new Padding(0, 3, 0, 3);
            lblTabPressureBlueJitter.Size = new Size(56, 13);
            lblTabPressureBlueJitter.TabIndex = 0;
            lblTabPressureBlueJitter.Text = Strings.JitterBlue;
            #endregion

            #region spinTabPressureMaxBlueJitter
            spinTabPressureMaxBlueJitter.Dock = DockStyle.Right;
            spinTabPressureMaxBlueJitter.Location = new Point(115, 0);
            spinTabPressureMaxBlueJitter.Margin = new Padding(3, 3, 0, 3);
            spinTabPressureMaxBlueJitter.Size = new Size(41, 20);
            spinTabPressureMaxBlueJitter.TabIndex = 114;
            spinTabPressureMaxBlueJitter.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            #endregion

            #region cmbxTabPressureBlueJitter
            cmbxTabPressureBlueJitter.Font = detailsFont;
            cmbxTabPressureBlueJitter.IntegralHeight = false;
            cmbxTabPressureBlueJitter.ItemHeight = 13;
            cmbxTabPressureBlueJitter.Location = new Point(0, 25);
            cmbxTabPressureBlueJitter.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureBlueJitter.MaxDropDownItems = 9;
            cmbxTabPressureBlueJitter.Size = new Size(156, 21);
            cmbxTabPressureBlueJitter.TabIndex = 116;
            cmbxTabPressureBlueJitter.BackColor = Color.White;
            cmbxTabPressureBlueJitter.DisplayMember = "DisplayMember";
            cmbxTabPressureBlueJitter.DropDownHeight = 140;
            cmbxTabPressureBlueJitter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbxTabPressureBlueJitter.DropDownWidth = 20;
            cmbxTabPressureBlueJitter.FormattingEnabled = true;
            cmbxTabPressureBlueJitter.ValueMember = "ValueMember";
            cmbxTabPressureBlueJitter.MouseHover += new EventHandler(CmbxTabPressure_MouseHover);
            #endregion

            #region panelTabPressureHueJitter
            panelTabPressureHueJitter.AutoSize = true;
            panelTabPressureHueJitter.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelTabPressureHueJitter.FlowDirection = FlowDirection.TopDown;
            panelTabPressureHueJitter.Location = new Point(0, 883);
            panelTabPressureHueJitter.Margin = new Padding(0, 3, 0, 3);
            panelTabPressureHueJitter.Size = new Size(156, 49);
            panelTabPressureHueJitter.TabIndex = 120;
            panelTabPressureHueJitter.Controls.Add(panel16);
            panelTabPressureHueJitter.Controls.Add(cmbxTabPressureHueJitter);
            #endregion

            #region panel16
            panel16.Location = new Point(0, 3);
            panel16.Margin = new Padding(0, 3, 0, 0);
            panel16.Size = new Size(156, 22);
            panel16.TabIndex = 117;
            panel16.Controls.Add(spinTabPressureMinHueJitter);
            panel16.Controls.Add(lblTabPressureHueJitter);
            panel16.Controls.Add(spinTabPressureMaxHueJitter);
            #endregion

            #region spinTabPressureMinHueJitter
            spinTabPressureMinHueJitter.Dock = DockStyle.Right;
            spinTabPressureMinHueJitter.Location = new Point(74, 0);
            spinTabPressureMinHueJitter.Margin = new Padding(3, 3, 0, 3);
            spinTabPressureMinHueJitter.Size = new Size(41, 20);
            spinTabPressureMinHueJitter.TabIndex = 118;
            spinTabPressureMinHueJitter.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            #endregion

            #region lblTabPressureHueJitter
            lblTabPressureHueJitter.Text = Strings.JitterHue;
            lblTabPressureHueJitter.AutoSize = true;
            lblTabPressureHueJitter.Dock = DockStyle.Left;
            lblTabPressureHueJitter.Font = detailsFont;
            lblTabPressureHueJitter.Location = new Point(0, 0);
            lblTabPressureHueJitter.Margin = new Padding(0, 3, 0, 3);
            lblTabPressureHueJitter.Size = new Size(55, 13);
            lblTabPressureHueJitter.TabIndex = 0;
            #endregion

            #region spinTabPressureMaxHueJitter
            spinTabPressureMaxHueJitter.Dock = DockStyle.Right;
            spinTabPressureMaxHueJitter.Location = new Point(115, 0);
            spinTabPressureMaxHueJitter.Margin = new Padding(3, 3, 0, 3);
            spinTabPressureMaxHueJitter.Size = new Size(41, 20);
            spinTabPressureMaxHueJitter.TabIndex = 119;
            spinTabPressureMaxHueJitter.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            #endregion

            #region cmbxTabPressureHueJitter
            cmbxTabPressureHueJitter.Font = detailsFont;
            cmbxTabPressureHueJitter.IntegralHeight = false;
            cmbxTabPressureHueJitter.ItemHeight = 13;
            cmbxTabPressureHueJitter.Location = new Point(0, 25);
            cmbxTabPressureHueJitter.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureHueJitter.MaxDropDownItems = 9;
            cmbxTabPressureHueJitter.Size = new Size(156, 21);
            cmbxTabPressureHueJitter.TabIndex = 121;
            cmbxTabPressureHueJitter.BackColor = Color.White;
            cmbxTabPressureHueJitter.DisplayMember = "DisplayMember";
            cmbxTabPressureHueJitter.DropDownHeight = 140;
            cmbxTabPressureHueJitter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbxTabPressureHueJitter.DropDownWidth = 20;
            cmbxTabPressureHueJitter.FormattingEnabled = true;
            cmbxTabPressureHueJitter.ValueMember = "ValueMember";
            cmbxTabPressureHueJitter.MouseHover += new EventHandler(CmbxTabPressure_MouseHover);
            #endregion

            #region panelTabPressureSatJitter
            panelTabPressureSatJitter.AutoSize = true;
            panelTabPressureSatJitter.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelTabPressureSatJitter.FlowDirection = FlowDirection.TopDown;
            panelTabPressureSatJitter.Location = new Point(0, 938);
            panelTabPressureSatJitter.Margin = new Padding(0, 3, 0, 3);
            panelTabPressureSatJitter.Size = new Size(156, 49);
            panelTabPressureSatJitter.TabIndex = 125;
            panelTabPressureSatJitter.Controls.Add(panel17);
            panelTabPressureSatJitter.Controls.Add(cmbxTabPressureSatJitter);
            #endregion

            #region panel17
            panel17.Location = new Point(0, 3);
            panel17.Margin = new Padding(0, 3, 0, 0);
            panel17.Size = new Size(156, 22);
            panel17.TabIndex = 122;
            panel17.Controls.Add(spinTabPressureMinSatJitter);
            panel17.Controls.Add(lblTabPressureSatJitter);
            panel17.Controls.Add(spinTabPressureMaxSatJitter);
            #endregion

            #region spinTabPressureMinSatJitter
            spinTabPressureMinSatJitter.Dock = DockStyle.Right;
            spinTabPressureMinSatJitter.Location = new Point(74, 0);
            spinTabPressureMinSatJitter.Margin = new Padding(3, 3, 0, 3);
            spinTabPressureMinSatJitter.Size = new Size(41, 20);
            spinTabPressureMinSatJitter.TabIndex = 123;
            spinTabPressureMinSatJitter.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            #endregion

            #region lblTabPressureSatJitter
            lblTabPressureSatJitter.AutoSize = true;
            lblTabPressureSatJitter.Dock = DockStyle.Left;
            lblTabPressureSatJitter.Font = detailsFont;
            lblTabPressureSatJitter.Location = new Point(0, 0);
            lblTabPressureSatJitter.Margin = new Padding(0, 3, 0, 3);
            lblTabPressureSatJitter.Size = new Size(54, 13);
            lblTabPressureSatJitter.TabIndex = 0;
            lblTabPressureSatJitter.Text = Strings.JitterSaturation;
            #endregion

            #region spinTabPressureMaxSatJitter
            spinTabPressureMaxSatJitter.Dock = DockStyle.Right;
            spinTabPressureMaxSatJitter.Location = new Point(115, 0);
            spinTabPressureMaxSatJitter.Margin = new Padding(3, 3, 0, 3);
            spinTabPressureMaxSatJitter.Size = new Size(41, 20);
            spinTabPressureMaxSatJitter.TabIndex = 124;
            spinTabPressureMaxSatJitter.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            #endregion

            #region cmbxTabPressureSatJitter
            cmbxTabPressureSatJitter.Font = detailsFont;
            cmbxTabPressureSatJitter.IntegralHeight = false;
            cmbxTabPressureSatJitter.ItemHeight = 13;
            cmbxTabPressureSatJitter.Location = new Point(0, 25);
            cmbxTabPressureSatJitter.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureSatJitter.MaxDropDownItems = 9;
            cmbxTabPressureSatJitter.Size = new Size(156, 21);
            cmbxTabPressureSatJitter.TabIndex = 126;
            cmbxTabPressureSatJitter.BackColor = Color.White;
            cmbxTabPressureSatJitter.DisplayMember = "DisplayMember";
            cmbxTabPressureSatJitter.DropDownHeight = 140;
            cmbxTabPressureSatJitter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbxTabPressureSatJitter.DropDownWidth = 20;
            cmbxTabPressureSatJitter.FormattingEnabled = true;
            cmbxTabPressureSatJitter.ValueMember = "ValueMember";
            cmbxTabPressureSatJitter.MouseHover += new EventHandler(CmbxTabPressure_MouseHover);
            #endregion

            #region panelTabPressureValueJitter
            panelTabPressureValueJitter.AutoSize = true;
            panelTabPressureValueJitter.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelTabPressureValueJitter.FlowDirection = FlowDirection.TopDown;
            panelTabPressureValueJitter.Location = new Point(0, 993);
            panelTabPressureValueJitter.Margin = new Padding(0, 3, 0, 3);
            panelTabPressureValueJitter.Size = new Size(156, 49);
            panelTabPressureValueJitter.TabIndex = 130;
            panelTabPressureValueJitter.Controls.Add(panel18);
            panelTabPressureValueJitter.Controls.Add(cmbxTabPressureValueJitter);
            #endregion

            #region panel18
            panel18.Location = new Point(0, 3);
            panel18.Margin = new Padding(0, 3, 0, 0);
            panel18.Size = new Size(156, 22);
            panel18.TabIndex = 127;
            panel18.Controls.Add(spinTabPressureMinValueJitter);
            panel18.Controls.Add(lblTabPressureValueJitter);
            panel18.Controls.Add(spinTabPressureMaxValueJitter);
            #endregion

            #region spinTabPressureMinValueJitter
            spinTabPressureMinValueJitter.Dock = DockStyle.Right;
            spinTabPressureMinValueJitter.Location = new Point(74, 0);
            spinTabPressureMinValueJitter.Margin = new Padding(3, 3, 0, 3);
            spinTabPressureMinValueJitter.Size = new Size(41, 20);
            spinTabPressureMinValueJitter.TabIndex = 128;
            spinTabPressureMinValueJitter.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            #endregion

            #region lblTabPressureValueJitter
            lblTabPressureValueJitter.AutoSize = true;
            lblTabPressureValueJitter.Dock = DockStyle.Left;
            lblTabPressureValueJitter.Font = detailsFont;
            lblTabPressureValueJitter.Location = new Point(0, 0);
            lblTabPressureValueJitter.Margin = new Padding(0, 3, 0, 3);
            lblTabPressureValueJitter.Size = new Size(62, 13);
            lblTabPressureValueJitter.TabIndex = 0;
            lblTabPressureValueJitter.Text = Strings.JitterValue;
            #endregion

            #region spinTabPressureMaxValueJitter
            spinTabPressureMaxValueJitter.Dock = DockStyle.Right;
            spinTabPressureMaxValueJitter.Location = new Point(115, 0);
            spinTabPressureMaxValueJitter.Margin = new Padding(3, 3, 0, 3);
            spinTabPressureMaxValueJitter.Size = new Size(41, 20);
            spinTabPressureMaxValueJitter.TabIndex = 129;
            spinTabPressureMaxValueJitter.Minimum = new decimal(new int[] {
            100,
            0,
            0,
            -2147483648});
            #endregion

            #region cmbxTabPressureValueJitter
            cmbxTabPressureValueJitter.Font = detailsFont;
            cmbxTabPressureValueJitter.IntegralHeight = false;
            cmbxTabPressureValueJitter.ItemHeight = 13;
            cmbxTabPressureValueJitter.Location = new Point(0, 25);
            cmbxTabPressureValueJitter.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureValueJitter.MaxDropDownItems = 9;
            cmbxTabPressureValueJitter.Size = new Size(156, 21);
            cmbxTabPressureValueJitter.TabIndex = 131;
            cmbxTabPressureValueJitter.BackColor = Color.White;
            cmbxTabPressureValueJitter.DisplayMember = "DisplayMember";
            cmbxTabPressureValueJitter.DropDownHeight = 140;
            cmbxTabPressureValueJitter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbxTabPressureValueJitter.DropDownWidth = 20;
            cmbxTabPressureValueJitter.FormattingEnabled = true;
            cmbxTabPressureValueJitter.ValueMember = "ValueMember";
            cmbxTabPressureValueJitter.MouseHover += new EventHandler(CmbxTabPressure_MouseHover);
            #endregion

            #region bttnSettings
            bttnSettings.BackColor = Color.Black;
            bttnSettings.ForeColor = Color.WhiteSmoke;
            bttnSettings.Location = new Point(0, 2899);
            bttnSettings.Margin = new Padding(0, 3, 0, 3);
            bttnSettings.Size = new Size(155, 23);
            bttnSettings.TabIndex = 132;
            bttnSettings.TextAlign = ContentAlignment.MiddleLeft;
            bttnSettings.UseVisualStyleBackColor = false;
            #endregion

            #region panelSettings
            panelSettings.AutoSize = true;
            panelSettings.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelSettings.BackColor = SystemColors.Control;
            panelSettings.FlowDirection = FlowDirection.TopDown;
            panelSettings.Location = new Point(0, 2928);
            panelSettings.Margin = new Padding(0, 3, 0, 0);
            panelSettings.Size = new Size(156, 145);
            panelSettings.TabIndex = 133;
            panelSettings.Controls.Add(bttnUpdateCurrentBrush);
            panelSettings.Controls.Add(bttnClearSettings);
            panelSettings.Controls.Add(bttnDeleteBrush);
            panelSettings.Controls.Add(bttnSaveBrush);
            #endregion

            #region bttnUpdateCurrentBrush
            bttnUpdateCurrentBrush.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            bttnUpdateCurrentBrush.Enabled = false;
            bttnUpdateCurrentBrush.Location = new Point(0, 3);
            bttnUpdateCurrentBrush.Margin = new Padding(0, 3, 0, 3);
            bttnUpdateCurrentBrush.Size = new Size(156, 23);
            bttnUpdateCurrentBrush.TabIndex = 133;
            bttnUpdateCurrentBrush.UseVisualStyleBackColor = true;
            bttnUpdateCurrentBrush.Click += new EventHandler(BttnUpdateCurrentBrush_Click);
            bttnUpdateCurrentBrush.MouseEnter += new EventHandler(BttnUpdateCurrentBrush_MouseEnter);
            #endregion

            #region bttnClearSettings
            bttnClearSettings.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            bttnClearSettings.Location = new Point(0, 61);
            bttnClearSettings.Margin = new Padding(0, 3, 0, 3);
            bttnClearSettings.Size = new Size(156, 23);
            bttnClearSettings.TabIndex = 135;
            bttnClearSettings.UseVisualStyleBackColor = true;
            bttnClearSettings.Click += new EventHandler(BttnClearSettings_Click);
            bttnClearSettings.MouseEnter += new EventHandler(BttnClearSettings_MouseEnter);
            #endregion

            #region bttnDeleteBrush
            bttnDeleteBrush.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            bttnDeleteBrush.Enabled = false;
            bttnDeleteBrush.Location = new Point(0, 90);
            bttnDeleteBrush.Margin = new Padding(0, 3, 0, 3);
            bttnDeleteBrush.Size = new Size(156, 23);
            bttnDeleteBrush.TabIndex = 136;
            bttnDeleteBrush.UseVisualStyleBackColor = true;
            bttnDeleteBrush.Click += new EventHandler(BttnDeleteBrush_Click);
            bttnDeleteBrush.MouseEnter += new EventHandler(BttnDeleteBrush_MouseEnter);
            #endregion

            #region bttnSaveBrush
            bttnSaveBrush.Location = new Point(0, 119);
            bttnSaveBrush.Margin = new Padding(0, 3, 0, 3);
            bttnSaveBrush.Size = new Size(156, 23);
            bttnSaveBrush.TabIndex = 137;
            bttnSaveBrush.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            bttnSaveBrush.UseVisualStyleBackColor = true;
            bttnSaveBrush.Click += new EventHandler(BttnSaveBrush_Click);
            bttnSaveBrush.MouseEnter += new EventHandler(BttnSaveBrush_MouseEnter);
            #endregion

            #region WinDynamicDraw
            AcceptButton = bttnOk;
            AutoScaleDimensions = new SizeF(96f, 96f);
            BackgroundImageLayout = ImageLayout.None;
            BackColor = SystemColors.ControlLight;
            CancelButton = bttnCancel;
            ClientSize = new Size(829, 541);
            Location = new Point(0, 0);
            Margin = new Padding(5, 5, 5, 5);
            DoubleBuffered = true;
            KeyPreview = true;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            SizeGripStyle = SizeGripStyle.Auto;
            Controls.Add(panelAllSettingsContainer);
            Controls.Add(topMenu);
            Controls.Add(displayCanvas);
            Load += new System.EventHandler(DynamicDrawWindow_Load);
            Resize += WinDynamicDraw_Resize;
            #endregion

            #region Resume and perform layout on them all, order is VERY delicate
            topMenu.ResumeLayout(false);
            topMenu.PerformLayout();
            displayCanvas.ResumeLayout(false);
            displayCanvas.PerformLayout();
            ((ISupportInitialize)(displayCanvas)).EndInit();
            panelUndoRedoOkCancel.ResumeLayout(false);
            panelAllSettingsContainer.ResumeLayout(false);
            panelDockSettingsContainer.ResumeLayout(false);
            panelDockSettingsContainer.PerformLayout();
            panelSettingsContainer.ResumeLayout(false);
            panelSettingsContainer.PerformLayout();
            panelBrush.ResumeLayout(false);
            panelBrush.PerformLayout();
            panelBrushAddPickColor.ResumeLayout(false);
            panelBrushAddPickColor.PerformLayout();
            ((ISupportInitialize)(sliderColorInfluence)).EndInit();
            panelColorInfluenceHSV.ResumeLayout(false);
            panelColorInfluenceHSV.PerformLayout();
            ((ISupportInitialize)(sliderBrushOpacity)).EndInit();
            ((ISupportInitialize)(sliderBrushFlow)).EndInit();
            ((ISupportInitialize)(sliderBrushRotation)).EndInit();
            ((ISupportInitialize)(sliderBrushSize)).EndInit();
            panelSpecialSettings.ResumeLayout(false);
            panelSpecialSettings.PerformLayout();
            panelChosenEffect.ResumeLayout(false);
            panelChosenEffect.PerformLayout();
            panelRGBLocks.ResumeLayout(false);
            panelRGBLocks.PerformLayout();
            panelHSVLocks.ResumeLayout(false);
            panelHSVLocks.PerformLayout();
            ((ISupportInitialize)(sliderMinDrawDistance)).EndInit();
            ((ISupportInitialize)(sliderBrushDensity)).EndInit();
            panelJitterBasics.ResumeLayout(false);
            ((ISupportInitialize)(sliderRandMinSize)).EndInit();
            ((ISupportInitialize)(sliderRandMaxSize)).EndInit();
            ((ISupportInitialize)(sliderRandRotLeft)).EndInit();
            ((ISupportInitialize)(sliderRandRotRight)).EndInit();
            ((ISupportInitialize)(sliderRandFlowLoss)).EndInit();
            ((ISupportInitialize)(sliderRandHorzShift)).EndInit();
            ((ISupportInitialize)(sliderRandVertShift)).EndInit();
            panelJitterColor.ResumeLayout(false);
            ((ISupportInitialize)(sliderJitterMinRed)).EndInit();
            ((ISupportInitialize)(sliderJitterMaxRed)).EndInit();
            ((ISupportInitialize)(sliderJitterMinGreen)).EndInit();
            ((ISupportInitialize)(sliderJitterMaxGreen)).EndInit();
            ((ISupportInitialize)(sliderJitterMinBlue)).EndInit();
            ((ISupportInitialize)(sliderJitterMaxBlue)).EndInit();
            ((ISupportInitialize)(sliderJitterMinHue)).EndInit();
            ((ISupportInitialize)(sliderJitterMaxHue)).EndInit();
            ((ISupportInitialize)(sliderJitterMinSat)).EndInit();
            ((ISupportInitialize)(sliderJitterMaxSat)).EndInit();
            ((ISupportInitialize)(sliderJitterMinVal)).EndInit();
            ((ISupportInitialize)(sliderJitterMaxVal)).EndInit();
            panelShiftBasics.ResumeLayout(false);
            ((ISupportInitialize)(sliderShiftSize)).EndInit();
            ((ISupportInitialize)(sliderShiftRotation)).EndInit();
            ((ISupportInitialize)(sliderShiftFlow)).EndInit();
            panelTabletAssignPressure.ResumeLayout(false);
            panelTabletAssignPressure.PerformLayout();
            panelTabPressureBrushOpacity.ResumeLayout(false);
            panel19.ResumeLayout(false);
            panel19.PerformLayout();
            ((ISupportInitialize)(spinTabPressureBrushOpacity)).EndInit();
            panelTabPressureBrushFlow.ResumeLayout(false);
            panel3.ResumeLayout(false);
            panel3.PerformLayout();
            ((ISupportInitialize)(spinTabPressureBrushFlow)).EndInit();
            panelTabPressureBrushSize.ResumeLayout(false);
            panel8.ResumeLayout(false);
            panel8.PerformLayout();
            ((ISupportInitialize)(spinTabPressureBrushSize)).EndInit();
            panelTabPressureBrushRotation.ResumeLayout(false);
            panel2.ResumeLayout(false);
            panel2.PerformLayout();
            ((ISupportInitialize)(spinTabPressureBrushRotation)).EndInit();
            panelTabPressureMinDrawDistance.ResumeLayout(false);
            panel1.ResumeLayout(false);
            panel1.PerformLayout();
            ((ISupportInitialize)(spinTabPressureMinDrawDistance)).EndInit();
            panelTabPressureBrushDensity.ResumeLayout(false);
            panel4.ResumeLayout(false);
            panel4.PerformLayout();
            ((ISupportInitialize)(spinTabPressureBrushDensity)).EndInit();
            panelTabPressureRandMinSize.ResumeLayout(false);
            panel5.ResumeLayout(false);
            panel5.PerformLayout();
            ((ISupportInitialize)(spinTabPressureRandMinSize)).EndInit();
            panelTabPressureRandMaxSize.ResumeLayout(false);
            panel6.ResumeLayout(false);
            panel6.PerformLayout();
            ((ISupportInitialize)(spinTabPressureRandMaxSize)).EndInit();
            panelTabPressureRandRotLeft.ResumeLayout(false);
            panel7.ResumeLayout(false);
            panel7.PerformLayout();
            ((ISupportInitialize)(spinTabPressureRandRotLeft)).EndInit();
            panelTabPressureRandRotRight.ResumeLayout(false);
            panel9.ResumeLayout(false);
            panel9.PerformLayout();
            ((ISupportInitialize)(spinTabPressureRandRotRight)).EndInit();
            panelTabPressureRandFlowLoss.ResumeLayout(false);
            panel10.ResumeLayout(false);
            panel10.PerformLayout();
            ((ISupportInitialize)(spinTabPressureRandFlowLoss)).EndInit();
            panelTabPressureRandHorShift.ResumeLayout(false);
            panel12.ResumeLayout(false);
            panel12.PerformLayout();
            ((ISupportInitialize)(spinTabPressureRandHorShift)).EndInit();
            panelTabPressureRandVerShift.ResumeLayout(false);
            panel11.ResumeLayout(false);
            panel11.PerformLayout();
            ((ISupportInitialize)(spinTabPressureRandVerShift)).EndInit();
            panelTabPressureRedJitter.ResumeLayout(false);
            panel13.ResumeLayout(false);
            panel13.PerformLayout();
            ((ISupportInitialize)(spinTabPressureMinRedJitter)).EndInit();
            ((ISupportInitialize)(spinTabPressureMaxRedJitter)).EndInit();
            panelTabPressureGreenJitter.ResumeLayout(false);
            panel14.ResumeLayout(false);
            panel14.PerformLayout();
            ((ISupportInitialize)(spinTabPressureMinGreenJitter)).EndInit();
            ((ISupportInitialize)(spinTabPressureMaxGreenJitter)).EndInit();
            panelTabPressureBlueJitter.ResumeLayout(false);
            panel15.ResumeLayout(false);
            panel15.PerformLayout();
            ((ISupportInitialize)(spinTabPressureMinBlueJitter)).EndInit();
            ((ISupportInitialize)(spinTabPressureMaxBlueJitter)).EndInit();
            panelTabPressureHueJitter.ResumeLayout(false);
            panel16.ResumeLayout(false);
            panel16.PerformLayout();
            ((ISupportInitialize)(spinTabPressureMinHueJitter)).EndInit();
            ((ISupportInitialize)(spinTabPressureMaxHueJitter)).EndInit();
            panelTabPressureSatJitter.ResumeLayout(false);
            panel17.ResumeLayout(false);
            panel17.PerformLayout();
            ((ISupportInitialize)(spinTabPressureMinSatJitter)).EndInit();
            ((ISupportInitialize)(spinTabPressureMaxSatJitter)).EndInit();
            panelTabPressureValueJitter.ResumeLayout(false);
            panel18.ResumeLayout(false);
            panel18.PerformLayout();
            ((ISupportInitialize)(spinTabPressureMinValueJitter)).EndInit();
            ((ISupportInitialize)(spinTabPressureMaxValueJitter)).EndInit();
            panelSettings.ResumeLayout(false);
            ResumeLayout(false);
            #endregion
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
        /// Detects old settings paths that this plugin used to save to, and migrates them to preserve the user's
        /// preferences and brush image directories. Saves to the modern path, then deletes the old files.
        /// </summary>
        private void MigrateLegacySettings()
        {
            /* This migrates while loading for the 3 possible locations of the settings file:
             * Documents/paint.net User Files/BrushFactorySettings.xml
             * Program Files/paint.net/UserFiles/DynamicDrawSettings.xml
             * Documents/paint.net User Files/DynamicDrawSettings.json
             * 
             * Settings were at first stored in the registry, then moved to an XML file in User Files. When the
             * newer location under the paint.net folder became available, settings were moved there as version 3
             * of the plugin came around. Since then, there's been consistent issues with denial of access by UAC
             * for being stored in Program Files, so the settings have been moved back to documents (and modernized
             * to JSON).
             */

            IUserFilesService userFilesService =
                (IUserFilesService)Services.GetService(typeof(IUserFilesService));

            string newPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "paint.net User Files", "DynamicDrawSettings.json");
            settings = new SettingsSerialization(newPath);

            if (!File.Exists(newPath))
            {
                string oldestPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "paint.net User Files", "BrushFactorySettings.xml");
                string oldPath = Path.Combine(userFilesService.UserFilesPath ?? "", "DynamicDrawSettings.xml");

                // Migrates settings from the old settings filepath.
                if (File.Exists(oldPath))
                {
                    // This goes up to version 3.3, inclusive.
                    SettingsSerialization legacySettings = new SettingsSerialization(oldPath);
                    legacySettings.LoadSavedSettings(true);
                    settings.CustomBrushes = legacySettings.CustomBrushes;
                    settings.CustomBrushImageDirectories = legacySettings.CustomBrushImageDirectories;
                    settings.UseDefaultBrushes = legacySettings.UseDefaultBrushes;
                }
                else if (File.Exists(oldestPath))
                {
                    SettingsSerialization legacySettings = new SettingsSerialization(oldestPath);
                    legacySettings.LoadSavedSettings(true);
                    settings.CustomBrushImageDirectories = legacySettings.CustomBrushImageDirectories;
                    settings.UseDefaultBrushes = legacySettings.UseDefaultBrushes;
                }

                settings.Save(true);

                // Deletes any old settings files after migration.
                if (File.Exists(oldestPath)) { File.Delete(oldestPath); }
                if (File.Exists(oldPath)) { File.Delete(oldPath); }
            }
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
            bttnToolEraser.BackColor = SystemColors.ButtonFace;
            bttnColorPicker.BackColor = SystemColors.ButtonFace;
            bttnToolOrigin.BackColor = SystemColors.ButtonFace;

            switch (toolToSwitchTo)
            {
                case Tool.Eraser:
                    bttnToolEraser.BackColor = SystemColors.ButtonShadow;
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
            angle -= sliderCanvasAngle.ValueInt * Math.PI / 180;

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
        /// Attempts to read the clipboard image, if any, and copy it to a bitmap designated to store the background.
        /// </summary>
        private void UpdateBackgroundFromClipboard(bool showErrors)
        {
            // If clipboard contains an image, read it.
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

                        displayCanvas.Refresh();
                    }
                }
                catch
                {
                    if (showErrors)
                    {
                        MessageBox.Show(Strings.ClipboardErrorUnusable,
                            Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }

                    bmpBackgroundClipboard?.Dispose();
                }
                finally
                {
                    stream?.Dispose();
                }
            }
        }

        /// <summary>
        /// Updates the top menu based on current preferences.
        /// </summary>
        private void UpdateTopMenuState()
        {
            menuSetCanvasBgImageFit.Checked = UserSettings.BackgroundDisplayMode == BackgroundDisplayMode.ClipboardFit;
            menuSetCanvasBgImageOnlyIfFits.Checked = UserSettings.BackgroundDisplayMode == BackgroundDisplayMode.ClipboardOnlyIfFits;
            menuSetCanvasBgTransparent.Checked = UserSettings.BackgroundDisplayMode == BackgroundDisplayMode.Transparent;
            menuSetCanvasBgGray.Checked = UserSettings.BackgroundDisplayMode == BackgroundDisplayMode.Gray;
            menuSetCanvasBgWhite.Checked = UserSettings.BackgroundDisplayMode == BackgroundDisplayMode.White;
            menuSetCanvasBgBlack.Checked = UserSettings.BackgroundDisplayMode == BackgroundDisplayMode.Black;
            menuBrushIndicatorSquare.Checked = UserSettings.BrushCursorPreview == BrushCursorPreview.Square;
            menuBrushIndicatorPreview.Checked = UserSettings.BrushCursorPreview == BrushCursorPreview.Preview;
            menuShowSymmetryLinesInUse.Checked = UserSettings.ShowSymmetryLinesWhenUsingSymmetry;
            menuShowMinDistanceInUse.Checked = UserSettings.ShowCircleRadiusWhenUsingMinDistance;
            menuConfirmCloseSave.Checked = UserSettings.DisableConfirmationOnCloseOrSave;
            menuColorPickerIncludesAlpha.Checked = UserSettings.ColorPickerIncludesAlpha;
            menuColorPickerSwitchesToPrevTool.Checked = UserSettings.ColorPickerSwitchesToLastTool;
            menuRemoveUnfoundImagePaths.Checked = UserSettings.RemoveBrushImagePathsWhenNotFound;
        }

        /// <summary>
        /// Updates all settings based on the currently selected brush.
        /// </summary>
        private void UpdateBrush(BrushSettings settings)
        {
            // Whether the delete brush button is enabled or not.
            bttnUpdateCurrentBrush.Enabled = currentBrushPath != null &&
                !PersistentSettings.defaultBrushes.ContainsKey(currentBrushPath);
            bttnDeleteBrush.Enabled = bttnUpdateCurrentBrush.Enabled;

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
            float multAlpha = (activeTool == Tool.Eraser || effectToDraw.Effect != null || (BlendMode)cmbxBlendMode.SelectedIndex != BlendMode.Overwrite)
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
                // Applies the color and alpha changes.
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
            bool enableColorInfluence = !chkbxColorizeBrush.Checked && activeTool != Tool.Eraser && effectToDraw.Effect == null;
            bool enableColorJitter = activeTool != Tool.Eraser && effectToDraw.Effect == null && (chkbxColorizeBrush.Checked || sliderColorInfluence.Value != 0);

            sliderBrushOpacity.Enabled = ((BlendMode)cmbxBlendMode.SelectedIndex) != BlendMode.Overwrite && activeTool != Tool.Eraser && effectToDraw.Effect == null;

            chkbxColorizeBrush.Enabled = activeTool != Tool.Eraser && effectToDraw.Effect == null;
            txtColorInfluence.Visible = enableColorInfluence;
            sliderColorInfluence.Visible = enableColorInfluence;
            panelColorInfluenceHSV.Visible = enableColorInfluence && sliderColorInfluence.Value != 0;
            chkbxLockAlpha.Enabled = activeTool != Tool.Eraser && effectToDraw.Effect == null;
            cmbxBlendMode.Enabled = activeTool != Tool.Eraser && effectToDraw.Effect == null;

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

            bttnBrushColor.Visible = (chkbxColorizeBrush.Checked || sliderColorInfluence.Value != 0) && activeTool != Tool.Eraser && effectToDraw.Effect == null;

            if ((BlendMode)cmbxBlendMode.SelectedIndex == BlendMode.Overwrite &&
                activeTool == Tool.Brush && effectToDraw.Effect == null)
            {
                txtBrushFlow.Text = String.Format("{0} {1}",
                    Strings.BrushFlowAlpha,
                    sliderBrushFlow.Value);
            }
            else
            {
                txtBrushFlow.Text = String.Format("{0} {1}",
                    Strings.BrushFlow,
                    sliderBrushFlow.Value);
            }
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
                if (sliderCanvasZoom.ValueInt < 5)
                {
                    zoom = 1;
                }
                else if (sliderCanvasZoom.ValueInt < 10)
                {
                    zoom = 3;
                }
                else if (sliderCanvasZoom.ValueInt < 20)
                {
                    zoom = 5;
                }
                else if (sliderCanvasZoom.ValueInt < 50)
                {
                    zoom = 10;
                }
                else if (sliderCanvasZoom.ValueInt < 100)
                {
                    zoom = 15;
                }
                else if (sliderCanvasZoom.ValueInt < 200)
                {
                    zoom = 30;
                }
                else if (sliderCanvasZoom.ValueInt < 500)
                {
                    zoom = 50;
                }
                else if (sliderCanvasZoom.ValueInt < 1000)
                {
                    zoom = 100;
                }
                else if (sliderCanvasZoom.ValueInt < 2000)
                {
                    zoom = 200;
                }
                else
                {
                    zoom = 300;
                }

                zoom *= Math.Sign(mouseWheelDetents);
                int newValue = sliderCanvasZoom.ValueInt + zoom;

                // When sliding past 100% zoom, snaps to it. This is a common target for users.
                if (sliderCanvasZoom.ValueInt != 100 && Math.Sign(sliderCanvasZoom.ValueInt - 100) != Math.Sign(newValue - 100))
                {
                    newValue = 100;
                }

                //Updates the corresponding slider as well (within its range).
                sliderCanvasZoom.ValueInt = Math.Clamp(
                    newValue,
                    sliderCanvasZoom.MinimumInt,
                    sliderCanvasZoom.MaximumInt);

                return;
            }

            //Calculates the zooming percent.
            float newZoomFactor = sliderCanvasZoom.ValueInt / 100f;

            //Updates the canvas zoom factor.
            canvasZoom = newZoomFactor;

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

                                        //Pads the image to be square if needed, makes fully
                                        //opaque images use intensity for alpha, and draws the
                                        //altered loaded bitmap to the brush image.
                                        using Bitmap newBmp = Utils.MakeTransparent(scaledBrushImage ?? item.Image);
                                        Bitmap brushImage = Utils.MakeBitmapSquare(newBmp);

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

                                //Pads the image to be square if needed, makes fully
                                //opaque images use intensity for alpha, and draws the
                                //altered loaded bitmap to the brush.
                                using Bitmap newBmp = Utils.MakeTransparent(scaledBrush ?? bmp);
                                brushImage = Utils.MakeBitmapSquare(newBmp);

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
        /// Every second, if the user is using a clipboard image as the background, updates the bitmap to match.
        /// </summary>
        private void ClipboardDataCheck_Tick(object sender, EventArgs e)
        {
            if ((UserSettings.BackgroundDisplayMode == BackgroundDisplayMode.ClipboardFit ||
                UserSettings.BackgroundDisplayMode == BackgroundDisplayMode.ClipboardOnlyIfFits) &&
                pluginHasLoaded && bmpBackgroundClipboard == null)
            {
                UpdateBackgroundFromClipboard(false);
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
                    // Ctrl + RMB: deletes all points.
                    if (currentKeysPressed.Contains(Keys.ControlKey))
                    {
                        symmetryOrigins.Clear();
                    }
                    // RMB: Deletes points within a small radius of the mouse.
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
            }

            //Pans the image.
            else if (e.Button == MouseButtons.Middle ||
                (e.Button == MouseButtons.Left && currentKeysPressed.Contains(Keys.ControlKey)) ||
                currentKeysPressed.Contains(Keys.Space))
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

                    if (UserSettings.ColorPickerSwitchesToLastTool)
                    {
                        SwitchTool(lastTool);
                    }
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
                m.RotateAt(sliderCanvasAngle.ValueInt, new PointF(range.X + range.Width / 2f, range.Y + range.Height / 2f));
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

            if (isUserDrawing.canvasChanged && effectToDraw.Effect != null)
            {
                ActiveEffectRender();
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
            e.Graphics.RotateTransform(sliderCanvasAngle.ValueInt);
            e.Graphics.TranslateTransform(-drawingOffsetX, -drawingOffsetY);

            // Computes the visible region of the image. This speed optimization is only used for unrotated canvases
            // due to complexity in figuring out the math for the rotated canvas at the moment.
            int leftCutoff = -Math.Min(canvas.x, 0);
            int topCutoff = -Math.Min(canvas.y, 0);
            int overshootX = -Math.Min(displayCanvas.Width - (canvas.x + canvas.width), 0);
            int overshootY = -Math.Min(displayCanvas.Height - (canvas.y + canvas.height), 0);
            float lCutoffUnzoomed = leftCutoff / canvasZoom;
            float tCutoffUnzoomed = topCutoff / canvasZoom;

            Rectangle visibleBounds = (sliderCanvasAngle.ValueInt == 0)
                ? new Rectangle(
                    leftCutoff, topCutoff,
                    canvas.width - overshootX - leftCutoff,
                    canvas.height - overshootY - topCutoff)
                : new Rectangle(0, 0, canvas.width, canvas.height);

            // Draws the background according to the selected background display mode. Note it will
            // be drawn with nearest neighbor interpolation, for speed.
            if (UserSettings.BackgroundDisplayMode == BackgroundDisplayMode.Transparent ||
                UserSettings.BackgroundDisplayMode == BackgroundDisplayMode.ClipboardFit ||
                UserSettings.BackgroundDisplayMode == BackgroundDisplayMode.ClipboardOnlyIfFits)
            {
                HatchBrush hatchBrush = new HatchBrush(HatchStyle.LargeCheckerBoard, Color.White, Color.FromArgb(191, 191, 191));
                e.Graphics.FillRectangle(hatchBrush, visibleBounds);
            }
            if (bmpBackgroundClipboard != null && (
                UserSettings.BackgroundDisplayMode == BackgroundDisplayMode.ClipboardFit || (
                    UserSettings.BackgroundDisplayMode == BackgroundDisplayMode.ClipboardOnlyIfFits &&
                    bmpBackgroundClipboard.Width == bmpCommitted.Width &&
                    bmpBackgroundClipboard.Height == bmpCommitted.Height)))
            {
                if (sliderCanvasAngle.ValueInt == 0)
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
            else if (UserSettings.BackgroundDisplayMode == BackgroundDisplayMode.White)
            {
                e.Graphics.FillRectangle(Brushes.White, visibleBounds);
            }
            else if (UserSettings.BackgroundDisplayMode == BackgroundDisplayMode.Black)
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
                isPreviewingEffect.settingsOpen || isPreviewingEffect.hoverPreview ? bmpStaged :
                isUserDrawing.stagedChanged ? bmpMerged : bmpCommitted;

            if (sliderCanvasAngle.ValueInt == 0)
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
                        selectionOutline.GetRegionReadOnly());
                }
            }

            e.Graphics.ResetTransform();

            if (activeTool == Tool.Brush || activeTool == Tool.Eraser)
            {
                //Draws the brush indicator when the user isn't drawing.
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

                    if (UserSettings.BrushCursorPreview == BrushCursorPreview.Preview && bmpBrushEffects != null)
                    {
                        e.Graphics.DrawImage(
                            bmpBrushEffects, new Point[] {
                                Point.Empty,
                                new Point(radius, 0),
                                new Point(0, radius)
                            },
                            bmpBrushEffects.GetBounds(),
                            GraphicsUnit.Pixel,
                            Utils.ColorImageAttr(0, 0, 0, 0.5f));
                    }
                    else
                    {
                        e.Graphics.DrawRectangle(Pens.Black, 0, 0, radius, radius);
                        e.Graphics.DrawRectangle(Pens.White, -1, -1, radius + 2, radius + 2);
                    }

                    e.Graphics.ResetTransform();
                }

                // Draws the minimum distance circle if min distance is in use.
                else if (finalMinDrawDistance > 0 && UserSettings.ShowCircleRadiusWhenUsingMinDistance)
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
            e.Graphics.RotateTransform(sliderCanvasAngle.ValueInt);
            e.Graphics.TranslateTransform(-drawingOffsetX, -drawingOffsetY);

            // Draws the symmetry origins for symmetry modes when it's enabled.
            if (activeTool == Tool.SetSymmetryOrigin
                || (UserSettings.ShowSymmetryLinesWhenUsingSymmetry && cmbxSymmetry.SelectedIndex != (int)SymmetryMode.None))
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
                                angle -= sliderCanvasAngle.ValueInt * Math.PI / 180;
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
        /// Positions the window according to the paint.net window.
        /// </summary>
        private void DynamicDrawWindow_Load(object sender, EventArgs e)
        {
            DesktopLocation = Owner.PointToScreen(new Point(0, 30));
            Size = new Size(Owner.ClientSize.Width, Owner.ClientSize.Height - 30);
            WindowState = Owner.WindowState;
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
        /// Handles manual resizing of any element that requires it.
        /// </summary>
        private void WinDynamicDraw_Resize(object sender, EventArgs e)
        {
            txtTooltip.MaximumSize = new Size(displayCanvas.Width, displayCanvas.Height);
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

        /// <summary>
        /// Displays a dialog allowing the user to edit effect settings.
        /// </summary>
        private void BttnChooseEffectSettings_Click(object sender, EventArgs e)
        {
            ActiveEffectPrepareAndPreview(effectToDraw, true);
        }

        private void BttnChooseEffectSettings_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.ChooseEffectSettingsTip);
            if (effectToDraw.Effect != null)
            {
                isPreviewingEffect.hoverPreview = true;
                displayCanvas.Refresh();
            }
        }

        private void BttnChooseEffectSettings_MouseLeave(object sender, EventArgs e)
        {
            isPreviewingEffect.hoverPreview = false;
            displayCanvas.Refresh();
        }

        private void BttnBlendMode_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.BlendModeTip);
        }

        private void BttnBlendMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateEnabledControls();
            SliderBrushFlow_ValueChanged(null, null);
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
            if (effectToDraw.Effect != null)
            {
                isPreviewingEffect.hoverPreview = true;
                displayCanvas.Refresh();
            }
        }

        /// <summary>
        /// Displays loaded effects by their associated icon and name in the combobox dropdown.
        /// </summary>
        private void CmbxChosenEffect_DrawItem(object sender, DrawItemEventArgs e)
        {
            var effect = effectOptions[e.Index];

            //Constrains the image drawing space of each item's picture so it
            //draws without distortion, which is why size is height * height.
            Rectangle pictureLocation = new Rectangle(2, e.Bounds.Top,
                e.Bounds.Height, e.Bounds.Height);

            //Repaints white over the image and text area.
            e.Graphics.FillRectangle(
                Brushes.White,
                new Rectangle(2, e.Bounds.Top, e.Bounds.Width, e.Bounds.Height));

            //Draws the image of the current item to be repairennted.
            if (effect.Item2 != null && effect.Item2.Image != null)
            {
                e.Graphics.DrawImage(effect.Item2.Image, pictureLocation);
            }

            int textPosition = (effect.Item2 == null)
                ? e.Bounds.X + 4
                : e.Bounds.X + pictureLocation.Width + 2;

            e.Graphics.DrawString(
                effect.Item1,
                cmbxChosenEffect.Font,
                Brushes.Black,
                new Point(textPosition, e.Bounds.Y + 6));
        }

        private void CmbxChosenEffect_MouseLeave(object sender, EventArgs e)
        {
            isPreviewingEffect.hoverPreview = false;
            displayCanvas.Refresh();
        }

        private void CmbxChosenEffect_SelectedIndexChanged(object sender, EventArgs e)
        {
            isPreviewingEffect.hoverPreview = false;

            // Open the configuration dialog when changing the chosen effect. It won't be opened when the plugin
            // first opens (indicated by whether bmpBrushEffects is null or not).
            if (pluginHasLoaded)
            {
                ActiveEffectPrepareAndPreview();
            }
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
            if (!UserSettings.DisableConfirmationOnCloseOrSave && undoHistory.Count > 0 && !bttnCancel.Focused &&
                MessageBox.Show(Strings.ConfirmCancel, Strings.Confirm, MessageBoxButtons.YesNo) != DialogResult.Yes)
            {
                DialogResult = DialogResult.None;
                return;
            }

            //Disables the button so it can't accidentally be called twice.
            bttnCancel.Enabled = false;

            Close();
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
            currentBrushPath = null;
            bttnUpdateCurrentBrush.Enabled = false;
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
            if (!UserSettings.DisableConfirmationOnCloseOrSave && undoHistory.Count > 0 && !bttnOk.Focused &&
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

            Close();
        }

        private void BttnOk_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.OkTip);
        }

        /// <summary>
        /// Opens the preferences dialog to define persistent settings.
        /// </summary>
        private void BttnUpdateCurrentBrush_Click(object sender, EventArgs e)
        {
            settings.CustomBrushes[currentBrushPath] = CreateSettingsObjectFromCurrentSettings();
        }

        private void BttnUpdateCurrentBrush_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.UpdateCurrentBrushTip);
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

                    if (effectToDraw.Effect == null)
                    {
                        Utils.ColorImage(bmpStaged, ColorBgra.Black, 0);
                    }
                    else
                    {
                        ActiveEffectRender();
                    }
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

                settings.CustomBrushes.Add(inputText, CreateSettingsObjectFromCurrentSettings());

                // Deselect whatever's selected and add a brush as selected.
                foreach (ListViewItem item in listviewBrushPicker.Items)
                {
                    item.Selected = false;
                }

                listviewBrushPicker.Items.Add(new ListViewItem(inputText) { Selected = true });
                listviewBrushPicker.AutoResizeColumn(0, ColumnHeaderAutoResizeStyle.ColumnContent);
                currentBrushPath = inputText;
                bttnUpdateCurrentBrush.Enabled = true;
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

                    if (effectToDraw.Effect == null)
                    {
                        Utils.ColorImage(bmpStaged, ColorBgra.Black, 0);
                    }
                    else
                    {
                        ActiveEffectRender();
                    }
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
            if ((BlendMode)cmbxBlendMode.SelectedIndex == BlendMode.Overwrite &&
                activeTool == Tool.Brush && effectToDraw.Effect == null)
            {
                txtBrushFlow.Text = String.Format("{0} {1}",
                    Strings.BrushFlowAlpha,
                    sliderBrushFlow.Value);
            }
            else
            {
                txtBrushFlow.Text = String.Format("{0} {1}",
                    Strings.BrushFlow,
                    sliderBrushFlow.Value);
            }

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

        private void SliderCanvasZoom_ValueChanged(object sender, float e)
        {
            Zoom(0, false);
        }

        private void SliderCanvasAngle_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.CanvasAngleTip);
        }

        private void SliderCanvasAngle_ValueChanged(object sender, float e)
        {
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