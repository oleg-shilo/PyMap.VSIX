using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

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

namespace CodeMap
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
        Point _dragStartPoint;
        object _draggedItem;
        AdornerLayer _adornerLayer;
        DropPositionAdorner _dropAdorner;

        static ToolWindow1Control Instance;

        CommandEvents commandEvents;
        WindowEvents windowEvents;
        // TextEditorEvents textEditorEvents;   // The same as for SelectionEvents, it's not suitable for detection of the caret position

        DTE2 dte;
        SyntaxParser parser = new SyntaxParser();
        FileSystemWatcher watcher = new FileSystemWatcher();
        DispatcherTimer timer = new DispatcherTimer(DispatcherPriority.Background);
        DispatcherTimer dispatcherTimer = new DispatcherTimer(DispatcherPriority.Background);

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
            // This is to avoid jumps to most right when the member signature is too wide

            _ = Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
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

        void SaveSettings()
        {
            try
            {
                if (initialized)
                {
                    // pump date from VM to V
                    // TODO: need to automate the property exchange

                    Settings.Instance.FontSize = codeMapList.FontSize;
                    Settings.Instance.Classes = parser.Classes;
                    Settings.Instance.Interfaces = parser.Interfaces;
                    Settings.Instance.Structs = parser.Structs;
                    Settings.Instance.Others = parser.Others;
                    Settings.Instance.PublicMethods = parser.PublicMethods;
                    Settings.Instance.PublicProperties = parser.PublicProperties;
                    Settings.Instance.PublicFields = parser.PublicFields;
                    Settings.Instance.PrivateMethods = parser.PrivateMethods;
                    Settings.Instance.PrivateProperties = parser.PrivateProperties;
                    Settings.Instance.PrivateFields = parser.PrivateFields;
                    Settings.Instance.SortMembers = parser.SortMembers;
                    Settings.Instance.AutoSynch = parser.AutoSynch;
                    Settings.Instance.ShowMethodSignatures = parser.ShowMethodSignatures;

                    Settings.Instance.Save();
                }
            }
            catch { }
        }

        void ReadSettings()
        {
            try
            {
                Settings.Init();

                // Settings.Instance.FontSize = codeMapList.FontSize;
                // Settings.Instance.Classes = parser.Classes;
                // Settings.Instance.Interfaces = parser.Interfaces;
                // Settings.Instance.Structs = parser.Structs;
                // Settings.Instance.Others = parser.Others;
                // Settings.Instance.PublicMethods = parser.PublicMethods;
                // Settings.Instance.PublicProperties = parser.PublicProperties;
                // Settings.Instance.PublicFields = parser.PublicFields;
                // Settings.Instance.PrivateMethods = parser.PrivateMethods;
                // Settings.Instance.PrivateProperties = parser.PrivateProperties;
                // Settings.Instance.PrivateFields = parser.PrivateFields;
                // Settings.Instance.SortMembers = parser.SortMembers;
                // Settings.Instance.AutoSynch = parser.AutoSynch;
                // Settings.Instance.ShowMethodSignatures = parser.ShowMethodSignatures;
            }
            catch { }
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
                else
                {
                    docFile = null;
                    parser.Clear();
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

        public event EventHandler BookmarkMenuClick;

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (codeMapList.SelectedItem is MemberInfo info && sender is MenuItem menuItem)
            {
                try
                {
                    var bookmarkName = menuItem.Tag.ToString();
                    if (bookmarkName == "none-all")
                    {
                        parser.MemberList.ToList().ForEach(x => x.ColorContext = "");
                        BookmarksStore.Clear(docFile);
                    }
                    else if (info != null)
                    {
                        info.ColorContext = (bookmarkName == "none") ? "" : bookmarkName;
                        BookmarksStore.Store(docFile, info.Id, info.ColorContext);
                    }
                    _ = Task.Run(BookmarksStore.Save);
                    BookmarkMenuClick?.Invoke(this, null);
                }
                catch
                {
                }
            }
        }

        void codeMapList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!parser.CanParse(docFile) || !parser.IsCSharp || parser.SortMembers)
            {
                _draggedItem = null;
                return;
            }
            _dragStartPoint = e.GetPosition(null);
            var item = GetListBoxItemAt(e.GetPosition(codeMapList));
            _draggedItem = item?.DataContext;
        }

        void codeMapList_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!parser.CanParse(docFile) || !parser.IsCSharp || parser.SortMembers)
                return;

            if (e.LeftButton == MouseButtonState.Pressed && _draggedItem != null)
            {
                var pos = e.GetPosition(null);
                if (Math.Abs(pos.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(pos.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    DragDrop.DoDragDrop(codeMapList, _draggedItem, DragDropEffects.Move);
                }
            }
        }

        void codeMapList_DragOver(object sender, DragEventArgs e)
        {
            var pos = e.GetPosition(codeMapList);
            int insertIndex = GetInsertIndex(pos);

            if (_adornerLayer == null)
                _adornerLayer = AdornerLayer.GetAdornerLayer(codeMapList);

            RemoveDropAdorner();

            // prevent dragging over if the document is not C# or razor file
            if (!parser.CanParse(docFile) || !parser.IsCSharp)
            {
                e.Effects = DragDropEffects.None;
                Mouse.SetCursor(Cursors.None);
                return;
            }

            _dropAdorner = new DropPositionAdorner(codeMapList, insertIndex);
            _adornerLayer.Add(_dropAdorner);

            e.Effects = DragDropEffects.Move;
            e.Handled = true;
            Mouse.SetCursor(Cursors.Arrow);
        }

        void codeMapList_Drop(object sender, DragEventArgs e)
        {
            RemoveDropAdorner();

            var pos = e.GetPosition(codeMapList);
            int insertIndex = GetInsertIndex(pos);

            var items = codeMapList.ItemsSource as System.Collections.IList;

            // MemberInfo src = _draggedItem as MemberInfo;
            MemberInfo src = e.Data.GetData(typeof(MemberInfo)) as MemberInfo;
            MemberInfo dest = items[insertIndex] as MemberInfo;

            if (items != null && src != null)
            {
                if (src != null && dest != null && src != dest && !(insertIndex > 0 && items[insertIndex - 1] == src))
                {
                    // Debug.WriteLine($"Source: {src.Id} -> Dest: above {dest.Id}; ");
                    MoveDocumentRegionInEditor(src.Line, src.EndLine, dest.Line);
                    try
                    {
                        dte.ActiveDocument.Save();
                    }
                    catch { }

                    // cannot call RefreshMap as it will use the file to read the code not the dte
                    // so Refresh will be called by the file watcher
                    // RefreshMap(true);

                    // should be done in the background on the refresh
                    // int oldIndex = items.IndexOf(src);
                    // if (oldIndex >= 0)
                    // {
                    //     items.RemoveAt(oldIndex);
                    //     if (insertIndex > oldIndex) insertIndex--;
                    // }

                    // items.Insert(insertIndex, src);
                }
            }

            e.Handled = true;
        }

        void RemoveDropAdorner()
        {
            if (_dropAdorner != null && _adornerLayer != null)
            {
                _adornerLayer.Remove(_dropAdorner);
                _dropAdorner = null;
            }
        }

        ListBoxItem GetListBoxItemAt(Point position)
        {
            var element = codeMapList.InputHitTest(position) as DependencyObject;
            while (element != null && !(element is ListBoxItem))
                element = VisualTreeHelper.GetParent(element);
            return element as ListBoxItem;
        }

        int GetInsertIndex(Point position)
        {
            for (int i = 0; i < codeMapList.Items.Count; i++)
            {
                var item = codeMapList.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
                if (item != null)
                {
                    var bounds = VisualTreeHelper.GetDescendantBounds(item);
                    var topLeft = item.TranslatePoint(new Point(0, 0), codeMapList);
                    if (position.Y < topLeft.Y + bounds.Height / 2)
                        return i;
                }
            }
            return codeMapList.Items.Count;
        }

        private void codeMapList_PreviewDragOver(object sender, DragEventArgs e)
        {
            // Always allow move, even in the gap between items
            // if (e.Data.GetDataPresent(typeof(MemberInfo)))
            e.Effects = DragDropEffects.Move;
            // else
            // e.Effects = DragDropEffects.None;

            // e.Handled = true;
        }

        private void Border_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (!parser.CanParse(docFile) || !parser.IsCSharp || parser.SortMembers)
                e.Effects = DragDropEffects.None;
            else
                e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        private void Border_PreviewDragEnter(object sender, DragEventArgs e)
        {
            if (!parser.CanParse(docFile) || !parser.IsCSharp || parser.SortMembers)
                e.Effects = DragDropEffects.None;
            else
                e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        void MoveDocumentRegionInEditor(int startLine, int endLine, int insertBeforeLine)
        {
            IWpfTextView textView = Global.GetTextView();
            ITextSnapshot snapshot = textView.TextSnapshot;
            ITextBuffer buffer = snapshot.TextBuffer;

            // Validate input
            if (startLine < 0 || endLine >= snapshot.LineCount || startLine > endLine)
                throw new ArgumentOutOfRangeException("Invalid start or end line.");
            if (insertBeforeLine < 0 || insertBeforeLine > snapshot.LineCount)
                throw new ArgumentOutOfRangeException("Invalid insertBeforeLine.");

            // Extend endLine to include any empty lines after the original endLine
            int lastLine = snapshot.LineCount - 1;
            int extendedEndLine = endLine;
            while (extendedEndLine < lastLine)
            {
                var nextLine = snapshot.GetLineFromLineNumber(extendedEndLine + 1);
                if (string.IsNullOrWhiteSpace(nextLine.GetText()))
                    extendedEndLine++;
                else
                    break;
            }

            // Get the region text
            var startLineObj = snapshot.GetLineFromLineNumber(startLine);
            var endLineObj = snapshot.GetLineFromLineNumber(extendedEndLine);
            int regionStart = startLineObj.Start.Position;
            int regionEnd = endLineObj.EndIncludingLineBreak.Position;
            string regionText = snapshot.GetText(regionStart, regionEnd - regionStart);

            // Remove the region
            using (var edit = buffer.CreateEdit())
            {
                edit.Delete(regionStart, regionEnd - regionStart);
                edit.Apply();
            }

            // After removal, recalculate the insert position
            snapshot = buffer.CurrentSnapshot;
            int newInsertLine = insertBeforeLine;
            if (insertBeforeLine > extendedEndLine)
                newInsertLine -= (extendedEndLine - startLine + 1);

            int insertPos = (newInsertLine < snapshot.LineCount)
                ? snapshot.GetLineFromLineNumber(newInsertLine).Start.Position
                : snapshot.Length;

            // Insert the region
            using (var edit = buffer.CreateEdit())
            {
                edit.Insert(insertPos, regionText);
                edit.Apply();
            }
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