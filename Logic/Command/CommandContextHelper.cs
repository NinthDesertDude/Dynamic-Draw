using System;
using System.Collections.Generic;

namespace DynamicDraw
{
    /// <summary>
    /// Provides useful functionality related to the <see cref="CommandContext"/> class.
    /// </summary>
    static class CommandContextHelper
    {
        /// <summary>
        /// Returns a hashset of command contexts for all the contexts associated directly to the given tool.
        /// </summary>
        public static HashSet<CommandContext> GetAllContextsForTool(Tool tool)
        {
            var contexts = new HashSet<CommandContext>();

            switch (tool)
            {
                case Tool.Brush:
                    contexts.Add(CommandContext.ToolBrushActive);
                    break;
                case Tool.Eraser:
                    contexts.Add(CommandContext.ToolEraserActive);
                    break;
                case Tool.ColorPicker:
                    contexts.Add(CommandContext.ToolColorPickerActive);
                    break;
                case Tool.SetSymmetryOrigin:
                    contexts.Add(CommandContext.ToolSetOriginActive);
                    break;
                case Tool.CloneStamp:
                    contexts.Add(CommandContext.ToolCloneStampActive);
                    contexts.Add(CommandContext.CloneStampOriginUnsetStage);
                    contexts.Add(CommandContext.CloneStampOriginSetStage);
                    break;
                case Tool.Line:
                    contexts.Add(CommandContext.ToolLineToolActive);
                    contexts.Add(CommandContext.LineToolUnstartedStage);
                    contexts.Add(CommandContext.LineToolConfirmStage);
                    break;
            }

            return contexts;
        }

        /// <summary>
        /// Modifies the given context hashset to remove any context associated to another tool. Tools are responsible
        /// for restoring whichever contexts make sense when they are switched to.
        /// </summary>
        /// <param name="tool">The tool which should not have contexts removed.</param>
        /// <param name="set">The hashset to modify.</param>
        public static void RemoveContextsFromOtherTools(Tool tool, HashSet<CommandContext> set)
        {
            var tools = Enum.GetValues(typeof(Tool));

            foreach (Tool currentTool in tools)
            {
                if (tool != currentTool)
                {
                    set.ExceptWith(GetAllContextsForTool(currentTool));
                }
            }
        }
    }
}