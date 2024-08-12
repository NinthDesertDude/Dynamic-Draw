using System.Text.Json.Serialization;

namespace DynamicDraw
{
    /// <summary>
    /// Scripts store an action that is executed when these trigger(s) occur.
    /// </summary>
    public enum ScriptTrigger
    {
        /// <summary>
        /// This never fires.
        /// </summary>
        [JsonPropertyName("Disabled")]
        Disabled = 0,

        /// <summary>
        /// This fires when the user first begins a brush stroke.
        /// </summary>
        [JsonPropertyName("StartBrushStroke")]
        StartBrushStroke = 1,

        /// <summary>
        /// This fires when the user ends a brush stroke (no longer pressing).
        /// </summary>
        [JsonPropertyName("EndBrushStroke")]
        EndBrushStroke = 2,

        /// <summary>
        /// This fires when the brush would normally be stamped to the canvas, before any dynamic brush effects such
        /// as jitter are executed.
        /// </summary>
        [JsonPropertyName("OnBrushStamp")]
        OnBrushStamp = 3,

        /// <summary>
        /// This fires whenever the brush moves.
        /// </summary>
        [JsonPropertyName("OnMouseMoved")]
        OnMouseMoved = 4,
    }
}
