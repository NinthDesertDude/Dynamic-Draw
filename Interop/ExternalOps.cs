using System.Runtime.InteropServices;
using System.Security;

namespace DynamicDraw.Interop
{
    [SuppressUnmanagedCodeSecurity]
    internal static class ExternalOps
    {
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
    }
}
