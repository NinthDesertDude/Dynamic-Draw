using DynamicDraw.Interop;
using System.Collections.Generic;
using System.Windows.Forms;

namespace DynamicDraw.Logic
{
    /// <summary>
    /// Handles keyboard shortcut assignments.
    /// </summary>
    public static class KeyShortcutManager
    {
        /// <summary>
        /// Fires any shortcuts that might be registered, which would be fired based on the currently-held keys.
        /// </summary>
        public static void FireShortcuts(HashSet<KeyboardShortcut> shortcuts, Keys key, bool ctrlHeld, bool shiftHeld, bool altHeld)
        {
            foreach (var entry in shortcuts)
            {
                if (entry.Key != key ||
                    entry.RequireCtrl != ctrlHeld ||
                    entry.RequireShift != shiftHeld ||
                    entry.RequireAlt != altHeld)
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
