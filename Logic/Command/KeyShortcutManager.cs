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
            HashSet<Keys> regularKeys = KeyboardShortcut.SeparateKeyModifiers(
                keys, out bool ctrlHeld, out bool shiftHeld, out bool altHeld);

            foreach (var entry in shortcuts)
            {
                if (!regularKeys.SetEquals(entry.Keys) ||
                    entry.RequireCtrl != ctrlHeld ||
                    entry.RequireShift != shiftHeld ||
                    entry.RequireAlt != altHeld ||
                    (entry.RequireWheel != wheelDownFired && entry.RequireWheel != wheelUpFired) ||
                    (!entry.RequireWheel && entry.RequireWheelUp != wheelUpFired) ||
                    (!entry.RequireWheel && entry.RequireWheelDown != wheelDownFired) ||
                    entry.ContextsDenied.Overlaps(contexts) ||
                    (!entry.ContextsRequired.IsSubsetOf(contexts)))
                {
                    continue;
                }

                entry.OnInvoke?.Invoke();
            }
        }
    }
}
