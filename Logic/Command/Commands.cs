using System;
using System.Collections.Generic;

namespace DynamicDraw
{
    /// <summary>
    /// Exposes the list of default commands.
    /// </summary>
    internal class Commands
    {
        /// <summary>
        /// Commands indexed by target, including display name and action data type info.
        /// </summary>
        public static Dictionary<CommandTarget, Commands> All;

        static Commands()
        {
            All = new Dictionary<CommandTarget, Commands>()
            {
                { CommandTarget.AutomaticBrushDensity, new Commands(Localization.Strings.AutomaticBrushDensity, CommandActionDataType.Bool) },
                { CommandTarget.BrushStrokeDensity, new Commands(Localization.Strings.ShortcutDensity, 0, 50) },
                { CommandTarget.CanvasZoom, new Commands(Localization.Strings.ShortcutCanvasZoom, 1, 6400) },
                { CommandTarget.Color, new Commands(Localization.Strings.BrushColor, CommandActionDataType.Color) },
                { CommandTarget.ColorizeBrush, new Commands(Localization.Strings.ColorizeBrush, CommandActionDataType.Bool) },
                { CommandTarget.Flow, new Commands(Localization.Strings.ShortcutFlow, 0, 255) },
                { CommandTarget.FlowShift, new Commands(Localization.Strings.ShortcutFlowShift, -255, 255) },
                { CommandTarget.JitterBlueMax, new Commands(Localization.Strings.ShortcutJitterBlueMax, 0, 100) },
                { CommandTarget.JitterBlueMin, new Commands(Localization.Strings.ShortcutJitterBlueMin, 0, 100) },
                { CommandTarget.JitterGreenMax, new Commands(Localization.Strings.ShortcutJitterGreenMax, 0, 100) },
                { CommandTarget.JitterGreenMin, new Commands(Localization.Strings.ShortcutJitterGreenMin, 0, 100) },
                { CommandTarget.JitterHorSpray, new Commands(Localization.Strings.ShortcutJitterHorSpray, 0, 100) },
                { CommandTarget.JitterHueMax, new Commands(Localization.Strings.ShortcutJitterHueMax, 0, 100) },
                { CommandTarget.JitterHueMin, new Commands(Localization.Strings.ShortcutJitterHueMin, 0, 100) },
                { CommandTarget.JitterFlowLoss, new Commands(Localization.Strings.ShortcutJitterFlowLoss, 0, 255) },
                { CommandTarget.JitterMaxSize, new Commands(Localization.Strings.ShortcutJitterMaxSize, 0, 1000) },
                { CommandTarget.JitterMinSize, new Commands(Localization.Strings.ShortcutJitterMinSize, 0, 1000) },
                { CommandTarget.JitterRedMax, new Commands(Localization.Strings.ShortcutJitterRedMax, 0, 100) },
                { CommandTarget.JitterRedMin, new Commands(Localization.Strings.ShortcutJitterRedMin, 0, 100) },
                { CommandTarget.JitterRotLeft, new Commands(Localization.Strings.ShortcutJitterRotLeft, 0, 180) },
                { CommandTarget.JitterRotRight, new Commands(Localization.Strings.ShortcutJitterRotRight, 0, 180) },
                { CommandTarget.JitterSatMax, new Commands(Localization.Strings.ShortcutJitterSatMax, 0, 100) },
                { CommandTarget.JitterSatMin, new Commands(Localization.Strings.ShortcutJitterSatMin, 0, 100) },
                { CommandTarget.JitterValMax, new Commands(Localization.Strings.ShortcutJitterValueMax, 0, 100) },
                { CommandTarget.JitterValMin, new Commands(Localization.Strings.ShortcutJitterValueMin, 0, 100) },
                { CommandTarget.JitterVerSpray, new Commands(Localization.Strings.ShortcutJitterVerSpray, 0, 100) },
                { CommandTarget.DoLockAlpha, new Commands(Localization.Strings.LockAlpha, CommandActionDataType.Bool) },
                { CommandTarget.MinDrawDistance, new Commands(Localization.Strings.ShortcutMinDrawDist, 0, 100) },
                { CommandTarget.RedoAction, new Commands(Localization.Strings.Redo, CommandActionDataType.Action) },
                { CommandTarget.RotateWithMouse, new Commands(Localization.Strings.OrientToMouse, CommandActionDataType.Bool) },
                { CommandTarget.Rotation, new Commands(Localization.Strings.ShortcutRotation, -180, 180) },
                { CommandTarget.RotShift, new Commands(Localization.Strings.ShortcutRotShift, -180, 180) },
                { CommandTarget.SelectedBrush, new Commands(Localization.Strings.ShortcutSelectedBrush, CommandActionDataType.String) },
                { CommandTarget.SelectedBrushImage, new Commands(Localization.Strings.ShortcutSelectedBrushImage, CommandActionDataType.String) },
                { CommandTarget.SelectedTool, new Commands(Localization.Strings.ShortcutSelectedTool, 0, Enum.GetValues(typeof(Tool)).Length - 1) },
                { CommandTarget.Size, new Commands(Localization.Strings.ShortcutSize, 1, 1000) },
                { CommandTarget.SizeShift, new Commands(Localization.Strings.ShortcutSizeShift, -1000, 1000) },
                { CommandTarget.SmoothingMode, new Commands(Localization.Strings.ShortcutBrushSmoothing, 0, Enum.GetValues(typeof(CmbxSmoothing.Smoothing)).Length - 1) },
                { CommandTarget.SymmetryMode, new Commands(Localization.Strings.ShortcutSymmetryMode, 0, Enum.GetValues(typeof(SymmetryMode)).Length - 1) },
                { CommandTarget.TabPressureBrushDensity, new Commands(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutDensity), -50, 50) },
                { CommandTarget.TabPressureFlow, new Commands(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutFlow), 0, 255) },
                { CommandTarget.TabPressureJitterBlueMax, new Commands(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterBlueMax), -100, 100) },
                { CommandTarget.TabPressureJitterBlueMin, new Commands(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterBlueMin), -100, 100) },
                { CommandTarget.TabPressureJitterFlowLoss, new Commands(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterFlowLoss), -255, 255) },
                { CommandTarget.TabPressureJitterGreenMax, new Commands(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterGreenMax), -100, 100) },
                { CommandTarget.TabPressureJitterGreenMin, new Commands(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterGreenMin), -100, 100) },
                { CommandTarget.TabPressureJitterHorShift, new Commands(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterHorSpray), -100, 100) },
                { CommandTarget.TabPressureJitterHueMax, new Commands(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterHueMax), -100, 100) },
                { CommandTarget.TabPressureJitterHueMin, new Commands(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterHueMin), -100, 100) },
                { CommandTarget.TabPressureJitterMaxSize, new Commands(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterMaxSize), -1000, 1000) },
                { CommandTarget.TabPressureJitterMinSize, new Commands(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterMinSize), -1000, 1000) },
                { CommandTarget.TabPressureJitterRedMax, new Commands(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterRedMax), -100, 100) },
                { CommandTarget.TabPressureJitterRedMin, new Commands(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterRedMin), -100, 100) },
                { CommandTarget.TabPressureJitterRotLeft, new Commands(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterRotLeft), -360, 360) },
                { CommandTarget.TabPressureJitterRotRight, new Commands(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterRotRight), -360, 360) },
                { CommandTarget.TabPressureJitterSatMax, new Commands(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterSatMax), -100, 100) },
                { CommandTarget.TabPressureJitterSatMin, new Commands(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterSatMin), -100, 100) },
                { CommandTarget.TabPressureJitterValMax, new Commands(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterValueMax), -100, 100) },
                { CommandTarget.TabPressureJitterValMin, new Commands(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterValueMin), -100, 100) },
                { CommandTarget.TabPressureJitterVerShift, new Commands(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterVerSpray), -100, 100) },
                { CommandTarget.TabPressureMinDrawDistance, new Commands(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.MinDrawDistance), -100, 100) },
                { CommandTarget.TabPressureSize, new Commands(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutSize), -1000, 1000) },
                { CommandTarget.TabPressureRotation, new Commands(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutRotation), -180, 180) },
                { CommandTarget.UndoAction, new Commands(Localization.Strings.Undo, CommandActionDataType.Action) },
                { CommandTarget.ResetCanvasTransforms, new Commands(Localization.Strings.ShortcutResetCanvas, CommandActionDataType.Action) },
                { CommandTarget.CanvasX, new Commands(Localization.Strings.ShortcutNudgeCanvasX, -2000, 2000) },
                { CommandTarget.CanvasY, new Commands(Localization.Strings.ShortcutNudgeCanvasY, -2000, 2000) },
                { CommandTarget.CanvasRotation, new Commands(Localization.Strings.ShortcutRotateCanvas, -180, 180) },
                { CommandTarget.BlendMode, new Commands(Localization.Strings.ShortcutBlendMode, 0, Enum.GetValues(typeof(BlendMode)).Length - 1) },
                { CommandTarget.SeamlessDrawing, new Commands(Localization.Strings.SeamlessDrawing, CommandActionDataType.Bool) },
                { CommandTarget.ColorInfluence, new Commands(Localization.Strings.ShortcutColorInfluence, 0, 100) },
                { CommandTarget.ColorInfluenceHue, new Commands(Localization.Strings.HueAbbr, CommandActionDataType.Bool) },
                { CommandTarget.ColorInfluenceSat, new Commands(Localization.Strings.SatAbbr, CommandActionDataType.Bool) },
                { CommandTarget.ColorInfluenceVal, new Commands(Localization.Strings.ValAbbr, CommandActionDataType.Bool) },
                { CommandTarget.DitherDraw, new Commands(Localization.Strings.DitherDraw, CommandActionDataType.Bool) },
                { CommandTarget.DoLockR, new Commands(Localization.Strings.ShortcutLockR, CommandActionDataType.Bool) },
                { CommandTarget.DoLockG, new Commands(Localization.Strings.ShortcutLockG, CommandActionDataType.Bool) },
                { CommandTarget.DoLockB, new Commands(Localization.Strings.ShortcutLockB, CommandActionDataType.Bool) },
                { CommandTarget.DoLockHue, new Commands(Localization.Strings.ShortcutLockHue, CommandActionDataType.Bool) },
                { CommandTarget.DoLockSat, new Commands(Localization.Strings.ShortcutLockSat, CommandActionDataType.Bool) },
                { CommandTarget.DoLockVal, new Commands(Localization.Strings.ShortcutLockVal, CommandActionDataType.Bool) },
                { CommandTarget.BrushOpacity, new Commands(Localization.Strings.ShortcutBrushOpacity, 0, 255) },
                { CommandTarget.CanvasZoomToMouse, new Commands(Localization.Strings.ShortcutCanvasZoom, 1, 6400) },
                { CommandTarget.ChosenEffect, new Commands(Localization.Strings.ShortcutChosenEffect, 1, 1000) },
                { CommandTarget.CanvasZoomFit, new Commands(Localization.Strings.ShortcutCanvasZoom, CommandActionDataType.Action) },
                { CommandTarget.SwapPrimarySecondaryColors, new Commands(Localization.Strings.ShortcutSwapColors, CommandActionDataType.Action) },
                { CommandTarget.OpenColorPickerDialog, new Commands(Localization.Strings.OpenColorPickerDialog, CommandActionDataType.Action) },
                { CommandTarget.OpenQuickCommandDialog, new Commands(Localization.Strings.OpenQuickCommandDialog, CommandActionDataType.Action) },
                { CommandTarget.SwitchPalette, new Commands(Localization.Strings.ShortcutNameSwitchPalette, 0, 400) },
                { CommandTarget.PickFromPalette, new Commands(Localization.Strings.ShortcutNamePickFromPalette, 0, ColorUtils.MaxPaletteSize - 1) },
                { CommandTarget.ConfirmLine, new Commands(Localization.Strings.ShortcutNameConfirmLine, CommandActionDataType.Action) }
            };
        }

        /// <summary>
        /// The human-friendly name of the command.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The type of action data associated to the command.
        /// </summary>
        public CommandActionDataType ValueType { get; set; }

        /// <summary>
        /// For <see cref="CommandActionDataType.Integer"/> data types, this is the min and max numeric range as ints.
        /// </summary>
        public Tuple<int, int> MinMaxRange { get; set; } = null;

        /// <summary>
        /// For <see cref="CommandActionDataType.Float"/> data types, this is the min and max numeric range as floats.
        /// </summary>
        public Tuple<float, float> MinMaxRangeF { get; set; } = null;

        /// <summary>
        /// Defines a command with an integer data type, including the min/max range allowed (both bounds inclusive).
        /// </summary>
        public Commands(string name, int min, int max)
        {
            Name = name;
            ValueType = CommandActionDataType.Integer;
            MinMaxRange = new Tuple<int, int>(min, max);
        }

        /// <summary>
        /// Defines a command with a float data type, including the min/max range allowed (both bounds inclusive).
        /// </summary>
        public Commands(string name, float min, float max)
        {
            Name = name;
            ValueType = CommandActionDataType.Float;
            MinMaxRangeF = new Tuple<float, float>(min, max);
        }

        /// <summary>
        /// Defines a command with a bool, color, or string data type. Any other type given will throw an exception.
        /// </summary>
        public Commands(string name, CommandActionDataType typeWithoutData)
        {
            Name = name;

            if (typeWithoutData != CommandActionDataType.Bool &&
                typeWithoutData != CommandActionDataType.Color &&
                typeWithoutData != CommandActionDataType.String &&
                typeWithoutData != CommandActionDataType.Action)
            {
                throw new ArgumentException("Only data types without data are allowed by this constructor.");
            }

            ValueType = typeWithoutData;
        }

        /// <summary>
        /// Returns true if the command doesn't make use of numeric min/max ranges, or if the given value falls within
        /// the command's min and max ranges.
        /// </summary>
        /// <param name="input">A value that may or may not fit within the range allowed by the command.</param>
        public bool ValidateNumberValue(int input)
        {
            if (ValueType == CommandActionDataType.Integer)
            {
                return input >= MinMaxRange.Item1 && input <= MinMaxRange.Item2;
            }

            return true;
        }

        /// <summary>
        /// Returns true if the command doesn't make use of numeric min/max ranges, or if the given value falls within
        /// the command's min and max ranges.
        /// </summary>
        /// <param name="input">A value that may or may not fit within the range allowed by the command.</param>
        public bool ValidateNumberValue(float input)
        {
            if (ValueType == CommandActionDataType.Float)
            {
                return input >= MinMaxRangeF.Item1 && input <= MinMaxRangeF.Item2;
            }

            return true;
        }

        /// <summary>
        /// Prevent instantiation.
        /// </summary>
        private Commands() { }
    }
}