using PaintDotNet;
using PaintDotNet.Effects;
using PaintDotNet.PropertySystem;
using System;

namespace DynamicDraw
{
    /// <summary>
    /// A user effect, e.g. Gaussian Blur or one loaded from a plugin, which can be applied from within this plugin.
    /// </summary>
    public class CustomEffect : IDisposable
    {
        private Effect effect;
        private EffectConfigToken settings;
        private PropertyCollection propertySettings;
        private RenderArgs srcArgs;
        private RenderArgs dstArgs;

        /// <summary>
        /// Get or set the effect to use. Disposing is automatic.
        /// </summary>
        public Effect Effect
        {
            get { return effect; }
            set
            {
                if (effect != value)
                {
                    effect?.Dispose();
                    effect = value;
                }
            }
        }

        /// <summary>
        /// If effect is non-null, this contains the dialog token for the effect. The dialog token contains the
        /// settings for the effect, so that dialog controls can be restored to expected values on multiple runs and
        /// when reopened the next time the plugin is used.
        /// </summary>
        public EffectConfigToken Settings
        {
            get { return settings; }
            set { settings = value; }
        }

        /// <summary>
        /// Iff effect is non-null and if it's property-based, this contains the collection of properties for it. A
        /// property-based effect is ideal (when possible) for tokens because predictable data types for storage allow
        /// for better manipulation for plugin authors, and for generating the UI dialog entirely just by properties.
        /// </summary>
        public PropertyCollection PropertySettings
        {
            get { return propertySettings; }
            set { propertySettings = value; }
        }

        /// <summary>
        /// The source args are used when setting the render info so that plugins can interact with environment
        /// parameters before rendering. The source bitmap should be the most recent copy of whatever is committed.
        /// </summary>
        public RenderArgs SrcArgs
        {
            get { return srcArgs; }
            set
            {
                if (srcArgs != value)
                {
                    srcArgs?.Surface?.Dispose();
                    srcArgs?.Dispose();
                    srcArgs = value;
                }
            }
        }

        /// <summary>
        /// The destination args are used when setting the render info so that plugins can interact with environment
        /// parameters before rendering. The destination bitmap is the surface that effects should manipulate.
        /// Note: Paint.NET expects the destination bitmap to be, by default, identical to the source bitmap.
        /// </summary>
        public RenderArgs DstArgs
        {
            get { return dstArgs; }
            set
            {
                if (dstArgs != value)
                {
                    dstArgs?.Surface?.Dispose();
                    dstArgs?.Dispose();
                    dstArgs = value;
                }
            }
        }

        #region Constructors
        /// <summary>
        /// Represents a user effect such as Gaussian Blur or one loaded from a plugin. All values are null.
        /// </summary>
        public CustomEffect()
        {
            effect = null;
            settings = null;
            propertySettings = null;
            srcArgs = null;
            dstArgs = null;
        }

        /// <summary>
        /// Represents a user effect such as Gaussian Blur or one loaded from a plugin.
        /// </summary>
        public CustomEffect(CustomEffect other, bool preserveRenderInfo = false)
        {
            effect = other?.effect;
            settings = other?.settings;
            propertySettings = other?.propertySettings;
            srcArgs = preserveRenderInfo ? other?.srcArgs : null;
            dstArgs = preserveRenderInfo ? other?.dstArgs : null;
        }

        /// <summary>
        /// Represents a user effect such as Gaussian Blur or one loaded from a plugin.
        /// </summary>
        public CustomEffect(Effect effect, EffectConfigToken settings, PropertyCollection propertySettings, RenderArgs srcArgs, RenderArgs dstArgs)
        {
            this.effect = effect;
            this.settings = settings;
            this.propertySettings = propertySettings;
            this.srcArgs = srcArgs;
            this.dstArgs = dstArgs;
        }
        #endregion

        #region IDisposable
        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                effect?.Dispose();
                srcArgs?.Surface?.Dispose();
                srcArgs?.Dispose();
                dstArgs?.Surface?.Dispose();
                dstArgs?.Dispose();
                disposedValue = true;
            }
        }

        ~CustomEffect()
        {
             // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
             Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
