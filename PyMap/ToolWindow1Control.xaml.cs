using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

// using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;

// using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
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
        WindowEvents windowEvents;
        // TextEditorEvents textEditorEvents;   // The same as for SelectionEvents, it's not suitable for detection of the caret position

        DTE2 dte;
        SyntaxParser parser = new SyntaxParser();
        FileSystemWatcher watcher = new FileSystemWatcher();
        DispatcherTimer timer = new DispatcherTimer(DispatcherPriority.Background);

        /// <summary>
        /// Initializes a new instance of the <see cref="ToolWindow1Control"/> class.
        /// </summary>
        public ToolWindow1Control()
        {
            Instance = this;

            ThreadHelper.ThrowIfNotOnUIThread();

            this.InitializeComponent();

            this.DataContext = this.parser;

            // any changes in IDE should trigger checking if the active doc is pointing to a different file and the map needs to regenerated
            this.dte = Global.GetDTE2();
            commandEvents = dte.Events.CommandEvents;
            windowEvents = dte.Events.WindowEvents;

            commandEvents.AfterExecute += (a, b, c, d) => RefreshMapIfRequired();
            windowEvents.WindowActivated += (a, b) => RefreshMapIfRequired();

            // watcher's filter (file being watched) is updated every time the map is refreshed
            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
            watcher.Changed += Watcher_Changed;
            watcher.Created += Watcher_Changed;
            watcher.Deleted += Watcher_Changed;
            watcher.Renamed += Watcher_Changed;

            timer.Tick += (x, y) => CheckIfThemeChanged();
            timer.Interval = TimeSpan.FromSeconds(2);
            timer.Start();

            ReadSettings();

            codeMapList.SelectionChanged += CodeMapList_SelectionChanged;
            codeMapList.PreviewMouseLeftButtonUp += CodeMapList_PreviewMouseLeftButtonUp;
            parser.MapInvalidated += () => RefreshMap(force: true);

            initialized = true;
        }

        object lastSelectedItem;

        private void CodeMapList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (lastSelectedItem == codeMapList.SelectedItem)
            {
                // go to the member stat line as you could ne in the middle of the large method
                NavigateToSelectedMember();
            }
            else
            {
                // on selection change will handle it. It will also handle other selection
                // changing triggers like keyboard strokes
            }
            lastSelectedItem = codeMapList.SelectedItem;
        }

        int lastCaretPosition = -1;

        void CheckCurrentCaretPosition()
        {
            try
            {
                var currLine = Global.GetTextView().Selection.Start.Position.GetContainingLineNumber();
                if (lastCaretPosition != currLine)
                    SynchButton_MouseDown(null, null);
            }
            catch { }
        }

        void CheckIfThemeChanged()
        {
            try
            {
                if (parser.AutoSynch)
                    CheckCurrentCaretPosition();

                var isCurrentThemeDark = !codeMapList.Background.IsBright();
                if (ExtensionHost.IsDarkTheme != isCurrentThemeDark)
                {
                    ExtensionHost.IsDarkTheme = isCurrentThemeDark;

                    parser.OnThemChange();
                    RefreshMap(true);
                }
            }
            catch { }
        }

        bool skipNextSelectionChange = false;

        private void CodeMapList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (skipNextSelectionChange)
                skipNextSelectionChange = false;
            else
                NavigateToSelectedMember();

            // if the selection is changed, the scroll position should be changed to the default position.
            // This is to avoid jumps to most right when the member signature is too wide.
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                codeMapList.FindChild<ScrollViewer>()?.ScrollToHorizontalOffset(0);
            }));
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

        // TODO: need to automate serialization
        string SerializeSettings()
        {
            return $"FontSize:{codeMapList.FontSize}\n" +
                   $"PublicMethods:{parser.PublicMethods}\n" +
                   $"PublicProperties:{parser.PublicProperties}\n" +
                   $"PublicFields:{parser.PublicFields}\n" +
                   $"PrivateMethods:{parser.PrivateMethods}\n" +
                   $"PrivateProperties:{parser.PrivateProperties}\n" +
                   $"PrivateFields:{parser.PrivateFields}\n" +
                   $"SortMembers:{parser.SortMembers}\n" +
                   $"AutoSynch:{parser.AutoSynch}\n" +
                   $"ShowMethodSignatures:{parser.ShowMethodSignatures}";
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
                else if (item.Key == "SortMembers") parser.SortMembers = bool.Parse(item.Value);
                else if (item.Key == "ShowMethodSignatures") parser.ShowMethodSignatures = bool.Parse(item.Value);
                else if (item.Key == "AutoSynch") parser.AutoSynch = bool.Parse(item.Value);
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

        void RefreshMapIfRequired()
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
                        lastSelectedItem = codeMapList.SelectedItem;
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

        int lastAutoRecover = Environment.TickCount;

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
                        textView.SelectLine(info.Line);
                    }
                }
            }
            catch (Exception)
            {
                // the system may not be ready yet
                // and throw even for the already loaded file
                // Saving document seems to be a good reset for the IDE
                // parser.ErrorMessage = ex.Message;

                if ((Environment.TickCount - lastAutoRecover) > 1000)
                {
                    lastAutoRecover = Environment.TickCount;
                    ScheduleSaveDoc(700);
                }
            }
        }

        void ScheduleSaveDoc(int delay)
        {
            _ = Task.Run(async () =>
            {
                System.Threading.Thread.Sleep(delay);
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                try
                {
                    dte.ActiveDocument.Save();
                }
                catch { }
            });
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

        private void SynchButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var currLine = Global.GetTextView().Selection.Start.Position.GetContainingLineNumber();
                var correspondingMember = this.parser.MemberList.OrderBy(x => x.Line).TakeWhile(x => x.Line <= currLine).LastOrDefault();

                if (codeMapList.SelectedItem != correspondingMember)
                {
                    skipNextSelectionChange = true;
                    codeMapList.SelectedItem = correspondingMember;
                    codeMapList.ScrollIntoView(codeMapList.SelectedItem);
                    lastSelectedItem = codeMapList.SelectedItem;
                }
            }
            catch { }
        }

        private void ClearButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            parser.ClassName = "";
            parser.MemberName = "";
        }
    }
}

static class UIHelper
{
    /// <summary>
    /// Finds a Child of a given item in the visual tree.
    /// </summary>
    /// <param name="parent">A direct parent of the queried item.</param>
    /// <typeparam name="T">The type of the queried item.</typeparam>
    /// <param name="childName">x:Name or Name of child. </param>
    /// <returns>The first parent item that matches the submitted type parameter.
    /// If not matching item can be found,
    /// a null parent is being returned.</returns>
    public static T FindChild<T>(this DependencyObject parent, string childName = null)
        where T : DependencyObject
    {
        if (parent == null)
            return null;

        T foundChild = null;

        int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childrenCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            T childType = child as T;
            if (childType == null)
            {
                foundChild = FindChild<T>(child, childName);

                if (foundChild != null)
                    break;
            }
            else if (!string.IsNullOrEmpty(childName))
            {
                var frameworkElement = child as FrameworkElement;
                if (frameworkElement != null && frameworkElement.Name == childName)
                {
                    foundChild = (T)child;
                    break;
                }
            }
            else
            {
                foundChild = (T)child;
                break;
            }
        }

        return foundChild;
    }
}