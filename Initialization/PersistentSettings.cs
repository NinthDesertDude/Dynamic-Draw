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
                BuiltInShortcutId = 0,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.B },
                Name = Strings.ShortcutNameSwitchToBrush,
                Target = ShortcutTarget.SelectedTool
            },
            new KeyboardShortcut() // K: color picker tool
            {
                ActionData = $"{(int)Tool.ColorPicker},{(int)Tool.PreviousTool}|cycle",
                BuiltInShortcutId = 1,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.K },
                Name = Strings.ShortcutNameSwitchToColorPicker,
                Target = ShortcutTarget.SelectedTool
            },
            new KeyboardShortcut() // E: eraser tool
            {
                ActionData = $"{(int)Tool.Eraser},{(int)Tool.PreviousTool}|cycle",
                BuiltInShortcutId = 2,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.E },
                Name = Strings.ShortcutNameSwitchToEraser,
                Target = ShortcutTarget.SelectedTool
            },
            new KeyboardShortcut() // O: origin tool
            {
                ActionData = $"{(int)Tool.SetSymmetryOrigin},{(int)Tool.PreviousTool}|cycle",
                BuiltInShortcutId = 3,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.O },
                Name = Strings.ShortcutNameSwitchToSetOrigin,
                Target = ShortcutTarget.SelectedTool
            },
            new KeyboardShortcut() // Ctrl + Z: undo
            {
                ActionData = null,
                BuiltInShortcutId = 4,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Z },
                Name = Strings.Undo,
                Target = ShortcutTarget.UndoAction,
                RequireCtrl = true
            },
            new KeyboardShortcut() // Ctrl + Alt + Z: undo (common alt shortcut)
            {
                ActionData = null,
                BuiltInShortcutId = 5,
                CommandDialogIgnore = true,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Z },
                Name = Strings.Undo,
                Target = ShortcutTarget.UndoAction,
                RequireCtrl = true,
                RequireAlt = true
            },
            new KeyboardShortcut() // Ctrl + Y: redo
            {
                ActionData = null,
                BuiltInShortcutId = 6,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Y },
                Name = Strings.Redo,
                Target = ShortcutTarget.RedoAction,
                RequireCtrl = true
            },
            new KeyboardShortcut() // Ctrl + Shift + Z: redo (common alt shortcut)
            {
                ActionData = null,
                BuiltInShortcutId = 7,
                CommandDialogIgnore = true,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Z },
                Name = Strings.Redo,
                Target = ShortcutTarget.RedoAction,
                RequireCtrl = true,
                RequireShift = true
            },
            new KeyboardShortcut() // Ctrl + Wheel up: zoom in
            {
                ActionData = "1,5,10,13,17,20,25,33,50,67,100,150,200,300,400,500,600,800,1000,1200,1400,1600,2000,2400,2800,3200,4000,4800,5600,6400|cycle-add-stop",
                BuiltInShortcutId = 8,
                CommandDialogIgnore = true,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas },
                Name = Strings.ShortcutNameZoomIn,
                Target = ShortcutTarget.CanvasZoomToMouse,
                RequireCtrl = true,
                RequireWheelUp = true
            },
            new KeyboardShortcut() // Ctrl + Wheel down: zoom out
            {
                ActionData = "1,5,10,13,17,20,25,33,50,67,100,150,200,300,400,500,600,800,1000,1200,1400,1600,2000,2400,2800,3200,4000,4800,5600,6400|cycle-sub-stop",
                BuiltInShortcutId = 9,
                CommandDialogIgnore = true,
                Name = Strings.ShortcutNameZoomOut,
                Target = ShortcutTarget.CanvasZoomToMouse,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas },
                RequireCtrl = true,
                RequireWheelDown = true
            },
            new KeyboardShortcut() // +: zoom in
            {
                ActionData = "1,5,10,13,17,20,25,33,50,67,100,150,200,300,400,500,600,800,1000,1200,1400,1600,2000,2400,2800,3200,4000,4800,5600,6400|cycle-add-stop",
                BuiltInShortcutId = 10,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Oemplus },
                Name = Strings.ShortcutNameZoomIn,
                Target = ShortcutTarget.CanvasZoom,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas }
            },
            new KeyboardShortcut() // Ctrl + +: zoom in (common alt shortcut)
            {
                ActionData = "1,5,10,13,17,20,25,33,50,67,100,150,200,300,400,500,600,800,1000,1200,1400,1600,2000,2400,2800,3200,4000,4800,5600,6400|cycle-add-stop",
                BuiltInShortcutId = 11,
                CommandDialogIgnore = true,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Oemplus },
                Name = Strings.ShortcutNameZoomIn,
                Target = ShortcutTarget.CanvasZoom,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas },
                RequireCtrl = true
            },
            new KeyboardShortcut() // -: zoom out
            {
                ActionData = "1,5,10,13,17,20,25,33,50,67,100,150,200,300,400,500,600,800,1000,1200,1400,1600,2000,2400,2800,3200,4000,4800,5600,6400|cycle-sub-stop",
                BuiltInShortcutId = 12,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.OemMinus },
                Name = Strings.ShortcutNameZoomOut,
                Target = ShortcutTarget.CanvasZoom,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas }
            },
            new KeyboardShortcut() // Ctrl + -: zoom out (common alt shortcut)
            {
                ActionData = "1,5,10,13,17,20,25,33,50,67,100,150,200,300,400,500,600,800,1000,1200,1400,1600,2000,2400,2800,3200,4000,4800,5600,6400|cycle-sub-stop",
                BuiltInShortcutId = 13,
                CommandDialogIgnore = true,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.OemMinus },
                Name = Strings.ShortcutNameZoomOut,
                Target = ShortcutTarget.CanvasZoom,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas },
                RequireCtrl = true
            },
            new KeyboardShortcut() // 0: reset canvas transforms
            {
                ActionData = null,
                BuiltInShortcutId = 14,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.D0 },
                Name = Strings.ShortcutNameRecenterTheCanvas,
                Target = ShortcutTarget.ResetCanvasTransforms,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas }
            },
            new KeyboardShortcut() // 0: reset canvas transforms
            {
                ActionData = null,
                BuiltInShortcutId = 15,
                CommandDialogIgnore = true,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.NumPad0 },
                Name = Strings.ShortcutNameRecenterTheCanvas,
                Target = ShortcutTarget.ResetCanvasTransforms,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas }
            },
            new KeyboardShortcut() // Left arrow: nudge left
            {
                ActionData = "5|sub",
                BuiltInShortcutId = 16,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Left },
                Name = Strings.ShortcutNameNudgeCanvasLeft,
                Target = ShortcutTarget.CanvasX,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas }
            },
            new KeyboardShortcut() // Right arrow: nudge right
            {
                ActionData = "5|add",
                BuiltInShortcutId = 17,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Right },
                Name = Strings.ShortcutNameNudgeCanvasRight,
                Target = ShortcutTarget.CanvasX,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas }
            },
            new KeyboardShortcut() // Up arrow: nudge up
            {
                ActionData = "5|sub",
                BuiltInShortcutId = 18,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Up },
                Name = Strings.ShortcutNameNudgeCanvasUp,
                Target = ShortcutTarget.CanvasY,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas }
            },
            new KeyboardShortcut() // Down arrow: nudge down
            {
                ActionData = "5|add",
                BuiltInShortcutId = 19,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Down },
                Name = Strings.ShortcutNameNudgeCanvasDown,
                Target = ShortcutTarget.CanvasY,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas }
            },
            new KeyboardShortcut() // Shift + Left arrow: rotate canvas counter-clockwise
            {
                ActionData = "10|sub",
                BuiltInShortcutId = 20,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Left },
                Name = Strings.ShortcutNameRotateCanvasCounter,
                Target = ShortcutTarget.CanvasRotation,
                RequireShift = true
            },
            new KeyboardShortcut() // Shift + Right arrow: rotate canvas clockwise
            {
                ActionData = "10|add",
                BuiltInShortcutId = 21,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Right },
                Name = Strings.ShortcutNameRotateCanvasClockwise,
                Target = ShortcutTarget.CanvasRotation,
                RequireShift = true
            },
            new KeyboardShortcut() // Shift + Wheel up: rotate canvas counter-clockwise
            {
                ActionData = "10|add",
                BuiltInShortcutId = 22,
                CommandDialogIgnore = true,
                Name = Strings.ShortcutNameRotateCanvasCounter,
                Target = ShortcutTarget.CanvasRotation,
                RequireShift = true,
                RequireWheelUp = true
            },
            new KeyboardShortcut() // Shift + Wheel down: rotate canvas clockwise
            {
                ActionData = "10|sub",
                BuiltInShortcutId = 23,
                CommandDialogIgnore = true,
                Name = Strings.ShortcutNameRotateCanvasClockwise,
                Target = ShortcutTarget.CanvasRotation,
                RequireShift = true,
                RequireWheelDown = true
            },
            new KeyboardShortcut() // Ctrl + S + Wheel up: increase brush size
            {
                ActionData = "1,2,3,4,5,6,7,8,9,10,15,20,25,30,40,50,60,70,80,90,100,120,140,160,180,200,220,240,260,280,300,320,340,360,380,400,450,500,550,600,650,700,750,800,850,900,950,1000|cycle-add-stop",
                BuiltInShortcutId = 24,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.S },
                Name = Strings.ShortcutNameIncreaseBrushSize,
                Target = ShortcutTarget.Size,
                RequireCtrl = true,
                RequireWheelUp = true
            },
            new KeyboardShortcut() // Ctrl + S + Wheel down: decrease brush size
            {
                ActionData = "1,2,3,4,5,6,7,8,9,10,15,20,25,30,40,50,60,70,80,90,100,120,140,160,180,200,220,240,260,280,300,320,340,360,380,400,450,500,550,600,650,700,750,800,850,900,950,1000|cycle-sub-stop",
                BuiltInShortcutId = 25,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.S },
                Name = Strings.ShortcutNameDecreaseBrushSize,
                Target = ShortcutTarget.Size,
                RequireCtrl = true,
                RequireWheelDown = true
            },
            new KeyboardShortcut() // Ctrl + R + Wheel up: rotate brush counter-clockwise
            {
                ActionData = "-165,-150,-135,-120,-105,-90,-75,-60,-45,-30,-15,0,15,30,45,60,75,90,105,120,135,150,165,180|cycle-add-wrap",
                BuiltInShortcutId = 26,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.R },
                Name = Strings.ShortcutNameRotateBrushCounter,
                Target = ShortcutTarget.Rotation,
                RequireCtrl = true,
                RequireWheelUp = true
            },
            new KeyboardShortcut() // Ctrl + R + Wheel down: rotate brush clockwise
            {
                ActionData = "-165,-150,-135,-120,-105,-90,-75,-60,-45,-30,-15,0,15,30,45,60,75,90,105,120,135,150,165,180|cycle-sub-wrap",
                BuiltInShortcutId = 27,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.R },
                Name = "Rotate brush clockwise",
                Target = ShortcutTarget.Rotation,
                RequireCtrl = true,
                RequireWheelDown = true
            },
            new KeyboardShortcut() // Ctrl + O + Wheel up: increase brush opacity
            {
                ActionData = "10|add",
                BuiltInShortcutId = 28,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.O },
                Name = Strings.ShortcutNameIncreaseBrushOpacity,
                Target = ShortcutTarget.BrushOpacity,
                RequireCtrl = true,
                RequireWheelUp = true
            },
            new KeyboardShortcut() // Ctrl + O + Wheel down: decrease brush opacity
            {
                ActionData = "10|sub",
                BuiltInShortcutId = 29,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.O },
                Name = Strings.ShortcutNameDecreaseBrushOpacity,
                Target = ShortcutTarget.BrushOpacity,
                RequireCtrl = true,
                RequireWheelDown = true
            },
            new KeyboardShortcut() // Ctrl + F + Wheel up: increase brush flow
            {
                ActionData = "10|add",
                BuiltInShortcutId = 30,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.F },
                Name = Strings.ShortcutNameIncreaseBrushFlow,
                Target = ShortcutTarget.Flow,
                RequireCtrl = true,
                RequireWheelUp = true
            },
            new KeyboardShortcut() // Ctrl + F + Wheel down: decrease brush flow
            {
                ActionData = "10|sub",
                BuiltInShortcutId = 31,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.F },
                Name = Strings.ShortcutNameDecreaseBrushFlow,
                Target = ShortcutTarget.Flow,
                RequireCtrl = true,
                RequireWheelDown = true
            },
            new KeyboardShortcut() // Ctrl + S + ]: increase brush size
            {
                ActionData = "1|add",
                BuiltInShortcutId = 32,
                CommandDialogIgnore = true,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.S, System.Windows.Forms.Keys.OemCloseBrackets },
                Name = Strings.ShortcutNameIncreaseBrushSize,
                Target = ShortcutTarget.Size,
                RequireCtrl = true
            },
            new KeyboardShortcut() // Ctrl + Shift + S + ]: increase brush size (faster)
            {
                ActionData = "5|add",
                BuiltInShortcutId = 33,
                CommandDialogIgnore = true,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.S, System.Windows.Forms.Keys.OemCloseBrackets },
                Name = Strings.ShortcutNameIncreaseBrushSize,
                Target = ShortcutTarget.Size,
                RequireCtrl = true,
                RequireShift = true
            },
            new KeyboardShortcut() // Ctrl + S + [: decrease brush size
            {
                ActionData = "1|sub",
                BuiltInShortcutId = 34,
                CommandDialogIgnore = true,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.S, System.Windows.Forms.Keys.OemOpenBrackets },
                Name = Strings.ShortcutNameDecreaseBrushSize,
                Target = ShortcutTarget.Size,
                RequireCtrl = true
            },
            new KeyboardShortcut() // Ctrl + Shift + S + [: decrease brush size (faster)
            {
                ActionData = "5|sub",
                BuiltInShortcutId = 35,
                CommandDialogIgnore = true,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.S, System.Windows.Forms.Keys.OemOpenBrackets },
                Name = Strings.ShortcutNameDecreaseBrushSize,
                Target = ShortcutTarget.Size,
                RequireCtrl = true,
                RequireShift = true
            },
            new KeyboardShortcut() // Ctrl + R + ]: increase brush angle
            {
                ActionData = "1|add",
                BuiltInShortcutId = 36,
                CommandDialogIgnore = true,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.R, System.Windows.Forms.Keys.OemCloseBrackets },
                Name = Strings.ShortcutNameRotateBrushClockwise,
                Target = ShortcutTarget.Rotation,
                RequireCtrl = true
            },
            new KeyboardShortcut() // Ctrl + Shift + R + ]: increase brush angle (faster)
            {
                ActionData = "5|add",
                BuiltInShortcutId = 37,
                CommandDialogIgnore = true,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.R, System.Windows.Forms.Keys.OemCloseBrackets },
                Name = Strings.ShortcutNameRotateBrushClockwise,
                Target = ShortcutTarget.Rotation,
                RequireCtrl = true,
                RequireShift = true
            },
            new KeyboardShortcut() // Ctrl + R + [: decrease brush angle
            {
                ActionData = "1|sub",
                BuiltInShortcutId = 38,
                CommandDialogIgnore = true,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.R, System.Windows.Forms.Keys.OemOpenBrackets },
                Name = Strings.ShortcutNameRotateBrushCounter,
                Target = ShortcutTarget.Rotation,
                RequireCtrl = true
            },
            new KeyboardShortcut() // Ctrl + Shift + R + [: decrease brush angle (faster)
            {
                ActionData = "5|sub",
                BuiltInShortcutId = 39,
                CommandDialogIgnore = true,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.R, System.Windows.Forms.Keys.OemOpenBrackets },
                Name = Strings.ShortcutNameRotateBrushCounter,
                Target = ShortcutTarget.Rotation,
                RequireCtrl = true,
                RequireShift = true
            },
            new KeyboardShortcut() // Ctrl + O + ]: increase brush opacity
            {
                ActionData = "1|add",
                BuiltInShortcutId = 40,
                CommandDialogIgnore = true,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.O, System.Windows.Forms.Keys.OemCloseBrackets },
                Name = Strings.ShortcutNameIncreaseBrushOpacity,
                Target = ShortcutTarget.BrushOpacity,
                RequireCtrl = true
            },
            new KeyboardShortcut() // Ctrl + Shift + O + ]: increase brush opacity (faster)
            {
                ActionData = "5|add",
                BuiltInShortcutId = 41,
                CommandDialogIgnore = true,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.O, System.Windows.Forms.Keys.OemCloseBrackets },
                Name = Strings.ShortcutNameIncreaseBrushOpacity,
                Target = ShortcutTarget.BrushOpacity,
                RequireCtrl = true,
                RequireShift = true
            },
            new KeyboardShortcut() // Ctrl + O + [: decrease brush opacity
            {
                ActionData = "1|sub",
                BuiltInShortcutId = 42,
                CommandDialogIgnore = true,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.O, System.Windows.Forms.Keys.OemOpenBrackets },
                Name = Strings.ShortcutNameDecreaseBrushOpacity,
                Target = ShortcutTarget.BrushOpacity,
                RequireCtrl = true
            },
            new KeyboardShortcut() // Ctrl + Shift + O + [: decrease brush opacity (faster)
            {
                ActionData = "5|sub",
                BuiltInShortcutId = 43,
                CommandDialogIgnore = true,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.O, System.Windows.Forms.Keys.OemOpenBrackets },
                Name = Strings.ShortcutNameDecreaseBrushOpacity,
                Target = ShortcutTarget.BrushOpacity,
                RequireCtrl = true,
                RequireShift = true
            },
            new KeyboardShortcut() // Ctrl + F + ]: increase brush flow
            {
                ActionData = "1|add",
                BuiltInShortcutId = 44,
                CommandDialogIgnore = true,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.F, System.Windows.Forms.Keys.OemCloseBrackets },
                Name = Strings.ShortcutNameIncreaseBrushFlow,
                Target = ShortcutTarget.Flow,
                RequireCtrl = true
            },
            new KeyboardShortcut() // Ctrl + Shift + F + ]: increase brush flow (faster)
            {
                ActionData = "5|add",
                BuiltInShortcutId = 45,
                CommandDialogIgnore = true,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.F, System.Windows.Forms.Keys.OemCloseBrackets },
                Name = Strings.ShortcutNameIncreaseBrushFlow,
                Target = ShortcutTarget.Flow,
                RequireCtrl = true,
                RequireShift = true
            },
            new KeyboardShortcut() // Ctrl + F + [: decrease brush flow
            {
                ActionData = "1|sub",
                BuiltInShortcutId = 46,
                CommandDialogIgnore = true,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.F, System.Windows.Forms.Keys.OemOpenBrackets },
                Name = Strings.ShortcutNameDecreaseBrushFlow,
                Target = ShortcutTarget.Flow,
                RequireCtrl = true
            },
            new KeyboardShortcut() // Ctrl + Shift + F + [: decrease brush flow (faster)
            {
                ActionData = "5|sub",
                BuiltInShortcutId = 47,
                CommandDialogIgnore = true,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.F, System.Windows.Forms.Keys.OemOpenBrackets },
                Name = Strings.ShortcutNameDecreaseBrushFlow,
                Target = ShortcutTarget.Flow,
                RequireCtrl = true,
                RequireShift = true
            },
            new KeyboardShortcut() // C: swap colors
            {
                ActionData = null,
                BuiltInShortcutId = 48,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.C },
                Name = Strings.ShortcutNameSwitchPrimarySecondary,
                Target = ShortcutTarget.SwapPrimarySecondaryColors
            },
            new KeyboardShortcut() // /: open quick command dialog
            {
                ActionData = null,
                BuiltInShortcutId = 49,
                CommandDialogIgnore = true,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.OemQuestion },
                Name = Strings.OpenQuickCommandDialog,
                Target = ShortcutTarget.OpenQuickCommandDialog
            }
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
        public HashSet<KeyboardShortcut> CustomShortcuts { get; set; }

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
            CustomShortcuts = new HashSet<KeyboardShortcut>(defaultShortcuts);
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
            CustomShortcuts = new HashSet<KeyboardShortcut>(other.CustomShortcuts);
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

        /// <summary>
        /// Returns a new hashset containing all shortcuts from the original list, plus shortcuts from the default list
        /// that aren't omitted by ID. This is intended to be used just after loading serialized results.
        /// </summary>
        public static HashSet<KeyboardShortcut> InjectDefaultShortcuts(HashSet<KeyboardShortcut> origList, HashSet<int> omitList)
        {
            HashSet<KeyboardShortcut> newList = new HashSet<KeyboardShortcut>(origList);

            foreach (KeyboardShortcut shortcut in defaultShortcuts)
            {
                if (omitList.Contains(shortcut.BuiltInShortcutId))
                {
                    continue;
                }

                newList.Add(shortcut);
            }

            return newList;
        }

        /// <summary>
        /// Returns a new list containing no default shortcuts.
        /// </summary>
        public static HashSet<KeyboardShortcut> RemoveDefaultShortcuts(HashSet<KeyboardShortcut> combinedList)
        {
            HashSet<KeyboardShortcut> shortcutsCopy = new HashSet<KeyboardShortcut>(combinedList);
            shortcutsCopy.RemoveWhere((shortcut) => shortcut.BuiltInShortcutId >= 0);
            return shortcutsCopy;
        }
    }
}