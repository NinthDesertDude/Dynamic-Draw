List of current issues:
1.  There is a problematic difference in alpha values when drawing and finalizing.

==============================================================================
Features that will be abandoned:
==============================================================================

1. Color shifting.
2. Dynamics based on mouse speed.
3. Dynamics based on mouse direction.
4. Brush strokes in overwrite mode rather than blending.
5. Dynamics based on a full stroke.
6. Save the last viewed directory for adding brushes.

7.  Option to make lines between strokes with the image. Option to set the
density of the brush drawing like this.

8.  Some way to set the rotation origin, which is currently the center of the
image.

9.  Dynamics based on each brush stroke. Each stroke has beginning settings
and some incremental effect. Just do alpha, at least.

10.  While a drawing is in progress, store it on a separate layer. This opens
avenues for improving undo/redo, applying blending and overwriting, and so on.

11.  Hold a button to trigger mouse_move event continuously so the brush
redraws in-place.

12.  Consider setting presets and a button to reset to default.

==============================================================================
Todo:
==============================================================================
1.  Make scrollbars automatically appear in display canvas area for scrolling
it up/down easier when zoomed in.

2.  Can't zoom into the mouse position yet. Would be nice.

3.  Publish extra brushes.

3.  Help, documentation, and tutorials.

4.  Handle localization.

5.  Presets.

6.  Brush strokes are drawn on a separate surface. Have a feature to mix only up
to 255 alpha. Colors are never overwritten once a pixel is fully opaque.

==============================================================================
Code Refactoring:
==============================================================================
1.  look for TODO stuff left over and do it.

2.  Move all the fancy customized code out of the Designer file and into an
appropriate model.

3.  Move the Utils to the non-constructor, non-events methods, or move them
to Utils.

4.  If you're bored, look for alternatives to using the entire
PresentationCore dll just to read input keys.

5.  Find all the vital functionality in the cluttered wasteland of gui code
and extract it. You know, mvc or mvvm or stuff like that.

==============================================================================
Adding New Content:
==============================================================================

1.  To add new embedded brushes, add to Resources.Designer.cs and add to the
list in the form's code-behind constructor method.

2.  To add new controls, add to the form designer. Then, set up MouseEnter for
tooltip and ValueChanged for trackbars. Trackbars with text labels will need
to update the text in ValueChanged. All text and tooltips should be registered
through the Globalization resources file.

3. To add a new persistent setting, add to PersistentSettings class and its
constructors. In form's code-behind, add to InitInitialToken(),
InitTokenFromDialog(), and InitDialogFromToken().

For the next project: Filter Draw. Works similarly without colors; start of
each brush stroke session (beginning when mouse is down and ending when up) a
filter will be applied on a separate surface and the brush will draw the
region to expose it (in black and white) which is used as a transparency map
to apply the filter. There will also be blending modes, I think.