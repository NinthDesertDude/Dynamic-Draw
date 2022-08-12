using System.Text.Json.Serialization;

namespace DynamicDraw
{
    /// <summary>
    /// Represents a combobox option for the palette. Immutable.
    /// </summary>
    public struct PaletteComboboxOptions
    {
        /// <summary>
        /// When <see cref="Location"/> is null, this defines the special type of palette.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("Type")]
        public PaletteSpecialType SpecialType { get; private set; }

        /// <summary>
        /// If non-null, the palette will be loaded from this location and whatever value is assigned to
        /// <see cref="SpecialType"/> is ignored.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("Location")]
        public string Location { get; private set; }

        /// <summary>
        /// Creates a palette option that defaults to the <see cref="PaletteSpecialType.Current"/> special type.
        /// Location is null.
        /// </summary>
        public PaletteComboboxOptions()
        {
            SpecialType = PaletteSpecialType.None;
            Location = null;
        }

        /// <summary>
        /// Creates a palette option based on a special palette type.
        /// </summary>
        public PaletteComboboxOptions(PaletteSpecialType type)
        {
            SpecialType = type;
            Location = null;
        }

        /// <summary>
        /// Creates a palette option based on a loaded palette.
        /// </summary>
        public PaletteComboboxOptions(string location)
        {
            SpecialType = PaletteSpecialType.None;
            Location = location;
        }
    }
}