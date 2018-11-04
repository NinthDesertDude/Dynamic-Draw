using System;
using System.IO;

namespace BrushFactory
{
    /// <summary>
    /// A temporary directory that auto-deletes when disposed
    /// </summary>
    /// <seealso cref="IDisposable"/>
    internal sealed class TempDirectory : IDisposable
    {
        private readonly string tempDir;
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="TempDirectory"/> class.
        /// </summary>
        public TempDirectory()
        {
            tempDir = CreateTempDirectory();
            disposed = false;
        }

        /// <summary>
        /// Deletes any previous temporary directories.
        /// </summary>
        public static void CleanupPreviousDirectories()
        {
            try
            {
                string rootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BrushFactory");

                if (Directory.Exists(rootPath))
                {
                    foreach (string path in Directory.EnumerateDirectories(rootPath))
                    {
                        DeleteTempDirectory(path);
                    }
                }
            }
            catch (ArgumentException)
            {
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;

                DeleteTempDirectory(tempDir);
            }
        }

        /// <summary>
        /// Gets a random file name in the directory.
        /// </summary>
        /// <returns>A random file name in the directory.</returns>
        public string GetRandomFileName()
        {
            return Path.Combine(tempDir, Path.GetRandomFileName());
        }

        /// <summary>
        /// Gets the path of a temporary file with the specified name.
        /// </summary>
        /// <returns>The path of the temporary file.</returns>
        public string GetTempPathName(string fileName)
        {
            return Path.Combine(tempDir, fileName);
        }

        /// <summary>
        /// Creates the temporary directory.
        /// </summary>
        /// <returns>The path of the created directory.</returns>
        private static string CreateTempDirectory()
        {
            string rootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BrushFactory");

            DirectoryInfo directoryInfo = new DirectoryInfo(rootPath);
            if (!directoryInfo.Exists)
            {
                directoryInfo.Create();
            }

            while (true)
            {
                string tempDirectoryPath = Path.Combine(rootPath, Path.GetRandomFileName());

                try
                {
                    Directory.CreateDirectory(tempDirectoryPath);

                    return tempDirectoryPath;
                }
                catch (IOException)
                {
                    // Try again if the directory is a file that already exists.
                }
            }
        }

        /// <summary>
        /// Deletes the temporary directory and all of its contents.
        /// </summary>
        /// <param name="path">The path pf the temporary directory.</param>
        private static void DeleteTempDirectory(string path)
        {
            try
            {
                foreach (string item in Directory.EnumerateDirectories(path))
                {
                    Directory.Delete(item, true);
                }

                Directory.Delete(path, true);
            }
            catch (ArgumentException)
            {
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
