using BrushFactory.Abr;
using BrushFactory.Gui;
using BrushFactory.Interop;
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

        List<PointF> symmetryOrigins;

        /// <summary>
        /// Where to draw around for symmetry.
        /// </summary>
        PointF symmetryOrigin = PointF.Empty;

        /// <summary>
        /// The tablet service instance used to detect and work with tablets.
        /// </summary>
        private TabletService tabletService;

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
        private Panel displayCanvasBG;

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
        private DoubleBufferedListView bttnBrushSelector;
        private Panel panelBrushAddPickColor;
        private CheckBox chkbxColorizeBrush;
        private Button bttnAddBrushes;
        private ProgressBar brushLoadProgressBar;
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
        private Button bttnCustomBrushLocations;
        private Button bttnClearBrushes;
        private Button bttnClearSettings;
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
            smoothingMethods.Add(new InterpolationItem(Localization.Strings.SmoothingNormal, InterpolationMode.Bilinear));
            smoothingMethods.Add(new InterpolationItem(Localization.Strings.SmoothingHigh, InterpolationMode.HighQualityBicubic));
            smoothingMethods.Add(new InterpolationItem(Localization.Strings.SmoothingJagged, InterpolationMode.NearestNeighbor));
            cmbxBrushSmoothing.DataSource = smoothingMethods;
            cmbxBrushSmoothing.DisplayMember = "Name";
            cmbxBrushSmoothing.ValueMember = "Method";

            //Configures items for the symmetry options combobox.
            symmetryOrigins = new List<PointF>();
            symmetryOptions = new BindingList<Tuple<string, SymmetryMode>>();
            symmetryOptions.Add(new Tuple<string, SymmetryMode>(Localization.Strings.SymmetryNone, SymmetryMode.None));
            symmetryOptions.Add(new Tuple<string, SymmetryMode>(Localization.Strings.SymmetryHorizontal, SymmetryMode.Horizontal));
            symmetryOptions.Add(new Tuple<string, SymmetryMode>(Localization.Strings.SymmetryVertical, SymmetryMode.Vertical));
            symmetryOptions.Add(new Tuple<string, SymmetryMode>(Localization.Strings.SymmetryBoth, SymmetryMode.Star2));
            symmetryOptions.Add(new Tuple<string, SymmetryMode>(Localization.Strings.SymmetrySetPoints, SymmetryMode.SetPoints));
            symmetryOptions.Add(new Tuple<string, SymmetryMode>(Localization.Strings.Symmetry3pt, SymmetryMode.Star3));
            symmetryOptions.Add(new Tuple<string, SymmetryMode>(Localization.Strings.Symmetry4pt, SymmetryMode.Star4));
            symmetryOptions.Add(new Tuple<string, SymmetryMode>(Localization.Strings.Symmetry5pt, SymmetryMode.Star5));
            symmetryOptions.Add(new Tuple<string, SymmetryMode>(Localization.Strings.Symmetry6pt, SymmetryMode.Star6));
            symmetryOptions.Add(new Tuple<string, SymmetryMode>(Localization.Strings.Symmetry7pt, SymmetryMode.Star7));
            symmetryOptions.Add(new Tuple<string, SymmetryMode>(Localization.Strings.Symmetry8pt, SymmetryMode.Star8));
            symmetryOptions.Add(new Tuple<string, SymmetryMode>(Localization.Strings.Symmetry9pt, SymmetryMode.Star9));
            symmetryOptions.Add(new Tuple<string, SymmetryMode>(Localization.Strings.Symmetry10pt, SymmetryMode.Star10));
            symmetryOptions.Add(new Tuple<string, SymmetryMode>(Localization.Strings.Symmetry11pt, SymmetryMode.Star11));
            symmetryOptions.Add(new Tuple<string, SymmetryMode>(Localization.Strings.Symmetry12pt, SymmetryMode.Star12));
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
            bttnBrushControls.UpdateAccordion(Localization.Strings.AccordionBrush, false, new Control[] { panelBrush });
            bttnSpecialSettings.UpdateAccordion(Localization.Strings.AccordionSpecialSettings, true, new Control[] { panelSpecialSettings });
            bttnJitterBasicsControls.UpdateAccordion(Localization.Strings.AccordionJitterBasics, true, new Control[] { panelJitterBasics });
            bttnJitterColorControls.UpdateAccordion(Localization.Strings.AccordionJitterColor, true, new Control[] { panelJitterColor });
            bttnShiftBasicsControls.UpdateAccordion(Localization.Strings.AccordionShiftBasics, true, new Control[] { panelShiftBasics });
            bttnSettings.UpdateAccordion(Localization.Strings.AccordionSettings, true, new Control[] { panelSettings });
            bttnTabAssignPressureControls.UpdateAccordion(Localization.Strings.AccordionTabPressureControls, true, new Control[] { panelTabletAssignPressure });

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
            cmbxTabPressureBrushAlpha.SelectedIndex = token.CmbxTabPressureBrushAlpha;
            cmbxTabPressureBrushDensity.SelectedIndex = token.CmbxTabPressureBrushDensity;
            cmbxTabPressureBrushRotation.SelectedIndex = token.CmbxTabPressureBrushRotation;
            cmbxTabPressureBrushSize.SelectedIndex = token.CmbxTabPressureBrushSize;
            cmbxTabPressureBlueJitter.SelectedIndex = token.CmbxTabPressureBlueJitter;
            cmbxTabPressureGreenJitter.SelectedIndex = token.CmbxTabPressureGreenJitter;
            cmbxTabPressureHueJitter.SelectedIndex = token.CmbxTabPressureHueJitter;
            cmbxTabPressureMinDrawDistance.SelectedIndex = token.CmbxTabPressureMinDrawDistance;
            cmbxTabPressureRedJitter.SelectedIndex = token.CmbxTabPressureRedJitter;
            cmbxTabPressureSatJitter.SelectedIndex = token.CmbxTabPressureSatJitter;
            cmbxTabPressureValueJitter.SelectedIndex = token.CmbxTabPressureValueJitter;
            cmbxTabPressureRandHorShift.SelectedIndex = token.CmbxTabPressureRandHorShift;
            cmbxTabPressureRandMaxSize.SelectedIndex = token.CmbxTabPressureRandMaxSize;
            cmbxTabPressureRandMinAlpha.SelectedIndex = token.CmbxTabPressureRandMinAlpha;
            cmbxTabPressureRandMinSize.SelectedIndex = token.CmbxTabPressureRandMinSize;
            cmbxTabPressureRandRotLeft.SelectedIndex = token.CmbxTabPressureRandRotLeft;
            cmbxTabPressureRandRotRight.SelectedIndex = token.CmbxTabPressureRandRotRight;
            cmbxTabPressureRandVerShift.SelectedIndex = token.CmbxTabPressureRandVerShift;
            spinTabPressureBrushAlpha.Value = token.TabPressureBrushAlpha;
            spinTabPressureBrushDensity.Value = token.TabPressureBrushDensity;
            spinTabPressureBrushRotation.Value = token.TabPressureBrushRotation;
            spinTabPressureBrushSize.Value = token.TabPressureBrushSize;
            spinTabPressureMaxBlueJitter.Value = token.TabPressureMaxBlueJitter;
            spinTabPressureMaxGreenJitter.Value = token.TabPressureMaxGreenJitter;
            spinTabPressureMaxHueJitter.Value = token.TabPressureMaxHueJitter;
            spinTabPressureMaxRedJitter.Value = token.TabPressureMaxRedJitter;
            spinTabPressureMaxSatJitter.Value = token.TabPressureMaxSatJitter;
            spinTabPressureMaxValueJitter.Value = token.TabPressureMaxValueJitter;
            spinTabPressureMinBlueJitter.Value = token.TabPressureMinBlueJitter;
            spinTabPressureMinDrawDistance.Value = token.TabPressureMinDrawDistance;
            spinTabPressureMinGreenJitter.Value = token.TabPressureMinGreenJitter;
            spinTabPressureMinHueJitter.Value = token.TabPressureMinHueJitter;
            spinTabPressureMinRedJitter.Value = token.TabPressureMinRedJitter;
            spinTabPressureMinSatJitter.Value = token.TabPressureMinSatJitter;
            spinTabPressureMinValueJitter.Value = token.TabPressureMinValueJitter;
            spinTabPressureRandHorShift.Value = token.TabPressureRandHorShift;
            spinTabPressureRandMaxSize.Value = token.TabPressureRandMaxSize;
            spinTabPressureRandMinAlpha.Value = token.TabPressureRandMinAlpha;
            spinTabPressureRandMinSize.Value = token.TabPressureRandMinSize;
            spinTabPressureRandRotLeft.Value = token.TabPressureRandRotLeft;
            spinTabPressureRandRotRight.Value = token.TabPressureRandRotRight;
            spinTabPressureRandVerShift.Value = token.TabPressureRandVerShift;
            cmbxSymmetry.SelectedIndex = (int)token.Symmetry;

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
            token.Symmetry = (SymmetryMode)cmbxSymmetry.SelectedIndex;
            token.CmbxTabPressureBrushAlpha = cmbxTabPressureBrushAlpha.SelectedIndex;
            token.CmbxTabPressureBrushDensity = cmbxTabPressureBrushDensity.SelectedIndex;
            token.CmbxTabPressureBrushRotation = cmbxTabPressureBrushRotation.SelectedIndex;
            token.CmbxTabPressureBrushSize = cmbxTabPressureBrushSize.SelectedIndex;
            token.CmbxTabPressureBlueJitter = cmbxTabPressureBlueJitter.SelectedIndex;
            token.CmbxTabPressureGreenJitter = cmbxTabPressureGreenJitter.SelectedIndex;
            token.CmbxTabPressureHueJitter = cmbxTabPressureHueJitter.SelectedIndex;
            token.CmbxTabPressureMinDrawDistance = cmbxTabPressureMinDrawDistance.SelectedIndex;
            token.CmbxTabPressureRedJitter = cmbxTabPressureRedJitter.SelectedIndex;
            token.CmbxTabPressureSatJitter = cmbxTabPressureSatJitter.SelectedIndex;
            token.CmbxTabPressureValueJitter = cmbxTabPressureValueJitter.SelectedIndex;
            token.CmbxTabPressureRandHorShift = cmbxTabPressureRandHorShift.SelectedIndex;
            token.CmbxTabPressureRandMaxSize = cmbxTabPressureRandMaxSize.SelectedIndex;
            token.CmbxTabPressureRandMinAlpha = cmbxTabPressureRandMinAlpha.SelectedIndex;
            token.CmbxTabPressureRandMinSize = cmbxTabPressureRandMinSize.SelectedIndex;
            token.CmbxTabPressureRandRotLeft = cmbxTabPressureRandRotLeft.SelectedIndex;
            token.CmbxTabPressureRandRotRight = cmbxTabPressureRandRotRight.SelectedIndex;
            token.CmbxTabPressureRandVerShift = cmbxTabPressureRandVerShift.SelectedIndex;
            token.TabPressureBrushAlpha = (int)spinTabPressureBrushAlpha.Value;
            token.TabPressureBrushDensity = (int)spinTabPressureBrushDensity.Value;
            token.TabPressureBrushRotation = (int)spinTabPressureBrushRotation.Value;
            token.TabPressureBrushSize = (int)spinTabPressureBrushSize.Value;
            token.TabPressureMaxBlueJitter = (int)spinTabPressureMaxBlueJitter.Value;
            token.TabPressureMaxGreenJitter = (int)spinTabPressureMaxGreenJitter.Value;
            token.TabPressureMaxHueJitter = (int)spinTabPressureMaxHueJitter.Value;
            token.TabPressureMaxRedJitter = (int)spinTabPressureMaxRedJitter.Value;
            token.TabPressureMaxSatJitter = (int)spinTabPressureMaxSatJitter.Value;
            token.TabPressureMaxValueJitter = (int)spinTabPressureMaxValueJitter.Value;
            token.TabPressureMinBlueJitter = (int)spinTabPressureMinBlueJitter.Value;
            token.TabPressureMinDrawDistance = (int)spinTabPressureMinDrawDistance.Value;
            token.TabPressureMinGreenJitter = (int)spinTabPressureMinGreenJitter.Value;
            token.TabPressureMinHueJitter = (int)spinTabPressureMinHueJitter.Value;
            token.TabPressureMinRedJitter = (int)spinTabPressureMinRedJitter.Value;
            token.TabPressureMinSatJitter = (int)spinTabPressureMinSatJitter.Value;
            token.TabPressureMinValueJitter = (int)spinTabPressureMinValueJitter.Value;
            token.TabPressureRandHorShift = (int)spinTabPressureRandHorShift.Value;
            token.TabPressureRandMaxSize = (int)spinTabPressureRandMaxSize.Value;
            token.TabPressureRandMinAlpha = (int)spinTabPressureRandMinAlpha.Value;
            token.TabPressureRandMinSize = (int)spinTabPressureRandMinSize.Value;
            token.TabPressureRandRotLeft = (int)spinTabPressureRandRotLeft.Value;
            token.TabPressureRandRotRight = (int)spinTabPressureRandRotRight.Value;
            token.TabPressureRandVerShift = (int)spinTabPressureRandVerShift.Value;
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
            symmetryOrigin = new PointF(
                EnvironmentParameters.SourceSurface.Width / 2f,
                EnvironmentParameters.SourceSurface.Height / 2f);

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

            UpdateTooltip(string.Empty);

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
            }
            catch (Exception ex)
            {
                if (ex is NullReferenceException)
                {
                    MessageBox.Show(Localization.Strings.SettingsUnavailableError,
                        Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else if (ex is IOException || ex is UnauthorizedAccessException)
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

            if (e.KeyCode == Keys.B)
            {
                SwitchTool(Tool.Brush);
            }
            else if (e.KeyCode == Keys.E)
            {
                SwitchTool(Tool.Eraser);
            }
            else if (e.KeyCode == Keys.K)
            {
                SwitchTool(Tool.ColorPicker);
            }
            else if (e.KeyCode == Keys.O)
            {
                SwitchTool(Tool.SetSymmetryOrigin);

                // The origin points could be displayed relative to the center or current mouse. It's more convenient
                // for the user to display at the mouse position so wherever they click to set a new origin point is
                // exactly where the origin will be when they switch back to brush mode.
                if (cmbxSymmetry.SelectedIndex == (int)SymmetryMode.SetPoints)
                {
                    symmetryOrigin.X = mouseLoc.X / displayCanvasZoom;
                    symmetryOrigin.Y = mouseLoc.Y / displayCanvasZoom;
                }

                // Invalidate to immediately update the symmetry origin guidelines drawn in symmetry mode.
                displayCanvas.Invalidate();
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
        private void DrawBrush(PointF loc, int radius)
        {
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
                UpdateBrush();
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

            //This is used to randomly rotate the image by some amount.
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
                g.InterpolationMode = (InterpolationMode)cmbxBrushSmoothing.SelectedValue;

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
                        //Gets the center of the image.
                        PointF origin = new PointF(
                            symmetryOrigin.X - (newRadius / 2f),
                            symmetryOrigin.Y - (newRadius / 2f));

                        //Gets the drawn location relative to center.
                        PointF locRelativeToOrigin = new PointF(
                            loc.X - origin.X,
                            loc.Y - origin.Y);

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
                                origin.X + (float)(dist * Math.Cos(angle)),
                                origin.Y + (float)(dist * Math.Sin(angle)),
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
                            //Gets the center of the image.
                            PointF center = new PointF(
                                (bmpCurrentDrawing.Width / 2f) - (newRadius / 2f),
                                (bmpCurrentDrawing.Height / 2f) - (newRadius / 2f));

                            //Gets the drawn location relative to center.
                            PointF locRelativeToCenter = new PointF(
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
                            int numPoints = cmbxSymmetry.SelectedIndex - 2;
                            double angleIncrease = (2 * Math.PI) / numPoints;
                            for (int i = 0; i < numPoints; i++)
                            {
                                float posX = (float)(center.X + dist * Math.Cos(angle));
                                float posY = (float)(center.Y + dist * Math.Sin(angle));

                                destination[0] = new PointF(posX, posY);
                                destination[1] = new PointF(posX + scaleFactor, posY);
                                destination[2] = new PointF(posX, posY + scaleFactor);

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

            if (settings?.UseDefaultBrushes ?? true)
            {
                loadedBrushes.Add(new BrushSelectorItem(Localization.Strings.DefaultBrushLine, Resources.BrLine));
                loadedBrushes.Add(new BrushSelectorItem(Localization.Strings.DefaultBrushSmallDots, Resources.BrDotsTiny));
                loadedBrushes.Add(new BrushSelectorItem(Localization.Strings.DefaultBrushBigDots, Resources.BrDotsBig));
                loadedBrushes.Add(new BrushSelectorItem(Localization.Strings.DefaultBrushSpark, Resources.BrSpark));
                loadedBrushes.Add(new BrushSelectorItem(Localization.Strings.DefaultBrushGravel, Resources.BrGravel));
                loadedBrushes.Add(new BrushSelectorItem(Localization.Strings.DefaultBrushRain, Resources.BrRain));
                loadedBrushes.Add(new BrushSelectorItem(Localization.Strings.DefaultBrushGrass, Resources.BrGrass));
                loadedBrushes.Add(new BrushSelectorItem(Localization.Strings.DefaultBrushSmoke, Resources.BrSmoke));
                loadedBrushes.Add(new BrushSelectorItem(Localization.Strings.DefaultBrushScales, Resources.BrScales));
                loadedBrushes.Add(new BrushSelectorItem(Localization.Strings.DefaultBrushFractalDirt, Resources.BrFractalDirt));
                loadedBrushes.Add(new BrushSelectorItem(Localization.Strings.DefaultBrushDirt3, Resources.BrDirt3));
                loadedBrushes.Add(new BrushSelectorItem(Localization.Strings.DefaultBrushDirt2, Resources.BrDirt2));
                loadedBrushes.Add(new BrushSelectorItem(Localization.Strings.DefaultBrushDirt, Resources.BrDirt));
                loadedBrushes.Add(new BrushSelectorItem(Localization.Strings.DefaultBrushCracks, Resources.BrCracks));
                loadedBrushes.Add(new BrushSelectorItem(Localization.Strings.DefaultBrushSpiral, Resources.BrSpiral));
                loadedBrushes.Add(new BrushSelectorItem(Localization.Strings.DefaultBrushCircleSegmented, Resources.BrCircleSegmented));
                loadedBrushes.Add(new BrushSelectorItem(Localization.Strings.DefaultBrushCircleSketchy, Resources.BrCircleSketchy));
                loadedBrushes.Add(new BrushSelectorItem(Localization.Strings.DefaultBrushCircleRough, Resources.BrCircleRough));
                loadedBrushes.Add(new BrushSelectorItem(Localization.Strings.DefaultBrushCircleHard, Resources.BrCircleHard));
                loadedBrushes.Add(new BrushSelectorItem(Localization.Strings.DefaultBrushCircleMed, Resources.BrCircleMedium));
            }

            //Loads stored brushes.
            loadedBrushes.Add(new BrushSelectorItem(Localization.Strings.DefaultBrushCircle, Resources.BrCircle));
            bttnBrushSelector.VirtualListSize = loadedBrushes.Count;

            //Loads any custom brushes.
            if (importBrushesFromToken)
            {
                importBrushesFromToken = false;

                ImportBrushesFromFiles(loadedBrushPaths, false, false);
            }
            else
            {
                ImportBrushesFromDirectories(settings?.CustomBrushDirectories ?? new HashSet<string>());
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
            this.bttnToolBrush = new System.Windows.Forms.Button();
            this.dummyImageList = new System.Windows.Forms.ImageList(this.components);
            this.panelUndoRedoOkCancel = new System.Windows.Forms.FlowLayoutPanel();
            this.bttnUndo = new System.Windows.Forms.Button();
            this.bttnRedo = new System.Windows.Forms.Button();
            this.bttnOk = new System.Windows.Forms.Button();
            this.bttnCancel = new System.Windows.Forms.Button();
            this.brushLoadingWorker = new System.ComponentModel.BackgroundWorker();
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
            this.bttnBrushSelector = new BrushFactory.DoubleBufferedListView();
            this.panelBrushAddPickColor = new System.Windows.Forms.Panel();
            this.chkbxColorizeBrush = new System.Windows.Forms.CheckBox();
            this.bttnAddBrushes = new System.Windows.Forms.Button();
            this.brushLoadProgressBar = new System.Windows.Forms.ProgressBar();
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
            this.bttnCustomBrushLocations = new System.Windows.Forms.Button();
            this.bttnClearBrushes = new System.Windows.Forms.Button();
            this.bttnClearSettings = new System.Windows.Forms.Button();
            this.displayCanvasBG.SuspendLayout();
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
            this.txtTooltip.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.txtTooltip.ForeColor = System.Drawing.SystemColors.HighlightText;
            this.txtTooltip.Name = "txtTooltip";
            // 
            // displayCanvasBG
            // 
            resources.ApplyResources(this.displayCanvasBG, "displayCanvasBG");
            this.displayCanvasBG.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(207)))), ((int)(((byte)(207)))), ((int)(((byte)(207)))));
            this.displayCanvasBG.Controls.Add(this.txtTooltip);
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
            // bttnToolBrush
            // 
            this.bttnToolBrush.BackColor = System.Drawing.SystemColors.ButtonShadow;
            this.bttnToolBrush.Image = global::BrushFactory.Properties.Resources.ToolBrush;
            resources.ApplyResources(this.bttnToolBrush, "bttnToolBrush");
            this.bttnToolBrush.Name = "bttnToolBrush";
            this.bttnToolBrush.UseVisualStyleBackColor = false;
            this.bttnToolBrush.Click += new System.EventHandler(this.bttnToolBrush_Click);
            this.bttnToolBrush.MouseEnter += new System.EventHandler(this.bttnToolBrush_MouseEnter);
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
            // brushLoadingWorker
            // 
            this.brushLoadingWorker.WorkerReportsProgress = true;
            this.brushLoadingWorker.WorkerSupportsCancellation = true;
            this.brushLoadingWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(this.BrushLoadingWorker_DoWork);
            this.brushLoadingWorker.ProgressChanged += new System.ComponentModel.ProgressChangedEventHandler(this.BrushLoadingWorker_ProgressChanged);
            this.brushLoadingWorker.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.BrushLoadingWorker_RunWorkerCompleted);
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
            this.bttnToolOrigin.Click += new System.EventHandler(this.bttnToolOrigin_Click);
            this.bttnToolOrigin.MouseEnter += new System.EventHandler(this.bttnToolOrigin_MouseEnter);
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
            this.panelBrush.Controls.Add(this.bttnBrushSelector);
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
            // panelBrushAddPickColor
            // 
            resources.ApplyResources(this.panelBrushAddPickColor, "panelBrushAddPickColor");
            this.panelBrushAddPickColor.Controls.Add(this.chkbxColorizeBrush);
            this.panelBrushAddPickColor.Controls.Add(this.bttnAddBrushes);
            this.panelBrushAddPickColor.Controls.Add(this.brushLoadProgressBar);
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
            // bttnAddBrushes
            // 
            this.bttnAddBrushes.Image = global::BrushFactory.Properties.Resources.AddBrushIcon;
            resources.ApplyResources(this.bttnAddBrushes, "bttnAddBrushes");
            this.bttnAddBrushes.Name = "bttnAddBrushes";
            this.bttnAddBrushes.UseVisualStyleBackColor = true;
            this.bttnAddBrushes.Click += new System.EventHandler(this.BttnAddBrushes_Click);
            this.bttnAddBrushes.MouseEnter += new System.EventHandler(this.BttnAddBrushes_MouseEnter);
            // 
            // brushLoadProgressBar
            // 
            resources.ApplyResources(this.brushLoadProgressBar, "brushLoadProgressBar");
            this.brushLoadProgressBar.Name = "brushLoadProgressBar";
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
            this.panelSpecialSettings.Controls.Add(this.txtBrushDensity);
            this.panelSpecialSettings.Controls.Add(this.sliderBrushDensity);
            this.panelSpecialSettings.Controls.Add(this.cmbxSymmetry);
            this.panelSpecialSettings.Controls.Add(this.cmbxBrushSmoothing);
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
            this.sliderJitterMinHue.ValueChanged += new System.EventHandler(this.sliderJitterMinHue_ValueChanged);
            this.sliderJitterMinHue.MouseEnter += new System.EventHandler(this.sliderJitterMinHue_MouseEnter);
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
            this.sliderJitterMinSat.ValueChanged += new System.EventHandler(this.sliderJitterMinSat_ValueChanged);
            this.sliderJitterMinSat.MouseEnter += new System.EventHandler(this.sliderJitterMinSat_MouseEnter);
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
            this.sliderJitterMinVal.ValueChanged += new System.EventHandler(this.sliderJitterMinVal_ValueChanged);
            this.sliderJitterMinVal.MouseEnter += new System.EventHandler(this.sliderJitterMinVal_MouseEnter);
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
            this.panelSettings.Controls.Add(this.bttnCustomBrushLocations);
            this.panelSettings.Controls.Add(this.bttnClearBrushes);
            this.panelSettings.Controls.Add(this.bttnClearSettings);
            this.panelSettings.Name = "panelSettings";
            // 
            // bttnCustomBrushLocations
            // 
            resources.ApplyResources(this.bttnCustomBrushLocations, "bttnCustomBrushLocations");
            this.bttnCustomBrushLocations.Name = "bttnCustomBrushLocations";
            this.bttnCustomBrushLocations.UseVisualStyleBackColor = true;
            this.bttnCustomBrushLocations.Click += new System.EventHandler(this.BttnPreferences_Click);
            this.bttnCustomBrushLocations.MouseEnter += new System.EventHandler(this.BttnPreferences_MouseEnter);
            // 
            // bttnClearBrushes
            // 
            resources.ApplyResources(this.bttnClearBrushes, "bttnClearBrushes");
            this.bttnClearBrushes.Name = "bttnClearBrushes";
            this.bttnClearBrushes.UseVisualStyleBackColor = true;
            this.bttnClearBrushes.Click += new System.EventHandler(this.BttnClearBrushes_Click);
            this.bttnClearBrushes.MouseEnter += new System.EventHandler(this.BttnClearBrushes_MouseEnter);
            // 
            // bttnClearSettings
            // 
            resources.ApplyResources(this.bttnClearSettings, "bttnClearSettings");
            this.bttnClearSettings.Name = "bttnClearSettings";
            this.bttnClearSettings.UseVisualStyleBackColor = true;
            this.bttnClearSettings.Click += new System.EventHandler(this.BttnClearSettings_Click);
            this.bttnClearSettings.MouseEnter += new System.EventHandler(this.BttnClearSettings_MouseEnter);
            // 
            // WinBrushFactory
            // 
            this.AcceptButton = this.bttnOk;
            resources.ApplyResources(this, "$this");
            this.BackColor = System.Drawing.SystemColors.ControlLight;
            this.CancelButton = this.bttnCancel;
            this.Controls.Add(this.panelAllSettingsContainer);
            this.Controls.Add(this.displayCanvasBG);
            this.DoubleBuffered = true;
            this.KeyPreview = true;
            this.MaximizeBox = true;
            this.Name = "WinBrushFactory";
            this.displayCanvasBG.ResumeLayout(false);
            this.displayCanvasBG.PerformLayout();
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
        /// Determines whether the specified key is down.
        /// </summary>
        private static bool IsKeyDown(Keys key)
        {
            return SafeNativeMethods.GetKeyState((int)key) < 0;
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
        /// Recreates the brush with color and alpha effects applied.
        /// </summary>
        private void UpdateBrush()
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
            displayCanvasZoom = newZoomFactor;
            txtCanvasZoom.Text = string.Format(
                "{0} {1:p0}", Localization.Strings.CanvasZoom, newZoomFactor);

            //Gets the new width and height, adjusted for zooming.
            float zoomWidth = bmpCurrentDrawing.Width * newZoomFactor;
            float zoomHeight = bmpCurrentDrawing.Height * newZoomFactor;

            PointF zoomingPoint = isWheelZooming
                ? mouseLoc
                : new PointF(
                    displayCanvasBG.ClientSize.Width / 2f - displayCanvas.Location.X,
                    displayCanvasBG.ClientSize.Height / 2f - displayCanvas.Location.Y);

            int zoomX = (int)(displayCanvas.Location.X + zoomingPoint.X -
                zoomingPoint.X * zoomWidth / displayCanvas.Width);
            int zoomY = (int)(displayCanvas.Location.Y + zoomingPoint.Y -
                zoomingPoint.Y * zoomHeight / displayCanvas.Height);

            isWheelZooming = false;

            //Sets the new canvas position (center) and size using zoom.
            displayCanvas.Bounds = new Rectangle(zoomX, zoomY, (int)zoomWidth, (int)zoomHeight);
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
            if (e.Button == MouseButtons.Right)
            {
                // Removes points near the mouse.
                if (activeTool == Tool.SetSymmetryOrigin && cmbxSymmetry.SelectedIndex == (int)SymmetryMode.SetPoints)
                {
                    // Deletes all points.
                    if (IsKeyDown(Keys.ControlKey))
                    {
                        symmetryOrigins.Clear();
                    }
                    // Deletes points within a small radius of the mouse.
                    else
                    {
                        for (int i = 0; i < symmetryOrigins.Count; i++)
                        {
                            double radius = Math.Sqrt(
                                Math.Pow(mouseLoc.X / displayCanvasZoom - (symmetryOrigin.X + symmetryOrigins[i].X), 2) +
                                Math.Pow(mouseLoc.Y / displayCanvasZoom - (symmetryOrigin.Y + symmetryOrigins[i].Y), 2));

                            if (radius <= 15 / displayCanvasZoom)
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
                (e.Button == MouseButtons.Left && IsKeyDown(Keys.ControlKey)))
            {
                isUserPanning = true;
                mouseLocPrev = e.Location;
            }

            else if (e.Button == MouseButtons.Left)
            {
                mouseLocPrev = e.Location;

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
                            mouseLocPrev.X / displayCanvasZoom,
                            mouseLocPrev.Y / displayCanvasZoom),
                            finalBrushSize);
                    }
                }
                // Grabs the color under the mouse.
                else if (activeTool == Tool.ColorPicker)
                {
                    GetColorFromCanvas(new Point(
                        (int)(mouseLocPrev.X / displayCanvasZoom),
                        (int)(mouseLocPrev.Y / displayCanvasZoom)));

                    SwitchTool(lastTool);
                }
                // Changes the symmetry origin or adds a new origin (if in SetPoints symmetry mode).
                else if (activeTool == Tool.SetSymmetryOrigin)
                {
                    if (cmbxSymmetry.SelectedIndex == (int)SymmetryMode.SetPoints)
                    {
                        symmetryOrigins.Add(new PointF(
                            e.Location.X / displayCanvasZoom - symmetryOrigin.X,
                            e.Location.Y / displayCanvasZoom - symmetryOrigin.Y));
                    }
                    else
                    {
                        symmetryOrigin = new PointF(
                            Utils.ClampF(e.Location.X / displayCanvasZoom, 0, bmpCurrentDrawing.Width),
                            Utils.ClampF(e.Location.Y / displayCanvasZoom, 0, bmpCurrentDrawing.Height)
                        );
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
            mouseLoc = e.Location;

            //Handles panning the screen.
            if (isUserPanning && !displayCanvasBG.ClientRectangle.Contains(displayCanvas.ClientRectangle))
            {
                Rectangle range = GetRange();

                //Moves the drawing region.
                int locx = displayCanvas.Left + (int)Math.Round(mouseLoc.X - mouseLocPrev.X);
                int locy = displayCanvas.Top + (int)Math.Round(mouseLoc.Y - mouseLocPrev.Y);

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
                int finalMinDrawDistance = Utils.Clamp(Utils.GetStrengthMappedValue(sliderMinDrawDistance.Value,
                        (int)spinTabPressureMinDrawDistance.Value,
                        sliderMinDrawDistance.Maximum,
                        tabletPressureRatio,
                        ((CmbxTabletValueType.CmbxEntry)cmbxTabPressureMinDrawDistance.SelectedItem).ValueMember),
                        0, sliderMinDrawDistance.Maximum);

                PointF mouseLocBrushOriginal = mouseLocBrush ?? mouseLocPrev;

                // Doesn't draw unless the minimum drawing distance is met.
                if (finalMinDrawDistance != 0)
                {
                    if (mouseLocBrush.HasValue)
                    {
                        float deltaX = mouseLocBrush.Value.X - mouseLoc.X;
                        float deltaY = mouseLocBrush.Value.Y - mouseLoc.Y;

                        if (Math.Sqrt(deltaX * deltaX + deltaY * deltaY) <
                            finalMinDrawDistance * displayCanvasZoom)
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
                                mouseLoc.X / displayCanvasZoom,
                                mouseLoc.Y / displayCanvasZoom),
                                finalBrushSize);
                        }

                        mouseLocPrev = e.Location;
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

                        double deltaX = (mouseLoc.X - mouseLocPrev.X) / displayCanvasZoom;
                        double deltaY = (mouseLoc.Y - mouseLocPrev.Y) / displayCanvasZoom;
                        double brushWidthFrac = finalBrushSize / (double)finalBrushDensity;
                        double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                        double angle = Math.Atan2(deltaY, deltaX);
                        double xDist = Math.Cos(angle);
                        double yDist = Math.Sin(angle);
                        double numIntervals = distance / (Double.IsNaN(brushWidthFrac) ? 1 : brushWidthFrac);

                        for (int i = 1; i <= (int)numIntervals; i++)
                        {
                            DrawBrush(new PointF(
                                (float)(mouseLocPrev.X / displayCanvasZoom + xDist * brushWidthFrac * i),
                                (float)(mouseLocPrev.Y / displayCanvasZoom + yDist * brushWidthFrac * i)),
                                finalBrushSize);
                        }

                        double extraDist = brushWidthFrac * (numIntervals - (int)numIntervals);

                        // Same as mouse position except for remainder.
                        mouseLoc = new PointF(
                            (float)(e.Location.X - xDist * extraDist * displayCanvasZoom),
                            (float)(e.Location.Y - yDist * extraDist * displayCanvasZoom));
                        mouseLocPrev = mouseLoc;
                    }
                }
            }
            else if (e.Button == MouseButtons.Left
                && activeTool == Tool.SetSymmetryOrigin
                && cmbxSymmetry.SelectedIndex != (int)SymmetryMode.SetPoints)
            {
                symmetryOrigin = new PointF(
                    Utils.ClampF(e.Location.X / displayCanvasZoom, 0, bmpCurrentDrawing.Width),
                    Utils.ClampF(e.Location.Y / displayCanvasZoom, 0, bmpCurrentDrawing.Height)
                );
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
                        Utils.CopyBitmapPure(prevStroke, bmpCurrentDrawing, true);
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
            if ((activeTool == Tool.Brush || activeTool == Tool.Eraser) && !isUserDrawing)
            {
                int radius = (int)(sliderBrushSize.Value * displayCanvasZoom);

                e.Graphics.DrawRectangle(
                    Pens.Black,
                    mouseLoc.X - (radius / 2f),
                    mouseLoc.Y - (radius / 2f),
                    radius,
                    radius);

                e.Graphics.DrawRectangle(
                    Pens.White,
                    mouseLoc.X - (radius / 2f) - 1,
                    mouseLoc.Y - (radius / 2f) - 1,
                    radius + 2,
                    radius + 2);
            }

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
                            new PointF(0, symmetryOrigin.Y * displayCanvasZoom),
                            new PointF(bmpCurrentDrawing.Width * displayCanvasZoom, symmetryOrigin.Y * displayCanvasZoom));
                        e.Graphics.DrawLine(
                            Pens.Red,
                            new PointF(symmetryOrigin.X * displayCanvasZoom, 0),
                            new PointF(symmetryOrigin.X * displayCanvasZoom, bmpCurrentDrawing.Height * displayCanvasZoom));
                    }

                    // Draws a rectangle for each origin point, either relative to the symmetry origin (in
                    // SetSymmetryOrigin tool) or the mouse (with the Brush tool)
                    float pointsDrawnX = (activeTool == Tool.SetSymmetryOrigin) ? symmetryOrigin.X : (mouseLoc.X / displayCanvasZoom);
                    float pointsDrawnY = (activeTool == Tool.SetSymmetryOrigin) ? symmetryOrigin.Y : (mouseLoc.Y / displayCanvasZoom);
                    float zoomCompensation = (activeTool == Tool.SetSymmetryOrigin) ? displayCanvasZoom : displayCanvasZoom;

                    for (int i = 0; i < symmetryOrigins.Count; i++)
                    {
                        e.Graphics.DrawRectangle(
                            Pens.Red,
                            (pointsDrawnX + symmetryOrigins[i].X) * zoomCompensation - 1,
                            (pointsDrawnY + symmetryOrigins[i].Y) * zoomCompensation - 1,
                            2,
                            2);
                    }
                }
                else
                {
                    e.Graphics.DrawLine(
                        Pens.Red,
                        new PointF(0, symmetryOrigin.Y * displayCanvasZoom),
                        new PointF(bmpCurrentDrawing.Width * displayCanvasZoom, symmetryOrigin.Y * displayCanvasZoom));
                    e.Graphics.DrawLine(
                        Pens.Red,
                        new PointF(symmetryOrigin.X * displayCanvasZoom, 0),
                        new PointF(symmetryOrigin.X * displayCanvasZoom, bmpCurrentDrawing.Height * displayCanvasZoom));
                }
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

            UpdateTooltip(string.Empty);
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
            UpdateTooltip(Localization.Strings.AddBrushesTip);
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
            UpdateTooltip(Localization.Strings.BrushColorTip);
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
            UpdateTooltip(Localization.Strings.BrushSelectorTip);
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
            UpdateTooltip(Localization.Strings.BrushSmoothingTip);
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
            UpdateTooltip(Localization.Strings.CancelTip);
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
            UpdateTooltip(Localization.Strings.ClearBrushesTip);
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
            UpdateTooltip(Localization.Strings.ClearSettingsTip);
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
            UpdateTooltip(Localization.Strings.OkTip);
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
                MessageBox.Show(Localization.Strings.SettingsUnavailableError,
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BttnPreferences_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Localization.Strings.CustomBrushLocationsTip);
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
            UpdateTooltip(Localization.Strings.RedoTip);
        }

        private void bttnToolBrush_Click(object sender, EventArgs e)
        {
            SwitchTool(Tool.Brush);
        }

        private void bttnToolBrush_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Localization.Strings.ToolBrushTip);
        }

        private void BttnToolColorPicker_Click(object sender, EventArgs e)
        {
            SwitchTool(Tool.ColorPicker);
        }

        private void BttnToolColorPicker_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Localization.Strings.ColorPickerTip);
        }

        private void BttnToolEraser_Click(object sender, EventArgs e)
        {
            SwitchTool(Tool.Eraser);
        }

        private void BttnToolEraser_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Localization.Strings.ToolEraserTip);
        }

        private void bttnToolOrigin_Click(object sender, EventArgs e)
        {
            SwitchTool(Tool.SetSymmetryOrigin);
        }

        private void bttnToolOrigin_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Localization.Strings.ToolOriginTip);
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
            UpdateTooltip(Localization.Strings.UndoTip);
        }

        private void BttnSymmetry_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Localization.Strings.SymmetryTip);
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
            UpdateTooltip(Localization.Strings.ColorizeBrushTip);
        }

        private void ChkbxLockAlpha_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Localization.Strings.LockAlphaTip);
        }

        private void ChkbxOrientToMouse_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Localization.Strings.OrientToMouseTip);
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

        private void SliderBrushAlpha_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Localization.Strings.BrushAlphaTip);
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
            UpdateTooltip(Localization.Strings.BrushDensityTip);
        }

        private void SliderBrushDensity_ValueChanged(object sender, EventArgs e)
        {
            txtBrushDensity.Text = String.Format("{0} {1}",
                Localization.Strings.BrushDensity,
                sliderBrushDensity.Value);
        }

        private void SliderBrushSize_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Localization.Strings.BrushSizeTip);
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
            UpdateTooltip(Localization.Strings.BrushRotationTip);
        }

        private void SliderBrushRotation_ValueChanged(object sender, EventArgs e)
        {
            txtBrushRotation.Text = String.Format("{0} {1}°",
                Localization.Strings.Rotation,
                sliderBrushRotation.Value);
        }

        private void SliderCanvasZoom_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Localization.Strings.CanvasZoomTip);
        }

        private void SliderCanvasZoom_ValueChanged(object sender, EventArgs e)
        {
            Zoom(0, false);
        }

        private void SliderMinDrawDistance_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Localization.Strings.MinDrawDistanceTip);
        }

        private void SliderMinDrawDistance_ValueChanged(object sender, EventArgs e)
        {
            txtMinDrawDistance.Text = String.Format("{0} {1}",
                Localization.Strings.MinDrawDistance,
                sliderMinDrawDistance.Value);
        }

        private void SliderRandHorzShift_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Localization.Strings.RandHorzShiftTip);
        }

        private void SliderRandHorzShift_ValueChanged(object sender, EventArgs e)
        {
            txtRandHorzShift.Text = String.Format("{0} {1}%",
                Localization.Strings.RandHorzShift,
                sliderRandHorzShift.Value);
        }

        private void SliderJitterMaxBlue_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Localization.Strings.JitterBlueTip);
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
            UpdateTooltip(Localization.Strings.JitterGreenTip);
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
            UpdateTooltip(Localization.Strings.JitterRedTip);
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
            UpdateTooltip(Localization.Strings.RandMaxSizeTip);
        }

        private void SliderRandMaxSize_ValueChanged(object sender, EventArgs e)
        {
            txtRandMaxSize.Text = String.Format("{0} {1}",
                Localization.Strings.RandMaxSize,
                sliderRandMaxSize.Value);
        }

        private void SliderRandMinAlpha_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Localization.Strings.RandMinAlphaTip);
        }

        private void SliderRandMinAlpha_ValueChanged(object sender, EventArgs e)
        {
            txtRandMinAlpha.Text = String.Format("{0} {1}",
                Localization.Strings.RandMinAlpha,
                sliderRandMinAlpha.Value);
        }

        private void SliderJitterMinBlue_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Localization.Strings.JitterBlueTip);
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
            UpdateTooltip(Localization.Strings.JitterGreenTip);
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
            UpdateTooltip(Localization.Strings.JitterHueTip);
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
            UpdateTooltip(Localization.Strings.JitterHueTip);
        }

        private void SliderJitterMinRed_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Localization.Strings.JitterRedTip);
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
            UpdateTooltip(Localization.Strings.JitterSaturationTip);
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
            UpdateTooltip(Localization.Strings.JitterSaturationTip);
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
            UpdateTooltip(Localization.Strings.JitterValueTip);
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
            UpdateTooltip(Localization.Strings.JitterValueTip);
        }

        private void SliderRandMinSize_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Localization.Strings.RandMinSizeTip);
        }

        private void SliderRandMinSize_ValueChanged(object sender, EventArgs e)
        {
            txtRandMinSize.Text = String.Format("{0} {1}",
                Localization.Strings.RandMinSize,
                sliderRandMinSize.Value);
        }

        private void SliderRandRotLeft_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Localization.Strings.RandRotLeftTip);
        }

        private void SliderRandRotLeft_ValueChanged(object sender, EventArgs e)
        {
            txtRandRotLeft.Text = String.Format("{0} {1}°",
                Localization.Strings.RandRotLeft,
                sliderRandRotLeft.Value);
        }

        private void SliderRandRotRight_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Localization.Strings.RandRotRightTip);
        }

        private void SliderRandRotRight_ValueChanged(object sender, EventArgs e)
        {
            txtRandRotRight.Text = String.Format("{0} {1}°",
                Localization.Strings.RandRotRight,
                sliderRandRotRight.Value);
        }

        private void SliderRandVertShift_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Localization.Strings.RandVertShiftTip);
        }

        private void SliderRandVertShift_ValueChanged(object sender, EventArgs e)
        {
            txtRandVertShift.Text = String.Format("{0} {1}%",
                Localization.Strings.RandVertShift,
                sliderRandVertShift.Value);
        }

        private void SliderShiftAlpha_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Localization.Strings.ShiftAlphaTip);
        }

        private void SliderShiftAlpha_ValueChanged(object sender, EventArgs e)
        {
            txtShiftAlpha.Text = String.Format("{0} {1}",
                Localization.Strings.ShiftAlpha,
                sliderShiftAlpha.Value);
        }

        private void SliderShiftRotation_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Localization.Strings.ShiftRotationTip);
        }

        private void SliderShiftRotation_ValueChanged(object sender, EventArgs e)
        {
            txtShiftRotation.Text = String.Format("{0} {1}°",
                Localization.Strings.ShiftRotation,
                sliderShiftRotation.Value);
        }

        private void SliderShiftSize_MouseEnter(object sender, EventArgs e)
        {
            UpdateTooltip(Localization.Strings.ShiftSizeTip);
        }

        private void SliderShiftSize_ValueChanged(object sender, EventArgs e)
        {
            txtShiftSize.Text = String.Format("{0} {1}",
                Localization.Strings.ShiftSize,
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