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
    using System.Windows.Controls;
    using System.Windows.Input;

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

        }

        private void Watcher_Changed(object sender, FileSystemEventArgs e)
        {
            Dispatcher.Invoke(()=>RefreshMap());
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
                        if (force  || docFileTimestamp != File.GetLastWriteTimeUtc(docFile))
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

                    if (line.StartsWithAny("def ", "class "))
                    {
                        var info = new MemberInfo();
                        info.ContentIndent = new string(' ', (code[i].Length - line.Length));
                        info.Line = i;

                        if (line.StartsWith("class "))
                        {
                            if(MemberList.Any())
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