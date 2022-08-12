using System.Text.Json.Serialization;

namespace DynamicDraw
{
    /// <summary>
    /// Represents a handling method and value for a constraint.
    /// </summary>
    public struct BrushSettingConstraint
    {
        [JsonInclude]
        [JsonPropertyName("handleMethod")]
        public ConstraintValueHandlingMethod handleMethod;

        [JsonInclude]
        [JsonPropertyName("value")]
        public int value;

        public BrushSettingConstraint(ConstraintValueHandlingMethod handleMethod, int value)
        {
            this.handleMethod = handleMethod;
            this.value = value;
        }
    }
}