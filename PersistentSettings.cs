using BrushFactory.Gui;
using BrushFactory.Localization;
using BrushFactory.Logic;
using System.Collections.Generic;

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
                ActionData = "100|add",
                Key = System.Windows.Forms.Keys.Oemplus,
                Target = ShortcutTarget.CanvasZoom,
                RequireCtrl = true
            },
            new KeyboardShortcut()
            {
                ActionData = "100|sub",
                Key = System.Windows.Forms.Keys.OemMinus,
                Target = ShortcutTarget.CanvasZoom,
                RequireCtrl = true
            }
        };

        public static readonly Dictionary<string, BrushSettings> defaultBrushes = new()
        {
            {
                Strings.CustomBrushesDefaultBrush,
                new BrushSettings()
                {
                    BrushImageName = Strings.DefaultBrushCircle,
                    BrushDensity = 2,
                    CmbxTabPressureBrushSize = (int)CmbxTabletValueType.ValueHandlingMethod.Add,
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
            CurrentBrushSettings = defaultBrushes[Strings.CustomBrushesDefaultBrush];
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