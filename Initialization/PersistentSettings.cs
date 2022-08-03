using DynamicDraw.Localization;
using System.Collections.Generic;
using System.Drawing;
using System.Text.Json.Serialization;
using System;
using System.IO;

namespace DynamicDraw
{
    /// <summary>
    /// Represents the settings used in the dialog so they can be stored and
    /// loaded when applying the effect consecutively for convenience.
    /// </summary>
    public class PersistentSettings : PaintDotNet.Effects.EffectConfigToken
    {
        /// <summary>
        /// The built-in default keyboard shortcuts.
        /// </summary>
        private static readonly HashSet<KeyboardShortcut> defaultShortcuts = new()
        {
            new KeyboardShortcut() // B: brush tool
            {
                ActionData = $"{(int)Tool.Brush},{(int)Tool.PreviousTool}|cycle",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.B },
                Target = ShortcutTarget.SelectedTool,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas }
            },
            new KeyboardShortcut() // K: color picker tool
            {
                ActionData = $"{(int)Tool.ColorPicker},{(int)Tool.PreviousTool}|cycle",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.K },
                Target = ShortcutTarget.SelectedTool,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas }
            },
            new KeyboardShortcut() // E: eraser tool
            {
                ActionData = $"{(int)Tool.Eraser},{(int)Tool.PreviousTool}|cycle",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.E },
                Target = ShortcutTarget.SelectedTool,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas }
            },
            new KeyboardShortcut() // O: origin tool
            {
                ActionData = $"{(int)Tool.SetSymmetryOrigin},{(int)Tool.PreviousTool}|cycle",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.O },
                Target = ShortcutTarget.SelectedTool,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas }
            },
            new KeyboardShortcut() // Ctrl + Z: undo
            {
                ActionData = null,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Z },
                Target = ShortcutTarget.UndoAction,
                RequireCtrl = true
            },
            new KeyboardShortcut() // Ctrl + Alt + Z: undo (common alt shortcut)
            {
                ActionData = null,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Z },
                Target = ShortcutTarget.UndoAction,
                RequireCtrl = true,
                RequireAlt = true
            },
            new KeyboardShortcut() // Ctrl + Y: redo
            {
                ActionData = null,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Y },
                Target = ShortcutTarget.RedoAction,
                RequireCtrl = true
            },
            new KeyboardShortcut() // Ctrl + Shift + Z: redo (common alt shortcut)
            {
                ActionData = null,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Z },
                Target = ShortcutTarget.RedoAction,
                RequireCtrl = true,
                RequireShift = true
            },
            new KeyboardShortcut() // Ctrl + Wheel up: zoom in
            {
                ActionData = "1,5,10,13,17,20,25,33,50,67,100,150,200,300,400,500,600,800,1000,1200,1400,1600,2000,2400,2800,3200,4000,4800,5600,6400|cycle-add-stop",
                Target = ShortcutTarget.CanvasZoomToMouse,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas },
                RequireCtrl = true,
                RequireWheelUp = true
            },
            new KeyboardShortcut() // Ctrl + Wheel down: zoom out
            {
                ActionData = "1,5,10,13,17,20,25,33,50,67,100,150,200,300,400,500,600,800,1000,1200,1400,1600,2000,2400,2800,3200,4000,4800,5600,6400|cycle-sub-stop",
                Target = ShortcutTarget.CanvasZoomToMouse,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas },
                RequireCtrl = true,
                RequireWheelDown = true
            },
            new KeyboardShortcut() // +: zoom in
            {
                ActionData = "1,5,10,13,17,20,25,33,50,67,100,150,200,300,400,500,600,800,1000,1200,1400,1600,2000,2400,2800,3200,4000,4800,5600,6400|cycle-add-stop",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Oemplus },
                Target = ShortcutTarget.CanvasZoom,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas }
            },
            new KeyboardShortcut() // Ctrl + +: zoom in (common alt shortcut)
            {
                ActionData = "1,5,10,13,17,20,25,33,50,67,100,150,200,300,400,500,600,800,1000,1200,1400,1600,2000,2400,2800,3200,4000,4800,5600,6400|cycle-add-stop",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Oemplus },
                Target = ShortcutTarget.CanvasZoom,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas },
                RequireCtrl = true
            },
            new KeyboardShortcut() // -: zoom out
            {
                ActionData = "1,5,10,13,17,20,25,33,50,67,100,150,200,300,400,500,600,800,1000,1200,1400,1600,2000,2400,2800,3200,4000,4800,5600,6400|cycle-sub-stop",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.OemMinus },
                Target = ShortcutTarget.CanvasZoom,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas }
            },
            new KeyboardShortcut() // Ctrl + -: zoom out (common alt shortcut)
            {
                ActionData = "1,5,10,13,17,20,25,33,50,67,100,150,200,300,400,500,600,800,1000,1200,1400,1600,2000,2400,2800,3200,4000,4800,5600,6400|cycle-sub-stop",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.OemMinus },
                Target = ShortcutTarget.CanvasZoom,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas },
                RequireCtrl = true
            },
            new KeyboardShortcut() // 0: reset canvas transforms
            {
                ActionData = null,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.D0 },
                Target = ShortcutTarget.ResetCanvasTransforms,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas }
            },
            new KeyboardShortcut() // 0: reset canvas transforms
            {
                ActionData = null,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.NumPad0 },
                Target = ShortcutTarget.ResetCanvasTransforms,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas }
            },
            new KeyboardShortcut() // Left arrow: nudge left
            {
                ActionData = "5|sub",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Left },
                Target = ShortcutTarget.CanvasX,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas }
            },
            new KeyboardShortcut() // Right arrow: nudge right
            {
                ActionData = "5|add",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Right },
                Target = ShortcutTarget.CanvasX,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas }
            },
            new KeyboardShortcut() // Up arrow: nudge up
            {
                ActionData = "5|sub",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Up },
                Target = ShortcutTarget.CanvasY,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas }
            },
            new KeyboardShortcut() // Down arrow: nudge down
            {
                ActionData = "5|add",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Down },
                Target = ShortcutTarget.CanvasY,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas }
            },
            new KeyboardShortcut() // Shift + Left arrow: rotate canvas counter-clockwise
            {
                ActionData = "10|sub",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Left },
                Target = ShortcutTarget.CanvasRotation,
                RequireShift = true
            },
            new KeyboardShortcut() // Shift + Right arrow: rotate canvas clockwise
            {
                ActionData = "10|add",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Right },
                Target = ShortcutTarget.CanvasRotation,
                RequireShift = true
            },
            new KeyboardShortcut() // Shift + Wheel up: rotate canvas counter-clockwise
            {
                ActionData = "10|add",
                Target = ShortcutTarget.CanvasRotation,
                RequireShift = true,
                RequireWheelUp = true
            },
            new KeyboardShortcut() // Shift + Wheel down: rotate canvas clockwise
            {
                ActionData = "10|sub",
                Target = ShortcutTarget.CanvasRotation,
                RequireShift = true,
                RequireWheelDown = true
            },
            new KeyboardShortcut() // Ctrl + S + Wheel up: increase brush size
            {
                ActionData = "1,2,3,4,5,6,7,8,9,10,15,20,25,30,40,50,60,70,80,90,100,120,140,160,180,200,220,240,260,280,300,320,340,360,380,400,450,500,550,600,650,700,750,800,850,900,950,1000|cycle-add-stop",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.S },
                Target = ShortcutTarget.Size,
                RequireCtrl = true,
                RequireWheelUp = true
            },
            new KeyboardShortcut() // Ctrl + S + Wheel down: decrease brush size
            {
                ActionData = "1,2,3,4,5,6,7,8,9,10,15,20,25,30,40,50,60,70,80,90,100,120,140,160,180,200,220,240,260,280,300,320,340,360,380,400,450,500,550,600,650,700,750,800,850,900,950,1000|cycle-sub-stop",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.S },
                Target = ShortcutTarget.Size,
                RequireCtrl = true,
                RequireWheelDown = true
            },
            new KeyboardShortcut() // Ctrl + R + Wheel up: rotate brush counter-clockwise
            {
                ActionData = "-165,-150,-135,-120,-105,-90,-75,-60,-45,-30,-15,0,15,30,45,60,75,90,105,120,135,150,165,180|cycle-add-wrap",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.R },
                Target = ShortcutTarget.Rotation,
                RequireCtrl = true,
                RequireWheelUp = true
            },
            new KeyboardShortcut() // Ctrl + R + Wheel down: rotate brush clockwise
            {
                ActionData = "-165,-150,-135,-120,-105,-90,-75,-60,-45,-30,-15,0,15,30,45,60,75,90,105,120,135,150,165,180|cycle-sub-wrap",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.R },
                Target = ShortcutTarget.Rotation,
                RequireCtrl = true,
                RequireWheelDown = true
            },
            new KeyboardShortcut() // Ctrl + O + Wheel up: increase brush opacity
            {
                ActionData = "10|add",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.O },
                Target = ShortcutTarget.BrushOpacity,
                RequireCtrl = true,
                RequireWheelUp = true
            },
            new KeyboardShortcut() // Ctrl + O + Wheel down: decrease brush opacity
            {
                ActionData = "10|sub",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.O },
                Target = ShortcutTarget.BrushOpacity,
                RequireCtrl = true,
                RequireWheelDown = true
            },
            new KeyboardShortcut() // Ctrl + F + Wheel up: increase brush flow
            {
                ActionData = "10|add",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.F },
                Target = ShortcutTarget.Flow,
                RequireCtrl = true,
                RequireWheelUp = true
            },
            new KeyboardShortcut() // Ctrl + F + Wheel down: decrease brush flow
            {
                ActionData = "10|sub",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.F },
                Target = ShortcutTarget.Flow,
                RequireCtrl = true,
                RequireWheelDown = true
            },
            new KeyboardShortcut() // Ctrl + S + ]: increase brush size
            {
                ActionData = "1|add",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.S, System.Windows.Forms.Keys.OemCloseBrackets },
                Target = ShortcutTarget.Size,
                RequireCtrl = true
            },
            new KeyboardShortcut() // Ctrl + Shift + S + ]: increase brush size (faster)
            {
                ActionData = "5|add",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.S, System.Windows.Forms.Keys.OemCloseBrackets },
                Target = ShortcutTarget.Size,
                RequireCtrl = true,
                RequireShift = true
            },
            new KeyboardShortcut() // Ctrl + S + [: decrease brush size
            {
                ActionData = "1|sub",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.S, System.Windows.Forms.Keys.OemOpenBrackets },
                Target = ShortcutTarget.Size,
                RequireCtrl = true
            },
            new KeyboardShortcut() // Ctrl + Shift + S + [: decrease brush size (faster)
            {
                ActionData = "5|sub",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.S, System.Windows.Forms.Keys.OemOpenBrackets },
                Target = ShortcutTarget.Size,
                RequireCtrl = true,
                RequireShift = true
            },
            new KeyboardShortcut() // Ctrl + S + ]: increase brush angle
            {
                ActionData = "1|add",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.R, System.Windows.Forms.Keys.OemCloseBrackets },
                Target = ShortcutTarget.Rotation,
                RequireCtrl = true
            },
            new KeyboardShortcut() // Ctrl + Shift + S + ]: increase brush angle (faster)
            {
                ActionData = "5|add",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.R, System.Windows.Forms.Keys.OemCloseBrackets },
                Target = ShortcutTarget.Rotation,
                RequireCtrl = true,
                RequireShift = true
            },
            new KeyboardShortcut() // Ctrl + S + [: decrease brush angle
            {
                ActionData = "1|sub",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.R, System.Windows.Forms.Keys.OemOpenBrackets },
                Target = ShortcutTarget.Rotation,
                RequireCtrl = true
            },
            new KeyboardShortcut() // Ctrl + Shift + S + [: decrease brush angle (faster)
            {
                ActionData = "5|sub",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.R, System.Windows.Forms.Keys.OemOpenBrackets },
                Target = ShortcutTarget.Rotation,
                RequireCtrl = true,
                RequireShift = true
            },
            new KeyboardShortcut() // Ctrl + S + ]: increase brush opacity
            {
                ActionData = "1|add",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.O, System.Windows.Forms.Keys.OemCloseBrackets },
                Target = ShortcutTarget.BrushOpacity,
                RequireCtrl = true
            },
            new KeyboardShortcut() // Ctrl + Shift + S + ]: increase brush opacity (faster)
            {
                ActionData = "5|add",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.O, System.Windows.Forms.Keys.OemCloseBrackets },
                Target = ShortcutTarget.BrushOpacity,
                RequireCtrl = true,
                RequireShift = true
            },
            new KeyboardShortcut() // Ctrl + S + [: decrease brush opacity
            {
                ActionData = "1|sub",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.O, System.Windows.Forms.Keys.OemOpenBrackets },
                Target = ShortcutTarget.BrushOpacity,
                RequireCtrl = true
            },
            new KeyboardShortcut() // Ctrl + Shift + S + [: decrease brush opacity (faster)
            {
                ActionData = "5|sub",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.O, System.Windows.Forms.Keys.OemOpenBrackets },
                Target = ShortcutTarget.BrushOpacity,
                RequireCtrl = true,
                RequireShift = true
            },
            new KeyboardShortcut() // Ctrl + S + ]: increase brush flow
            {
                ActionData = "1|add",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.F, System.Windows.Forms.Keys.OemCloseBrackets },
                Target = ShortcutTarget.Flow,
                RequireCtrl = true
            },
            new KeyboardShortcut() // Ctrl + Shift + S + ]: increase brush flow (faster)
            {
                ActionData = "5|add",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.F, System.Windows.Forms.Keys.OemCloseBrackets },
                Target = ShortcutTarget.Flow,
                RequireCtrl = true,
                RequireShift = true
            },
            new KeyboardShortcut() // Ctrl + S + [: decrease brush flow
            {
                ActionData = "1|sub",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.F, System.Windows.Forms.Keys.OemOpenBrackets },
                Target = ShortcutTarget.Flow,
                RequireCtrl = true
            },
            new KeyboardShortcut() // Ctrl + Shift + S + [: decrease brush flow (faster)
            {
                ActionData = "5|sub",
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.F, System.Windows.Forms.Keys.OemOpenBrackets },
                Target = ShortcutTarget.Flow,
                RequireCtrl = true,
                RequireShift = true
            },
        };

        /// <summary>
        /// The default list of brushes available.
        /// </summary>
        public static readonly Dictionary<string, BrushSettings> defaultBrushes = new()
        {
            {
                Strings.BuiltInBrushPencil,
                new BrushSettings()
                {
                    BrushImagePath = Strings.DefaultBrushCircle,
                    BrushDensity = 2,
                    CmbxTabPressureBrushSize = (int)ConstraintValueHandlingMethod.Add,
                    TabPressureBrushSize = 10,
                }
            },
            {
                Strings.BuiltInBrushAirbrush,
                new BrushSettings()
                {
                    BrushImagePath = Strings.DefaultBrushBigDots,
                    BrushSize = 9,
                    RandRotLeft = 180,
                    RandRotRight = 180,
                    RandHorzShift = 3,
                    RandVertShift = 3
                }
            },
            {
                Strings.BuiltInBrushGrass,
                new BrushSettings()
                {
                    BrushImagePath = Strings.DefaultBrushGrass,
                    BrushColor = Color.FromArgb(255, 20, 192, 20),
                    BrushSize = 50,
                    DoRotateWithMouse = true,
                    RandMinSize = 12,
                    RandRotLeft = 25,
                    RandRotRight = 25,
                    RandVertShift = 3,
                    RandMinV = 10,
                    RandMaxH = 4,
                    RandMinH = 4
                }
            },
            {
                Strings.BuiltInBrushRecolor,
                new BrushSettings()
                {
                    BrushImagePath = Strings.DefaultBrushCircle,
                    BrushColor = Color.Red,
                    BrushSize = 30,
                    DoLockAlpha = true,
                    DoLockVal= true,
                    DoLockSat = true,
                    CmbxTabPressureBrushSize = (int)ConstraintValueHandlingMethod.Add,
                    TabPressureBrushSize = 10,
                }
            }
        };

        /// <summary>
        /// The default list of palette locations to search for palettes in.
        /// </summary>
        public static readonly HashSet<string> defaultPalettePaths;

        #region Fields
        /// <summary>
        /// A chosen effect the user can render and draw on the canvas, and the combobox index for it.
        /// </summary>
        public (int index, CustomEffect effect) ActiveEffect { get; set; }

        /// <summary>
        /// The last used brush settings the user had.
        /// </summary>
        public BrushSettings CurrentBrushSettings { get; set; }

        /// <summary>
        /// Contains a list of all custom brushes to reload. The program will attempt to read the paths of each brush
        /// and add them if possible.
        /// </summary>
        public HashSet<string> CustomBrushLocations
        {
            get;
            set;
        }

        /// <summary>
        /// Contains a list of all keyboard shortcuts.
        /// </summary>
        public HashSet<KeyboardShortcut> KeyboardShortcuts { get; set; }

        /// <summary>
        /// The user's program preferences.
        /// </summary>
        public UserSettings UserSettings { get; set; }
        #endregion

        static PersistentSettings()
        {
            string mainDir = AppDomain.CurrentDomain.BaseDirectory;
            string documentsDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            defaultPalettePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                Path.Combine(documentsDir, "paint.net App Files\\Palettes"),
                Path.Combine(mainDir, "UserFiles\\Palettes")
            };
        }

        /// <summary>
        /// Creates a new settings token.
        /// </summary>
        [JsonConstructor]
        public PersistentSettings()
        {
            ActiveEffect = new(0, null);
            CurrentBrushSettings = new BrushSettings(defaultBrushes[Strings.BuiltInBrushPencil]);
            CustomBrushLocations = new HashSet<string>();
            KeyboardShortcuts = new HashSet<KeyboardShortcut>(defaultShortcuts);
            UserSettings = new UserSettings();
        }

        /// <summary>
        /// Copies all settings to another token.
        /// </summary>
        protected PersistentSettings(PersistentSettings other)
            : base(other)
        {
            ActiveEffect = new(other.ActiveEffect.index, new CustomEffect(other.ActiveEffect.effect));
            CurrentBrushSettings = new BrushSettings(other.CurrentBrushSettings);
            CustomBrushLocations = new HashSet<string>(
                other.CustomBrushLocations,
                other.CustomBrushLocations.Comparer);
            KeyboardShortcuts = new HashSet<KeyboardShortcut>(other.KeyboardShortcuts);
            UserSettings = new UserSettings(other.UserSettings);
        }

        /// <summary>
        /// Copies all settings to another token.
        /// </summary>
        public override object Clone()
        {
            return new PersistentSettings(this);
        }

        /// <summary>
        /// Returns a new HashSet containing the same KeyboardShortcut objects exposed by static context app-wide.
        /// Do NOT mutate any of the shortcut objects.
        /// </summary>
        public static HashSet<KeyboardShortcut> GetShallowShortcutsList()
        {
            return new HashSet<KeyboardShortcut>(defaultShortcuts);
        }
    }
}