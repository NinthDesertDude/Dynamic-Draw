using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DynamicDraw
{
    /// <summary>
    /// Implements the loading and saving of the settings.
    /// </summary>
    public class SettingsSerialization
    {
        private readonly string settingsPath;
        private bool createUserFilesDir;
        private bool deleteMigratedRegistrySettings;
        private bool loadedSettings;

        private HashSet<string> customBrushDirectories;
        private HashSet<string> paletteDirectories;
        private Dictionary<string, BrushSettings> customBrushes;
        private HashSet<Command> customShortcuts;
        private HashSet<int> disabledShortcuts;
        private UserSettings preferences;
        private bool useDefaultBrushes;

        [JsonConstructor]
        public SettingsSerialization()
        {
            settingsPath = "";
            createUserFilesDir = false;
            deleteMigratedRegistrySettings = false;
            loadedSettings = false;
            InitializeDefaultSettings();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsSerialization"/> class.
        /// </summary>
        /// <param name="path">The setting file path.</param>
        public SettingsSerialization(string path)
        {
            settingsPath = path;
            createUserFilesDir = false;
            deleteMigratedRegistrySettings = false;
            loadedSettings = false;
            InitializeDefaultSettings();
        }

        [JsonInclude]
        [JsonPropertyName("Version")]
        /// <summary>
        /// Version is included to make it easier to handle migration going forward.
        /// </summary>
        public string Version
        {
            get
            {
                Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                return $"{version.Major}.{version.Minor}";
            }
        }

        [JsonInclude]
        [JsonPropertyName("BrushImagePaths")]
        public HashSet<string> CustomBrushImageDirectories
        {
            get
            {
                return customBrushDirectories;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (!customBrushDirectories.SetEquals(value))
                {
                    customBrushDirectories = new HashSet<string>(value, StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        [JsonInclude]
        [JsonPropertyName("CustomBrushes")]
        public Dictionary<string, BrushSettings> CustomBrushes
        {
            get
            {
                return customBrushes;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException("value");
                }

                customBrushes = new Dictionary<string, BrushSettings>(value);
            }
        }

        /// <summary>
        /// Gets or sets the list of registered keyboard shortcuts (user-changeable).
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("CustomShortcuts")]
        public HashSet<Command> CustomShortcuts
        {
            get
            {
                return customShortcuts;
            }
            set
            {
                if (customShortcuts != value)
                {
                    customShortcuts = new HashSet<Command>(value);
                }
            }
        }

        /// <summary>
        /// Gets the list of default shortcuts that aren't enabled (user-changeable).
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("DisabledShortcuts")]
        public HashSet<int> DisabledShortcuts
        {
            get
            {
                return disabledShortcuts;
            }
            set
            {
                if (disabledShortcuts != value)
                {
                    disabledShortcuts = new HashSet<int>(value);
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether to use the default brushes.
        /// </summary>
        /// <value>
        ///   <c>true</c> if the default brushes should be used; otherwise, <c>false</c>.
        /// </value>
        [JsonInclude]
        [JsonPropertyName("UseDefaultBrushImages")]
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
                }
            }
        }

        [JsonInclude]
        [JsonPropertyName("PalettePaths")]
        public HashSet<string> PaletteDirectories
        {
            get
            {
                return paletteDirectories;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (!paletteDirectories.SetEquals(value))
                {
                    paletteDirectories = new HashSet<string>(value, StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        /// <summary>
        /// Gets or sets the program preferences of the user.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("Preferences")]
        public UserSettings Preferences
        {
            get
            {
                return preferences;
            }
            set
            {
                preferences = new UserSettings(value);
            }
        }

        /// <summary>
        /// Initializes the settings fields to their default values.
        /// </summary>
        private void InitializeDefaultSettings()
        {
            customBrushDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            paletteDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string path in PersistentSettings.defaultPalettePaths)
            {
                paletteDirectories.Add(path);
            }

            customBrushes = new Dictionary<string, BrushSettings>();
            customShortcuts = PersistentSettings.GetShallowShortcutsList();
            disabledShortcuts = new HashSet<int>();
            preferences = new UserSettings();
            useDefaultBrushes = true;
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
                        string rootPath = Path.GetFileNameWithoutExtension(settingsPath);
                        SettingsSerialization savedSettings;

                        savedSettings = (SettingsSerialization)JsonSerializer.Deserialize(stream, typeof(SettingsSerialization));

                        customBrushDirectories = new HashSet<string>(savedSettings.CustomBrushImageDirectories, StringComparer.OrdinalIgnoreCase);
                        paletteDirectories = new HashSet<string>(savedSettings.PaletteDirectories, StringComparer.OrdinalIgnoreCase);
                        customBrushes = new(savedSettings.customBrushes);

                        // Version <= 4.0: BrushImagePath allowed only one string of its modern equivalent, BrushImagePaths.
                        if (float.TryParse(savedSettings.Version, out float result) && result < 4.0)
                        {
                            foreach (var brush in savedSettings.customBrushes)
                            {
                                if (brush.Value.LegacySerializedInfo.ContainsKey(BrushSettings.Legacy_BrushImagePath))
                                {
                                    brush.Value.BrushImagePaths = new List<string>() { brush.Value.LegacySerializedInfo[BrushSettings.Legacy_BrushImagePath] as string };
                                }
                            }
                        }

                        disabledShortcuts = savedSettings.DisabledShortcuts;
                        customShortcuts = PersistentSettings.InjectDefaultShortcuts(
                            savedSettings.CustomShortcuts ?? new HashSet<Command>(),
                            disabledShortcuts);
                        preferences = new UserSettings(savedSettings.Preferences);
                        useDefaultBrushes = savedSettings.UseDefaultBrushes;
                    }
                }
                catch (DirectoryNotFoundException)
                {
                    // The Paint.NET User Files directory does not exist, it will be created when the file is saved.
                    createUserFilesDir = true;
                    MigrateSettingsFromRegistry();
                }
                catch (Exception ex)
                {
                    // Migrate the settings from the registry or save the default settings.
                    if (ex is FileNotFoundException || ex is JsonException)
                    {
                        MigrateSettingsFromRegistry();
                    }

                    throw;
                }
            }
        }

        /// <summary>
        /// Saves the changed settings.
        /// </summary>
        public void SaveChangedSettings()
        {
            Save();

            if (deleteMigratedRegistrySettings)
            {
                Registry.CurrentUser.DeleteSubKey(@"SOFTWARE\paint.net_brushfactory");
            }
        }

        /// <summary>
        /// Saves the settings for this instance.
        /// </summary>
        /// <param name="forceCreateUserFilesDir">Used to save settings for the first time when necessary.</param>
        public void Save(bool forceCreateUserFilesDir = false)
        {
            if (settingsPath == null)
            {
                return;
            }

            if (createUserFilesDir || forceCreateUserFilesDir)
            {
                DirectoryInfo info = new DirectoryInfo(Path.GetDirectoryName(settingsPath));

                if (!info.Exists)
                {
                    info.Create();
                }
            }

            // After loading, custom shortcuts is combined with enabled default shortcuts. This removes them all when
            // saving, then puts them back in after.
            HashSet<Command> shortcutsCopy = new HashSet<Command>(customShortcuts);
            customShortcuts = PersistentSettings.RemoveDefaultShortcuts(customShortcuts);

            using (FileStream stream = new FileStream(settingsPath, FileMode.Create, FileAccess.Write))
            {
                JsonSerializer.Serialize(stream, this, typeof(SettingsSerialization));
            }

            customShortcuts = shortcutsCopy;
        }

        /// <summary>
        /// Migrates the settings from the registry. This is the oldest legacy settings location.
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
    }
}
