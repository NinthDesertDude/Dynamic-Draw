#if FASTDEBUG
using System;
using System.Drawing;
using System.Windows.Forms;

namespace DynamicDraw
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            //Copies necessary user variables for dialog access.
            UserSettings.userPrimaryColor = Color.Black;

            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new WinDynamicDraw());
        }
    }
}
#endif
