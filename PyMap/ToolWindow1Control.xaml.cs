//------------------------------------------------------------------------------
// <copyright file="ToolWindow1Control.xaml.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Collections.ObjectModel;
using System.Linq;
using EnvDTE80;

namespace PyMap
{
    using EnvDTE;
    using Microsoft.VisualStudio.Text.Editor;
    using System.ComponentModel;
    using System.IO;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;

    /// <summary>
    /// Interaction logic for ToolWindow1Control.
    /// </summary>
    public partial class ToolWindow1Control : UserControl
    {
        CommandEvents commandEvents;
        DTE2 dte;
        PythonParser parser = new PythonParser();
        FileSystemWatcher watcher = new FileSystemWatcher();

        /// <summary>
        /// Initializes a new instance of the <see cref="ToolWindow1Control"/> class.
        /// </summary>
        public ToolWindow1Control()
        {
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

            initialized = true;
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

                if (item.Key == "FontWeight" && fontWeights.Items.Contains(item.Value))
                    fontWeights.SelectedItem = item.Value;

                if (item.Key == "FontSize" && double.TryParse(item.Value, out double new_size))
                    codeMapList.FontSize = new_size;
            }
        }

        private void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            Dispatcher.Invoke(() => RefreshMap());
        }

        string docFile = null;
        DateTime? docFileTimestamp = null;

        void CommandEvents_AfterExecute(string Guid, int ID, object CustomIn, object CustomOut)
        {
            var doc = dte.ActiveDocument;
            if (doc != null)
            {
                if (docFile != doc.FullName)
                {
                    docFile = doc.FullName;
                    RefreshMap();
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
                if (docFile.IsPythonFile())
                {
                    if (File.Exists(docFile))
                    {
                        if (force || docFileTimestamp != File.GetLastWriteTimeUtc(docFile))
                        {
                            parser.GenerateContentPython(docFile);
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
            catch (Exception e)
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
            return; // disable for now

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
    }

    class PythonParser : INotifyPropertyChanged
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

        public void GenerateContentPython(string file)
        {
            try
            {
                var code = File.ReadAllLines(file);

                MemberList.Clear();

                for (int i = 0; i < code.Length; i++)
                {
                    var line = code[i].TrimStart();

                    if (line.StartsWithAny("def ", "class ", "@"))
                    {
                        var info = new MemberInfo();
                        info.ContentIndent = new string(' ', (code[i].Length - line.Length));
                        info.Line = i;

                        if (line.StartsWith("@"))
                        {
                            info.ContentType = "@";
                            info.Content = line.Substring("@".Length).Trim();
                        }
                        else if (line.StartsWith("class"))
                        {
                            if (MemberList.Any())
                                MemberList.Add(new MemberInfo { Line = -1 });
                            info.ContentType = "class";
                            info.Content = line.Substring("class ".Length).TrimEnd();
                        }
                        else
                        {
                            info.ContentType = "def";
                            info.Content = line.Substring("def ".Length).TrimEnd();
                        }

                        MemberList.Add(info);
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