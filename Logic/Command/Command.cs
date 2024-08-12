using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Text.Json.Serialization;
using DynamicDraw.Localization;

namespace DynamicDraw
{
    /// <summary>
    /// Represents a keyboard shortcut.
    /// </summary>
    public class Command
    {
        /// <summary>
        /// A string used as data depending on the type of action. See <see cref="KeyShortcutAction"/>'s enum entries
        /// for a description of how the string should be interpreted based on the named action.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("ActionData")]
        public string ActionData { get; set; }

        /// <summary>
        /// Only hardcoded shortcuts can set this value, which should be a positive integer for built-in commands,
        /// otherwise -1. Once set, the value should not be updated because users can disable built-in shortcuts, and
        /// they're identified by this value.
        /// </summary>
        [JsonIgnore]
        public int BuiltInShortcutId { get; set; }

        /// <summary>
        /// Only hardcoded shortcuts can set this value, which is false for all custom shortcuts.
        /// </summary>
        [JsonIgnore]
        public bool CommandDialogIgnore { get; set; }

        /// <summary>
        /// While any of these contexts are valid, the shortcut is automatically disabled.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("ContextsDenied")]
        public HashSet<CommandContext> ContextsDenied { get; set; }

        /// <summary>
        /// The shortcut is automatically disabled while any of these contexts are absent.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("ContextsRequired")]
        public HashSet<CommandContext> ContextsRequired { get; set; }

        /// <summary>
        /// The key that, when pressed in conjunction with any listed modifier keys, triggers the shortcut.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("Keys")]
        public HashSet<Keys> Keys { get; set; }

        [JsonInclude]
        [JsonPropertyName("Name")]
        public string Name { get; set; }

        /// <summary>
        /// Whether or not the alt modifier key must be held to trigger the shortcut.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("RequireAlt")]
        public bool RequireAlt { get; set; }

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
        /// Whether the mouse wheel must be scrolled down to trigger the shortcut. All keyboard keys in the command
        /// should already be held before the mouse wheel is moved in order to fire the command.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("RequireWheelDown")]
        public bool RequireWheelDown { get; set; }

        /// <summary>
        /// Whether the mouse wheel must be scrolled up to trigger the shortcut. All keyboard keys in the command
        /// should already be held before the mouse wheel is moved in order to fire the command.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("RequireWheelUp")]
        public bool RequireWheelUp { get; set; }

        /// <summary>
        /// The setting/action associated with this key shortcut.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("ShortcutToExecute")]
        public CommandTarget Target { get; set; }

        /// <summary>
        /// The action to take when the keyboard shortcut is invoked.
        /// </summary>
        [JsonIgnore]
        public Action OnInvoke { get; set; } = null;

        public Command()
        {
            BuiltInShortcutId = -1;
            CommandDialogIgnore = false;
            ContextsDenied = new HashSet<CommandContext>();
            ContextsRequired = new HashSet<CommandContext>();
            Keys = new HashSet<Keys>();
            Name = "";
            RequireCtrl = false;
            RequireShift = false;
            RequireAlt = false;
            RequireWheelUp = false;
            RequireWheelDown = false;
            Target = CommandTarget.None;
            ActionData = null;
        }

        #region Methods
        /// <summary>
        /// Verifies all given data is valid for this command's data type.
        /// </summary>
        /// <param name="data">A string representation of the data.</param>
        public bool IsActionValid()
        {
            int numArgs = CommandTargetInfo.All[Target].Arguments.Count;
            if (numArgs == 0 && !string.IsNullOrEmpty(ActionData))
            {
                return false;
            }

            string[] args = ActionData.Split(';');
            if (args.Length != numArgs)
            {
                return false;
            }

            for (int i = 0; i < numArgs; i++)
            {
                if (!CommandTargetInfo.All[Target].Arguments[i].IsActionValid(args[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Returns user-legible string like "Ctrl + A + Mouse wheel" describing the key sequence used to invoke the
        /// given shortcut.
        /// </summary>
        public string GetShortcutKeysString()
        {
            return GetShortcutKeysString(Keys, RequireCtrl, RequireShift, RequireAlt, RequireWheelUp, RequireWheelDown);
        }

        /// <summary>
        /// For commands with one argument, this returns the result of
        /// <see cref="CommandTargetArgument.GetDataAsInt(string, int, int, int)"/> on the first argument.
        /// </summary>
        public int GetDataAsInt(int origValue, int minValue, int maxValue)
        {
            if (CommandTargetInfo.All[Target].Arguments.Count != 1)
            {
                return -1;
            }

            return CommandTargetInfo.All[Target].Arguments[0].GetDataAsInt(ActionData, origValue, minValue, maxValue);
        }

        /// <summary>
        /// For commands with one argument, this returns the result of
        /// <see cref="CommandTargetArgument.GetDataAsFloat(string, float, float, float)"/> on the first argument.
        /// </summary>
        public float GetDataAsFloat(float origValue, float minValue, float maxValue)
        {
            if (CommandTargetInfo.All[Target].Arguments.Count != 1)
            {
                return -1;
            }

            return CommandTargetInfo.All[Target].Arguments[0].GetDataAsFloat(ActionData, origValue, minValue, maxValue);
        }

        /// <summary>
        /// For commands with one argument, this returns the result of
        /// <see cref="CommandTargetArgument.GetDataAsFloat(string, bool)"/> on the first argument.
        /// </summary>
        public bool GetDataAsBool(bool origValue)
        {
            if (CommandTargetInfo.All[Target].Arguments.Count != 1)
            {
                return false;
            }

            return CommandTargetInfo.All[Target].Arguments[0].GetDataAsBool(ActionData, origValue);
        }

        /// <summary>
        /// For commands with one argument, this returns the result of
        /// <see cref="CommandTargetArgument.GetDataAsColor(string, System.Drawing.Color)"/> on the first argument.
        /// </summary>
        public System.Drawing.Color GetDataAsColor(System.Drawing.Color origValue)
        {
            if (CommandTargetInfo.All[Target].Arguments.Count != 1)
            {
                return System.Drawing.Color.Transparent;
            }

            return CommandTargetInfo.All[Target].Arguments[0].GetDataAsColor(ActionData, origValue);
        }
        #endregion

        #region Static Methods
        /// <summary>
        /// Returns user-legible string like "Ctrl + A + Mouse wheel" describing the key sequence used to invoke the
        /// given shortcut.
        /// </summary>
        public static string GetShortcutKeysString(HashSet<Keys> keys,
            bool ctrlHeld, bool shiftHeld, bool altHeld, bool wheelUp, bool wheelDown)
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

            // Remove any modifiers if present
            keyValue = keyValue & ~(
                (int)System.Windows.Forms.Keys.Control|
                (int)System.Windows.Forms.Keys.Alt|
                (int)System.Windows.Forms.Keys.Shift);
            key = (Keys)keyValue;

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
            if (key == System.Windows.Forms.Keys.D0 || key == System.Windows.Forms.Keys.NumPad0) { return isShiftHeld ? ")" : "0"; }
            if (key == System.Windows.Forms.Keys.D1 || key == System.Windows.Forms.Keys.NumPad1) { return isShiftHeld ? "!" : "1"; }
            if (key == System.Windows.Forms.Keys.D2 || key == System.Windows.Forms.Keys.NumPad2) { return isShiftHeld ? "@" : "2"; }
            if (key == System.Windows.Forms.Keys.D3 || key == System.Windows.Forms.Keys.NumPad3) { return isShiftHeld ? "#" : "3"; }
            if (key == System.Windows.Forms.Keys.D4 || key == System.Windows.Forms.Keys.NumPad4) { return isShiftHeld ? "$" : "4"; }
            if (key == System.Windows.Forms.Keys.D5 || key == System.Windows.Forms.Keys.NumPad5) { return isShiftHeld ? "%" : "5"; }
            if (key == System.Windows.Forms.Keys.D6 || key == System.Windows.Forms.Keys.NumPad6) { return isShiftHeld ? "^" : "6"; }
            if (key == System.Windows.Forms.Keys.D7 || key == System.Windows.Forms.Keys.NumPad7) { return isShiftHeld ? "&" : "7"; }
            if (key == System.Windows.Forms.Keys.D8 || key == System.Windows.Forms.Keys.NumPad8) { return isShiftHeld ? "*" : "8"; }
            if (key == System.Windows.Forms.Keys.D9 || key == System.Windows.Forms.Keys.NumPad9) { return isShiftHeld ? "(" : "9"; }

            // Symbols
            if (isShiftHeld)
            {
                if (key == System.Windows.Forms.Keys.Oemplus) { return "+"; }
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
                if (key == System.Windows.Forms.Keys.Oemplus) { return "="; }
                if (key == System.Windows.Forms.Keys.OemBackslash) { return "\\"; }
                if (key == System.Windows.Forms.Keys.OemCloseBrackets) { return "]"; }
                if (key == System.Windows.Forms.Keys.Oemcomma) { return ","; }
                if (key == System.Windows.Forms.Keys.OemMinus || key == System.Windows.Forms.Keys.Subtract) { return "-"; }
                if (key == System.Windows.Forms.Keys.OemOpenBrackets) { return "["; }
                if (key == System.Windows.Forms.Keys.OemPeriod) { return "."; }
                if (key == System.Windows.Forms.Keys.OemPipe) { return "\\"; }
                if (key == System.Windows.Forms.Keys.OemQuestion) { return "/"; }
                if (key == System.Windows.Forms.Keys.OemQuotes) { return "'"; }
                if (key == System.Windows.Forms.Keys.OemSemicolon) { return ";"; }
                if (key == System.Windows.Forms.Keys.Oemtilde) { return "`"; }
            }

            if (key == System.Windows.Forms.Keys.Add) { return "+"; }
            if (key == System.Windows.Forms.Keys.Divide) { return "/"; }
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
        #endregion
    }
}
