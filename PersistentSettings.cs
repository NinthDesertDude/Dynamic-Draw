using System.Collections.Generic;
using System.Drawing;

namespace BrushFactory
{
    /// <summary>
    /// Represents the settings used in the dialog so they can be stored and
    /// loaded when applying the effect consecutively for convenience.
    /// </summary>
    public class PersistentSettings : PaintDotNet.Effects.EffectConfigToken
    {
        #region Fields
        /// <summary>
        /// The last used brush settings the user had.
        /// </summary>
        public BrushSettings CurrentBrushSettings { get; set; }

        /// <summary>
        /// Contains a list of all custom brushes to reload. The dialog will
        /// attempt to read the paths of each brush and add them if possible.
        /// </summary>
        public HashSet<string> CustomBrushLocations
        {
            get;
            set;
        }
        #endregion

        /// <summary>
        /// Creates a new settings token.
        /// </summary>
        public PersistentSettings()
        {
            CurrentBrushSettings = new BrushSettings();
            CustomBrushLocations = new HashSet<string>();
        }

        /// <summary>
        /// Copies all settings to another token.
        /// </summary>
        protected PersistentSettings(PersistentSettings other)
            : base(other)
        {
            CurrentBrushSettings = new BrushSettings(other.CurrentBrushSettings);
            CustomBrushLocations = new HashSet<string>(
                other.CustomBrushLocations,
                other.CustomBrushLocations.Comparer);
        }

        /// <summary>
        /// Copies all settings to another token.
        /// </summary>
        public override object Clone()
        {
            return new PersistentSettings(this);
        }
    }
}