using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows.Forms;

namespace DynamicDraw.Interop
{
    [SuppressUnmanagedCodeSecurity]
    internal static class ExternalOps
    {
        #region user32.dll -> mouse click simulation
        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        private static extern void mouse_event(long dwFlags, long dx, long dy, long cButtons, long dwExtraInfo);

        /// <summary>
        /// Mouse events that can be simulated.
        /// </summary>
        public enum MouseEvents
        {
            LeftDown,
            LeftUp
        }

        /// <summary>
        /// Fires a mouse click event that will trigger everything underneath.
        /// Adapted under CC-By-SA 3.0 from https://stackoverflow.com/a/32648987.
        /// Link to license: https://creativecommons.org/licenses/by-sa/3.0/.
        /// </summary>
        /// <param name="e">The mouse event to fire (from a limited pool).</param>
        public static void SimulateClick(MouseEvents e)
        {
            switch (e)
            {
                case MouseEvents.LeftDown:
                    {
                        mouse_event(0x02, 0, 0, 0, 0);
                        break;
                    }
                case MouseEvents.LeftUp:
                    {
                        mouse_event(0x04, 0, 0, 0, 0);
                        break;
                    }
            }
        }
        #endregion

        #region uxtheme.dll -> painting scrollbars in dark mode
        /// <summary>
        /// Invokes Wi.ndows to set the window theme corresponding to the given app name on the given control
        /// </summary>
        /// <param name="hWnd">The control handle.</param>
        /// <param name="pszSubAppName">The containing app's name.</param>
        [DllImport("uxtheme.dll", SetLastError = true, ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);

        /// <summary>
        /// Uses an as-yet unpublished API that exists in Windows to set the window theme to dark mode for the
        /// scrollbar control. There is no other way except to recreate the scrollbar manually.
        /// Note: This is a hack that might break in the future.
        /// </summary>
        internal static void UpdateDarkMode(Control control)
        {
            if (control == null || control.Disposing || control.IsDisposed)
            {
                return;
            }

            // Hack doesn't exist in versions below Windows 10 1809
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
            {
                return;
            }

            // Identify this app as if it's File Explorer in Dark Mode, which is the only way right now to work.
            bool enable = (SemanticTheme.CurrentTheme != ThemeName.Light);
            string themeName = enable ? "DarkMode_Explorer" : null;
            SetWindowTheme(control.Handle, themeName, null);
        }
        #endregion
    }
}
