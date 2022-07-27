using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Text.Json.Serialization;
using DynamicDraw.Localization;

namespace DynamicDraw
{
    /// <summary>
    /// Represents a keyboard shortcut.
    /// </summary>
    public class KeyboardShortcut
    {
        /// <summary>
        /// Identifies an action/intent associated with the shortcut data type and/or numeric data. For example,
        /// <see cref="IntentTagId.BoolTrue"/> for a boolean type should set the identified target to true when the
        /// shortcut triggers. <see cref="IntentTagId.NumAdd"/> for an integer type should add the associated number
        /// to the shortcut target's current value.
        /// </summary>
        public enum IntentTagId
        {
            /// <summary>
            /// Represents nothing / no value.
            /// </summary>
            None = 0,

            /// <summary>
            /// Set to false
            /// </summary>
            BoolFalse = 1,

            /// <summary>
            /// Set to true
            /// </summary>
            BoolTrue = 2,

            /// <summary>
            /// Toggle value
            /// </summary>
            BoolToggle = 3,

            /// <summary>
            /// Add to value
            /// </summary>
            NumAdd = 4,

            /// <summary>
            /// Add to value. If exceeding max/min, wrap around to the other side.
            /// </summary>
            NumAddWrap = 5,

            /// <summary>
            /// Set to value
            /// </summary>
            NumSet = 6,

            /// <summary>
            /// Subtract from value
            /// </summary>
            NumSub = 7,

            /// <summary>
            /// Subtracts from value. If exceeding min/max, wrap around to the other side.
            /// </summary>
            NumSubWrap = 8
        }

        /// <summary>
        /// Associates intent tags to their display text representation.
        /// </summary>
        public static Dictionary<IntentTagId, string> IntentTags = new Dictionary<IntentTagId, string>()
        {
            { IntentTagId.BoolFalse, Strings.ShortcutIntentBoolFalse },
            { IntentTagId.BoolTrue, Strings.ShortcutIntentBoolTrue },
            { IntentTagId.BoolToggle, Strings.ShortcutIntentBoolToggle },
            { IntentTagId.NumAdd, Strings.ShortcutIntentNumAdd },
            { IntentTagId.NumAddWrap, Strings.ShortcutIntentNumAddWrap },
            { IntentTagId.NumSet, Strings.ShortcutIntentNumSet },
            { IntentTagId.NumSub, Strings.ShortcutIntentNumSub },
            { IntentTagId.NumSubWrap, Strings.ShortcutIntentNumSubWrap }
        };

        /// <summary>
        /// While any of these contexts are valid, the shortcut is automatically disabled.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("ContextsDenied")]
        public HashSet<ShortcutContext> ContextsDenied { get; set; }

        /// <summary>
        /// The shortcut is automatically disabled while any of these contexts are absent.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("ContextsRequired")]
        public HashSet<ShortcutContext> ContextsRequired { get; set; }

        /// <summary>
        /// The key that, when pressed in conjunction with any listed modifier keys, triggers the shortcut.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("Keys")]
        public HashSet<Keys> Keys { get; set; }

        /// <summary>
        /// Whether or not the control modifier key must be held to trigger the shortcut.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("RequireCtrl")]
        public bool RequireCtrl { get; set; }

        /// <summary>
        /// Whether or not the shift modifier key must be held to trigger the shortcut.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("RequireShift")]
        public bool RequireShift { get; set; }

        /// <summary>
        /// Whether or not the alt modifier key must be held to trigger the shortcut.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("RequireAlt")]
        public bool RequireAlt { get; set; }

        /// <summary>
        /// Whether the mouse wheel must be scrolled to trigger the shortcut. This condition is met on wheel up or
        /// wheel down. All keyboard keys in the command should already be held before the mouse wheel is moved in
        /// order to fire the command.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("RequireWheel")]
        public bool RequireWheel { get; set; }

        /// <summary>
        /// Whether the mouse wheel must be scrolled up to trigger the shortcut. All keyboard keys in the command
        /// should already be held before the mouse wheel is moved in order to fire the command.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("RequireWheelUp")]
        public bool RequireWheelUp { get; set; }

        /// <summary>
        /// Whether the mouse wheel must be scrolled down to trigger the shortcut. All keyboard keys in the command
        /// should already be held before the mouse wheel is moved in order to fire the command.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("RequireWheelDown")]
        public bool RequireWheelDown { get; set; }

        /// <summary>
        /// Identifies the setting to change associated with this key shortcut.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("Target")]
        public ShortcutTarget Target { get; set; }

        /// <summary>
        /// A string used as data depending on the type of action. See <see cref="KeyShortcutAction"/>'s enum entries
        /// for a description of how the string should be interpreted based on the named action.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("ActionData")]
        public string ActionData { get; set; }

        /// <summary>
        /// The action to take when the keyboard shortcut is invoked.
        /// </summary>
        [JsonIgnore]
        public Action OnInvoke { get; set; } = null;

        public KeyboardShortcut()
        {
            ContextsDenied = new HashSet<ShortcutContext>();
            ContextsRequired = new HashSet<ShortcutContext>();
            Keys = new HashSet<Keys>();
            RequireCtrl = false;
            RequireShift = false;
            RequireAlt = false;
            RequireWheel = false;
            RequireWheelUp = false;
            RequireWheelDown = false;
            Target = ShortcutTarget.None;
            ActionData = null;
        }

        /// <summary>
        /// Verifies the given data is valid for this setting's data type.
        /// </summary>
        /// <param name="data">A string representation of the data.</param>
        public bool IsActionValid()
        {
            return IsActionValid(Target, ActionData);
        }

        /// <summary>
        /// Inteprets the data as representing a color in the six-digit lowercase hex format.
        /// Assumes the data is already in the proper format. Use <see cref="IsActionValid"/> to ensure.
        /// </summary>
        public Color GetDataAsColor()
        {
            if (Setting.AllSettings[Target].ValueType != ShortcutTargetDataType.Color)
            {
                throw new Exception("Was expecting setting to be of the color data type.");
            }

            return Color.FromArgb(int.Parse(ActionData, System.Globalization.NumberStyles.HexNumber));
        }

        /// <summary>
        /// Interprets the data as representing an integer that may be positive or negative.
        /// Assumes the data is already in the proper format. Use <see cref="IsActionValid"/> to ensure.
        /// </summary>
        public int GetDataAsInt(int origValue, int minValue, int maxValue)
        {
            return (int)Math.Round(GetDataAsFloat(origValue, minValue, maxValue));
        }

        /// <summary>
        /// Interprets the data as representing an integer that may be positive or negative.
        /// Assumes the data is already in the proper format. Use <see cref="IsActionValid"/> to ensure.
        /// </summary>
        public float GetDataAsFloat(float origValue, float minValue, float maxValue)
        {
            string[] chunks = ActionData.Split('|');
            if (chunks.Length != 2)
            {
                throw new Exception("Was expecting numeric setting to have two pieces of data.");
            }

            if (Setting.AllSettings[Target].ValueType != ShortcutTargetDataType.Integer &&
                Setting.AllSettings[Target].ValueType != ShortcutTargetDataType.Float)
            {
                throw new Exception("Was expecting setting to be a numeric data type.");
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
                        throw new Exception("Was expecting setting to contain purely numeric data with comma delimiters.");
                    }

                    numbers.Add(Math.Clamp(result, minValue, maxValue));
                }

                if (numbers.Count == 0)
                {
                    throw new Exception("Was expecting setting to contain at least one number for cycle.");
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
                        nearestValueDiff = numbers[i];
                    }
                }

                // snaps to nearest if set, else jumps to start.
                return (nearestValueIndex != -1)
                    ? numbers[nearestValueIndex]
                    : numbers[0];
            }

            float value = float.Parse(chunks[0]);

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
                while (val < minValue) { val += maxValue; }
                while (val > maxValue) { val -= maxValue; }

                value = Math.Clamp(val, minValue, maxValue);
            }
            else if (chunks[1].Equals("sub-wrap"))
            {
                float val = origValue - value;
                while (val < minValue) { val += maxValue; }
                while (val > maxValue) { val -= maxValue; }

                value = Math.Clamp(val, minValue, maxValue);
            }
            else
            {
                value = float.Parse(ActionData);
            }

            return value;
        }

        /// <summary>
        /// Interprets the data as representing true or false from either "t" or "f". Alternatively, it can be
        /// "toggle" to flip the value.
        /// Assumes the data is already in the proper format. Use <see cref="IsActionValid"/> to ensure.
        /// </summary>
        public bool GetDataAsBool(bool origValue)
        {
            if (Setting.AllSettings[Target].ValueType != ShortcutTargetDataType.Bool)
            {
                throw new Exception("Was expecting setting to be of the bool data type.");
            }

            if (ActionData.Equals("toggle"))
            {
                return !origValue;
            }

            return ActionData.Equals("t");
        }

        /// <summary>
        /// Returns the friendly display name of the shortcut target.
        /// </summary>
        public string GetShortcutName()
        {
            return Setting.AllSettings[Target].Name;
        }

        /// <summary>
        /// Returns user-legible string like "Ctrl + A + Mouse wheel" describing the key sequence used to invoke the
        /// given shortcut.
        /// </summary>
        public string GetShortcutKeysString()
        {
            return GetShortcutKeysString(Keys,
                RequireCtrl, RequireShift, RequireAlt,
                RequireWheel, RequireWheelUp, RequireWheelDown);
        }

        /// <summary>
        /// Verifies the given data is valid for this setting's data type.
        /// </summary>
        /// <param name="actionData">A string representation of the data.</param>
        public static bool IsActionValid(ShortcutTarget target, string actionData)
        {
            // Actions should have no associated action data.
            if (Setting.AllSettings[target].ValueType == ShortcutTargetDataType.Action)
            {
                return string.IsNullOrEmpty(actionData);
            }

            // Everything else must have associated action data.
            if (actionData == null)
            {
                return false;
            }

            // Integers and floats must follow the allowed value|type syntaxes and have valid numeric values.
            if (Setting.AllSettings[target].ValueType == ShortcutTargetDataType.Integer ||
                Setting.AllSettings[target].ValueType == ShortcutTargetDataType.Float)
            {
                // expects format: values|type
                string[] chunks = actionData.Split('|');
                if (chunks.Length != 2)
                {
                    return false;
                }

                bool isCycleType =
                    chunks[1] == "cycle" ||
                    chunks[1] == "cycle-stop" ||
                    chunks[1] == "cycle-add" ||
                    chunks[1] == "cycle-add-stop" ||
                    chunks[1] == "cycle-add-wrap" ||
                    chunks[1] == "cycle-sub" ||
                    chunks[1] == "cycle-sub-stop" ||
                    chunks[1] == "cycle-sub-wrap";

                // Type must be recognized.
                if (!isCycleType &&
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

                // There must be at least 1 cycle value, and all values must be valid floats (this is intentional for
                // int types, since float math can still be useful).
                if (isCycleType)
                {
                    string[] numberStrings = chunks[0].Split(",");

                    if (numberStrings.Length == 0)
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

            // Bools must be t for true, f for false, or toggle to switch when fired.
            if (Setting.AllSettings[target].ValueType == ShortcutTargetDataType.Bool)
            {
                return actionData.Equals("t") || actionData.Equals("f") || actionData.Equals("toggle");
            }

            // Colors must be 6 hexadecimal characters, lowercase alphabet.
            if (Setting.AllSettings[target].ValueType == ShortcutTargetDataType.Color)
            {
                return Regex.Match(actionData, "^([0-9]|[a-f]){6}$").Success;
            }

            // Strings must be non-null (already guaranteed by code logic above).
            if (Setting.AllSettings[target].ValueType == ShortcutTargetDataType.String)
            {
                return true;
            }

            throw new Exception("Unhandled shortcut target data type: " + Enum.GetName(Setting.AllSettings[target].ValueType));
        }

        /// <summary>
        /// Returns user-legible string like "Ctrl + A + Mouse wheel" describing the key sequence used to invoke the
        /// given shortcut.
        /// </summary>
        public static string GetShortcutKeysString(HashSet<Keys> keys,
            bool ctrlHeld, bool shiftHeld, bool altHeld,
            bool wheel, bool wheelUp, bool wheelDown)
        {
            List<string> keysList = new List<string>();
            if (ctrlHeld) { keysList.Add(Strings.ShortcutInputCtrl); }
            if (shiftHeld) { keysList.Add(Strings.ShortcutInputShift); }
            if (altHeld) { keysList.Add(Strings.ShortcutInputAlt); }
            foreach (var key in keys)
            {
                if (key == System.Windows.Forms.Keys.D0) { keysList.Add(string.Format(Strings.ShortcutInputDigitNumber, "0")); }
                else if (key == System.Windows.Forms.Keys.D1) { keysList.Add(string.Format(Strings.ShortcutInputDigitNumber, "1")); }
                else if (key == System.Windows.Forms.Keys.D2) { keysList.Add(string.Format(Strings.ShortcutInputDigitNumber, "2")); }
                else if (key == System.Windows.Forms.Keys.D3) { keysList.Add(string.Format(Strings.ShortcutInputDigitNumber, "3")); }
                else if (key == System.Windows.Forms.Keys.D4) { keysList.Add(string.Format(Strings.ShortcutInputDigitNumber, "4")); }
                else if (key == System.Windows.Forms.Keys.D5) { keysList.Add(string.Format(Strings.ShortcutInputDigitNumber, "5")); }
                else if (key == System.Windows.Forms.Keys.D6) { keysList.Add(string.Format(Strings.ShortcutInputDigitNumber, "6")); }
                else if (key == System.Windows.Forms.Keys.D7) { keysList.Add(string.Format(Strings.ShortcutInputDigitNumber, "7")); }
                else if (key == System.Windows.Forms.Keys.D8) { keysList.Add(string.Format(Strings.ShortcutInputDigitNumber, "8")); }
                else if (key == System.Windows.Forms.Keys.D9) { keysList.Add(string.Format(Strings.ShortcutInputDigitNumber, "9")); }
                else if (key == System.Windows.Forms.Keys.NumPad0) { keysList.Add(string.Format(Strings.ShortcutInputNumpadNumber, "0")); }
                else if (key == System.Windows.Forms.Keys.NumPad1) { keysList.Add(string.Format(Strings.ShortcutInputNumpadNumber, "1")); }
                else if (key == System.Windows.Forms.Keys.NumPad2) { keysList.Add(string.Format(Strings.ShortcutInputNumpadNumber, "2")); }
                else if (key == System.Windows.Forms.Keys.NumPad3) { keysList.Add(string.Format(Strings.ShortcutInputNumpadNumber, "3")); }
                else if (key == System.Windows.Forms.Keys.NumPad4) { keysList.Add(string.Format(Strings.ShortcutInputNumpadNumber, "4")); }
                else if (key == System.Windows.Forms.Keys.NumPad5) { keysList.Add(string.Format(Strings.ShortcutInputNumpadNumber, "5")); }
                else if (key == System.Windows.Forms.Keys.NumPad6) { keysList.Add(string.Format(Strings.ShortcutInputNumpadNumber, "6")); }
                else if (key == System.Windows.Forms.Keys.NumPad7) { keysList.Add(string.Format(Strings.ShortcutInputNumpadNumber, "7")); }
                else if (key == System.Windows.Forms.Keys.NumPad8) { keysList.Add(string.Format(Strings.ShortcutInputNumpadNumber, "8")); }
                else if (key == System.Windows.Forms.Keys.NumPad9) { keysList.Add(string.Format(Strings.ShortcutInputNumpadNumber, "9")); }
                else if (key == System.Windows.Forms.Keys.Add || key == System.Windows.Forms.Keys.Oemplus) { keysList.Add("+"); }
                else if (key == System.Windows.Forms.Keys.Back || key == System.Windows.Forms.Keys.Clear) { keysList.Add(Strings.ShortcutInputBack); }
                else if (key == System.Windows.Forms.Keys.Capital || key == System.Windows.Forms.Keys.CapsLock) { keysList.Add(Strings.ShortcutInputCapsLock); }
                else if (key == System.Windows.Forms.Keys.Delete) { keysList.Add(Strings.ShortcutInputDelete); }
                else if (key == System.Windows.Forms.Keys.Down) { keysList.Add("↓"); }
                else if (key == System.Windows.Forms.Keys.End) { keysList.Add(Strings.ShortcutInputEnd); }
                else if (key == System.Windows.Forms.Keys.Enter || key == System.Windows.Forms.Keys.Return) { keysList.Add(Strings.ShortcutInputEnter); }
                else if (key == System.Windows.Forms.Keys.Escape) { keysList.Add(Strings.ShortcutInputEscape); }
                else if (key == System.Windows.Forms.Keys.Home) { keysList.Add(Strings.ShortcutInputHome); }
                else if (key == System.Windows.Forms.Keys.Insert) { keysList.Add(Strings.ShortcutInputInsert); }
                else if (key == System.Windows.Forms.Keys.Left) { keysList.Add("←"); }
                else if (key == System.Windows.Forms.Keys.Multiply) { keysList.Add("*"); }
                else if (key == System.Windows.Forms.Keys.Next || key == System.Windows.Forms.Keys.PageUp) { keysList.Add(Strings.ShortcutInputPageUp); }
                else if (key == System.Windows.Forms.Keys.NumLock) { keysList.Add(Strings.ShortcutInputNumLock); }
                else if (key == System.Windows.Forms.Keys.OemBackslash) { keysList.Add("\\"); }
                else if (key == System.Windows.Forms.Keys.OemCloseBrackets) { keysList.Add("]"); }
                else if (key == System.Windows.Forms.Keys.Oemcomma) { keysList.Add(","); }
                else if (key == System.Windows.Forms.Keys.OemMinus || key == System.Windows.Forms.Keys.Subtract) { keysList.Add("-"); }
                else if (key == System.Windows.Forms.Keys.OemOpenBrackets) { keysList.Add("["); }
                else if (key == System.Windows.Forms.Keys.OemPeriod) { keysList.Add("."); }
                else if (key == System.Windows.Forms.Keys.OemPipe) { keysList.Add("|"); }
                else if (key == System.Windows.Forms.Keys.OemQuestion) { keysList.Add("?"); }
                else if (key == System.Windows.Forms.Keys.OemQuotes) { keysList.Add("'"); }
                else if (key == System.Windows.Forms.Keys.OemSemicolon) { keysList.Add(";"); }
                else if (key == System.Windows.Forms.Keys.Oemtilde) { keysList.Add("~"); }
                else if (key == System.Windows.Forms.Keys.PageDown || key == System.Windows.Forms.Keys.Prior) { keysList.Add(Strings.ShortcutInputPageDown); }
                else if (key == System.Windows.Forms.Keys.Pause) { keysList.Add(Strings.ShortcutInputPause); }
                else if (key == System.Windows.Forms.Keys.PrintScreen) { keysList.Add(Strings.ShortcutInputPrintScreen); }
                else if (key == System.Windows.Forms.Keys.Right) { keysList.Add("→"); }
                else if (key == System.Windows.Forms.Keys.Scroll) { keysList.Add(Strings.ShortcutInputScrollLock); }
                else if (key == System.Windows.Forms.Keys.Space) { keysList.Add(Strings.ShortcutInputSpace); }
                else if (key == System.Windows.Forms.Keys.Tab) { keysList.Add(Strings.ShortcutInputTab); }
                else if (key == System.Windows.Forms.Keys.Up) { keysList.Add("↑"); }
                else { keysList.Add(key.ToString()); }
            }

            if (wheel) { keysList.Add(Strings.ShortcutInputWheel); }
            if (wheelUp) { keysList.Add(Strings.ShortcutInputWheelUp); }
            if (wheelDown) { keysList.Add(Strings.ShortcutInputWheelDown); }

            return string.Join(" ＋ ", keysList);
        }

        /// <summary>
        /// Returns the string literal equivalent of a character, such as 0 for the numpad or OEM zero key. Returns an
        /// empty string if there is no equivalent.
        /// </summary>
        public static string GetPrintableKey(Keys key)
        {
            bool isShiftHeld = key.HasFlag(System.Windows.Forms.Keys.Shift);
            int keyValue = (int)key;

            // Alphabet
            if (keyValue >= 65 && keyValue <= 90)
            {
                if (isShiftHeld)
                {
                    return ((char)keyValue).ToString();
                }

                return ((char)(keyValue + 32)).ToString();
            }

            // Digits
            if (key == System.Windows.Forms.Keys.D0 || key == System.Windows.Forms.Keys.NumPad0) { return "0"; }
            if (key == System.Windows.Forms.Keys.D1 || key == System.Windows.Forms.Keys.NumPad1) { return "1"; }
            if (key == System.Windows.Forms.Keys.D2 || key == System.Windows.Forms.Keys.NumPad2) { return "2"; }
            if (key == System.Windows.Forms.Keys.D3 || key == System.Windows.Forms.Keys.NumPad3) { return "3"; }
            if (key == System.Windows.Forms.Keys.D4 || key == System.Windows.Forms.Keys.NumPad4) { return "4"; }
            if (key == System.Windows.Forms.Keys.D5 || key == System.Windows.Forms.Keys.NumPad5) { return "5"; }
            if (key == System.Windows.Forms.Keys.D6 || key == System.Windows.Forms.Keys.NumPad6) { return "6"; }
            if (key == System.Windows.Forms.Keys.D7 || key == System.Windows.Forms.Keys.NumPad7) { return "7"; }
            if (key == System.Windows.Forms.Keys.D8 || key == System.Windows.Forms.Keys.NumPad8) { return "8"; }
            if (key == System.Windows.Forms.Keys.D9 || key == System.Windows.Forms.Keys.NumPad9) { return "9"; }

            // Symbols
            if (isShiftHeld)
            {
                if (key == System.Windows.Forms.Keys.Add || key == System.Windows.Forms.Keys.Oemplus) { return "+"; }
                if (key == System.Windows.Forms.Keys.OemBackslash) { return "|"; }
                if (key == System.Windows.Forms.Keys.OemCloseBrackets) { return "}"; }
                if (key == System.Windows.Forms.Keys.Oemcomma) { return "<"; }
                if (key == System.Windows.Forms.Keys.OemMinus || key == System.Windows.Forms.Keys.Subtract) { return "_"; }
                if (key == System.Windows.Forms.Keys.OemOpenBrackets) { return "{"; }
                if (key == System.Windows.Forms.Keys.OemPeriod) { return ">"; }
                if (key == System.Windows.Forms.Keys.OemPipe) { return "|"; }
                if (key == System.Windows.Forms.Keys.OemQuestion) { return "?"; }
                if (key == System.Windows.Forms.Keys.OemQuotes) { return "\""; }
                if (key == System.Windows.Forms.Keys.OemSemicolon) { return ":"; }
                if (key == System.Windows.Forms.Keys.Oemtilde) { return "~"; }
            }
            else
            {
                if (key == System.Windows.Forms.Keys.Add || key == System.Windows.Forms.Keys.Oemplus) { return "="; }
                if (key == System.Windows.Forms.Keys.OemBackslash) { return "\\"; }
                if (key == System.Windows.Forms.Keys.OemCloseBrackets) { return "]"; }
                if (key == System.Windows.Forms.Keys.Oemcomma) { return ","; }
                if (key == System.Windows.Forms.Keys.OemMinus || key == System.Windows.Forms.Keys.Subtract) { return "-"; }
                if (key == System.Windows.Forms.Keys.OemOpenBrackets) { return "["; }
                if (key == System.Windows.Forms.Keys.OemPeriod) { return "."; }
                if (key == System.Windows.Forms.Keys.OemPipe) { return "|"; }
                if (key == System.Windows.Forms.Keys.OemQuestion) { return "/"; }
                if (key == System.Windows.Forms.Keys.OemQuotes) { return "'"; }
                if (key == System.Windows.Forms.Keys.OemSemicolon) { return ";"; }
                if (key == System.Windows.Forms.Keys.Oemtilde) { return "`"; }
            }

            if (key == System.Windows.Forms.Keys.Multiply) { return "*"; }
            if (key == System.Windows.Forms.Keys.Space) { return " "; }

            // Return nothing if non-printable / not recognized.
            return "";
        }

        /// <summary>
        /// This helper function takes a hash set of keys and removes Ctrl, Shift, Alt from the sequence, returning
        /// those statuses through out variables. A clone of the list without those modifiers is returned directly.
        /// </summary>
        /// <param name="keys">A hash set containing keys.</param>
        /// <param name="ctrlHeld">Whether or not ControlKey was previously in the list.</param>
        /// <param name="shiftHeld">Whether or not ShiftKey was previously in the list.</param>
        /// <param name="altHeld">Whether or not Alt was previously in the list.</param>
        /// <returns></returns>
        public static HashSet<Keys> SeparateKeyModifiers(HashSet<Keys> keys, out bool ctrlHeld, out bool shiftHeld, out bool altHeld)
        {
            bool ctrl = false;
            bool shift = false;
            bool alt = false;

            HashSet<Keys> regularKeys = keys.Where((key) =>
            {
                if (key == System.Windows.Forms.Keys.ControlKey) { ctrl = true; }
                else if (key == System.Windows.Forms.Keys.ShiftKey) { shift = true; }
                else if (key == System.Windows.Forms.Keys.Alt || key == System.Windows.Forms.Keys.Menu) { alt = true; }
                else
                {
                    return true;
                }

                return false;
            }).ToHashSet();

            altHeld = alt;
            ctrlHeld = ctrl;
            shiftHeld = shift;

            return regularKeys;
        }
    }
}
