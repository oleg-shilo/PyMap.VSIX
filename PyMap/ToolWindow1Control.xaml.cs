using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using EnvDTE80;
using PyMap;

namespace PyMap
{
    using System.ComponentModel;
    using System.IO;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Threading;
    using EnvDTE;

    /// <summary>
    /// Interaction logic for ToolWindow1Control.
    /// </summary>
    public partial class ToolWindow1Control : UserControl
    {
        public static bool IsDarkVSTheme => Instance?.IsDarkTheme?.IsChecked == true;
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

            foreach (var itemName in typeof(FontWeights).GetProperties().Select(x => x.Name))
            {
                fontWeights.Items.Add(itemName);
                if (fontWeights.SelectedItem == null && codeMapList.FontWeight.ToString() == itemName)
                    fontWeights.SelectedItem = itemName;
            }

            foreach (var itemName in Fonts.SystemFontFamilies.Select(x => x.ToString()).OrderBy(x => x))
            {
                fonts.Items.Add(itemName);
                if (fonts.SelectedItem == null && codeMapList.FontFamily.ToString() == itemName)
                    fonts.SelectedItem = itemName;
            }

            ReadSettings();

            IsDarkTheme.Checked += IsDarkThemeChanged;
            IsDarkTheme.Unchecked += IsDarkThemeChanged;

            initialized = true;
        }

        void IsDarkThemeChanged(object sender, RoutedEventArgs e)
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
                    string data = ExtractFontSettings();
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
                ApplyFontSettings(data);
            }
            catch { }
        }

        string ExtractFontSettings()
        {
            return $"FontFamily:{codeMapList.FontFamily}\n" +
                   $"FontSize:{codeMapList.FontSize}\n" +
                   $"IsDarkTheme:{IsDarkTheme.IsChecked}\n" +
                   $"FontWeight:{codeMapList.FontWeight}";
        }

        void ApplyFontSettings(string settings)
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
                if (item.Key == "FontFamily" && fonts.Items.Contains(item.Value))
                    fonts.SelectedItem = item.Value;

                if (item.Key == "IsDarkTheme")
                    IsDarkTheme.IsChecked = bool.Parse(item.Value);

                if (item.Key == "FontWeight" && fontWeights.Items.Contains(item.Value))
                    fontWeights.SelectedItem = item.Value;

                if (item.Key == "FontSize" && double.TryParse(item.Value, out double new_size))
                    codeMapList.FontSize = new_size;
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
            ThreadHelper.ThrowIfNotOnUIThread();

            var doc = dte.ActiveDocument;
            if (doc != null)
            {
                if (docFile != doc.FullName)
                {
                    docFile = doc.FullName;
                    RefreshMap(true);
                }
            }
        }

        void ListBox_MouseDoubleClick(object sender, MouseEventArgs e)
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

        void RefreshMap(bool force = false)
        {
            try
            {
                if (parser.CanParse(docFile))
                {
                    if (File.Exists(docFile))
                    {
                        if (force || docFileTimestamp != File.GetLastWriteTimeUtc(docFile))
                        {
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

        private void fontDec_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            codeMapList.FontSize--;

            SaveSettings();
        }

        private void fontInc_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            codeMapList.FontSize++;

            SaveSettings();
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

        private void fonts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (fonts.SelectedItem != null && codeMapList.FontFamily.ToString() != (string)fonts.SelectedItem)
            {
                codeMapList.FontFamily = new FontFamily((string)fonts.SelectedItem);

                SaveSettings();
            }
        }

        private void fontWeights_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                FontWeight weight = typeof(FontWeights).GetProperties()
                                                       .Where(x => x.Name == (string)fontWeights.SelectedItem)
                                                       .Select(x => (FontWeight)x.GetValue(null))
                                                       .FirstOrDefault();

                codeMapList.FontWeight = weight;

                SaveSettings();
            }
            catch { }
        }

        private void ItemSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is Image)
            {
                var img = (sender as Image);
                img.Width = img.ActualHeight;
            }
        }
    }

    class SyntaxParser : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public ObservableCollection<MemberInfo> MemberList { get; set; } = new ObservableCollection<MemberInfo>();
        string errorMessage;

        public string ErrorMessage
        {
            get { return errorMessage; }

            set
            {
                errorMessage = value;
                OnPropertyChanged(nameof(ErrorMessage));
            }
        }

        public void Clear()
        {
            errorMessage = null;
            MemberList.Clear();
        }

        public bool CanParse(string file)
        {
            var fileType = Path.GetExtension(file).ToLower();
            return mappers.ContainsKey(fileType);
        }

        Dictionary<string, Func<string, IEnumerable<MemberInfo>>> mappers = new Dictionary<string, Func<string, IEnumerable<MemberInfo>>>()
        {
            { ".cs", CSharpMapper.Generate },
            { ".py", PythonMapper.Generate },
            { ".pyw", PythonMapper.Generate },
        };

        public void GenerateMap(string file)
        {
            try
            {
                MemberList.Clear();

                var fileType = Path.GetExtension(file).ToLower();
                var generateMap = mappers[fileType];

                foreach (var item in generateMap(file))
                    MemberList.Add(item);

                ErrorMessage = null;
            }
            catch (Exception e)
            {
                ErrorMessage = e.Message;
            }
        }
    }
}