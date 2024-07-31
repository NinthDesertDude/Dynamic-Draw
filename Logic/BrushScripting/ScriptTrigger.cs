using System.Text.Json.Serialization;

namespace DynamicDraw
{
    /// <summary>
    /// Scripted brushes store a series of actions that start with a trigger, exit unless all conditions are met, and
    /// otherwise perform the given actions.
    /// </summary>
    public enum ScriptTrigger
    {
        /// <summary>
        /// This fires when the user begins a brush stroke.
        /// </summary>
        [JsonPropertyName("OnBrushStroke")]
        OnBrushStroke = 0,

        /// <summary>
        /// This fires when the brush would normally be stamped to the canvas, before any dynamic brush effects such
        /// as jitter are executed. It doesn't fire from any additional brush stamps that a scripted brush creates.
        /// </summary>
        [JsonPropertyName("OnStartBrushStamp")]
        OnStartBrushStamp = 1,

        /// <summary>
        /// This fires when the brush would normally be stamped to the canvas, after any dynamic brush effects such
        /// as jitter are executed. It doesn't fire from any additional brush stamps that a scripted brush creates.
        /// </summary>
        [JsonPropertyName("OnEndBrushStamp")]
        OnEndBrushStamp = 2,

        /// <summary>
        /// This fires whenever the brush moves.
        /// </summary>
        [JsonPropertyName("OnMouseMoved")]
        OnMouseMoved = 3,
    }
}
