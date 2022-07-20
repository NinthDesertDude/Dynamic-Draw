using DynamicDraw.Interop;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Linq;

namespace DynamicDraw.Logic
{
    /// <summary>
    /// Handles keyboard shortcut assignments.
    /// </summary>
    public static class KeyShortcutManager
    {
        /// <summary>
        /// Fires all registered shortcuts that exactly match the requirements for pressed controls.
        /// </summary>
        public static void FireShortcuts(
            HashSet<KeyboardShortcut> shortcuts,
            HashSet<Keys> keys,
            bool wheelUpFired,
            bool wheelDownFired,
            HashSet<ShortcutContext> contexts)
        {
            bool ctrlHeld = false;
            bool shiftHeld = false;
            bool altHeld = false;
            HashSet<Keys> regularKeys = keys.Where((key) =>
            {
                if (key == Keys.ControlKey) { ctrlHeld = true; }
                else if (key == Keys.ShiftKey) { shiftHeld = true; }
                else if (key == Keys.Alt) { altHeld = true; }
                else
                {
                    return true;
                }

                return false;
            }).ToHashSet();

            foreach (var entry in shortcuts)
            {
                if (!regularKeys.SetEquals(entry.Keys) ||
                    entry.RequireCtrl != ctrlHeld ||
                    entry.RequireShift != shiftHeld ||
                    entry.RequireAlt != altHeld ||
                    (entry.RequireWheel != wheelDownFired && entry.RequireWheel != wheelUpFired) ||
                    (!entry.RequireWheel && entry.RequireWheelUp != wheelUpFired) ||
                    (!entry.RequireWheel && entry.RequireWheelDown != wheelDownFired) ||
                    (entry.ContextsDenied?.Overlaps(contexts) ?? false) ||
                    (!entry.ContextsRequired?.IsSubsetOf(contexts) ?? false))
                {
                    continue;
                }

                entry.OnInvoke?.Invoke();
            }
        }

        /// <summary>
        /// Determines whether the specified key is down.
        /// </summary>
        public static bool IsKeyDown(Keys key)
        {
            return SafeNativeMethods.GetKeyState((int)key) < 0;
        }
    }
}
