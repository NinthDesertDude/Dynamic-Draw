using System.Collections.Generic;

namespace BrushFactory.Logic
{
    /// <summary>
    /// A collection of settings read when loading user-defined brush images from files and directories.
    /// </summary>
    internal sealed class BrushImageLoadingSettings
    {
        /// <summary>
        /// Gets the file paths used when adding image files.
        /// </summary>
        public IReadOnlyCollection<string> FilePaths { get; }

        /// <summary>
        /// Gets the directories that are searched for image files.
        /// </summary>
        public IEnumerable<string> SearchDirectories { get; }

        /// <summary>
        /// Gets a value indicating whether the files will be added to the settings.
        /// </summary>
        public bool AddtoSettings { get; }

        /// <summary>
        /// Gets a value indicating whether error messages should be displayed.
        /// </summary>
        public bool DisplayErrors { get; }

        /// <summary>
        /// Gets the height of a single ListView item.
        /// </summary>
        public int ListViewItemHeight { get; }

        public int MaxBrushSize { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkerArgs"/> class.
        /// </summary>
        /// <param name="filePaths">The file paths used when adding image files.</param>
        /// <param name="addtoSettings"><c>true</c> if the files will be added to the settings; otherwise, <c>false</c>.</param>
        /// <param name="displayErrors"><c>true</c> error messages should be displayed; otherwise, <c>false</c>.</param>
        /// <param name="listViewItemHeight">The height of a single ListView item.</param>
        /// <param name="maxBrushSize">The maximum size of a brush image.</param>
        public BrushImageLoadingSettings(IReadOnlyCollection<string> filePaths, bool addtoSettings, bool displayErrors,
            int listViewItemHeight, int maxBrushSize)
        {
            FilePaths = filePaths;
            SearchDirectories = null;
            AddtoSettings = addtoSettings;
            DisplayErrors = displayErrors;
            ListViewItemHeight = listViewItemHeight;
            MaxBrushSize = maxBrushSize;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkerArgs"/> class.
        /// </summary>
        /// <param name="directories">The directories that are searched for image files.</param>
        /// <param name="listViewItemHeight">The height of a single ListView item.</param>
        /// <param name="maxBrushSize">The maximum size of a brush image.</param>
        public BrushImageLoadingSettings(IEnumerable<string> directories, int listViewItemHeight, int maxBrushSize)
        {
            FilePaths = null;
            SearchDirectories = directories;
            AddtoSettings = true;
            DisplayErrors = false;
            ListViewItemHeight = listViewItemHeight;
            MaxBrushSize = maxBrushSize;
        }
    }
}