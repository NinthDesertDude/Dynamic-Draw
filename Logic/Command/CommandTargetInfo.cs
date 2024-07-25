using System;
using System.Collections.Generic;

namespace DynamicDraw
{
    /// <summary>
    /// Exposes the list of default commands.
    /// </summary>
    internal class CommandTargetInfo
    {
        /// <summary>
        /// Commands indexed by target, including display name and action data type info.
        /// </summary>
        public static Dictionary<CommandTarget, CommandTargetInfo> All;

        static CommandTargetInfo()
        {
            All = new Dictionary<CommandTarget, CommandTargetInfo>()
            {
                { CommandTarget.AutomaticBrushDensity, new CommandTargetInfo(Localization.Strings.AutomaticBrushDensity, CommandActionDataType.Bool) },
                { CommandTarget.BrushStrokeDensity, new CommandTargetInfo(Localization.Strings.ShortcutDensity, 0, 50) },
                { CommandTarget.CanvasZoom, new CommandTargetInfo(Localization.Strings.ShortcutCanvasZoom, 1, 6400) },
                { CommandTarget.Color, new CommandTargetInfo(Localization.Strings.BrushColor, CommandActionDataType.Color) },
                { CommandTarget.ColorizeBrush, new CommandTargetInfo(Localization.Strings.ColorizeBrush, CommandActionDataType.Bool) },
                { CommandTarget.Flow, new CommandTargetInfo(Localization.Strings.ShortcutFlow, 0, 255) },
                { CommandTarget.FlowShift, new CommandTargetInfo(Localization.Strings.ShortcutFlowShift, -255, 255) },
                { CommandTarget.JitterBlueMax, new CommandTargetInfo(Localization.Strings.ShortcutJitterBlueMax, 0, 100) },
                { CommandTarget.JitterBlueMin, new CommandTargetInfo(Localization.Strings.ShortcutJitterBlueMin, 0, 100) },
                { CommandTarget.JitterGreenMax, new CommandTargetInfo(Localization.Strings.ShortcutJitterGreenMax, 0, 100) },
                { CommandTarget.JitterGreenMin, new CommandTargetInfo(Localization.Strings.ShortcutJitterGreenMin, 0, 100) },
                { CommandTarget.JitterHorSpray, new CommandTargetInfo(Localization.Strings.ShortcutJitterHorSpray, 0, 100) },
                { CommandTarget.JitterHueMax, new CommandTargetInfo(Localization.Strings.ShortcutJitterHueMax, 0, 100) },
                { CommandTarget.JitterHueMin, new CommandTargetInfo(Localization.Strings.ShortcutJitterHueMin, 0, 100) },
                { CommandTarget.JitterFlowLoss, new CommandTargetInfo(Localization.Strings.ShortcutJitterFlowLoss, 0, 255) },
                { CommandTarget.JitterMaxSize, new CommandTargetInfo(Localization.Strings.ShortcutJitterMaxSize, 0, 1000) },
                { CommandTarget.JitterMinSize, new CommandTargetInfo(Localization.Strings.ShortcutJitterMinSize, 0, 1000) },
                { CommandTarget.JitterRedMax, new CommandTargetInfo(Localization.Strings.ShortcutJitterRedMax, 0, 100) },
                { CommandTarget.JitterRedMin, new CommandTargetInfo(Localization.Strings.ShortcutJitterRedMin, 0, 100) },
                { CommandTarget.JitterRotLeft, new CommandTargetInfo(Localization.Strings.ShortcutJitterRotLeft, 0, 180) },
                { CommandTarget.JitterRotRight, new CommandTargetInfo(Localization.Strings.ShortcutJitterRotRight, 0, 180) },
                { CommandTarget.JitterSatMax, new CommandTargetInfo(Localization.Strings.ShortcutJitterSatMax, 0, 100) },
                { CommandTarget.JitterSatMin, new CommandTargetInfo(Localization.Strings.ShortcutJitterSatMin, 0, 100) },
                { CommandTarget.JitterValMax, new CommandTargetInfo(Localization.Strings.ShortcutJitterValueMax, 0, 100) },
                { CommandTarget.JitterValMin, new CommandTargetInfo(Localization.Strings.ShortcutJitterValueMin, 0, 100) },
                { CommandTarget.JitterVerSpray, new CommandTargetInfo(Localization.Strings.ShortcutJitterVerSpray, 0, 100) },
                { CommandTarget.DoLockAlpha, new CommandTargetInfo(Localization.Strings.LockAlpha, CommandActionDataType.Bool) },
                { CommandTarget.MinDrawDistance, new CommandTargetInfo(Localization.Strings.ShortcutMinDrawDist, 0, 100) },
                { CommandTarget.RedoAction, new CommandTargetInfo(Localization.Strings.Redo, CommandActionDataType.Action) },
                { CommandTarget.RotateWithMouse, new CommandTargetInfo(Localization.Strings.OrientToMouse, CommandActionDataType.Bool) },
                { CommandTarget.Rotation, new CommandTargetInfo(Localization.Strings.ShortcutRotation, -180, 180) },
                { CommandTarget.RotShift, new CommandTargetInfo(Localization.Strings.ShortcutRotShift, -180, 180) },
                { CommandTarget.SelectedBrush, new CommandTargetInfo(Localization.Strings.ShortcutSelectedBrush, CommandActionDataType.String) },
                { CommandTarget.SelectedBrushImage, new CommandTargetInfo(Localization.Strings.ShortcutSelectedBrushImage, CommandActionDataType.String) },
                { CommandTarget.SelectedTool, new CommandTargetInfo(Localization.Strings.ShortcutSelectedTool, 0, Enum.GetValues(typeof(Tool)).Length - 1) },
                { CommandTarget.Size, new CommandTargetInfo(Localization.Strings.ShortcutSize, 1, 1000) },
                { CommandTarget.SizeShift, new CommandTargetInfo(Localization.Strings.ShortcutSizeShift, -1000, 1000) },
                { CommandTarget.SmoothingMode, new CommandTargetInfo(Localization.Strings.ShortcutBrushSmoothing, 0, Enum.GetValues(typeof(CmbxSmoothing.Smoothing)).Length - 1) },
                { CommandTarget.SymmetryMode, new CommandTargetInfo(Localization.Strings.ShortcutSymmetryMode, 0, Enum.GetValues(typeof(SymmetryMode)).Length - 1) },
                { CommandTarget.TabPressureBrushDensity, new CommandTargetInfo(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutDensity), -50, 50) },
                { CommandTarget.TabPressureFlow, new CommandTargetInfo(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutFlow), 0, 255) },
                { CommandTarget.TabPressureJitterBlueMax, new CommandTargetInfo(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterBlueMax), -100, 100) },
                { CommandTarget.TabPressureJitterBlueMin, new CommandTargetInfo(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterBlueMin), -100, 100) },
                { CommandTarget.TabPressureJitterFlowLoss, new CommandTargetInfo(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterFlowLoss), -255, 255) },
                { CommandTarget.TabPressureJitterGreenMax, new CommandTargetInfo(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterGreenMax), -100, 100) },
                { CommandTarget.TabPressureJitterGreenMin, new CommandTargetInfo(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterGreenMin), -100, 100) },
                { CommandTarget.TabPressureJitterHorShift, new CommandTargetInfo(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterHorSpray), -100, 100) },
                { CommandTarget.TabPressureJitterHueMax, new CommandTargetInfo(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterHueMax), -100, 100) },
                { CommandTarget.TabPressureJitterHueMin, new CommandTargetInfo(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterHueMin), -100, 100) },
                { CommandTarget.TabPressureJitterMaxSize, new CommandTargetInfo(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterMaxSize), -1000, 1000) },
                { CommandTarget.TabPressureJitterMinSize, new CommandTargetInfo(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterMinSize), -1000, 1000) },
                { CommandTarget.TabPressureJitterRedMax, new CommandTargetInfo(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterRedMax), -100, 100) },
                { CommandTarget.TabPressureJitterRedMin, new CommandTargetInfo(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterRedMin), -100, 100) },
                { CommandTarget.TabPressureJitterRotLeft, new CommandTargetInfo(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterRotLeft), -360, 360) },
                { CommandTarget.TabPressureJitterRotRight, new CommandTargetInfo(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterRotRight), -360, 360) },
                { CommandTarget.TabPressureJitterSatMax, new CommandTargetInfo(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterSatMax), -100, 100) },
                { CommandTarget.TabPressureJitterSatMin, new CommandTargetInfo(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterSatMin), -100, 100) },
                { CommandTarget.TabPressureJitterValMax, new CommandTargetInfo(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterValueMax), -100, 100) },
                { CommandTarget.TabPressureJitterValMin, new CommandTargetInfo(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterValueMin), -100, 100) },
                { CommandTarget.TabPressureJitterVerShift, new CommandTargetInfo(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterVerSpray), -100, 100) },
                { CommandTarget.TabPressureMinDrawDistance, new CommandTargetInfo(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.MinDrawDistance), -100, 100) },
                { CommandTarget.TabPressureSize, new CommandTargetInfo(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutSize), -1000, 1000) },
                { CommandTarget.TabPressureRotation, new CommandTargetInfo(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutRotation), -180, 180) },
                { CommandTarget.UndoAction, new CommandTargetInfo(Localization.Strings.Undo, CommandActionDataType.Action) },
                { CommandTarget.ResetCanvasTransforms, new CommandTargetInfo(Localization.Strings.ShortcutResetCanvas, CommandActionDataType.Action) },
                { CommandTarget.CanvasX, new CommandTargetInfo(Localization.Strings.ShortcutNudgeCanvasX, -2000, 2000) },
                { CommandTarget.CanvasY, new CommandTargetInfo(Localization.Strings.ShortcutNudgeCanvasY, -2000, 2000) },
                { CommandTarget.CanvasRotation, new CommandTargetInfo(Localization.Strings.ShortcutRotateCanvas, -180, 180) },
                { CommandTarget.BlendMode, new CommandTargetInfo(Localization.Strings.ShortcutBlendMode, 0, Enum.GetValues(typeof(BlendMode)).Length - 1) },
                { CommandTarget.SeamlessDrawing, new CommandTargetInfo(Localization.Strings.SeamlessDrawing, CommandActionDataType.Bool) },
                { CommandTarget.ColorInfluence, new CommandTargetInfo(Localization.Strings.ShortcutColorInfluence, 0, 100) },
                { CommandTarget.ColorInfluenceHue, new CommandTargetInfo(Localization.Strings.HueAbbr, CommandActionDataType.Bool) },
                { CommandTarget.ColorInfluenceSat, new CommandTargetInfo(Localization.Strings.SatAbbr, CommandActionDataType.Bool) },
                { CommandTarget.ColorInfluenceVal, new CommandTargetInfo(Localization.Strings.ValAbbr, CommandActionDataType.Bool) },
                { CommandTarget.DitherDraw, new CommandTargetInfo(Localization.Strings.DitherDraw, CommandActionDataType.Bool) },
                { CommandTarget.DoLockR, new CommandTargetInfo(Localization.Strings.ShortcutLockR, CommandActionDataType.Bool) },
                { CommandTarget.DoLockG, new CommandTargetInfo(Localization.Strings.ShortcutLockG, CommandActionDataType.Bool) },
                { CommandTarget.DoLockB, new CommandTargetInfo(Localization.Strings.ShortcutLockB, CommandActionDataType.Bool) },
                { CommandTarget.DoLockHue, new CommandTargetInfo(Localization.Strings.ShortcutLockHue, CommandActionDataType.Bool) },
                { CommandTarget.DoLockSat, new CommandTargetInfo(Localization.Strings.ShortcutLockSat, CommandActionDataType.Bool) },
                { CommandTarget.DoLockVal, new CommandTargetInfo(Localization.Strings.ShortcutLockVal, CommandActionDataType.Bool) },
                { CommandTarget.BrushOpacity, new CommandTargetInfo(Localization.Strings.ShortcutBrushOpacity, 0, 255) },
                { CommandTarget.CanvasZoomToMouse, new CommandTargetInfo(Localization.Strings.ShortcutCanvasZoom, 1, 6400) },
                { CommandTarget.ChosenEffect, new CommandTargetInfo(Localization.Strings.ShortcutChosenEffect, 1, 1000) },
                { CommandTarget.CanvasZoomFit, new CommandTargetInfo(Localization.Strings.ShortcutCanvasZoom, CommandActionDataType.Action) },
                { CommandTarget.SwapPrimarySecondaryColors, new CommandTargetInfo(Localization.Strings.ShortcutSwapColors, CommandActionDataType.Action) },
                { CommandTarget.OpenColorPickerDialog, new CommandTargetInfo(Localization.Strings.OpenColorPickerDialog, CommandActionDataType.Action) },
                { CommandTarget.OpenQuickCommandDialog, new CommandTargetInfo(Localization.Strings.OpenQuickCommandDialog, CommandActionDataType.Action) },
                { CommandTarget.SwitchPalette, new CommandTargetInfo(Localization.Strings.ShortcutNameSwitchPalette, 0, 400) },
                { CommandTarget.PickFromPalette, new CommandTargetInfo(Localization.Strings.ShortcutNamePickFromPalette, 0, ColorUtils.MaxPaletteSize - 1) },
                { CommandTarget.ConfirmLine, new CommandTargetInfo(Localization.Strings.ShortcutNameConfirmLine, CommandActionDataType.Action) }
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
        public CommandTargetInfo(string name, int min, int max)
        {
            Name = name;
            ValueType = CommandActionDataType.Integer;
            MinMaxRange = new Tuple<int, int>(min, max);
        }

        /// <summary>
        /// Defines a command with a float data type, including the min/max range allowed (both bounds inclusive).
        /// </summary>
        public CommandTargetInfo(string name, float min, float max)
        {
            Name = name;
            ValueType = CommandActionDataType.Float;
            MinMaxRangeF = new Tuple<float, float>(min, max);
        }

        /// <summary>
        /// Defines a command with a bool, color, or string data type. Any other type given will throw an exception.
        /// </summary>
        public CommandTargetInfo(string name, CommandActionDataType typeWithoutData)
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
        private CommandTargetInfo() { }
    }
}