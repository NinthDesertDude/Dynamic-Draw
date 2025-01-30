using PaintDotNet;
using PaintDotNet.Effects;
using System.Drawing;
using DynamicDraw.Properties;
using PaintDotNet.Imaging;

namespace DynamicDraw
{
    /// <summary>
    /// Called remotely by Paint.Net. In short, a GUI is instantiated by
    /// <see cref="CreateConfigDialog"/> and when the dialog signals OK, Render is called,
    /// passing OnSetRenderInfo to it. The dialog stores its result in an
    /// intermediate class called <see cref="RenderSettings"/>, which is then accessed to
    /// draw the final result in Render.
    /// </summary>
    [PluginSupportInfo(typeof(PluginSupportInfo), DisplayName = "Dynamic Draw")]
    public class EffectPlugin : BitmapEffect
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
            BitmapEffectOptions.Create() with { IsConfigurable = true })
        {
        }
        #endregion

        #region Methods
        protected override IEffectConfigForm OnCreateConfigForm()
        {
            //Copies necessary user variables for dialog access.
            PdnUserSettings.userPrimaryColor = Environment.PrimaryColor.GetSrgb();
            PdnUserSettings.userSecondaryColor = Environment.SecondaryColor.GetSrgb();

            //Static variables are remembered between plugin calls, so clear them.
            RenderSettings.Clear();

            //Creates and returns a new dialog.
            return new WinDynamicDraw();
        }

        protected override void OnInitializeRenderInfo(IBitmapEffectRenderInfo renderInfo)
        {
            // TODO: set any properties on renderInfo, if you want/need
            base.OnInitializeRenderInfo(renderInfo);
        }

        protected override void OnRender(IBitmapEffectOutput output)
        {
            if (!RenderSettings.EffectApplied &&
                RenderSettings.DoApplyEffect && !IsCancelRequested)
            {
                //The effect should only render once.
                RenderSettings.EffectApplied = true;

                using IBitmapLock<ColorBgra32> outputLock = output.LockBgra32();
                RenderSettings.SurfaceToRender.Render(
                    outputLock.AsRegionPtr().Cast<ColorBgra>(), 
                    output.Bounds.Location);
            }
        }
        #endregion
    }
}