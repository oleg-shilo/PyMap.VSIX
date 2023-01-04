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
using System.Windows.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
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
            commandEvents.AfterExecute += CommandEvents_AfterExecute;

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
            parser.MapInvalidated += () => RefreshMap(force: true);

            initialized = true;
        }

        int lastCaretPosition = -1;

        void CshckCurrentCaretPosition()
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
            if (parser.AutoSynch)
                CshckCurrentCaretPosition();
            try
            {
                var isCurrentThemeDark = !codeMapList.Background.IsBright();
                if (ExtensionHost.IsDarkTheme != isCurrentThemeDark)
                {
                    ExtensionHost.IsDarkTheme = isCurrentThemeDark;
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
                   $"AutoSynch:{parser.AutoSynch}\n" +
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
                            parser.OnPropertyChanged(nameof(parser.SynchIcon));
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
                }
            }
            catch { }
        }
    }

    internal class IDEEventsListener : IVsRunningDocTableEvents3
    {
        public int OnBeforeSave(uint docCookie)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return VSConstants.S_OK;
        }

        public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining) => VSConstants.S_OK;

        public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining) => VSConstants.S_OK;

        public int OnAfterSave(uint docCookie) => VSConstants.S_OK;

        public int OnAfterAttributeChange(uint docCookie, uint grfAttribs) => VSConstants.S_OK;

        public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame) => VSConstants.S_OK;

        public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame) => VSConstants.S_OK;

        public int OnAfterAttributeChangeEx(uint docCookie, uint grfAttribs, IVsHierarchy pHierOld, uint itemidOld, string pszMkDocumentOld, IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew) => VSConstants.S_OK;

        public void OnAfterDocumentLockCountChanged(uint docCookie, uint dwRDTLockType, uint dwOldLockCount, uint dwNewLockCount)
        { }
    }
}