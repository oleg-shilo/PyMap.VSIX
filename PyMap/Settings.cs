using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Text;
using Microsoft.VisualStudio.Shell;

namespace CodeMap
{
    public class Options : DialogPage
    {
        [Category("Appearance options")]
        [DisplayName("Show Bookmarks on Margin")]
        [Description("Render the bookmarks on the document margin. Changing this setting will take effect after the code map is regenerated.")]
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
        internal double FontSize { get; set; } = 12.0;
        public bool Classes { get; set; } = true;
        public bool Interfaces { get; set; } = true;
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

                    var items = lines.Select(x => x.Trim())
                                     .Where(x => !string.IsNullOrEmpty(x))
                                     .Select(x =>
                                     {
                                         var parts = x.Split(':');
                                         return new { Key = parts[0], Value = parts[1] };
                                     });

                    foreach (var item in items)
                    {
                        if (item.Key == nameof(Settings.PublicFields)) settings.PublicFields = bool.Parse(item.Value);
                        else if (item.Key == nameof(Settings.PublicProperties)) settings.PublicProperties = bool.Parse(item.Value);
                        else if (item.Key == nameof(Settings.PublicMethods)) settings.PublicMethods = bool.Parse(item.Value);
                        else if (item.Key == nameof(Settings.Classes)) settings.Classes = bool.Parse(item.Value);
                        else if (item.Key == nameof(Settings.Interfaces)) settings.Interfaces = bool.Parse(item.Value);
                        else if (item.Key == nameof(Settings.Structs)) settings.Structs = bool.Parse(item.Value);
                        else if (item.Key == nameof(Settings.Others)) settings.Others = bool.Parse(item.Value);
                        else if (item.Key == nameof(Settings.PrivateProperties)) settings.PrivateProperties = bool.Parse(item.Value);
                        else if (item.Key == nameof(Settings.PrivateFields)) settings.PrivateFields = bool.Parse(item.Value);
                        else if (item.Key == nameof(Settings.PrivateMethods)) settings.PrivateMethods = bool.Parse(item.Value);
                        else if (item.Key == nameof(Settings.SortMembers)) settings.SortMembers = bool.Parse(item.Value);
                        else if (item.Key == nameof(Settings.ShowMethodSignatures)) settings.ShowMethodSignatures = bool.Parse(item.Value);
                        else if (item.Key == nameof(Settings.ShowBookmarkMargin)) settings.ShowBookmarkMargin = bool.Parse(item.Value);
                        else if (item.Key == nameof(Settings.AutoSynch)) settings.AutoSynch = bool.Parse(item.Value);
                        else if (item.Key == nameof(Settings.FontSize) && double.TryParse(item.Value, out double new_size)) settings.FontSize = new_size;
                    }
                }
            }
            catch { }
            return settings;
        }

        public static Settings Save(this Settings settings)
        {
            try
            {
                // TODO: move to JSON serialization

                var data = $"{nameof(Settings.FontSize)}:{settings.FontSize}\n" +
                           $"{nameof(Settings.Classes)}:{settings.Classes}\n" +
                           $"{nameof(Settings.Interfaces)}:{settings.Interfaces}\n" +
                           $"{nameof(Settings.Structs)}:{settings.Structs}\n" +
                           $"{nameof(Settings.Others)}:{settings.Others}\n" +
                           $"{nameof(Settings.PublicMethods)}:{settings.PublicMethods}\n" +
                           $"{nameof(Settings.PublicProperties)}:{settings.PublicProperties}\n" +
                           $"{nameof(Settings.PublicFields)}:{settings.PublicFields}\n" +
                           $"{nameof(Settings.PrivateMethods)}:{settings.PrivateMethods}\n" +
                           $"{nameof(Settings.PrivateProperties)}:{settings.PrivateProperties}\n" +
                           $"{nameof(Settings.PrivateFields)}:{settings.PrivateFields}\n" +
                           $"{nameof(Settings.SortMembers)}:{settings.SortMembers}\n" +
                           $"{nameof(Settings.AutoSynch)}:{settings.AutoSynch}\n" +
                           $"{nameof(Settings.ShowMethodSignatures)}:{settings.ShowMethodSignatures}\n" +
                           $"{nameof(Settings.ShowBookmarkMargin)}:{settings.ShowBookmarkMargin}";

                File.WriteAllText(settingsFile, data);
            }
            catch { }
            return settings;
        }
    }
}