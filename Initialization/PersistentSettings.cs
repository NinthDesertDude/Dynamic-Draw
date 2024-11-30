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
        private static readonly HashSet<Command> defaultShortcuts = new()
        {
            new Command() // B: brush tool
            {
                ActionData = $"{(int)Tool.Brush},{(int)Tool.PreviousTool}|cycle",
                BuiltInShortcutId = 0,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.B },
                Name = Strings.ShortcutNameSwitchToBrush,
                Target = CommandTarget.SelectedTool
            },
            new Command() // K: color picker tool
            {
                ActionData = $"{(int)Tool.ColorPicker},{(int)Tool.PreviousTool}|cycle",
                BuiltInShortcutId = 1,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.K },
                Name = Strings.ShortcutNameSwitchToColorPicker,
                Target = CommandTarget.SelectedTool
            },
            new Command() // E: eraser tool
            {
                ActionData = $"{(int)Tool.Eraser},{(int)Tool.PreviousTool}|cycle",
                BuiltInShortcutId = 2,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.E },
                Name = Strings.ShortcutNameSwitchToEraser,
                Target = CommandTarget.SelectedTool
            },
            new Command() // Ctrl + Shift + O: origin tool
            {
                ActionData = $"{(int)Tool.SetSymmetryOrigin},{(int)Tool.PreviousTool}|cycle",
                BuiltInShortcutId = 3,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.O },
                Name = Strings.ShortcutNameSwitchToSetOrigin,
                RequireCtrl = true,
                RequireShift = true,
                Target = CommandTarget.SelectedTool
            },
            new Command() // Ctrl + Z: undo
            {
                ActionData = null,
                BuiltInShortcutId = 4,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Z },
                Name = Strings.Undo,
                Target = CommandTarget.UndoAction,
                RequireCtrl = true
            },
            new Command() // Ctrl + Alt + Z: undo (common alt shortcut)
            {
                ActionData = null,
                BuiltInShortcutId = 5,
                CommandDialogIgnore = true,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Z },
                Name = Strings.Undo,
                Target = CommandTarget.UndoAction,
                RequireCtrl = true,
                RequireAlt = true
            },
            new Command() // Ctrl + Y: redo
            {
                ActionData = null,
                BuiltInShortcutId = 6,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Y },
                Name = Strings.Redo,
                Target = CommandTarget.RedoAction,
                RequireCtrl = true
            },
            new Command() // Ctrl + Shift + Z: redo (common alt shortcut)
            {
                ActionData = null,
                BuiltInShortcutId = 7,
                CommandDialogIgnore = true,
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Z },
                Name = Strings.Redo,
                Target = CommandTarget.RedoAction,
                RequireCtrl = true,
                RequireShift = true
            },
            new Command() // Ctrl + Wheel up: zoom in
            {
                ActionData = "1,5,10,13,17,20,25,33,50,67,100,150,200,300,400,500,600,800,1000,1200,1400,1600,2000,2400,2800,3200,4000,4800,5600,6400|cycle-add-stop",
                BuiltInShortcutId = 8,
                CommandDialogIgnore = true,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Name = Strings.ShortcutNameZoomIn,
                Target = CommandTarget.CanvasZoomToMouse,
                RequireCtrl = true,
                RequireWheelUp = true
            },
            new Command() // Ctrl + Wheel down: zoom out
            {
                ActionData = "1,5,10,13,17,20,25,33,50,67,100,150,200,300,400,500,600,800,1000,1200,1400,1600,2000,2400,2800,3200,4000,4800,5600,6400|cycle-sub-stop",
                BuiltInShortcutId = 9,
                CommandDialogIgnore = true,
                Name = Strings.ShortcutNameZoomOut,
                Target = CommandTarget.CanvasZoomToMouse,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                RequireCtrl = true,
                RequireWheelDown = true
            },
            new Command() // +: zoom in
            {
                ActionData = "1,5,10,13,17,20,25,33,50,67,100,150,200,300,400,500,600,800,1000,1200,1400,1600,2000,2400,2800,3200,4000,4800,5600,6400|cycle-add-stop",
                BuiltInShortcutId = 10,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Oemplus },
                Name = Strings.ShortcutNameZoomIn,
                Target = CommandTarget.CanvasZoom
            },
            new Command() // Ctrl + +: zoom in (common alt shortcut)
            {
                ActionData = "1,5,10,13,17,20,25,33,50,67,100,150,200,300,400,500,600,800,1000,1200,1400,1600,2000,2400,2800,3200,4000,4800,5600,6400|cycle-add-stop",
                BuiltInShortcutId = 11,
                CommandDialogIgnore = true,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Oemplus },
                Name = Strings.ShortcutNameZoomIn,
                Target = CommandTarget.CanvasZoom,
                RequireCtrl = true
            },
            new Command() // -: zoom out
            {
                ActionData = "1,5,10,13,17,20,25,33,50,67,100,150,200,300,400,500,600,800,1000,1200,1400,1600,2000,2400,2800,3200,4000,4800,5600,6400|cycle-sub-stop",
                BuiltInShortcutId = 12,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.OemMinus },
                Name = Strings.ShortcutNameZoomOut,
                Target = CommandTarget.CanvasZoom,
            },
            new Command() // Ctrl + -: zoom out (common alt shortcut)
            {
                ActionData = "1,5,10,13,17,20,25,33,50,67,100,150,200,300,400,500,600,800,1000,1200,1400,1600,2000,2400,2800,3200,4000,4800,5600,6400|cycle-sub-stop",
                BuiltInShortcutId = 13,
                CommandDialogIgnore = true,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.OemMinus },
                Name = Strings.ShortcutNameZoomOut,
                Target = CommandTarget.CanvasZoom,
                RequireCtrl = true
            },
            new Command() // 0: reset canvas transforms
            {
                ActionData = null,
                BuiltInShortcutId = 14,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.D0 },
                Name = Strings.ShortcutNameRecenterTheCanvas,
                Target = CommandTarget.ResetCanvasTransforms
            },
            new Command() // 0: reset canvas transforms
            {
                ActionData = null,
                BuiltInShortcutId = 15,
                CommandDialogIgnore = true,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.NumPad0 },
                Name = Strings.ShortcutNameRecenterTheCanvas,
                Target = CommandTarget.ResetCanvasTransforms
            },
            new Command() // Left arrow: nudge left
            {
                ActionData = "5|sub",
                BuiltInShortcutId = 16,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Left },
                Name = Strings.ShortcutNameNudgeCanvasLeft,
                Target = CommandTarget.CanvasX
            },
            new Command() // Right arrow: nudge right
            {
                ActionData = "5|add",
                BuiltInShortcutId = 17,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Right },
                Name = Strings.ShortcutNameNudgeCanvasRight,
                Target = CommandTarget.CanvasX
            },
            new Command() // Up arrow: nudge up
            {
                ActionData = "5|sub",
                BuiltInShortcutId = 18,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Up },
                Name = Strings.ShortcutNameNudgeCanvasUp,
                Target = CommandTarget.CanvasY
            },
            new Command() // Down arrow: nudge down
            {
                ActionData = "5|add",
                BuiltInShortcutId = 19,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Down },
                Name = Strings.ShortcutNameNudgeCanvasDown,
                Target = CommandTarget.CanvasY
            },
            new Command() // Shift + Left arrow: rotate canvas counter-clockwise
            {
                ActionData = "10|sub",
                BuiltInShortcutId = 20,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Left },
                Name = Strings.ShortcutNameRotateCanvasCounter,
                Target = CommandTarget.CanvasRotation,
                RequireShift = true
            },
            new Command() // Shift + Right arrow: rotate canvas clockwise
            {
                ActionData = "10|add",
                BuiltInShortcutId = 21,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Right },
                Name = Strings.ShortcutNameRotateCanvasClockwise,
                Target = CommandTarget.CanvasRotation,
                RequireShift = true
            },
            new Command() // Shift + Wheel up: rotate canvas counter-clockwise
            {
                ActionData = "10|add",
                BuiltInShortcutId = 22,
                CommandDialogIgnore = true,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Name = Strings.ShortcutNameRotateCanvasCounter,
                Target = CommandTarget.CanvasRotation,
                RequireShift = true,
                RequireWheelUp = true
            },
            new Command() // Shift + Wheel down: rotate canvas clockwise
            {
                ActionData = "10|sub",
                BuiltInShortcutId = 23,
                CommandDialogIgnore = true,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Name = Strings.ShortcutNameRotateCanvasClockwise,
                Target = CommandTarget.CanvasRotation,
                RequireShift = true,
                RequireWheelDown = true
            },
            new Command() // Ctrl + S + Wheel up: increase brush size
            {
                ActionData = "1,2,3,4,5,6,7,8,9,10,15,20,25,30,40,50,60,70,80,90,100,120,140,160,180,200,220,240,260,280,300,320,340,360,380,400,450,500,550,600,650,700,750,800,850,900,950,1000|cycle-add-stop",
                BuiltInShortcutId = 24,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.S },
                Name = Strings.ShortcutNameIncreaseBrushSize,
                Target = CommandTarget.Size,
                RequireCtrl = true,
                RequireWheelUp = true
            },
            new Command() // Ctrl + S + Wheel down: decrease brush size
            {
                ActionData = "1,2,3,4,5,6,7,8,9,10,15,20,25,30,40,50,60,70,80,90,100,120,140,160,180,200,220,240,260,280,300,320,340,360,380,400,450,500,550,600,650,700,750,800,850,900,950,1000|cycle-sub-stop",
                BuiltInShortcutId = 25,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.S },
                Name = Strings.ShortcutNameDecreaseBrushSize,
                Target = CommandTarget.Size,
                RequireCtrl = true,
                RequireWheelDown = true
            },
            new Command() // Ctrl + R + Wheel up: rotate brush counter-clockwise
            {
                ActionData = "-165,-150,-135,-120,-105,-90,-75,-60,-45,-30,-15,0,15,30,45,60,75,90,105,120,135,150,165,180|cycle-add",
                BuiltInShortcutId = 26,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.R },
                Name = Strings.ShortcutNameRotateBrushCounter,
                Target = CommandTarget.Rotation,
                RequireCtrl = true,
                RequireWheelUp = true
            },
            new Command() // Ctrl + R + Wheel down: rotate brush clockwise
            {
                ActionData = "-165,-150,-135,-120,-105,-90,-75,-60,-45,-30,-15,0,15,30,45,60,75,90,105,120,135,150,165,180|cycle-sub",
                BuiltInShortcutId = 27,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.R },
                Name = Strings.ShortcutNameRotateBrushClockwise,
                Target = CommandTarget.Rotation,
                RequireCtrl = true,
                RequireWheelDown = true
            },
            new Command() // Ctrl + O + Wheel up: increase brush opacity
            {
                ActionData = "10|add",
                BuiltInShortcutId = 28,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.O },
                Name = Strings.ShortcutNameIncreaseBrushOpacity,
                Target = CommandTarget.BrushOpacity,
                RequireCtrl = true,
                RequireWheelUp = true
            },
            new Command() // Ctrl + O + Wheel down: decrease brush opacity
            {
                ActionData = "10|sub",
                BuiltInShortcutId = 29,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.O },
                Name = Strings.ShortcutNameDecreaseBrushOpacity,
                Target = CommandTarget.BrushOpacity,
                RequireCtrl = true,
                RequireWheelDown = true
            },
            new Command() // Ctrl + F + Wheel up: increase brush flow
            {
                ActionData = "10|add",
                BuiltInShortcutId = 30,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.F },
                Name = Strings.ShortcutNameIncreaseBrushFlow,
                Target = CommandTarget.Flow,
                RequireCtrl = true,
                RequireWheelUp = true
            },
            new Command() // Ctrl + F + Wheel down: decrease brush flow
            {
                ActionData = "10|sub",
                BuiltInShortcutId = 31,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.F },
                Name = Strings.ShortcutNameDecreaseBrushFlow,
                Target = CommandTarget.Flow,
                RequireCtrl = true,
                RequireWheelDown = true
            },
            new Command() // ]: increase brush size
            {
                ActionData = "1|add",
                BuiltInShortcutId = 53,
                CommandDialogIgnore = true,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.OemCloseBrackets },
                Name = Strings.ShortcutNameIncreaseBrushSize,
                Target = CommandTarget.Size
            },
            new Command() // Ctrl + ]: increase brush size (faster)
            {
                ActionData = "5|add",
                BuiltInShortcutId = 54,
                CommandDialogIgnore = true,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.OemCloseBrackets },
                Name = Strings.ShortcutNameIncreaseBrushSize,
                Target = CommandTarget.Size,
                RequireCtrl = true
            },
            new Command() // Ctrl + S + ]: increase brush size
            {
                ActionData = "1|add",
                BuiltInShortcutId = 32,
                CommandDialogIgnore = true,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.S, System.Windows.Forms.Keys.OemCloseBrackets },
                Name = Strings.ShortcutNameIncreaseBrushSize,
                Target = CommandTarget.Size,
                RequireCtrl = true
            },
            new Command() // Ctrl + Shift + S + ]: increase brush size (faster)
            {
                ActionData = "5|add",
                BuiltInShortcutId = 33,
                CommandDialogIgnore = true,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.S, System.Windows.Forms.Keys.OemCloseBrackets },
                Name = Strings.ShortcutNameIncreaseBrushSize,
                Target = CommandTarget.Size,
                RequireCtrl = true,
                RequireShift = true
            },
            new Command() // [: decrease brush size
            {
                ActionData = "1|sub",
                BuiltInShortcutId = 55,
                CommandDialogIgnore = true,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.OemOpenBrackets },
                Name = Strings.ShortcutNameIncreaseBrushSize,
                Target = CommandTarget.Size
            },
            new Command() // Ctrl + [: decrease brush size (faster)
            {
                ActionData = "5|sub",
                BuiltInShortcutId = 56,
                CommandDialogIgnore = true,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.OemOpenBrackets },
                Name = Strings.ShortcutNameIncreaseBrushSize,
                Target = CommandTarget.Size,
                RequireCtrl = true
            },
            new Command() // Ctrl + S + [: decrease brush size
            {
                ActionData = "1|sub",
                BuiltInShortcutId = 34,
                CommandDialogIgnore = true,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.S, System.Windows.Forms.Keys.OemOpenBrackets },
                Name = Strings.ShortcutNameDecreaseBrushSize,
                Target = CommandTarget.Size,
                RequireCtrl = true
            },
            new Command() // Ctrl + Shift + S + [: decrease brush size (faster)
            {
                ActionData = "5|sub",
                BuiltInShortcutId = 35,
                CommandDialogIgnore = true,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.S, System.Windows.Forms.Keys.OemOpenBrackets },
                Name = Strings.ShortcutNameDecreaseBrushSize,
                Target = CommandTarget.Size,
                RequireCtrl = true,
                RequireShift = true
            },
            new Command() // Ctrl + R + ]: increase brush angle
            {
                ActionData = "1|add",
                BuiltInShortcutId = 36,
                CommandDialogIgnore = true,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.R, System.Windows.Forms.Keys.OemCloseBrackets },
                Name = Strings.ShortcutNameRotateBrushClockwise,
                Target = CommandTarget.Rotation,
                RequireCtrl = true
            },
            new Command() // Ctrl + Shift + R + ]: increase brush angle (faster)
            {
                ActionData = "5|add",
                BuiltInShortcutId = 37,
                CommandDialogIgnore = true,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.R, System.Windows.Forms.Keys.OemCloseBrackets },
                Name = Strings.ShortcutNameRotateBrushClockwise,
                Target = CommandTarget.Rotation,
                RequireCtrl = true,
                RequireShift = true
            },
            new Command() // Ctrl + R + [: decrease brush angle
            {
                ActionData = "1|sub",
                BuiltInShortcutId = 38,
                CommandDialogIgnore = true,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.R, System.Windows.Forms.Keys.OemOpenBrackets },
                Name = Strings.ShortcutNameRotateBrushCounter,
                Target = CommandTarget.Rotation,
                RequireCtrl = true
            },
            new Command() // Ctrl + Shift + R + [: decrease brush angle (faster)
            {
                ActionData = "5|sub",
                BuiltInShortcutId = 39,
                CommandDialogIgnore = true,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.R, System.Windows.Forms.Keys.OemOpenBrackets },
                Name = Strings.ShortcutNameRotateBrushCounter,
                Target = CommandTarget.Rotation,
                RequireCtrl = true,
                RequireShift = true
            },
            new Command() // Ctrl + O + ]: increase brush opacity
            {
                ActionData = "1|add",
                BuiltInShortcutId = 40,
                CommandDialogIgnore = true,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.O, System.Windows.Forms.Keys.OemCloseBrackets },
                Name = Strings.ShortcutNameIncreaseBrushOpacity,
                Target = CommandTarget.BrushOpacity,
                RequireCtrl = true
            },
            new Command() // Ctrl + Shift + O + ]: increase brush opacity (faster)
            {
                ActionData = "5|add",
                BuiltInShortcutId = 41,
                CommandDialogIgnore = true,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.O, System.Windows.Forms.Keys.OemCloseBrackets },
                Name = Strings.ShortcutNameIncreaseBrushOpacity,
                Target = CommandTarget.BrushOpacity,
                RequireCtrl = true,
                RequireShift = true
            },
            new Command() // Ctrl + O + [: decrease brush opacity
            {
                ActionData = "1|sub",
                BuiltInShortcutId = 42,
                CommandDialogIgnore = true,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.O, System.Windows.Forms.Keys.OemOpenBrackets },
                Name = Strings.ShortcutNameDecreaseBrushOpacity,
                Target = CommandTarget.BrushOpacity,
                RequireCtrl = true
            },
            new Command() // Ctrl + Shift + O + [: decrease brush opacity (faster)
            {
                ActionData = "5|sub",
                BuiltInShortcutId = 43,
                CommandDialogIgnore = true,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.O, System.Windows.Forms.Keys.OemOpenBrackets },
                Name = Strings.ShortcutNameDecreaseBrushOpacity,
                Target = CommandTarget.BrushOpacity,
                RequireCtrl = true,
                RequireShift = true
            },
            new Command() // Ctrl + F + ]: increase brush flow
            {
                ActionData = "1|add",
                BuiltInShortcutId = 44,
                CommandDialogIgnore = true,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.F, System.Windows.Forms.Keys.OemCloseBrackets },
                Name = Strings.ShortcutNameIncreaseBrushFlow,
                Target = CommandTarget.Flow,
                RequireCtrl = true
            },
            new Command() // Ctrl + Shift + F + ]: increase brush flow (faster)
            {
                ActionData = "5|add",
                BuiltInShortcutId = 45,
                CommandDialogIgnore = true,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.F, System.Windows.Forms.Keys.OemCloseBrackets },
                Name = Strings.ShortcutNameIncreaseBrushFlow,
                Target = CommandTarget.Flow,
                RequireCtrl = true,
                RequireShift = true
            },
            new Command() // Ctrl + F + [: decrease brush flow
            {
                ActionData = "1|sub",
                BuiltInShortcutId = 46,
                CommandDialogIgnore = true,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.F, System.Windows.Forms.Keys.OemOpenBrackets },
                Name = Strings.ShortcutNameDecreaseBrushFlow,
                Target = CommandTarget.Flow,
                RequireCtrl = true
            },
            new Command() // Ctrl + Shift + F + [: decrease brush flow (faster)
            {
                ActionData = "5|sub",
                BuiltInShortcutId = 47,
                CommandDialogIgnore = true,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.F, System.Windows.Forms.Keys.OemOpenBrackets },
                Name = Strings.ShortcutNameDecreaseBrushFlow,
                Target = CommandTarget.Flow,
                RequireCtrl = true,
                RequireShift = true
            },
            new Command() // X: swap colors
            {
                ActionData = null,
                BuiltInShortcutId = 48,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.X },
                Name = Strings.ShortcutNameSwitchPrimarySecondary,
                Target = CommandTarget.SwapPrimarySecondaryColors
            },
            new Command() // /: open quick command dialog
            {
                ActionData = null,
                BuiltInShortcutId = 49,
                CommandDialogIgnore = true,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.OemQuestion },
                Name = Strings.OpenQuickCommandDialog,
                Target = CommandTarget.OpenQuickCommandDialog
            },
            new Command() // L: clone stamp tool
            {
                ActionData = $"{(int)Tool.CloneStamp},{(int)Tool.PreviousTool}|cycle",
                BuiltInShortcutId = 50,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.L },
                Name = Strings.ShortcutNameSwitchToCloneStamp,
                Target = CommandTarget.SelectedTool
            },
            new Command() // O: line tool
            {
                ActionData = $"{(int)Tool.Line},{(int)Tool.PreviousTool}|cycle",
                BuiltInShortcutId = 51,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.O },
                Name = Strings.ShortcutNameSwitchToLine,
                Target = CommandTarget.SelectedTool
            },
            new Command() // in line tool, after drawing, Enter: confirm line
            {
                BuiltInShortcutId = 52,
                CommandDialogIgnore = true,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas, CommandContext.LineToolConfirmStage },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.Enter },
                Name = Strings.ShortcutNameConfirmLine,
                Target = CommandTarget.ConfirmLine
            },
            new Command() // Ctrl + /: open script editor
            {
                ActionData = null,
                BuiltInShortcutId = 57,
                ContextsRequired = new HashSet<CommandContext>() { CommandContext.OnCanvas },
                Keys = new HashSet<System.Windows.Forms.Keys>() { System.Windows.Forms.Keys.OemQuestion },
                Name = Strings.OpenScriptEditorDialog,
                Target = CommandTarget.OpenScriptEditorDialog,
                RequireCtrl = true
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
                    BrushImagePaths = new() { Strings.DefaultBrushCircle },
                    BrushDensity = 2,
                    TabPressureConstraints = new Dictionary<CommandTarget, BrushSettingConstraint>()
                    {
                        { CommandTarget.Size, new BrushSettingConstraint(ConstraintValueHandlingMethod.Add, 10) }
                    }
                }
            },
            {
                Strings.BuiltInBrushAirbrush,
                new BrushSettings()
                {
                    BrushImagePaths = new() { Strings.DefaultBrushBigDots },
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
                    BrushImagePaths = new() { Strings.DefaultBrushGrass },
                    BrushColor = Color.FromArgb(255, 20, 192, 20).ToArgb(),
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
                    BrushImagePaths = new() { Strings.DefaultBrushCircle },
                    BrushColor = Color.Red.ToArgb(),
                    BrushSize = 30,
                    DoLockAlpha = true,
                    DoLockVal= true,
                    DoLockSat = true,
                    TabPressureConstraints = new Dictionary<CommandTarget, BrushSettingConstraint>()
                    {
                        { CommandTarget.Size, new BrushSettingConstraint(ConstraintValueHandlingMethod.Add, 10) }
                    }
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
        public HashSet<Command> CustomShortcuts { get; set; }

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
                Path.Combine(documentsDir, "paint.net User Files\\Palettes"),
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
            CustomShortcuts = new HashSet<Command>(defaultShortcuts);
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
            CustomShortcuts = new HashSet<Command>(other.CustomShortcuts);
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
        public static HashSet<Command> GetShallowShortcutsList()
        {
            return new HashSet<Command>(defaultShortcuts);
        }

        /// <summary>
        /// Returns a new hashset containing all shortcuts from the original list, plus shortcuts from the default list
        /// that aren't omitted by ID. This is intended to be used just after loading serialized results.
        /// </summary>
        public static HashSet<Command> InjectDefaultShortcuts(HashSet<Command> origList, HashSet<int> omitList)
        {
            HashSet<Command> newList = new HashSet<Command>(origList);

            foreach (Command shortcut in defaultShortcuts)
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
        public static HashSet<Command> RemoveDefaultShortcuts(HashSet<Command> combinedList)
        {
            HashSet<Command> shortcutsCopy = new HashSet<Command>(combinedList);
            shortcutsCopy.RemoveWhere((shortcut) => shortcut.BuiltInShortcutId >= 0);
            return shortcutsCopy;
        }
    }
}