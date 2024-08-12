using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DynamicDraw
{
    /// <summary>
    /// Scripted brushes store a series of actions that start with a trigger, exit unless all conditions are met, and
    /// otherwise perform the given actions.
    /// </summary>
    public class ToolScripts
    {
        /// <summary>
        /// All the scripts that a brush uses. Scripts are evaluated in order, and actions are performed for each one in
        /// order.
        /// </summary>
        [JsonPropertyName("Scripts")]
        public List<Script> Scripts { get; set; }

        [JsonConstructor]
        public ToolScripts()
        {
            Scripts = new List<Script>();
        }

        /// <summary>
        /// Copy constructor.
        /// </summary>
        public ToolScripts(ToolScripts other)
        {
            Scripts = new List<Script>();
            if (other?.Scripts != null)
            {
                for (int i = 0; i < other.Scripts.Count; i++)
                {
                    Scripts.Add(new Script(other.Scripts[i]));
                }
            }
        }
    }
}
