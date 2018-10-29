using PaintDotNet;
using PaintDotNet.Effects;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using BrushFactory.Properties;

namespace BrushFactory
{
    /// <summary>
    /// Contains assembly information, accessible through variables.
    /// </summary>
    public class PluginSupportInfo : IPluginSupportInfo
    {
        #region Properties
        /// <summary>
        /// Gets the author.
        /// </summary>
        public string Author
        {
            get
            {
                return ((AssemblyCompanyAttribute)base.GetType().Assembly
                    .GetCustomAttributes(typeof(AssemblyCompanyAttribute), false)[0]).Company;
            }
        }

        /// <summary>
        /// Gets the copyright information.
        /// </summary>
        public string Copyright
        {
            get
            {
                return ((AssemblyCopyrightAttribute)base.GetType().Assembly
                    .GetCustomAttributes(typeof(AssemblyCopyrightAttribute), false)[0]).Copyright;
            }
        }

        /// <summary>
        /// Gets the name of the product.
        /// </summary>
        public string DisplayName
        {
            get
            {
                return ((AssemblyProductAttribute)base.GetType().Assembly
                    .GetCustomAttributes(typeof(AssemblyProductAttribute), false)[0]).Product;
            }
        }

        /// <summary>
        /// Gets the version number.
        /// </summary>
        public Version Version
        {
            get
            {
                return base.GetType().Assembly.GetName().Version;
            }
        }

        /// <summary>
        /// Gets the URL where the plugin is released to the public.
        /// </summary>
        public Uri WebsiteUri
        {
            get
            {
                return new Uri("http://forums.getpaint.net/index.php?/forum/7-plugins-publishing-only/");
            }
        }
        #endregion
    }

    /// <summary>
    /// Controls the effect. In short, a GUI is instantiated by
    /// CreateConfigDialog and when the dialog signals OK, Render is called,
    /// passing OnSetRenderInfo to it. The dialog stores its result in an
    /// intermediate class called RenderSettings, which is then accessed to
    /// draw the final result in Render.
    /// </summary>
    [PluginSupportInfo(typeof(PluginSupportInfo), DisplayName = "Brush Factory")]
    public class EffectPlugin : Effect
    {
        #region Properties
        /// <summary>
        /// The icon of the plugin to be displayed next to its menu entry.
        /// </summary>
        public static Bitmap StaticImage
        {
            get
            {
                return Resources.IconPng;
            }
        }

        /// <summary>
        /// The name of the plugin as it appears in Paint.NET.
        /// </summary>
        public static string StaticName
        {
            get
            {
                return Localization.Strings.Title;
            }
        }

        /// <summary>
        /// The name of the menu category the plugin appears under.
        /// </summary>
        public static string StaticSubMenuName
        {
            get
            {
                return "Tools";
            }
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Constructor.
        /// </summary>
        public EffectPlugin()
            : base(
            StaticName,
            StaticImage,
            StaticSubMenuName,
            EffectFlags.Configurable)
        {
        }
        #endregion

        #region Methods
        /// <summary>
        /// Tells Paint.NET which form to instantiate as the plugin's GUI.
        /// Called remotely by Paint.NET.
        /// </summary>
        public override EffectConfigDialog CreateConfigDialog()
        {
            //Copies necessary user variables for dialog access.
            UserSettings.userPrimaryColor = EnvironmentParameters.PrimaryColor;

            //Static variables are remembered between plugin calls, so clear them.
            RenderSettings.Clear();

            //Creates and returns a new dialog.
            return new WinBrushFactory();
        }

        /// <summary>
        /// Gets the render information.
        /// </summary>
        /// <param name="parameters">
        /// Saved settings used to restore the GUI to the same settings it was
        /// saved with last time the effect was applied.
        /// </param>
        /// <param name="dstArgs">The destination canvas.</param>
        /// <param name="srcArgs">The source canvas.</param>
        protected override void OnSetRenderInfo(
            EffectConfigToken parameters,
            RenderArgs dstArgs,
            RenderArgs srcArgs)
        {
            //Copies the render information to the base Effect class.
            base.OnSetRenderInfo(parameters, dstArgs, srcArgs);
        }

        /// <summary>
        /// Renders the effect over rectangular regions automatically
        /// determined and handled by Paint.NET for multithreading support.
        /// </summary>
        /// <param name="parameters">
        /// Saved settings used to restore the GUI to the same settings it was
        /// saved with last time the effect was applied.
        /// </param>
        /// <param name="dstArgs">The destination canvas.</param>
        /// <param name="srcArgs">The source canvas.</param>
        /// <param name="rois">
        /// A list of rectangular regions to split this effect into so it can
        /// be optimized by worker threads. Determined and managed by
        /// Paint.NET.
        /// </param>
        /// <param name="startIndex">
        /// The rectangle to begin rendering with. Used in Paint.NET's effect
        /// multithreading process.
        /// </param>
        /// <param name="length">
        /// The number of rectangles to render at once. Used in Paint.NET's
        /// effect multithreading process.
        /// </param>
        public override void Render(
            EffectConfigToken parameters,
            RenderArgs dstArgs,
            RenderArgs srcArgs,
            Rectangle[] rois,
            int startIndex,
            int length)
        {
            //Renders the effect if the dialog is closed and accepted.
            if (!RenderSettings.EffectApplied &&
                RenderSettings.DoApplyEffect && !IsCancelRequested)
            {
                //The effect should only render once.
                RenderSettings.EffectApplied = true;

                using (Graphics g = new RenderArgs(dstArgs.Surface).Graphics)
                {
                    //Copies the drawn image, clipping it to the selection.
                    g.CompositingMode = CompositingMode.SourceCopy;
                    Region region = new Region(EnvironmentParameters
                        .GetSelection(srcArgs.Bounds).GetRegionData());
                    g.SetClip(region, CombineMode.Replace);

                    g.DrawImage(RenderSettings.BmpToRender, 0, 0,
                        RenderSettings.BmpToRender.Width,
                        RenderSettings.BmpToRender.Height);

                    //TODO: This copies perfectly, but can't handle clipping to a region.
                    //Utils.CopyBitmapPure(RenderSettings.BmpToRender, dstArgs.Bitmap);
                }
            }
        }
        #endregion
    }
}