using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

namespace BrushFactory
{
    /// <summary>
    /// Implements the loading and saving of the settings.
    /// </summary>
    /// <seealso cref="IBrushFactorySettings" />
    [DataContract(Name = "BrushFactorySettings", Namespace = "")]
    internal sealed class BrushFactorySettings : IBrushFactorySettings
    {
        private readonly string settingsPath;
        private bool changed;
        private bool createUserFilesDir;
        private bool deleteMigratedRegistrySettings;
        private bool loadedSettings;

        private HashSet<string> customBrushDirectories;
        private bool useDefaultBrushes;

        /// <summary>
        /// Initializes a new instance of the <see cref="BrushFactorySettings"/> class.
        /// </summary>
        /// <param name="path">The setting file path.</param>
        public BrushFactorySettings(string path)
        {
            settingsPath = path;
            changed = false;
            createUserFilesDir = false;
            deleteMigratedRegistrySettings = false;
            InitializeDefaultSettings();
        }

        /// <summary>
        /// Gets or sets the custom brush directories.
        /// </summary>
        /// <value>
        /// The custom brush directories.
        /// </value>
        /// <exception cref="ArgumentNullException">value is null.</exception>
        [DataMember(Name = "CustomBrushDirectories")]
        public HashSet<string> CustomBrushDirectories
        {
            get
            {
                return customBrushDirectories;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException("value");
                }

                if (!customBrushDirectories.SetEquals(value))
                {
                    customBrushDirectories = new HashSet<string>(value, StringComparer.OrdinalIgnoreCase);
                    changed = true;
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether to use the default brushes.
        /// </summary>
        /// <value>
        ///   <c>true</c> if the default brushes should be used; otherwise, <c>false</c>.
        /// </value>
        [DataMember(Name = "UseDefaultBrushes")]
        public bool UseDefaultBrushes
        {
            get
            {
                return useDefaultBrushes;
            }
            set
            {
                if (useDefaultBrushes != value)
                {
                    useDefaultBrushes = value;
                    changed = true;
                }
            }
        }

        /// <summary>
        /// Loads the saved settings for this instance.
        /// </summary>
        public void LoadSavedSettings()
        {
            if (!loadedSettings)
            {
                loadedSettings = true;

                if (settingsPath == null)
                {
                    return;
                }

                try
                {
                    using (FileStream stream = new FileStream(settingsPath, FileMode.Open, FileAccess.Read))
                    {
                        DataContractSerializer serializer = new DataContractSerializer(typeof(BrushFactorySettings));
                        BrushFactorySettings savedSettings = (BrushFactorySettings)serializer.ReadObject(stream);

                        customBrushDirectories = new HashSet<string>(savedSettings.CustomBrushDirectories, StringComparer.OrdinalIgnoreCase);
                        useDefaultBrushes = savedSettings.UseDefaultBrushes;
                        changed = false;
                    }
                }
                catch (DirectoryNotFoundException)
                {
                    // The Paint.NET User Files directory does not exist, it will be created when the file is saved.
                    createUserFilesDir = true;
                    MigrateSettingsFromRegistry();
                    changed = true;
                }
                catch (FileNotFoundException)
                {
                    // Migrate the settings from the registry or save the default settings.
                    MigrateSettingsFromRegistry();
                    changed = true;
                }
            }
        }

        /// <summary>
        /// Saves the changed settings.
        /// </summary>
        public void SaveChangedSettings()
        {
            if (changed)
            {
                Save();
                changed = false;

                if (deleteMigratedRegistrySettings)
                {
                    Registry.CurrentUser.DeleteSubKey(@"SOFTWARE\paint.net_brushfactory");
                }
            }
        }

        /// <summary>
        /// Initializes the settings fields to their default values.
        /// </summary>
        private void InitializeDefaultSettings()
        {
            customBrushDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            useDefaultBrushes = true;
        }

        /// <summary>
        /// Migrates the settings from the registry.
        /// </summary>
        private void MigrateSettingsFromRegistry()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\paint.net_brushfactory"))
            {
                if (key != null)
                {
                    string useDefBrushes = (string)key.GetValue("useDefaultBrushes");
                    string customBrushLocs = (string)key.GetValue("customBrushLocations");

                    if (!string.IsNullOrEmpty(useDefBrushes) && bool.TryParse(useDefBrushes, out bool result))
                    {
                        useDefaultBrushes = result;
                    }
                    if (!string.IsNullOrEmpty(customBrushLocs))
                    {
                        string[] values = customBrushLocs.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                        customBrushDirectories = new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
                    }

                    deleteMigratedRegistrySettings = true;
                }
            }
        }

        /// <summary>
        /// Called when the object is deserializing.
        /// </summary>
        /// <param name="context">The streaming context.</param>
        [OnDeserializing]
#pragma warning disable RCS1163 // Unused parameter.
        private void OnDeserializing(StreamingContext context)
#pragma warning restore RCS1163 // Unused parameter.
        {
            // The DataContractSerializer does not call the constructor to initialize the class fields.
            // https://blogs.msdn.microsoft.com/mohamedg/2014/02/05/warning-datacontractserializer-wont-call-your-constructor/
            //
            // This method initializes the fields to their default values when the class is deserializing.

            InitializeDefaultSettings();
        }

        /// <summary>
        /// Saves the settings for this instance.
        /// </summary>
        private void Save()
        {
            if (settingsPath == null)
            {
                return;
            }

            if (createUserFilesDir)
            {
                DirectoryInfo info = new DirectoryInfo(Path.GetDirectoryName(settingsPath));

                if (!info.Exists)
                {
                    info.Create();
                }
            }

            using (FileStream stream = new FileStream(settingsPath, FileMode.Create, FileAccess.Write))
            {
                DataContractSerializer serializer = new DataContractSerializer(typeof(BrushFactorySettings));
                serializer.WriteObject(stream, this);
            }
        }
    }
}
