using PaintDotNet;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace DynamicDraw
{
    /// <summary>
    /// Represents the metadata of one argument for a command target. This abstraction allows command targets to
    /// associate multiple types of data to themselves, which is used for automatic input validation and clamping in
    /// places such as keyboard shortcuts and user scripts.
    /// </summary>
    public class CommandTargetArgument
    {
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
        /// Defines an argument with an integer data type, including the min/max range allowed (both bounds inclusive).
        /// </summary>
        public CommandTargetArgument(int min, int max)
        {
            ValueType = CommandActionDataType.Integer;
            MinMaxRange = new Tuple<int, int>(min, max);
        }

        /// <summary>
        /// Defines an argument with a float data type, including the min/max range allowed (both bounds inclusive).
        /// </summary>
        public CommandTargetArgument(float min, float max)
        {
            ValueType = CommandActionDataType.Float;
            MinMaxRangeF = new Tuple<float, float>(min, max);
        }

        /// <summary>
        /// Defines an argument with a bool, color, or string data type. Any other type given will throw an exception.
        /// </summary>
        public CommandTargetArgument(CommandActionDataType typeWithoutData)
        {
            if (typeWithoutData != CommandActionDataType.Bool &&
                typeWithoutData != CommandActionDataType.Color &&
                typeWithoutData != CommandActionDataType.String)
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
        /// Inteprets the data as representing a color in the six-digit lowercase hex format.
        /// Assumes the data is already in the proper format. Use <see cref="IsActionValid"/> to ensure.
        /// </summary>
        public Color GetDataAsColor(string actiondata, Color origColor)
        {
            string[] chunks = actiondata.Split('|');
            if (chunks.Length != 2)
            {
                actiondata = $"{actiondata}|set";
                chunks = actiondata.Split('|');
            }

            if (ValueType != CommandActionDataType.Color)
            {
                throw new Exception("Was expecting command target to be of the color data type.");
            }

            /** Cycles the list of numbers based on the current value. Valid syntaxes:
             * cycle: selects next item, or first item if current value isn't in list.
             * cycle--stop: same as cycle, but does nothing if at end of list.
             * cycle-add: selects next item, or nearest greater number if current value isn't in list.
             * cycle-add-stop: same as cycle-add, but does nothing if at end of list.
             * cycle-sub: selects prev item, or nearest smaller number if current value isn't in list.
             * cycle-sub-stop: same as cycle-sub, but does nothing if at start of list.
             */
            if (chunks[1].StartsWith("cycle"))
            {
                List<Color> colors = new List<Color>();
                string[] colorStrings = chunks[0].Split(",");
                for (int i = 0; i < colorStrings.Length; i++)
                {
                    Color? color = ColorUtils.GetColorFromText(colorStrings[i], true);
                    if (color == null)
                    {
                        throw new Exception("Was expecting command to contain purely color data with comma delimiters.");
                    }

                    colors.Add((Color)color);
                }

                if (colors.Count == 0)
                {
                    throw new Exception("Was expecting command to contain at least one color for cycle.");
                }

                string[] actionDataOptions = chunks[1].Split("-");
                bool doAdd = actionDataOptions.Length > 1 && actionDataOptions[1].Equals("add");
                bool doSub = actionDataOptions.Length > 1 && actionDataOptions[1].Equals("sub");
                bool doStop = actionDataOptions.Length > 2 && actionDataOptions[2].Equals("stop");

                // Jump to the exact color.
                int nearestValueIndex = -1;
                int nearestRGBDiff = 255 + 255 + 255;
                for (int i = 0; i < colors.Count; i++)
                {
                    // If a color in the cycle is matched, shift to the next/prev.
                    if (origColor == colors[i])
                    {
                        if (doSub)
                        {
                            return (i != 0)
                                ? colors[i - 1]
                                : doStop ? origColor : colors[^1];
                        }

                        return (i < colors.Count - 1)
                            ? colors[i + 1]
                            : doStop ? origColor : colors[0];
                    }

                    // While no color is matched, track the nearest one to snap to.
                    int rDiff = (origColor.R - colors[i].R) * (doSub ? -1 : 1);
                    int gDiff = (origColor.G - colors[i].G) * (doSub ? -1 : 1);
                    int bDiff = (origColor.B - colors[i].B) * (doSub ? -1 : 1);
                    int combinedDiff = rDiff + gDiff + bDiff;

                    if (combinedDiff > 0 && combinedDiff < nearestRGBDiff)
                    {
                        nearestRGBDiff = combinedDiff;
                        nearestValueIndex = i;
                    }
                }

                // snaps to nearest if set, else jumps to start.
                return (nearestValueIndex != -1)
                    ? colors[nearestValueIndex]
                    : colors[0];
            }

            Color col = Color.Transparent;
            bool isColorSet = false;


            /**
             * random-list-set: randomly picks a value from the list, then sets to it.
             * random-list-add: randomly picks a value from the list, then adds it.
             * random-list-sub: randomly picks a value from the list, then subtracts it.
             * random-list-mul: randomly picks a value from the list, then multiplies it.
             * random-list-div: randomly picks a value from the list, then divides it.
             * random-list-add-hsv, random-list-sub-hsv, random-list-mul-hsv, random-list-div-hsv: same but works in HSV arithmetic
             */
            if (chunks[1].StartsWith("random-list-"))
            {
                List<Color> colors = new List<Color>();
                string[] colorStrings = chunks[0].Split(",");

                // If alpha isn't provided, default to opaque for set (it's absolute), else 0 for arithmetic (since it's relative math).
                byte defaultAlpha = chunks[1].Equals("random-list-set") ? (byte)255 : (byte)0;

                for (int i = 0; i < colorStrings.Length; i++)
                {
                    Color? color = ColorUtils.GetColorFromText(colorStrings[i], true, defaultAlpha)
                        ?? throw new Exception("Was expecting command to contain purely color data with comma delimiters.");

                    colors.Add((Color)color);
                }

                if (colors.Count == 0)
                {
                    throw new Exception("Was expecting command to contain at least one color for random list.");
                }

                // Pick a random color from the list.
                Random rng = new Random();
                col = colors[rng.Next(colors.Count)];
                isColorSet = true;

                // Remove the prefix and continue.
                chunks[1] = chunks[1][12..];
            }

            /**
             * random-set: sets to a value between [min, max).
             * random-add: adds a value between [min, max].
             * random-sub: subtracts a value between [min, max].
             * random-mul: multiplies by a value between [min, max].
             * random-div: divides by a value between [min, max].
             * random-add-hsv, random-sub-hsv, random-mul-hsv, random-div-hsv: same but works in HSV arithmetic
             */
            else if (chunks[1].StartsWith("random-"))
            {
                List<Color> colors = new List<Color>();
                string[] colorStrings = chunks[0].Split(",");

                // If alpha isn't provided, default to opaque for set (it's absolute), else 0 for arithmetic (since it's relative math).
                byte defaultAlpha = chunks[1].Equals("random-set") ? (byte)255 : (byte)0;

                for (int i = 0; i < colorStrings.Length; i++)
                {
                    Color? color = ColorUtils.GetColorFromText(colorStrings[i], true, defaultAlpha)
                        ?? throw new Exception("Was expecting command to contain purely color data with comma delimiters.");

                    colors.Add((Color)color);
                }

                if (colors.Count != 2)
                {
                    throw new Exception("Was expecting command to contain exactly two colors for min,max range.");
                }

                // Pick a random color from the list.
                Random rng = new Random();
                int R = Math.Clamp(rng.Next(colors[0].R, colors[1].R), 0, 255);
                int G = Math.Clamp(rng.Next(colors[0].G, colors[1].G), 0, 255);
                int B = Math.Clamp(rng.Next(colors[0].B, colors[1].B), 0, 255);
                int A = Math.Clamp(rng.Next(colors[0].A, colors[1].A), 0, 255);
                col = Color.FromArgb(A, R, G, B);
                isColorSet = true;

                // Remove the prefix and continue.
                chunks[1] = chunks[1][7..];
            }

            if (chunks[1].Equals("set"))
            {
                return isColorSet ? col : (Color)ColorUtils.GetColorFromText(chunks[0], true);
            }

            // default to alpha 0 for relative operations.
            if (!isColorSet)
            {
                col = (Color)ColorUtils.GetColorFromText(chunks[0], true, 0);
            }

            if (chunks[1].Equals("add"))
            {
                return Color.FromArgb(
                    Math.Clamp(origColor.A + col.A, 0, 255),
                    Math.Clamp(origColor.R + col.R, 0, 255),
                    Math.Clamp(origColor.G + col.G, 0, 255),
                    Math.Clamp(origColor.B + col.B, 0, 255)
                );
            }
            if (chunks[1].Equals("sub"))
            {
                return Color.FromArgb(
                    Math.Clamp(origColor.A - col.A, 0, 255),
                    Math.Clamp(origColor.R - col.R, 0, 255),
                    Math.Clamp(origColor.G - col.G, 0, 255),
                    Math.Clamp(origColor.B - col.B, 0, 255)
                );
            }
            if (chunks[1].Equals("mul"))
            {
                return Color.FromArgb(
                    Math.Clamp(origColor.A * col.A, 0, 255),
                    Math.Clamp(origColor.R * col.R, 0, 255),
                    Math.Clamp(origColor.G * col.G, 0, 255),
                    Math.Clamp(origColor.B * col.B, 0, 255)
                );
            }
            if (chunks[1].Equals("div"))
            {
                return Color.FromArgb(
                    Math.Clamp(origColor.A / (col.A == 0 ? 1 : col.A), 0, 255),
                    Math.Clamp(origColor.R / (col.R == 0 ? 1 : col.R), 0, 255),
                    Math.Clamp(origColor.G / (col.G == 0 ? 1 : col.G), 0, 255),
                    Math.Clamp(origColor.B / (col.B == 0 ? 1 : col.B), 0, 255)
                );
            }
            if (chunks[1].Equals("add-hsv"))
            {
                // Note channels like col.R in the hsv arithmetic are just used as relative numbers, not real colors.
                HsvColor origHsv = HsvColor.FromColor(origColor);
                int newAlpha = Math.Clamp(origColor.A + col.A, 0, 255);

                // Hue is cyclic by nature, it should always wrap
                int newH = origHsv.Hue + col.R;
                while (newH < 0) { newH += 360; }
                while (newH > 360) { newH -= 360; }

                return Color.FromArgb(newAlpha, new HsvColor(
                    newH,
                    Math.Clamp(origHsv.Saturation + col.G, 0, 100),
                    Math.Clamp(origHsv.Value + col.B, 0, 100))
                    .ToColor()
                );
            }
            if (chunks[1].Equals("sub-hsv"))
            {
                // Note channels like col.R in the hsv arithmetic are just used as relative numbers, not real colors.
                HsvColor origHsv = HsvColor.FromColor(origColor);
                int newAlpha = Math.Clamp(origColor.A - col.A, 0, 255);

                // Hue is cyclic by nature, it should always wrap
                int newH = origHsv.Hue - col.R;
                while (newH < 0) { newH += 360; }
                while (newH > 360) { newH -= 360; }

                return Color.FromArgb(newAlpha, new HsvColor(
                    newH,
                    Math.Clamp(origHsv.Saturation - col.G, 0, 100),
                    Math.Clamp(origHsv.Value - col.B, 0, 100))
                    .ToColor()
                );
            }
            if (chunks[1].Equals("mul-hsv"))
            {
                // Using RGBA as relative numbers w/o semantic meaning here. They're converted to ratios from 0 - 1.
                HsvColor origHsv = HsvColor.FromColor(origColor);
                float r = col.R / 255f;
                float g = col.G / 255f;
                float b = col.B / 255f;
                float a = col.A / 255f;
                int newAlpha = Math.Clamp((int)Math.Round(origColor.A * a), 0, 255);

                // There's no cyclic handling of hue because multiplying by floats from [0, 1] can't go out of domain.
                return Color.FromArgb(newAlpha, new HsvColor(
                    Math.Clamp((int)Math.Round(origHsv.Hue * r), 0, 360),
                    Math.Clamp((int)Math.Round(origHsv.Saturation * g), 0, 100),
                    Math.Clamp((int)Math.Round(origHsv.Value * b), 0, 100))
                    .ToColor()
                );
            }
            if (chunks[1].Equals("div-hsv"))
            {
                // Using RGBA as relative numbers w/o semantic meaning here. They're converted to ratios from 0 - 1.
                HsvColor origHsv = HsvColor.FromColor(origColor);
                float r = col.R / 255f;
                float g = col.G / 255f;
                float b = col.B / 255f;
                float a = col.A / 255f;
                int newAlpha = Math.Clamp((int)Math.Round(origColor.A / (a == 0 ? 1 : a)), 0, 255);

                float newH = origHsv.Hue / (r == 0 ? 1 : r);
                while (newH > 360) { newH -= 360; }
                while (newH < 0) { newH += 360; }

                return Color.FromArgb(newAlpha, new HsvColor(
                    (int)Math.Round(newH),
                    Math.Clamp((int)Math.Round(origHsv.Saturation / (g == 0 ? 1 : g)), 0, 100),
                    Math.Clamp((int)Math.Round(origHsv.Value / (b == 0 ? 1 : b)), 0, 100))
                    .ToColor()
                );
            }

            return col;
        }

        /// <summary>
        /// Interprets the data as representing an integer that may be positive or negative.
        /// Assumes the data is already in the proper format. Use <see cref="IsActionValid"/> to ensure.
        /// </summary>
        public int GetDataAsInt(string actiondata, int origValue, int minValue, int maxValue)
        {
            return (int)Math.Round(GetDataAsFloat(actiondata, origValue, minValue, maxValue));
        }

        /// <summary>
        /// Interprets the data as representing an integer that may be positive or negative.
        /// Assumes the data is already in the proper format. Use <see cref="IsActionValid"/> to ensure.
        /// </summary>
        public float GetDataAsFloat(string actiondata, float origValue, float minValue, float maxValue)
        {
            string[] chunks = actiondata.Split('|');

            if (chunks.Length != 2)
            {
                if (!float.TryParse(actiondata, out float actionDataAsNum))
                {
                    throw new Exception("Was expecting command to have two pieces of data.");
                }

                actiondata = $"{actionDataAsNum}|set";
                chunks = actiondata.Split('|');
            }

            if (ValueType != CommandActionDataType.Integer &&
                ValueType != CommandActionDataType.Float)
            {
                throw new Exception("Was expecting command to be a numeric data type.");
            }

            /** Cycles the list of numbers based on the current value. Valid syntaxes:
             * cycle: selects next item, or first item if current value isn't in list.
             * cycle--stop: same as cycle, but does nothing if at end of list.
             * cycle-add: selects next item, or nearest greater number if current value isn't in list.
             * cycle-add-stop: same as cycle-add, but does nothing if at end of list.
             * cycle-sub: selects prev item, or nearest smaller number if current value isn't in list.
             * cycle-sub-stop: same as cycle-sub, but does nothing if at start of list.
             */
            if (chunks[1].StartsWith("cycle"))
            {
                List<float> numbers = new List<float>();
                string[] numberStrings = chunks[0].Split(",");
                for (int i = 0; i < numberStrings.Length; i++)
                {
                    if (!float.TryParse(numberStrings[i], out float result))
                    {
                        throw new Exception("Was expecting command to contain purely numeric data with comma delimiters.");
                    }

                    numbers.Add(Math.Clamp(result, minValue, maxValue));
                }

                if (numbers.Count == 0)
                {
                    throw new Exception("Was expecting command to contain at least one number for cycle.");
                }

                string[] actionDataOptions = chunks[1].Split("-");
                bool doAdd = actionDataOptions.Length > 1 && actionDataOptions[1].Equals("add");
                bool doSub = actionDataOptions.Length > 1 && actionDataOptions[1].Equals("sub");
                bool doStop = actionDataOptions.Length > 2 && actionDataOptions[2].Equals("stop");

                // Jump to the exact number.
                int nearestValueIndex = -1;
                float nearestValueDiff = float.MaxValue;
                for (int i = 0; i < numbers.Count; i++)
                {
                    // If a number in the cycle is matched, shift to the next/prev.
                    if (origValue == numbers[i])
                    {
                        if (doSub)
                        {
                            return (i != 0)
                                ? numbers[i - 1]
                                : doStop ? origValue : numbers[numbers.Count - 1];
                        }

                        return (i < numbers.Count - 1)
                            ? numbers[i + 1]
                            : doStop ? origValue : numbers[0];
                    }

                    // While no number is matched, track the nearest one to snap to.
                    if ((doAdd && numbers[i] - origValue > 0 && numbers[i] - origValue < nearestValueDiff) ||
                        (doSub && origValue - numbers[i] > 0 && origValue - numbers[i] < nearestValueDiff))
                    {
                        nearestValueIndex = i;
                        nearestValueDiff = origValue - numbers[i];
                    }
                }

                // snaps to nearest if set, else jumps to start.
                return (nearestValueIndex != -1)
                    ? numbers[nearestValueIndex]
                    : numbers[0];
            }

            float value = origValue;

            /** Performs randomization. Valid syntaxes:
             * random-set: sets to a value between [min, max).
             * random-add: adds a value between [min, max].
             * random-sub: subtracts a value between [min, max].
             * random-mul: multiplies by a value between [min, max].
             * random-div: divides by a value between [min, max].
             * random-list-set: randomly picks a value from the list, then sets to it.
             * random-list-add: randomly picks a value from the list, then adds it.
             * random-list-sub: randomly picks a value from the list, then subtracts it.
             * random-list-mul: randomly picks a value from the list, then multiplies it.
             * random-list-div: randomly picks a value from the list, then divides it.
             */
            if (chunks[1].StartsWith("random-list"))
            {
                List<float> numbers = new List<float>();
                string[] numberStrings = chunks[0].Split(",");
                if (numberStrings.Length < 1)
                {
                    throw new Exception("Was expecting at least one number to pick from for randomization.");
                }

                for (int i = 0; i < numberStrings.Length; i++)
                {
                    if (!float.TryParse(numberStrings[i], out float result))
                    {
                        throw new Exception("Was expecting random number list to contain purely numeric data with comma delimiters.");
                    }

                    numbers.Add(result);
                }

                Random rng = new Random();
                float pickedNumber = numbers[rng.Next(numbers.Count)];

                if (chunks[1].Equals("random-list-set"))
                {
                    value = Math.Clamp(pickedNumber, minValue, maxValue);
                }
                else if (chunks[1].Equals("random-list-add"))
                {
                    value = Math.Clamp(origValue + pickedNumber, minValue, maxValue);
                }
                else if (chunks[1].Equals("random-list-sub"))
                {
                    value = Math.Clamp(origValue - pickedNumber, minValue, maxValue);
                }
                else if (chunks[1].Equals("random-list-mul"))
                {
                    value = Math.Clamp(origValue * pickedNumber, minValue, maxValue);
                }
                else if (chunks[1].Equals("random-list-div"))
                {
                    value = Math.Clamp(origValue / pickedNumber, minValue, maxValue);
                }

                return value;
            }
            else if (chunks[1].StartsWith("random"))
            {
                List<float> numbers = new List<float>();
                string[] rangeStrings = chunks[0].Split(",");
                if (rangeStrings.Length != 2)
                {
                    throw new Exception("Was expecting random range to have two numbers.");
                }

                for (int i = 0; i < rangeStrings.Length; i++)
                {
                    if (!float.TryParse(rangeStrings[i], out float result))
                    {
                        throw new Exception("Was expecting random range to contain purely numeric data with comma delimiters.");
                    }

                    numbers.Add(result);
                }

                Random rng = new Random();

                if (chunks[1].Equals("random-set"))
                {
                    value = Math.Clamp(numbers[0] + (float)rng.NextDouble() * (numbers[1] - numbers[0]), minValue, maxValue);
                }
                else if (chunks[1].Equals("random-add"))
                {
                    value = Math.Clamp(origValue + (numbers[0] + (float)rng.NextDouble()
                        * Math.Abs(numbers[1] - numbers[0])), minValue, maxValue);
                }
                else if (chunks[1].Equals("random-sub"))
                {
                    value = Math.Clamp(origValue - (numbers[0] + (float)rng.NextDouble()
                        * Math.Abs(numbers[1] - numbers[0])), minValue, maxValue);
                }
                else if (chunks[1].Equals("random-mul"))
                {
                    value = Math.Clamp(origValue * (numbers[0] + (float)rng.NextDouble()
                        * Math.Abs(numbers[1] - numbers[0])), minValue, maxValue);
                }
                else if (chunks[1].Equals("random-div"))
                {
                    float newVal = numbers[0] + (float)rng.NextDouble() * (numbers[1] - numbers[0]);
                    value = Math.Clamp(newVal == 0 ? maxValue : origValue / newVal, minValue, maxValue);
                }

                return value;
            }

            value = float.Parse(chunks[0]);

            if (chunks[1].Equals("set"))
            {
                value = Math.Clamp(value, minValue, maxValue);
            }
            else if (chunks[1].Equals("add"))
            {
                value = Math.Clamp(origValue + value, minValue, maxValue);
            }
            else if (chunks[1].Equals("sub"))
            {
                value = Math.Clamp(origValue - value, minValue, maxValue);
            }
            else if (chunks[1].Equals("mul"))
            {
                value = Math.Clamp(origValue * value, minValue, maxValue);
            }
            else if (chunks[1].Equals("div"))
            {
                value = Math.Clamp(value == 0 ? maxValue : origValue / value, minValue, maxValue);
            }
            else if (chunks[1].Equals("add-wrap"))
            {
                float val = origValue + value;
                while (val < minValue) { val += maxValue + (1 - minValue); }
                while (val > maxValue) { val -= maxValue + (1 - minValue); }

                value = Math.Clamp(val, minValue, maxValue);
            }
            else if (chunks[1].Equals("sub-wrap"))
            {
                float val = origValue - value;
                while (val < minValue) { val += maxValue + (1 - minValue); }
                while (val > maxValue) { val -= maxValue + (1 - minValue); }

                value = Math.Clamp(val, minValue, maxValue);
            }

            return value;
        }

        /// <summary>
        /// Interprets the data as representing true or false from either "t" or "true" or "f" or "false".
        /// Alternatively, it can be "toggle" to flip the value.
        /// Assumes the data is already in the proper format. Use <see cref="IsActionValid"/> to ensure.
        /// </summary>
        public bool GetDataAsBool(string actiondata, bool origValue)
        {
            if (ValueType != CommandActionDataType.Bool)
            {
                throw new Exception("Was expecting command to be of the bool data type.");
            }

            if (actiondata.Equals("toggle", StringComparison.InvariantCultureIgnoreCase))
            {
                return !origValue;
            }

            return actiondata.Equals("t") || actiondata.Equals("true", StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Verifies the given data is valid for this command's data type.
        /// </summary>
        /// <param name="actionData">A string representation of the data.</param>
        public bool IsActionValid(string actionData)
        {
            // All arguments have associated action data by design.
            if (actionData == null)
            {
                return false;
            }

            // Integers and floats must follow the allowed value|type syntaxes and have valid numeric values.
            if (ValueType == CommandActionDataType.Integer ||
                ValueType == CommandActionDataType.Float)
            {
                // expects format: values|type
                string[] chunks = actionData.Split('|');
                if (chunks.Length != 2)
                {
                    return false;
                }

                bool isListType =
                    chunks[1] == "cycle" ||
                    chunks[1] == "cycle-stop" ||
                    chunks[1] == "cycle-add" ||
                    chunks[1] == "cycle-add-stop" ||
                    chunks[1] == "cycle-sub" ||
                    chunks[1] == "cycle-sub-stop" ||
                    chunks[1] == "random-list-set" ||
                    chunks[1] == "random-list-add" ||
                    chunks[1] == "random-list-sub" ||
                    chunks[1] == "random-list-mul" ||
                    chunks[1] == "random-list-div";

                bool isRangeType =
                    chunks[1] == "random-add" ||
                    chunks[1] == "random-set" ||
                    chunks[1] == "random-sub" ||
                    chunks[1] == "random-mul" ||
                    chunks[1] == "random-div";

                // Type must be recognized.
                if (!isListType && !isRangeType &&
                    chunks[1] != "add" &&
                    chunks[1] != "set" &&
                    chunks[1] != "sub" &&
                    chunks[1] != "mul" &&
                    chunks[1] != "div" &&
                    chunks[1] != "add-wrap" &&
                    chunks[1] != "sub-wrap")
                {
                    return false;
                }

                // There must be at least 1 value, and all values must be valid floats (this is intentional for
                // int types, since float math can still be useful).
                if (isListType || isRangeType)
                {
                    string[] numberStrings = chunks[0].Split(",");

                    if (numberStrings.Length == 0)
                    {
                        return false;
                    }
                    if (isRangeType && numberStrings.Length != 2)
                    {
                        return false;
                    }

                    foreach (string numberString in numberStrings)
                    {
                        if (!float.TryParse(numberString, out _))
                        {
                            return false;
                        }
                    }

                    return true;
                }

                return float.TryParse(chunks[0], out float _);
            }

            // Bools must be t or true for true, f or false for false, or toggle to switch when fired.
            if (ValueType == CommandActionDataType.Bool)
            {
                string actionDataLower = actionData.ToLower();
                return actionDataLower.Equals("t") || actionDataLower.Equals("true") ||
                    actionDataLower.Equals("f") || actionDataLower.Equals("false") ||
                    actionDataLower.Equals("toggle");
            }

            // Colors must follow the allowed value|type syntaxes and be 6 or 8 hexadecimal lowercase characters.
            if (ValueType == CommandActionDataType.Color)
            {
                // expects format: values|type
                string[] chunks = actionData.Split('|');
                if (chunks.Length != 2)
                {
                    return false;
                }

                bool isListType =
                    chunks[1] == "cycle" ||
                    chunks[1] == "cycle-stop" ||
                    chunks[1] == "cycle-add" ||
                    chunks[1] == "cycle-add-stop" ||
                    chunks[1] == "cycle-sub" ||
                    chunks[1] == "cycle-sub-stop" ||
                    chunks[1] == "random-list-add" ||
                    chunks[1] == "random-list-set" ||
                    chunks[1] == "random-list-sub" ||
                    chunks[1] == "random-list-mul" ||
                    chunks[1] == "random-list-div" ||
                    chunks[1] == "random-list-add-hsv" ||
                    chunks[1] == "random-list-sub-hsv" ||
                    chunks[1] == "random-list-mul-hsv" ||
                    chunks[1] == "random-list-div-hsv";

                bool isRangeType =
                    chunks[1] == "random-add" ||
                    chunks[1] == "random-set" ||
                    chunks[1] == "random-sub" ||
                    chunks[1] == "random-mul" ||
                    chunks[1] == "random-div" ||
                    chunks[1] == "random-add-hsv" ||
                    chunks[1] == "random-sub-hsv" ||
                    chunks[1] == "random-mul-hsv" ||
                    chunks[1] == "random-div-hsv";

                // Type must be recognized.
                if (!isListType && !isRangeType &&
                    chunks[1] != "add" &&
                    chunks[1] != "set" &&
                    chunks[1] != "sub" &&
                    chunks[1] != "mul" &&
                    chunks[1] != "div" &&
                    chunks[1] != "add-hsv" &&
                    chunks[1] != "sub-hsv" &&
                    chunks[1] != "mul-hsv" &&
                    chunks[1] != "div-hsv")
                {
                    return false;
                }

                // There must be at least 1 value, and all values must be valid colors
                if (isListType || isRangeType)
                {
                    string[] colorStrings = chunks[0].Split(",");

                    if (colorStrings.Length == 0)
                    {
                        return false;
                    }
                    if (isRangeType && colorStrings.Length != 2)
                    {
                        return false;
                    }

                    List<Color> colors = new();
                    foreach (string colorString in colorStrings)
                    {
                        Color? col = ColorUtils.GetColorFromText(colorString, true);
                        if (col == null)
                        {
                            return false;
                        }
                        colors.Add(col.Value);
                    }

                    if (isRangeType &&
                        (colors[0].R > colors[1].R ||
                        colors[0].G > colors[1].G ||
                        colors[0].B > colors[1].B ||
                        colors[0].A > colors[1].A))
                    {
                        return false;
                    }

                    return true;
                }

                return ColorUtils.GetColorFromText(chunks[0], true) != null;
            }

            // Strings must be non-null (already guaranteed by code logic above).
            if (ValueType == CommandActionDataType.String)
            {
                return true;
            }

            throw new Exception("Unhandled shortcut target data type: " + Enum.GetName(ValueType));
        }
    }
}