using DynamicDraw.Gui;
using DynamicDraw.Localization;
using DynamicDraw.Logic;
using System.Collections.Generic;
using System.Drawing;

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
                Key = System.Windows.Forms.Keys.B,
                Target = ShortcutTarget.SelectedTool,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas }
            },
            new KeyboardShortcut() // K: color picker tool
            {
                ActionData = $"{(int)Tool.ColorPicker},{(int)Tool.PreviousTool}|cycle",
                Key = System.Windows.Forms.Keys.K,
                Target = ShortcutTarget.SelectedTool,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas }
            },
            new KeyboardShortcut() // E: eraser tool
            {
                ActionData = $"{(int)Tool.Eraser},{(int)Tool.PreviousTool}|cycle",
                Key = System.Windows.Forms.Keys.E,
                Target = ShortcutTarget.SelectedTool,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas }
            },
            new KeyboardShortcut() // O: origin tool
            {
                ActionData = $"{(int)Tool.SetSymmetryOrigin},{(int)Tool.PreviousTool}|cycle",
                Key = System.Windows.Forms.Keys.O,
                Target = ShortcutTarget.SelectedTool,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas }
            },
            new KeyboardShortcut() // Ctrl + Z: undo
            {
                ActionData = null,
                Key = System.Windows.Forms.Keys.Z,
                Target = ShortcutTarget.UndoAction,
                RequireCtrl = true
            },
            new KeyboardShortcut() // Ctrl + Alt + Z: undo (common alt shortcut)
            {
                ActionData = null,
                Key = System.Windows.Forms.Keys.Z,
                Target = ShortcutTarget.UndoAction,
                RequireCtrl = true,
                RequireAlt = true
            },
            new KeyboardShortcut() // Ctrl + Y: redo
            {
                ActionData = null,
                Key = System.Windows.Forms.Keys.Y,
                Target = ShortcutTarget.RedoAction,
                RequireCtrl = true
            },
            new KeyboardShortcut() // Ctrl + Shift + Z: redo (common alt shortcut)
            {
                ActionData = null,
                Key = System.Windows.Forms.Keys.Z,
                Target = ShortcutTarget.RedoAction,
                RequireCtrl = true,
                RequireShift = true
            },
            new KeyboardShortcut() // +: zoom in
            {
                ActionData = "1,5,10,13,17,20,25,33,50,67,100,150,200,300,400,500,600,800,1000,1200,1400,1600,2000,2400,2800,3200,4000,4800,5600,6400|cycle-add-stop",
                Key = System.Windows.Forms.Keys.Oemplus,
                Target = ShortcutTarget.CanvasZoom,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas }
            },
            new KeyboardShortcut() // Ctrl + +: zoom in (common alt shortcut)
            {
                ActionData = "1,5,10,13,17,20,25,33,50,67,100,150,200,300,400,500,600,800,1000,1200,1400,1600,2000,2400,2800,3200,4000,4800,5600,6400|cycle-add-stop",
                Key = System.Windows.Forms.Keys.Oemplus,
                Target = ShortcutTarget.CanvasZoom,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas },
                RequireCtrl = true
            },
            new KeyboardShortcut() // -: zoom out
            {
                ActionData = "1,5,10,13,17,20,25,33,50,67,100,150,200,300,400,500,600,800,1000,1200,1400,1600,2000,2400,2800,3200,4000,4800,5600,6400|cycle-sub-stop",
                Key = System.Windows.Forms.Keys.OemMinus,
                Target = ShortcutTarget.CanvasZoom,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas }
            },
            new KeyboardShortcut() // Ctrl + -: zoom out (common alt shortcut)
            {
                ActionData = "1,5,10,13,17,20,25,33,50,67,100,150,200,300,400,500,600,800,1000,1200,1400,1600,2000,2400,2800,3200,4000,4800,5600,6400|cycle-sub-stop",
                Key = System.Windows.Forms.Keys.OemMinus,
                Target = ShortcutTarget.CanvasZoom,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas },
                RequireCtrl = true
            },
            new KeyboardShortcut() // 0: reset canvas transforms
            {
                ActionData = null,
                Key = System.Windows.Forms.Keys.D0,
                Target = ShortcutTarget.ResetCanvasTransforms,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas }
            },
            new KeyboardShortcut() // 0: reset canvas transforms
            {
                ActionData = null,
                Key = System.Windows.Forms.Keys.NumPad0,
                Target = ShortcutTarget.ResetCanvasTransforms,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas }
            },
            new KeyboardShortcut() // left arrow: nudge left
            {
                ActionData = "5|sub",
                Key = System.Windows.Forms.Keys.Left,
                Target = ShortcutTarget.CanvasX,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas }
            },
            new KeyboardShortcut() // right arrow: nudge right
            {
                ActionData = "5|add",
                Key = System.Windows.Forms.Keys.Right,
                Target = ShortcutTarget.CanvasX,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas }
            },
            new KeyboardShortcut() // up arrow: nudge up
            {
                ActionData = "5|sub",
                Key = System.Windows.Forms.Keys.Up,
                Target = ShortcutTarget.CanvasY,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas }
            },
            new KeyboardShortcut() // down arrow: nudge down
            {
                ActionData = "5|add",
                Key = System.Windows.Forms.Keys.Down,
                Target = ShortcutTarget.CanvasY,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas }
            },
            new KeyboardShortcut() // shift + left arrow: rotate counter-clockwise
            {
                ActionData = "10|sub",
                Key = System.Windows.Forms.Keys.Left,
                Target = ShortcutTarget.CanvasRotation,
                RequireShift = true
            },
            new KeyboardShortcut() // shift + right arrow: rotate clockwise
            {
                ActionData = "10|add",
                Key = System.Windows.Forms.Keys.Right,
                Target = ShortcutTarget.CanvasRotation,
                RequireShift = true
            }
        };

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
                    BrushImagePath = Strings.DefaultBrushCircle,
                    CmbxTabPressureBrushOpacity = (int)ConstraintValueHandlingMethod.MatchValue,
                    TabPressureBrushOpacity = 255,
                    BrushSize = 20
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

        #region Fields
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
        #endregion

        /// <summary>
        /// Creates a new settings token.
        /// </summary>
        public PersistentSettings()
        {
            CurrentBrushSettings = new BrushSettings(defaultBrushes[Strings.BuiltInBrushPencil]);
            CustomBrushLocations = new HashSet<string>();
            KeyboardShortcuts = new HashSet<KeyboardShortcut>(defaultShortcuts);
        }

        /// <summary>
        /// Copies all settings to another token.
        /// </summary>
        protected PersistentSettings(PersistentSettings other)
            : base(other)
        {
            CurrentBrushSettings = new BrushSettings(other.CurrentBrushSettings);
            CustomBrushLocations = new HashSet<string>(
                other.CustomBrushLocations,
                other.CustomBrushLocations.Comparer);
            KeyboardShortcuts = new HashSet<KeyboardShortcut>(other.KeyboardShortcuts);
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