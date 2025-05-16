using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CodeMap
{
    using System.ComponentModel;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Media.Imaging;

    public struct MyStruct
    {
        public int X;
        public int Y;
    }

    public interface MyInterface
    {
    }

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

        string className;
        string memberName;

        public bool ShowMethodSignatures
        {
            get => Settings.Instance.ShowMethodSignatures; set { Settings.Instance.ShowMethodSignatures = value; OnPropertyChanged(nameof(ShowMethodSignatures)); }
        }

        public bool AutoSynch
        {
            get => Settings.Instance.AutoSynch; set { Settings.Instance.AutoSynch = value; OnPropertyChanged(nameof(AutoSynch)); }
        }

        public bool SortMembers
        {
            get => Settings.Instance.SortMembers; set { Settings.Instance.SortMembers = value; OnPropertyChanged(nameof(SortMembers)); }
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
            get => Settings.Instance.PublicFields; set { Settings.Instance.PublicFields = value; OnPropertyChanged(nameof(PublicFields)); }
        }

        public bool PrivateProperties
        {
            get => Settings.Instance.PrivateProperties; set { Settings.Instance.PrivateProperties = value; OnPropertyChanged(nameof(PrivateProperties)); }
        }

        public bool PublicProperties
        {
            get => Settings.Instance.PublicProperties; set { Settings.Instance.PublicProperties = value; OnPropertyChanged(nameof(PublicProperties)); }
        }

        public bool PublicMethods
        {
            get => Settings.Instance.PublicMethods; set { Settings.Instance.PublicMethods = value; OnPropertyChanged(nameof(PublicMethods)); }
        }

        public bool PrivateMethods
        {
            get => Settings.Instance.PrivateMethods; set { Settings.Instance.PrivateMethods = value; OnPropertyChanged(nameof(PrivateMethods)); }
        }

        public bool PrivateFields
        {
            get => Settings.Instance.PrivateFields; set { Settings.Instance.PrivateFields = value; OnPropertyChanged(nameof(PrivateFields)); }
        }

        public bool Structs
        {
            get => Settings.Instance.Structs; set { Settings.Instance.Structs = value; OnPropertyChanged(nameof(Structs)); }
        }

        public bool Others
        {
            get => Settings.Instance.Others; set { Settings.Instance.Others = value; OnPropertyChanged(nameof(Others)); }
        }

        public bool Classes
        {
            get => Settings.Instance.Classes; set { Settings.Instance.Classes = value; OnPropertyChanged(nameof(Classes)); }
        }

        public bool Interfaces
        {
            get => Settings.Instance.Interfaces; set { Settings.Instance.Interfaces = value; OnPropertyChanged(nameof(Interfaces)); }
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

        Dictionary<string, Func<string, bool, IEnumerable<MemberInfo>>> mappers = new Dictionary<string, Func<string, bool, IEnumerable<MemberInfo>>>();

        public SyntaxParser()
        {
            mappers.Add(".cs", CSharpMapper.Generate);
            mappers.Add(".razor", CSharpMapper.Generate);
            mappers.Add(".py", PythonMapper.Generate);
            mappers.Add(".pyw", PythonMapper.Generate);
            mappers.Add(".css", CssMapper.Generate);
            mappers.Add(".js", JavaScriptMapper.Generate);
        }

        public void GenerateMap(string file)
        {
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    MemberList.Clear();

                    var fileType = Path.GetExtension(file).ToLower();
                    var generateMap = mappers[fileType];

                    var items = generateMap(file, ShowMethodSignatures).OrderBy(x => x.Line);

                    foreach (MemberInfo item in items)
                    {
                        var bookmark = BookmarksStore.Read(file, item.Id);
                        if (bookmark != null)
                            item.ColorContext = bookmark;

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

                        var children = item.Children?.OrderBy(x => x.Line).ToList();

                        if (item.MemberType == MemberType.Class)
                        {
                            if ((item.IsPublic && !Classes) ||
                                (!item.IsPublic && !Interfaces))
                                continue;
                        }

                        if (item.MemberType == MemberType.Class && !Classes)
                            continue;

                        if (item.MemberType == MemberType.Interface && !Interfaces)
                            continue;

                        if (item.MemberType == MemberType.Struct && !Structs)
                            continue;

                        if ((item.MemberType != MemberType.Class &&
                             item.MemberType != MemberType.Interface &&
                             item.MemberType != MemberType.Struct)
                             && !Others)
                            continue;

                        if (item.MemberType == MemberType.Class
                            || item.MemberType == MemberType.Interface
                            || item.MemberType == MemberType.Type
                            || item.MemberType == MemberType.Struct)
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
                        {
                            bookmark = BookmarksStore.Read(file, member.Id);
                            if (bookmark != null)
                                member.ColorContext = bookmark;

                            MemberList.Add(member);
                        }
                    }

                    // `items` is a list of all root level MemberInfo objects where which item has only one level
                    // of nesting: item.Children of `MemberInfo` type.
                    // Flattening the list of all items and their children to capture the all current bookmarks

                    var currentFileBookmarks = items.Concat(items.SelectMany(x => x.Children)).Select(x => x.Id).ToArray();

                    _ = Task.Run(() => BookmarksStore.Purge(file, currentFileBookmarks));

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
        public static BitmapSource PrivateOverlay => (ExtensionHost.IsDarkTheme ? "CodeMap.Resources.icons.dark.private.png".LoadAsEmbeddedResourceImage() : "CodeMap.Resources.icons.light.private.png".LoadAsEmbeddedResourceImage());
        public static BitmapSource PublicOverlay => (ExtensionHost.IsDarkTheme ? "CodeMap.Resources.icons.dark.public.png".LoadAsEmbeddedResourceImage() : "CodeMap.Resources.icons.light.public.png".LoadAsEmbeddedResourceImage());
        public static BitmapSource Synch => (ExtensionHost.IsDarkTheme ? "CodeMap.Resources.icons.dark.synch.png".LoadAsEmbeddedResourceImage() : "CodeMap.Resources.icons.light.synch.png".LoadAsEmbeddedResourceImage());
        public static BitmapSource Clear => (ExtensionHost.IsDarkTheme ? "CodeMap.Resources.icons.dark.clear.png".LoadAsEmbeddedResourceImage() : "CodeMap.Resources.icons.light.clear.png".LoadAsEmbeddedResourceImage());
        public static BitmapSource Filter => (ExtensionHost.IsDarkTheme ? "CodeMap.Resources.icons.dark.filter.png".LoadAsEmbeddedResourceImage() : "CodeMap.Resources.icons.light.filter.png".LoadAsEmbeddedResourceImage());
    }
}