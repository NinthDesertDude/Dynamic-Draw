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
    [System.ComponentModel.DesignerCategory("")] // disable winforms designer, it will corrupt this.
    public class WinDynamicDraw : EffectConfigDialog
    {
        #region Fields (Non Gui)
        #region Bitmaps
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
        #endregion

        #region Brush Image Loading
        /// <summary>
        /// Whether the brush loading worker should reload brushes after cancelation.
        /// </summary>
        private bool doReinitializeBrushImages;

        /// <summary>
        /// The last directory that brush images were imported from via the add brush images button, by the user during
        /// this run of the plugin.
        /// </summary>
        private string importBrushImagesLastDirectory = null;

        /// <summary>
        /// Stores the user's custom brush images by file and path until it can
        /// be copied to persistent settings, or ignored.
        /// </summary>
        private readonly HashSet<string> loadedBrushImagePaths =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates the list of brushes used by the brush selector.
        /// </summary>
        private BrushSelectorItemCollection loadedBrushImages;

        /// <summary>
        /// The selected brush image path from the effect token.
        /// </summary>
        private string tokenSelectedBrushImagePath;
        #endregion

        #region Combobox Sources
        /// <summary>
        /// Contains the list of all blend mode options for brush strokes.
        /// </summary>
        private readonly BindingList<Tuple<string, BlendMode>> blendModeOptions;

        /// <summary>
        /// Contains the list of all available effects to use while drawing.
        /// </summary>
        private readonly BindingList<Tuple<string, IEffectInfo>> effectOptions;

        /// <summary>
        /// The list of palettes, with the filename (no file extension) as the key, and the path to the file, including
        /// extension, as the value.
        /// </summary>
        private readonly BindingList<Tuple<string, PaletteComboboxOptions>> paletteOptions;

        /// <summary>
        /// Contains the list of all interpolation options for applying brush
        /// strokes.
        /// </summary>
        readonly BindingList<InterpolationItem> smoothingMethods;

        /// <summary>
        /// Contains the list of all symmetry options for using brush strokes.
        /// </summary>
        readonly BindingList<Tuple<string, SymmetryMode>> symmetryOptions;
        #endregion

        #region Canvas and Action Tracking
        /// <summary>
        /// The position and size of the canvas.
        /// </summary>
        private (int x, int y, int width, int height) canvas;

        /// <summary>
        /// A multiplier for zoom percent, e.g. 2 = 200% (zoomed in), 0.5 = 50% (zoomed out).
        /// </summary>
        private float canvasZoom = 1;

        /// <summary>
        /// A list of all contexts.
        /// </summary>
        private readonly HashSet<CommandContext> contexts = new HashSet<CommandContext>();

        /// <summary>
        /// While drawing the canvas, a half-pixel offset avoids visual edge boundary discrepancies. When zoomed in,
        /// this offset causes the position the user draws at to be incongruent with their mouse location. Subtracting
        /// half a pixel solves this schism.
        /// </summary>
        private const float halfPixelOffset = 0.5f;

        /// <summary>
        /// How close the mouse must be to a drag point using any tool in order to drag it.
        /// </summary>
        private const int dragHotspotRadius = 15;

        /// <summary>
        /// Tracks when the user has begun drawing by pressing down the mouse or applying pressure, and if they
        /// actually affected the image (which might not happen based on the dynamic brush settings), and whether
        /// changes were made to the staged layer or not (there's a major performance boost for not having to use or
        /// draw the staged layer because that involves merging two layers in realtime as you draw).
        /// </summary>
        private (bool started, bool canvasChanged, bool stagedChanged) isUserDrawing = new(false, false, false);

        private bool isUserPanning = false;
        private bool isWheelZooming = false;
        #endregion

        #region Tablet Pressure Constraint
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

        private Dictionary<CommandTarget, BrushSettingConstraint> tabPressureConstraints;
        #endregion

        #region Input Tracking
        /// <summary>
        /// List of keys currently pressed. The last item in the list is always the most recently pressed key.
        /// </summary>
        private readonly HashSet<Keys> currentKeysPressed;

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

        #endregion

        #region Palette
        /// <summary>
        /// Generated palettes occupy one row only. This is how many are allowed.
        /// </summary>
        private int paletteGeneratedMaxColors = 20;

        /// <summary>
        /// The max number of colors allowed for the recent colors palette.
        /// </summary>
        private readonly int paletteRecentMaxColors = 10;

        /// <summary>
        /// The selected swatch in the palette, or -1 if none. Mainly useful for shortcuts.
        /// </summary>
        private int paletteSelectedSwatchIndex = -1;

        /// <summary>
        /// A list of all recently-used colors, up to the value 
        /// </summary>
        private readonly List<Color> paletteRecent = new List<Color>();
        #endregion

        #region Settings
        private Tool activeTool = Tool.Brush;

        /// <summary>
        /// The loaded scripts for the current brush.
        /// </summary>
        private ToolScripts currentBrushScripts = null;

        /// <summary>
        /// The LUA interpreter used to execute brush scripts, if present. It's loaded when a scripted tool is set, and
        /// global variables persist until a different set of scripts is loaded.
        /// </summary>
        private MoonSharp.Interpreter.Script scriptEnv = null;

        /// <summary>
        /// Active brush in settings. The file path identifying the currently loaded brush, or name for built-in
        /// brushes, or null if no saved brush is currently active.
        /// </summary>
        private string currentBrushPath = null;

        /// <summary>
        /// Tracks whatever the theme inherited from paint.net was, so it can be used if set.
        /// </summary>
        private ThemeName detectedTheme;

        /// <summary>
        /// If set and using the brush tool (not eraser), the effect and its settings will be
        /// applied to the staged bmp when first starting to draw, and copied to committed bmp as
        /// the user draws. <see cref="settings"/> is used for non-property based effects, while
        /// <see cref="propertySettings"/> is used for property-based effects.
        /// </summary>
        private CustomEffect effectToDraw = new CustomEffect();

        /// <summary>
        /// Flow shift in settings. Determines the direction of flow shifting, which can be growing (true) or shrinking
        /// (false). Used by the flow shift slider.
        /// </summary>
        private bool isGrowingFlow = true;

        /// <summary>
        /// Size shift in settings. Determines the direction of size shifting, which can be growing (true) or shrinking
        /// (false). Used by the size shift slider.
        /// </summary>
        private bool isGrowingSize = true;

        /// <summary>
        /// Shortcuts deserialize asynchronously (and can fail sometimes). This returns that object if it exists, else
        /// it returns a copy of the shortcut defaults (so any changes are discarded).
        /// </summary>
        private HashSet<Command> KeyboardShortcuts
        {
            get
            {
                return settings?.CustomShortcuts ?? new PersistentSettings().CustomShortcuts;
            }
            set
            {
                if (settings?.CustomShortcuts != null)
                {
                    settings.CustomShortcuts = value;
                }
            }
        }

        /// <summary>
        /// All user settings including custom brushes / brush image locations and the previous brush settings from
        /// the last time the effect was ran. This overlaps other properties, like <see cref="KeyboardShortcuts"/>
        /// since this one is asynchronous. The others are synchronously available, using default values until they've
        /// fully loaded. This is guaranteed available after OnShown is called. Don't use properties directly from
        /// this object unless you're sure that OnShown has been called.
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
        #endregion

        #region Undo / Redo
        /// <summary>
        /// List of temporary file names to load to perform redo.
        /// </summary>
        private readonly Stack<string> redoHistory = new Stack<string>();

        /// <summary>
        /// The folder used to store undo/redo images, and deleted on exit.
        /// </summary>
        private TempDirectory tempDir;

        /// <summary>
        /// List of temporary file names to load to perform undo.
        /// </summary>
        private readonly Stack<string> undoHistory = new Stack<string>();
        #endregion

        #region Tool variables
        /// <summary>
        /// The point relative to the cursor to use as the source for the clone stamp tool. This value is null to mark
        /// when the point hasn't been set by the user.
        /// </summary>
        private PointF? cloneStampOrigin = null;

        /// <summary>
        /// This is used to decide when to convert the clone stamp origin to a relative location. At first, the clone
        /// stamp position will be absolute until the user starts drawing, at which point it will be converted to a
        /// relative location, and this value set to true. It's set to false any time the origin is set to null.
        /// </summary>
        private bool cloneStampStartedDrawing = false;

        private Tool lastTool = Tool.Brush;

        /// <summary>
        /// The origins used by the line tool, including the locations for the start and end of the line, and the index
        /// for which drag hotspot is active. When a location is null, it hasn't been set. When the drag hotspot is
        /// null, it means the user isn't dragging any point.
        /// </summary>
        private (List<PointF> points, int? dragIndex) lineOrigins = (new List<PointF>(), null);
        #endregion

        /// <summary>
        /// The calculated minimum draw distance including factors such as pressure sensitivity.
        /// This is specially tracked as a top-level variable since it's set in the MouseMove
        /// event and must be read in the Paint event.
        /// </summary>
        private int finalMinDrawDistance = 0;

        private bool isFormClosing = false;

        /// <summary>
        /// True when the user sets an effect like blur, and is currently modifying the settings.
        /// </summary>
        private (bool settingsOpen, bool hoverPreview) isPreviewingEffect = (false, false);

        /// <summary>
        /// A performance optimization when using a merge layer. It's easier to add rectangles to
        /// this list and merge them down only once, vs. merging the entire viewport or image.
        /// </summary>
        private readonly List<Rectangle> mergeRegions = new List<Rectangle>();

        /// <summary>
        /// Indicates whether the plugin has finished loading basic elements. This includes the form component init,
        /// the token initialization by paint.net, the canvas/bitmap elements and the default brush being set, as well
        /// as the chosen effect being applied (if any was active from before). It does not guarantee that all brushes
        /// have finished loading, both default and custom ones.
        /// </summary>
        private bool pluginHasLoaded = false;

        private readonly Random random = new Random();

        /// <summary>
        /// The outline of the user's selection.
        /// </summary>
        private PdnRegion selectionOutline;

        /// <summary>
        /// The location to draw symmetry on the transformed canvas (transformations are already applied to this point).
        /// </summary>
        private PointF symmetryOrigin = PointF.Empty;

        /// <summary>
        /// The points relative to the cursor used in multi-point symmetry mode.
        /// </summary>
        private readonly List<PointF> symmetryOrigins;
        #endregion

        #region Fields (Gui)
        private readonly Font boldFont = null;
        private readonly Font detailsFont = null;
        private readonly Font tooltipFont = null;

        /// <summary>
        /// Loads user's custom brush images asynchronously.
        /// </summary>
        private BackgroundWorker brushImageLoadingWorker;

        /// <summary>
        /// Stores the disposable data for the color picker cursor.
        /// </summary>
        private Cursor cursorColorPicker;

        /// <summary>
        /// An empty image list that allows the brush thumbnail size to be changed.
        /// </summary>
        private ImageList dummyImageList;

        /// <summary>
        /// A list of all visible items in the brush selector for thumbnails.
        /// </summary>
        private ListViewItem[] visibleBrushImages;

        /// <summary>
        /// The starting index in the brush selector cache.
        /// </summary>
        private int visibleBrushImagesIndex;

        private ThemedButton bttnCancel;
        private ThemedButton bttnDone;

        private IContainer components;
        internal PictureBox displayCanvas;

        private FlowLayoutPanel topMenu;
        private ThemedButton menuOptions;
        private ThemedButton menuRedo;
        private ThemedButton menuUndo;
        private ToolStripMenuItem menuSetTheme, menuSetThemeDefault, menuSetThemeLight, menuSetThemeDark;
        private ToolStripMenuItem menuResetCanvas, menuSetCanvasBackground, menuDisplaySettings;
        private ToolStripMenuItem menuSetCanvasBgImage, menuSetCanvasBgImageFit, menuSetCanvasBgImageOnlyIfFits;
        private ToolStripMenuItem menuSetCanvasBgTransparent, menuSetCanvasBgGray, menuSetCanvasBgWhite, menuSetCanvasBgBlack;
        private ToolStripMenuItem menuBrushIndicator, menuBrushIndicatorSquare, menuBrushIndicatorPreview;
        private ToolStripMenuItem menuShowSymmetryLinesInUse, menuShowMinDistanceInUse;
        private ToolStripMenuItem menuBrushImageDirectories, menuKeyboardShortcutsDialog;
        private ToolStripMenuItem menuColorPickerIncludesAlpha, menuColorPickerSwitchesToPrevTool;
        private ToolStripMenuItem menuRemoveUnfoundImagePaths, menuConfirmCloseSave;
        private ThemedButton menuCanvasZoomBttn, menuCanvasAngleBttn;
        private Slider sliderCanvasZoom, sliderCanvasAngle;
        private ToolStripMenuItem menuCanvasZoomReset, menuCanvasZoomFit, menuCanvasZoomTo;
        private ToolStripMenuItem menuCanvasAngleReset, menuCanvasAngle90, menuCanvasAngle180, menuCanvasAngle270, menuCanvasAngleTo;
        private SwatchBox menuActiveColors, menuPalette;
        private ThemedComboBox cmbxPaletteDropdown;
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
        private ThemedCheckbox bttnColorPicker;
        private Panel panelAllSettingsContainer;
        private ThemedPanel panelDockSettingsContainer;
        private ThemedCheckbox bttnToolBrush;
        private ThemedCheckbox bttnToolEraser;
        private ThemedCheckbox bttnToolOrigin;
        private ThemedCheckbox bttnToolCloneStamp;
        private ThemedCheckbox bttnToolLine;
        private FlowLayoutPanel panelSettingsContainer;
        private Accordion bttnBrushControls;
        private FlowLayoutPanel panelBrush;
        private DoubleBufferedListView listviewBrushImagePicker;
        private Panel panelBrushAddPickColor;
        private ThemedCheckbox chkbxColorizeBrush;
        private ThemedButton bttnAddBrushImages;
        private ProgressBar brushImageLoadProgressBar;
        private Slider sliderColorInfluence;
        private FlowLayoutPanel panelColorInfluenceHSV;
        private ThemedCheckbox chkbxColorInfluenceHue;
        private ThemedCheckbox chkbxColorInfluenceSat;
        private ThemedCheckbox chkbxColorInfluenceVal;
        private ThemedButton bttnSetScript;
        private Panel panelChosenEffect;
        private ThemedComboBox cmbxChosenEffect;
        private ThemedButton bttnChooseEffectSettings;
        private ThemedComboBox cmbxBlendMode;
        private Slider sliderBrushOpacity;
        private Slider sliderBrushFlow;
        private Slider sliderBrushRotation;
        private Slider sliderBrushSize;
        private Accordion bttnColorControls;
        private FlowLayoutPanel panelColorControls;
        private FlowLayoutPanel panelWheelWithValueSlider;
        private ColorWheel wheelColor;
        private Slider sliderColorValue;
        private FlowLayoutPanel panelColorWithHexBox;
        private SwatchBox swatchPrimaryColor;
        private ColorTextbox txtbxColorHexfield;
        private Accordion bttnSpecialSettings;
        private FlowLayoutPanel panelSpecialSettings;
        private Slider sliderMinDrawDistance;
        private Slider sliderBrushDensity;
        private ThemedCheckbox chkbxAutomaticBrushDensity;
        private ThemedComboBox cmbxSymmetry;
        private ThemedComboBox cmbxBrushSmoothing;
        private ThemedCheckbox chkbxOrientToMouse;
        private ThemedCheckbox chkbxSeamlessDrawing;
        private ThemedCheckbox chkbxDitherDraw;
        private ThemedCheckbox chkbxLockAlpha;
        private Panel panelRGBLocks;
        private ThemedCheckbox chkbxLockR;
        private ThemedCheckbox chkbxLockG;
        private ThemedCheckbox chkbxLockB;
        private Panel panelHSVLocks;
        private ThemedCheckbox chkbxLockHue;
        private ThemedCheckbox chkbxLockSat;
        private ThemedCheckbox chkbxLockVal;
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
        private Accordion bttnSettings;
        private FlowLayoutPanel panelSettings;
        private ThemedButton bttnUpdateCurrentBrush;
        private ThemedButton bttnClearSettings;
        private ListView listviewBrushPicker;
        private ThemedButton bttnSaveBrush;
        private ThemedButton bttnDeleteBrush;
        private Label txtTooltip;
        private readonly Dictionary<CommandTarget, (Slider, CmbxTabletValueType)> pressureConstraintControls;
        #endregion

        #region Constructors
        /// <summary>
        /// Initializes components and brushes.
        /// </summary>
        public WinDynamicDraw()
        {
            boldFont = new Font("Microsoft Sans Serif", 12f, FontStyle.Bold);
            detailsFont = new Font("Microsoft Sans Serif", 8.25f);
            tooltipFont = new Font("Microsoft Sans Serif", 10);
            tabPressureConstraints = new Dictionary<CommandTarget, BrushSettingConstraint>();
            pressureConstraintControls = new Dictionary<CommandTarget, (Slider, CmbxTabletValueType)>();

            SetupGUI();

            // The temp directory is used to store undo/redo images.
            TempDirectory.CleanupPreviousDirectories();
            tempDir = new TempDirectory();

            loadedBrushImages = new BrushSelectorItemCollection();
            KeyboardShortcuts = new HashSet<Command>();
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
            paletteOptions = new BindingList<Tuple<string, PaletteComboboxOptions>>()
            {
                new Tuple<string, PaletteComboboxOptions>(Strings.Current, new PaletteComboboxOptions(PaletteSpecialType.Current)),
                new Tuple<string, PaletteComboboxOptions>(Strings.ColorSchemeRecent, new PaletteComboboxOptions(PaletteSpecialType.Recent)),
                new Tuple<string, PaletteComboboxOptions>(Strings.ColorSchemeGradient, new PaletteComboboxOptions(PaletteSpecialType.PrimaryToSecondary)),
                new Tuple<string, PaletteComboboxOptions>(Strings.ColorSchemeMonochromatic, new PaletteComboboxOptions(PaletteSpecialType.LightToDark)),
                new Tuple<string, PaletteComboboxOptions>(Strings.ColorSchemeAnalogous3, new PaletteComboboxOptions(PaletteSpecialType.Similar3)),
                new Tuple<string, PaletteComboboxOptions>(Strings.ColorSchemeAnalogous4, new PaletteComboboxOptions(PaletteSpecialType.Similar4)),
                new Tuple<string, PaletteComboboxOptions>(Strings.ColorSchemeComplementary, new PaletteComboboxOptions(PaletteSpecialType.Complement)),
                new Tuple<string, PaletteComboboxOptions>(Strings.ColorSchemeSplitComplementary, new PaletteComboboxOptions(PaletteSpecialType.SplitComplement)),
                new Tuple<string, PaletteComboboxOptions>(Strings.ColorSchemeTriadic, new PaletteComboboxOptions(PaletteSpecialType.Triadic)),
                new Tuple<string, PaletteComboboxOptions>(Strings.ColorSchemeSquare, new PaletteComboboxOptions(PaletteSpecialType.Square))
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

                boldFont?.Dispose();
                detailsFont?.Dispose();
                tooltipFont?.Dispose();

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
            InitKeyboardShortcuts(token.CustomShortcuts);

            token.CurrentBrushSettings.BrushColor = PdnUserSettings.userPrimaryColor.ToArgb();
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
            menuActiveColors.Swatches[1] = PdnUserSettings.userSecondaryColor;

            UpdateBrush(token.CurrentBrushSettings);
            UpdateBrushImage(false);
        }

        /// <summary>
        /// Overwrites the settings with the dialog's current settings so they
        /// can be reused later; i.e. this saves the settings.
        /// </summary>
        protected override void InitTokenFromDialog()
        {
            PersistentSettings token = (PersistentSettings)EffectToken;
            token.UserSettings = UserSettings;
            token.CustomShortcuts = KeyboardShortcuts;
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
                        ThemedMessageBox.Show(Strings.CannotSaveSettingsError, Text, MessageBoxButtons.OK);
                        currentKeysPressed.Clear(); // modal dialogs leave key-reading in odd states. Clears it.
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
            bttnSetScript.Text = Strings.SetScript;
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

            // Apply line with line tool, or click done/cancel
            if (lineOrigins.points.Count >= 2 && lineOrigins.points.Count == 2 &&
                (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter))
            {
                MergeStaged();
                lineOrigins.points.Clear();
                lineOrigins.dragIndex = null;
                contexts.Remove(CommandContext.LineToolConfirmStage);
                contexts.Add(CommandContext.LineToolUnstartedStage);
                displayCanvas.Refresh();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                BttnCancel_Click(null, null);
            }
            else if (e.KeyCode == Keys.Enter)
            {
                BttnDone_Click(null, null);
            }

            // Fires any shortcuts that don't require the mouse wheel.
            if (displayCanvas.Focused)
            {
                contexts.Add(CommandContext.OnCanvas);
                contexts.Remove(CommandContext.OnSidebar);
            }
            else
            {
                contexts.Remove(CommandContext.OnCanvas);
                contexts.Add(CommandContext.OnSidebar);
            }

            CommandManager.FireShortcuts(KeyboardShortcuts, currentKeysPressed, false, false, contexts);

            // Display a hand icon while panning.
            if (!isUserDrawing.started && e.KeyCode == Keys.Space)
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
            if (displayCanvas.Focused)
            {
                contexts.Add(CommandContext.OnCanvas);
                contexts.Remove(CommandContext.OnSidebar);
            }
            else
            {
                contexts.Remove(CommandContext.OnCanvas);
                contexts.Add(CommandContext.OnSidebar);
            }

            bool wheelDirectionUp = Math.Sign(e.Delta) > 0;

            CommandManager.FireShortcuts(
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
            // Removes all but the special palettes, assuming they're listed first. -1 to account for type None.
            int defaultPaletteCount = Enum.GetNames(typeof(PaletteSpecialType)).Length - 1;
            while (paletteOptions.Count > defaultPaletteCount)
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
                        paletteOptions.Add(new Tuple<string, PaletteComboboxOptions>(
                            Path.GetFileNameWithoutExtension(entry), new PaletteComboboxOptions(entry)));
                    }
                    else if (Directory.Exists(entry))
                    {
                        string[] files = Directory.GetFiles(entry);
                        foreach (string file in files)
                        {
                            if (Path.GetExtension(file).ToLower() == ".txt")
                            {
                                paletteOptions.Add(new Tuple<string, PaletteComboboxOptions>(
                                    Path.GetFileNameWithoutExtension(file), new PaletteComboboxOptions(file)));
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
                            Color? result = ColorUtils.GetColorFromText(line, true);
                            if (result != null)
                            {
                                paletteColors.Add(result.Value);

                                if (paletteColors.Count == ColorUtils.MaxPaletteSize)
                                {
                                    break;
                                }
                            }
                        }

                        paletteSelectedSwatchIndex = -1;
                        menuPalette.Swatches = paletteColors;
                        UpdatePaletteSize();
                    }
                }
                catch
                {
                    ThemedMessageBox.Show(Strings.LoadPaletteError);
                    currentKeysPressed.Clear(); // modal dialogs leave key-reading in odd states. Clears it.
                }
            }
        }

        /// <summary>
        /// Deals with palette generation, including reading the current palette from Paint.NET or switching to
        /// recent colors.
        /// </summary>
        private void GeneratePalette(PaletteSpecialType type)
        {
            // Loads the default palette for empty paths. It will fail silently if the plugin hasn't loaded.
            if (type == PaletteSpecialType.Current)
            {
                IPalettesService palettesService = (IPalettesService)Services?.GetService(typeof(IPalettesService));

                if (palettesService != null)
                {
                    List<Color> paletteColors = new List<Color>();
                    for (int i = 0; i < palettesService.CurrentPalette.Count && i < ColorUtils.MaxPaletteSize; i++)
                    {
                        paletteColors.Add((Color)palettesService.CurrentPalette[i]);
                    }

                    paletteSelectedSwatchIndex = -1;
                    menuPalette.Swatches = paletteColors;
                }
                else if (pluginHasLoaded)
                {
                    ThemedMessageBox.Show(Strings.LoadPaletteError);
                    currentKeysPressed.Clear(); // modal dialogs leave key-reading in odd states. Clears it.
                }
            }
            else if (type == PaletteSpecialType.Recent)
            {
                paletteSelectedSwatchIndex = -1;
                menuPalette.Swatches = new List<Color>(paletteRecent);
            }
            else
            {
                paletteGeneratedMaxColors = type switch
                {
                    PaletteSpecialType.Recent => paletteRecentMaxColors,
                    PaletteSpecialType.Similar3 or PaletteSpecialType.Triadic => 30,
                    _ => 20,
                };

                paletteSelectedSwatchIndex = type switch
                {
                    PaletteSpecialType.PrimaryToSecondary => 0,
                    PaletteSpecialType.LightToDark => 10,
                    PaletteSpecialType.Similar3 => 15,
                    PaletteSpecialType.Complement or PaletteSpecialType.Triadic => 5,
                    PaletteSpecialType.SplitComplement or PaletteSpecialType.Square => 2,
                    _ => -1
                };

                menuPalette.Swatches = ColorUtils.GeneratePalette(
                    type,
                    paletteGeneratedMaxColors,
                    menuActiveColors.Swatches[0],
                    menuActiveColors.Swatches[1]);
            }

            UpdatePaletteSize();
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
                    ThemedMessageBox.Show(Strings.SettingsUnavailableError, Text, MessageBoxButtons.OK);
                    currentKeysPressed.Clear(); // modal dialogs leave key-reading in odd states. Clears it.
                }
                else if (ex is IOException || ex is UnauthorizedAccessException)
                {
                    ThemedMessageBox.Show(Strings.CannotLoadSettingsError, Text, MessageBoxButtons.OK);
                    currentKeysPressed.Clear(); // modal dialogs leave key-reading in odd states. Clears it.
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
                ThemedMessageBox.Show(Strings.EffectFailedToWorkError);
                currentKeysPressed.Clear(); // modal dialogs leave key-reading in odd states. Clears it.
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
                ThemedMessageBox.Show(Strings.EffectFailedToWorkError);
                currentKeysPressed.Clear(); // modal dialogs leave key-reading in odd states. Clears it.
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

            // Applies the restored effect immediately under current settings without previewing.
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
                    ThemedMessageBox.Show(Strings.EffectFailedToWorkError);
                    currentKeysPressed.Clear(); // modal dialogs leave key-reading in odd states. Clears it.
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
                        ThemedMessageBox.Show(Strings.EffectFailedToWorkError);
                        currentKeysPressed.Clear(); // modal dialogs leave key-reading in odd states. Clears it.
                        dialog.Close();
                    }

                    displayCanvas.Refresh();
                };

                isPreviewingEffect.settingsOpen = true;

                dialog.Owner = this;
                var dlgResult = dialog.ShowDialog();
                currentKeysPressed.Clear(); // avoids issues with key interception from the dialog.

                if (repaintTimer.Enabled)
                {
                    repaintTimer.Stop();
                    bool effectSuccessfullyExecuted = ActiveEffectRender(dialog.EffectToken);
                    if (!effectSuccessfullyExecuted)
                    {
                        ThemedMessageBox.Show(Strings.EffectFailedToWorkError);
                        currentKeysPressed.Clear(); // modal dialogs leave key-reading in odd states. Clears it.
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
                ThemedMessageBox.Show(Strings.EffectFailedToWorkError);
                currentKeysPressed.Clear(); // modal dialogs leave key-reading in odd states. Clears it.
            }
        }

        /// <summary>
        /// Executes all scripts of the current brush, if any, that are triggered by the given trigger.
        /// </summary>
        /// <param name="trigger">The trigger for which the scripts should execute.</param>
        private void BrushScriptsExecute(ScriptTrigger trigger)
        {
            if (currentBrushScripts == null ||
                currentBrushScripts.Scripts == null ||
                currentBrushScripts.Scripts.Count == 0)
            {
                return;
            }

            int scriptNum = 0;

            try
            {
                for (; scriptNum < currentBrushScripts.Scripts.Count; scriptNum++)
                {
                    if (currentBrushScripts.Scripts[scriptNum].Trigger == trigger &&
                        currentBrushScripts.Scripts[scriptNum].Action != "")
                    {
                        scriptEnv.DoString(
                            currentBrushScripts.Scripts[scriptNum].Action);
                    }
                }
            }
            catch (Exception e)
            {
                var script = currentBrushScripts.Scripts[scriptNum];

                if (e is MoonSharp.Interpreter.SyntaxErrorException ||
                    e is MoonSharp.Interpreter.ScriptRuntimeException ||
                    e is MoonSharp.Interpreter.DynamicExpressionException ||
                    e is MoonSharp.Interpreter.InternalErrorException)
                {
                    ThemedMessageBox.Show(
                        string.Format(
                            Strings.BrushScriptError,
                            string.IsNullOrWhiteSpace(script.Name) ? $"#{scriptNum + 1}" : $"\"{script.Name}\"",
                            e.Message),
                        Text, MessageBoxButtons.OK);
                    currentKeysPressed.Clear(); // modal dialogs leave key-reading in odd states. Clears it.
                }
                else
                {
                    ThemedMessageBox.Show(
                        string.Format(
                            Strings.BrushScriptGenericError,
                            string.IsNullOrWhiteSpace(script.Name) ? $"#{scriptNum + 1}" : $"\"{script.Name}\""),
                        Text, MessageBoxButtons.OK);
                    currentKeysPressed.Clear(); // modal dialogs leave key-reading in odd states. Clears it.
                }

                // Temp-disables all scripts (until this is repopulated by e.g. reselecting the brush, restarting the
                // plugin, etc.) because scripts may depend on each other and lead to dangerous unexpected behavior if
                // partially disabled.
                currentBrushScripts.Scripts.Clear();
            }
        }

        /// <summary>
        /// If the current brush supports scripting, this sets the values of many special variables for its scripts.
        /// If <paramref name="clearCustomVariables"/> is true, all user-defined variables are also wiped, which
        /// should be done only when a different scripted brush is selected because it's valuable to allow users to
        /// create persisting variables in their scripts (but it's bad to allow variables set by other brushes to affect
        /// each other).
        /// </summary>
        /// <param name="clearCustomVariables">If true, removes all user-set variables.</param>
        private void BrushScriptsPrepare(bool clearCustomVariables)
        {
            if (currentBrushScripts == null ||
                currentBrushScripts.Scripts == null ||
                currentBrushScripts.Scripts.Count == 0)
            {
                return;
            }

            if (scriptEnv == null)
            {
                scriptEnv = new MoonSharp.Interpreter.Script(MoonSharp.Interpreter.CoreModules.Preset_SoftSandbox);
                MoonSharp.Interpreter.Script.WarmUp();
            }

            if (clearCustomVariables)
            {
                scriptEnv.Globals.Clear();
                scriptEnv.Globals["get"] = (Func<string, MoonSharp.Interpreter.DynValue>)BrushScriptsGetValue;
                scriptEnv.Globals["set"] = (Action<string, MoonSharp.Interpreter.DynValue>)BrushScriptsSetValue;
            }
        }

        /// <summary>
        /// Takes 1 token, a reserved brush script API identifier for a variable name, and attempts to resolve it.
        /// Returns the resolved value as a literal token, or a variable token with a null value on failure.
        /// </summary>
        private MoonSharp.Interpreter.DynValue BrushScriptsGetValue(string specialVariableName)
        {
            CommandTarget resolvedTarget = Script.ResolveBuiltInToCommandTarget(specialVariableName, true);

            // Returns the value of special variables from Dynamic Draw.
            if (resolvedTarget != CommandTarget.None)
            {
                string resultStr = GetTargetValue(resolvedTarget);
                switch (CommandTargetInfo.All[resolvedTarget].ValueType)
                {
                    case CommandActionDataType.Action:
                        return null;
                    case CommandActionDataType.Bool:
                        return MoonSharp.Interpreter.DynValue.NewBoolean(bool.Parse(resultStr));
                    case CommandActionDataType.Float:
                        return MoonSharp.Interpreter.DynValue.NewNumber(float.Parse(resultStr));
                    case CommandActionDataType.Integer:
                        return MoonSharp.Interpreter.DynValue.NewNumber(int.Parse(resultStr));
                    case CommandActionDataType.Color:
                    case CommandActionDataType.String:
                        return MoonSharp.Interpreter.DynValue.NewString(resultStr);
                }
            }

            return MoonSharp.Interpreter.DynValue.NewNil();
        }

        /// <summary>
        /// Takes 1 or 2 tokens and attempts to create a command with a valid target, defaulting to
        /// <see cref="CommandTarget.None"/>. Token 1 should be a reserved brush script API identifier for the target,
        /// and token 2 should be an optional string of action data for it. Returns a token equal to 0.
        /// </summary>
        private void BrushScriptsSetValue(string specialVariableName, MoonSharp.Interpreter.DynValue result)
        {
            CommandTarget resolvedTarget = Script.ResolveBuiltInToCommandTarget(specialVariableName);

            // Allows setting arbitrary variables with primitive-type values.
            if (resolvedTarget != CommandTarget.None)
            {
                HandleShortcut(new Command()
                {
                    Target = resolvedTarget,
                    ActionData = result.ToPrintString()
                });
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
                BrushColor = menuActiveColors.Swatches[0].ToArgb(),
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
                BrushScripts = new(currentBrushScripts),
                SizeChange = sliderShiftSize.ValueInt,
                Smoothing = (CmbxSmoothing.Smoothing)cmbxBrushSmoothing.SelectedIndex,
                Symmetry = (SymmetryMode)cmbxSymmetry.SelectedIndex,
                CmbxChosenEffect = cmbxChosenEffect.SelectedIndex,
                TabPressureConstraints = new(tabPressureConstraints),
            };

            return newSettings;
        }

        /// <summary>
        /// Draws the brush in a line between two points using its current density settings. If the brush has a brush
        /// density of 0 (unbounded), it will draw as if it has a brush density of 1.
        /// </summary>
        private void DrawBrushLine(PointF loc1, PointF loc2)
        {
            finalMinDrawDistance = GetPressureValue(CommandTarget.MinDrawDistance, sliderMinDrawDistance.ValueInt, tabletPressureRatio);

            int density = Math.Max(sliderBrushDensity.ValueInt, 1);
            int finalBrushDensity = GetPressureValue(CommandTarget.BrushStrokeDensity, density, tabletPressureRatio);
            int finalBrushSize = GetPressureValue(CommandTarget.Size, sliderBrushSize.ValueInt, tabletPressureRatio);

            double deltaX = loc2.X - loc1.X;
            double deltaY = loc2.Y - loc1.Y;
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
                if (tabletPressureRatioPrev != tabletPressureRatio &&
                    tabPressureConstraints.ContainsKey(CommandTarget.Size) &&
                    tabPressureConstraints[CommandTarget.Size].value != 0)
                {
                    tabletPressure = (float)(tabletPressureRatioPrev + i / numIntervals * (tabletPressureRatio - tabletPressureRatioPrev));
                    finalBrushSize = GetPressureValue(CommandTarget.Size, sliderBrushSize.ValueInt, tabletPressure);
                }

                DrawBrush(new PointF(
                    (float)(loc1.X + xDist * brushWidthFrac * i - halfPixelOffset),
                    (float)(loc1.Y + yDist * brushWidthFrac * i - halfPixelOffset)),
                    finalBrushSize, tabletPressure);
            }
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
                if (menuRedo.Enabled)
                {
                    menuRedo.Enabled = false;
                }

                //Removes all redo history.
                redoHistory.Clear();

                // Scripted brushes execute their pre-brush stamp logic now.
                BrushScriptsExecute(ScriptTrigger.OnBrushStroke);
            }

            // Scripted brushes execute their pre-brush stamp logic now.
            BrushScriptsExecute(ScriptTrigger.OnStartBrushStamp);

            #region apply size jitter
            // Change the brush size based on settings.
            int finalRandMinSize = GetPressureValue(CommandTarget.JitterMinSize, sliderRandMinSize.ValueInt, pressure);
            int finalRandMaxSize = GetPressureValue(CommandTarget.JitterMaxSize, sliderRandMaxSize.ValueInt, pressure);

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
            else if (pressure > 0 &&
                tabPressureConstraints.ContainsKey(CommandTarget.Flow) &&
                tabPressureConstraints[CommandTarget.Flow].handleMethod != ConstraintValueHandlingMethod.DoNothing)
            {
                // If not changing sliderBrushFlow already by shifting it in the if-statement above, the brush has to
                // be manually redrawn when modifying brush flow. This is done to avoid editing sliderBrushFlow and
                // having to use an extra variable to mitigate the cumulative effect it would cause.
                UpdateBrushImage(false);
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
            int finalRandHorzShift = GetPressureValue(CommandTarget.JitterHorSpray, sliderRandHorzShift.ValueInt, pressure);
            int finalRandVertShift = GetPressureValue(CommandTarget.JitterVerSpray, sliderRandVertShift.ValueInt, pressure);

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
            int finalBrushRotation = GetPressureValue(CommandTarget.Rotation, sliderBrushRotation.ValueInt, pressure);
            int finalRandRotLeft = GetPressureValue(CommandTarget.JitterRotLeft, sliderRandRotLeft.ValueInt, pressure);
            int finalRandRotRight = GetPressureValue(CommandTarget.JitterRotRight, sliderRandRotRight.ValueInt, pressure);

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
            int finalRandFlowLoss = GetPressureValue(CommandTarget.JitterFlowLoss, sliderRandFlowLoss.ValueInt, pressure);
            #endregion

            #region apply color jitter
            ImageAttributes recolorMatrix = null;
            ColorBgra adjustedColor = menuActiveColors.Swatches[0];
            int newFlowLoss = random.Next(finalRandFlowLoss);
            adjustedColor.A = (byte)Math.Round(Math.Clamp(sliderBrushFlow.Value - newFlowLoss, 0f, 255f));

            if (activeTool == Tool.Eraser || activeTool == Tool.CloneStamp || effectToDraw.Effect != null)
            {
                if (newFlowLoss != 0)
                {
                    recolorMatrix = DrawingUtils.ColorImageAttr(1, 1, 1, (255 - newFlowLoss) / 255f);
                }
            }
            else if (chkbxColorizeBrush.Checked || sliderColorInfluence.Value != 0 || cmbxBlendMode.SelectedIndex == (int)BlendMode.Overwrite)
            {
                int finalJitterMaxRed = GetPressureValue(CommandTarget.JitterRedMax, sliderJitterMaxRed.ValueInt, pressure);
                int finalJitterMinRed = GetPressureValue(CommandTarget.JitterRedMin, sliderJitterMinRed.ValueInt, pressure);
                int finalJitterMaxGreen = GetPressureValue(CommandTarget.JitterGreenMax, sliderJitterMaxGreen.ValueInt, pressure);
                int finalJitterMinGreen = GetPressureValue(CommandTarget.JitterGreenMin, sliderJitterMinGreen.ValueInt, pressure);
                int finalJitterMaxBlue = GetPressureValue(CommandTarget.JitterBlueMax, sliderJitterMaxBlue.ValueInt, pressure);
                int finalJitterMinBlue = GetPressureValue(CommandTarget.JitterBlueMin, sliderJitterMinBlue.ValueInt, pressure);
                int finalJitterMaxHue = GetPressureValue(CommandTarget.JitterHueMax, sliderJitterMaxHue.ValueInt, pressure);
                int finalJitterMinHue = GetPressureValue(CommandTarget.JitterHueMin, sliderJitterMinHue.ValueInt, pressure);
                int finalJitterMaxSat = GetPressureValue(CommandTarget.JitterSatMax, sliderJitterMaxSat.ValueInt, pressure);
                int finalJitterMinSat = GetPressureValue(CommandTarget.JitterSatMin, sliderJitterMinSat.ValueInt, pressure);
                int finalJitterMaxVal = GetPressureValue(CommandTarget.JitterValMax, sliderJitterMaxVal.ValueInt, pressure);
                int finalJitterMinVal = GetPressureValue(CommandTarget.JitterValMin, sliderJitterMinVal.ValueInt, pressure);

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
                    float newAlpha = (
                        activeTool == Tool.Eraser
                        || activeTool == Tool.CloneStamp
                        || effectToDraw.Effect != null
                        || cmbxBlendMode.SelectedIndex != (int)BlendMode.Overwrite)
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

            byte finalOpacity = (byte)GetPressureValue(CommandTarget.BrushOpacity, sliderBrushOpacity.ValueInt, pressure);

            // The staged bitmap is only needed for layer opacity and layer blend modes, because the brush stroke
            // opacity becomes important in calculating separately from the regular drawing. The staged bitmap will
            // be drawn the whole time and when done with the brush stroke, so it's tracked to know when committing to
            // it is necessary, to save speed. Note that the eraser tool always erases on the committed bitmap.
            bool drawToStagedBitmap = activeTool == Tool.Line ||
                (activeTool != Tool.Eraser
                && activeTool != Tool.CloneStamp
                && effectToDraw.Effect == null
                && ((BlendMode)cmbxBlendMode.SelectedIndex != BlendMode.Overwrite)
                && (isUserDrawing.stagedChanged
                    || BlendModeUtils.BlendModeToUserBlendOp((BlendMode)cmbxBlendMode.SelectedIndex) != null
                    || finalOpacity != sliderBrushOpacity.Maximum));

            if (drawToStagedBitmap && !isUserDrawing.stagedChanged)
            {
                isUserDrawing.stagedChanged = true;
                DrawingUtils.OverwriteBits(bmpCommitted, bmpMerged);
            }

            Bitmap bmpToDrawOn = drawToStagedBitmap ? bmpStaged : bmpCommitted;

            // Sets the source bitmap/surface for masked drawing based on the tool.
            (Surface surface, Bitmap bmp) maskedDrawSource = (null, null);
            if (activeTool == Tool.Eraser)
            {
                maskedDrawSource.surface = EnvironmentParameters.SourceSurface;
            }
            else if (activeTool == Tool.CloneStamp || activeTool == Tool.Line || effectToDraw.Effect != null)
            {
                maskedDrawSource.bmp = bmpStaged;
            }

            // Draws the brush.
            using (Graphics g = Graphics.FromImage(bmpToDrawOn))
            {
                // Lockbits is needed for overwrite blend mode, channel locks, and seamless drawing.
                bool useLockbitsDrawing = activeTool == Tool.Eraser
                    || activeTool == Tool.CloneStamp
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
                if (activeTool != Tool.Eraser && activeTool != Tool.CloneStamp && effectToDraw.Effect == null)
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

                // Scripted brushes execute their post-brush stamp logic now.
                BrushScriptsExecute(ScriptTrigger.OnEndBrushStamp);

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
                                if (activeTool == Tool.Eraser || activeTool == Tool.CloneStamp || effectToDraw.Effect != null)
                                {
                                    Point destPt = new Point(
                                        (int)Math.Round(rotatedLoc.X - (scaleFactor / 2f)),
                                        (int)Math.Round(rotatedLoc.Y - (scaleFactor / 2f)));

                                    Point originPt = (activeTool == Tool.CloneStamp && cloneStampOrigin != null)
                                        ? new Point(
                                            destPt.X + (int)cloneStampOrigin.Value.X,
                                            destPt.Y + (int)cloneStampOrigin.Value.Y)
                                        : destPt;

                                    DrawingUtils.OverwriteMasked(
                                        maskedDrawSource,
                                        bmpCommitted,
                                        bmpBrushRotScaled,
                                        originPt,
                                        destPt,
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
                                if (activeTool == Tool.Eraser || activeTool == Tool.CloneStamp || effectToDraw.Effect != null)
                                {
                                    Point destPt = new Point(
                                        (int)Math.Round(origin.X - halfScaleFactor + (symmetryX ? xDist : -xDist)),
                                        (int)Math.Round(origin.Y - halfScaleFactor + (symmetryY ? yDist : -yDist)));

                                    Point originPt = (activeTool == Tool.CloneStamp && cloneStampOrigin != null)
                                        ? new Point(
                                            destPt.X + (int)cloneStampOrigin.Value.X,
                                            destPt.Y + (int)cloneStampOrigin.Value.Y)
                                        : destPt;

                                    DrawingUtils.OverwriteMasked(
                                        maskedDrawSource,
                                        bmpCommitted,
                                        bmpBrushRotScaled,
                                        originPt,
                                        destPt,
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

                                    if (activeTool == Tool.Eraser || activeTool == Tool.CloneStamp || effectToDraw.Effect != null)
                                    {
                                        Point destPt = new Point(
                                            (int)Math.Round(transformedPoint.X - halfScaleFactor),
                                            (int)Math.Round(transformedPoint.Y - halfScaleFactor));

                                        Point originPt = (activeTool == Tool.CloneStamp && cloneStampOrigin != null)
                                            ? new Point(
                                                destPt.X + (int)cloneStampOrigin.Value.X,
                                                destPt.Y + (int)cloneStampOrigin.Value.Y)
                                            : destPt;

                                        DrawingUtils.OverwriteMasked(
                                            maskedDrawSource,
                                            bmpCommitted,
                                            bmpBrushRotScaled,
                                            originPt,
                                            destPt,
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
                                    if (activeTool == Tool.Eraser || activeTool == Tool.CloneStamp || effectToDraw.Effect != null)
                                    {
                                        Point destPt = new Point(
                                            (int)Math.Round(origin.X - (scaleFactor / 2f) + (float)(dist * Math.Cos(angle))),
                                            (int)Math.Round(origin.Y - (scaleFactor / 2f) + (float)(dist * Math.Sin(angle))));

                                        Point originPt = (activeTool == Tool.CloneStamp && cloneStampOrigin != null)
                                            ? new Point(
                                                destPt.X + (int)cloneStampOrigin.Value.X,
                                                destPt.Y + (int)cloneStampOrigin.Value.Y)
                                            : destPt;

                                        DrawingUtils.OverwriteMasked(
                                            maskedDrawSource,
                                            bmpCommitted,
                                            bmpBrushRotScaled,
                                            originPt,
                                            destPt,
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
        /// Creates a named slider with a combobox for the given shortcut, which auto-updates the pressure sensitivity
        /// values listed for it.
        /// </summary>
        private FlowLayoutPanel GeneratePressureControl(CommandTarget target, int min = 0)
        {
            FlowLayoutPanel panel = new FlowLayoutPanel();
            panel.FlowDirection = FlowDirection.TopDown;
            panel.AutoSize = true;

            Slider valueSlider = new Slider(target, min);
            valueSlider.IntegerOnly = true;
            valueSlider.Margin = Padding.Empty;
            valueSlider.Width = 150;
            valueSlider.ValueChanged += (sender, val) =>
            {
                if (!tabPressureConstraints.ContainsKey(target))
                {
                    tabPressureConstraints.Add(target, new BrushSettingConstraint(ConstraintValueHandlingMethod.DoNothing, valueSlider.ValueInt));
                }
                else
                {
                    tabPressureConstraints[target] = new BrushSettingConstraint(tabPressureConstraints[target].handleMethod, valueSlider.ValueInt);
                }
            };
            valueSlider.ComputeText = (val) => $"{CommandTargetInfo.All[target].Name}: {val}";
            if (target == CommandTarget.Size)
            {
                valueSlider.LostFocus += SliderTabPressureBrushSize_LostFocus;
            }

            CmbxTabletValueType cmbxValueType = new CmbxTabletValueType();
            cmbxValueType.Font = detailsFont;
            cmbxValueType.Margin = new Padding(0, 0, 0, 3);
            cmbxValueType.Width = 150;
            cmbxValueType.MouseHover += CmbxTabPressure_MouseHover;
            cmbxValueType.MouseWheel += IgnoreMouseWheelEvent;
            cmbxValueType.SelectedIndexChanged += (a, b) =>
            {
                if (cmbxValueType.SelectedIndex != -1)
                {
                    var handlingMethod = ((CmbxTabletValueType.CmbxEntry)cmbxValueType.SelectedItem).ValueMember;
                    var min = CommandTargetInfo.All[target].MinMaxRangeF?.Item1 ?? CommandTargetInfo.All[target].MinMaxRange.Item1;
                    var max = CommandTargetInfo.All[target].MinMaxRangeF?.Item2 ?? CommandTargetInfo.All[target].MinMaxRange.Item2;

                    valueSlider.Enabled = (handlingMethod != ConstraintValueHandlingMethod.DoNothing);

                    switch (handlingMethod)
                    {
                        case ConstraintValueHandlingMethod.Add:
                            valueSlider.SetNumericStops(new float[] { -max, max });
                            valueSlider.Refresh();
                            break;
                        case ConstraintValueHandlingMethod.AddPercent:
                            valueSlider.SetNumericStops(new float[] { -100, 100 });
                            valueSlider.Refresh();
                            break;
                        case ConstraintValueHandlingMethod.AddPercentCurrent:
                            valueSlider.SetNumericStops(new float[] { -10000, 10000 });
                            valueSlider.Refresh();
                            break;
                        case ConstraintValueHandlingMethod.DoNothing:
                        case ConstraintValueHandlingMethod.MatchValue:
                            valueSlider.SetNumericStops(new float[] { min, max });
                            valueSlider.Refresh();
                            break;
                        case ConstraintValueHandlingMethod.MatchPercent:
                            valueSlider.SetNumericStops(new float[] { -100, 100 });
                            valueSlider.Refresh();
                            break;
                    }

                    if (!tabPressureConstraints.ContainsKey(target))
                    {
                        tabPressureConstraints.Add(target, new(handlingMethod, 0));
                    }
                    else
                    {
                        tabPressureConstraints[target] = new(handlingMethod, tabPressureConstraints[target].value);
                    }
                }
            };

            pressureConstraintControls.Add(target, new(valueSlider, cmbxValueType));
            panel.Controls.Add(valueSlider);
            panel.Controls.Add(cmbxValueType);

            return panel;
        }

        /// <summary>
        /// Sets the active color based on the color from the canvas at the given point.
        /// </summary>
        /// <param name="loc">The point to get the color from.</param>
        private void GetColorFromCanvas(PointF loc)
        {
            PointF rotatedLoc = TransformPoint(new PointF(loc.X - halfPixelOffset, loc.Y - 1), true);

            int finalX = (int)Math.Round(rotatedLoc.X);
            int finalY = (int)Math.Round(rotatedLoc.Y);

            if (finalX >= 0 && finalY >= 0 &&
                finalX <= bmpCommitted.Width - 1 &&
                finalY <= bmpCommitted.Height - 1)
            {
                Color pixel = bmpCommitted.GetPixel(finalX, finalY);

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
        /// Given a numeric shortcut, modifies the current value based on the pressure sensitivity constraint values
        /// listed for it, with strength determined linearly by the current pressure.
        /// </summary>
        /// <param name="target">The setting to apply to.</param>
        /// <param name="value">The unmodified value of the actual control (not the constraint value).</param>
        /// <param name="pressure">A value from 0 to 1 indicating the amount of pressure the user is applying.</param>
        private int GetPressureValue(CommandTarget target, int value, float pressure)
        {
            if (pressure != 0 && tabPressureConstraints.ContainsKey(target))
            {
                int min = (int)(CommandTargetInfo.All[target].MinMaxRangeF?.Item2 ?? CommandTargetInfo.All[target].MinMaxRange.Item1);
                int max = (int)(CommandTargetInfo.All[target].MinMaxRangeF?.Item2 ?? CommandTargetInfo.All[target].MinMaxRange.Item2);

                return Math.Clamp(
                    Constraint.GetStrengthMappedValue(
                        value,
                        tabPressureConstraints[target].value,
                        max,
                        pressure,
                        tabPressureConstraints[target].handleMethod),
                    min, max);
            }

            return value;
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
        /// Returns a string with the current value of the given command target, or an empty string if it fails.
        /// </summary>
        /// <param name="target">A target for a command.</param>
        private string GetTargetValue(CommandTarget target)
        {
            switch (target)
            {
                case CommandTarget.Flow:
                    return sliderBrushFlow.Value.ToString();
                case CommandTarget.FlowShift:
                    return sliderBrushFlow.Value.ToString();
                case CommandTarget.AutomaticBrushDensity:
                    return chkbxAutomaticBrushDensity.Checked.ToString();
                case CommandTarget.BrushStrokeDensity:
                    return sliderBrushDensity.Value.ToString();
                case CommandTarget.CanvasZoom:
                    return sliderCanvasZoom.Value.ToString();
                case CommandTarget.Color:
                    return ColorUtils.GetTextFromColor(swatchPrimaryColor.Swatches[0]);
                case CommandTarget.ColorizeBrush:
                    return chkbxColorizeBrush.Checked.ToString();
                case CommandTarget.ColorInfluence:
                    return sliderColorInfluence.Value.ToString();
                case CommandTarget.ColorInfluenceHue:
                    return chkbxColorInfluenceHue.Checked.ToString();
                case CommandTarget.ColorInfluenceSat:
                    return chkbxColorInfluenceSat.Checked.ToString();
                case CommandTarget.ColorInfluenceVal:
                    return chkbxColorInfluenceVal.Checked.ToString();
                case CommandTarget.DitherDraw:
                    return chkbxDitherDraw.Checked.ToString();
                case CommandTarget.JitterBlueMax:
                    return sliderJitterMaxBlue.Value.ToString();
                case CommandTarget.JitterBlueMin:
                    return sliderJitterMinBlue.Value.ToString();
                case CommandTarget.JitterGreenMax:
                    return sliderJitterMaxGreen.Value.ToString();
                case CommandTarget.JitterGreenMin:
                    return sliderJitterMinGreen.Value.ToString();
                case CommandTarget.JitterHorSpray:
                    return sliderRandHorzShift.Value.ToString();
                case CommandTarget.JitterHueMax:
                    return sliderJitterMaxHue.Value.ToString();
                case CommandTarget.JitterHueMin:
                    return sliderJitterMinHue.Value.ToString();
                case CommandTarget.JitterFlowLoss:
                    return sliderRandFlowLoss.Value.ToString();
                case CommandTarget.JitterMaxSize:
                    return sliderRandMaxSize.Value.ToString();
                case CommandTarget.JitterMinSize:
                    return sliderRandMinSize.Value.ToString();
                case CommandTarget.JitterRedMax:
                    return sliderJitterMaxRed.Value.ToString();
                case CommandTarget.JitterRedMin:
                    return sliderJitterMinRed.Value.ToString();
                case CommandTarget.JitterRotLeft:
                    return sliderRandRotLeft.Value.ToString();
                case CommandTarget.JitterRotRight:
                    return sliderRandRotRight.Value.ToString();
                case CommandTarget.JitterSatMax:
                    return sliderJitterMaxSat.Value.ToString();
                case CommandTarget.JitterSatMin:
                    return sliderJitterMinSat.Value.ToString();
                case CommandTarget.JitterValMax:
                    return sliderJitterMaxVal.Value.ToString();
                case CommandTarget.JitterValMin:
                    return sliderJitterMinVal.Value.ToString();
                case CommandTarget.JitterVerSpray:
                    return sliderRandVertShift.Value.ToString();
                case CommandTarget.DoLockAlpha:
                    return chkbxLockAlpha.Checked.ToString();
                case CommandTarget.DoLockR:
                    return chkbxLockR.Checked.ToString();
                case CommandTarget.DoLockG:
                    return chkbxLockG.Checked.ToString();
                case CommandTarget.DoLockB:
                    return chkbxLockB.Checked.ToString();
                case CommandTarget.DoLockHue:
                    return chkbxLockHue.Checked.ToString();
                case CommandTarget.DoLockSat:
                    return chkbxLockSat.Checked.ToString();
                case CommandTarget.DoLockVal:
                    return chkbxLockVal.Checked.ToString();
                case CommandTarget.MinDrawDistance:
                    return sliderMinDrawDistance.Value.ToString();
                case CommandTarget.RotateWithMouse:
                    return chkbxOrientToMouse.Checked.ToString();
                case CommandTarget.Rotation:
                    return sliderBrushRotation.Value.ToString();
                case CommandTarget.RotShift:
                    return sliderShiftRotation.Value.ToString();
                case CommandTarget.SelectedBrush:
                    return currentBrushPath;
                case CommandTarget.SelectedBrushImage:
                    string allImages = "";
                    for (int i = 0; i < listviewBrushImagePicker.SelectedIndices.Count; i++)
                    {
                        allImages += loadedBrushImages[0].Name;
                        if (i < listviewBrushImagePicker.SelectedIndices.Count - 1)
                        {
                            allImages += ",";
                        }
                    }
                    return allImages;
                case CommandTarget.SelectedTool:
                    // TODO: this is not a protected value across API. If the combobox gets new items it'll change the depended behavior.
                    return ((int)activeTool).ToString();
                case CommandTarget.Size:
                    return sliderBrushSize.Value.ToString();
                case CommandTarget.SizeShift:
                    return sliderShiftSize.Value.ToString();
                case CommandTarget.SmoothingMode:
                    // TODO: this is not a protected value across API. If the combobox gets new items it'll change the depended behavior.
                    return cmbxBrushSmoothing.SelectedIndex.ToString();
                case CommandTarget.SymmetryMode:
                    // TODO: this is not a protected value across API. If the combobox gets new items it'll change the depended behavior.
                    return cmbxSymmetry.SelectedIndex.ToString();
                case CommandTarget.CanvasX:
                    return canvas.x.ToString();
                case CommandTarget.CanvasY:
                    return canvas.y.ToString();
                case CommandTarget.CanvasRotation:
                    return sliderCanvasAngle.Value.ToString();
                case CommandTarget.BlendMode:
                    // TODO: this is not a protected value across API. If the combobox gets new items it'll change the depended behavior.
                    return cmbxBlendMode.SelectedIndex.ToString();
                case CommandTarget.SeamlessDrawing:
                    return chkbxSeamlessDrawing.Checked.ToString();
                case CommandTarget.BrushOpacity:
                    return sliderBrushOpacity.Value.ToString();
                case CommandTarget.ChosenEffect:
                    return cmbxChosenEffect.SelectedIndex.ToString();
                default:
                    return "";
            };
        }

        /// <summary>
        /// Executes actions for invoked keyboard shortcuts. This is connected to shortcuts located in persistent
        /// settings from <see cref="InitTokenFromDialog"/>.
        /// </summary>
        /// <param name="shortcut">Any shortcut invoked</param>
        private void HandleShortcut(Command shortcut)
        {
            switch (shortcut.Target)
            {
                case CommandTarget.Flow:
                    sliderBrushFlow.Value =
                        shortcut.GetDataAsInt(sliderBrushFlow.ValueInt,
                        sliderBrushFlow.MinimumInt,
                        sliderBrushFlow.MaximumInt);
                    break;
                case CommandTarget.FlowShift:
                    sliderBrushFlow.Value =
                        shortcut.GetDataAsInt(sliderBrushFlow.ValueInt,
                        sliderBrushFlow.MinimumInt,
                        sliderBrushFlow.MaximumInt);
                    break;
                case CommandTarget.AutomaticBrushDensity:
                    chkbxAutomaticBrushDensity.Checked = shortcut.GetDataAsBool(chkbxAutomaticBrushDensity.Checked);
                    break;
                case CommandTarget.BrushStrokeDensity:
                    sliderBrushDensity.Value =
                        shortcut.GetDataAsInt(sliderBrushDensity.ValueInt,
                        sliderBrushDensity.MinimumInt,
                        sliderBrushDensity.MaximumInt);
                    break;
                case CommandTarget.CanvasZoom:
                    sliderCanvasZoom.ValueInt =
                        shortcut.GetDataAsInt(sliderCanvasZoom.ValueInt,
                        sliderCanvasZoom.MinimumInt, sliderCanvasZoom.MaximumInt);
                    break;
                case CommandTarget.Color:
                    UpdateBrushColor(shortcut.GetDataAsColor(menuActiveColors.Swatches[0]), true);
                    break;
                case CommandTarget.ColorizeBrush:
                    chkbxColorizeBrush.Checked = shortcut.GetDataAsBool(chkbxColorizeBrush.Checked);
                    UpdateBrushImage();
                    break;
                case CommandTarget.ColorInfluence:
                    sliderColorInfluence.Value = shortcut.GetDataAsInt(sliderColorInfluence.ValueInt,
                        sliderColorInfluence.MinimumInt, sliderColorInfluence.MaximumInt);
                    UpdateBrushImage();
                    break;
                case CommandTarget.ColorInfluenceHue:
                    chkbxColorInfluenceHue.Checked = shortcut.GetDataAsBool(chkbxColorInfluenceHue.Checked);
                    break;
                case CommandTarget.ColorInfluenceSat:
                    chkbxColorInfluenceSat.Checked = shortcut.GetDataAsBool(chkbxColorInfluenceSat.Checked);
                    break;
                case CommandTarget.ColorInfluenceVal:
                    chkbxColorInfluenceVal.Checked = shortcut.GetDataAsBool(chkbxColorInfluenceVal.Checked);
                    break;
                case CommandTarget.DitherDraw:
                    chkbxDitherDraw.Checked = shortcut.GetDataAsBool(chkbxDitherDraw.Checked);
                    break;
                case CommandTarget.JitterBlueMax:
                    sliderJitterMaxBlue.Value =
                        shortcut.GetDataAsInt(sliderJitterMaxBlue.ValueInt,
                        sliderJitterMaxBlue.MinimumInt, sliderJitterMaxBlue.MaximumInt);
                    break;
                case CommandTarget.JitterBlueMin:
                    sliderJitterMinBlue.Value =
                        shortcut.GetDataAsInt(sliderJitterMinBlue.ValueInt,
                        sliderJitterMinBlue.MinimumInt, sliderJitterMinBlue.MaximumInt);
                    break;
                case CommandTarget.JitterGreenMax:
                    sliderJitterMaxGreen.Value =
                        shortcut.GetDataAsInt(sliderJitterMaxGreen.ValueInt,
                        sliderJitterMaxGreen.MinimumInt, sliderJitterMaxGreen.MaximumInt);
                    break;
                case CommandTarget.JitterGreenMin:
                    sliderJitterMinGreen.Value =
                        shortcut.GetDataAsInt(sliderJitterMinGreen.ValueInt,
                        sliderJitterMinGreen.MinimumInt, sliderJitterMinGreen.MaximumInt);
                    break;
                case CommandTarget.JitterHorSpray:
                    sliderRandHorzShift.Value =
                        shortcut.GetDataAsInt(sliderRandHorzShift.ValueInt,
                        sliderRandHorzShift.MinimumInt, sliderRandHorzShift.MaximumInt);
                    break;
                case CommandTarget.JitterHueMax:
                    sliderJitterMaxHue.Value =
                        shortcut.GetDataAsInt(sliderJitterMaxHue.ValueInt,
                        sliderJitterMaxHue.MinimumInt, sliderJitterMaxHue.MaximumInt);
                    break;
                case CommandTarget.JitterHueMin:
                    sliderJitterMinHue.Value =
                        shortcut.GetDataAsInt(sliderJitterMinHue.ValueInt,
                        sliderJitterMinHue.MinimumInt, sliderJitterMinHue.MaximumInt);
                    break;
                case CommandTarget.JitterFlowLoss:
                    sliderRandFlowLoss.Value =
                        shortcut.GetDataAsInt(sliderRandFlowLoss.ValueInt,
                        sliderRandFlowLoss.MinimumInt, sliderRandFlowLoss.MaximumInt);
                    break;
                case CommandTarget.JitterMaxSize:
                    sliderRandMaxSize.Value =
                        shortcut.GetDataAsInt(sliderRandMaxSize.ValueInt,
                        sliderRandMaxSize.MinimumInt, sliderRandMaxSize.MaximumInt);
                    break;
                case CommandTarget.JitterMinSize:
                    sliderRandMinSize.Value =
                        shortcut.GetDataAsInt(sliderRandMinSize.ValueInt,
                        sliderRandMinSize.MinimumInt, sliderRandMinSize.MaximumInt);
                    break;
                case CommandTarget.JitterRedMax:
                    sliderJitterMaxRed.Value =
                        shortcut.GetDataAsInt(sliderJitterMaxRed.ValueInt,
                        sliderJitterMaxRed.MinimumInt, sliderJitterMaxRed.MaximumInt);
                    break;
                case CommandTarget.JitterRedMin:
                    sliderJitterMinRed.Value =
                        shortcut.GetDataAsInt(sliderJitterMinRed.ValueInt,
                        sliderJitterMinRed.MinimumInt, sliderJitterMinRed.MaximumInt);
                    break;
                case CommandTarget.JitterRotLeft:
                    sliderRandRotLeft.Value =
                        shortcut.GetDataAsInt(sliderRandRotLeft.ValueInt,
                        sliderRandRotLeft.MinimumInt, sliderRandRotLeft.MaximumInt);
                    break;
                case CommandTarget.JitterRotRight:
                    sliderRandRotRight.Value =
                        shortcut.GetDataAsInt(sliderRandRotRight.ValueInt,
                        sliderRandRotRight.MinimumInt, sliderRandRotRight.MaximumInt);
                    break;
                case CommandTarget.JitterSatMax:
                    sliderJitterMaxSat.Value =
                        shortcut.GetDataAsInt(sliderJitterMaxSat.ValueInt,
                        sliderJitterMaxSat.MinimumInt, sliderJitterMaxSat.MaximumInt);
                    break;
                case CommandTarget.JitterSatMin:
                    sliderJitterMinSat.Value =
                        shortcut.GetDataAsInt(sliderJitterMinSat.ValueInt,
                        sliderJitterMinSat.MinimumInt, sliderJitterMinSat.MaximumInt);
                    break;
                case CommandTarget.JitterValMax:
                    sliderJitterMaxVal.Value =
                        shortcut.GetDataAsInt(sliderJitterMaxVal.ValueInt,
                        sliderJitterMaxVal.MinimumInt, sliderJitterMaxVal.MaximumInt);
                    break;
                case CommandTarget.JitterValMin:
                    sliderJitterMinVal.Value =
                        shortcut.GetDataAsInt(sliderJitterMinVal.ValueInt,
                        sliderJitterMinVal.MinimumInt, sliderJitterMinVal.MaximumInt);
                    break;
                case CommandTarget.JitterVerSpray:
                    sliderRandVertShift.Value =
                        shortcut.GetDataAsInt(sliderRandVertShift.ValueInt,
                        sliderRandVertShift.MinimumInt, sliderRandVertShift.MaximumInt);
                    break;
                case CommandTarget.DoLockAlpha:
                    chkbxLockAlpha.Checked = shortcut.GetDataAsBool(chkbxLockAlpha.Checked);
                    break;
                case CommandTarget.DoLockR:
                    chkbxLockR.Checked = shortcut.GetDataAsBool(chkbxLockR.Checked);
                    break;
                case CommandTarget.DoLockG:
                    chkbxLockG.Checked = shortcut.GetDataAsBool(chkbxLockG.Checked);
                    break;
                case CommandTarget.DoLockB:
                    chkbxLockB.Checked = shortcut.GetDataAsBool(chkbxLockB.Checked);
                    break;
                case CommandTarget.DoLockHue:
                    chkbxLockHue.Checked = shortcut.GetDataAsBool(chkbxLockHue.Checked);
                    break;
                case CommandTarget.DoLockSat:
                    chkbxLockSat.Checked = shortcut.GetDataAsBool(chkbxLockSat.Checked);
                    break;
                case CommandTarget.DoLockVal:
                    chkbxLockVal.Checked = shortcut.GetDataAsBool(chkbxLockVal.Checked);
                    break;
                case CommandTarget.MinDrawDistance:
                    sliderMinDrawDistance.Value =
                        shortcut.GetDataAsInt(sliderMinDrawDistance.ValueInt,
                        sliderMinDrawDistance.MinimumInt, sliderMinDrawDistance.MaximumInt);
                    break;
                case CommandTarget.RotateWithMouse:
                    chkbxOrientToMouse.Checked = shortcut.GetDataAsBool(chkbxOrientToMouse.Checked);
                    break;
                case CommandTarget.Rotation:
                    sliderBrushRotation.Value =
                        shortcut.GetDataAsInt(sliderBrushRotation.ValueInt,
                        sliderBrushRotation.MinimumInt, sliderBrushRotation.MaximumInt);
                    break;
                case CommandTarget.RotShift:
                    sliderShiftRotation.Value =
                        shortcut.GetDataAsInt(sliderShiftRotation.ValueInt,
                        sliderShiftRotation.MinimumInt, sliderShiftRotation.MaximumInt);
                    break;
                case CommandTarget.SelectedBrush:
                    int indexToSelect = -1;
                    for (int i = 0; i < listviewBrushPicker.Items.Count; i++)
                    {
                        if (shortcut.ActionData.Equals(
                            listviewBrushPicker.Items[i].Text, StringComparison.CurrentCultureIgnoreCase))
                        {
                            indexToSelect = i;
                        }
                        else
                        {
                            listviewBrushPicker.Items[i].Selected = false;
                        }
                    }

                    if (indexToSelect != -1)
                    {
                        listviewBrushPicker.Items[indexToSelect].Selected = true;
                    }
                    break;
                case CommandTarget.SelectedBrushImage:
                    int selectedBrushImageIndex = -1;
                    for (int i = 0; i < loadedBrushImages.Count; i++)
                    {
                        if (shortcut.ActionData.Equals(loadedBrushImages[i].Location, StringComparison.CurrentCultureIgnoreCase))
                        {
                            selectedBrushImageIndex = i;
                        }
                    }

                    if (selectedBrushImageIndex == -1)
                    {
                        for (int i = 0; i < loadedBrushImages.Count; i++)
                        {
                            if (shortcut.ActionData.Equals(loadedBrushImages[i].Name, StringComparison.CurrentCultureIgnoreCase))
                            {
                                selectedBrushImageIndex = i;
                                break;
                            }
                        }
                    }

                    if (selectedBrushImageIndex != -1)
                    {
                        listviewBrushImagePicker.SelectedIndices.Clear();
                        listviewBrushImagePicker.SelectedIndices.Add(selectedBrushImageIndex);
                    }
                    break;
                case CommandTarget.SelectedTool:
                    Tool newTool = (Tool)shortcut.GetDataAsInt((int)activeTool, 0, Enum.GetValues(typeof(Tool)).Length - 1);
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
                case CommandTarget.Size:
                    sliderBrushSize.Value =
                        shortcut.GetDataAsInt(sliderBrushSize.ValueInt,
                        sliderBrushSize.MinimumInt, sliderBrushSize.MaximumInt);
                    break;
                case CommandTarget.SizeShift:
                    sliderShiftSize.Value =
                        shortcut.GetDataAsInt(sliderShiftSize.ValueInt,
                        sliderShiftSize.MinimumInt, sliderShiftSize.MaximumInt);
                    break;
                case CommandTarget.SmoothingMode:
                    cmbxBrushSmoothing.SelectedIndex =
                        shortcut.GetDataAsInt(cmbxBrushSmoothing.SelectedIndex,
                        0, cmbxBrushSmoothing.Items.Count - 1);
                    break;
                case CommandTarget.SymmetryMode:
                    cmbxSymmetry.SelectedIndex =
                        shortcut.GetDataAsInt(cmbxSymmetry.SelectedIndex, 0, cmbxSymmetry.Items.Count - 1);
                    break;
                case CommandTarget.UndoAction:
                    BttnUndo_Click(null, null);
                    break;
                case CommandTarget.RedoAction:
                    BttnRedo_Click(null, null);
                    break;
                case CommandTarget.ResetCanvasTransforms:
                    canvas.width = bmpCommitted.Width;
                    canvas.height = bmpCommitted.Height;
                    canvas.x = (displayCanvas.Width - canvas.width) / 2;
                    canvas.y = (displayCanvas.Height - canvas.height) / 2;
                    sliderCanvasAngle.ValueInt = 0;
                    sliderCanvasZoom.ValueInt = 100;
                    break;
                case CommandTarget.CanvasX:
                    canvas.x -= (int)((shortcut.GetDataAsInt(canvas.x, int.MinValue, int.MaxValue) - canvas.x) * canvasZoom);
                    displayCanvas.Refresh();
                    break;
                case CommandTarget.CanvasY:
                    canvas.y -= (int)((shortcut.GetDataAsInt(canvas.y, int.MinValue, int.MaxValue) - canvas.y) * canvasZoom);
                    displayCanvas.Refresh();
                    break;
                case CommandTarget.CanvasRotation:
                    var newValue = shortcut.GetDataAsInt(sliderCanvasAngle.ValueInt, int.MinValue, int.MaxValue);
                    while (newValue <= -180) { newValue += 360; }
                    while (newValue > 180) { newValue -= 360; }
                    sliderCanvasAngle.ValueInt = newValue;
                    break;
                case CommandTarget.BlendMode:
                    cmbxBlendMode.SelectedIndex =
                        shortcut.GetDataAsInt(cmbxBlendMode.SelectedIndex, 0, cmbxBlendMode.Items.Count - 1);
                    break;
                case CommandTarget.SeamlessDrawing:
                    chkbxSeamlessDrawing.Checked = shortcut.GetDataAsBool(chkbxSeamlessDrawing.Checked);
                    break;
                case CommandTarget.BrushOpacity:
                    sliderBrushOpacity.Value =
                        shortcut.GetDataAsInt(sliderBrushOpacity.ValueInt,
                        sliderBrushOpacity.MinimumInt,
                        sliderBrushOpacity.MaximumInt);
                    break;
                case CommandTarget.ChosenEffect:
                    cmbxChosenEffect.SelectedIndex =
                        shortcut.GetDataAsInt(cmbxChosenEffect.SelectedIndex, 0, cmbxChosenEffect.Items.Count - 1);
                    break;
                case CommandTarget.CanvasZoomToMouse:
                    isWheelZooming = true;
                    sliderCanvasZoom.ValueInt =
                        shortcut.GetDataAsInt(sliderCanvasZoom.ValueInt,
                        sliderCanvasZoom.MinimumInt, sliderCanvasZoom.MaximumInt);
                    break;
                case CommandTarget.CanvasZoomFit:
                    canvas.width = bmpCommitted.Width;
                    canvas.height = bmpCommitted.Height;
                    canvas.x = (displayCanvas.Width - canvas.width) / 2;
                    canvas.y = (displayCanvas.Height - canvas.height) / 2;

                    // We're fitting the bitmap W*H in the canvas, but since it can be rotated, we need to get the
                    // rotated coordinates and use those instead. It's the same at no rotation.
                    double radAngle = sliderCanvasAngle.Value * Math.PI / 180;
                    double cos = Math.Abs(Math.Cos(radAngle));
                    double sin = Math.Abs(Math.Sin(radAngle));
                    double newWidth = bmpCommitted.Width * cos + bmpCommitted.Height * sin;
                    double newHeight = bmpCommitted.Width * sin + bmpCommitted.Height * cos;

                    double newZoom = 100 * Math.Min(
                        displayCanvas.ClientSize.Width / newWidth,
                        displayCanvas.ClientSize.Height / newHeight);

                    int result = (int)Math.Clamp(newZoom, sliderCanvasZoom.MinimumInt, sliderCanvasZoom.MaximumInt);
                    sliderCanvasZoom.ValueInt = result > 1 ? result : 1;
                    break;
                case CommandTarget.SwapPrimarySecondaryColors:
                    menuActiveColors.Swatches.Reverse();
                    UpdateBrushColor(menuActiveColors.Swatches[0], true);
                    break;
                case CommandTarget.OpenColorPickerDialog:
                    ColorPickerDialog dlgPicker = new ColorPickerDialog(menuActiveColors.Swatches[0], true);
                    if (dlgPicker.ShowDialog() == DialogResult.OK)
                    {
                        UpdateBrushColor(dlgPicker.AssociatedColor, true);
                    }
                    currentKeysPressed.Clear(); // avoids issues with key interception from the dialog.
                    break;
                case CommandTarget.OpenQuickCommandDialog:
                    CommandDialog dlgCommand = new CommandDialog(KeyboardShortcuts);
                    if (dlgCommand.ShowDialog() == DialogResult.OK)
                    {
                        HandleShortcut(dlgCommand.ShortcutToExecute);
                    }
                    currentKeysPressed.Clear(); // avoids issues with key interception from the dialog.
                    break;
                case CommandTarget.SwitchPalette:
                    int index = shortcut.GetDataAsInt(cmbxPaletteDropdown.SelectedIndex, 0, cmbxPaletteDropdown.Items.Count - 1);
                    cmbxPaletteDropdown.SelectedIndex = index;
                    break;
                case CommandTarget.PickFromPalette:
                    int index2 = shortcut.GetDataAsInt(paletteSelectedSwatchIndex, 0, menuPalette.Swatches.Count - 1);
                    paletteSelectedSwatchIndex = index2;
                    menuPalette.SelectedIndex = paletteSelectedSwatchIndex;
                    UpdateBrushColor(menuPalette.Swatches[paletteSelectedSwatchIndex], true, false, false, false);
                    break;
                case CommandTarget.ConfirmLine:
                    if (activeTool == Tool.Line)
                    {
                        MergeStaged();
                        lineOrigins.points.Clear();
                        lineOrigins.dragIndex = null;
                        contexts.Remove(CommandContext.LineToolConfirmStage);
                        contexts.Add(CommandContext.LineToolUnstartedStage);
                    }
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
            openFileDialog.InitialDirectory = importBrushImagesLastDirectory ?? defPath;
            openFileDialog.Multiselect = true;
            openFileDialog.Title = Strings.CustomBrushImagesDirectoryTitle;
            openFileDialog.Filter = Strings.CustomBrushImagesDirectoryFilter +
                "|*.png;*.bmp;*.jpg;*.gif;*.tif;*.exif*.jpeg;*.tiff;*.abr;";

            //Displays the dialog. Loads the files if it worked.
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                importBrushImagesLastDirectory = Path.GetDirectoryName(openFileDialog.FileName);
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
        /// (Re)registers the keyboard shortcuts for the app.
        /// </summary>
        private void InitKeyboardShortcuts(HashSet<Command> shortcutsToApply)
        {
            KeyboardShortcuts = new HashSet<Command>(); // Prevents duplicate shortcut handling.

            foreach (Command shortcut in shortcutsToApply)
            {
                shortcut.OnInvoke = new Action(() =>
                {
                    HandleShortcut(shortcut);
                });

                KeyboardShortcuts.Add(shortcut);
            }
        }

        /// <summary>
        /// Sets up all GUI elements in this form.
        /// </summary>
        private void SetupGUI()
        {
            components = new Container();
            ComponentResourceManager resources = new ComponentResourceManager(typeof(WinDynamicDraw));

            #region initialize every component at once
            timerRepositionUpdate = new Timer(components);
            timerClipboardDataCheck = new Timer(components);
            txtTooltip = new Label();
            displayCanvas = new PictureBox();
            topMenu = new FlowLayoutPanel();
            bttnToolBrush = new ThemedCheckbox(false);
            dummyImageList = new ImageList(components);
            panelOkCancel = new FlowLayoutPanel();
            menuUndo = new ThemedButton();
            menuRedo = new ThemedButton();
            bttnDone = new ThemedButton(false, true);
            bttnCancel = new ThemedButton(false, true);
            brushImageLoadingWorker = new BackgroundWorker();
            bttnColorPicker = new ThemedCheckbox(false);
            panelAllSettingsContainer = new Panel();
            panelDockSettingsContainer = new ThemedPanel();
            bttnToolEraser = new ThemedCheckbox(false);
            bttnToolOrigin = new ThemedCheckbox(false);
            bttnToolCloneStamp = new ThemedCheckbox(false);
            bttnToolLine = new ThemedCheckbox(false);
            panelSettingsContainer = new FlowLayoutPanel();
            bttnBrushControls = new Accordion(true);
            panelBrush = new FlowLayoutPanel();
            listviewBrushPicker = new ListView();
            listviewBrushImagePicker = new DoubleBufferedListView();
            panelBrushAddPickColor = new Panel();
            chkbxColorizeBrush = new ThemedCheckbox();
            sliderColorInfluence = new Slider(CommandTarget.ColorInfluence, 0f);
            panelColorInfluenceHSV = new FlowLayoutPanel();
            chkbxColorInfluenceHue = new ThemedCheckbox();
            chkbxColorInfluenceSat = new ThemedCheckbox();
            chkbxColorInfluenceVal = new ThemedCheckbox();
            bttnAddBrushImages = new ThemedButton();
            brushImageLoadProgressBar = new ProgressBar();
            cmbxBlendMode = new ThemedComboBox();
            sliderBrushOpacity = new Slider(CommandTarget.BrushOpacity, 255f);
            sliderBrushFlow = new Slider(CommandTarget.Flow, 255f);
            sliderBrushRotation = new Slider(CommandTarget.Rotation, 0f);
            sliderBrushSize = new Slider(CommandTarget.Size, 10f);
            bttnColorControls = new Accordion(true);
            panelColorControls = new FlowLayoutPanel();
            panelWheelWithValueSlider = new FlowLayoutPanel();
            wheelColor = new ColorWheel();
            sliderColorValue = new Slider(SliderSpecialType.ValGraph, Color.Black);
            panelColorWithHexBox = new FlowLayoutPanel();
            swatchPrimaryColor = new SwatchBox(new List<Color>() { Color.Black }, 1);
            txtbxColorHexfield = new ColorTextbox(Color.Black, true);
            bttnSpecialSettings = new Accordion(true);
            panelSpecialSettings = new FlowLayoutPanel();
            bttnSetScript = new ThemedButton();
            panelChosenEffect = new Panel();
            cmbxChosenEffect = new ThemedComboBox();
            bttnChooseEffectSettings = new ThemedButton();
            sliderMinDrawDistance = new Slider(CommandTarget.MinDrawDistance, 0f);
            sliderBrushDensity = new Slider(CommandTarget.BrushStrokeDensity, 10f);
            cmbxSymmetry = new ThemedComboBox();
            cmbxBrushSmoothing = new ThemedComboBox();
            chkbxSeamlessDrawing = new ThemedCheckbox();
            chkbxOrientToMouse = new ThemedCheckbox();
            chkbxDitherDraw = new ThemedCheckbox();
            chkbxLockAlpha = new ThemedCheckbox(false);
            panelRGBLocks = new Panel();
            chkbxLockR = new ThemedCheckbox(false);
            chkbxLockG = new ThemedCheckbox(false);
            chkbxLockB = new ThemedCheckbox(false);
            panelHSVLocks = new Panel();
            chkbxLockHue = new ThemedCheckbox(false);
            chkbxLockSat = new ThemedCheckbox(false);
            chkbxLockVal = new ThemedCheckbox(false);
            bttnJitterBasicsControls = new Accordion(true);
            panelJitterBasics = new FlowLayoutPanel();
            sliderRandMinSize = new Slider(CommandTarget.JitterMinSize, 0f);
            sliderRandMaxSize = new Slider(CommandTarget.JitterMaxSize, 0f);
            sliderRandRotLeft = new Slider(CommandTarget.JitterRotLeft, 0f);
            sliderRandRotRight = new Slider(CommandTarget.JitterRotRight, 0f);
            sliderRandFlowLoss = new Slider(CommandTarget.JitterFlowLoss, 0f);
            sliderRandHorzShift = new Slider(CommandTarget.JitterHorSpray, 0f);
            sliderRandVertShift = new Slider(CommandTarget.JitterVerSpray, 0f);
            bttnJitterColorControls = new Accordion(true);
            panelJitterColor = new FlowLayoutPanel();
            sliderJitterMinRed = new Slider(CommandTarget.JitterRedMin, 0f);
            sliderJitterMaxRed = new Slider(CommandTarget.JitterRedMin, 0f);
            sliderJitterMinGreen = new Slider(CommandTarget.JitterGreenMin, 0f);
            sliderJitterMaxGreen = new Slider(CommandTarget.JitterGreenMax, 0f);
            sliderJitterMinBlue = new Slider(CommandTarget.JitterBlueMin, 0f);
            sliderJitterMaxBlue = new Slider(CommandTarget.JitterBlueMax, 0f);
            sliderJitterMinHue = new Slider(CommandTarget.JitterHueMin, 0f);
            sliderJitterMaxHue = new Slider(CommandTarget.JitterHueMax, 0f);
            sliderJitterMinSat = new Slider(CommandTarget.JitterSatMin, 0f);
            sliderJitterMaxSat = new Slider(CommandTarget.JitterSatMax, 0f);
            sliderJitterMinVal = new Slider(CommandTarget.JitterValMin, 0f);
            sliderJitterMaxVal = new Slider(CommandTarget.JitterValMax, 0f);
            bttnShiftBasicsControls = new Accordion(true);
            panelShiftBasics = new FlowLayoutPanel();
            sliderShiftSize = new Slider(CommandTarget.SizeShift, 0f);
            sliderShiftRotation = new Slider(CommandTarget.RotShift, 0f);
            sliderShiftFlow = new Slider(CommandTarget.FlowShift, 0f);
            bttnTabAssignPressureControls = new Accordion(true);
            panelTabletAssignPressure = new FlowLayoutPanel();
            bttnSettings = new Accordion(true);
            panelSettings = new FlowLayoutPanel();
            bttnUpdateCurrentBrush = new ThemedButton();
            bttnClearSettings = new ThemedButton();
            bttnDeleteBrush = new ThemedButton();
            bttnSaveBrush = new ThemedButton();
            chkbxAutomaticBrushDensity = new ThemedCheckbox();
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
            panelWheelWithValueSlider.SuspendLayout();
            panelColorWithHexBox.SuspendLayout();
            panelSpecialSettings.SuspendLayout();
            panelChosenEffect.SuspendLayout();
            panelRGBLocks.SuspendLayout();
            panelHSVLocks.SuspendLayout();
            panelJitterBasics.SuspendLayout();
            panelJitterColor.SuspendLayout();
            panelShiftBasics.SuspendLayout();
            panelTabletAssignPressure.SuspendLayout();
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
            txtTooltip.BackColor = SemanticTheme.GetColor(ThemeName.Dark, ThemeSlot.ControlBg);
            txtTooltip.ForeColor = SemanticTheme.GetColor(ThemeName.Dark, ThemeSlot.Text);
            txtTooltip.AutoSize = true;
            txtTooltip.Dock = DockStyle.Top;
            txtTooltip.Font = tooltipFont;
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
            menuOptions = new ThemedButton
            {
                Text = Strings.MenuOptions,
                Height = 29,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            ContextMenuStrip preferencesContextMenu = new ContextMenuStrip();
            menuBrushImageDirectories = new ToolStripMenuItem(Strings.MenuCustomBrushImages);
            menuKeyboardShortcutsDialog = new ToolStripMenuItem(Strings.MenuKeyboardShortcuts);
            menuResetCanvas = new ToolStripMenuItem(Strings.MenuRecenterTheCanvas);
            menuSetCanvasBackground = new ToolStripMenuItem(Strings.MenuSetCanvasBackground);
            menuSetCanvasBgImageFit = new ToolStripMenuItem(Strings.MenuCanvasBgStretchToFit);
            menuSetCanvasBgImageOnlyIfFits = new ToolStripMenuItem(Strings.MenuCanvasBgUseOnlyIfSameSize);
            menuSetCanvasBgImage = new ToolStripMenuItem(Strings.BackgroundImage);
            menuSetCanvasBgTransparent = new ToolStripMenuItem(Strings.BackgroundTransparent);
            menuSetCanvasBgGray = new ToolStripMenuItem(Strings.BackgroundNone);
            menuSetCanvasBgWhite = new ToolStripMenuItem(Strings.BackgroundWhite);
            menuSetCanvasBgBlack = new ToolStripMenuItem(Strings.BackgroundBlack);
            menuDisplaySettings = new ToolStripMenuItem(Strings.MenuDisplaySettings);
            menuBrushIndicator = new ToolStripMenuItem(Strings.MenuDisplayBrushIndicator);
            menuBrushIndicatorSquare = new ToolStripMenuItem(Strings.MenuDisplayBrushIndicatorSquare);
            menuBrushIndicatorPreview = new ToolStripMenuItem(Strings.MenuDisplayBrushIndicatorPreview);
            menuShowSymmetryLinesInUse = new ToolStripMenuItem(Strings.MenuDisplayShowSymmetryLines);
            menuShowMinDistanceInUse = new ToolStripMenuItem(Strings.MenuDisplayShowMinDistCircle);
            menuSetTheme = new ToolStripMenuItem(Strings.MenuSetTheme);
            menuSetThemeDefault = new ToolStripMenuItem(Strings.MenuSetThemeDefault);
            menuSetThemeLight = new ToolStripMenuItem(Strings.MenuSetThemeLight);
            menuSetThemeDark = new ToolStripMenuItem(Strings.MenuSetThemeDark);
            menuColorPickerIncludesAlpha = new ToolStripMenuItem(Strings.MenuColorPickerCopiesTransparency);
            menuColorPickerSwitchesToPrevTool = new ToolStripMenuItem(Strings.MenuColorPickerSwitches);
            menuRemoveUnfoundImagePaths = new ToolStripMenuItem(Strings.MenuRemoveUnfoundImagePaths);
            menuConfirmCloseSave = new ToolStripMenuItem(Strings.MenuDontConfirmCloseSave);
            menuCanvasZoomBttn = new ThemedButton(true);
            ContextMenuStrip CanvasZoomButtonContextMenu = new ContextMenuStrip();
            menuCanvasZoomReset = new ToolStripMenuItem(Strings.MenuZoomReset);
            menuCanvasZoomFit = new ToolStripMenuItem(Strings.MenuZoomFit);
            menuCanvasZoomTo = new ToolStripMenuItem(Strings.MenuZoomTo);
            sliderCanvasZoom = new Slider(new float[] { 1, 5, 10, 13, 17, 20, 25, 33, 50, 67, 100, 150, 200, 300, 400, 500, 600, 800, 1000, 1200, 1400, 1600, 2000, 2400, 2800, 3200, 4000, 4800, 5600, 6400 }, 100);
            menuCanvasAngleBttn = new ThemedButton(true);
            ContextMenuStrip CanvasAngleButtonContextMenu = new ContextMenuStrip();
            menuCanvasAngleReset = new ToolStripMenuItem(Strings.MenuRotateReset);
            menuCanvasAngle90 = new ToolStripMenuItem(Strings.MenuRotate90);
            menuCanvasAngle180 = new ToolStripMenuItem(Strings.MenuRotate180);
            menuCanvasAngle270 = new ToolStripMenuItem(Strings.MenuRotate270);
            menuCanvasAngleTo = new ToolStripMenuItem(Strings.MenuRotateTo);
            sliderCanvasAngle = new Slider(CommandTarget.CanvasRotation, 0);
            panelTools = new FlowLayoutPanel();
            menuActiveColors = new SwatchBox(new List<Color>() { Color.Black, Color.White }, 2);
            menuPalette = new SwatchBox(null, 3);
            cmbxPaletteDropdown = new ThemedComboBox();

            preferencesContextMenu.Renderer = new ThemedMenuRenderer();

            // Options -> custom brush images...
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
                    ThemedMessageBox.Show(Strings.SettingsUnavailableError, Text, MessageBoxButtons.OK);
                    currentKeysPressed.Clear(); // modal dialogs leave key-reading in odd states. Clears it.
                }
            };
            preferencesContextMenu.Items.Add(menuBrushImageDirectories);

            // Options -> keyboard shortcuts...
            menuKeyboardShortcutsDialog.Click += (a, b) =>
            {
                var shortcutsDialog = new EditKeyboardShortcuts(KeyboardShortcuts, settings.DisabledShortcuts);
                if (shortcutsDialog.ShowDialog() == DialogResult.OK)
                {
                    settings.DisabledShortcuts = shortcutsDialog.GetDisabledShortcutsAfterDialogOK();
                    InitKeyboardShortcuts(shortcutsDialog.GetShortcutsAfterDialogOK());
                }
            };
            preferencesContextMenu.Items.Add(menuKeyboardShortcutsDialog);

            // Separator
            preferencesContextMenu.Items.Add(new ToolStripSeparator());

            // Options -> reset canvas
            menuResetCanvas.Click += (a, b) =>
            {
                HandleShortcut(new Command() { Target = CommandTarget.ResetCanvasTransforms });
            };

            preferencesContextMenu.Items.Add(menuResetCanvas);

            // Options -> set canvas background
            menuSetCanvasBgImage.DropDown.Items.Add(menuSetCanvasBgImageFit);
            menuSetCanvasBgImage.DropDown.Items.Add(menuSetCanvasBgImageOnlyIfFits);

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
            menuShowSymmetryLinesInUse.Click += (a, b) =>
            {
                UserSettings.ShowSymmetryLinesWhenUsingSymmetry = !UserSettings.ShowSymmetryLinesWhenUsingSymmetry;
                UpdateTopMenuState();
            };
            menuDisplaySettings.DropDown.Items.Add(menuShowSymmetryLinesInUse);

            // Options -> display settings -> show the circle for minimum distance when in use
            menuShowMinDistanceInUse.Click += (a, b) =>
            {
                UserSettings.ShowCircleRadiusWhenUsingMinDistance = !UserSettings.ShowCircleRadiusWhenUsingMinDistance;
                UpdateTopMenuState();
            };
            menuDisplaySettings.DropDown.Items.Add(menuShowMinDistanceInUse);
            preferencesContextMenu.Items.Add(menuDisplaySettings);

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
            menuColorPickerIncludesAlpha.Click += (a, b) =>
            {
                UserSettings.ColorPickerIncludesAlpha = !UserSettings.ColorPickerIncludesAlpha;
                UpdateTopMenuState();
            };
            preferencesContextMenu.Items.Add(menuColorPickerIncludesAlpha);

            // Options -> color picker switches to last tool when used
            menuColorPickerSwitchesToPrevTool.Click += (a, b) =>
            {
                UserSettings.ColorPickerSwitchesToLastTool = !UserSettings.ColorPickerSwitchesToLastTool;
                UpdateTopMenuState();
            };
            preferencesContextMenu.Items.Add(menuColorPickerSwitchesToPrevTool);

            // Options -> remove brush image paths when not found
            menuRemoveUnfoundImagePaths.Click += (a, b) =>
            {
                UserSettings.RemoveBrushImagePathsWhenNotFound = !UserSettings.RemoveBrushImagePathsWhenNotFound;
                UpdateTopMenuState();
            };
            preferencesContextMenu.Items.Add(menuRemoveUnfoundImagePaths);

            // Options -> don't ask to confirm when closing/saving
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

            CanvasZoomButtonContextMenu.Renderer = new ThemedMenuRenderer();

            // sets up the canvas zoom button
            menuCanvasZoomBttn.Image = Resources.MenuZoom;
            menuCanvasZoomBttn.Height = 16;
            menuCanvasZoomBttn.Width = 16;
            menuCanvasZoomBttn.Margin = new Padding(0, 7, 0, 0);

            menuCanvasZoomBttn.Click += (a, b) => {
                CanvasZoomButtonContextMenu.Show(menuCanvasZoomBttn.PointToScreen(new Point(0, menuCanvasZoomBttn.Height)));
            };

            // canvas zoom -> reset zoom
            menuCanvasZoomReset.Click += (a, b) =>
            {
                HandleShortcut(new Command() { Target = CommandTarget.CanvasZoom, ActionData = "100|set" });
            };
            CanvasZoomButtonContextMenu.Items.Add(menuCanvasZoomReset);

            // canvas zoom -> fit to window
            menuCanvasZoomFit.Click += (a, b) =>
            {
                HandleShortcut(new Command() { Target = CommandTarget.CanvasZoomFit });
            };
            CanvasZoomButtonContextMenu.Items.Add(menuCanvasZoomFit);

            // canvas zoom -> zoom to...
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

                        if (!CommandTargetInfo.All[CommandTarget.CanvasZoom].ValidateNumberValue(value))
                        {
                            return string.Format(Strings.TextboxDialogRangeInvalid,
                                CommandTargetInfo.All[CommandTarget.CanvasZoom].MinMaxRange.Item1,
                                CommandTargetInfo.All[CommandTarget.CanvasZoom].MinMaxRange.Item2);
                        }

                        return null;
                    });

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    HandleShortcut(new Command() {
                        Target = CommandTarget.CanvasZoom,
                        ActionData = int.Parse(dlg.GetSubmittedText()) + "|set"
                    });
                }
            };
            CanvasZoomButtonContextMenu.Items.Add(menuCanvasZoomTo);

            topMenu.Controls.Add(new PanelSeparator(true, 22, 29));
            topMenu.Controls.Add(menuCanvasZoomBttn);

            // canvas zoom slider
            sliderCanvasZoom.DiscreteStops = true;
            sliderCanvasZoom.Width = 128;
            sliderCanvasZoom.Height = 29;
            sliderCanvasZoom.Margin = Padding.Empty;
            sliderCanvasZoom.ComputeText = (value) => { return $"{value}%"; };
            sliderCanvasZoom.MouseEnter += SliderCanvasZoom_MouseEnter;
            sliderCanvasZoom.ValueChanged += SliderCanvasZoom_ValueChanged;

            topMenu.Controls.Add(sliderCanvasZoom);
            CanvasAngleButtonContextMenu.Renderer = new ThemedMenuRenderer();

            // sets up the canvas angle button
            menuCanvasAngleBttn.Image = Resources.MenuAngle;
            menuCanvasAngleBttn.Height = 16;
            menuCanvasAngleBttn.Width = 16;
            menuCanvasAngleBttn.Margin = new Padding(0, 7, 0, 0);

            menuCanvasAngleBttn.Click += (a, b) => {
                CanvasAngleButtonContextMenu.Show(menuCanvasAngleBttn.PointToScreen(new Point(0, menuCanvasAngleBttn.Height)));
            };

            // canvas angle -> reset angle
            menuCanvasAngleReset.Click += (a, b) =>
            {
                HandleShortcut(new Command() { Target = CommandTarget.CanvasRotation, ActionData = "0|set" });
            };
            CanvasAngleButtonContextMenu.Items.Add(menuCanvasAngleReset);

            // canvas angle -> rotate to 90
            menuCanvasAngle90.Click += (a, b) =>
            {
                HandleShortcut(new Command() { Target = CommandTarget.CanvasRotation, ActionData = "90|set" });
            };
            CanvasAngleButtonContextMenu.Items.Add(menuCanvasAngle90);

            // canvas angle -> rotate to 180
            menuCanvasAngle180.Click += (a, b) =>
            {
                HandleShortcut(new Command() { Target = CommandTarget.CanvasRotation, ActionData = "180|set" });
            };
            CanvasAngleButtonContextMenu.Items.Add(menuCanvasAngle180);

            // canvas angle -> rotate to 270
            menuCanvasAngle270.Click += (a, b) =>
            {
                HandleShortcut(new Command() { Target = CommandTarget.CanvasRotation, ActionData = "270|set" });
            };
            CanvasAngleButtonContextMenu.Items.Add(menuCanvasAngle270);

            UpdateTopMenuState();

            // canvas angle -> rotate to...
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
                    HandleShortcut(new Command()
                    {
                        Target = CommandTarget.CanvasRotation,
                        ActionData = int.Parse(dlg.GetSubmittedText()) + "|set"
                    });
                }
            };
            CanvasAngleButtonContextMenu.Items.Add(menuCanvasAngleTo);

            topMenu.Controls.Add(new PanelSeparator(true, 22, 29));
            topMenu.Controls.Add(menuCanvasAngleBttn);

            // canvas angle slider
            sliderCanvasAngle.IntegerOnly = true;
            sliderCanvasAngle.Width = 128;
            sliderCanvasAngle.Height = 29;
            sliderCanvasAngle.Margin = Padding.Empty;
            sliderCanvasAngle.ComputeText = (value) => { return $"{value}°"; };
            sliderCanvasAngle.MouseEnter += SliderCanvasAngle_MouseEnter;
            sliderCanvasAngle.ValueChanged += SliderCanvasAngle_ValueChanged;

            topMenu.Controls.Add(sliderCanvasAngle);

            // canvas tool buttons
            panelTools.AutoSize = true;
            panelTools.FlowDirection = FlowDirection.LeftToRight;
            panelTools.Controls.Add(bttnToolBrush);
            panelTools.Controls.Add(bttnToolEraser);
            panelTools.Controls.Add(bttnColorPicker);
            panelTools.Controls.Add(bttnToolOrigin);
            panelTools.Controls.Add(bttnToolCloneStamp);
            panelTools.Controls.Add(bttnToolLine);
            panelTools.Margin = Padding.Empty;
            panelTools.Padding = Padding.Empty;
            panelTools.TabIndex = 30;

            topMenu.Controls.Add(new PanelSeparator(true, 22, 29));
            topMenu.Controls.Add(panelTools);

            // user's primary & secondary colors
            menuActiveColors.Width = 29;
            menuActiveColors.Height = 29;
            menuActiveColors.Margin = new Padding(0, 0, 4, 0);
            menuActiveColors.SwatchClicked += (col) =>
            {
                if (col == 0)
                {
                    HandleShortcut(new Command() { Target = CommandTarget.OpenColorPickerDialog });
                }
                else
                {
                    HandleShortcut(new Command() { Target = CommandTarget.SwapPrimarySecondaryColors });
                }
            };
            menuActiveColors.MouseEnter += MenuActiveColors_MouseEnter;

            topMenu.Controls.Add(new PanelSeparator(true, 22, 29));
            topMenu.Controls.Add(menuActiveColors);

            menuPalette.MouseEnter += MenuPalette_MouseEnter;
            menuPalette.Width = 264;
            menuPalette.Height = 29;
            menuPalette.SwatchClicked += (col) =>
            {
                paletteSelectedSwatchIndex = col;
                menuPalette.SelectedIndex = col;
                UpdateBrushColor(menuPalette.Swatches[col], true, false, false, false);
            };

            topMenu.Controls.Add(menuPalette);

            cmbxPaletteDropdown.MouseEnter += CmbxPaletteDropdown_MouseEnter;
            cmbxPaletteDropdown.FlatStyle = FlatStyle.Flat;
            cmbxPaletteDropdown.Width = 100;
            cmbxPaletteDropdown.Margin = new Padding(0, 3, 0, 0);
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
            panelOkCancel.Controls.Add(bttnDone);
            panelOkCancel.Controls.Add(bttnCancel);
            panelOkCancel.Dock = DockStyle.Bottom;
            panelOkCancel.Location = new Point(0, 484);
            panelOkCancel.Margin = new Padding(0, 3, 0, 3);
            panelOkCancel.Size = new Size(173, 32);
            panelOkCancel.TabIndex = 145;
            #endregion

            #region bttnDone
            bttnDone.Location = new Point(3, 32);
            bttnDone.Margin = new Padding(3, 3, 13, 3);
            bttnDone.Size = new Size(77, 23);
            bttnDone.TabIndex = 143;
            bttnDone.Text = Strings.Done;
            bttnDone.Click += BttnDone_Click;
            bttnDone.MouseEnter += BttnDone_MouseEnter;
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

            #region bttnToolCloneStamp
            bttnToolCloneStamp.Image = Resources.ToolCloneStamp;
            bttnToolCloneStamp.Margin = Padding.Empty;
            bttnToolCloneStamp.Size = new Size(29, 29);
            bttnToolCloneStamp.TabIndex = 2;
            bttnToolCloneStamp.Click += BttnToolCloneStamp_Click;
            bttnToolCloneStamp.MouseEnter += BttnToolCloneStamp_MouseEnter;
            #endregion

            #region bttnToolLine
            bttnToolLine.Image = Resources.ToolLine;
            bttnToolLine.Margin = Padding.Empty;
            bttnToolLine.Size = new Size(29, 29);
            bttnToolLine.TabIndex = 2;
            bttnToolLine.Click += BttnToolLine_Click;
            bttnToolLine.MouseEnter += BttnToolLine_MouseEnter;
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
            panelColorControls.Controls.Add(panelWheelWithValueSlider);
            panelColorControls.Controls.Add(panelColorWithHexBox);
            #endregion

            #region panelWheelWithValueSlider
            panelWheelWithValueSlider.Margin = new Padding(0, 3, 0, 3);
            panelWheelWithValueSlider.Size = new Size(156, 196);
            panelWheelWithValueSlider.TabIndex = 19;
            panelWheelWithValueSlider.AutoSize = true;
            panelWheelWithValueSlider.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelWheelWithValueSlider.Controls.Add(wheelColor);
            panelWheelWithValueSlider.Controls.Add(sliderColorValue);
            #endregion

            #region wheelColor
            wheelColor.Width = 120;
            wheelColor.Height = 120;
            wheelColor.Margin = Padding.Empty;
            wheelColor.Padding = Padding.Empty;
            wheelColor.ColorChanged += (_1, _2) => UpdateColorSelectionSliders(null, true);
            wheelColor.MouseEnter += WheelColor_MouseEnter;
            #endregion

            #region sliderColorV
            sliderColorValue.AutoSize = false;
            sliderColorValue.Location = new Point(3, 52);
            sliderColorValue.Size = new Size(22, 118);
            sliderColorValue.TabIndex = 21;
            sliderColorValue.ValueChanged += (_1, _2) => UpdateColorSelectionSliders(SliderSpecialType.ValGraph);
            sliderColorValue.ComputeText = (val) => Math.Round(val).ToString();
            sliderColorValue.MouseEnter += SliderColorV_MouseEnter;
            #endregion

            #region panelColorWithHexBox
            panelColorWithHexBox.FlowDirection = FlowDirection.LeftToRight;
            panelColorWithHexBox.Margin = new Padding(0, 3, 0, 3);
            panelColorWithHexBox.Size = new Size(156, 196);
            panelColorWithHexBox.TabIndex = 19;
            panelColorWithHexBox.AutoSize = true;
            panelColorWithHexBox.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelColorWithHexBox.Controls.Add(swatchPrimaryColor);
            panelColorWithHexBox.Controls.Add(txtbxColorHexfield);
            #endregion

            #region swatchPrimaryColor
            swatchPrimaryColor.Width = 32;
            swatchPrimaryColor.Height = 32;
            swatchPrimaryColor.Margin = new Padding(0, 4, 0, 0);
            swatchPrimaryColor.SwatchClicked += (col) =>
            {
                HandleShortcut(new Command() { Target = CommandTarget.OpenColorPickerDialog });
            };
            swatchPrimaryColor.MouseEnter += SwatchPrimaryColor_MouseEnter;
            #endregion

            #region txtbxColorHexfield
            txtbxColorHexfield.Height = 24;
            txtbxColorHexfield.Width = 68;
            txtbxColorHexfield.Margin = new Padding(4, 8, 0, 0);
            txtbxColorHexfield.Text = ColorUtils.GetTextFromColor(Color.Black);
            txtbxColorHexfield.ColorUpdatedByText += TxtbxColorHexfield_ColorUpdatedByText;
            txtbxColorHexfield.MouseEnter += TxtbxColorHexfield_MouseEnter;
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
            panelSpecialSettings.Controls.Add(bttnSetScript);
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

            #region bttnSetScript
            bttnSetScript.Location = new Point(0, 3);
            bttnSetScript.Margin = new Padding(3, 3, 3, 3);
            bttnSetScript.Size = new Size(150, 23);
            bttnSetScript.TabIndex = 137;
            bttnSetScript.Click += BttnSetScript_Click;
            bttnSetScript.MouseEnter += BttnSetScript_MouseEnter;
            #endregion

            #region panelChosenEffect
            panelChosenEffect.Location = new Point(0, 61);
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
            sliderMinDrawDistance.Location = new Point(3, 110);
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
            sliderBrushDensity.ComputeText = (val) =>
            {
                return (val == 0)
                    ? string.Format("{0} {1}", Strings.BrushDensity, Strings.BrushDensityMax)
                    : string.Format("{0} {1}", Strings.BrushDensity, val);
            };
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
            chkbxLockAlpha.ToggleImage = new(Resources.Locked, Resources.Unlocked);
            chkbxLockAlpha.ImageAlign = ContentAlignment.MiddleLeft;
            chkbxLockAlpha.AutoSize = true;
            chkbxLockAlpha.Location = new Point(3, 222);
            chkbxLockAlpha.TabIndex = 28;
            chkbxLockAlpha.MouseEnter += ChkbxLockAlpha_MouseEnter;
            #endregion

            #region chkbxLockR
            chkbxLockR.ToggleImage = new(Resources.Locked, Resources.Unlocked);
            chkbxLockR.ImageAlign = ContentAlignment.MiddleLeft;
            chkbxLockR.AutoSize = true;
            chkbxLockR.Location = new Point(3, 0);
            chkbxLockR.Size = new Size(80, 17);
            chkbxLockR.TabIndex = 1;
            chkbxLockR.MouseEnter += ChkbxLockR_MouseEnter;
            #endregion

            #region chkbxLockG
            chkbxLockG.ToggleImage = new(Resources.Locked, Resources.Unlocked);
            chkbxLockG.ImageAlign = ContentAlignment.MiddleLeft;
            chkbxLockG.AutoSize = true;
            chkbxLockG.Location = new Point(44, 0);
            chkbxLockG.Size = new Size(80, 17);
            chkbxLockG.TabIndex = 2;
            chkbxLockG.MouseEnter += ChkbxLockG_MouseEnter;
            #endregion

            #region chkbxLockB
            chkbxLockB.ToggleImage = new(Resources.Locked, Resources.Unlocked);
            chkbxLockB.ImageAlign = ContentAlignment.MiddleLeft;
            chkbxLockB.AutoSize = true;
            chkbxLockB.Location = new Point(82, 0);
            chkbxLockB.Size = new Size(80, 17);
            chkbxLockB.TabIndex = 3;
            chkbxLockB.MouseEnter += ChkbxLockB_MouseEnter;
            #endregion

            #region chkbxLockHue
            chkbxLockHue.ToggleImage = new(Resources.Locked, Resources.Unlocked);
            chkbxLockHue.ImageAlign = ContentAlignment.MiddleLeft;
            chkbxLockHue.AutoSize = true;
            chkbxLockHue.Location = new Point(3, 0);
            chkbxLockHue.Size = new Size(80, 17);
            chkbxLockHue.TabIndex = 1;
            chkbxLockHue.MouseEnter += ChkbxLockHue_MouseEnter;
            #endregion

            #region chkbxLockSat
            chkbxLockSat.ToggleImage = new(Resources.Locked, Resources.Unlocked);
            chkbxLockSat.ImageAlign = ContentAlignment.MiddleLeft;
            chkbxLockSat.AutoSize = true;
            chkbxLockSat.Location = new Point(44, 0);
            chkbxLockSat.Size = new Size(80, 17);
            chkbxLockSat.TabIndex = 2;
            chkbxLockSat.MouseEnter += ChkbxLockSat_MouseEnter;
            #endregion

            #region chkbxLockVal
            chkbxLockVal.ToggleImage = new(Resources.Locked, Resources.Unlocked);
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
            bttnTabAssignPressureControls.OnCollapsedChanged += (isCollapsed) => LoadUnloadGuiAssignPressureButton(!isCollapsed);
            bttnTabAssignPressureControls.VisibleChanged += (a, b) => LoadUnloadGuiAssignPressureButton(bttnTabAssignPressureControls.Visible);
            #endregion

            #region panelTabletAssignPressure
            panelTabletAssignPressure.AutoSize = true;
            panelTabletAssignPressure.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelTabletAssignPressure.FlowDirection = FlowDirection.TopDown;
            panelTabletAssignPressure.Location = new Point(0, 1903);
            panelTabletAssignPressure.Margin = new Padding(0, 3, 0, 3);
            panelTabletAssignPressure.Size = new Size(156, 990);
            panelTabletAssignPressure.TabIndex = 55;
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

            #region DynamicDrawWindow
            AutoScaleDimensions = new SizeF(96f, 96f);
            BackgroundImageLayout = ImageLayout.None;
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
            panelWheelWithValueSlider.ResumeLayout(false);
            panelWheelWithValueSlider.PerformLayout();
            panelColorWithHexBox.ResumeLayout(false);
            panelColorWithHexBox.PerformLayout();
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
            panelSettings.ResumeLayout(false);
            ResumeLayout(false);
            #endregion
        }

        /// <summary>
        /// Generates or removes all the pressure constraint controls.
        /// </summary>
        private void LoadUnloadGuiAssignPressureButton(bool doLoad)
        {
            if (doLoad && panelTabletAssignPressure.Controls.Count == 0)
            {
                panelTabletAssignPressure.SuspendLayout();
                panelTabletAssignPressure.Controls.AddRange(new Control[]
                {
                    GeneratePressureControl(CommandTarget.BrushOpacity),
                    GeneratePressureControl(CommandTarget.Flow),
                    GeneratePressureControl(CommandTarget.Rotation),
                    GeneratePressureControl(CommandTarget.Size, 1),
                    GeneratePressureControl(CommandTarget.MinDrawDistance),
                    GeneratePressureControl(CommandTarget.BrushStrokeDensity),
                    GeneratePressureControl(CommandTarget.JitterMinSize),
                    GeneratePressureControl(CommandTarget.JitterMaxSize),
                    GeneratePressureControl(CommandTarget.JitterRotLeft),
                    GeneratePressureControl(CommandTarget.JitterRotRight),
                    GeneratePressureControl(CommandTarget.JitterFlowLoss),
                    GeneratePressureControl(CommandTarget.JitterHorSpray),
                    GeneratePressureControl(CommandTarget.JitterVerSpray),
                    GeneratePressureControl(CommandTarget.JitterRedMin),
                    GeneratePressureControl(CommandTarget.JitterRedMax),
                    GeneratePressureControl(CommandTarget.JitterGreenMin),
                    GeneratePressureControl(CommandTarget.JitterGreenMax),
                    GeneratePressureControl(CommandTarget.JitterBlueMin),
                    GeneratePressureControl(CommandTarget.JitterBlueMax),
                    GeneratePressureControl(CommandTarget.JitterHueMin),
                    GeneratePressureControl(CommandTarget.JitterHueMax),
                    GeneratePressureControl(CommandTarget.JitterSatMin),
                    GeneratePressureControl(CommandTarget.JitterSatMax),
                    GeneratePressureControl(CommandTarget.JitterValMin),
                    GeneratePressureControl(CommandTarget.JitterValMax),
                });
                panelTabletAssignPressure.ResumeLayout();
                panelTabletAssignPressure.PerformLayout();
            }
            else if (!doLoad && panelTabletAssignPressure.Controls.Count != 0)
            {
                panelTabletAssignPressure.Controls.Clear();
                pressureConstraintControls.Clear();
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
        /// Merges the staged layer to committed. This is called to finalize any drawing on the staged layer.
        /// </summary>
        private void MergeStaged()
        {
            DrawingUtils.MergeImage(bmpStaged, bmpCommitted, bmpCommitted,
                    bmpCommitted.GetBounds(),
                    (BlendMode)cmbxBlendMode.SelectedIndex,
                    (chkbxLockAlpha.Checked,
                    chkbxLockR.Checked, chkbxLockG.Checked, chkbxLockB.Checked,
                    chkbxLockHue.Checked, chkbxLockSat.Checked, chkbxLockVal.Checked));

            DrawingUtils.ColorImage(bmpStaged, ColorBgra.Black, 0);
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

            bttnToolBrush.Checked = toolToSwitchTo == Tool.Brush;
            bttnToolEraser.Checked = toolToSwitchTo == Tool.Eraser;
            bttnColorPicker.Checked = toolToSwitchTo == Tool.ColorPicker;
            bttnToolOrigin.Checked = toolToSwitchTo == Tool.SetSymmetryOrigin;
            bttnToolCloneStamp.Checked = toolToSwitchTo == Tool.CloneStamp;
            bttnToolLine.Checked = toolToSwitchTo == Tool.Line;

            CommandContextHelper.RemoveContextsFromOtherTools(toolToSwitchTo, contexts);
            displayCanvas.Cursor = toolToSwitchTo == Tool.SetSymmetryOrigin ? Cursors.Hand : Cursors.Default;
            lineOrigins.points.Clear();
            lineOrigins.dragIndex = null;

            switch (toolToSwitchTo)
            {
                case Tool.Brush:
                    contexts.Add(CommandContext.ToolBrushActive);
                    break;
                case Tool.Eraser:
                    contexts.Add(CommandContext.ToolEraserActive);
                    break;
                case Tool.SetSymmetryOrigin:
                    contexts.Add(CommandContext.ToolSetOriginActive);
                    break;
                case Tool.ColorPicker:
                    contexts.Add(CommandContext.ToolColorPickerActive);

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
                case Tool.CloneStamp:
                    contexts.Add(CommandContext.ToolCloneStampActive);
                    if (cloneStampOrigin != null)
                    {
                        contexts.Add(CommandContext.CloneStampOriginSetStage);
                        contexts.Remove(CommandContext.CloneStampOriginUnsetStage);
                    }
                    else
                    {
                        contexts.Remove(CommandContext.CloneStampOriginSetStage);
                        contexts.Add(CommandContext.CloneStampOriginUnsetStage);
                    }

                    DrawingUtils.OverwriteBits(bmpCommitted, bmpStaged);
                    break;
                case Tool.Line:
                    contexts.Add(CommandContext.ToolLineToolActive);
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
                        // Sets the clipboard background image.
                        using (Image clipboardImage = Image.FromStream(stream))
                        {
                            if (UserSettings.BackgroundDisplayMode != BackgroundDisplayMode.ClipboardOnlyIfFits ||
                                (clipboardImage.Width == bmpCommitted.Width &&
                                clipboardImage.Height == bmpCommitted.Height))
                            {
                                bmpBackgroundClipboard?.Dispose();
                                bmpBackgroundClipboard = new Bitmap(bmpCommitted.Width, bmpCommitted.Height, PixelFormat.Format32bppPArgb);
                                using (Graphics graphics = Graphics.FromImage(bmpBackgroundClipboard))
                                {
                                    graphics.CompositingMode = CompositingMode.SourceCopy;
                                    graphics.DrawImage(clipboardImage, 0, 0, bmpBackgroundClipboard.Width, bmpBackgroundClipboard.Height);
                                }
                            }
                        }

                        displayCanvas.Refresh();
                    }
                }
                catch
                {
                    if (showErrors)
                    {
                        ThemedMessageBox.Show(Strings.ClipboardErrorUnusable, Text, MessageBoxButtons.OK);
                        currentKeysPressed.Clear(); // modal dialogs leave key-reading in odd states. Clears it.
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
        /// Refreshes all scripted brush settings if a script is present and isn't ref-equal to the current brush's
        /// scripts. The assumption is if any are ref-equal, the user hasn't changed their brush (in which case it's
        /// bad to clear their custom variables). See PrepareBrushScripts for more reasoning.
        /// </summary>
        private void UpdateBrushScripts(ToolScripts scripts)
        {
            if (scripts != null && scripts.Scripts != null && scripts.Scripts.Count != 0)
            {
                if (currentBrushScripts == null || currentBrushScripts.Scripts == null || currentBrushScripts.Scripts.Count == 0 ||
                    (scripts.Scripts[0] != currentBrushScripts.Scripts[0]))
                {
                    bttnSetScript.Text = Strings.EditScript;
                    currentBrushScripts = new(scripts);
                    BrushScriptsPrepare(true);
                }
            }
            else
            {
                currentBrushScripts = null;
                bttnSetScript.Text = Strings.SetScript;
            }
        }

        /// <summary>
        /// Updates all settings based on the currently selected brush.
        /// </summary>
        private void UpdateBrush(BrushSettings settings)
        {
            UpdateBrushScripts(settings.BrushScripts);

            // Whether the update/delete brush buttons are enabled or not.
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
            tabPressureConstraints = new Dictionary<CommandTarget, BrushSettingConstraint>(settings.TabPressureConstraints);
            cmbxBlendMode.SelectedIndex = (int)settings.BlendMode;
            cmbxBrushSmoothing.SelectedIndex = (int)settings.Smoothing;
            cmbxSymmetry.SelectedIndex = (int)settings.Symmetry;

            UpdateBrushColor(Color.FromArgb(settings.BrushColor), true);
            UpdateTabPressureControls();
            UpdateEnabledControls();
        }

        /// <summary>
        /// Updates the current brush color to the desired color, and optionally syncs the opacity slider.
        /// </summary>
        /// <param name="newColor">The new color to set the brush to.</param>
        private void UpdateBrushColor(Color newColor, bool updateOpacity = false,
            bool fromOpacityChanging = false, bool fromColorSliderChanging = false, bool updatePalette = true)
        {
            //Sets the color and updates the brushes.
            menuActiveColors.Swatches[0] = newColor;
            menuActiveColors.Refresh();
            swatchPrimaryColor.Swatches[0] = newColor;
            swatchPrimaryColor.Refresh();
            txtbxColorHexfield.AssociatedColor = newColor;

            if (updatePalette &&
                paletteOptions[cmbxPaletteDropdown.SelectedIndex].Item2.SpecialType != PaletteSpecialType.None &&
                paletteOptions[cmbxPaletteDropdown.SelectedIndex].Item2.SpecialType != PaletteSpecialType.Current &&
                paletteOptions[cmbxPaletteDropdown.SelectedIndex].Item2.SpecialType != PaletteSpecialType.Recent)
            {
                GeneratePalette(paletteOptions[cmbxPaletteDropdown.SelectedIndex].Item2.SpecialType);
            }

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
        private void UpdateBrushImage(bool doRefresh = true)
        {
            if (bmpBrush == null)
            {
                return;
            }

            int finalBrushFlow = GetPressureValue(CommandTarget.Flow, sliderBrushFlow.ValueInt, tabletPressureRatio);

            //Sets the color and alpha.
            Color setColor = menuActiveColors.Swatches[0];
            float multAlpha = (
                activeTool == Tool.Eraser
                || activeTool == Tool.CloneStamp
                || effectToDraw.Effect != null
                || (BlendMode)cmbxBlendMode.SelectedIndex != BlendMode.Overwrite)
                ? finalBrushFlow / 255f
                : 1;

            int maxPossibleSize =
                Math.Max(Math.Max(
                    sliderRandMaxSize.ValueInt,
                    GetPressureValue(CommandTarget.JitterMaxSize, sliderRandMaxSize.ValueInt, 0)),
                    GetPressureValue(CommandTarget.JitterMaxSize, sliderRandMaxSize.ValueInt, 1)) +
                Math.Max(Math.Max(
                    sliderBrushSize.ValueInt,
                    GetPressureValue(CommandTarget.Size, sliderBrushSize.ValueInt, 0)),
                    GetPressureValue(CommandTarget.Size, sliderBrushSize.ValueInt, 1));

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

            //Updates to show changes in the brush indicator.
            if (doRefresh)
            {
                displayCanvas.Refresh();
            }
        }

        /// <summary>
        /// Updates all sliders in tandem with each other, or based on the primary color changing.
        /// </summary>
        private void UpdateColorSelectionSliders(SliderSpecialType? sliderChanged, bool fromWheel = false)
        {
            // Gets the modified color from the slider that was changed, or the current primary color
            Color newColor = menuActiveColors.Swatches[0];

            // Overwrite the hue and saturation of the new color from the wheel, if picking color from it.
            if (fromWheel)
            {
                HsvColorF hsvCol = ColorUtils.HSVFFromBgra(newColor);
                hsvCol.Hue = wheelColor.HsvColor.Hue;
                hsvCol.Saturation = wheelColor.HsvColor.Saturation;

                // Users often have pure black as their color and expect the color to change to match the hue wheel,
                // but also expect it not to adjust value in other cases. So if it's pure black, change the value. This
                // also matches Paint.NET's native behavior.
                if (hsvCol.Value == 0)
                {
                    hsvCol.Value = wheelColor.HsvColor.Value;
                }

                newColor = ColorUtils.HSVFToBgra(hsvCol, newColor.A);
            }
            else
            {
                HsvColorF hsvCol = ColorUtils.HSVFFromBgra(newColor);
                wheelColor.HsvColor = new HsvColor(
                    (int)Math.Round(hsvCol.Hue),
                    (int)Math.Round(hsvCol.Saturation),
                    100);
            }

            // Get from, or update sliders.
            if (sliderChanged == SliderSpecialType.ValGraph)
            {
                newColor = sliderColorValue.GetColor();
            }
            else
            {
                sliderColorValue.SetColor(newColor);
            }

            // Updates the brush color if it changed.
            if (sliderChanged != null || fromWheel)
            {
                UpdateBrushColor(newColor, sliderChanged == SliderSpecialType.AlphaGraph, false, true);
            }
        }

        /// <summary>
        /// Updates which controls are enabled or not based on current settings.
        /// </summary>
        private void UpdateEnabledControls()
        {
            bool enableColorInfluence = !chkbxColorizeBrush.Checked && activeTool != Tool.Eraser && activeTool != Tool.CloneStamp && effectToDraw.Effect == null;
            bool enableColorJitter = activeTool != Tool.Eraser && activeTool != Tool.CloneStamp && effectToDraw.Effect == null && (chkbxColorizeBrush.Checked || sliderColorInfluence.Value != 0);

            sliderBrushOpacity.Enabled = ((BlendMode)cmbxBlendMode.SelectedIndex) != BlendMode.Overwrite && activeTool != Tool.Eraser && activeTool != Tool.CloneStamp && effectToDraw.Effect == null;

            chkbxColorizeBrush.Enabled = activeTool != Tool.Eraser && activeTool != Tool.CloneStamp && effectToDraw.Effect == null;
            sliderColorInfluence.Visible = enableColorInfluence;
            panelColorInfluenceHSV.Visible = enableColorInfluence && sliderColorInfluence.Value != 0;
            chkbxLockAlpha.Enabled = activeTool != Tool.Eraser && activeTool != Tool.CloneStamp && effectToDraw.Effect == null;
            cmbxBlendMode.Enabled = activeTool != Tool.Eraser && activeTool != Tool.CloneStamp && effectToDraw.Effect == null;

            bttnJitterColorControls.Visible = enableColorJitter;
            if (pressureConstraintControls.ContainsKey(CommandTarget.JitterRedMax)) { pressureConstraintControls[CommandTarget.JitterRedMax].Item1.Enabled = enableColorJitter; }
            if (pressureConstraintControls.ContainsKey(CommandTarget.JitterRedMin)) { pressureConstraintControls[CommandTarget.JitterRedMin].Item1.Enabled = enableColorJitter; }
            if (pressureConstraintControls.ContainsKey(CommandTarget.JitterBlueMax)) { pressureConstraintControls[CommandTarget.JitterBlueMax].Item1.Enabled = enableColorJitter; }
            if (pressureConstraintControls.ContainsKey(CommandTarget.JitterBlueMin)) { pressureConstraintControls[CommandTarget.JitterBlueMin].Item1.Enabled = enableColorJitter; }
            if (pressureConstraintControls.ContainsKey(CommandTarget.JitterGreenMax)) { pressureConstraintControls[CommandTarget.JitterGreenMax].Item1.Enabled = enableColorJitter; }
            if (pressureConstraintControls.ContainsKey(CommandTarget.JitterGreenMin)) { pressureConstraintControls[CommandTarget.JitterGreenMin].Item1.Enabled = enableColorJitter; }
            if (pressureConstraintControls.ContainsKey(CommandTarget.JitterHueMax)) { pressureConstraintControls[CommandTarget.JitterHueMax].Item1.Enabled = enableColorJitter; }
            if (pressureConstraintControls.ContainsKey(CommandTarget.JitterHueMin)) { pressureConstraintControls[CommandTarget.JitterHueMin].Item1.Enabled = enableColorJitter; }
            if (pressureConstraintControls.ContainsKey(CommandTarget.JitterSatMax)) { pressureConstraintControls[CommandTarget.JitterSatMax].Item1.Enabled = enableColorJitter; }
            if (pressureConstraintControls.ContainsKey(CommandTarget.JitterSatMin)) { pressureConstraintControls[CommandTarget.JitterSatMin].Item1.Enabled = enableColorJitter; }
            if (pressureConstraintControls.ContainsKey(CommandTarget.JitterValMax)) { pressureConstraintControls[CommandTarget.JitterValMax].Item1.Enabled = enableColorJitter; }
            if (pressureConstraintControls.ContainsKey(CommandTarget.JitterValMin)) { pressureConstraintControls[CommandTarget.JitterValMin].Item1.Enabled = enableColorJitter; }

            menuActiveColors.Visible = (chkbxColorizeBrush.Checked || sliderColorInfluence.Value != 0) && activeTool != Tool.Eraser && activeTool != Tool.CloneStamp && effectToDraw.Effect == null;
            menuPalette.Visible = menuActiveColors.Visible;
            cmbxPaletteDropdown.Visible = menuActiveColors.Visible;
            bttnColorControls.Visible = menuActiveColors.Visible;
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
        /// Sets the size of the palette swatch box based on its contents.
        /// </summary>
        private void UpdatePaletteSize()
        {
            if (cmbxPaletteDropdown.SelectedIndex < 0 || cmbxPaletteDropdown.SelectedIndex >= paletteOptions.Count)
            {
                return;
            }

            int minSwatchSize = 8; // 8x8 min size per swatch, anything less is hard to use
            var entry = paletteOptions[cmbxPaletteDropdown.SelectedIndex].Item2;

            /// Generated palettes are displayed on a single row, except <see cref="PaletteSpecialType.Current"/>.
            if (entry.SpecialType != PaletteSpecialType.None && entry.SpecialType != PaletteSpecialType.Current)
            {
                if (entry.SpecialType == PaletteSpecialType.Recent)
                {
                    menuPalette.NumRows = 1;
                    menuPalette.Width = minSwatchSize * 2 * menuPalette.Swatches.Count;
                }
                else if (
                    entry.SpecialType == PaletteSpecialType.PrimaryToSecondary ||
                    entry.SpecialType == PaletteSpecialType.LightToDark)
                {
                    menuPalette.NumRows = 1;
                    menuPalette.Width = minSwatchSize * 2 * paletteGeneratedMaxColors;
                }
                else if (
                    entry.SpecialType == PaletteSpecialType.Similar3 ||
                    entry.SpecialType == PaletteSpecialType.Triadic)
                {
                    menuPalette.NumRows = 3;
                    menuPalette.Width = minSwatchSize * paletteGeneratedMaxColors;
                }
                else if (
                    entry.SpecialType == PaletteSpecialType.Similar4 ||
                    entry.SpecialType == PaletteSpecialType.Complement ||
                    entry.SpecialType == PaletteSpecialType.Square ||
                    entry.SpecialType == PaletteSpecialType.SplitComplement)
                {
                    menuPalette.NumRows = 2;
                    menuPalette.Width = minSwatchSize * paletteGeneratedMaxColors;
                }
                else
                {
                    menuPalette.NumRows = 1;
                    menuPalette.Width = minSwatchSize * paletteGeneratedMaxColors;
                }
            }

            // Loaded palettes vary based on how many colors there are.
            else if (menuPalette.Swatches.Count > 0)
            {
                if (menuPalette.Swatches.Count <= 15)
                {
                    menuPalette.NumRows = 1;
                    menuPalette.Width = minSwatchSize * 2 * menuPalette.Swatches.Count;
                }
                else if (menuPalette.Swatches.Count <= 32 && menuPalette.Swatches.Count % 2 == 0)
                {
                    menuPalette.NumRows = 2;
                    menuPalette.Width = minSwatchSize * menuPalette.Swatches.Count;
                }
                else if (menuPalette.Swatches.Count <= 48 && menuPalette.Swatches.Count % 2 == 0)
                {
                    menuPalette.NumRows = 2;
                    menuPalette.Width = minSwatchSize * menuPalette.Swatches.Count / 2;
                }
                else
                {
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
            }
        }

        /// <summary>
        /// Sets the values for each tablet pressure constraint control.
        /// </summary>
        private void UpdateTabPressureControls()
        {
            foreach (var constraint in tabPressureConstraints)
            {
                if (pressureConstraintControls.ContainsKey(constraint.Key))
                {
                    pressureConstraintControls[constraint.Key].Item1.Value = constraint.Value.value;
                    pressureConstraintControls[constraint.Key].Item2.SelectMatchingItem(constraint.Value.handleMethod);
                }
            }
        }

        /// <summary>
        /// Updates the tooltip popup (reused for all tooltips) and its visibility. It's visible when non-null and non
        /// empty. Up to the first 4 registered shortcuts with a matching shortcut target are appended to the end of
        /// the tooltip.
        /// </summary>
        private void UpdateTooltip(CommandTarget target, string newTooltip)
        {
            UpdateTooltip((kbTarget) => kbTarget.Target == target, newTooltip);
        }

        /// <summary>
        /// Updates the tooltip popup (reused for all tooltips) and its visibility. It's visible when non-null and non
        /// empty. Up to the first 4 registered shortcuts for which the filter function returns true are appended to
        /// the end of the tooltip.
        /// </summary>
        private void UpdateTooltip(Func<Command, bool> filterFunc, string newTooltip)
        {
            string finalTooltip = newTooltip;
            List<string> shortcuts = new List<string>();

            // Creates a list of up to 4 bound keyboard shortcuts for the shortcut target.
            int extraCount = 0;
            foreach (Command shortcut in KeyboardShortcuts)
            {
                if (filterFunc?.Invoke(shortcut) ?? false)
                {
                    if (shortcuts.Count == 4)
                    {
                        extraCount++;
                        continue;
                    }

                    shortcuts.Add(shortcut.Name + " " + Command.GetShortcutKeysString(
                        shortcut.Keys,
                        shortcut.RequireCtrl,
                        shortcut.RequireShift,
                        shortcut.RequireAlt,
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
                    ThemedMessageBox.Show(e.Error.Message, Text, MessageBoxButtons.OK);
                    currentKeysPressed.Clear(); // modal dialogs leave key-reading in odd states. Clears it.
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

                            if (radius <= dragHotspotRadius / canvasZoom)
                            {
                                symmetryOrigins.RemoveAt(i);
                                i--;
                            }
                        }
                    }
                }
            }

            //Pans the image.
            else if (e.Button == MouseButtons.Middle || currentKeysPressed.Contains(Keys.Space))
            {
                isUserPanning = true;
                mouseLocPrev = new Point(e.Location.X - canvas.x, e.Location.Y - canvas.y);
            }

            else if (e.Button == MouseButtons.Left)
            {
                mouseLocPrev = new Point(e.Location.X - canvas.x, e.Location.Y - canvas.y);

                // Sets the clone stamp origin or tells the user it hasn't been set.
                if (activeTool == Tool.CloneStamp)
                {
                    if (currentKeysPressed.Contains(Keys.ControlKey))
                    {
                        cloneStampStartedDrawing = false;
                        cloneStampOrigin = TransformPoint(e.Location);
                        return;
                    }
                    else if (cloneStampOrigin == null)
                    {
                        ThemedMessageBox.Show(Strings.CloneStampOriginError);
                        currentKeysPressed.Clear(); // modal dialogs leave key-reading in odd states. Clears it.
                        return;
                    }
                }

                //Draws with the current brush settings.
                if (activeTool == Tool.Brush || activeTool == Tool.Eraser || activeTool == Tool.CloneStamp)
                {
                    // When using clone stamp for the first time after starting to draw, converts the origin from
                    // absolute to relative coordinates (relative to the cursor).
                    if (!isUserDrawing.started && cloneStampOrigin != null && !cloneStampStartedDrawing)
                    {
                        cloneStampStartedDrawing = true;
                        PointF newPoint = TransformPoint(e.Location);
                        newPoint = new PointF(
                            cloneStampOrigin.Value.X - newPoint.X,
                            cloneStampOrigin.Value.Y - newPoint.Y);
                        cloneStampOrigin = newPoint;
                    }

                    isUserDrawing.started = true;
                    timerRepositionUpdate.Enabled = true;

                    // Updates the recent colors palette if it's active.
                    if (activeTool == Tool.Brush)
                    {
                        int existingIndex = paletteRecent.IndexOf(menuActiveColors.Swatches[0]);
                        if (existingIndex != -1)
                        { paletteRecent.RemoveAt(existingIndex); }
                        else if (paletteRecent.Count == paletteRecentMaxColors)
                        { paletteRecent.RemoveAt(paletteRecent.Count - 1); }

                        paletteRecent.Insert(0, menuActiveColors.Swatches[0]);

                        if (cmbxPaletteDropdown.SelectedIndex >= 0 && paletteOptions.Count > 0 &&
                            paletteOptions[cmbxPaletteDropdown.SelectedIndex].Item2.SpecialType == PaletteSpecialType.Recent)
                        {
                            if (existingIndex != -1)
                            { menuPalette.Swatches.RemoveAt(existingIndex); }
                            else if (paletteRecent.Count == paletteRecentMaxColors)
                            { menuPalette.Swatches.RemoveAt(menuPalette.Swatches.Count - 1); }

                            menuPalette.Swatches.Insert(0, menuActiveColors.Swatches[0]);
                            UpdatePaletteSize();
                        }
                    }

                    //Draws the brush on the first canvas click. Lines aren't drawn at a single point.
                    //Doesn't draw for tablets, since the user hasn't exerted full pressure yet.
                    if (!chkbxOrientToMouse.Checked)
                    {
                        int finalBrushSize = GetPressureValue(CommandTarget.Size, sliderBrushSize.ValueInt, tabletPressureRatio);

                        DrawBrush(new PointF(
                            mouseLocPrev.X / canvasZoom - halfPixelOffset,
                            mouseLocPrev.Y / canvasZoom - halfPixelOffset),
                            finalBrushSize, tabletPressureRatio);
                    }
                }

                // Samples the color under the mouse.
                else if (activeTool == Tool.ColorPicker)
                {
                    GetColorFromCanvas(new PointF(
                        mouseLocPrev.X,
                        mouseLocPrev.Y));
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

                // Draws a line using the current brush settings.
                else if (activeTool == Tool.Line)
                {
                    // If start is unset, set it.
                    if (lineOrigins.points.Count < 1)
                    {
                        lineOrigins.points.Add(new PointF(mouseLoc.X / canvasZoom, mouseLoc.Y / canvasZoom));
                        contexts.Remove(CommandContext.LineToolUnstartedStage);
                    }

                    // If start and end are set and the user clicks in proximity of a drag handle, set the drag handle.
                    else if (lineOrigins.points.Count >= 2)
                    {
                        lineOrigins.dragIndex = null;
                        var rotatedPoint = TransformPoint(mouseLoc, true, false, false);

                        for (int i = 0; i < lineOrigins.points.Count; i++)
                        {
                            var lineOrigin = TransformPoint(lineOrigins.points[i], true, true, false);
                            double radius = Math.Sqrt(
                                Math.Pow(rotatedPoint.X - lineOrigin.X, 2) +
                                Math.Pow(rotatedPoint.Y - lineOrigin.Y, 2));

                            if (radius <= dragHotspotRadius / canvasZoom)
                            {
                                lineOrigins.dragIndex = i;
                                break;
                            }
                        }
                    }

                    // If start and end are set, clears staged and draws the line.
                    if (lineOrigins.points.Count >= 2 && lineOrigins.dragIndex == null)
                    {
                        DrawingUtils.ColorImage(bmpStaged, Color.Black, 0f);
                        DrawBrushLine(lineOrigins.points[0], lineOrigins.points[1]);

                        MergeStaged();
                        lineOrigins.points.Clear();
                        lineOrigins.points.Add(new PointF(mouseLoc.X / canvasZoom, mouseLoc.Y / canvasZoom));
                        contexts.Remove(CommandContext.LineToolConfirmStage);
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
                finalMinDrawDistance = GetPressureValue(CommandTarget.MinDrawDistance, sliderMinDrawDistance.ValueInt, tabletPressureRatio);

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

                if (activeTool == Tool.Brush || activeTool == Tool.CloneStamp || activeTool == Tool.Eraser)
                {
                    int finalBrushDensity = GetPressureValue(CommandTarget.BrushStrokeDensity, sliderBrushDensity.ValueInt, tabletPressureRatio);

                    // Draws without speed control. Messier, but faster.
                    if (finalBrushDensity == 0)
                    {
                        int finalBrushSize = GetPressureValue(CommandTarget.Size, sliderBrushSize.ValueInt, tabletPressureRatio);
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
                        int finalBrushSize = GetPressureValue(CommandTarget.Size, sliderBrushSize.ValueInt, tabletPressureRatio);
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
                            if (tabletPressureRatioPrev != tabletPressureRatio &&
                                tabPressureConstraints.ContainsKey(CommandTarget.Size) &&
                                tabPressureConstraints[CommandTarget.Size].value != 0)
                            {
                                tabletPressure = (float)(tabletPressureRatioPrev + i / numIntervals * (tabletPressureRatio - tabletPressureRatioPrev));
                                finalBrushSize = GetPressureValue(CommandTarget.Size, sliderBrushSize.ValueInt, tabletPressure);
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
                    GetColorFromCanvas(new PointF(mouseLoc.X, mouseLoc.Y));
                }

                // Sets the symmetry origin.
                else if (activeTool == Tool.SetSymmetryOrigin
                    && cmbxSymmetry.SelectedIndex != (int)SymmetryMode.SetPoints)
                {
                    symmetryOrigin = TransformPoint(new PointF(e.Location.X, e.Location.Y));
                }

                else if (activeTool == Tool.Line)
                {
                    // Moves drag handles.
                    if (lineOrigins.dragIndex != null)
                    {
                        PointF pt = lineOrigins.points[lineOrigins.dragIndex.Value];
                        PointF ptNew = new PointF(mouseLoc.X / canvasZoom, mouseLoc.Y / canvasZoom);

                        if ((int)lineOrigins.points[0].X == (int)lineOrigins.points[1].X &&
                            (int)lineOrigins.points[0].Y == (int)lineOrigins.points[1].Y)
                        {
                            DrawingUtils.ColorImage(bmpStaged, Color.Black, 0);
                            lineOrigins.points.Clear();
                            lineOrigins.dragIndex = null;
                            contexts.Remove(CommandContext.LineToolConfirmStage);
                            contexts.Add(CommandContext.LineToolUnstartedStage);
                            Refresh();
                        }
                        else if (pt != ptNew)
                        {
                            lineOrigins.points[lineOrigins.dragIndex.Value] = ptNew;
                            DrawingUtils.ColorImage(bmpStaged, Color.Black, 0f);
                            DrawBrushLine(lineOrigins.points[0], lineOrigins.points[1]);
                        }
                    }

                    // Updates the preview while drawing.
                    else if (lineOrigins.points.Count == 1)
                    {
                        DrawingUtils.ColorImage(bmpStaged, Color.Black, 0f);
                        DrawBrushLine(lineOrigins.points[0], new PointF(mouseLoc.X / canvasZoom, mouseLoc.Y / canvasZoom));
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
            if (isUserDrawing.started && !isUserDrawing.canvasChanged)
            {
                DrawBrush(new PointF(
                    mouseLocPrev.X / canvasZoom - halfPixelOffset,
                    mouseLocPrev.Y / canvasZoom - halfPixelOffset),
                    sliderBrushSize.ValueInt, 1); // opinionated: pass as full pressure because this is usually desired.
            }

            // Merge the staged layer down to committed, then clear the staged layer.
            if (isUserDrawing.stagedChanged && activeTool != Tool.Line)
            {
                MergeStaged();
            }

            if (isUserDrawing.canvasChanged)
            {
                if (activeTool == Tool.CloneStamp)
                {
                    DrawingUtils.OverwriteBits(bmpCommitted, bmpStaged);
                }
                if (effectToDraw.Effect != null)
                {
                    ActiveEffectRender();
                }
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

            // Color picker has a common precedent to auto-switch to last tool; this allows it.
            if (activeTool == Tool.ColorPicker && UserSettings.ColorPickerSwitchesToLastTool)
            {
                SwitchTool(lastTool);
            }

            // Adds the second point and stops dragging handles for line tool.
            else if (activeTool == Tool.Line)
            {
                if (lineOrigins.points.Count == 1)
                {
                    // Cancels if the user releases the mouse in the same position. No line should be drawn.
                    if (lineOrigins.points[0].X == mouseLoc.X / canvasZoom &&
                        lineOrigins.points[0].Y == mouseLoc.Y / canvasZoom)
                    {
                        lineOrigins.points.Clear();
                        lineOrigins.dragIndex = null;
                        contexts.Remove(CommandContext.LineToolConfirmStage);
                        contexts.Add(CommandContext.LineToolUnstartedStage);
                    }
                    // Confirms otherwise.
                    else
                    {
                        lineOrigins.points.Add(new PointF(mouseLoc.X / canvasZoom, mouseLoc.Y / canvasZoom));
                        contexts.Add(CommandContext.LineToolConfirmStage);
                    }
                }

                lineOrigins.dragIndex = null;
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

            #region Draws the canvas image
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
                e.Graphics.FillRectangle(SemanticTheme.SpecialBrushCheckeredTransparent, visibleBounds);
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
                isPreviewingEffect.settingsOpen || isPreviewingEffect.hoverPreview ||
                (activeTool == Tool.Line && lineOrigins.points.Count >= 1) ? bmpStaged :
                isUserDrawing.stagedChanged ? bmpMerged : bmpCommitted;

            if (sliderCanvasAngle.ValueInt == 0)
            {
                if (activeTool == Tool.Line && lineOrigins.points.Count >= 1)
                {
                    e.Graphics.DrawImage(
                        bmpCommitted,
                        visibleBounds,
                        lCutoffUnzoomed,
                        tCutoffUnzoomed,
                        EnvironmentParameters.SourceSurface.Width - overshootX / canvasZoom - lCutoffUnzoomed,
                        EnvironmentParameters.SourceSurface.Height - overshootY / canvasZoom - tCutoffUnzoomed,
                        GraphicsUnit.Pixel);
                }

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
                if (activeTool == Tool.Line && lineOrigins.points.Count >= 1)
                {
                    e.Graphics.DrawImage(bmpCommitted, 0, 0, canvas.width, canvas.height);
                }

                e.Graphics.DrawImage(bmpToDraw, 0, 0, canvas.width, canvas.height);
            }
            #endregion

            #region Draws the selection
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
            #endregion

            e.Graphics.ResetTransform();

            #region Draws the brush indicator for tools that use it
            if (activeTool == Tool.Brush || activeTool == Tool.Eraser || activeTool == Tool.CloneStamp)
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
            #endregion

            e.Graphics.TranslateTransform(canvas.x + drawingOffsetX, canvas.y + drawingOffsetY);
            e.Graphics.RotateTransform(sliderCanvasAngle.ValueInt);
            e.Graphics.TranslateTransform(-drawingOffsetX, -drawingOffsetY);

            #region Draws brush settings -> symmetry origins
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
                            using Pen transparentRed = new Pen(Color.FromArgb(128, 128, 0, 0), 1);

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

                    e.Graphics.ScaleTransform(1 / canvasZoom, 1 / canvasZoom);
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
            #endregion

            #region Draws clone stamp -> stamp origin
            if (activeTool == Tool.CloneStamp && cloneStampOrigin != null)
            {
                if (!cloneStampStartedDrawing)
                {
                    float halfSize = sliderBrushSize.Value / 2f;
                    e.Graphics.DrawRectangle(
                        Pens.Black,
                        (cloneStampOrigin.Value.X - halfSize) * canvasZoom,
                        (cloneStampOrigin.Value.Y - halfSize) * canvasZoom,
                        sliderBrushSize.Value * canvasZoom,
                        sliderBrushSize.Value * canvasZoom);
                }
                else
                {
                    float halfSize = sliderBrushSize.Value / 2f;
                    int radius = (int)(sliderBrushSize.Value * canvasZoom);
                    float xOffset = cloneStampOrigin.Value.X * canvasZoom;
                    float yOffset = cloneStampOrigin.Value.Y * canvasZoom;

                    float x = sliderBrushSize.Value == 1
                        ? (int)(mouseLoc.X / canvasZoom) * canvasZoom + canvas.x + xOffset
                        : (int)(mouseLoc.X / canvasZoom - halfSize + halfPixelOffset) * canvasZoom + canvas.x + xOffset;

                    float y = sliderBrushSize.Value == 1
                        ? (int)(mouseLoc.Y / canvasZoom) * canvasZoom + canvas.y + yOffset
                        : (int)(mouseLoc.Y / canvasZoom - halfSize + halfPixelOffset) * canvasZoom + canvas.y + yOffset;

                    e.Graphics.ResetTransform();
                    e.Graphics.TranslateTransform(x + radius / 2f, y + radius / 2f);
                    e.Graphics.RotateTransform(sliderBrushRotation.Value);
                    e.Graphics.TranslateTransform(-radius / 2f, -radius / 2f);

                    e.Graphics.DrawRectangle(Pens.Black, 0, 0, radius, radius);
                    e.Graphics.DrawRectangle(Pens.White, -1, -1, radius + 2, radius + 2);

                    e.Graphics.TranslateTransform(canvas.x + drawingOffsetX, canvas.y + drawingOffsetY);
                    e.Graphics.RotateTransform(sliderCanvasAngle.ValueInt);
                    e.Graphics.TranslateTransform(-drawingOffsetX, -drawingOffsetY);
                }
            }
            #endregion

            #region Draws line tool -> drag handles
            else if (activeTool == Tool.Line && lineOrigins.points.Count >= 1)
            {
                for (int i = 0; i < lineOrigins.points.Count; i++)
                {
                    var pt = TransformPoint(lineOrigins.points[i], true, true, true);
                    e.Graphics.DrawEllipse(
                        Pens.White,
                        pt.X * canvasZoom - 7,
                        pt.Y * canvasZoom - 7,
                        14, 14);
                    e.Graphics.DrawEllipse(
                        Pens.Black,
                        pt.X * canvasZoom - 6,
                        pt.Y * canvasZoom - 6,
                        12, 12);
                }
            }
            #endregion

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
            displayCanvas.BackColor = SemanticTheme.GetColor(ThemeSlot.CanvasBg);
            listviewBrushPicker.BackColor = SemanticTheme.GetColor(ThemeSlot.ControlBg);
            listviewBrushPicker.ForeColor = SemanticTheme.GetColor(ThemeSlot.Text);
            panelDockSettingsContainer.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuBg);
            topMenu.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuBg);
            topMenu.ForeColor = SemanticTheme.GetColor(ThemeSlot.Text);
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
            UpdateTooltip(CommandTarget.AutomaticBrushDensity, Strings.AutomaticBrushDensityTip);
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
            UpdateTooltip(CommandTarget.BlendMode, Strings.BlendModeTip);
        }

        private void BttnBlendMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateEnabledControls();
            SliderBrushFlow_ValueChanged(null, 0);
        }

        private void MenuActiveColors_MouseEnter(object sender, EventArgs e)
        {
            static bool filter(Command shortcut)
            {
                return shortcut.Target == CommandTarget.Color
                    || shortcut.Target == CommandTarget.SwapPrimarySecondaryColors;
            }

            UpdateTooltip(filter, Strings.BrushColorTip);
        }

        private void CmbxChosenEffect_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.ChosenEffect, Strings.ChosenEffectTip);
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
                SemanticTheme.Instance.GetBrush(ThemeSlot.ControlBg),
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
                SemanticTheme.Instance.GetBrush(ThemeSlot.Text),
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
            UpdateTooltip(CommandTarget.SmoothingMode, Strings.BrushSmoothingTip);
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
                ThemedMessageBox.Show(Strings.ConfirmCancel, Strings.Confirm, MessageBoxButtons.YesNo) != DialogResult.Yes)
            {
                currentKeysPressed.Clear(); // modal dialogs leave key-reading in odd states. Clears it.
                DialogResult = DialogResult.None;
                return;
            }

            currentKeysPressed.Clear(); // modal dialogs leave key-reading in odd states. Clears it.

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
            if (ThemedMessageBox.Show(Strings.ConfirmDeleteBrush, "", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                settings.CustomBrushes.Remove(currentBrushPath);
                listviewBrushPicker.Items.RemoveAt(listviewBrushPicker.SelectedIndices[0]);
                listviewBrushPicker.AutoResizeColumn(0, ColumnHeaderAutoResizeStyle.ColumnContent);
                currentBrushPath = null;
                bttnUpdateCurrentBrush.Enabled = false;
                bttnDeleteBrush.Enabled = false;
            }

            currentKeysPressed.Clear(); // modal dialogs leave key-reading in odd states. Clears it.
        }

        private void BttnDeleteBrush_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.DeleteBrushTip);
        }

        /// <summary>
        /// Accepts and applies the effect.
        /// </summary>
        private void BttnDone_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;

            // It's easy to hit the enter key on accident, especially when toggling other controls.
            // If any changes were made and the OK button was indirectly invoked, ask for confirmation first.
            if (!UserSettings.DisableConfirmationOnCloseOrSave && undoHistory.Count > 0 && !bttnDone.Focused &&
                ThemedMessageBox.Show(Strings.ConfirmChanges, Strings.Confirm, MessageBoxButtons.YesNo) != DialogResult.Yes)
            {
                currentKeysPressed.Clear(); // modal dialogs leave key-reading in odd states. Clears it.
                DialogResult = DialogResult.None;
                return;
            }

            currentKeysPressed.Clear(); // modal dialogs leave key-reading in odd states. Clears it.

            //Disables the button so it can't accidentally be called twice.
            //Ensures settings will be saved.
            bttnDone.Enabled = false;

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

        private void BttnDone_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.DoneTip);
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
        /// Opens a dialog to edit brush scripts.
        /// </summary>
        private void BttnSetScript_Click(object sender, EventArgs e)
        {
            var scriptsDialog = new EditScriptDialog(currentBrushScripts);
            if (scriptsDialog.ShowDialog() == DialogResult.OK)
            {
                UpdateBrushScripts(scriptsDialog.GetScriptsAfterDialogOK());

                // If editing a saved brush, save changes immediately.
                if (currentBrushPath != null && !PersistentSettings.defaultBrushes.ContainsKey(currentBrushPath))
                {
                    settings.CustomBrushes[currentBrushPath].BrushScripts = currentBrushScripts;
                }
            }
            currentKeysPressed.Clear(); // modal dialogs leave key-reading in odd states. Clears it.
        }

        private void BttnSetScript_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.SetScriptTip);
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

            // Clears tool states
            isUserDrawing.started = false;
            isUserDrawing.canvasChanged = false;
            isUserDrawing.stagedChanged = false;
            lineOrigins.points.Clear();
            lineOrigins.dragIndex = null;

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

                    if (effectToDraw.Effect == null && activeTool != Tool.CloneStamp)
                    {
                        DrawingUtils.ColorImage(bmpStaged, ColorBgra.Black, 0);
                    }
                    else
                    {
                        if (activeTool == Tool.CloneStamp)
                        {
                            DrawingUtils.OverwriteBits(bmpCommitted, bmpStaged);
                        }
                        if (effectToDraw.Effect != null)
                        {
                            ActiveEffectRender();
                        }
                    }
                }

                displayCanvas.Refresh();
            }
            else
            {
                ThemedMessageBox.Show(Strings.RedoFileNotFoundError, Text, MessageBoxButtons.OK);
                currentKeysPressed.Clear(); // modal dialogs leave key-reading in odd states. Clears it.
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
            UpdateTooltip(CommandTarget.RedoAction, Strings.RedoTip);
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
            currentKeysPressed.Clear(); // avoids issues with key interception from the dialog.
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
            UpdateTooltip(CommandTarget.SymmetryMode, Strings.SymmetryTip);
        }

        private void BttnToolBrush_Click(object sender, EventArgs e)
        {
            SwitchTool(Tool.Brush);
        }

        private void BttnToolBrush_MouseEnter(object sender, EventArgs e)
        {
            static bool filter(Command shortcut)
            {
                int index = (int)Tool.Brush;
                return shortcut.Target == CommandTarget.SelectedTool &&
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
            static bool filter(Command shortcut)
            {
                int index = (int)Tool.ColorPicker;
                return shortcut.Target == CommandTarget.SelectedTool &&
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
            static bool filter(Command shortcut)
            {
                int index = (int)Tool.Eraser;
                return shortcut.Target == CommandTarget.SelectedTool &&
                    (shortcut.ActionData.Contains($"{index}|set") ||
                    (shortcut.ActionData.Contains("cycle") && shortcut.ActionData.Contains(index.ToString())));
            }

            UpdateTooltip(filter, Strings.ToolEraserTip);
        }

        private void BttnToolCloneStamp_Click(object sender, EventArgs e)
        {
            SwitchTool(Tool.CloneStamp);
        }

        private void BttnToolCloneStamp_MouseEnter(object sender, EventArgs e)
        {
            static bool filter(Command shortcut)
            {
                int index = (int)Tool.CloneStamp;
                return shortcut.Target == CommandTarget.SelectedTool &&
                    (shortcut.ActionData.Contains($"{index}|set") ||
                    (shortcut.ActionData.Contains("cycle") && shortcut.ActionData.Contains(index.ToString())));
            }

            UpdateTooltip(filter, Strings.ToolCloneStampTip);
        }

        private void BttnToolLine_Click(object sender, EventArgs e)
        {
            SwitchTool(Tool.Line);
        }

        private void BttnToolLine_MouseEnter(object sender, EventArgs e)
        {
            static bool filter(Command shortcut)
            {
                int index = (int)Tool.Line;
                return shortcut.Target == CommandTarget.SelectedTool &&
                    (shortcut.ActionData.Contains($"{index}|set") ||
                    (shortcut.ActionData.Contains("cycle") && shortcut.ActionData.Contains(index.ToString())));
            }

            UpdateTooltip(filter, Strings.ToolLineTip);
        }

        private void BttnToolOrigin_Click(object sender, EventArgs e)
        {
            SwitchTool(Tool.SetSymmetryOrigin);
        }

        private void BttnToolOrigin_MouseEnter(object sender, EventArgs e)
        {
            static bool filter(Command shortcut)
            {
                int index = (int)Tool.SetSymmetryOrigin;
                return shortcut.Target == CommandTarget.SelectedTool &&
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

            // Clears tool states
            isUserDrawing.started = false;
            isUserDrawing.canvasChanged = false;
            isUserDrawing.stagedChanged = false;
            lineOrigins.points.Clear();
            lineOrigins.dragIndex = null;

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

                    if (effectToDraw.Effect == null && activeTool != Tool.CloneStamp)
                    {
                        DrawingUtils.ColorImage(bmpStaged, ColorBgra.Black, 0);
                    }
                    else
                    {
                        if (activeTool == Tool.CloneStamp)
                        {
                            DrawingUtils.OverwriteBits(bmpCommitted, bmpStaged);
                        }
                        if (effectToDraw.Effect != null)
                        {
                            ActiveEffectRender();
                        }
                    }
                }

                displayCanvas.Refresh();
            }
            else
            {
                ThemedMessageBox.Show(Strings.RedoFileNotFoundError, Text, MessageBoxButtons.OK);
                currentKeysPressed.Clear(); // modal dialogs leave key-reading in odd states. Clears it.
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
            UpdateTooltip(CommandTarget.UndoAction, Strings.UndoTip);
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
            UpdateTooltip(CommandTarget.ColorizeBrush, Strings.ColorizeBrushTip);
        }

        private void ChkbxColorInfluenceHue_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.ColorInfluenceHue, Strings.ColorInfluenceHTip);
        }

        private void ChkbxColorInfluenceSat_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.ColorInfluenceSat, Strings.ColorInfluenceSTip);
        }

        private void ChkbxColorInfluenceVal_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.ColorInfluenceVal, Strings.ColorInfluenceVTip);
        }

        private void ChkbxDitherDraw_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.DitherDraw, Strings.DitherDrawTip);
        }

        private void ChkbxLockAlpha_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.LockAlphaTip);
        }

        private void ChkbxLockR_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.DoLockR, Strings.LockRTip);
        }

        private void ChkbxLockG_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.DoLockG, Strings.LockGTip);
        }

        private void ChkbxLockB_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.DoLockB, Strings.LockBTip);
        }

        private void ChkbxLockHue_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.DoLockHue, Strings.LockHueTip);
        }

        private void ChkbxLockSat_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.DoLockSat, Strings.LockSatTip);
        }

        private void ChkbxLockVal_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.DoLockVal, Strings.LockValTip);
        }

        private void ChkbxOrientToMouse_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.RotateWithMouse, Strings.OrientToMouseTip);
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
                var entry = paletteOptions[cmbxPaletteDropdown.SelectedIndex].Item2;
                if (entry.SpecialType == PaletteSpecialType.None)
                {
                    LoadPalette(entry.Location);
                }
                else
                {
                    GeneratePalette(entry.SpecialType);
                }
            }

            cmbxPaletteDropdown.Width = TextRenderer.MeasureText(
                paletteOptions[cmbxPaletteDropdown.SelectedIndex].Item1,
                cmbxPaletteDropdown.Font).Width + 24;
            cmbxPaletteDropdown.DropDownWidth = 100;

            menuPalette.Refresh();
        }

        private void ChkbxSeamlessDrawing_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.SeamlessDrawing, Strings.SeamlessDrawingTip);
        }

        private void CmbxPaletteDropdown_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.SwitchPalette, Strings.SwitchPaletteTip);
        }

        private void CmbxSwatchColorTheory_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.ColorSchemeTip);
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
            UpdateTooltip(CommandTarget.SelectedBrushImage, Strings.BrushImageSelectorTip);
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
            UpdateTooltip(CommandTarget.SelectedBrush, Strings.BrushSelectorTip);
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

        private void MenuPalette_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.PickFromPalette, Strings.PaletteTip);
        }

        private void SliderBrushDensity_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.BrushStrokeDensity, Strings.BrushDensityTip);
        }

        private void SliderBrushFlow_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.Flow, Strings.BrushFlowTip);
        }

        private void SliderBrushFlow_ValueChanged(object sender, float e)
        {
            UpdateBrushImage();
        }

        private void SliderBrushOpacity_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.BrushOpacity, Strings.BrushOpacityTip);
        }

        private void SliderBrushOpacity_ValueChanged(object sender, float e)
        {
            UpdateBrushColor(Color.FromArgb((int)sliderBrushOpacity.Value, menuActiveColors.Swatches[0]), false, true);
        }

        private void SliderBrushSize_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.Size, Strings.BrushSizeTip);
        }

        private void SliderBrushSize_ValueChanged(object sender, float e)
        {
            UpdateBrushImage();
        }

        private void SliderBrushRotation_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.Rotation, Strings.BrushRotationTip);
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
            UpdateTooltip(CommandTarget.CanvasZoom, Strings.CanvasZoomTip);
        }

        private void SliderCanvasZoom_ValueChanged(object sender, float e)
        {
            Zoom(0, false);
        }

        private void SliderCanvasAngle_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.CanvasRotation, Strings.CanvasAngleTip);
        }

        private void SliderCanvasAngle_ValueChanged(object sender, float e)
        {
            if (activeTool == Tool.Line)
            {
                if (lineOrigins.points.Count == 2)
                {
                    MergeStaged();
                }

                lineOrigins.points.Clear();
                lineOrigins.dragIndex = null;
                contexts.Remove(CommandContext.LineToolConfirmStage);
                contexts.Add(CommandContext.LineToolUnstartedStage);
            }

            displayCanvas.Refresh();
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
            UpdateTooltip(CommandTarget.ColorInfluence, Strings.ColorInfluenceTip);
        }

        private void SliderColorV_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Strings.ValTip);
        }

        private void SliderMinDrawDistance_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.MinDrawDistance, Strings.MinDrawDistanceTip);
        }

        private void SliderRandHorzShift_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.JitterHorSpray, Strings.RandHorzShiftTip);
        }

        private void SliderJitterMaxBlue_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.JitterBlueMax, Strings.JitterBlueTip);
        }

        private void SliderJitterMaxGreen_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.JitterGreenMax, Strings.JitterGreenTip);
        }

        private void SliderJitterMaxRed_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.JitterRedMax, Strings.JitterRedTip);
        }

        private void SliderRandFlowLoss_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.JitterFlowLoss, Strings.RandFlowLossTip);
        }

        private void SliderRandMaxSize_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.JitterMaxSize, Strings.RandMaxSizeTip);
        }

        private void SliderJitterMinBlue_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.JitterBlueMin, Strings.JitterBlueTip);
        }

        private void SliderJitterMinGreen_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.JitterGreenMin, Strings.JitterGreenTip);
        }

        private void SliderJitterMaxHue_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.JitterHueMax, Strings.JitterHueTip);
        }

        private void SliderJitterMinHue_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.JitterHueMin, Strings.JitterHueTip);
        }

        private void SliderJitterMinRed_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.JitterRedMin, Strings.JitterRedTip);
        }

        private void SliderJitterMaxSat_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.JitterSatMax, Strings.JitterSaturationTip);
        }

        private void SliderJitterMinSat_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.JitterSatMin, Strings.JitterSaturationTip);
        }

        private void SliderJitterMaxVal_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.JitterValMax, Strings.JitterValueTip);
        }

        private void SliderJitterMinVal_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.JitterValMin, Strings.JitterValueTip);
        }

        private void SliderRandMinSize_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.JitterMinSize, Strings.RandMinSizeTip);
        }

        private void SliderRandRotLeft_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.JitterRotLeft, Strings.RandRotLeftTip);
        }

        private void SliderRandRotRight_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.JitterRotRight, Strings.RandRotRightTip);
        }

        private void SliderRandVertShift_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.JitterVerSpray, Strings.RandVertShiftTip);
        }

        private void SliderShiftFlow_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.FlowShift, Strings.ShiftFlowTip);
        }

        private void SliderShiftRotation_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.RotShift, Strings.ShiftRotationTip);
        }

        private void SliderShiftSize_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.SizeShift, Strings.ShiftSizeTip);
        }

        private void SliderTabPressureBrushSize_LostFocus(object sender, EventArgs e)
        {
            // Included in brush size calculation in this function when on.
            if (tabPressureConstraints.ContainsKey(CommandTarget.Size) &&
                tabPressureConstraints[CommandTarget.Size].handleMethod != ConstraintValueHandlingMethod.DoNothing)
            {
                UpdateBrushImage(false);
            }
        }

        private void SwatchPrimaryColor_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.Color, Strings.BrushPrimaryColorTip);
        }

        private void TxtbxColorHexfield_ColorUpdatedByText()
        {
            UpdateBrushColor(txtbxColorHexfield.AssociatedColor, true);
        }

        private void TxtbxColorHexfield_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.Color, Strings.ColorHexfieldTip);
        }

        private void WheelColor_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(CommandTarget.Color, Strings.ColorWheelTip);
        }
        #endregion
    }
}