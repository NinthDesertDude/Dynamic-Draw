using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace DynamicDraw.Logic
{
    /// <summary>
    /// Represents a keyboard shortcut.
    /// </summary>
    [DataContract(Name = "Shortcut", Namespace = "")]
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
            { IntentTagId.BoolFalse, Localization.Strings.ShortcutIntentBoolFalse },
            { IntentTagId.BoolTrue, Localization.Strings.ShortcutIntentBoolTrue },
            { IntentTagId.BoolToggle, Localization.Strings.ShortcutIntentBoolToggle },
            { IntentTagId.NumAdd, Localization.Strings.ShortcutIntentNumAdd },
            { IntentTagId.NumAddWrap, Localization.Strings.ShortcutIntentNumAddWrap },
            { IntentTagId.NumSet, Localization.Strings.ShortcutIntentNumSet },
            { IntentTagId.NumSub, Localization.Strings.ShortcutIntentNumSub },
            { IntentTagId.NumSubWrap, Localization.Strings.ShortcutIntentNumSubWrap }
        };

        /// <summary>
        /// While any of these contexts are valid, the shortcut is automatically disabled.
        /// </summary>
        [DataMember(Name = "ContextsDenied")]
        public HashSet<ShortcutContext> ContextsDenied { get; set; }

        /// <summary>
        /// The shortcut is automatically disabled while any of these contexts are absent.
        /// </summary>
        [DataMember(Name = "ContextsRequired")]
        public HashSet<ShortcutContext> ContextsRequired { get; set; }

        /// <summary>
        /// The key that, when pressed in conjunction with any listed modifier keys, triggers the shortcut.
        /// </summary>
        [DataMember(Name = "Key")]
        public Keys Key { get; set; }

        /// <summary>
        /// Whether or not the control modifier key must be held to trigger the shortcut.
        /// </summary>
        [DataMember(Name = "ReqCtrl")]
        public bool RequireCtrl { get; set; }

        /// <summary>
        /// Whether or not the shift modifier key must be held to trigger the shortcut.
        /// </summary>
        [DataMember(Name = "ReqShift")]
        public bool RequireShift { get; set; }

        /// <summary>
        /// Whether or not the alt modifier key must be held to trigger the shortcut.
        /// </summary>
        [DataMember(Name = "ReqAlt")]
        public bool RequireAlt { get; set; }

        /// <summary>
        /// Identifies the setting to change associated with this key shortcut.
        /// </summary>
        [DataMember(Name = "Setting")]
        public ShortcutTarget Target { get; set; }

        /// <summary>
        /// A string used as data depending on the type of action. See <see cref="KeyShortcutAction"/>'s enum entries
        /// for a description of how the string should be interpreted based on the named action.
        /// </summary>
        [DataMember(Name = "ActionData")]
        public string ActionData { get; set; }

        /// <summary>
        /// The action to take when the keyboard shortcut is invoked.
        /// </summary>
        public Action OnInvoke { get; set; } = null;

        public KeyboardShortcut()
        {
            Key = Keys.None;
            RequireAlt = false;
            RequireCtrl = false;
            RequireShift = false;
            Target = (ShortcutTarget)(-1);
            ActionData = null;
        }

        /// <summary>
        /// Verifies the given data is valid for this setting's data type.
        /// </summary>
        /// <param name="data">A string representation of the data.</param>
        public bool IsActionValid()
        {
            if (Setting.AllSettings[Target].ValueType == ShortcutTargetDataType.Integer)
            {
                if (int.TryParse(ActionData, out int value))
                {
                    return Setting.AllSettings[Target].ValidateNumberValue(value);
                }

                return false;
            }

            if (Setting.AllSettings[Target].ValueType == ShortcutTargetDataType.Float)
            {
                if (float.TryParse(ActionData, out float value))
                {
                    return Setting.AllSettings[Target].ValidateNumberValue(value);
                }

                return false;
            }

            if (Setting.AllSettings[Target].ValueType == ShortcutTargetDataType.Bool)
            {
                return ActionData.Equals("t") || ActionData.Equals("f");
            }
            
            if (Setting.AllSettings[Target].ValueType == ShortcutTargetDataType.Color)
            {
                return Regex.Match(ActionData, "^([0-9]|[a-f]){6}$").Success;
            }

            return !string.IsNullOrEmpty(ActionData);
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
    }
}
