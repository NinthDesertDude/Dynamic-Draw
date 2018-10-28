using System.Runtime.InteropServices;
using System.Security;

namespace BrushFactory.Interop
{
    [SuppressUnmanagedCodeSecurity]
    internal static class SafeNativeMethods
    {
        [DllImport("user32.dll", ExactSpelling = true)]
        internal static extern short GetKeyState(int keyCode);
    }
}
