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

            void hookCheckbox(CheckBox control)
            {
                control.Checked += SettingsChanged;
                control.Unchecked += SettingsChanged;
            }

            hookCheckbox(IsDarkTheme);

            codeMapList.SelectionChanged += CodeMapList_SelectionChanged;

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
                   $"IsDarkTheme:{IsDarkTheme.IsChecked}\n" +
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
                else if (item.Key == "FontFamily" && fonts.Items.Contains(item.Value)) fonts.SelectedItem = item.Value;
                else if (item.Key == "IsDarkTheme") IsDarkTheme.IsChecked = bool.Parse(item.Value);
                else if (item.Key == "FontWeight" && fontWeights.Items.Contains(item.Value)) fontWeights.SelectedItem = item.Value;
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
            catch (Exception ex)
            {
                // the system may not be ready yet
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

        void fonts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (fonts.SelectedItem != null && codeMapList.FontFamily.ToString() != (string)fonts.SelectedItem)
            {
                codeMapList.FontFamily = new FontFamily((string)fonts.SelectedItem);

                SaveSettings();
            }
        }

        void fontWeights_SelectionChanged(object sender, SelectionChangedEventArgs e)
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

        void ItemSizeChanged(object sender, SizeChangedEventArgs e)
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

        bool privateFields = true;
        bool publicFields = true;
        bool privateProperties = true;
        bool publicProperties = true;
        bool publicMethods = true;
        bool privateMethods = true;

        public bool PublicFields
        {
            get => publicFields; set { publicFields = value; OnPropertyChanged(nameof(PublicFields)); }
        }

        public bool PrivateProperties
        {
            get => privateProperties; set { privateProperties = value; OnPropertyChanged(nameof(PrivateProperties)); }
        }

        public bool PublicProperties
        {
            get => publicProperties; set { publicProperties = value; OnPropertyChanged(nameof(PublicProperties)); }
        }

        public bool PublicMethods
        {
            get => publicMethods; set { publicMethods = value; OnPropertyChanged(nameof(PublicMethods)); }
        }

        public bool PrivateMethods
        {
            get => privateMethods; set { privateMethods = value; OnPropertyChanged(nameof(PrivateMethods)); }
        }

        public bool PrivateFields
        {
            get => privateFields; set { privateFields = value; OnPropertyChanged(nameof(PrivateFields)); }
        }

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

                foreach (var type in generateMap(file))
                {
                    MemberList.Add(type);

                    if (type.Children?.Any() == true)
                    {
                        if (PublicMethods || PrivateMethods)
                        {
                            var members = type.Children.Where(x => x.MemberType == MemberType.Method);
                            if (!PublicMethods && PrivateMethods) members = members.Where(x => !x.IsPublic);
                            if (PublicMethods && !PrivateMethods) members = members.Where(x => x.IsPublic);

                            foreach (var m in members.OrderBy(x => x.ToString()))
                                MemberList.Add(m);
                        }

                        if (PublicProperties || PrivateProperties)
                        {
                            var members = type.Children.Where(x => x.MemberType == MemberType.Property);
                            if (!PublicProperties && PrivateProperties) members = members.Where(x => !x.IsPublic);
                            if (PublicProperties && !PrivateProperties) members = members.Where(x => x.IsPublic);

                            foreach (var m in members.OrderBy(x => x.ToString()))
                                MemberList.Add(m);
                        }

                        if (PublicFields || PrivateFields)
                        {
                            var members = type.Children.Where(x => x.MemberType == MemberType.Field);
                            if (!PublicFields && PrivateFields) members = members.Where(x => !x.IsPublic);
                            if (PublicFields && !PrivateFields) members = members.Where(x => x.IsPublic);

                            foreach (var m in members.OrderBy(x => x.ToString()))
                                MemberList.Add(m);
                        }
                    }
                }

                ErrorMessage = null;
            }
            catch (Exception e)
            {
                ErrorMessage = e.Message;
            }
        }
    }
}