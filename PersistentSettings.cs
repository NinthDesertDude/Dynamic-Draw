using BrushFactory.Gui;
using BrushFactory.Localization;
using BrushFactory.Logic;
using System.Collections.Generic;
using System.Drawing;

namespace BrushFactory
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
            new KeyboardShortcut()
            {
                ActionData = $"{(int)Tool.Brush}|set",
                Key = System.Windows.Forms.Keys.B,
                Target = ShortcutTarget.SelectedTool
            },
            new KeyboardShortcut()
            {
                ActionData = $"{(int)Tool.ColorPicker}|set",
                Key = System.Windows.Forms.Keys.K,
                Target = ShortcutTarget.SelectedTool
            },
            new KeyboardShortcut()
            {
                ActionData = $"{(int)Tool.Eraser}|set",
                Key = System.Windows.Forms.Keys.E,
                Target = ShortcutTarget.SelectedTool
            },
            new KeyboardShortcut()
            {
                ActionData = $"{(int)Tool.SetSymmetryOrigin}|set",
                Key = System.Windows.Forms.Keys.O,
                Target = ShortcutTarget.SelectedTool
            },
            new KeyboardShortcut()
            {
                ActionData = null,
                Key = System.Windows.Forms.Keys.Z,
                Target = ShortcutTarget.UndoAction,
                RequireCtrl = true
            },
            new KeyboardShortcut()
            {
                ActionData = null,
                Key = System.Windows.Forms.Keys.Y,
                Target = ShortcutTarget.RedoAction,
                RequireCtrl = true
            },
            new KeyboardShortcut()
            {
                ActionData = null,
                Key = System.Windows.Forms.Keys.Z,
                Target = ShortcutTarget.RedoAction,
                RequireCtrl = true,
                RequireShift = true
            },
            new KeyboardShortcut()
            {
                ActionData = "2|mul",
                Key = System.Windows.Forms.Keys.Oemplus,
                Target = ShortcutTarget.CanvasZoom,
                RequireCtrl = true
            },
            new KeyboardShortcut()
            {
                ActionData = "0.5|mul",
                Key = System.Windows.Forms.Keys.OemMinus,
                Target = ShortcutTarget.CanvasZoom,
                RequireCtrl = true
            },
            new KeyboardShortcut()
            {
                ActionData = null,
                Key = System.Windows.Forms.Keys.D0,
                Target = ShortcutTarget.ResetCanvasTransforms
            },
            new KeyboardShortcut()
            {
                ActionData = "5|sub",
                Key = System.Windows.Forms.Keys.Left,
                Target = ShortcutTarget.CanvasX,
                RequireCtrl = true
            },
            new KeyboardShortcut()
            {
                ActionData = "5|add",
                Key = System.Windows.Forms.Keys.Right,
                Target = ShortcutTarget.CanvasX,
                RequireCtrl = true
            },
            new KeyboardShortcut()
            {
                ActionData = "5|sub",
                Key = System.Windows.Forms.Keys.Up,
                Target = ShortcutTarget.CanvasY,
                RequireCtrl = true
            },
            new KeyboardShortcut()
            {
                ActionData = "5|add",
                Key = System.Windows.Forms.Keys.Down,
                Target = ShortcutTarget.CanvasY,
                RequireCtrl = true
            },
            new KeyboardShortcut()
            {
                ActionData = "10|sub",
                Key = System.Windows.Forms.Keys.Left,
                Target = ShortcutTarget.CanvasRotation,
                RequireShift = true
            },
            new KeyboardShortcut()
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
                    CmbxTabPressureBrushSize = (int)CmbxTabletValueType.ValueHandlingMethod.Add,
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