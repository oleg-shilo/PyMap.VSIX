using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PyMap
{
    using System.ComponentModel;
    using System.IO;
    using System.Threading;
    using System.Windows.Media.Imaging;

    class SyntaxParser : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public event Action MapInvalidated;

        public void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            if (propertyName != nameof(ErrorMessage) &&
                propertyName != nameof(IsErrorState) &&
                propertyName != nameof(IsCSharp) &&
                propertyName != nameof(IsPython))
            {
                MapInvalidated?.Invoke();
            }
        }

        public void OnThemChange()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SynchIcon)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ClearIcon)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FilterIcon)));
        }

        public ObservableCollection<MemberInfo> MemberList { get; set; } = new ObservableCollection<MemberInfo>();

        bool privateFields = true;
        bool publicFields = true;
        bool privateProperties = true;
        bool publicProperties = true;
        bool publicMethods = true;
        bool privateMethods = true;
        bool sortMembers = true;
        string className;
        string memberName;
        bool autoSynch;

        public bool AutoSynch
        {
            get => autoSynch; set { autoSynch = value; OnPropertyChanged(nameof(AutoSynch)); }
        }

        public bool SortMembers
        {
            get => sortMembers; set { sortMembers = value; OnPropertyChanged(nameof(SortMembers)); }
        }

        public string MemberName
        {
            get => memberName; set { memberName = value; OnPropertyChanged(nameof(MemberName)); }
        }

        public string ClassName
        {
            get => className; set { className = value; OnPropertyChanged(nameof(ClassName)); }
        }

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

        bool isCSharp = true;

        public bool IsCSharp
        {
            get => isCSharp; set { isCSharp = value; OnPropertyChanged(nameof(IsCSharp)); OnPropertyChanged(nameof(IsPython)); }
        }

        public bool IsPython => !isCSharp;

        string errorMessage;

        public bool IsErrorState => !string.IsNullOrEmpty(ErrorMessage);

        public string ErrorMessage
        {
            get { return errorMessage; }

            set
            {
                errorMessage = value;
                OnPropertyChanged(nameof(ErrorMessage));
                OnPropertyChanged(nameof(IsErrorState));
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
            IsCSharp = (fileType == ".cs" || fileType == ".razor");

            return mappers.ContainsKey(fileType);
        }

        Dictionary<string, Func<string, IEnumerable<MemberInfo>>> mappers = new Dictionary<string, Func<string, IEnumerable<MemberInfo>>>()
        {
            { ".cs", CSharpMapper.Generate },
            { ".razor", CSharpMapper.Generate },
            { ".py", PythonMapper.Generate },
            { ".pyw", PythonMapper.Generate },
        };

        public void GenerateMap(string file)
        {
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    MemberList.Clear();

                    var fileType = Path.GetExtension(file).ToLower();
                    var generateMap = mappers[fileType];

                    var items = generateMap(file).OrderBy(x => x.Line);

                    foreach (var item in items)
                    {
                        // Only Python parser is primitive because classes and functions do not encode relationships.
                        // just plain list. so ignore the class name filter
                        if (!IsPython)
                            if (ClassName?.Any() == true && // class name filter is set
                                item.Title.IndexOf(ClassName, StringComparison.OrdinalIgnoreCase) == -1)
                                continue;

                        List<MemberInfo> typeMembers = new List<MemberInfo>();

                        bool matchingMember(string name)
                        {
                            if (MemberName?.Any() == true)
                                return name.IndexOf(MemberName, StringComparison.OrdinalIgnoreCase) != -1;
                            else
                                return true;
                        }

                        var children = item.Children.OrderBy(x => x.Line).ToList();

                        if (item.MemberType == MemberType.Class || item.MemberType == MemberType.Interface)
                        {
                            MemberList.Add(item);
                            children = item.Children;
                        }
                        else
                        {
                            children = new List<MemberInfo> { item };
                        }

                        if (children?.Any() == true)
                        {
                            if (PublicMethods || PrivateMethods)
                            {
                                // if (item.MemberType == MemberType.Region)
                                // {
                                //     MemberList.Add(item);
                                // }

                                var members = children.Where(x => x.MemberType == MemberType.Method || x.MemberType == MemberType.Constructor);

                                if (!PublicMethods && PrivateMethods) members = members.Where(x => !x.IsPublic);
                                if (PublicMethods && !PrivateMethods) members = members.Where(x => x.IsPublic);

                                foreach (var m in members.OrderBy(x => x.MemberType != MemberType.Constructor)
                                                         .ThenBy(x => x.ToString()))
                                {
                                    if (matchingMember(m.Content))
                                        typeMembers.Add(m);
                                }
                            }

                            if (PublicProperties || PrivateProperties)
                            {
                                var members = children.Where(x => x.MemberType == MemberType.Property);

                                if (!PublicProperties && PrivateProperties) members = members.Where(x => !x.IsPublic);
                                if (PublicProperties && !PrivateProperties) members = members.Where(x => x.IsPublic);

                                foreach (var m in members.OrderBy(x => x.ToString()))
                                    if (matchingMember(m.Content))
                                        typeMembers.Add(m);
                            }

                            if (PublicFields || PrivateFields)
                            {
                                var members = children.Where(x => x.MemberType == MemberType.Field);

                                if (!PublicFields && PrivateFields) members = members.Where(x => !x.IsPublic);
                                if (PublicFields && !PrivateFields) members = members.Where(x => x.IsPublic);

                                foreach (var m in members.OrderBy(x => x.ToString()))
                                    if (matchingMember(m.Content))
                                        typeMembers.Add(m);
                            }
                        }

                        if (!SortMembers)
                        {
                            typeMembers.AddRange(children.Where(x => x.MemberType == MemberType.Region));
                            typeMembers = typeMembers.OrderBy(x => x.Line).ToList();
                        }
                        foreach (var member in typeMembers)
                            MemberList.Add(member);
                        // typeMembers.ForEach(MemberList.Add);
                    }

                    ErrorMessage = null;
                    return;
                }
                catch (Exception e)
                {
                    ErrorMessage = e.Message;
                }
                // retrying as the file might be locked
                Thread.Sleep(400);
            }
        }

        public BitmapSource SynchIcon => AppImages.Synch;
        public BitmapSource ClearIcon => AppImages.Clear;
        public BitmapSource FilterIcon => AppImages.Filter;

        public BitmapSource PrivateOverlayIcon => AppImages.PrivateOverlay;
        public BitmapSource PublicOverlayIcon => AppImages.PublicOverlay;
    }

    public class AppImages
    {
        public static BitmapSource PrivateOverlay => (ExtensionHost.IsDarkTheme ? "PyMap.Resources.icons.dark.private.png".LoadAsEmbeddedResourceImage() : "PyMap.Resources.icons.light.private.png".LoadAsEmbeddedResourceImage());
        public static BitmapSource PublicOverlay => (ExtensionHost.IsDarkTheme ? "PyMap.Resources.icons.dark.public.png".LoadAsEmbeddedResourceImage() : "PyMap.Resources.icons.light.public.png".LoadAsEmbeddedResourceImage());
        public static BitmapSource Synch => (ExtensionHost.IsDarkTheme ? "PyMap.Resources.icons.dark.synch.png".LoadAsEmbeddedResourceImage() : "PyMap.Resources.icons.light.synch.png".LoadAsEmbeddedResourceImage());
        public static BitmapSource Clear => (ExtensionHost.IsDarkTheme ? "PyMap.Resources.icons.dark.clear.png".LoadAsEmbeddedResourceImage() : "PyMap.Resources.icons.light.clear.png".LoadAsEmbeddedResourceImage());
        public static BitmapSource Filter => (ExtensionHost.IsDarkTheme ? "PyMap.Resources.icons.dark.filter.png".LoadAsEmbeddedResourceImage() : "PyMap.Resources.icons.light.filter.png".LoadAsEmbeddedResourceImage());
    }
}