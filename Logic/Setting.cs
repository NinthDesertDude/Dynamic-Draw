using System;
using System.Collections.Generic;

namespace BrushFactory
{
    /// <summary>
    /// Associates characteristics about settings for use in other areas, e.g. keyboard shortcuts.
    /// </summary>
    internal class Setting
    {
        /// <summary>
        /// Settings organized by name, including numeric range and type information.
        /// </summary>
        public static Dictionary<ShortcutTarget, Setting> AllSettings;
        static Setting()
        {
            AllSettings = new Dictionary<ShortcutTarget, Setting>()
            {
                { ShortcutTarget.Alpha, new Setting(Localization.Strings.ShortcutAlpha, 0, 99) },
                { ShortcutTarget.AlphaShift, new Setting(Localization.Strings.ShortcutAlphaShift, -100, 100) },
                { ShortcutTarget.BrushStrokeDensity, new Setting(Localization.Strings.ShortcutDensity, 0, 50) },
                { ShortcutTarget.CanvasZoom, new Setting(Localization.Strings.ShortcutCanvasZoom, 1, 1600) },
                { ShortcutTarget.Color, new Setting(Localization.Strings.BrushColor, ShortcutTargetDataType.Color) },
                { ShortcutTarget.ColorizeBrush, new Setting(Localization.Strings.ColorizeBrush, ShortcutTargetDataType.Bool) },
                { ShortcutTarget.JitterBlueMax, new Setting(Localization.Strings.ShortcutJitterBlueMax, 0, 100) },
                { ShortcutTarget.JitterBlueMin, new Setting(Localization.Strings.ShortcutJitterBlueMin, 0, 100) },
                { ShortcutTarget.JitterGreenMax, new Setting(Localization.Strings.ShortcutJitterGreenMax, 0, 100) },
                { ShortcutTarget.JitterGreenMin, new Setting(Localization.Strings.ShortcutJitterGreenMin, 0, 100) },
                { ShortcutTarget.JitterHorSpray, new Setting(Localization.Strings.ShortcutJitterHorSpray, 0, 100) },
                { ShortcutTarget.JitterHueMax, new Setting(Localization.Strings.ShortcutJitterHueMax, 0, 100) },
                { ShortcutTarget.JitterHueMin, new Setting(Localization.Strings.ShortcutJitterHueMin, 0, 100) },
                { ShortcutTarget.JitterMinAlpha, new Setting(Localization.Strings.ShortcutJitterMinAlpha, 0, 100) },
                { ShortcutTarget.JitterMaxSize, new Setting(Localization.Strings.ShortcutJitterMaxSize, 0, 1000) },
                { ShortcutTarget.JitterMinSize, new Setting(Localization.Strings.ShortcutJitterMinSize, 0, 1000) },
                { ShortcutTarget.JitterRedMax, new Setting(Localization.Strings.ShortcutJitterRedMax, 0, 100) },
                { ShortcutTarget.JitterRedMin, new Setting(Localization.Strings.ShortcutJitterRedMin, 0, 100) },
                { ShortcutTarget.JitterRotLeft, new Setting(Localization.Strings.ShortcutJitterRotLeft, 0, 180) },
                { ShortcutTarget.JitterRotRight, new Setting(Localization.Strings.ShortcutJitterRotRight, 0, 180) },
                { ShortcutTarget.JitterSatMax, new Setting(Localization.Strings.ShortcutJitterSatMax, 0, 100) },
                { ShortcutTarget.JitterSatMin, new Setting(Localization.Strings.ShortcutJitterSatMin, 0, 100) },
                { ShortcutTarget.JitterValMax, new Setting(Localization.Strings.ShortcutJitterValueMax, 0, 100) },
                { ShortcutTarget.JitterValMin, new Setting(Localization.Strings.ShortcutJitterValueMin, 0, 100) },
                { ShortcutTarget.JitterVerSpray, new Setting(Localization.Strings.ShortcutJitterVerSpray, 0, 100) },
                { ShortcutTarget.LockAlpha, new Setting(Localization.Strings.LockAlpha, ShortcutTargetDataType.Bool) },
                { ShortcutTarget.MinDrawDistance, new Setting(Localization.Strings.MinDrawDistance, 0, 100) },
                { ShortcutTarget.RedoAction, new Setting(Localization.Strings.Redo, ShortcutTargetDataType.Action) },
                { ShortcutTarget.RotateWithMouse, new Setting(Localization.Strings.OrientToMouse, ShortcutTargetDataType.Bool) },
                { ShortcutTarget.Rotation, new Setting(Localization.Strings.ShortcutRotation, -180, 180) },
                { ShortcutTarget.RotShift, new Setting(Localization.Strings.ShortcutRotShift, -180, 180) },
                { ShortcutTarget.SelectedBrush, new Setting(Localization.Strings.ShortcutSelectedBrush, ShortcutTargetDataType.String) },
                { ShortcutTarget.SelectedBrushImage, new Setting(Localization.Strings.ShortcutSelectedBrushImage, ShortcutTargetDataType.String) },
                { ShortcutTarget.SelectedTool, new Setting(Localization.Strings.ShortcutSelectedTool, 0, Enum.GetValues(typeof(Tool)).Length - 1) },
                { ShortcutTarget.Size, new Setting(Localization.Strings.ShortcutSize, 1, 1000) },
                { ShortcutTarget.SizeShift, new Setting(Localization.Strings.ShortcutSizeShift, -1000, 1000) },
                { ShortcutTarget.SmoothingMode, new Setting(Localization.Strings.ShortcutBrushSmoothing, 0, Enum.GetValues(typeof(CmbxSmoothing.Smoothing)).Length - 1) },
                { ShortcutTarget.SymmetryMode, new Setting(Localization.Strings.ShortcutSymmetryMode, 0, Enum.GetValues(typeof(SymmetryMode)).Length - 1) },
                { ShortcutTarget.TabPressureAlpha, new Setting(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutAlpha), 0, 99) },
                { ShortcutTarget.TabPressureBrushDensity, new Setting(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutDensity), -50, 50) },
                { ShortcutTarget.TabPressureJitterBlueMax, new Setting(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterBlueMax), -100, 100) },
                { ShortcutTarget.TabPressureJitterBlueMin, new Setting(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterBlueMin), -100, 100) },
                { ShortcutTarget.TabPressureJitterGreenMax, new Setting(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterGreenMax), -100, 100) },
                { ShortcutTarget.TabPressureJitterGreenMin, new Setting(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterGreenMin), -100, 100) },
                { ShortcutTarget.TabPressureJitterHorShift, new Setting(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterHorSpray), -100, 100) },
                { ShortcutTarget.TabPressureJitterHueMax, new Setting(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterHueMax), -100, 100) },
                { ShortcutTarget.TabPressureJitterHueMin, new Setting(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterHueMin), -100, 100) },
                { ShortcutTarget.TabPressureJitterMaxSize, new Setting(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterMaxSize), -1000, 1000) },
                { ShortcutTarget.TabPressureJitterMinAlpha, new Setting(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterMinAlpha), -100, 100) },
                { ShortcutTarget.TabPressureJitterMinSize, new Setting(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterMinSize), -1000, 1000) },
                { ShortcutTarget.TabPressureJitterRedMax, new Setting(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterRedMax), -100, 100) },
                { ShortcutTarget.TabPressureJitterRedMin, new Setting(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterRedMin), -100, 100) },
                { ShortcutTarget.TabPressureJitterRotLeft, new Setting(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterRotLeft), -360, 360) },
                { ShortcutTarget.TabPressureJitterRotRight, new Setting(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterRotRight), -360, 360) },
                { ShortcutTarget.TabPressureJitterSatMax, new Setting(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterSatMax), -100, 100) },
                { ShortcutTarget.TabPressureJitterSatMin, new Setting(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterSatMin), -100, 100) },
                { ShortcutTarget.TabPressureJitterValMax, new Setting(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterValueMax), -100, 100) },
                { ShortcutTarget.TabPressureJitterValMin, new Setting(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterValueMin), -100, 100) },
                { ShortcutTarget.TabPressureJitterVerShift, new Setting(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutJitterVerSpray), -100, 100) },
                { ShortcutTarget.TabPressureMinDrawDistance, new Setting(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.MinDrawDistance), -100, 100) },
                { ShortcutTarget.TabPressureSize, new Setting(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutSize), -1000, 1000) },
                { ShortcutTarget.TabPressureRotation, new Setting(string.Format(Localization.Strings.TabPressureSetting, Localization.Strings.TabPressure, Localization.Strings.ShortcutRotation), -180, 180) },
                { ShortcutTarget.UndoAction, new Setting(Localization.Strings.Undo, ShortcutTargetDataType.Action) }
            };
        }

        public string Name { get; set; }

        public ShortcutTargetDataType ValueType { get; set; }

        public Tuple<int, int> MinMaxRange { get; set; } = null;

        /// <summary>
        /// Defines a setting with an integer data type, including the min/max range allowed (both bounds inclusive).
        /// </summary>
        public Setting(string name, int min, int max)
        {
            Name = name;
            ValueType = ShortcutTargetDataType.Integer;
            MinMaxRange = new Tuple<int, int>(min, max);
        }

        /// <summary>
        /// Defines a setting with a bool, color, or string data type. Any other type given will throw an exception.
        /// </summary>
        public Setting(string name, ShortcutTargetDataType typeWithoutData)
        {
            Name = name;

            if (typeWithoutData != ShortcutTargetDataType.Bool &&
                typeWithoutData != ShortcutTargetDataType.Color &&
                typeWithoutData != ShortcutTargetDataType.String &&
                typeWithoutData != ShortcutTargetDataType.Action)
            {
                throw new ArgumentException("Only data types without data are allowed by this constructor.");
            }

            ValueType = typeWithoutData;
        }

        /// <summary>
        /// Returns true if the setting doesn't make use of numeric min/max ranges, or if the given value falls within
        /// the setting's min and max ranges.
        /// </summary>
        /// <param name="input">A value that may or may not fit within the range allowed by the setting.</param>
        public bool ValidateNumberValue(int input)
        {
            if (ValueType == ShortcutTargetDataType.Integer)
            {
                return input >= MinMaxRange.Item1 && input <= MinMaxRange.Item2;
            }

            return true;
        }
    }
}