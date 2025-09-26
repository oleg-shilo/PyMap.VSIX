using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Text;
using Microsoft.VisualStudio.Shell;

namespace CodeMap
{
    public class Options : DialogPage
    {
        [Category("Appearance options")]
        [DisplayName("Show Bookmarks on Margin")]
        [Description("Render the bookmarks on the document margin. Changing this setting will take effect after the code map is regenerated (e.g. document saved).")]
        public bool ShowBookmarkMarginProp
        {
            get { return Settings.Instance.ShowBookmarkMargin; }
            set { Settings.Instance.ShowBookmarkMargin = value; Settings.Instance.Save(); }
        }
    }

    public class Settings
    {
        Settings()
        {
            this.Load();
        }

        public static void Init() // force static constructor to run
        {
        }

        static Settings()
        {
            Instance = new Settings();
        }

        public static Settings Instance;

        public bool ShowBookmarkMargin = true;
        public double FontSize { get; set; } = 12.0;
        public bool Classes { get; set; } = true;
        public bool Interfaces { get; set; } = true;
        public bool StartRegion { get; set; } = true;
        public string StartRegionTemplate { get; set; } = "#start {name}";
        public bool EndRegion { get; set; } = true;
        public string EndRegionTemplate { get; set; } = "#end {name}";
        public bool Structs { get; set; } = true;
        public bool Others { get; set; } = true;
        public bool PublicMethods { get; set; } = true;
        public bool PublicProperties { get; set; } = true;
        public bool PublicFields { get; set; } = true;
        public bool PrivateMethods { get; set; } = true;
        public bool PrivateProperties { get; set; } = true;
        public bool PrivateFields { get; set; } = true;
        public bool SortMembers { get; set; } = true;
        public bool AutoSynch { get; set; } = false;
        public bool ShowMethodSignatures { get; set; } = true;
    }

    static class SettingsStorage
    {
        static string settingsFile
        {
            get
            {
                if (!File.Exists(_settingsFile))
                {
                    string settings_dir = Path.GetDirectoryName(_settingsFile);
                    if (!Directory.Exists(settings_dir))
                        Directory.CreateDirectory(settings_dir);

                    if (File.Exists(_settingsFileOld))
                    {
                        File.Move(_settingsFileOld, _settingsFile);
                        try { Directory.Delete(Path.GetDirectoryName(_settingsFileOld), true); } catch { }
                    }
                    else
                    {
                        File.WriteAllText(_settingsFile, "");
                    }
                }
                return _settingsFile;
            }
        }

        static string _settingsFileOld = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PyMap.2022.VSIX", "settings.dat");
        static string _settingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodeMap.2022.VSIX", "settings.dat");

        // VS does not load settings until the options dialog is opened.
        // interestingly enough `LoadSettingsFromStorage`does not read the same data that is loaded/saved from options dialog
        public static Settings Load(this Settings settings)
        {
            try
            {
                if (File.Exists(settingsFile))
                {
                    var lines = File.ReadAllLines(settingsFile);

                    lines.Select(x => x.Trim())
                         .Where(x => !string.IsNullOrEmpty(x)).ToList()
                         .ForEach(x =>
                         {
                             var parts = x.Split(":".ToCharArray(), 2);
                             var key = parts[0];
                             var value = parts[1];

                             var prop = settingPersistedProps.FirstOrDefault(p => p.Name == key);
                             prop?.SetValue(settings, Convert.ChangeType(value, prop.PropertyType));
                         });
                }
            }
            catch { }
            return settings;
        }

        static List<PropertyInfo> settingPersistedProps = typeof(Settings).GetProperties().Where(p => p.CanRead && p.CanWrite).ToList();

        public static Settings Save(this Settings settings)
        {
            try
            {
                // TODO: move to JSON serialization
                var lines = settingPersistedProps.Select(x => $"{x.Name}:{x.GetValue(settings)}");
                File.WriteAllLines(settingsFile, lines);
            }
            catch { }
            return settings;
        }
    }
}