using PaintDotNet;
using System;
using System.Reflection;

namespace DynamicDraw
{
    /// <summary>
    /// 
    /// 
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
                return new Uri("https://forums.getpaint.net/topic/110673-dynamic-draw-v3-christmas-2021/");
            }
        }
        #endregion
    }
}