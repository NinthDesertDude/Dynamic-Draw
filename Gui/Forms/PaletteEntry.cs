using System.Text.Json.Serialization;

namespace DynamicDraw
{
    /// <summary>
    /// Represents a combobox option for the palette. Immutable.
    /// </summary>
    public struct PaletteEntry
    {
        /// <summary>
        /// When <see cref="Location"/> is null, this defines the special type of palette.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("Type")]
        public PaletteSpecialType SpecialType { get; set; }

        /// <summary>
        /// These determine when the palette should refresh.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("RefreshTriggers")]
        public PaletteRefreshTriggerFlags RefreshTriggers { get; set; }

        /// <summary>
        /// If non-null, the palette will be loaded from this location and whatever value is assigned to
        /// <see cref="SpecialType"/> is ignored.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("Location")]
        public string Location { get; set; }

        /// <summary>
        /// Creates a palette option that defaults to the <see cref="PaletteSpecialType.Current"/> special type.
        /// Location is null.
        /// </summary>
        public PaletteEntry()
        {
            SpecialType = PaletteSpecialType.None;
            Location = null;
            RefreshTriggers = PaletteRefreshTriggerFlags.OnColorChange;
        }

        /// <summary>
        /// Creates a palette option based on a special palette type.
        /// </summary>
        public PaletteEntry(PaletteSpecialType type)
        {
            SpecialType = type;
            Location = null;

            switch (type)
            {
                case PaletteSpecialType.None:
                case PaletteSpecialType.Complement:
                case PaletteSpecialType.Current:
                case PaletteSpecialType.LightToDark:
                case PaletteSpecialType.PrimaryToSecondary:
                case PaletteSpecialType.Recent:
                case PaletteSpecialType.Similar3:
                case PaletteSpecialType.Similar4:
                case PaletteSpecialType.SplitComplement:
                case PaletteSpecialType.Square:
                case PaletteSpecialType.Triadic:
                    RefreshTriggers = PaletteRefreshTriggerFlags.OnColorChange;
                    break;
                case PaletteSpecialType.FromImageAHVS:
                case PaletteSpecialType.FromImageHVSA:
                case PaletteSpecialType.FromImageUsage:
                case PaletteSpecialType.FromImageVHSA:
                    RefreshTriggers = PaletteRefreshTriggerFlags.OnCanvasChange;
                    break;
                case PaletteSpecialType.FromImagePrimaryDistance:
                    RefreshTriggers = PaletteRefreshTriggerFlags.OnColorChange & PaletteRefreshTriggerFlags.OnCanvasChange;
                    break;
                default:
                    RefreshTriggers = PaletteRefreshTriggerFlags.OnColorChange;
                    break;
            }
        }

        /// <summary>
        /// Creates a palette option based on a loaded palette.
        /// </summary>
        public PaletteEntry(string location)
        {
            SpecialType = PaletteSpecialType.None;
            Location = location;
            RefreshTriggers = PaletteRefreshTriggerFlags.OnColorChange;
        }

        public PaletteEntry(PaletteEntry other)
        {
            SpecialType = other.SpecialType;
            Location = other.Location;
            RefreshTriggers = other.RefreshTriggers;
        }
    }
}