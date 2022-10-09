using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using EnvDTE;
using EnvDTE80;

namespace PyMap
{
    static class ExtensionHost
    {
        public static bool IsDarkTheme;
    }

    /// <summary>
    /// Interaction logic for ToolWindow1Control.
    /// </summary>
    public partial class ToolWindow1Control : UserControl
    {
        static ToolWindow1Control Instance;

        CommandEvents commandEvents;
        DTE2 dte;
        SyntaxParser parser = new SyntaxParser();
        FileSystemWatcher watcher = new FileSystemWatcher();

        /// <summary>
        /// Initializes a new instance of the <see cref="ToolWindow1Control"/> class.
        /// </summary>
        public ToolWindow1Control()
        {
            Instance = this;

            ThreadHelper.ThrowIfNotOnUIThread();

            this.InitializeComponent();

            this.DataContext = this.parser;

            this.dte = Global.GetDTE2();
            commandEvents = dte.Events.CommandEvents;
            commandEvents.AfterExecute += CommandEvents_AfterExecute;

            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
            watcher.Changed += Watcher_Changed;
            watcher.Created += Watcher_Changed;
            watcher.Deleted += Watcher_Changed;
            watcher.Renamed += Watcher_Changed;

            ReadSettings();

            codeMapList.SelectionChanged += CodeMapList_SelectionChanged;
            parser.MapInvalidated += () => RefreshMap(force: true);

            initialized = true;
        }

        private void CodeMapList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            NavigateToSelectedMember();
        }

        void SettingsChanged(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            RefreshMap(true);
        }

        bool initialized = false;

        static string settingsFile
        {
            get
            {
                if (!File.Exists(_settingsFile))
                {
                    string settings_dir = Path.GetDirectoryName(_settingsFile);
                    if (!Directory.Exists(settings_dir))
                        Directory.CreateDirectory(settings_dir);
                    File.WriteAllText(_settingsFile, "");
                }
                return _settingsFile;
            }
        }

        static string _settingsFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PyMap.2022.VSIX", "settings.dat");

        void SaveSettings()
        {
            try
            {
                if (initialized)
                {
                    string data = SerializeSettings();
                    File.WriteAllText(settingsFile, data);
                }
            }
            catch { }
        }

        void ReadSettings()
        {
            try
            {
                string data = File.ReadAllText(settingsFile);
                ApplySettings(data);
            }
            catch { }
        }

        string SerializeSettings()
        {
            return $"FontFamily:{codeMapList.FontFamily}\n" +
                   $"FontSize:{codeMapList.FontSize}\n" +
                   $"PublicMethods:{parser.PublicMethods}\n" +
                   $"PublicProperties:{parser.PublicProperties}\n" +
                   $"PublicFields:{parser.PublicFields}\n" +
                   $"PrivateMethods:{parser.PrivateMethods}\n" +
                   $"PrivateProperties:{parser.PrivateProperties}\n" +
                   $"PrivateFields:{parser.PrivateFields}\n" +
                   $"FontWeight:{codeMapList.FontWeight}";
        }

        void ApplySettings(string settings)
        {
            var items = settings.Split('\n')
                                .Select(x => x.Trim())
                                .Where(x => !string.IsNullOrEmpty(x))
                                .Select(x =>
                                {
                                    var parts = x.Split(':');
                                    return new { Key = parts[0], Value = parts[1] };
                                });

            foreach (var item in items)
            {
                if (item.Key == "PublicFields") parser.PublicFields = bool.Parse(item.Value);
                else if (item.Key == "PublicProperties") parser.PublicProperties = bool.Parse(item.Value);
                else if (item.Key == "PublicMethods") parser.PublicMethods = bool.Parse(item.Value);
                else if (item.Key == "PrivateProperties") parser.PrivateProperties = bool.Parse(item.Value);
                else if (item.Key == "PrivateFields") parser.PrivateFields = bool.Parse(item.Value);
                else if (item.Key == "PrivateMethods") parser.PrivateMethods = bool.Parse(item.Value);
                else if (item.Key == "FontSize" && double.TryParse(item.Value, out double new_size)) codeMapList.FontSize = new_size;
            }
        }

        void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                RefreshMap();
            });
        }

        string docFile = null;
        DateTime? docFileTimestamp = null;

        void CommandEvents_AfterExecute(string Guid, int ID, object CustomIn, object CustomOut)
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                Document doc = null;
                try { doc = dte.ActiveDocument; } // can throw (e.g. project properties page)
                catch { }

                if (doc != null)
                {
                    if (docFile != doc.FullName)
                    {
                        docFile = doc.FullName;
                        RefreshMap(true);
                    }
                }
            }
            catch (Exception ex)
            {
                // the system may not be ready yet
                parser.Clear();
                parser.ErrorMessage = ex.Message;
            }
        }

        void NavigateToSelectedMember()
        {
            try
            {
                var info = codeMapList.SelectedItem as MemberInfo;
                if (info != null)
                {
                    if (info.Line != -1)
                    {
                        IWpfTextView textView = Global.GetTextView();
                        textView.MoveCaretToLine(info.Line);
                    }
                }
            }
            catch (Exception ex)
            {
                // the system may not be ready yet
                parser.ErrorMessage = ex.Message;
            }
        }

        void RefreshMap(bool force = false)
        {
            try
            {
                if (docFile != null && parser.CanParse(docFile))
                {
                    if (File.Exists(docFile))
                    {
                        if (force || docFileTimestamp != File.GetLastWriteTimeUtc(docFile))
                        {
                            ExtensionHost.IsDarkTheme = !codeMapList.Background.IsBright();

                            parser.GenerateMap(docFile);
                            docFileTimestamp = File.GetLastWriteTimeUtc(docFile);
                        }

                        StartMonitoring(docFile);
                    }
                }
                else
                {
                    StopMonitoring();
                    parser.Clear();
                }
            }
            catch
            {
            }
        }

        void StopMonitoring()
        {
            watcher.EnableRaisingEvents = false;
        }

        void StartMonitoring(string file)
        {
            watcher.Filter = Path.GetFileName(file);
            watcher.Path = Path.GetDirectoryName(file);
            watcher.EnableRaisingEvents = true;
        }

        private void codeMapList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.GetKeyStates(Key.LeftCtrl) == KeyStates.Down)
            {
                if (e.Delta > 0)
                    codeMapList.FontSize += 0.5;
                else if (e.Delta < 0)
                    codeMapList.FontSize -= 0.5;

                e.Handled = true;

                SaveSettings();
            }
        }

        void ItemSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is Image)
            {
                var img = (sender as Image);
                img.Width = img.ActualHeight;
            }
        }
    }
}