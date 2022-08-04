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
        /// The maximum number of colors allowed for a palette.
        /// </summary>
        private const int paletteMaxColors = 256;

        /// <summary>
        /// The list of palettes, with the filename (no file extension) as the key, and the path to the file, including
        /// extension, as the value.
        /// </summary>
        private BindingList<Tuple<string, string>> paletteOptions;

        /// <summary>
        /// Contains the list of all blend mode options for brush strokes.
        /// </summary>
        private readonly BindingList<Tuple<string, BlendMode>> blendModeOptions;

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
        /// Tracks whatever the theme inherited from paint.net was, so it can be used if set.
        /// </summary>
        private ThemeName detectedTheme;

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
        private BasicButton bttnCancel;
        private BasicButton bttnOk;

        private IContainer components;
        internal PictureBox displayCanvas;

        private FlowLayoutPanel topMenu;
        private BasicButton menuOptions;
        private BasicButton menuRedo;
        private BasicButton menuUndo;
        private ToolStripMenuItem menuSetTheme, menuSetThemeDefault, menuSetThemeLight, menuSetThemeDark;
        private ToolStripMenuItem menuResetCanvas, menuSetCanvasBackground, menuDisplaySettings;
        private ToolStripMenuItem menuSetCanvasBgImage, menuSetCanvasBgImageFit, menuSetCanvasBgImageOnlyIfFits;
        private ToolStripMenuItem menuSetCanvasBgTransparent, menuSetCanvasBgGray, menuSetCanvasBgWhite, menuSetCanvasBgBlack;
        private ToolStripMenuItem menuBrushIndicator, menuBrushIndicatorSquare, menuBrushIndicatorPreview;
        private ToolStripMenuItem menuShowSymmetryLinesInUse, menuShowMinDistanceInUse;
        private ToolStripMenuItem menuBrushImageDirectories, menuKeyboardShortcutsDialog;
        private ToolStripMenuItem menuColorPickerIncludesAlpha, menuColorPickerSwitchesToPrevTool;
        private ToolStripMenuItem menuRemoveUnfoundImagePaths, menuConfirmCloseSave;
        private BasicButton menuCanvasZoomBttn, menuCanvasAngleBttn;
        private Slider sliderCanvasZoom, sliderCanvasAngle;
        private ToolStripMenuItem menuCanvasZoomReset, menuCanvasZoomFit, menuCanvasZoomTo;
        private ToolStripMenuItem menuCanvasAngleReset, menuCanvasAngle90, menuCanvasAngle180, menuCanvasAngle270, menuCanvasAngleTo;
        private SwatchBox menuActiveColors, menuPalette;
        private ComboBox cmbxPaletteDropdown;
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

        private FlowLayoutPanel panelOkCancel;
        private ToggleButton bttnColorPicker;
        private Panel panelAllSettingsContainer;
        private Panel panelDockSettingsContainer;
        private ToggleButton bttnToolBrush;
        private ToggleButton bttnToolOrigin;
        private ToggleButton bttnToolEraser;
        private FlowLayoutPanel panelSettingsContainer;
        private Accordion bttnBrushControls;
        private FlowLayoutPanel panelBrush;
        private DoubleBufferedListView listviewBrushImagePicker;
        private Panel panelBrushAddPickColor;
        private ToggleButton chkbxColorizeBrush;
        private BasicButton bttnAddBrushImages;
        private ProgressBar brushImageLoadProgressBar;
        private Slider sliderColorInfluence;
        private FlowLayoutPanel panelColorInfluenceHSV;
        private ToggleButton chkbxColorInfluenceHue;
        private ToggleButton chkbxColorInfluenceSat;
        private ToggleButton chkbxColorInfluenceVal;
        private Panel panelChosenEffect;
        private ComboBox cmbxChosenEffect;
        private BasicButton bttnChooseEffectSettings;
        private ComboBox cmbxBlendMode;
        private Slider sliderBrushOpacity;
        private Slider sliderBrushFlow;
        private Slider sliderBrushRotation;
        private Slider sliderBrushSize;
        private Accordion bttnColorControls;
        private FlowLayoutPanel panelColorControls;
        private Slider sliderColorR, sliderColorG, sliderColorB, sliderColorA;
        private Slider sliderColorH, sliderColorS, sliderColorV;
        private Accordion bttnSpecialSettings;
        private FlowLayoutPanel panelSpecialSettings;
        private Slider sliderMinDrawDistance;
        private Slider sliderBrushDensity;
        private ToggleButton chkbxAutomaticBrushDensity;
        private ComboBox cmbxSymmetry;
        private ComboBox cmbxBrushSmoothing;
        private ToggleButton chkbxOrientToMouse;
        private ToggleButton chkbxSeamlessDrawing;
        private ToggleButton chkbxDitherDraw;
        private ToggleButton chkbxLockAlpha;
        private Panel panelRGBLocks;
        private ToggleButton chkbxLockR;
        private ToggleButton chkbxLockG;
        private ToggleButton chkbxLockB;
        private Panel panelHSVLocks;
        private ToggleButton chkbxLockHue;
        private ToggleButton chkbxLockSat;
        private ToggleButton chkbxLockVal;
        private Accordion bttnJitterBasicsControls;
        private FlowLayoutPanel panelJitterBasics;
        private Slider sliderRandMinSize;
        private Slider sliderRandMaxSize;
        private Slider sliderRandRotLeft;
        private Slider sliderRandRotRight;
        private Slider sliderRandFlowLoss;
        private Slider sliderRandHorzShift;
        private Slider sliderRandVertShift;
        private Accordion bttnJitterColorControls;
        private FlowLayoutPanel panelJitterColor;
        private Slider sliderJitterMinRed;
        private Slider sliderJitterMaxRed;
        private Slider sliderJitterMinGreen;
        private Slider sliderJitterMaxGreen;
        private Slider sliderJitterMinBlue;
        private Slider sliderJitterMaxBlue;
        private Slider sliderJitterMinHue;
        private Slider sliderJitterMaxHue;
        private Slider sliderJitterMinSat;
        private Slider sliderJitterMaxSat;
        private Slider sliderJitterMinVal;
        private Slider sliderJitterMaxVal;
        private Accordion bttnShiftBasicsControls;
        private FlowLayoutPanel panelShiftBasics;
        private Slider sliderShiftSize;
        private Slider sliderShiftRotation;
        private Slider sliderShiftFlow;
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
        private BasicButton bttnUpdateCurrentBrush;
        private BasicButton bttnClearSettings;
        private ListView listviewBrushPicker;
        private BasicButton bttnSaveBrush;
        private BasicButton bttnDeleteBrush;
        private Label txtTooltip;
        #endregion

        #region Constructors
        /// <summary>
        /// Initializes components and brushes.
        /// </summary>
        public WinDynamicDraw()
        {
            SetupGUI();

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

            // Configures items for the available palettes.
            paletteOptions = new BindingList<Tuple<string, string>>()
            {
                new Tuple<string, string>(Strings.Current, null)
            };

            cmbxPaletteDropdown.DataSource = paletteOptions;
            cmbxPaletteDropdown.DisplayMember = "Item1";
            cmbxPaletteDropdown.ValueMember = "Item2";
            cmbxPaletteDropdown.SelectedIndex = 0;

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

                // Disposes all singletons, as this main form is naturally the full lifecycle and convenient for it.
                SemanticTheme.Instance?.Dispose();
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

            token.CurrentBrushSettings.BrushColor = PdnUserSettings.userPrimaryColor;
            token.CurrentBrushSettings.BrushOpacity = PdnUserSettings.userPrimaryColor.A;

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
        /// Raises the <see cref="E:Form.FormClosing" /> event.
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

            bmpCommitted = DrawingUtils.CreateBitmapFromSurface(EnvironmentParameters.SourceSurface);
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
            UpdateTooltip(string.Empty);

            chkbxColorizeBrush.Text = Strings.ColorizeBrush;
            chkbxColorInfluenceHue.Text = Strings.HueAbbr;
            chkbxColorInfluenceSat.Text = Strings.SatAbbr;
            chkbxColorInfluenceVal.Text = Strings.ValAbbr;
            chkbxLockAlpha.Text = Strings.LockAlpha;
            chkbxLockR.Text = Strings.ColorRedAbbr;
            chkbxLockG.Text = Strings.ColorGreenAbbr;
            chkbxLockB.Text = Strings.ColorBlueAbbr;
            chkbxLockHue.Text = Strings.ColorHueAbbr;
            chkbxLockSat.Text = Strings.ColorSatAbbr;
            chkbxLockVal.Text = Strings.ColorValAbbr;
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
            base.OnKeyDown(e);

            currentKeysPressed.Add(e.KeyCode & Keys.KeyCode);

            // Fires any shortcuts that don't require the mouse wheel.
            HashSet<ShortcutContext> contexts = new HashSet<ShortcutContext>();
            if (displayCanvas.Focused) { contexts.Add(ShortcutContext.OnCanvas); }
            else { contexts.Add(ShortcutContext.OnSidebar); }

            KeyShortcutManager.FireShortcuts(KeyboardShortcuts, currentKeysPressed, false, false, contexts);

            // Display a hand icon while panning.
            if (!isUserDrawing.started && (e.Control || e.KeyCode == Keys.Space))
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

            if (!e.Control && !currentKeysPressed.Contains(Keys.Space) && !isUserPanning)
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
        /// (Re)loads the available palettes in the dropdown from the user settings.
        /// </summary>
        private void LoadPaletteOptions()
        {
            // Removes all but the first item, which is a special one representing Paint.NET's current palette. (Can't
            // just clear all and add from scratch because ComboBox can't have a bound source with zero items.)
            while (paletteOptions.Count > 1)
            {
                paletteOptions.RemoveAt(paletteOptions.Count - 1);
            }

            // Loads every registered directory for palettes.
            foreach (string entry in settings.PaletteDirectories)
            {
                try
                {
                    if (File.Exists(entry))
                    {
                        paletteOptions.Add(new Tuple<string, string>(Path.GetFileNameWithoutExtension(entry), entry));
                    }
                    else if (Directory.Exists(entry))
                    {
                        string[] files = Directory.GetFiles(entry);
                        foreach (string file in files)
                        {
                            if (Path.GetExtension(file).ToLower() == ".txt")
                            {
                                paletteOptions.Add(new Tuple<string, string>(Path.GetFileNameWithoutExtension(file), file));
                            }
                        }
                    }
                }
                catch
                {
                    // Swallow the exception and do nothing, skipping this entry.
                }
            }

            cmbxPaletteDropdown.SelectedIndex = 0; // defaults to Paint.NET's current palette.
        }

        /// <summary>
        /// Loads the palette from the given path if possible. Null is treated as a special value representing the
        /// current palette in use by Paint.NET.
        /// </summary>
        private void LoadPalette(string path)
        {
            // Loads the palette from the given path, if possible.
            if (!string.IsNullOrWhiteSpace(path))
            {
                try
                {
                    if (File.Exists(path))
                    {
                        List<Color> paletteColors = new List<Color>();
                        string[] lines = File.ReadAllLines(path);

                        for (int i = 0; i < lines.Length; i++)
                        {
                            string line = lines[i].Trim().ToLower();
                            if (Regex.Match(line, "^([0-9]|[a-f]){8}$").Success)
                            {
                                paletteColors.Add(Color.FromArgb(int.Parse(line, System.Globalization.NumberStyles.HexNumber)));

                                if (paletteColors.Count == paletteMaxColors)
                                {
                                    break;
                                }
                            }
                        }

                        menuPalette.Swatches = paletteColors;
                    }
                }
                catch
                {
                    MessageBox.Show(Strings.LoadPaletteError);
                }
            }

            // Loads the default palette for empty paths. It will fail silently if the plugin hasn't loaded.
            else
            {
                IPalettesService palettesService = (IPalettesService)Services?.GetService(typeof(IPalettesService));

                if (palettesService != null)
                {
                    List<Color> paletteColors = new List<Color>();
                    for (int i = 0; i < palettesService.CurrentPalette.Count && i < paletteMaxColors; i++)
                    {
                        paletteColors.Add((Color)palettesService.CurrentPalette[i]);
                    }

                    menuPalette.Swatches = paletteColors;
                }
                else if (pluginHasLoaded)
                {
                    MessageBox.Show(Strings.LoadPaletteError);
                }
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
                LoadPaletteOptions();

                // now that PDN's async palette service is available, force load it.
                CmbxPaletteDropdown_SelectedIndexChanged(null, null);

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
                DrawingUtils.ColorImage(bmpStaged, ColorBgra.Black, 0f);
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
                    DrawingUtils.GetRois(bmpCommitted.Width, bmpCommitted.Height));
            }
            catch
            {
                MessageBox.Show(Strings.EffectFailedToWorkError);
                cmbxChosenEffect.SelectedIndex = 0; // corresponds to no effect chosen.
                isPreviewingEffect = (false, false);
                return false;
            }

            DrawingUtils.OverwriteBits(effectToDraw.DstArgs.Bitmap, bmpStaged);
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
                menuActiveColors.Swatches[0],
                menuActiveColors.Swatches[1],
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
                BrushColor = menuActiveColors.Swatches[0],
                BrushDensity = sliderBrushDensity.ValueInt,
                BrushFlow = sliderBrushFlow.ValueInt,
                BrushImagePath = index >= 0
                    ? loadedBrushImages[index].Location ?? loadedBrushImages[index].Name
                    : fallbackToCircleBrushPath ? Strings.DefaultBrushCircle : string.Empty,
                BrushOpacity = sliderBrushOpacity.ValueInt,
                BrushRotation = sliderBrushRotation.ValueInt,
                BrushSize = sliderBrushSize.ValueInt,
                ColorInfluence = sliderColorInfluence.ValueInt,
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
                FlowChange = sliderShiftFlow.ValueInt,
                SeamlessDrawing = chkbxSeamlessDrawing.Checked,
                DoRotateWithMouse = chkbxOrientToMouse.Checked,
                MinDrawDistance = sliderMinDrawDistance.ValueInt,
                RandFlowLoss = sliderRandFlowLoss.ValueInt,
                RandHorzShift = sliderRandHorzShift.ValueInt,
                RandMaxB = sliderJitterMaxBlue.ValueInt,
                RandMaxG = sliderJitterMaxGreen.ValueInt,
                RandMaxR = sliderJitterMaxRed.ValueInt,
                RandMaxH = sliderJitterMaxHue.ValueInt,
                RandMaxS = sliderJitterMaxSat.ValueInt,
                RandMaxV = sliderJitterMaxVal.ValueInt,
                RandMaxSize = sliderRandMaxSize.ValueInt,
                RandMinB = sliderJitterMinBlue.ValueInt,
                RandMinG = sliderJitterMinGreen.ValueInt,
                RandMinR = sliderJitterMinRed.ValueInt,
                RandMinH = sliderJitterMinHue.ValueInt,
                RandMinS = sliderJitterMinSat.ValueInt,
                RandMinV = sliderJitterMinVal.ValueInt,
                RandMinSize = sliderRandMinSize.ValueInt,
                RandRotLeft = sliderRandRotLeft.ValueInt,
                RandRotRight = sliderRandRotRight.ValueInt,
                RandVertShift = sliderRandVertShift.ValueInt,
                RotChange = sliderShiftRotation.ValueInt,
                SizeChange = sliderShiftSize.ValueInt,
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
                if (!menuUndo.Enabled)
                {
                    menuUndo.Enabled = true;
                }

                //Removes all redo history.
                redoHistory.Clear();
            }

            #region apply size jitter
            // Change the brush size based on settings.
            int finalRandMinSize = Constraint.GetStrengthMappedValue(sliderRandMinSize.ValueInt,
                (int)spinTabPressureRandMinSize.Value,
                sliderRandMinSize.MaximumInt,
                pressure,
                ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureRandMinSize.SelectedItem).ValueMember);

            int finalRandMaxSize = Constraint.GetStrengthMappedValue(sliderRandMaxSize.ValueInt,
                (int)spinTabPressureRandMaxSize.Value,
                sliderRandMaxSize.MaximumInt,
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
                int tempSize = sliderBrushSize.ValueInt;
                if (isGrowingSize)
                {
                    tempSize += sliderShiftSize.ValueInt;
                }
                else
                {
                    tempSize -= sliderShiftSize.ValueInt;
                }
                if (tempSize > sliderBrushSize.Maximum)
                {
                    tempSize = sliderBrushSize.MaximumInt;
                    isGrowingSize = !isGrowingSize; //handles values < 0.
                }
                else if (tempSize < sliderBrushSize.Minimum)
                {
                    tempSize = sliderBrushSize.MinimumInt;
                    isGrowingSize = !isGrowingSize;
                }

                sliderBrushSize.Value = Math.Clamp(tempSize,
                    sliderBrushSize.Minimum, sliderBrushSize.Maximum);
            }

            // Updates the brush flow (doesn't affect this brush stroke).
            if (sliderShiftFlow.Value != 0)
            {
                int tempFlow = sliderBrushFlow.ValueInt;
                if (isGrowingFlow)
                {
                    tempFlow += sliderShiftFlow.ValueInt;
                }
                else
                {
                    tempFlow -= sliderShiftFlow.ValueInt;
                }
                if (tempFlow > sliderBrushFlow.Maximum)
                {
                    tempFlow = sliderBrushFlow.MaximumInt;
                    isGrowingFlow = !isGrowingFlow; //handles values < 0.
                }
                else if (tempFlow < sliderBrushFlow.Minimum)
                {
                    tempFlow = sliderBrushFlow.MinimumInt;
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
                // be manually redrawn when modifying brush flow. This is done to avoid editing sliderBrushFlow and
                // having to use an extra variable to mitigate the cumulative effect it would cause.
                UpdateBrushImage();
            }

            if (sliderShiftRotation.Value != 0)
            {
                int tempRot = sliderBrushRotation.ValueInt + sliderShiftRotation.ValueInt;
                if (tempRot > sliderBrushRotation.Maximum)
                {
                    //The range goes negative, and is a total of 2 * max.
                    tempRot -= (2 * sliderBrushRotation.MaximumInt);
                }
                else if (tempRot < sliderBrushRotation.Minimum)
                {
                    tempRot += (2 * sliderBrushRotation.MaximumInt) - Math.Abs(tempRot);
                }

                sliderBrushRotation.Value = Math.Clamp(tempRot,
                    sliderBrushRotation.Minimum, sliderBrushRotation.Maximum);
            }
            #endregion

            #region apply position jitter
            int finalRandHorzShift = Math.Clamp(Constraint.GetStrengthMappedValue(sliderRandHorzShift.ValueInt,
                (int)spinTabPressureRandHorShift.Value,
                sliderRandHorzShift.MaximumInt,
                pressure,
                ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureRandHorShift.SelectedItem).ValueMember),
                0, 100);

            int finalRandVertShift = Math.Clamp(Constraint.GetStrengthMappedValue(sliderRandVertShift.ValueInt,
                (int)spinTabPressureRandVerShift.Value,
                sliderRandVertShift.MaximumInt,
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
            int finalBrushRotation = Constraint.GetStrengthMappedValue(sliderBrushRotation.ValueInt,
                (int)spinTabPressureBrushRotation.Value,
                sliderBrushRotation.MaximumInt,
                pressure,
                ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureBrushRotation.SelectedItem).ValueMember);

            int finalRandRotLeft = Constraint.GetStrengthMappedValue(sliderRandRotLeft.ValueInt,
                (int)spinTabPressureRandRotLeft.Value,
                sliderRandRotLeft.MaximumInt,
                pressure,
                ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureRandRotLeft.SelectedItem).ValueMember);

            int finalRandRotRight = Constraint.GetStrengthMappedValue(sliderRandRotRight.ValueInt,
                (int)spinTabPressureRandRotRight.Value,
                sliderRandRotRight.MaximumInt,
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
            int finalRandFlowLoss = Math.Clamp(Constraint.GetStrengthMappedValue(sliderRandFlowLoss.ValueInt,
                (int)spinTabPressureRandFlowLoss.Value,
                sliderRandFlowLoss.MaximumInt,
                pressure,
                ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureRandFlowLoss.SelectedItem).ValueMember),
                0, 255);
            #endregion

            #region apply color jitter
            ImageAttributes recolorMatrix = null;
            ColorBgra adjustedColor = menuActiveColors.Swatches[0];
            int newFlowLoss = random.Next(finalRandFlowLoss);
            adjustedColor.A = (byte)Math.Round(Math.Clamp(sliderBrushFlow.Value - newFlowLoss, 0f, 255f));

            if (activeTool == Tool.Eraser || effectToDraw.Effect != null)
            {
                if (newFlowLoss != 0)
                {
                    recolorMatrix = DrawingUtils.ColorImageAttr(1, 1, 1, (255 - newFlowLoss) / 255f);
                }
            }
            else if (chkbxColorizeBrush.Checked || sliderColorInfluence.Value != 0 || cmbxBlendMode.SelectedIndex == (int)BlendMode.Overwrite)
            {
                int finalJitterMaxRed = Math.Clamp(Constraint.GetStrengthMappedValue(sliderJitterMaxRed.ValueInt,
                    (int)spinTabPressureMaxRedJitter.Value,
                    sliderJitterMaxRed.MaximumInt,
                    pressure,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureRedJitter.SelectedItem).ValueMember),
                    0, 100);

                int finalJitterMinRed = Math.Clamp(Constraint.GetStrengthMappedValue(sliderJitterMinRed.ValueInt,
                    (int)spinTabPressureMinRedJitter.Value,
                    sliderJitterMinRed.MaximumInt,
                    pressure,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureRedJitter.SelectedItem).ValueMember),
                    0, 100);

                int finalJitterMaxGreen = Math.Clamp(Constraint.GetStrengthMappedValue(sliderJitterMaxGreen.ValueInt,
                    (int)spinTabPressureMaxGreenJitter.Value,
                    sliderJitterMaxGreen.MaximumInt,
                    pressure,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureGreenJitter.SelectedItem).ValueMember),
                    0, 100);

                int finalJitterMinGreen = Math.Clamp(Constraint.GetStrengthMappedValue(sliderJitterMinGreen.ValueInt,
                    (int)spinTabPressureMinGreenJitter.Value,
                    sliderJitterMinGreen.MaximumInt,
                    pressure,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureGreenJitter.SelectedItem).ValueMember),
                    0, 100);

                int finalJitterMaxBlue = Math.Clamp(Constraint.GetStrengthMappedValue(sliderJitterMaxBlue.ValueInt,
                    (int)spinTabPressureMaxBlueJitter.Value,
                    sliderJitterMaxBlue.MaximumInt,
                    pressure,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureBlueJitter.SelectedItem).ValueMember),
                    0, 100);

                int finalJitterMinBlue = Math.Clamp(Constraint.GetStrengthMappedValue(sliderJitterMinBlue.ValueInt,
                    (int)spinTabPressureMinBlueJitter.Value,
                    sliderJitterMinBlue.MaximumInt,
                    pressure,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureBlueJitter.SelectedItem).ValueMember),
                    0, 100);

                int finalJitterMaxHue = Math.Clamp(Constraint.GetStrengthMappedValue(sliderJitterMaxHue.ValueInt,
                    (int)spinTabPressureMaxHueJitter.Value,
                    sliderJitterMaxHue.MaximumInt,
                    pressure,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureHueJitter.SelectedItem).ValueMember),
                    0, 100);

                int finalJitterMinHue = Math.Clamp(Constraint.GetStrengthMappedValue(sliderJitterMinHue.ValueInt,
                    (int)spinTabPressureMinHueJitter.Value,
                    sliderJitterMinHue.MaximumInt,
                    pressure,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureHueJitter.SelectedItem).ValueMember),
                    0, 100);

                int finalJitterMaxSat = Math.Clamp(Constraint.GetStrengthMappedValue(sliderJitterMaxSat.ValueInt,
                    (int)spinTabPressureMaxSatJitter.Value,
                    sliderJitterMaxSat.MaximumInt,
                    pressure,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureSatJitter.SelectedItem).ValueMember),
                    0, 100);

                int finalJitterMinSat = Math.Clamp(Constraint.GetStrengthMappedValue(sliderJitterMinSat.ValueInt,
                    (int)spinTabPressureMinSatJitter.Value,
                    sliderJitterMinSat.MaximumInt,
                    pressure,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureSatJitter.SelectedItem).ValueMember),
                    0, 100);

                int finalJitterMaxVal = Math.Clamp(Constraint.GetStrengthMappedValue(sliderJitterMaxVal.ValueInt,
                    (int)spinTabPressureMaxValueJitter.Value,
                    sliderJitterMaxVal.MaximumInt,
                    pressure,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureValueJitter.SelectedItem).ValueMember),
                    0, 100);

                int finalJitterMinVal = Math.Clamp(Constraint.GetStrengthMappedValue(sliderJitterMinVal.ValueInt,
                    (int)spinTabPressureMinValueJitter.Value,
                    sliderJitterMinVal.MaximumInt,
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

                    float newRed = menuActiveColors.Swatches[0].R / 255f;
                    float newGreen = menuActiveColors.Swatches[0].G / 255f;
                    float newBlue = menuActiveColors.Swatches[0].B / 255f;

                    //Sets RGB color jitter.
                    if (jitterRgb)
                    {
                        newBlue = Math.Clamp((menuActiveColors.Swatches[0].B / 2.55f
                            - random.Next(finalJitterMinBlue)
                            + random.Next(finalJitterMaxBlue)) / 100f, 0, 1);

                        newGreen = Math.Clamp((menuActiveColors.Swatches[0].G / 2.55f
                            - random.Next(finalJitterMinGreen)
                            + random.Next(finalJitterMaxGreen)) / 100f, 0, 1);

                        newRed = Math.Clamp((menuActiveColors.Swatches[0].R / 2.55f
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

                    recolorMatrix = DrawingUtils.ColorImageAttr(newRed, newGreen, newBlue, newAlpha);
                    adjustedColor = ColorBgra.FromBgra(
                        (byte)Math.Round(newBlue * 255),
                        (byte)Math.Round(newGreen * 255),
                        (byte)Math.Round(newRed * 255),
                        (byte)Math.Round(newAlpha * 255));
                }
            }
            #endregion

            byte finalOpacity = (byte)Math.Clamp(Constraint.GetStrengthMappedValue(sliderBrushOpacity.ValueInt,
                (int)spinTabPressureBrushOpacity.Value,
                sliderBrushOpacity.MaximumInt,
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
                DrawingUtils.OverwriteBits(bmpCommitted, bmpMerged);
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
                        ? DrawingUtils.RotateImage(bmpBrushEffects, rotation, adjustedColor.A)
                        : DrawingUtils.RotateImage(bmpBrushEffects, rotation);
                }
                else if (isJagged)
                {
                    bmpBrushRot = DrawingUtils.AliasImageCopy(bmpBrushEffects, 255);
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

                            using (Bitmap bmpBrushRotScaled = DrawingUtils.ScaleImage(bmpBrushRot, new Size(scaleFactor, scaleFactor), false, false, recolorMatrix,
                                (CmbxSmoothing.Smoothing)cmbxBrushSmoothing.SelectedIndex))
                            {
                                if (activeTool == Tool.Eraser || effectToDraw.Effect != null)
                                {
                                    DrawingUtils.OverwriteMasked(
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
                                    DrawingUtils.DrawMasked(
                                        bmpToDrawOn,
                                        bmpBrushRotScaled,
                                        new Point(
                                            (int)Math.Round(rotatedLoc.X - (scaleFactor / 2f)),
                                            (int)Math.Round(rotatedLoc.Y - (scaleFactor / 2f))),
                                        (adjustedColor, newFlowLoss, finalOpacity),
                                        chkbxColorizeBrush.Checked ? null : sliderColorInfluence.Value == 0 ? (100, false, false, false) : (
                                            sliderColorInfluence.ValueInt, chkbxColorInfluenceHue.Checked,
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
                            using (Bitmap bmpBrushRotScaled = DrawingUtils.ScaleImage(
                                bmpBrushRot, new Size(scaleFactor, scaleFactor), !symmetryX, !symmetryY, null, (CmbxSmoothing.Smoothing)cmbxBrushSmoothing.SelectedIndex))
                            {
                                if (activeTool == Tool.Eraser || effectToDraw.Effect != null)
                                {
                                    DrawingUtils.OverwriteMasked(
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
                                    DrawingUtils.DrawMasked(
                                        bmpToDrawOn,
                                        bmpBrushRotScaled,
                                        new Point(
                                            (int)Math.Round(origin.X - halfScaleFactor + (symmetryX ? xDist : -xDist)),
                                            (int)Math.Round(origin.Y - halfScaleFactor + (symmetryY ? yDist : -yDist))),
                                        (adjustedColor, newFlowLoss, finalOpacity),
                                        chkbxColorizeBrush.Checked ? null : sliderColorInfluence.Value == 0 ? (100, false, false, false) : (
                                            sliderColorInfluence.ValueInt, chkbxColorInfluenceHue.Checked,
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
                            using (Bitmap bmpBrushRotScaled = DrawingUtils.ScaleImage(bmpBrushRot, new Size(scaleFactor, scaleFactor), false, false, null,
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
                                        DrawingUtils.OverwriteMasked(
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
                                        DrawingUtils.DrawMasked(
                                            bmpToDrawOn,
                                            bmpBrushRotScaled,
                                            new Point(
                                                (int)Math.Round(transformedPoint.X - halfScaleFactor),
                                                (int)Math.Round(transformedPoint.Y - halfScaleFactor)),
                                            (adjustedColor, newFlowLoss, finalOpacity),
                                            chkbxColorizeBrush.Checked ? null : sliderColorInfluence.Value == 0 ? (100, false, false, false) : (
                                                sliderColorInfluence.ValueInt, chkbxColorInfluenceHue.Checked,
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
                            using (Bitmap bmpBrushRotScaled = DrawingUtils.ScaleImage(
                                bmpBrushRot, new Size(scaleFactor, scaleFactor), false, false, null,
                                (CmbxSmoothing.Smoothing)cmbxBrushSmoothing.SelectedIndex))
                            {
                                for (int i = 0; i < numPoints; i++)
                                {
                                    if (activeTool == Tool.Eraser || effectToDraw.Effect != null)
                                    {
                                        DrawingUtils.OverwriteMasked(
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
                                        DrawingUtils.DrawMasked(
                                            bmpToDrawOn,
                                            bmpBrushRotScaled,
                                            new Point(
                                                (int)Math.Round(origin.X - (scaleFactor / 2f) + (float)(dist * Math.Cos(angle))),
                                                (int)Math.Round(origin.Y - (scaleFactor / 2f) + (float)(dist * Math.Sin(angle)))),
                                            (adjustedColor, newFlowLoss, finalOpacity),
                                            chkbxColorizeBrush.Checked ? null : sliderColorInfluence.Value == 0 ? (100, false, false, false) : (
                                                sliderColorInfluence.ValueInt, chkbxColorInfluenceHue.Checked,
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
                new PointF(loc.X + halfPixelOffset, loc.Y + halfPixelOffset),
                true, true, false);

            if (rotatedLoc.X >= 0 && rotatedLoc.Y >= 0 &&
                rotatedLoc.X <= bmpCommitted.Width - 1 &&
                rotatedLoc.Y <= bmpCommitted.Height - 1)
            {
                var pixel = bmpCommitted.GetPixel(
                    (int)Math.Round(rotatedLoc.X),
                    (int)Math.Round(rotatedLoc.Y));

                if (!UserSettings.ColorPickerIncludesAlpha)
                {
                    pixel = Color.FromArgb(sliderBrushOpacity.ValueInt, pixel);
                }

                UpdateBrushColor(pixel, true);
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
                        shortcut.GetDataAsInt(sliderBrushFlow.ValueInt,
                        sliderBrushFlow.MinimumInt,
                        sliderBrushFlow.MaximumInt);
                    break;
                case ShortcutTarget.FlowShift:
                    sliderBrushFlow.Value =
                        shortcut.GetDataAsInt(sliderBrushFlow.ValueInt,
                        sliderBrushFlow.MinimumInt,
                        sliderBrushFlow.MaximumInt);
                    break;
                case ShortcutTarget.AutomaticBrushDensity:
                    chkbxAutomaticBrushDensity.Checked = shortcut.GetDataAsBool(chkbxAutomaticBrushDensity.Checked);
                    break;
                case ShortcutTarget.BrushStrokeDensity:
                    sliderBrushDensity.Value =
                        shortcut.GetDataAsInt(sliderBrushDensity.ValueInt,
                        sliderBrushDensity.MinimumInt,
                        sliderBrushDensity.MaximumInt);
                    break;
                case ShortcutTarget.CanvasZoom:
                    sliderCanvasZoom.ValueInt =
                        shortcut.GetDataAsInt(sliderCanvasZoom.ValueInt,
                        sliderCanvasZoom.MinimumInt, sliderCanvasZoom.MaximumInt);
                    break;
                case ShortcutTarget.Color:
                    UpdateBrushColor(shortcut.GetDataAsColor(), true);
                    break;
                case ShortcutTarget.ColorizeBrush:
                    chkbxColorizeBrush.Checked = shortcut.GetDataAsBool(chkbxColorizeBrush.Checked);
                    UpdateBrushImage();
                    break;
                case ShortcutTarget.ColorInfluence:
                    sliderColorInfluence.Value = shortcut.GetDataAsInt(sliderColorInfluence.ValueInt,
                        sliderColorInfluence.MinimumInt, sliderColorInfluence.MaximumInt);
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
                        shortcut.GetDataAsInt(sliderJitterMaxBlue.ValueInt,
                        sliderJitterMaxBlue.MinimumInt, sliderJitterMaxBlue.MaximumInt);
                    break;
                case ShortcutTarget.JitterBlueMin:
                    sliderJitterMinBlue.Value =
                        shortcut.GetDataAsInt(sliderJitterMinBlue.ValueInt,
                        sliderJitterMinBlue.MinimumInt, sliderJitterMinBlue.MaximumInt);
                    break;
                case ShortcutTarget.JitterGreenMax:
                    sliderJitterMaxGreen.Value =
                        shortcut.GetDataAsInt(sliderJitterMaxGreen.ValueInt,
                        sliderJitterMaxGreen.MinimumInt, sliderJitterMaxGreen.MaximumInt);
                    break;
                case ShortcutTarget.JitterGreenMin:
                    sliderJitterMinGreen.Value =
                        shortcut.GetDataAsInt(sliderJitterMinGreen.ValueInt,
                        sliderJitterMinGreen.MinimumInt, sliderJitterMinGreen.MaximumInt);
                    break;
                case ShortcutTarget.JitterHorSpray:
                    sliderRandHorzShift.Value =
                        shortcut.GetDataAsInt(sliderRandHorzShift.ValueInt,
                        sliderRandHorzShift.MinimumInt, sliderRandHorzShift.MaximumInt);
                    break;
                case ShortcutTarget.JitterHueMax:
                    sliderJitterMaxHue.Value =
                        shortcut.GetDataAsInt(sliderJitterMaxHue.ValueInt,
                        sliderJitterMaxHue.MinimumInt, sliderJitterMaxHue.MaximumInt);
                    break;
                case ShortcutTarget.JitterHueMin:
                    sliderJitterMinHue.Value =
                        shortcut.GetDataAsInt(sliderJitterMinHue.ValueInt,
                        sliderJitterMinHue.MinimumInt, sliderJitterMinHue.MaximumInt);
                    break;
                case ShortcutTarget.JitterFlowLoss:
                    sliderRandFlowLoss.Value =
                        shortcut.GetDataAsInt(sliderRandFlowLoss.ValueInt,
                        sliderRandFlowLoss.MinimumInt, sliderRandFlowLoss.MaximumInt);
                    break;
                case ShortcutTarget.JitterMaxSize:
                    sliderRandMaxSize.Value =
                        shortcut.GetDataAsInt(sliderRandMaxSize.ValueInt,
                        sliderRandMaxSize.MinimumInt, sliderRandMaxSize.MaximumInt);
                    break;
                case ShortcutTarget.JitterMinSize:
                    sliderRandMinSize.Value =
                        shortcut.GetDataAsInt(sliderRandMinSize.ValueInt,
                        sliderRandMinSize.MinimumInt, sliderRandMinSize.MaximumInt);
                    break;
                case ShortcutTarget.JitterRedMax:
                    sliderJitterMaxRed.Value =
                        shortcut.GetDataAsInt(sliderJitterMaxRed.ValueInt,
                        sliderJitterMaxRed.MinimumInt, sliderJitterMaxRed.MaximumInt);
                    break;
                case ShortcutTarget.JitterRedMin:
                    sliderJitterMinRed.Value =
                        shortcut.GetDataAsInt(sliderJitterMinRed.ValueInt,
                        sliderJitterMinRed.MinimumInt, sliderJitterMinRed.MaximumInt);
                    break;
                case ShortcutTarget.JitterRotLeft:
                    sliderRandRotLeft.Value =
                        shortcut.GetDataAsInt(sliderRandRotLeft.ValueInt,
                        sliderRandRotLeft.MinimumInt, sliderRandRotLeft.MaximumInt);
                    break;
                case ShortcutTarget.JitterRotRight:
                    sliderRandRotRight.Value =
                        shortcut.GetDataAsInt(sliderRandRotRight.ValueInt,
                        sliderRandRotRight.MinimumInt, sliderRandRotRight.MaximumInt);
                    break;
                case ShortcutTarget.JitterSatMax:
                    sliderJitterMaxSat.Value =
                        shortcut.GetDataAsInt(sliderJitterMaxSat.ValueInt,
                        sliderJitterMaxSat.MinimumInt, sliderJitterMaxSat.MaximumInt);
                    break;
                case ShortcutTarget.JitterSatMin:
                    sliderJitterMinSat.Value =
                        shortcut.GetDataAsInt(sliderJitterMinSat.ValueInt,
                        sliderJitterMinSat.MinimumInt, sliderJitterMinSat.MaximumInt);
                    break;
                case ShortcutTarget.JitterValMax:
                    sliderJitterMaxVal.Value =
                        shortcut.GetDataAsInt(sliderJitterMaxVal.ValueInt,
                        sliderJitterMaxVal.MinimumInt, sliderJitterMaxVal.MaximumInt);
                    break;
                case ShortcutTarget.JitterValMin:
                    sliderJitterMinVal.Value =
                        shortcut.GetDataAsInt(sliderJitterMinVal.ValueInt,
                        sliderJitterMinVal.MinimumInt, sliderJitterMinVal.MaximumInt);
                    break;
                case ShortcutTarget.JitterVerSpray:
                    sliderRandVertShift.Value =
                        shortcut.GetDataAsInt(sliderRandVertShift.ValueInt,
                        sliderRandVertShift.MinimumInt, sliderRandVertShift.MaximumInt);
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
                        shortcut.GetDataAsInt(sliderMinDrawDistance.ValueInt,
                        sliderMinDrawDistance.MinimumInt, sliderMinDrawDistance.MaximumInt);
                    break;
                case ShortcutTarget.RotateWithMouse:
                    chkbxOrientToMouse.Checked = shortcut.GetDataAsBool(chkbxOrientToMouse.Checked);
                    break;
                case ShortcutTarget.Rotation:
                    sliderBrushRotation.Value =
                        shortcut.GetDataAsInt(sliderBrushRotation.ValueInt,
                        sliderBrushRotation.MinimumInt, sliderBrushRotation.MaximumInt);
                    break;
                case ShortcutTarget.RotShift:
                    sliderShiftRotation.Value =
                        shortcut.GetDataAsInt(sliderShiftRotation.ValueInt,
                        sliderShiftRotation.MinimumInt, sliderShiftRotation.MaximumInt);
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
                        shortcut.GetDataAsInt(sliderBrushSize.ValueInt,
                        sliderBrushSize.MinimumInt, sliderBrushSize.MaximumInt);
                    break;
                case ShortcutTarget.SizeShift:
                    sliderShiftSize.Value =
                        shortcut.GetDataAsInt(sliderShiftSize.ValueInt,
                        sliderShiftSize.MinimumInt, sliderShiftSize.MaximumInt);
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
                        shortcut.GetDataAsInt(sliderBrushOpacity.ValueInt,
                        sliderBrushOpacity.MinimumInt,
                        sliderBrushOpacity.MaximumInt);
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
                case ShortcutTarget.SwapPrimarySecondaryColors:
                    menuActiveColors.Swatches.Reverse();
                    UpdateBrushColor(menuActiveColors.Swatches[0], true);
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
                int maxBrushSize = sliderBrushSize.MaximumInt;

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
                int maxBrushSize = sliderBrushSize.MaximumInt;

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
        /// Sets up all GUI elements in this form.
        /// </summary>
        private void SetupGUI()
        {
            components = new Container();
            ComponentResourceManager resources = new ComponentResourceManager(typeof(WinDynamicDraw));

            Font boldFont = new Font("Microsoft Sans Serif", 12f, FontStyle.Bold);
            Font detailsFont = new Font("Microsoft Sans Serif", 8.25f);

            #region initialize every component at once
            timerRepositionUpdate = new Timer(components);
            timerClipboardDataCheck = new Timer(components);
            txtTooltip = new Label();
            displayCanvas = new PictureBox();
            topMenu = new FlowLayoutPanel();
            bttnToolBrush = new ToggleButton(false);
            dummyImageList = new ImageList(components);
            panelOkCancel = new FlowLayoutPanel();
            menuUndo = new BasicButton();
            menuRedo = new BasicButton();
            bttnOk = new BasicButton(false, true);
            bttnCancel = new BasicButton(false, true);
            brushImageLoadingWorker = new BackgroundWorker();
            bttnColorPicker = new ToggleButton(false);
            panelAllSettingsContainer = new Panel();
            panelDockSettingsContainer = new Panel();
            bttnToolEraser = new ToggleButton(false);
            bttnToolOrigin = new ToggleButton(false);
            panelSettingsContainer = new FlowLayoutPanel();
            bttnBrushControls = new Accordion(true);
            panelBrush = new FlowLayoutPanel();
            listviewBrushPicker = new ListView();
            listviewBrushImagePicker = new DoubleBufferedListView();
            panelBrushAddPickColor = new Panel();
            chkbxColorizeBrush = new ToggleButton();
            sliderColorInfluence = new Slider(ShortcutTarget.ColorInfluence, 0f);
            panelColorInfluenceHSV = new FlowLayoutPanel();
            chkbxColorInfluenceHue = new ToggleButton();
            chkbxColorInfluenceSat = new ToggleButton();
            chkbxColorInfluenceVal = new ToggleButton();
            bttnAddBrushImages = new BasicButton();
            brushImageLoadProgressBar = new ProgressBar();
            cmbxBlendMode = new ComboBox();
            sliderBrushOpacity = new Slider(ShortcutTarget.BrushOpacity, 255f);
            sliderBrushFlow = new Slider(ShortcutTarget.Flow, 255f);
            sliderBrushRotation = new Slider(ShortcutTarget.Rotation, 0f);
            sliderBrushSize = new Slider(ShortcutTarget.Size, 10f);
            bttnColorControls = new Accordion(true);
            panelColorControls = new FlowLayoutPanel();
            sliderColorR = new Slider(SliderSpecialType.RedGraph, Color.Black);
            sliderColorG = new Slider(SliderSpecialType.GreenGraph, Color.Black);
            sliderColorB = new Slider(SliderSpecialType.BlueGraph, Color.Black);
            sliderColorA = new Slider(SliderSpecialType.AlphaGraph, Color.Black);
            sliderColorH = new Slider(SliderSpecialType.HueGraph, Color.Black);
            sliderColorS = new Slider(SliderSpecialType.SatGraph, Color.Black);
            sliderColorV = new Slider(SliderSpecialType.ValGraph, Color.Black);
            bttnSpecialSettings = new Accordion(true);
            panelSpecialSettings = new FlowLayoutPanel();
            panelChosenEffect = new Panel();
            cmbxChosenEffect = new ComboBox();
            bttnChooseEffectSettings = new BasicButton();
            sliderMinDrawDistance = new Slider(ShortcutTarget.MinDrawDistance, 0f);
            sliderBrushDensity = new Slider(ShortcutTarget.BrushStrokeDensity, 10f);
            cmbxSymmetry = new ComboBox();
            cmbxBrushSmoothing = new ComboBox();
            chkbxSeamlessDrawing = new ToggleButton();
            chkbxOrientToMouse = new ToggleButton();
            chkbxDitherDraw = new ToggleButton();
            chkbxLockAlpha = new ToggleButton(false);
            panelRGBLocks = new Panel();
            chkbxLockR = new ToggleButton(false);
            chkbxLockG = new ToggleButton(false);
            chkbxLockB = new ToggleButton(false);
            panelHSVLocks = new Panel();
            chkbxLockHue = new ToggleButton(false);
            chkbxLockSat = new ToggleButton(false);
            chkbxLockVal = new ToggleButton(false);
            bttnJitterBasicsControls = new Accordion(true);
            panelJitterBasics = new FlowLayoutPanel();
            sliderRandMinSize = new Slider(ShortcutTarget.JitterMinSize, 0f);
            sliderRandMaxSize = new Slider(ShortcutTarget.JitterMaxSize, 0f);
            sliderRandRotLeft = new Slider(ShortcutTarget.JitterRotLeft, 0f);
            sliderRandRotRight = new Slider(ShortcutTarget.JitterRotRight, 0f);
            sliderRandFlowLoss = new Slider(ShortcutTarget.JitterFlowLoss, 0f);
            sliderRandHorzShift = new Slider(ShortcutTarget.JitterHorSpray, 0f);
            sliderRandVertShift = new Slider(ShortcutTarget.JitterVerSpray, 0f);
            bttnJitterColorControls = new Accordion(true);
            panelJitterColor = new FlowLayoutPanel();
            sliderJitterMinRed = new Slider(ShortcutTarget.JitterRedMin, 0f);
            sliderJitterMaxRed = new Slider(ShortcutTarget.JitterRedMin, 0f);
            sliderJitterMinGreen = new Slider(ShortcutTarget.JitterGreenMin, 0f);
            sliderJitterMaxGreen = new Slider(ShortcutTarget.JitterGreenMax, 0f);
            sliderJitterMinBlue = new Slider(ShortcutTarget.JitterBlueMin, 0f);
            sliderJitterMaxBlue = new Slider(ShortcutTarget.JitterBlueMax, 0f);
            sliderJitterMinHue = new Slider(ShortcutTarget.JitterHueMin, 0f);
            sliderJitterMaxHue = new Slider(ShortcutTarget.JitterHueMax, 0f);
            sliderJitterMinSat = new Slider(ShortcutTarget.JitterSatMin, 0f);
            sliderJitterMaxSat = new Slider(ShortcutTarget.JitterSatMax, 0f);
            sliderJitterMinVal = new Slider(ShortcutTarget.JitterValMin, 0f);
            sliderJitterMaxVal = new Slider(ShortcutTarget.JitterValMax, 0f);
            bttnShiftBasicsControls = new Accordion(true);
            panelShiftBasics = new FlowLayoutPanel();
            sliderShiftSize = new Slider(ShortcutTarget.SizeShift, 0f);
            sliderShiftRotation = new Slider(ShortcutTarget.RotShift, 0f);
            sliderShiftFlow = new Slider(ShortcutTarget.FlowShift, 0f);
            bttnTabAssignPressureControls = new Accordion(true);
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
            bttnSettings = new Accordion(true);
            panelSettings = new FlowLayoutPanel();
            bttnUpdateCurrentBrush = new BasicButton();
            bttnClearSettings = new BasicButton();
            bttnDeleteBrush = new BasicButton();
            bttnSaveBrush = new BasicButton();
            chkbxAutomaticBrushDensity = new ToggleButton();
            #endregion

            #region suspend them all, order is VERY delicate
            topMenu.SuspendLayout();
            displayCanvas.SuspendLayout();
            ((ISupportInitialize)(displayCanvas)).BeginInit();
            panelOkCancel.SuspendLayout();
            panelAllSettingsContainer.SuspendLayout();
            panelDockSettingsContainer.SuspendLayout();
            panelSettingsContainer.SuspendLayout();
            panelBrush.SuspendLayout();
            panelBrushAddPickColor.SuspendLayout();
            panelColorInfluenceHSV.SuspendLayout();
            panelColorControls.SuspendLayout();
            panelSpecialSettings.SuspendLayout();
            panelChosenEffect.SuspendLayout();
            panelRGBLocks.SuspendLayout();
            panelHSVLocks.SuspendLayout();
            panelJitterBasics.SuspendLayout();
            panelJitterColor.SuspendLayout();
            panelShiftBasics.SuspendLayout();
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
            timerRepositionUpdate.Tick += RepositionUpdate_Tick;
            #endregion

            #region timerClipboardDataCheck
            timerClipboardDataCheck.Interval = 1000;
            timerClipboardDataCheck.Tick += ClipboardDataCheck_Tick;
            timerClipboardDataCheck.Enabled = true;
            #endregion

            #region txtTooltip
            // Intentionally using the dark theme for all themes here. It works well.
            txtTooltip.BackColor = SemanticTheme.GetColor(ThemeName.Dark, ThemeSlot.MenuControlBg);
            txtTooltip.ForeColor = SemanticTheme.GetColor(ThemeName.Dark, ThemeSlot.MenuControlText);
            txtTooltip.AutoSize = true;
            txtTooltip.Dock = DockStyle.Top;
            txtTooltip.Font = new Font("Microsoft Sans Serif", 10);
            txtTooltip.Size = new Size(76, 17);
            txtTooltip.TabIndex = 0;
            #endregion

            #region displayCanvas
            displayCanvas.Controls.Add(txtTooltip);
            displayCanvas.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            displayCanvas.Location = new Point(0, 29);
            displayCanvas.Margin = Padding.Empty;
            displayCanvas.Size = new Size(656, 512);
            displayCanvas.TabStop = false;
            displayCanvas.Paint += DisplayCanvas_Paint;
            displayCanvas.MouseDown += DisplayCanvas_MouseDown;
            displayCanvas.MouseEnter += DisplayCanvas_MouseEnter;
            displayCanvas.MouseMove += DisplayCanvas_MouseMove;
            displayCanvas.MouseUp += DisplayCanvas_MouseUp;
            #endregion

            #region topMenu
            topMenu.FlowDirection = FlowDirection.LeftToRight;
            topMenu.Dock = DockStyle.Top;
            topMenu.Height = 29;
            topMenu.Margin = Padding.Empty;

            // The options button
            menuOptions = new BasicButton
            {
                Text = Strings.MenuOptions,
                Height = 29,
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
                    if (new EditCustomAssetDirectories(settings).ShowDialog() == DialogResult.OK)
                    {
                        LoadPaletteOptions();
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
            preferencesContextMenu.Items.Add(menuDisplaySettings);

            // Options -> Set theme
            menuSetTheme = new ToolStripMenuItem(Strings.MenuSetTheme);
            menuSetThemeDefault = new ToolStripMenuItem(Strings.MenuSetThemeDefault);
            menuSetThemeLight = new ToolStripMenuItem(Strings.MenuSetThemeLight);
            menuSetThemeDark = new ToolStripMenuItem(Strings.MenuSetThemeDark);

            // Options -> Set theme -> Default
            menuSetThemeDefault.Click += (a, b) =>
            {
                UserSettings.PreferredTheme = ThemePreference.Inherited;
                UpdateTopMenuState();
            };
            menuSetTheme.DropDown.Items.Add(menuSetThemeDefault);

            // Options -> Set theme -> Light
            menuSetThemeLight.Click += (a, b) =>
            {
                UserSettings.PreferredTheme = ThemePreference.Light;
                UpdateTopMenuState();
            };
            menuSetTheme.DropDown.Items.Add(menuSetThemeLight);

            // Options -> Set theme -> Dark
            menuSetThemeDark.Click += (a, b) =>
            {
                UserSettings.PreferredTheme = ThemePreference.Dark;
                UpdateTopMenuState();
            };
            menuSetTheme.DropDown.Items.Add(menuSetThemeDark);

            preferencesContextMenu.Items.Add(menuSetTheme);

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

            // Adds the undo and redo buttons
            topMenu.Controls.Add(new PanelSeparator(true, 22, 29));
            topMenu.Controls.Add(menuUndo);
            topMenu.Controls.Add(menuRedo);

            // sets up the canvas zoom button
            menuCanvasZoomBttn = new BasicButton(true)
            {
                Image = Resources.MenuZoom,
                Height = 16,
                Width = 16,
                Margin = new Padding(0, 7, 0, 0)
            };

            ContextMenuStrip CanvasZoomButtonContextMenu = new ContextMenuStrip();

            menuCanvasZoomBttn.Click += (a, b) => {
                CanvasZoomButtonContextMenu.Show(menuCanvasZoomBttn.PointToScreen(new Point(0, menuCanvasZoomBttn.Height)));
            };

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

                        return null;
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

            topMenu.Controls.Add(new PanelSeparator(true, 22, 29));
            topMenu.Controls.Add(menuCanvasZoomBttn);

            // canvas zoom slider
            sliderCanvasZoom = new Slider(
                new float[] { 1, 5, 10, 13, 17, 20, 25, 33, 50, 67, 100, 150, 200, 300, 400, 500, 600, 800, 1000, 1200, 1400, 1600, 2000, 2400, 2800, 3200, 4000, 4800, 5600, 6400 },
                100)
            {
                DiscreteStops = true,
                Width = 128,
                Height = 29,
                Margin = Padding.Empty,
                ComputeText = (value) => { return $"{value}%"; }
            };
            sliderCanvasZoom.MouseEnter += SliderCanvasZoom_MouseEnter;
            sliderCanvasZoom.ValueChanged += SliderCanvasZoom_ValueChanged;

            topMenu.Controls.Add(sliderCanvasZoom);

            // sets up the canvas angle button
            menuCanvasAngleBttn = new BasicButton(true)
            {
                Image = Resources.MenuAngle,
                Height = 16,
                Width = 16,
                Margin = new Padding(0, 7, 0, 0)
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

            topMenu.Controls.Add(new PanelSeparator(true, 22, 29));
            topMenu.Controls.Add(menuCanvasAngleBttn);

            // canvas angle slider
            sliderCanvasAngle = new Slider(ShortcutTarget.CanvasRotation, 0)
            {
                IntegerOnly = true,
                Width = 128,
                Height = 29,
                Margin = Padding.Empty,
                ComputeText = (value) => { return $"{value}°"; }
            };
            sliderCanvasAngle.MouseEnter += SliderCanvasAngle_MouseEnter;
            sliderCanvasAngle.ValueChanged += SliderCanvasAngle_ValueChanged;

            topMenu.Controls.Add(sliderCanvasAngle);

            // canvas tool buttons
            panelTools = new FlowLayoutPanel();
            panelTools.AutoSize = true;
            panelTools.FlowDirection = FlowDirection.LeftToRight;
            panelTools.Controls.Add(bttnToolBrush);
            panelTools.Controls.Add(bttnToolEraser);
            panelTools.Controls.Add(bttnColorPicker);
            panelTools.Controls.Add(bttnToolOrigin);
            panelTools.Margin = Padding.Empty;
            panelTools.Padding = Padding.Empty;
            panelTools.TabIndex = 30;

            topMenu.Controls.Add(new PanelSeparator(true, 22, 29));
            topMenu.Controls.Add(panelTools);

            // user's primary & secondary colors
            menuActiveColors = new SwatchBox(new List<Color>() { Color.Black, Color.White }, 2);
            menuActiveColors.Width = 29;
            menuActiveColors.Height = 29;
            menuActiveColors.Margin = new Padding(0, 0, 4, 0);
            menuActiveColors.SwatchClicked += (col) =>
            {
                if (col == 0)
                {
                    // Opens a color dialog to change the primary color.
                    MenuActiveColors_Click(null, null);
                }
                else
                {
                    HandleShortcut(new KeyboardShortcut() { Target = ShortcutTarget.SwapPrimarySecondaryColors });
                }
            };
            menuActiveColors.MouseEnter += MenuActiveColors_MouseEnter;

            topMenu.Controls.Add(new PanelSeparator(true, 22, 29));
            topMenu.Controls.Add(menuActiveColors);

            menuPalette = new SwatchBox(null, 3);
            menuPalette.Width = 264;
            menuPalette.Height = 29;
            menuPalette.SwatchClicked += (col) =>
            {
                UpdateBrushColor(menuPalette.Swatches[col], true);
            };

            topMenu.Controls.Add(menuPalette);

            cmbxPaletteDropdown = new ComboBox();
            cmbxPaletteDropdown.FlatStyle = FlatStyle.Flat;
            cmbxPaletteDropdown.Width = 100;
            cmbxPaletteDropdown.Margin = Padding.Empty;
            cmbxPaletteDropdown.SelectedIndexChanged += CmbxPaletteDropdown_SelectedIndexChanged;

            topMenu.Controls.Add(cmbxPaletteDropdown);
            #endregion

            #region menuUndo
            menuUndo.Enabled = false;
            menuUndo.Font = boldFont;
            menuUndo.Margin = new Padding(3, 0, 0, 0);
            menuUndo.Size = new Size(29, 29);
            menuUndo.TabIndex = 141;
            menuUndo.Text = "⮌";
            menuUndo.Click += BttnUndo_Click;
            menuUndo.MouseEnter += BttnUndo_MouseEnter;
            #endregion

            #region menuRedo
            menuRedo.Enabled = false;
            menuRedo.Font = boldFont;
            menuRedo.Margin = new Padding(0, 0, 3, 0);
            menuRedo.Size = new Size(29, 29);
            menuRedo.TabIndex = 142;
            menuRedo.Text = "⮎";
            menuRedo.Click += BttnRedo_Click;
            menuRedo.MouseEnter += BttnRedo_MouseEnter;
            #endregion

            #region bttnToolBrush
            bttnToolBrush.Checked = true;
            bttnToolBrush.Image = Resources.ToolBrush;
            bttnToolBrush.Click += BttnToolBrush_Click;
            bttnToolBrush.MouseEnter += BttnToolBrush_MouseEnter;
            bttnToolBrush.Margin = Padding.Empty;
            bttnToolBrush.Size = new Size(29, 29);
            bttnToolBrush.TabIndex = 1;
            #endregion

            #region dummyImageList
            dummyImageList.ColorDepth = ColorDepth.Depth32Bit;
            dummyImageList.TransparentColor = Color.Transparent;
            dummyImageList.ImageSize = new Size(24, 24);
            #endregion

            #region panelOkCancel
            panelOkCancel.Controls.Add(bttnOk);
            panelOkCancel.Controls.Add(bttnCancel);
            panelOkCancel.Dock = DockStyle.Bottom;
            panelOkCancel.Location = new Point(0, 484);
            panelOkCancel.Margin = new Padding(0, 3, 0, 3);
            panelOkCancel.Size = new Size(173, 32);
            panelOkCancel.TabIndex = 145;
            #endregion

            #region bttnOk
            bttnOk.Location = new Point(3, 32);
            bttnOk.Margin = new Padding(3, 3, 13, 3);
            bttnOk.Size = new Size(77, 23);
            bttnOk.TabIndex = 143;
            bttnOk.Text = Strings.Ok;
            bttnOk.Click += BttnOk_Click;
            bttnOk.MouseEnter += BttnOk_MouseEnter;
            #endregion

            #region bttnCancel
            bttnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            bttnCancel.DialogResult = DialogResult.Cancel;
            bttnCancel.Location = new Point(93, 32);
            bttnCancel.Margin = new Padding(0, 3, 0, 3);
            bttnCancel.Size = new Size(77, 23);
            bttnCancel.TabIndex = 144;
            bttnCancel.Text = Strings.Cancel;
            bttnCancel.Click += BttnCancel_Click;
            bttnCancel.MouseEnter += BttnCancel_MouseEnter;
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
            bttnColorPicker.Margin = Padding.Empty;
            bttnColorPicker.Size = new Size(29, 29);
            bttnColorPicker.TabIndex = 3;
            bttnColorPicker.Click += BttnToolColorPicker_Click;
            bttnColorPicker.MouseEnter += BttnToolColorPicker_MouseEnter;
            #endregion

            #region panelAllSettingsContainer
            panelAllSettingsContainer.Dock = DockStyle.Right;
            panelAllSettingsContainer.Location = new Point(656, 0);
            panelAllSettingsContainer.Size = new Size(173, 541);
            panelAllSettingsContainer.TabIndex = 140;
            panelAllSettingsContainer.Controls.Add(panelDockSettingsContainer);
            panelAllSettingsContainer.Controls.Add(panelOkCancel);
            #endregion

            #region panelDockSettingsContainer
            panelDockSettingsContainer.AutoScroll = true;
            panelDockSettingsContainer.Dock = DockStyle.Fill;
            panelDockSettingsContainer.Size = new Size(173, 484);
            panelDockSettingsContainer.TabIndex = 139;
            panelDockSettingsContainer.Controls.Add(panelSettingsContainer);
            #endregion

            #region BttnToolEraser
            bttnToolEraser.Image = Resources.ToolEraser;
            bttnToolEraser.Margin = Padding.Empty;
            bttnToolEraser.Size = new Size(29, 29);
            bttnToolEraser.TabIndex = 2;
            bttnToolEraser.Click += BttnToolEraser_Click;
            bttnToolEraser.MouseEnter += BttnToolEraser_MouseEnter;
            #endregion

            #region bttnToolOrigin
            bttnToolOrigin.Image = Resources.ToolOrigin;
            bttnToolOrigin.Margin = Padding.Empty;
            bttnToolOrigin.Size = new Size(29, 29);
            bttnToolOrigin.TabIndex = 4;
            bttnToolOrigin.Click += BttnToolOrigin_Click;
            bttnToolOrigin.MouseEnter += BttnToolOrigin_MouseEnter;
            #endregion

            #region panelSettingsContainer
            panelSettingsContainer.AutoSize = true;
            panelSettingsContainer.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelSettingsContainer.FlowDirection = FlowDirection.TopDown;
            panelSettingsContainer.Location = Point.Empty;
            panelSettingsContainer.Margin = new Padding(0, 3, 0, 3);
            panelSettingsContainer.Size = new Size(156, 3073);
            panelSettingsContainer.TabIndex = 138;
            panelSettingsContainer.Controls.Add(bttnBrushControls);
            panelSettingsContainer.Controls.Add(panelBrush);
            panelSettingsContainer.Controls.Add(bttnColorControls);
            panelSettingsContainer.Controls.Add(panelColorControls);
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

            #region panelBrush
            panelBrush.AutoSize = true;
            panelBrush.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelBrush.FlowDirection = FlowDirection.TopDown;
            panelBrush.Margin = new Padding(0, 3, 0, 3);
            panelBrush.Size = new Size(156, 546);
            panelBrush.TabIndex = 5;
            panelBrush.Controls.Add(listviewBrushPicker);
            panelBrush.Controls.Add(listviewBrushImagePicker);
            panelBrush.Controls.Add(panelBrushAddPickColor);
            panelBrush.Controls.Add(sliderColorInfluence);
            panelBrush.Controls.Add(panelColorInfluenceHSV);
            panelBrush.Controls.Add(cmbxBlendMode);
            panelBrush.Controls.Add(sliderBrushOpacity);
            panelBrush.Controls.Add(sliderBrushFlow);
            panelBrush.Controls.Add(sliderBrushRotation);
            panelBrush.Controls.Add(sliderBrushSize);
            #endregion

            #region listviewBrushPicker
            listviewBrushPicker.HideSelection = true;
            listviewBrushPicker.Location = new Point(0, 98);
            listviewBrushPicker.Margin = new Padding(0, 0, 0, 3);
            listviewBrushPicker.Size = new Size(156, 92);
            listviewBrushPicker.TabIndex = 8;
            listviewBrushPicker.Columns.Add("_"); // name hidden and unimportant
            listviewBrushPicker.HeaderStyle = ColumnHeaderStyle.None;
            listviewBrushPicker.UseCompatibleStateImageBehavior = false;
            listviewBrushPicker.View = View.Details;
            listviewBrushPicker.SelectedIndexChanged += ListViewBrushPicker_SelectedIndexChanged;
            listviewBrushPicker.MouseEnter += ListviewBrushPicker_MouseEnter;
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
            listviewBrushImagePicker.SelectedIndexChanged += ListViewBrushImagePicker_SelectedIndexChanged;
            listviewBrushImagePicker.MouseEnter += ListViewBrushImagePicker_MouseEnter;
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
            #endregion

            #region bttnChooseEffectSettings
            bttnChooseEffectSettings.Enabled = false;
            bttnChooseEffectSettings.Image = Resources.EffectSettingsIcon;
            bttnChooseEffectSettings.Location = new Point(123, 0);
            bttnChooseEffectSettings.Margin = Padding.Empty;
            bttnChooseEffectSettings.Size = new Size(29, 29);
            bttnChooseEffectSettings.TabIndex = 1;
            bttnChooseEffectSettings.Click += BttnChooseEffectSettings_Click;
            bttnChooseEffectSettings.MouseEnter += BttnChooseEffectSettings_MouseEnter;
            bttnChooseEffectSettings.MouseLeave += BttnChooseEffectSettings_MouseLeave;
            #endregion

            #region chkbxColorizeBrush
            chkbxColorizeBrush.AutoSize = true;
            chkbxColorizeBrush.Checked = true;
            chkbxColorizeBrush.Location = new Point(10, 48);
            chkbxColorizeBrush.Size = new Size(93, 17);
            chkbxColorizeBrush.TabIndex = 12;
            chkbxColorizeBrush.CheckedChanged += ChkbxColorizeBrush_CheckedChanged;
            chkbxColorizeBrush.MouseEnter += ChkbxColorizeBrush_MouseEnter;
            #endregion

            #region bttnAddBrushImages
            bttnAddBrushImages.Image = Resources.AddBrushIcon;
            bttnAddBrushImages.ImageAlign = ContentAlignment.MiddleLeft;
            bttnAddBrushImages.Location = new Point(3, 3);
            bttnAddBrushImages.Size = new Size(150, 32);
            bttnAddBrushImages.TabIndex = 11;
            bttnAddBrushImages.Text = Strings.AddBrushImages;
            bttnAddBrushImages.Click += BttnAddBrushImages_Click;
            bttnAddBrushImages.MouseEnter += BttnAddBrushImages_MouseEnter;
            #endregion

            #region brushImageLoadProgressBar
            brushImageLoadProgressBar.Location = new Point(0, 12);
            brushImageLoadProgressBar.Margin = new Padding(0, 3, 0, 3);
            brushImageLoadProgressBar.Size = new Size(153, 23);
            #endregion

            #region sliderColorInfluence
            sliderColorInfluence.IntegerOnly = true;
            sliderColorInfluence.Size = new Size(150, 25);
            sliderColorInfluence.ValueChanged += SliderColorInfluence_ValueChanged;
            sliderColorInfluence.MouseEnter += SliderColorInfluence_MouseEnter;
            sliderColorInfluence.ComputeText = (val) => string.Format("{0} {1}%", Strings.ColorInfluence, val);
            #endregion

            #region panelColorInfluenceHSV
            panelColorInfluenceHSV.AutoSizeMode = AutoSizeMode.GrowAndShrink;
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
            chkbxColorInfluenceHue.TabIndex = 16;
            chkbxColorInfluenceHue.Checked = true;
            chkbxColorInfluenceHue.MouseEnter += ChkbxColorInfluenceHue_MouseEnter;
            #endregion

            #region chkbxColorInfluenceSat
            chkbxColorInfluenceSat.AutoSize = true;
            chkbxColorInfluenceSat.TabIndex = 17;
            chkbxColorInfluenceSat.Checked = true;
            chkbxColorInfluenceSat.MouseEnter += ChkbxColorInfluenceSat_MouseEnter;
            #endregion

            #region chkbxColorInfluenceVal
            chkbxColorInfluenceVal.AutoSize = true;
            chkbxColorInfluenceVal.TabIndex = 18;
            chkbxColorInfluenceVal.MouseEnter += ChkbxColorInfluenceVal_MouseEnter;
            #endregion

            #region cmbxBlendMode
            cmbxBlendMode.Font = detailsFont;
            cmbxBlendMode.IntegralHeight = false;
            cmbxBlendMode.ItemHeight = 13;
            cmbxBlendMode.Location = new Point(3, 477);
            cmbxBlendMode.Margin = new Padding(0, 3, 0, 3);
            cmbxBlendMode.Size = new Size(153, 21);
            cmbxBlendMode.TabIndex = 19;
            cmbxBlendMode.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            cmbxBlendMode.FlatStyle = FlatStyle.Flat;
            cmbxBlendMode.DropDownHeight = 140;
            cmbxBlendMode.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbxBlendMode.DropDownWidth = 20;
            cmbxBlendMode.FormattingEnabled = true;
            cmbxBlendMode.SelectedIndexChanged += BttnBlendMode_SelectedIndexChanged;
            cmbxBlendMode.MouseEnter += BttnBlendMode_MouseEnter;
            cmbxBlendMode.MouseWheel += IgnoreMouseWheelEvent;
            #endregion

            #region sliderBrushOpacity
            sliderBrushOpacity.AutoSize = false;
            sliderBrushOpacity.IntegerOnly = true;
            sliderBrushOpacity.Location = new Point(3, 542);
            sliderBrushOpacity.Size = new Size(150, 25);
            sliderBrushOpacity.TabIndex = 20;
            sliderBrushOpacity.ValueChanged += SliderBrushOpacity_ValueChanged;
            sliderBrushOpacity.MouseEnter += SliderBrushOpacity_MouseEnter;
            sliderBrushOpacity.ComputeText = (val) => string.Format("{0} {1}", Strings.BrushOpacity, val);
            #endregion

            #region sliderBrushFlow
            sliderBrushFlow.AutoSize = false;
            sliderBrushFlow.IntegerOnly = true;
            sliderBrushFlow.Location = new Point(3, 590);
            sliderBrushFlow.Size = new Size(150, 25);
            sliderBrushFlow.TabIndex = 21;
            sliderBrushFlow.ValueChanged += SliderBrushFlow_ValueChanged;
            sliderBrushFlow.MouseEnter += SliderBrushFlow_MouseEnter;
            sliderBrushFlow.ComputeText = (val) =>
            {
                if ((BlendMode)cmbxBlendMode.SelectedIndex == BlendMode.Overwrite &&
                    activeTool == Tool.Brush && effectToDraw.Effect == null)
                {
                    return string.Format("{0} {1}", Strings.BrushFlowAlpha, val);
                }

                return string.Format("{0} {1}", Strings.BrushFlow, val);
            };

            #endregion

            #region sliderBrushRotation
            sliderBrushRotation.AutoSize = false;
            sliderBrushRotation.IntegerOnly = true;
            sliderBrushRotation.Location = new Point(3, 638);
            sliderBrushRotation.Size = new Size(150, 25);
            sliderBrushRotation.TabIndex = 22;
            sliderBrushRotation.ValueChanged += SliderBrushRotation_ValueChanged;
            sliderBrushRotation.MouseEnter += SliderBrushRotation_MouseEnter;
            sliderBrushRotation.ComputeText = (val) => string.Format("{0} {1}°", Strings.Rotation, val);
            #endregion

            #region sliderBrushSize
            sliderBrushSize.AutoSize = false;
            sliderBrushSize.IntegerOnly = true;
            sliderBrushSize.Location = new Point(3, 686);
            sliderBrushSize.Size = new Size(150, 25);
            sliderBrushSize.TabIndex = 23;
            sliderBrushSize.ValueChanged += SliderBrushSize_ValueChanged;
            sliderBrushSize.MouseEnter += SliderBrushSize_MouseEnter;
            sliderBrushSize.ComputeText = (val) => string.Format("{0} {1}", Strings.Size, val);
            #endregion

            #region bttnBrushControls
            bttnBrushControls.Location = new Point(0, 3);
            bttnBrushControls.Margin = new Padding(0, 3, 0, 3);
            bttnBrushControls.Size = new Size(155, 23);
            bttnBrushControls.TabIndex = 5;
            bttnBrushControls.TextAlign = ContentAlignment.MiddleLeft;
            bttnBrushControls.UpdateAccordion(Strings.AccordionBrush, false, new Control[] { panelBrush });
            #endregion

            #region bttnColorControls
            bttnColorControls.Location = new Point(0, 584);
            bttnColorControls.Margin = new Padding(0, 3, 0, 3);
            bttnColorControls.Size = new Size(155, 23);
            bttnColorControls.TabIndex = 18;
            bttnColorControls.TextAlign = ContentAlignment.MiddleLeft;
            bttnColorControls.UpdateAccordion(Strings.BrushColor, true, new Control[] { panelColorControls });
            #endregion

            #region panelColorControls
            panelColorControls.FlowDirection = FlowDirection.TopDown;
            panelColorControls.Location = new Point(0, 613);
            panelColorControls.Margin = new Padding(0, 3, 0, 3);
            panelColorControls.Size = new Size(156, 196);
            panelColorControls.TabIndex = 19;
            panelColorControls.AutoSize = true;
            panelColorControls.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelColorControls.Controls.Add(sliderColorR);
            panelColorControls.Controls.Add(sliderColorG);
            panelColorControls.Controls.Add(sliderColorB);
            panelColorControls.Controls.Add(sliderColorA);
            panelColorControls.Controls.Add(sliderColorH);
            panelColorControls.Controls.Add(sliderColorS);
            panelColorControls.Controls.Add(sliderColorV);
            #endregion

            #region sliderColorR
            sliderColorR.AutoSize = false;
            sliderColorR.Location = new Point(3, 52);
            sliderColorR.Size = new Size(150, 25);
            sliderColorR.TabIndex = 21;
            sliderColorR.ValueChanged += (_1, _2) => UpdateColorSelectionSliders(SliderSpecialType.RedGraph);
            sliderColorR.ComputeText = (val) => string.Format("{0}: {1}", Strings.ColorRedAbbr, val);
            sliderColorR.MouseEnter += SliderColorR_MouseEnter;
            #endregion

            #region sliderColorG
            sliderColorG.AutoSize = false;
            sliderColorG.Location = new Point(3, 52);
            sliderColorG.Size = new Size(150, 25);
            sliderColorG.TabIndex = 21;
            sliderColorG.ValueChanged += (_1, _2) => UpdateColorSelectionSliders(SliderSpecialType.GreenGraph);
            sliderColorG.ComputeText = (val) => string.Format("{0}: {1}", Strings.ColorGreenAbbr, val);
            sliderColorG.MouseEnter += SliderColorG_MouseEnter;
            #endregion

            #region sliderColorB
            sliderColorB.AutoSize = false;
            sliderColorB.Location = new Point(3, 52);
            sliderColorB.Size = new Size(150, 25);
            sliderColorB.TabIndex = 21;
            sliderColorB.ValueChanged += (_1, _2) => UpdateColorSelectionSliders(SliderSpecialType.BlueGraph);
            sliderColorB.ComputeText = (val) => string.Format("{0}: {1}", Strings.ColorBlueAbbr, val);
            sliderColorB.MouseEnter += SliderColorB_MouseEnter;
            #endregion

            #region sliderColorA
            sliderColorA.AutoSize = false;
            sliderColorA.Location = new Point(3, 52);
            sliderColorA.Size = new Size(150, 25);
            sliderColorA.TabIndex = 21;
            sliderColorA.ValueChanged += (_1, _2) => UpdateColorSelectionSliders(SliderSpecialType.AlphaGraph);
            sliderColorA.ComputeText = (val) => string.Format("{0}: {1}", Strings.Alpha, val);
            sliderColorA.MouseEnter += SliderColorA_MouseEnter;
            #endregion

            #region sliderColorH
            sliderColorH.AutoSize = false;
            sliderColorH.Location = new Point(3, 52);
            sliderColorH.Size = new Size(150, 25);
            sliderColorH.TabIndex = 21;
            sliderColorH.ValueChanged += (_1, _2) => UpdateColorSelectionSliders(SliderSpecialType.HueGraph);
            sliderColorH.ComputeText = (val) => string.Format("{0}: {1}", Strings.ColorHueAbbr, Math.Round(val));
            sliderColorH.MouseEnter += SliderColorH_MouseEnter;
            #endregion

            #region sliderColorS
            sliderColorS.AutoSize = false;
            sliderColorS.Location = new Point(3, 52);
            sliderColorS.Size = new Size(150, 25);
            sliderColorS.TabIndex = 21;
            sliderColorS.ValueChanged += (_1, _2) => UpdateColorSelectionSliders(SliderSpecialType.SatGraph);
            sliderColorS.ComputeText = (val) => string.Format("{0}: {1}", Strings.ColorSatAbbr, Math.Round(val));
            sliderColorS.MouseEnter += SliderColorS_MouseEnter;
            #endregion

            #region sliderColorV
            sliderColorV.AutoSize = false;
            sliderColorV.Location = new Point(3, 52);
            sliderColorV.Size = new Size(150, 25);
            sliderColorV.TabIndex = 21;
            sliderColorV.ValueChanged += (_1, _2) => UpdateColorSelectionSliders(SliderSpecialType.ValGraph);
            sliderColorV.ComputeText = (val) => string.Format("{0}: {1}", Strings.ColorValAbbr, Math.Round(val));
            sliderColorV.MouseEnter += SliderColorV_MouseEnter;
            #endregion

            #region bttnSpecialSettings
            bttnSpecialSettings.Location = new Point(0, 584);
            bttnSpecialSettings.Margin = new Padding(0, 3, 0, 3);
            bttnSpecialSettings.Size = new Size(155, 23);
            bttnSpecialSettings.TabIndex = 18;
            bttnSpecialSettings.TextAlign = ContentAlignment.MiddleLeft;
            bttnSpecialSettings.UpdateAccordion(Strings.AccordionSpecialSettings, true, new Control[] { panelSpecialSettings });
            #endregion

            #region panelSpecialSettings
            panelSpecialSettings.FlowDirection = FlowDirection.TopDown;
            panelSpecialSettings.Location = new Point(0, 613);
            panelSpecialSettings.Margin = new Padding(0, 3, 0, 3);
            panelSpecialSettings.Size = new Size(156, 196);
            panelSpecialSettings.TabIndex = 19;
            panelSpecialSettings.AutoSize = true;
            panelSpecialSettings.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelSpecialSettings.Controls.Add(panelChosenEffect);
            panelSpecialSettings.Controls.Add(sliderMinDrawDistance);
            panelSpecialSettings.Controls.Add(chkbxAutomaticBrushDensity);
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
            cmbxChosenEffect.FlatStyle = FlatStyle.Flat;
            cmbxChosenEffect.DrawMode = DrawMode.OwnerDrawFixed;
            cmbxChosenEffect.DropDownHeight = 140;
            cmbxChosenEffect.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbxChosenEffect.DropDownWidth = 150;
            cmbxChosenEffect.FormattingEnabled = true;
            cmbxChosenEffect.Font = detailsFont;
            cmbxChosenEffect.IntegralHeight = false;
            cmbxChosenEffect.ItemHeight = 24;
            cmbxChosenEffect.Location = new Point(3, 0);
            cmbxChosenEffect.Margin = new Padding(0, 3, 0, 3);
            cmbxChosenEffect.Size = new Size(121, 21);
            cmbxChosenEffect.TabIndex = 0;
            cmbxChosenEffect.DrawItem += new DrawItemEventHandler(CmbxChosenEffect_DrawItem);
            cmbxChosenEffect.MouseEnter += CmbxChosenEffect_MouseEnter;
            cmbxChosenEffect.MouseLeave += CmbxChosenEffect_MouseLeave;
            cmbxChosenEffect.SelectedIndexChanged += CmbxChosenEffect_SelectedIndexChanged;
            cmbxChosenEffect.MouseWheel += IgnoreMouseWheelEvent;
            #endregion

            #region sliderMinDrawDistance
            sliderMinDrawDistance.AutoSize = false;
            sliderMinDrawDistance.IntegerOnly = true;
            sliderMinDrawDistance.Location = new Point(3, 52);
            sliderMinDrawDistance.Size = new Size(150, 25);
            sliderMinDrawDistance.TabIndex = 21;
            sliderMinDrawDistance.ComputeText = (val) => string.Format("{0} {1}", Strings.MinDrawDistance, val);
            sliderMinDrawDistance.MouseEnter += SliderMinDrawDistance_MouseEnter;
            #endregion

            #region chkbxAutomaticBrushDensity
            chkbxAutomaticBrushDensity.AutoSize = false;
            chkbxAutomaticBrushDensity.Size = new Size(150, 24);
            chkbxAutomaticBrushDensity.Checked = true;
            chkbxAutomaticBrushDensity.MouseEnter += AutomaticBrushDensity_MouseEnter;
            chkbxAutomaticBrushDensity.CheckedChanged += AutomaticBrushDensity_CheckedChanged;
            chkbxAutomaticBrushDensity.TabIndex = 20;
            #endregion

            #region sliderBrushDensity
            sliderBrushDensity.AutoSize = false;
            sliderBrushDensity.Enabled = false;
            sliderBrushDensity.IntegerOnly = true;
            sliderBrushDensity.Location = new Point(3, 68);
            sliderBrushDensity.Size = new Size(150, 25);
            sliderBrushDensity.TabIndex = 22;
            sliderBrushDensity.ComputeText = (val) => string.Format("{0} {1}", Strings.BrushDensity, val);
            sliderBrushDensity.MouseEnter += SliderBrushDensity_MouseEnter;
            #endregion

            #region cmbxSymmetry
            cmbxSymmetry.FlatStyle = FlatStyle.Flat;
            cmbxSymmetry.DropDownHeight = 140;
            cmbxSymmetry.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbxSymmetry.DropDownWidth = 20;
            cmbxSymmetry.FormattingEnabled = true;
            cmbxSymmetry.Font = detailsFont;
            cmbxSymmetry.IntegralHeight = false;
            cmbxSymmetry.ItemHeight = 13;
            cmbxSymmetry.Location = new Point(3, 99);
            cmbxSymmetry.Size = new Size(150, 21);
            cmbxSymmetry.TabIndex = 24;
            cmbxSymmetry.MouseEnter += BttnSymmetry_MouseEnter;
            cmbxSymmetry.MouseWheel += IgnoreMouseWheelEvent;
            #endregion

            #region cmbxBrushSmoothing
            cmbxBrushSmoothing.FlatStyle = FlatStyle.Flat;
            cmbxBrushSmoothing.DropDownHeight = 140;
            cmbxBrushSmoothing.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbxBrushSmoothing.DropDownWidth = 20;
            cmbxBrushSmoothing.FormattingEnabled = true;
            cmbxBrushSmoothing.Font = detailsFont;
            cmbxBrushSmoothing.IntegralHeight = false;
            cmbxBrushSmoothing.ItemHeight = 13;
            cmbxBrushSmoothing.Location = new Point(3, 126);
            cmbxBrushSmoothing.Size = new Size(150, 21);
            cmbxBrushSmoothing.TabIndex = 23;
            cmbxBrushSmoothing.MouseEnter += BttnBrushSmoothing_MouseEnter;
            cmbxBrushSmoothing.MouseWheel += IgnoreMouseWheelEvent;
            #endregion

            #region chkbxSeamlessDrawing
            chkbxSeamlessDrawing.AutoSize = true;
            chkbxSeamlessDrawing.Location = new Point(0, 153);
            chkbxSeamlessDrawing.TabIndex = 25;
            chkbxSeamlessDrawing.MouseEnter += ChkbxSeamlessDrawing_MouseEnter;
            #endregion

            #region chkbxOrientToMouse
            chkbxOrientToMouse.AutoSize = true;
            chkbxOrientToMouse.Location = new Point(0, 176);
            chkbxOrientToMouse.TabIndex = 26;
            chkbxOrientToMouse.MouseEnter += ChkbxOrientToMouse_MouseEnter;
            #endregion

            #region chkbxDitherDraw
            chkbxDitherDraw.AutoSize = true;
            chkbxDitherDraw.Location = new Point(0, 199);
            chkbxDitherDraw.TabIndex = 27;
            chkbxDitherDraw.MouseEnter += ChkbxDitherDraw_MouseEnter;
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
            chkbxLockAlpha.ToggleImage = new(Resources.sprLocked, Resources.sprUnlocked);
            chkbxLockAlpha.ImageAlign = ContentAlignment.MiddleLeft;
            chkbxLockAlpha.AutoSize = true;
            chkbxLockAlpha.Location = new Point(3, 222);
            chkbxLockAlpha.TabIndex = 28;
            chkbxLockAlpha.MouseEnter += ChkbxLockAlpha_MouseEnter;
            #endregion

            #region chkbxLockR
            chkbxLockR.ToggleImage = new(Resources.sprLocked, Resources.sprUnlocked);
            chkbxLockR.ImageAlign = ContentAlignment.MiddleLeft;
            chkbxLockR.AutoSize = true;
            chkbxLockR.Location = new Point(3, 0);
            chkbxLockR.Size = new Size(80, 17);
            chkbxLockR.TabIndex = 1;
            chkbxLockR.MouseEnter += ChkbxLockR_MouseEnter;
            #endregion

            #region chkbxLockG
            chkbxLockG.ToggleImage = new(Resources.sprLocked, Resources.sprUnlocked);
            chkbxLockG.ImageAlign = ContentAlignment.MiddleLeft;
            chkbxLockG.AutoSize = true;
            chkbxLockG.Location = new Point(44, 0);
            chkbxLockG.Size = new Size(80, 17);
            chkbxLockG.TabIndex = 2;
            chkbxLockG.MouseEnter += ChkbxLockG_MouseEnter;
            #endregion

            #region chkbxLockB
            chkbxLockB.ToggleImage = new(Resources.sprLocked, Resources.sprUnlocked);
            chkbxLockB.ImageAlign = ContentAlignment.MiddleLeft;
            chkbxLockB.AutoSize = true;
            chkbxLockB.Location = new Point(82, 0);
            chkbxLockB.Size = new Size(80, 17);
            chkbxLockB.TabIndex = 3;
            chkbxLockB.MouseEnter += ChkbxLockB_MouseEnter;
            #endregion

            #region chkbxLockHue
            chkbxLockHue.ToggleImage = new(Resources.sprLocked, Resources.sprUnlocked);
            chkbxLockHue.ImageAlign = ContentAlignment.MiddleLeft;
            chkbxLockHue.AutoSize = true;
            chkbxLockHue.Location = new Point(3, 0);
            chkbxLockHue.Size = new Size(80, 17);
            chkbxLockHue.TabIndex = 1;
            chkbxLockHue.MouseEnter += ChkbxLockHue_MouseEnter;
            #endregion

            #region chkbxLockSat
            chkbxLockSat.ToggleImage = new(Resources.sprLocked, Resources.sprUnlocked);
            chkbxLockSat.ImageAlign = ContentAlignment.MiddleLeft;
            chkbxLockSat.AutoSize = true;
            chkbxLockSat.Location = new Point(44, 0);
            chkbxLockSat.Size = new Size(80, 17);
            chkbxLockSat.TabIndex = 2;
            chkbxLockSat.MouseEnter += ChkbxLockSat_MouseEnter;
            #endregion

            #region chkbxLockVal
            chkbxLockVal.ToggleImage = new(Resources.sprLocked, Resources.sprUnlocked);
            chkbxLockVal.ImageAlign = ContentAlignment.MiddleLeft;
            chkbxLockVal.AutoSize = true;
            chkbxLockVal.Location = new Point(82, 0);
            chkbxLockVal.Size = new Size(80, 17);
            chkbxLockVal.TabIndex = 3;
            chkbxLockVal.MouseEnter += ChkbxLockVal_MouseEnter;
            #endregion

            #region bttnJitterBasicsControls
            bttnJitterBasicsControls.Location = new Point(0, 815);
            bttnJitterBasicsControls.Margin = new Padding(0, 3, 0, 3);
            bttnJitterBasicsControls.Size = new Size(155, 23);
            bttnJitterBasicsControls.TabIndex = 27;
            bttnJitterBasicsControls.TextAlign = ContentAlignment.MiddleLeft;
            bttnJitterBasicsControls.UpdateAccordion(Strings.AccordionJitterBasics, true, new Control[] { panelJitterBasics });
            #endregion

            #region panelJitterBasics
            panelJitterBasics.AutoSize = true;
            panelJitterBasics.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelJitterBasics.FlowDirection = FlowDirection.TopDown;
            panelJitterBasics.Location = new Point(0, 844);
            panelJitterBasics.Margin = new Padding(0, 3, 0, 3);
            panelJitterBasics.Size = new Size(156, 336);
            panelJitterBasics.TabIndex = 28;
            panelJitterBasics.Controls.Add(sliderRandMinSize);
            panelJitterBasics.Controls.Add(sliderRandMaxSize);
            panelJitterBasics.Controls.Add(sliderRandRotLeft);
            panelJitterBasics.Controls.Add(sliderRandRotRight);
            panelJitterBasics.Controls.Add(sliderRandFlowLoss);
            panelJitterBasics.Controls.Add(sliderRandHorzShift);
            panelJitterBasics.Controls.Add(sliderRandVertShift);
            #endregion

            #region sliderRandMinSize
            sliderRandMinSize.AutoSize = false;
            sliderRandMinSize.IntegerOnly = true;
            sliderRandMinSize.Location = new Point(3, 20);
            sliderRandMinSize.Size = new Size(150, 25);
            sliderRandMinSize.TabIndex = 29;
            sliderRandMinSize.ComputeText = (val) => string.Format("{0} {1}", Strings.RandMinSize, val);
            sliderRandMinSize.MouseEnter += SliderRandMinSize_MouseEnter;
            #endregion

            #region sliderRandMaxSize
            sliderRandMaxSize.AutoSize = false;
            sliderRandMaxSize.IntegerOnly = true;
            sliderRandMaxSize.Location = new Point(3, 68);
            sliderRandMaxSize.Size = new Size(150, 25);
            sliderRandMaxSize.TabIndex = 30;
            sliderRandMaxSize.ComputeText = (val) => string.Format("{0} {1}", Strings.RandMaxSize, val);
            sliderRandMaxSize.MouseEnter += SliderRandMaxSize_MouseEnter;
            #endregion

            #region sliderRandRotLeft
            sliderRandRotLeft.AutoSize = false;
            sliderRandRotLeft.IntegerOnly = true;
            sliderRandRotLeft.Location = new Point(3, 116);
            sliderRandRotLeft.Size = new Size(150, 25);
            sliderRandRotLeft.TabIndex = 31;
            sliderRandRotLeft.ComputeText = (val) => string.Format("{0} {1}°", Strings.RandRotLeft, val);
            sliderRandRotLeft.MouseEnter += SliderRandRotLeft_MouseEnter;
            #endregion

            #region sliderRandRotRight
            sliderRandRotRight.AutoSize = false;
            sliderRandRotRight.IntegerOnly = true;
            sliderRandRotRight.Location = new Point(3, 164);
            sliderRandRotRight.Size = new Size(150, 25);
            sliderRandRotRight.TabIndex = 32;
            sliderRandRotRight.ComputeText = (val) => string.Format("{0} {1}°", Strings.RandRotRight, val);
            sliderRandRotRight.MouseEnter += SliderRandRotRight_MouseEnter;
            #endregion

            #region sliderRandFlowLoss
            sliderRandFlowLoss.AutoSize = false;
            sliderRandFlowLoss.IntegerOnly = true;
            sliderRandFlowLoss.Location = new Point(3, 212);
            sliderRandFlowLoss.Size = new Size(150, 25);
            sliderRandFlowLoss.TabIndex = 33;
            sliderRandFlowLoss.ComputeText = (val) => string.Format("{0} {1}", Strings.RandFlowLoss, val);
            sliderRandFlowLoss.MouseEnter += SliderRandFlowLoss_MouseEnter;
            #endregion

            #region sliderRandHorzShift
            sliderRandHorzShift.AutoSize = false;
            sliderRandHorzShift.IntegerOnly = true;
            sliderRandHorzShift.Location = new Point(3, 260);
            sliderRandHorzShift.Size = new Size(150, 25);
            sliderRandHorzShift.TabIndex = 34;
            sliderRandHorzShift.ComputeText = (val) => string.Format("{0} {1}%", Strings.RandHorzShift, val);
            sliderRandHorzShift.MouseEnter += SliderRandHorzShift_MouseEnter;
            #endregion

            #region sliderRandVertShift
            sliderRandVertShift.AutoSize = false;
            sliderRandVertShift.IntegerOnly = true;
            sliderRandVertShift.Location = new Point(3, 308);
            sliderRandVertShift.Size = new Size(150, 25);
            sliderRandVertShift.TabIndex = 35;
            sliderRandVertShift.ComputeText = (val) => string.Format("{0} {1}%", Strings.RandVertShift, val);
            sliderRandVertShift.MouseEnter += SliderRandVertShift_MouseEnter;
            #endregion

            #region bttnJitterColorControls
            bttnJitterColorControls.Location = new Point(0, 1186);
            bttnJitterColorControls.Margin = new Padding(0, 3, 0, 3);
            bttnJitterColorControls.Size = new Size(155, 23);
            bttnJitterColorControls.TabIndex = 36;
            bttnJitterColorControls.TextAlign = ContentAlignment.MiddleLeft;
            bttnJitterColorControls.UpdateAccordion(Strings.AccordionJitterColor, true, new Control[] { panelJitterColor });
            #endregion

            #region panelJitterColor
            panelJitterColor.AutoSize = true;
            panelJitterColor.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelJitterColor.FlowDirection = FlowDirection.TopDown;
            panelJitterColor.Location = new Point(0, 1215);
            panelJitterColor.Margin = new Padding(0, 3, 0, 3);
            panelJitterColor.Size = new Size(156, 474);
            panelJitterColor.TabIndex = 37;
            panelJitterColor.Controls.Add(sliderJitterMinRed);
            panelJitterColor.Controls.Add(sliderJitterMaxRed);
            panelJitterColor.Controls.Add(sliderJitterMinGreen);
            panelJitterColor.Controls.Add(sliderJitterMaxGreen);
            panelJitterColor.Controls.Add(sliderJitterMinBlue);
            panelJitterColor.Controls.Add(sliderJitterMaxBlue);
            panelJitterColor.Controls.Add(sliderJitterMinHue);
            panelJitterColor.Controls.Add(sliderJitterMaxHue);
            panelJitterColor.Controls.Add(sliderJitterMinSat);
            panelJitterColor.Controls.Add(sliderJitterMaxSat);
            panelJitterColor.Controls.Add(sliderJitterMinVal);
            panelJitterColor.Controls.Add(sliderJitterMaxVal);
            #endregion

            #region sliderJitterMinRed
            sliderJitterMinRed.AutoSize = false;
            sliderJitterMinRed.IntegerOnly = true;
            sliderJitterMinRed.Location = new Point(3, 20);
            sliderJitterMinRed.Size = new Size(150, 25);
            sliderJitterMinRed.TabIndex = 38;
            sliderJitterMinRed.ComputeText = (val) => string.Format("{0} -{1}%", Strings.JitterRed, val);
            sliderJitterMinRed.MouseEnter += SliderJitterMinRed_MouseEnter;
            #endregion

            #region sliderJitterMaxRed
            sliderJitterMaxRed.AutoSize = false;
            sliderJitterMaxRed.IntegerOnly = true;
            sliderJitterMaxRed.Location = new Point(3, 51);
            sliderJitterMaxRed.Size = new Size(150, 25);
            sliderJitterMaxRed.TabIndex = 39;
            sliderJitterMaxRed.ComputeText = (val) => string.Format("{0} +{1}%", Strings.JitterRed, val);
            sliderJitterMaxRed.MouseEnter += SliderJitterMaxRed_MouseEnter;
            #endregion

            #region sliderJitterMinGreen
            sliderJitterMinGreen.AutoSize = false;
            sliderJitterMinGreen.IntegerOnly = true;
            sliderJitterMinGreen.Location = new Point(3, 99);
            sliderJitterMinGreen.Size = new Size(150, 25);
            sliderJitterMinGreen.TabIndex = 40;
            sliderJitterMinGreen.ComputeText = (val) => string.Format("{0} -{1}%", Strings.JitterGreen, val);
            sliderJitterMinGreen.MouseEnter += SliderJitterMinGreen_MouseEnter;
            #endregion

            #region sliderJitterMaxGreen
            sliderJitterMaxGreen.AutoSize = false;
            sliderJitterMaxGreen.IntegerOnly = true;
            sliderJitterMaxGreen.Location = new Point(3, 130);
            sliderJitterMaxGreen.Size = new Size(150, 25);
            sliderJitterMaxGreen.TabIndex = 41;
            sliderJitterMaxGreen.ComputeText = (val) => string.Format("{0} +{1}%", Strings.JitterGreen, val);
            sliderJitterMaxGreen.MouseEnter += SliderJitterMaxGreen_MouseEnter;
            #endregion

            #region sliderJitterMinBlue
            sliderJitterMinBlue.AutoSize = false;
            sliderJitterMinBlue.IntegerOnly = true;
            sliderJitterMinBlue.Location = new Point(3, 178);
            sliderJitterMinBlue.Size = new Size(150, 25);
            sliderJitterMinBlue.TabIndex = 42;
            sliderJitterMinBlue.ComputeText = (val) => string.Format("{0} -{1}%", Strings.JitterBlue, val);
            sliderJitterMinBlue.MouseEnter += SliderJitterMinBlue_MouseEnter;
            #endregion

            #region sliderJitterMaxBlue
            sliderJitterMaxBlue.AutoSize = false;
            sliderJitterMaxBlue.IntegerOnly = true;
            sliderJitterMaxBlue.Location = new Point(3, 209);
            sliderJitterMaxBlue.Size = new Size(150, 25);
            sliderJitterMaxBlue.TabIndex = 43;
            sliderJitterMaxBlue.ComputeText = (val) => string.Format("{0} +{1}%", Strings.JitterBlue, val);
            sliderJitterMaxBlue.MouseEnter += SliderJitterMaxBlue_MouseEnter;
            #endregion

            #region sliderJitterMinHue
            sliderJitterMinHue.AutoSize = false;
            sliderJitterMinHue.IntegerOnly = true;
            sliderJitterMinHue.Location = new Point(3, 257);
            sliderJitterMinHue.Size = new Size(150, 25);
            sliderJitterMinHue.TabIndex = 44;
            sliderJitterMinHue.ComputeText = (val) => string.Format("{0} -{1}%", Strings.JitterHue, val);
            sliderJitterMinHue.MouseEnter += SliderJitterMinHue_MouseEnter;
            #endregion

            #region sliderJitterMaxHue
            sliderJitterMaxHue.AutoSize = false;
            sliderJitterMaxHue.IntegerOnly = true;
            sliderJitterMaxHue.Location = new Point(3, 288);
            sliderJitterMaxHue.Size = new Size(150, 25);
            sliderJitterMaxHue.TabIndex = 45;
            sliderJitterMaxHue.ComputeText = (val) => string.Format("{0} +{1}%", Strings.JitterHue, val);
            sliderJitterMaxHue.MouseEnter += SliderJitterMaxHue_MouseEnter;
            #endregion

            #region sliderJitterMinSat
            sliderJitterMinSat.AutoSize = false;
            sliderJitterMinSat.IntegerOnly = true;
            sliderJitterMinSat.Location = new Point(3, 336);
            sliderJitterMinSat.Size = new Size(150, 25);
            sliderJitterMinSat.TabIndex = 46;
            sliderJitterMinSat.ComputeText = (val) => string.Format("{0} -{1}%", Strings.JitterSaturation, val);
            sliderJitterMinSat.MouseEnter += SliderJitterMinSat_MouseEnter;
            #endregion

            #region sliderJitterMaxSat
            sliderJitterMaxSat.AutoSize = false;
            sliderJitterMaxSat.IntegerOnly = true;
            sliderJitterMaxSat.Location = new Point(3, 367);
            sliderJitterMaxSat.Size = new Size(150, 25);
            sliderJitterMaxSat.TabIndex = 47;
            sliderJitterMaxSat.ComputeText = (val) => string.Format("{0} +{1}%", Strings.JitterSaturation, val);
            sliderJitterMaxSat.MouseEnter += SliderJitterMaxSat_MouseEnter;
            #endregion

            #region sliderJitterMinVal
            sliderJitterMinVal.AutoSize = false;
            sliderJitterMinVal.IntegerOnly = true;
            sliderJitterMinVal.Location = new Point(3, 415);
            sliderJitterMinVal.Size = new Size(150, 25);
            sliderJitterMinVal.TabIndex = 48;
            sliderJitterMinVal.ComputeText = (val) => string.Format("{0} -{1}%", Strings.JitterValue, val);
            sliderJitterMinVal.MouseEnter += SliderJitterMinVal_MouseEnter;
            #endregion

            #region sliderJitterMaxVal
            sliderJitterMaxVal.AutoSize = false;
            sliderJitterMaxVal.IntegerOnly = true;
            sliderJitterMaxVal.Location = new Point(3, 446);
            sliderJitterMaxVal.Size = new Size(150, 25);
            sliderJitterMaxVal.TabIndex = 49;
            sliderJitterMaxVal.ComputeText = (val) => string.Format("{0} +{1}%", Strings.JitterValue, val);
            sliderJitterMaxVal.MouseEnter += SliderJitterMaxVal_MouseEnter;
            #endregion

            #region bttnShiftBasicsControls
            bttnShiftBasicsControls.Location = new Point(0, 1695);
            bttnShiftBasicsControls.Margin = new Padding(0, 3, 0, 3);
            bttnShiftBasicsControls.Size = new Size(155, 23);
            bttnShiftBasicsControls.TabIndex = 50;
            bttnShiftBasicsControls.TextAlign = ContentAlignment.MiddleLeft;
            bttnShiftBasicsControls.UpdateAccordion(Strings.AccordionShiftBasics, true, new Control[] { panelShiftBasics });
            #endregion

            #region panelShiftBasics
            panelShiftBasics.AutoSize = true;
            panelShiftBasics.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelShiftBasics.FlowDirection = FlowDirection.TopDown;
            panelShiftBasics.Location = new Point(0, 1724);
            panelShiftBasics.Margin = new Padding(0, 3, 0, 3);
            panelShiftBasics.Size = new Size(156, 144);
            panelShiftBasics.TabIndex = 51;
            panelShiftBasics.Controls.Add(sliderShiftSize);
            panelShiftBasics.Controls.Add(sliderShiftRotation);
            panelShiftBasics.Controls.Add(sliderShiftFlow);
            #endregion

            #region sliderShiftSize
            sliderShiftSize.AutoSize = false;
            sliderShiftSize.IntegerOnly = true;
            sliderShiftSize.Location = new Point(3, 20);
            sliderShiftSize.Size = new Size(150, 25);
            sliderShiftSize.TabIndex = 52;
            sliderShiftSize.ComputeText = (val) => string.Format("{0} {1}", Strings.ShiftSize, val);
            sliderShiftSize.MouseEnter += SliderShiftSize_MouseEnter;
            #endregion

            #region sliderShiftRotation
            sliderShiftRotation.AutoSize = false;
            sliderShiftRotation.IntegerOnly = true;
            sliderShiftRotation.Location = new Point(3, 68);
            sliderShiftRotation.Size = new Size(150, 25);
            sliderShiftRotation.TabIndex = 53;
            sliderShiftRotation.ComputeText = (val) => string.Format("{0} {1}°", Strings.ShiftRotation, val);
            sliderShiftRotation.MouseEnter += SliderShiftRotation_MouseEnter;
            #endregion

            #region sliderShiftFlow
            sliderShiftFlow.AutoSize = false;
            sliderShiftFlow.IntegerOnly = true;
            sliderShiftFlow.Location = new Point(3, 116);
            sliderShiftFlow.Size = new Size(150, 25);
            sliderShiftFlow.TabIndex = 54;
            sliderShiftFlow.ComputeText = (val) => string.Format("{0} {1}", Strings.ShiftFlow, val);
            sliderShiftFlow.MouseEnter += SliderShiftFlow_MouseEnter;
            #endregion

            #region bttnTabAssignPressureControls
            bttnTabAssignPressureControls.Location = new Point(0, 1874);
            bttnTabAssignPressureControls.Margin = new Padding(0, 3, 0, 3);
            bttnTabAssignPressureControls.Size = new Size(155, 23);
            bttnTabAssignPressureControls.TabIndex = 55;
            bttnTabAssignPressureControls.TextAlign = ContentAlignment.MiddleLeft;
            bttnTabAssignPressureControls.UpdateAccordion(Strings.AccordionTabPressureControls, true, new Control[] { panelTabletAssignPressure });
            #endregion

            #region panelTabletAssignPressure
            panelTabletAssignPressure.AutoSize = true;
            panelTabletAssignPressure.AutoSizeMode = AutoSizeMode.GrowAndShrink;
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
            txtTabPressureBrushOpacity.Location = Point.Empty;
            txtTabPressureBrushOpacity.Margin = new Padding(3, 3, 3, 3);
            txtTabPressureBrushOpacity.Size = new Size(105, 13);
            txtTabPressureBrushOpacity.TabIndex = 0;
            txtTabPressureBrushOpacity.Text = Strings.BrushOpacity;
            #endregion

            #region spinTabPressureBrushOpacity
            spinTabPressureBrushOpacity.BorderStyle = BorderStyle.FixedSingle;
            spinTabPressureBrushOpacity.Dock = DockStyle.Right;
            spinTabPressureBrushOpacity.Location = new Point(105, 0);
            spinTabPressureBrushOpacity.Margin = new Padding(3, 3, 0, 3);
            spinTabPressureBrushOpacity.Size = new Size(51, 20);
            spinTabPressureBrushOpacity.TabIndex = 58;
            spinTabPressureBrushOpacity.Maximum = int.MaxValue;
            spinTabPressureBrushOpacity.Minimum = int.MinValue;
            spinTabPressureBrushOpacity.MouseWheel += IgnoreMouseWheelEvent;
            #endregion

            #region cmbxTabPressureBrushOpacity
            cmbxTabPressureBrushOpacity.Font = detailsFont;
            cmbxTabPressureBrushOpacity.Location = new Point(0, 25);
            cmbxTabPressureBrushOpacity.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureBrushOpacity.Size = new Size(156, 21);
            cmbxTabPressureBrushOpacity.TabIndex = 59;
            cmbxTabPressureBrushOpacity.MouseHover += CmbxTabPressure_MouseHover;
            cmbxTabPressureBrushOpacity.MouseWheel += IgnoreMouseWheelEvent;
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
            spinTabPressureBrushFlow.Maximum = int.MaxValue;
            spinTabPressureBrushFlow.Minimum = int.MinValue;
            spinTabPressureBrushFlow.MouseWheel += IgnoreMouseWheelEvent;
            #endregion

            #region cmbxTabPressureBrushFlow
            cmbxTabPressureBrushFlow.Font = detailsFont;
            cmbxTabPressureBrushFlow.Location = new Point(0, 25);
            cmbxTabPressureBrushFlow.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureBrushFlow.Size = new Size(156, 21);
            cmbxTabPressureBrushFlow.TabIndex = 59;
            cmbxTabPressureBrushFlow.MouseHover += CmbxTabPressure_MouseHover;
            cmbxTabPressureBrushFlow.MouseWheel += IgnoreMouseWheelEvent;
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
            spinTabPressureBrushSize.Maximum = int.MaxValue;
            spinTabPressureBrushSize.Minimum = int.MinValue;
            spinTabPressureBrushSize.LostFocus += SpinTabPressureBrushSize_LostFocus;
            spinTabPressureBrushSize.MouseWheel += IgnoreMouseWheelEvent;
            #endregion

            #region cmbxTabPressureBrushSize
            cmbxTabPressureBrushSize.Font = detailsFont;
            cmbxTabPressureBrushSize.Location = new Point(0, 25);
            cmbxTabPressureBrushSize.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureBrushSize.Size = new Size(156, 21);
            cmbxTabPressureBrushSize.TabIndex = 63;
            cmbxTabPressureBrushSize.MouseHover += CmbxTabPressure_MouseHover;
            cmbxTabPressureBrushSize.SelectedIndexChanged += CmbxTabPressureBrushSize_SelectedIndexChanged;
            cmbxTabPressureBrushSize.MouseWheel += IgnoreMouseWheelEvent;
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
            spinTabPressureBrushRotation.Maximum = int.MaxValue;
            spinTabPressureBrushRotation.Minimum = int.MinValue;
            spinTabPressureBrushRotation.MouseWheel += IgnoreMouseWheelEvent;
            #endregion

            #region cmbxTabPressureBrushRotation
            cmbxTabPressureBrushRotation.Font = detailsFont;
            cmbxTabPressureBrushRotation.Location = new Point(0, 25);
            cmbxTabPressureBrushRotation.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureBrushRotation.Size = new Size(156, 21);
            cmbxTabPressureBrushRotation.TabIndex = 68;
            cmbxTabPressureBrushRotation.MouseHover += CmbxTabPressure_MouseHover;
            cmbxTabPressureBrushRotation.MouseWheel += IgnoreMouseWheelEvent;
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
            spinTabPressureMinDrawDistance.Minimum = int.MinValue;
            spinTabPressureMinDrawDistance.MouseWheel += IgnoreMouseWheelEvent;
            #endregion

            #region cmbxTabPressureMinDrawDistance
            cmbxTabPressureMinDrawDistance.Font = detailsFont;
            cmbxTabPressureMinDrawDistance.Location = new Point(0, 25);
            cmbxTabPressureMinDrawDistance.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureMinDrawDistance.Size = new Size(156, 21);
            cmbxTabPressureMinDrawDistance.TabIndex = 72;
            cmbxTabPressureMinDrawDistance.MouseHover += CmbxTabPressure_MouseHover;
            cmbxTabPressureMinDrawDistance.MouseWheel += IgnoreMouseWheelEvent;
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
            spinTabPressureBrushDensity.Maximum = int.MaxValue;
            spinTabPressureBrushDensity.Minimum = int.MinValue;
            spinTabPressureBrushDensity.MouseWheel += IgnoreMouseWheelEvent;
            #endregion

            #region cmbxTabPressureBrushDensity
            cmbxTabPressureBrushDensity.Font = detailsFont;
            cmbxTabPressureBrushDensity.Location = new Point(0, 25);
            cmbxTabPressureBrushDensity.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureBrushDensity.Size = new Size(156, 21);
            cmbxTabPressureBrushDensity.TabIndex = 76;
            cmbxTabPressureBrushDensity.MouseHover += CmbxTabPressure_MouseHover;
            cmbxTabPressureBrushDensity.MouseWheel += IgnoreMouseWheelEvent;
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
            spinTabPressureRandMinSize.Maximum = int.MaxValue;
            spinTabPressureRandMinSize.Minimum = int.MinValue;
            spinTabPressureRandMinSize.MouseWheel += IgnoreMouseWheelEvent;
            #endregion

            #region cmbxTabPressureRandMinSize
            cmbxTabPressureRandMinSize.Font = detailsFont;
            cmbxTabPressureRandMinSize.Location = new Point(0, 25);
            cmbxTabPressureRandMinSize.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureRandMinSize.Size = new Size(156, 21);
            cmbxTabPressureRandMinSize.TabIndex = 80;
            cmbxTabPressureRandMinSize.MouseHover += CmbxTabPressure_MouseHover;
            cmbxTabPressureRandMinSize.MouseWheel += IgnoreMouseWheelEvent;
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
            spinTabPressureRandMaxSize.Maximum = int.MaxValue;
            spinTabPressureRandMaxSize.Minimum = int.MinValue;
            spinTabPressureRandMaxSize.MouseWheel += IgnoreMouseWheelEvent;
            #endregion

            #region cmbxTabPressureRandMaxSize
            cmbxTabPressureRandMaxSize.Font = detailsFont;
            cmbxTabPressureRandMaxSize.Location = new Point(0, 25);
            cmbxTabPressureRandMaxSize.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureRandMaxSize.Size = new Size(156, 21);
            cmbxTabPressureRandMaxSize.TabIndex = 84;
            cmbxTabPressureRandMaxSize.MouseHover += CmbxTabPressure_MouseHover;
            cmbxTabPressureRandMaxSize.MouseWheel += IgnoreMouseWheelEvent;
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
            spinTabPressureRandRotLeft.Maximum = int.MaxValue;
            spinTabPressureRandRotLeft.Minimum = int.MinValue;
            spinTabPressureRandRotLeft.MouseWheel += IgnoreMouseWheelEvent;
            #endregion

            #region cmbxTabPressureRandRotLeft
            cmbxTabPressureRandRotLeft.Font = detailsFont;
            cmbxTabPressureRandRotLeft.Location = new Point(0, 25);
            cmbxTabPressureRandRotLeft.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureRandRotLeft.Size = new Size(156, 21);
            cmbxTabPressureRandRotLeft.TabIndex = 87;
            cmbxTabPressureRandRotLeft.MouseHover += CmbxTabPressure_MouseHover;
            cmbxTabPressureRandRotLeft.MouseWheel += IgnoreMouseWheelEvent;
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
            spinTabPressureRandRotRight.Maximum = int.MaxValue;
            spinTabPressureRandRotRight.Minimum = int.MinValue;
            spinTabPressureRandRotRight.MouseWheel += IgnoreMouseWheelEvent;
            #endregion

            #region cmbxTabPressureRandRotRight
            cmbxTabPressureRandRotRight.Font = detailsFont;
            cmbxTabPressureRandRotRight.Location = new Point(0, 25);
            cmbxTabPressureRandRotRight.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureRandRotRight.Size = new Size(156, 21);
            cmbxTabPressureRandRotRight.TabIndex = 91;
            cmbxTabPressureRandRotRight.MouseHover += CmbxTabPressure_MouseHover;
            cmbxTabPressureRandRotRight.MouseWheel += IgnoreMouseWheelEvent;
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
            spinTabPressureRandFlowLoss.Minimum = int.MinValue;
            spinTabPressureRandFlowLoss.MouseWheel += IgnoreMouseWheelEvent;
            #endregion

            #region cmbxTabPressureRandFlowLoss
            cmbxTabPressureRandFlowLoss.Font = detailsFont;
            cmbxTabPressureRandFlowLoss.Location = new Point(0, 25);
            cmbxTabPressureRandFlowLoss.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureRandFlowLoss.Size = new Size(156, 21);
            cmbxTabPressureRandFlowLoss.TabIndex = 95;
            cmbxTabPressureRandFlowLoss.MouseHover += CmbxTabPressure_MouseHover;
            cmbxTabPressureRandFlowLoss.MouseWheel += IgnoreMouseWheelEvent;
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
            spinTabPressureRandHorShift.Minimum = int.MinValue;
            spinTabPressureRandHorShift.MouseWheel += IgnoreMouseWheelEvent;
            #endregion

            #region cmbxTabPressureRandHorShift
            cmbxTabPressureRandHorShift.Font = detailsFont;
            cmbxTabPressureRandHorShift.Location = new Point(0, 25);
            cmbxTabPressureRandHorShift.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureRandHorShift.Size = new Size(156, 21);
            cmbxTabPressureRandHorShift.TabIndex = 99;
            cmbxTabPressureRandHorShift.MouseHover += CmbxTabPressure_MouseHover;
            cmbxTabPressureRandHorShift.MouseWheel += IgnoreMouseWheelEvent;
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
            spinTabPressureRandVerShift.Minimum = int.MinValue;
            spinTabPressureRandVerShift.MouseWheel += IgnoreMouseWheelEvent;
            #endregion

            #region cmbxTabPressureRandVerShift
            cmbxTabPressureRandVerShift.Font = detailsFont;
            cmbxTabPressureRandVerShift.Location = new Point(0, 25);
            cmbxTabPressureRandVerShift.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureRandVerShift.Size = new Size(156, 21);
            cmbxTabPressureRandVerShift.TabIndex = 103;
            cmbxTabPressureRandVerShift.MouseHover += CmbxTabPressure_MouseHover;
            cmbxTabPressureRandVerShift.MouseWheel += IgnoreMouseWheelEvent;
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
            spinTabPressureMinRedJitter.Minimum = int.MinValue;
            spinTabPressureMinRedJitter.MouseWheel += IgnoreMouseWheelEvent;
            #endregion

            #region lblTabPressureRedJitter
            lblTabPressureRedJitter.Text = Strings.JitterRed;
            lblTabPressureRedJitter.AutoSize = true;
            lblTabPressureRedJitter.Dock = DockStyle.Left;
            lblTabPressureRedJitter.Font = detailsFont;
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
            spinTabPressureMaxRedJitter.Minimum = int.MinValue;
            spinTabPressureMaxRedJitter.MouseWheel += IgnoreMouseWheelEvent;
            #endregion

            #region cmbxTabPressureRedJitter
            cmbxTabPressureRedJitter.Font = detailsFont;
            cmbxTabPressureRedJitter.Location = new Point(0, 25);
            cmbxTabPressureRedJitter.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureRedJitter.Size = new Size(156, 21);
            cmbxTabPressureRedJitter.TabIndex = 107;
            cmbxTabPressureRedJitter.MouseHover += CmbxTabPressure_MouseHover;
            cmbxTabPressureRedJitter.MouseWheel += IgnoreMouseWheelEvent;
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
            spinTabPressureMinGreenJitter.Minimum = int.MinValue;
            spinTabPressureMinGreenJitter.MouseWheel += IgnoreMouseWheelEvent;
            #endregion

            #region lblTabPressureGreenJitter
            lblTabPressureGreenJitter.AutoSize = true;
            lblTabPressureGreenJitter.Dock = DockStyle.Left;
            lblTabPressureGreenJitter.Font = detailsFont;
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
            spinTabPressureMaxGreenJitter.Minimum = int.MinValue;
            spinTabPressureMaxGreenJitter.MouseWheel += IgnoreMouseWheelEvent;
            #endregion

            #region cmbxTabPressureGreenJitter
            cmbxTabPressureGreenJitter.Font = detailsFont;
            cmbxTabPressureGreenJitter.Location = new Point(0, 25);
            cmbxTabPressureGreenJitter.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureGreenJitter.Size = new Size(156, 21);
            cmbxTabPressureGreenJitter.TabIndex = 111;
            cmbxTabPressureGreenJitter.MouseHover += CmbxTabPressure_MouseHover;
            cmbxTabPressureGreenJitter.MouseWheel += IgnoreMouseWheelEvent;
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
            spinTabPressureMinBlueJitter.Minimum = int.MinValue;
            spinTabPressureMinBlueJitter.MouseWheel += IgnoreMouseWheelEvent;
            #endregion

            #region lblTabPressureBlueJitter
            lblTabPressureBlueJitter.AutoSize = true;
            lblTabPressureBlueJitter.Dock = DockStyle.Left;
            lblTabPressureBlueJitter.Font = detailsFont;
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
            spinTabPressureMaxBlueJitter.Minimum = int.MinValue;
            spinTabPressureMaxBlueJitter.MouseWheel += IgnoreMouseWheelEvent;
            #endregion

            #region cmbxTabPressureBlueJitter
            cmbxTabPressureBlueJitter.Font = detailsFont;
            cmbxTabPressureBlueJitter.Location = new Point(0, 25);
            cmbxTabPressureBlueJitter.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureBlueJitter.Size = new Size(156, 21);
            cmbxTabPressureBlueJitter.TabIndex = 116;
            cmbxTabPressureBlueJitter.MouseHover += CmbxTabPressure_MouseHover;
            cmbxTabPressureBlueJitter.MouseWheel += IgnoreMouseWheelEvent;
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
            spinTabPressureMinHueJitter.Minimum = int.MinValue;
            spinTabPressureMinHueJitter.MouseWheel += IgnoreMouseWheelEvent;
            #endregion

            #region lblTabPressureHueJitter
            lblTabPressureHueJitter.Text = Strings.JitterHue;
            lblTabPressureHueJitter.AutoSize = true;
            lblTabPressureHueJitter.Dock = DockStyle.Left;
            lblTabPressureHueJitter.Font = detailsFont;
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
            spinTabPressureMaxHueJitter.Minimum = int.MinValue;
            spinTabPressureMaxHueJitter.MouseWheel += IgnoreMouseWheelEvent;
            #endregion

            #region cmbxTabPressureHueJitter
            cmbxTabPressureHueJitter.Font = detailsFont;
            cmbxTabPressureHueJitter.Location = new Point(0, 25);
            cmbxTabPressureHueJitter.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureHueJitter.Size = new Size(156, 21);
            cmbxTabPressureHueJitter.TabIndex = 121;
            cmbxTabPressureHueJitter.MouseHover += CmbxTabPressure_MouseHover;
            cmbxTabPressureHueJitter.MouseWheel += IgnoreMouseWheelEvent;
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
            spinTabPressureMinSatJitter.Minimum = int.MinValue;
            spinTabPressureMinSatJitter.MouseWheel += IgnoreMouseWheelEvent;
            #endregion

            #region lblTabPressureSatJitter
            lblTabPressureSatJitter.AutoSize = true;
            lblTabPressureSatJitter.Dock = DockStyle.Left;
            lblTabPressureSatJitter.Font = detailsFont;
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
            spinTabPressureMaxSatJitter.Minimum = int.MinValue;
            spinTabPressureMaxSatJitter.MouseWheel += IgnoreMouseWheelEvent;
            #endregion

            #region cmbxTabPressureSatJitter
            cmbxTabPressureSatJitter.Font = detailsFont;
            cmbxTabPressureSatJitter.Location = new Point(0, 25);
            cmbxTabPressureSatJitter.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureSatJitter.Size = new Size(156, 21);
            cmbxTabPressureSatJitter.TabIndex = 126;
            cmbxTabPressureSatJitter.MouseHover += CmbxTabPressure_MouseHover;
            cmbxTabPressureSatJitter.MouseWheel += IgnoreMouseWheelEvent;
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
            spinTabPressureMinValueJitter.Minimum = int.MinValue;
            spinTabPressureMinValueJitter.MouseWheel += IgnoreMouseWheelEvent;
            #endregion

            #region lblTabPressureValueJitter
            lblTabPressureValueJitter.AutoSize = true;
            lblTabPressureValueJitter.Dock = DockStyle.Left;
            lblTabPressureValueJitter.Font = detailsFont;
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
            spinTabPressureMaxValueJitter.Minimum = int.MinValue;
            spinTabPressureMaxValueJitter.MouseWheel += IgnoreMouseWheelEvent;
            #endregion

            #region cmbxTabPressureValueJitter
            cmbxTabPressureValueJitter.Font = detailsFont;
            cmbxTabPressureValueJitter.Location = new Point(0, 25);
            cmbxTabPressureValueJitter.Margin = new Padding(0, 0, 0, 3);
            cmbxTabPressureValueJitter.Size = new Size(156, 21);
            cmbxTabPressureValueJitter.TabIndex = 131;
            cmbxTabPressureValueJitter.MouseHover += CmbxTabPressure_MouseHover;
            cmbxTabPressureValueJitter.MouseWheel += IgnoreMouseWheelEvent;
            #endregion

            #region bttnSettings
            bttnSettings.Location = new Point(0, 2899);
            bttnSettings.Margin = new Padding(0, 3, 0, 3);
            bttnSettings.Size = new Size(155, 23);
            bttnSettings.TabIndex = 132;
            bttnSettings.TextAlign = ContentAlignment.MiddleLeft;
            bttnSettings.UpdateAccordion(Strings.AccordionSettingsBrush, true, new Control[] { panelSettings });
            #endregion

            #region panelSettings
            panelSettings.AutoSize = true;
            panelSettings.AutoSizeMode = AutoSizeMode.GrowAndShrink;
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
            bttnUpdateCurrentBrush.Enabled = false;
            bttnUpdateCurrentBrush.Location = new Point(0, 3);
            bttnUpdateCurrentBrush.Margin = new Padding(3, 3, 3, 3);
            bttnUpdateCurrentBrush.Size = new Size(150, 23);
            bttnUpdateCurrentBrush.TabIndex = 133;
            bttnUpdateCurrentBrush.Text = Strings.UpdateCurrentBrush;
            bttnUpdateCurrentBrush.Click += BttnUpdateCurrentBrush_Click;
            bttnUpdateCurrentBrush.MouseEnter += BttnUpdateCurrentBrush_MouseEnter;
            #endregion

            #region bttnClearSettings
            bttnClearSettings.Location = new Point(0, 61);
            bttnClearSettings.Margin = new Padding(3, 3, 3, 3);
            bttnClearSettings.Size = new Size(150, 23);
            bttnClearSettings.TabIndex = 135;
            bttnClearSettings.Text = Strings.ClearSettings;
            bttnClearSettings.Click += BttnClearSettings_Click;
            bttnClearSettings.MouseEnter += BttnClearSettings_MouseEnter;
            #endregion

            #region bttnDeleteBrush
            bttnDeleteBrush.Enabled = false;
            bttnDeleteBrush.Location = new Point(0, 90);
            bttnDeleteBrush.Margin = new Padding(3, 3, 3, 3);
            bttnDeleteBrush.Size = new Size(150, 23);
            bttnDeleteBrush.TabIndex = 136;
            bttnDeleteBrush.Click += BttnDeleteBrush_Click;
            bttnDeleteBrush.MouseEnter += BttnDeleteBrush_MouseEnter;
            #endregion

            #region bttnSaveBrush
            bttnSaveBrush.Location = new Point(0, 119);
            bttnSaveBrush.Margin = new Padding(3, 3, 3, 3);
            bttnSaveBrush.Size = new Size(150, 23);
            bttnSaveBrush.TabIndex = 137;
            bttnSaveBrush.Click += BttnSaveBrush_Click;
            bttnSaveBrush.MouseEnter += BttnSaveBrush_MouseEnter;
            #endregion

            #region WinDynamicDraw
            AcceptButton = bttnOk;
            AutoScaleDimensions = new SizeF(96f, 96f);
            BackgroundImageLayout = ImageLayout.None;
            CancelButton = bttnCancel;
            ClientSize = new Size(829, 541);
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
            Load += DynamicDrawWindow_Load;
            Resize += WinDynamicDraw_Resize;
            #endregion

            // Detects and uses the theme inherited from paint.net, which is either a very light or very dark color for
            // light and dark mode, respectively. So it just assumes the mode based on this color.
            UseAppThemeColors = true;
            detectedTheme = (BackColor.R > 128)
                ? ThemeName.Light
                : ThemeName.Dark;
            UseAppThemeColors = false;
            SemanticTheme.CurrentTheme = detectedTheme;

            SemanticTheme.ThemeChanged += HandleTheme;
            HandleTheme();

            #region Resume and perform layout on them all, order is VERY delicate
            topMenu.ResumeLayout(false);
            topMenu.PerformLayout();
            displayCanvas.ResumeLayout(false);
            displayCanvas.PerformLayout();
            ((ISupportInitialize)(displayCanvas)).EndInit();
            panelOkCancel.ResumeLayout(false);
            panelAllSettingsContainer.ResumeLayout(false);
            panelDockSettingsContainer.ResumeLayout(false);
            panelDockSettingsContainer.PerformLayout();
            panelSettingsContainer.ResumeLayout(false);
            panelSettingsContainer.PerformLayout();
            panelBrush.ResumeLayout(false);
            panelBrush.PerformLayout();
            panelBrushAddPickColor.ResumeLayout(false);
            panelBrushAddPickColor.PerformLayout();
            panelColorInfluenceHSV.ResumeLayout(false);
            panelColorInfluenceHSV.PerformLayout();
            panelColorControls.ResumeLayout(false);
            panelColorControls.PerformLayout();
            panelSpecialSettings.ResumeLayout(false);
            panelSpecialSettings.PerformLayout();
            panelChosenEffect.ResumeLayout(false);
            panelChosenEffect.PerformLayout();
            panelRGBLocks.ResumeLayout(false);
            panelRGBLocks.PerformLayout();
            panelHSVLocks.ResumeLayout(false);
            panelHSVLocks.PerformLayout();
            panelJitterBasics.ResumeLayout(false);
            panelJitterColor.ResumeLayout(false);
            panelShiftBasics.ResumeLayout(false);
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

            bttnToolBrush.Checked = false;
            bttnToolEraser.Checked = false;
            bttnColorPicker.Checked = false;
            bttnToolOrigin.Checked = false;

            switch (toolToSwitchTo)
            {
                case Tool.Eraser:
                    bttnToolEraser.Checked = true;
                    displayCanvas.Cursor = Cursors.Default;
                    break;
                case Tool.Brush:
                    bttnToolBrush.Checked = true;
                    displayCanvas.Cursor = Cursors.Default;
                    break;
                case Tool.SetSymmetryOrigin:
                    bttnToolOrigin.Checked = true;
                    displayCanvas.Cursor = Cursors.Hand;
                    break;
                case Tool.ColorPicker:
                    bttnColorPicker.Checked = true;

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
            menuActiveColors.Swatches[0] = settings.BrushColor;
            menuActiveColors.Swatches[1] = PdnUserSettings.userSecondaryColor;

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

            UpdateColorSelectionSliders(null);
            UpdateEnabledControls();
        }

        /// <summary>
        /// Updates the current brush color to the desired color, and optionally syncs the opacity slider.
        /// </summary>
        /// <param name="newColor">The new color to set the brush to.</param>
        private void UpdateBrushColor(Color newColor, bool updateOpacity = false,
            bool fromOpacityChanging = false, bool fromColorSliderChanging = false)
        {
            //Sets the color and updates the brushes.
            menuActiveColors.Swatches[0] = newColor;
            menuActiveColors.Refresh();

            if (!fromOpacityChanging && updateOpacity && (byte)sliderBrushOpacity.ValueInt != newColor.A)
            {
                sliderBrushOpacity.Value = newColor.A;
            }

            if (!fromColorSliderChanging)
            {
                UpdateColorSelectionSliders(null);
            }

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
        /// Recreates the brush image with color and alpha effects (via brush flow) applied.
        /// </summary>
        private void UpdateBrushImage()
        {
            if (bmpBrush == null)
            {
                return;
            }

            int finalBrushFlow = Math.Clamp(Constraint.GetStrengthMappedValue(sliderBrushFlow.ValueInt,
                (int)spinTabPressureBrushFlow.Value,
                sliderBrushFlow.MaximumInt,
                tabletPressureRatio,
                ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureBrushFlow.SelectedItem).ValueMember), 0, 255);

            //Sets the color and alpha.
            Color setColor = menuActiveColors.Swatches[0];
            float multAlpha = (activeTool == Tool.Eraser || effectToDraw.Effect != null || (BlendMode)cmbxBlendMode.SelectedIndex != BlendMode.Overwrite)
                ? finalBrushFlow / 255f
                : 1;

            int maxPossibleSize = sliderRandMaxSize.ValueInt
                + Math.Max(sliderBrushSize.ValueInt, Constraint.GetStrengthMappedValue(sliderBrushSize.ValueInt,
                    (int)spinTabPressureBrushSize.Value, sliderBrushSize.MaximumInt, 1,
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
                bmpBrushDownsized = DrawingUtils.ScaleImage(
                    bmpBrush,
                    new Size(maxPossibleSize, maxPossibleSize),
                    false, false, null,
                    (CmbxSmoothing.Smoothing)cmbxBrushSmoothing.SelectedIndex);
            }

            if (bmpBrushDownsized != null || bmpBrush != null)
            {
                // Applies the color and alpha changes.
                bmpBrushEffects?.Dispose();
                bmpBrushEffects = DrawingUtils.FormatImage(bmpBrushDownsized ?? bmpBrush, PixelFormat.Format32bppPArgb);

                // Replaces RGB entirely with the active color preemptive to drawing when possible, for performance.
                if (chkbxColorizeBrush.Checked)
                {
                    DrawingUtils.ColorImage(bmpBrushEffects, setColor, multAlpha);
                }
                else
                {
                    DrawingUtils.ColorImage(bmpBrushEffects, null, multAlpha);
                }
            }
        }

        /// <summary>
        /// Updates all sliders in tandem with each other, or based on the primary color changing.
        /// </summary>
        private void UpdateColorSelectionSliders(SliderSpecialType? sliderChanged)
        {
            // Gets the modified color from the slider that was changed, or the current primary color
            Color newColor = menuActiveColors.Swatches[0];
            if (sliderChanged == SliderSpecialType.RedGraph) { newColor = sliderColorR.GetColor(); }
            else if (sliderChanged == SliderSpecialType.GreenGraph) { newColor = sliderColorG.GetColor(); }
            else if (sliderChanged == SliderSpecialType.BlueGraph) { newColor = sliderColorB.GetColor(); }
            else if (sliderChanged == SliderSpecialType.AlphaGraph) { newColor = sliderColorA.GetColor(); }
            else if (sliderChanged == SliderSpecialType.HueGraph) { newColor = sliderColorH.GetColor(); }
            else if (sliderChanged == SliderSpecialType.SatGraph) { newColor = sliderColorS.GetColor(); }
            else if (sliderChanged == SliderSpecialType.ValGraph) { newColor = sliderColorV.GetColor(); }

            // Updates every slider that isn't invoking this change to the new color
            if (sliderChanged != SliderSpecialType.RedGraph) { sliderColorR.SetColor(newColor); }
            if (sliderChanged != SliderSpecialType.GreenGraph) { sliderColorG.SetColor(newColor); }
            if (sliderChanged != SliderSpecialType.BlueGraph) { sliderColorB.SetColor(newColor); }
            if (sliderChanged != SliderSpecialType.AlphaGraph) { sliderColorA.SetColor(newColor); }
            if (sliderChanged != SliderSpecialType.HueGraph) { sliderColorH.SetColor(newColor); }
            if (sliderChanged != SliderSpecialType.SatGraph) { sliderColorS.SetColor(newColor); }
            if (sliderChanged != SliderSpecialType.ValGraph) { sliderColorV.SetColor(newColor); }

            // Updates the brush color if it changed.
            if (sliderChanged != null)
            {
                UpdateBrushColor(newColor, sliderChanged == SliderSpecialType.AlphaGraph, false, true);
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

            menuActiveColors.Visible = (chkbxColorizeBrush.Checked || sliderColorInfluence.Value != 0) && activeTool != Tool.Eraser && effectToDraw.Effect == null;
            menuPalette.Visible = menuActiveColors.Visible;
            cmbxPaletteDropdown.Visible = menuActiveColors.Visible;
            sliderColorR.Enabled = menuActiveColors.Visible;
            sliderColorG.Enabled = menuActiveColors.Visible;
            sliderColorB.Enabled = menuActiveColors.Visible;
            sliderColorA.Enabled = menuActiveColors.Visible;
            sliderColorH.Enabled = menuActiveColors.Visible;
            sliderColorS.Enabled = menuActiveColors.Visible;
            sliderColorV.Enabled = menuActiveColors.Visible;
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
        /// Updates the tooltip popup (reused for all tooltips) and its visibility. It's visible when non-null and non
        /// empty. Up to the first 4 registered shortcuts with a matching shortcut target are appended to the end of
        /// the tooltip.
        /// </summary>
        private void UpdateTooltip(ShortcutTarget target, string newTooltip)
        {
            UpdateTooltip((kbTarget) => kbTarget.Target == target, newTooltip);
        }

        /// <summary>
        /// Updates the tooltip popup (reused for all tooltips) and its visibility. It's visible when non-null and non
        /// empty. Up to the first 4 registered shortcuts for which the filter function returns true are appended to
        /// the end of the tooltip.
        /// </summary>
        private void UpdateTooltip(Func<KeyboardShortcut, bool> filterFunc, string newTooltip)
        {
            string finalTooltip = newTooltip;
            List<string> shortcuts = new List<string>();

            // Creates a list of up to 4 bound keyboard shortcuts for the shortcut target.
            int extraCount = 0;
            foreach (KeyboardShortcut shortcut in KeyboardShortcuts)
            {
                if (filterFunc?.Invoke(shortcut) ?? false)
                {
                    if (shortcuts.Count == 4)
                    {
                        extraCount++;
                        continue;
                    }

                    shortcuts.Add(KeyboardShortcut.GetShortcutKeysString(
                        shortcut.Keys,
                        shortcut.RequireCtrl,
                        shortcut.RequireShift,
                        shortcut.RequireAlt,
                        shortcut.RequireWheel,
                        shortcut.RequireWheelUp,
                        shortcut.RequireWheelDown));
                }
            }

            if (shortcuts.Count != 0)
            {
                if (extraCount != 0)
                {
                    shortcuts.Add(string.Format(Strings.ShortcutsOver3Tip, extraCount));
                }

                finalTooltip += $"{Environment.NewLine}{Environment.NewLine}{Strings.ShortcutsTooltipTip}";
                finalTooltip += $"{Environment.NewLine}{string.Join(Environment.NewLine, shortcuts)}";
            }

            UpdateTooltip(finalTooltip);
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
        /// Updates the top menu GUI based on current preferences.
        /// </summary>
        private void UpdateTopMenuState()
        {
            if (UserSettings.PreferredTheme == ThemePreference.Inherited)
            {
                SemanticTheme.CurrentTheme = detectedTheme;
            }
            else if (UserSettings.PreferredTheme == ThemePreference.Light)
            {
                SemanticTheme.CurrentTheme = ThemeName.Light;
            }
            else if (UserSettings.PreferredTheme == ThemePreference.Dark)
            {
                SemanticTheme.CurrentTheme = ThemeName.Dark;
            }

            menuSetCanvasBgImageFit.Checked = UserSettings.BackgroundDisplayMode == BackgroundDisplayMode.ClipboardFit;
            menuSetCanvasBgImageOnlyIfFits.Checked = UserSettings.BackgroundDisplayMode == BackgroundDisplayMode.ClipboardOnlyIfFits;
            menuSetCanvasBgTransparent.Checked = UserSettings.BackgroundDisplayMode == BackgroundDisplayMode.Transparent;
            menuSetCanvasBgGray.Checked = UserSettings.BackgroundDisplayMode == BackgroundDisplayMode.Gray;
            menuSetCanvasBgWhite.Checked = UserSettings.BackgroundDisplayMode == BackgroundDisplayMode.White;
            menuSetCanvasBgBlack.Checked = UserSettings.BackgroundDisplayMode == BackgroundDisplayMode.Black;
            menuBrushIndicatorSquare.Checked = UserSettings.BrushCursorPreview == BrushCursorPreview.Square;
            menuBrushIndicatorPreview.Checked = UserSettings.BrushCursorPreview == BrushCursorPreview.Preview;
            menuSetThemeDefault.Checked = UserSettings.PreferredTheme == ThemePreference.Inherited;
            menuSetThemeLight.Checked = UserSettings.PreferredTheme == ThemePreference.Light;
            menuSetThemeDark.Checked = UserSettings.PreferredTheme == ThemePreference.Dark;
            menuShowSymmetryLinesInUse.Checked = UserSettings.ShowSymmetryLinesWhenUsingSymmetry;
            menuShowMinDistanceInUse.Checked = UserSettings.ShowCircleRadiusWhenUsingMinDistance;
            menuConfirmCloseSave.Checked = UserSettings.DisableConfirmationOnCloseOrSave;
            menuColorPickerIncludesAlpha.Checked = UserSettings.ColorPickerIncludesAlpha;
            menuColorPickerSwitchesToPrevTool.Checked = UserSettings.ColorPickerSwitchesToLastTool;
            menuRemoveUnfoundImagePaths.Checked = UserSettings.RemoveBrushImagePathsWhenNotFound;
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
                                            Size newImageSize = DrawingUtils.ComputeBrushSize(item.Image.Width, item.Image.Height, maxBrushImageSize);
                                            scaledBrushImage = DrawingUtils.ScaleImage(item.Image, newImageSize);
                                        }

                                        //Pads the image to be square if needed, makes fully
                                        //opaque images use intensity for alpha, and draws the
                                        //altered loaded bitmap to the brush image.
                                        using Bitmap newBmp = DrawingUtils.MakeTransparent(scaledBrushImage ?? item.Image);
                                        Bitmap brushImage = DrawingUtils.MakeBitmapSquare(newBmp);

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

                                    Size newImageSize = DrawingUtils.ComputeBrushSize(bmp.Width, bmp.Height, maxBrushImageSize);

                                    scaledBrush = DrawingUtils.ScaleImage(bmp, newImageSize);
                                }

                                //Pads the image to be square if needed, makes fully
                                //opaque images use intensity for alpha, and draws the
                                //altered loaded bitmap to the brush.
                                using Bitmap newBmp = DrawingUtils.MakeTransparent(scaledBrush ?? bmp);
                                brushImage = DrawingUtils.MakeBitmapSquare(newBmp);

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
                        int finalBrushSize = Constraint.GetStrengthMappedValue(sliderBrushSize.ValueInt,
                            (int)spinTabPressureBrushSize.Value,
                            sliderBrushSize.MaximumInt,
                            tabletPressureRatio,
                            ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureBrushSize.SelectedItem).ValueMember);

                        DrawBrush(new PointF(
                            mouseLocPrev.X / canvasZoom - halfPixelOffset,
                            mouseLocPrev.Y / canvasZoom - halfPixelOffset),
                            finalBrushSize, tabletPressureRatio);
                    }
                }
                // Samples the color under the mouse.
                else if (activeTool == Tool.ColorPicker)
                {
                    GetColorFromCanvas(new Point(
                        (int)Math.Round(mouseLocPrev.X / canvasZoom),
                        (int)Math.Round(mouseLocPrev.Y / canvasZoom)));
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
                finalMinDrawDistance = Math.Clamp(Constraint.GetStrengthMappedValue(sliderMinDrawDistance.ValueInt,
                    (int)spinTabPressureMinDrawDistance.Value,
                    sliderMinDrawDistance.MaximumInt,
                    tabletPressureRatio,
                    ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureMinDrawDistance.SelectedItem).ValueMember),
                    0, sliderMinDrawDistance.MaximumInt);

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
                    int finalBrushDensity = Math.Clamp(Constraint.GetStrengthMappedValue(sliderBrushDensity.ValueInt,
                        (int)spinTabPressureBrushDensity.Value,
                        sliderBrushDensity.MaximumInt,
                        tabletPressureRatio,
                        ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureBrushDensity.SelectedItem).ValueMember),
                        0, sliderBrushDensity.MaximumInt);

                    // Draws without speed control. Messier, but faster.
                    if (finalBrushDensity == 0)
                    {
                        int finalBrushSize = Constraint.GetStrengthMappedValue(sliderBrushSize.ValueInt,
                            (int)spinTabPressureBrushSize.Value,
                            sliderBrushSize.MaximumInt,
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
                        int finalBrushSize = Constraint.GetStrengthMappedValue(sliderBrushSize.ValueInt,
                            (int)spinTabPressureBrushSize.Value,
                            sliderBrushSize.MaximumInt,
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
                                finalBrushSize = Constraint.GetStrengthMappedValue(sliderBrushSize.ValueInt,
                                    (int)spinTabPressureBrushSize.Value,
                                    sliderBrushSize.MaximumInt,
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
            else if (e.Button == MouseButtons.Left)
            {
                // Samples the color under the mouse.
                if (activeTool == Tool.ColorPicker)
                {
                    GetColorFromCanvas(new Point(
                        (int)Math.Round(mouseLoc.X / canvasZoom),
                        (int)Math.Round(mouseLoc.Y / canvasZoom)));
                }
                else if (activeTool == Tool.SetSymmetryOrigin
                    && cmbxSymmetry.SelectedIndex != (int)SymmetryMode.SetPoints)
                {
                    symmetryOrigin = TransformPoint(new PointF(e.Location.X, e.Location.Y));
                }
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
                    sliderBrushSize.ValueInt, 1); // opinionated: pass as full pressure because this is usually desired.
            }

            // Merge the staged layer down to committed, then clear the staged layer.
            if (isUserDrawing.stagedChanged)
            {
                DrawingUtils.MergeImage(bmpStaged, bmpCommitted, bmpCommitted,
                    bmpCommitted.GetBounds(),
                    (BlendMode)cmbxBlendMode.SelectedIndex,
                    (chkbxLockAlpha.Checked,
                    chkbxLockR.Checked, chkbxLockG.Checked, chkbxLockB.Checked,
                    chkbxLockHue.Checked, chkbxLockSat.Checked, chkbxLockVal.Checked));

                DrawingUtils.ColorImage(bmpStaged, ColorBgra.Black, 0);
            }

            if (isUserDrawing.canvasChanged && effectToDraw.Effect != null)
            {
                ActiveEffectRender();
            }

            if (isUserPanning)
            {
                isUserPanning = false;
                Cursor = Cursors.Default;
            }

            isUserDrawing.started = false;
            isUserDrawing.canvasChanged = false;
            isUserDrawing.stagedChanged = false;
            timerRepositionUpdate.Enabled = false;

            if (activeTool == Tool.ColorPicker && UserSettings.ColorPickerSwitchesToLastTool)
            {
                SwitchTool(lastTool);
            }

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
                using (HatchBrush hatchBrush = new HatchBrush(HatchStyle.LargeCheckerBoard, Color.White, Color.FromArgb(191, 191, 191)))
                {
                    e.Graphics.FillRectangle(hatchBrush, visibleBounds);
                }
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
                        DrawingUtils.MergeImage(bmpStaged, bmpCommitted, bmpMerged,
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
                using (SolidBrush overlay = new SolidBrush(Color.FromArgb(63, 0, 0, 0)))
                {
                    e.Graphics.FillRegion(overlay, drawingArea);
                }

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
                            DrawingUtils.ColorImageAttr(0, 0, 0, 0.5f));
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
        /// Any color logic that gets set only once, dependent on the current theme, needs to subscribe to the theme
        /// changed event so it can be recalculated when theme preference loads from asynchronous user settings.
        /// </summary>
        private void HandleTheme()
        {
            BackColor = SemanticTheme.GetColor(ThemeSlot.MenuBg);
            cmbxBlendMode.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            cmbxBlendMode.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            cmbxBrushSmoothing.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            cmbxBrushSmoothing.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            cmbxChosenEffect.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            cmbxChosenEffect.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            cmbxPaletteDropdown.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            cmbxPaletteDropdown.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            cmbxSymmetry.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            cmbxSymmetry.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            displayCanvas.BackColor = SemanticTheme.GetColor(ThemeSlot.CanvasBg);
            lblTabPressureBlueJitter.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            lblTabPressureBrushDensity.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            lblTabPressureGreenJitter.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            lblTabPressureHueJitter.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            lblTabPressureMinDrawDistance.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            lblTabPressureRandFlowLoss.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            lblTabPressureRandHorShift.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            lblTabPressureRandMaxSize.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            lblTabPressureRandMinSize.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            lblTabPressureRandRotLeft.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            lblTabPressureRandRotRight.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            lblTabPressureRandVerShift.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            lblTabPressureRedJitter.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            lblTabPressureSatJitter.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            lblTabPressureValueJitter.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            listviewBrushPicker.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            listviewBrushPicker.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            panelDockSettingsContainer.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuBg);
            spinTabPressureBrushDensity.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            spinTabPressureBrushDensity.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            spinTabPressureBrushFlow.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            spinTabPressureBrushFlow.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            spinTabPressureBrushOpacity.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            spinTabPressureBrushOpacity.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            spinTabPressureBrushRotation.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            spinTabPressureBrushRotation.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            spinTabPressureBrushSize.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            spinTabPressureBrushSize.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            spinTabPressureMaxBlueJitter.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            spinTabPressureMaxBlueJitter.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            spinTabPressureMaxGreenJitter.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            spinTabPressureMaxGreenJitter.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            spinTabPressureMaxHueJitter.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            spinTabPressureMaxHueJitter.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            spinTabPressureMaxRedJitter.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            spinTabPressureMaxRedJitter.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            spinTabPressureMaxSatJitter.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            spinTabPressureMaxSatJitter.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            spinTabPressureMaxValueJitter.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            spinTabPressureMaxValueJitter.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            spinTabPressureMinBlueJitter.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            spinTabPressureMinBlueJitter.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            spinTabPressureMinDrawDistance.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            spinTabPressureMinDrawDistance.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            spinTabPressureMinGreenJitter.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            spinTabPressureMinGreenJitter.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            spinTabPressureMinHueJitter.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            spinTabPressureMinHueJitter.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            spinTabPressureMinRedJitter.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            spinTabPressureMinRedJitter.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            spinTabPressureMinSatJitter.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            spinTabPressureMinSatJitter.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            spinTabPressureMinValueJitter.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            spinTabPressureMinValueJitter.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            spinTabPressureRandFlowLoss.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            spinTabPressureRandFlowLoss.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            spinTabPressureRandHorShift.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            spinTabPressureRandHorShift.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            spinTabPressureRandMaxSize.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            spinTabPressureRandMaxSize.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            spinTabPressureRandMinSize.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            spinTabPressureRandMinSize.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            spinTabPressureRandRotLeft.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            spinTabPressureRandRotLeft.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            spinTabPressureRandRotRight.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            spinTabPressureRandRotRight.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            spinTabPressureRandVerShift.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            spinTabPressureRandVerShift.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            topMenu.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuBg);
            topMenu.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            txtTabPressureBrushFlow.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            txtTabPressureBrushOpacity.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            txtTabPressureBrushRotation.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            txtTabPressureBrushSize.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            Refresh();
        }

        /// <summary>
        /// Spin buttons and comboboxes (among others) handle the mouse wheel event, which makes it hard to scroll across
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
                ExternalOps.SimulateClick(ExternalOps.MouseEvents.LeftDown);
            }
            else if (tabletPressureRatio > deadzone && newPressureRatio <= deadzone)
            {
                ExternalOps.SimulateClick(ExternalOps.MouseEvents.LeftUp);
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
            UpdateTooltip(ShortcutTarget.AutomaticBrushDensity, Strings.AutomaticBrushDensityTip);
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
            UpdateTooltip(ShortcutTarget.BlendMode, Strings.BlendModeTip);
        }

        private void BttnBlendMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateEnabledControls();
            SliderBrushFlow_ValueChanged(null, 0);
        }

        /// <summary>
        /// Sets the new color of the brush.
        /// </summary>
        private void MenuActiveColors_Click(object sender, EventArgs e)
        {
            //Creates and configures a color dialog to display.
            ColorDialog dialog = new ColorDialog
            {
                FullOpen = true,
                Color = menuActiveColors.Swatches[0]
            };

            //If the user successfully chooses a color.
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                // TODO: until a color dialog with alpha is used, only change RGB channels.
                UpdateBrushColor(Color.FromArgb(sliderBrushOpacity.ValueInt, dialog.Color));
            }
        }

        private void MenuActiveColors_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.Color, Strings.BrushColorTip);
        }

        private void CmbxChosenEffect_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.ChosenEffect, Strings.ChosenEffectTip);
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
                SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlBg),
                new Rectangle(2, e.Bounds.Top, e.Bounds.Width, e.Bounds.Height));

            //Draws the image of the current item to be repainted.
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
                SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlText),
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
            UpdateTooltip(ShortcutTarget.SmoothingMode, Strings.BrushSmoothingTip);
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
        /// Saves the current brush settings over the selected brush.
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
                    DrawingUtils.OverwriteBits(redoBmp, bmpCommitted);

                    if (effectToDraw.Effect == null)
                    {
                        DrawingUtils.ColorImage(bmpStaged, ColorBgra.Black, 0);
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
                menuRedo.Enabled = false;
            }
            if (!menuUndo.Enabled && undoHistory.Count > 0)
            {
                menuUndo.Enabled = true;
            }
        }

        private void BttnRedo_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.RedoAction, Strings.RedoTip);
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
            UpdateTooltip(ShortcutTarget.SymmetryMode, Strings.SymmetryTip);
        }

        private void BttnToolBrush_Click(object sender, EventArgs e)
        {
            SwitchTool(Tool.Brush);
        }

        private void BttnToolBrush_MouseEnter(object sender, EventArgs e)
        {
            static bool filter(KeyboardShortcut shortcut)
            {
                int index = (int)Tool.Brush;
                return shortcut.Target == ShortcutTarget.SelectedTool &&
                    (shortcut.ActionData.Contains($"{index}|set") ||
                    (shortcut.ActionData.Contains("cycle") && shortcut.ActionData.Contains(index.ToString())));
            }

            UpdateTooltip(filter, Strings.ToolBrushTip);
        }

        private void BttnToolColorPicker_Click(object sender, EventArgs e)
        {
            SwitchTool(Tool.ColorPicker);
        }

        private void BttnToolColorPicker_MouseEnter(object sender, EventArgs e)
        {
            static bool filter(KeyboardShortcut shortcut)
            {
                int index = (int)Tool.ColorPicker;
                return shortcut.Target == ShortcutTarget.SelectedTool &&
                    (shortcut.ActionData.Contains($"{index}|set") ||
                    (shortcut.ActionData.Contains("cycle") && shortcut.ActionData.Contains(index.ToString())));
            }

            UpdateTooltip(filter, Strings.ColorPickerTip);
        }

        private void BttnToolEraser_Click(object sender, EventArgs e)
        {
            SwitchTool(Tool.Eraser);
        }

        private void BttnToolEraser_MouseEnter(object sender, EventArgs e)
        {
            static bool filter(KeyboardShortcut shortcut)
            {
                int index = (int)Tool.Eraser;
                return shortcut.Target == ShortcutTarget.SelectedTool &&
                    (shortcut.ActionData.Contains($"{index}|set") ||
                    (shortcut.ActionData.Contains("cycle") && shortcut.ActionData.Contains(index.ToString())));
            }

            UpdateTooltip(filter, Strings.ToolEraserTip);
        }

        private void BttnToolOrigin_Click(object sender, EventArgs e)
        {
            SwitchTool(Tool.SetSymmetryOrigin);
        }

        private void BttnToolOrigin_MouseEnter(object sender, EventArgs e)
        {
            static bool filter(KeyboardShortcut shortcut)
            {
                int index = (int)Tool.SetSymmetryOrigin;
                return shortcut.Target == ShortcutTarget.SelectedTool &&
                    (shortcut.ActionData.Contains($"{index}|set") ||
                    (shortcut.ActionData.Contains("cycle") && shortcut.ActionData.Contains(index.ToString())));
            }

            UpdateTooltip(filter, Strings.ToolOriginTip);
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
                    DrawingUtils.OverwriteBits(undoBmp, bmpCommitted);

                    if (effectToDraw.Effect == null)
                    {
                        DrawingUtils.ColorImage(bmpStaged, ColorBgra.Black, 0);
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
                menuUndo.Enabled = false;
            }
            if (!menuRedo.Enabled && redoHistory.Count > 0)
            {
                menuRedo.Enabled = true;
            }
        }

        private void BttnUndo_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.UndoAction, Strings.UndoTip);
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
            UpdateTooltip(ShortcutTarget.ColorizeBrush, Strings.ColorizeBrushTip);
        }

        private void ChkbxColorInfluenceHue_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.ColorInfluenceHue, Strings.ColorInfluenceHTip);
        }

        private void ChkbxColorInfluenceSat_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.ColorInfluenceSat, Strings.ColorInfluenceSTip);
        }

        private void ChkbxColorInfluenceVal_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.ColorInfluenceVal, Strings.ColorInfluenceVTip);
        }

        private void ChkbxDitherDraw_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.DitherDraw, Strings.DitherDrawTip);
        }

        private void ChkbxLockAlpha_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.LockAlphaTip);
        }

        private void ChkbxLockR_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.DoLockR, Strings.LockRTip);
        }

        private void ChkbxLockG_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.DoLockG, Strings.LockGTip);
        }

        private void ChkbxLockB_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.DoLockB, Strings.LockBTip);
        }

        private void ChkbxLockHue_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.DoLockHue, Strings.LockHueTip);
        }

        private void ChkbxLockSat_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.DoLockSat, Strings.LockSatTip);
        }

        private void ChkbxLockVal_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.DoLockVal, Strings.LockValTip);
        }

        private void ChkbxOrientToMouse_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.RotateWithMouse, Strings.OrientToMouseTip);
        }

        /// <summary>
        /// Parses the txt file associated with the selected dropdown option, replacing the current color palette with
        /// the colors loaded from that file. For the entry representing the default color palette, the colors are
        /// requested from Paint.NET's service for the current palette instead of loading from a file.
        /// </summary>
        private void CmbxPaletteDropdown_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbxPaletteDropdown.SelectedIndex >= 0 && cmbxPaletteDropdown.SelectedIndex < paletteOptions.Count)
            {
                LoadPalette(paletteOptions[cmbxPaletteDropdown.SelectedIndex].Item2);
            }

            // Adjusts the size of the palette based on how many colors were loaded.
            if (menuPalette.Swatches.Count > 0)
            {
                int minSwatchSize = 8; // 8x8 min size per swatch, anything less is hard to use
                int menuHeight = topMenu.Height; // height is the hard constraint, width can expand
                int maxNumRows = Math.Max(menuHeight / minSwatchSize, 1); // how many fit stacked in the height
                int squareOfSwatches = maxNumRows * maxNumRows; // this is height * width since it's all square
                float numSwatches = menuPalette.Swatches.Count; // float to avoid conversions in Ceiling
                int numberOfSquares = (int)Math.Ceiling(numSwatches / squareOfSwatches); // minimize space

                if (numSwatches > squareOfSwatches)
                {
                    // once there's enough colors to fill a square, it never shrinks the number of rows
                    menuPalette.NumRows = maxNumRows;
                }
                else
                {
                    // limits the number of rows if there're fewer swatches than what fits on the rows
                    menuPalette.NumRows = (int)Math.Ceiling(numSwatches / maxNumRows);
                }

                // maxNumRows * minSwatchSize is like menuHeight, but with no remainder (avoids taking extra space).
                menuPalette.Width = numberOfSquares * maxNumRows * minSwatchSize;
            }

            menuPalette.Refresh();
        }

        private void ChkbxSeamlessDrawing_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.SeamlessDrawing, Strings.SeamlessDrawingTip);
        }

        private void CmbxTabPressure_MouseHover(object sender, EventArgs e)
        {
            UpdateTooltip(
                Strings.ValueInfluenceTip + Environment.NewLine + Environment.NewLine
                + Strings.ValueTypeNothingTip + Environment.NewLine
                + Strings.ValueTypeAddTip + Environment.NewLine
                + Strings.ValueTypeAddPercentTip + Environment.NewLine
                + Strings.ValueTypeAddPercentCurrentTip + Environment.NewLine
                + Strings.ValueTypeMatchValueTip + Environment.NewLine
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
            UpdateTooltip(ShortcutTarget.SelectedBrushImage, Strings.BrushImageSelectorTip);
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
                    bmpBrush = DrawingUtils.FormatImage(
                        currentItem.Brush,
                        PixelFormat.Format32bppPArgb);
                    bmpBrushDownsized = null;

                    UpdateBrushImage();
                }

                listviewBrushImagePicker.Items[index].EnsureVisible();
            }
        }

        private void ListviewBrushPicker_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.SelectedBrush, Strings.BrushSelectorTip);
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

                selection.EnsureVisible();
            }
        }

        private void SliderBrushDensity_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.BrushStrokeDensity, Strings.BrushDensityTip);
        }

        private void SliderBrushFlow_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.Flow, Strings.BrushFlowTip);
        }

        private void SliderBrushFlow_ValueChanged(object sender, float e)
        {
            UpdateBrushImage();
        }

        private void SliderBrushOpacity_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.BrushOpacity, Strings.BrushOpacityTip);
        }

        private void SliderBrushOpacity_ValueChanged(object sender, float e)
        {
            UpdateBrushColor(Color.FromArgb((int)sliderBrushOpacity.Value, menuActiveColors.Swatches[0]), false, true);
        }

        private void SliderBrushSize_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.Size, Strings.BrushSizeTip);
        }

        private void SliderBrushSize_ValueChanged(object sender, float e)
        {
            //Updates to show changes in the brush indicator.
            UpdateBrushImage();
            displayCanvas.Refresh();
        }

        private void SliderBrushRotation_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.Rotation, Strings.BrushRotationTip);
        }

        private void SliderBrushRotation_ValueChanged(object sender, float e)
        {
            // Refreshes the brush indicator (ignore if the indicator wouldn't be shown).
            if (!isUserDrawing.started)
            {
                displayCanvas.Refresh();
            }
        }

        private void SliderCanvasZoom_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.CanvasZoom, Strings.CanvasZoomTip);
        }

        private void SliderCanvasZoom_ValueChanged(object sender, float e)
        {
            Zoom(0, false);
        }

        private void SliderCanvasAngle_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.CanvasRotation, Strings.CanvasAngleTip);
        }

        private void SliderCanvasAngle_ValueChanged(object sender, float e)
        {
            displayCanvas.Refresh();
        }

        private void SliderColorA_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.ATip);
        }

        private void SliderColorB_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.BTip);
        }

        private void SliderColorG_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.GTip);
        }

        private void SliderColorH_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.HueTip);
        }

        /// <summary>
        /// Resets the brush to reconfigure colorization. Colorization is
        /// applied when the brush is refreshed.
        /// </summary>
        private void SliderColorInfluence_ValueChanged(object sender, float e)
        {
            UpdateEnabledControls();
            UpdateBrushImage();
        }

        private void SliderColorInfluence_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.ColorInfluence, Strings.ColorInfluenceTip);
        }

        private void SliderColorR_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.RTip);
        }

        private void SliderColorS_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.SatTip);
        }

        private void SliderColorV_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.ValTip);
        }

        private void SliderMinDrawDistance_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.MinDrawDistance, Strings.MinDrawDistanceTip);
        }

        private void SliderRandHorzShift_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.JitterHorSpray, Strings.RandHorzShiftTip);
        }

        private void SliderJitterMaxBlue_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.JitterBlueMax, Strings.JitterBlueTip);
        }

        private void SliderJitterMaxGreen_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.JitterGreenMax, Strings.JitterGreenTip);
        }

        private void SliderJitterMaxRed_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.JitterRedMax, Strings.JitterRedTip);
        }

        private void SliderRandFlowLoss_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.JitterFlowLoss, Strings.RandFlowLossTip);
        }

        private void SliderRandMaxSize_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.JitterMaxSize, Strings.RandMaxSizeTip);
        }

        private void SliderJitterMinBlue_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.JitterBlueMin, Strings.JitterBlueTip);
        }

        private void SliderJitterMinGreen_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.JitterGreenMin, Strings.JitterGreenTip);
        }

        private void SliderJitterMaxHue_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.JitterHueMax, Strings.JitterHueTip);
        }

        private void SliderJitterMinHue_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.JitterHueMin, Strings.JitterHueTip);
        }

        private void SliderJitterMinRed_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.JitterRedMin, Strings.JitterRedTip);
        }

        private void SliderJitterMaxSat_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.JitterSatMax, Strings.JitterSaturationTip);
        }

        private void SliderJitterMinSat_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.JitterSatMin, Strings.JitterSaturationTip);
        }

        private void SliderJitterMaxVal_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.JitterValMax, Strings.JitterValueTip);
        }

        private void SliderJitterMinVal_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.JitterValMin, Strings.JitterValueTip);
        }

        private void SliderRandMinSize_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.JitterMinSize, Strings.RandMinSizeTip);
        }

        private void SliderRandRotLeft_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.JitterRotLeft, Strings.RandRotLeftTip);
        }

        private void SliderRandRotRight_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.JitterRotRight, Strings.RandRotRightTip);
        }

        private void SliderRandVertShift_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.JitterVerSpray, Strings.RandVertShiftTip);
        }

        private void SliderShiftFlow_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.FlowShift, Strings.ShiftFlowTip);
        }

        private void SliderShiftRotation_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.RotShift, Strings.ShiftRotationTip);
        }

        private void SliderShiftSize_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(ShortcutTarget.SizeShift, Strings.ShiftSizeTip);
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