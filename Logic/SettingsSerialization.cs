using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DynamicDraw
{
    /// <summary>
    /// Implements the loading and saving of the settings.
    /// </summary>
    [DataContract(Name = "DynamicDrawSettings", Namespace = "")] // for importing legacy xml, don't copy this pattern
    public class SettingsSerialization
    {
        private readonly string settingsPath;
        private bool createUserFilesDir;
        private bool deleteMigratedRegistrySettings;
        private bool loadedSettings;

        private HashSet<string> customBrushDirectories;
        private HashSet<string> paletteDirectories;
        private Dictionary<string, BrushSettings> customBrushes;
        private HashSet<KeyboardShortcut> keyboardShortcuts;
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

        [DataMember(Name = "CustomBrushDirectories")] // for importing legacy xml, don't copy this pattern
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

        [DataMember(Name = "CustomBrushes")] // for importing legacy xml, don't copy this pattern
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
        [JsonPropertyName("KeyboardShortcuts")]
        public HashSet<KeyboardShortcut> KeyboardShortcuts
        {
            get
            {
                return keyboardShortcuts;
            }
            set
            {
                if (keyboardShortcuts != value)
                {
                    keyboardShortcuts = new HashSet<KeyboardShortcut>(value);
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether to use the default brushes.
        /// </summary>
        /// <value>
        ///   <c>true</c> if the default brushes should be used; otherwise, <c>false</c>.
        /// </value>
        [DataMember(Name = "UseDefaultBrushes")] // for importing legacy xml, don't copy this pattern
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
            keyboardShortcuts = PersistentSettings.GetShallowShortcutsList();
            preferences = new UserSettings();
            useDefaultBrushes = true;
        }

        /// <summary>
        /// Loads the saved settings for this instance.
        /// </summary>
        public void LoadSavedSettings(bool isLegacyXml = false)
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

                        if (isLegacyXml)
                        {
                            DataContractSerializer serializer = new DataContractSerializer(typeof(SettingsSerialization), rootPath, "");
                            savedSettings = (SettingsSerialization)serializer.ReadObject(stream);
                        }
                        else
                        {
                            savedSettings = (SettingsSerialization)JsonSerializer.Deserialize(stream, typeof(SettingsSerialization));
                        }

                        customBrushDirectories = new HashSet<string>(savedSettings.CustomBrushImageDirectories, StringComparer.OrdinalIgnoreCase);
                        paletteDirectories = new HashSet<string>(savedSettings.PaletteDirectories, StringComparer.OrdinalIgnoreCase);
                        customBrushes = new Dictionary<string, BrushSettings>(savedSettings.CustomBrushes);
                        keyboardShortcuts = savedSettings.KeyboardShortcuts ?? PersistentSettings.GetShallowShortcutsList();
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

            using (FileStream stream = new FileStream(settingsPath, FileMode.Create, FileAccess.Write))
            {
                JsonSerializer.Serialize(stream, this, typeof(SettingsSerialization));
            }
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

        /// <summary>
        /// Supports loading old settings files formatted as XML.
        /// </summary>
        [OnDeserializing]
        private void LegacyXmlOnDeserializing(StreamingContext context)
        {
            // The DataContractSerializer does not call the constructor to initialize the class fields.
            // https://blogs.msdn.microsoft.com/mohamedg/2014/02/05/warning-datacontractserializer-wont-call-your-constructor/
            //
            // This method initializes the fields to their default values when the class is deserializing.

            InitializeDefaultSettings();
        }
    }
}
