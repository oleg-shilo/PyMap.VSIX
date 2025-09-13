using System;
using System.Collections.Generic;
using System.ComponentModel;

using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Package;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using EnvDTE;
using EnvDTE80;
using Newtonsoft.Json.Linq;

// using PyMap.Resources.icons.dark;

namespace CodeMap
{
    static class Global
    {
        static public Func<Type, object> GetService = Package.GetGlobalService;

        public static DTE2 GetDTE2()
        {
            return Get<DTE>() as DTE2;
        }

        public static T Get<T>()
        {
            return (T)GetService(typeof(T));
        }

        public static int GetIndent(this string text) => text.Length - text.TrimStart().Length;

        public static int Brightness(this Color color)
        {
            return (color.R + color.R + color.R + color.B + color.G + color.G + color.G + color.G) >> 3;
        }

        public static bool IsBright(this Brush brush)
        {
            var c = ((SolidColorBrush)brush).Color;
            const double brigthnessTreshold = 0.7;

            return Math.Sqrt(c.R * c.R * .241 + c.G * c.G * .691 + c.B * c.B * .068) / 255 >= brigthnessTreshold;
            //return (color.R + color.R + color.R + color.B + color.G + color.G + color.G + color.G) >> 3;
        }

        public static bool StartsWithAny(this string text, params string[] patterns)
        {
            return patterns.Any(x => text.StartsWith(x));
        }

        public static void MoveCaretToLine(this IWpfTextView obj, int line)
        {
            var snapshotLine = obj.GetLine(line);
            var pos = snapshotLine.Start.Position;
            obj.MoveCaretTo(pos);
            obj.ViewScroller.EnsureSpanVisible(new SnapshotSpan(snapshotLine.Start, 1));
            obj.VisualElement.Focus();
        }

        public static void SelectLine(this IWpfTextView obj, int line)
        {
            var snapshotLine = obj.GetLine(line);
            var start = snapshotLine.Start.Position;
            var end = snapshotLine.End.Position;

            var newSpan = new SnapshotSpan(snapshotLine.Start, end - start);

            obj.Selection.Select(newSpan, false);
        }

        public static void MoveCaretTo(this IWpfTextView obj, int position)
        {
            var point = new SnapshotPoint(obj.TextSnapshot, position);
            obj.Caret.MoveTo(point);
        }

        public static ITextSnapshotLine GetLine(this IWpfTextView obj, int lineNumber)
        {
            return obj.TextSnapshot.GetLineFromLineNumber(lineNumber);
        }

        public static IWpfTextView GetTextView()
        {
            return GetViewHost().TextView;
        }

        public static IWpfTextViewHost GetViewHost()
        {
            object holder;
            Guid guidViewHost = DefGuidList.guidIWpfTextViewHost;
            GetUserData().GetData(ref guidViewHost, out holder);
            return (IWpfTextViewHost)holder;
        }

        static IVsUserData GetUserData()
        {
            int mustHaveFocus = 1;//means true
            IVsTextView currentTextView;
            IVsTextManager txtMgr = (IVsTextManager)GetService(typeof(SVsTextManager));
            txtMgr.GetActiveView(mustHaveFocus, null, out currentTextView);

            if (currentTextView is IVsUserData)
                return currentTextView as IVsUserData;
            else
                throw new ApplicationException("No text view is currently open");
            // Console.WriteLine("No text view is currently open"); return;
        }
    }

    public static class Extensions
    {
        public static System.Drawing.Bitmap ToWinFormsBitmap(this BitmapSource bitmapsource)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapsource));
                enc.Save(stream);

                using (var tempBitmap = new System.Drawing.Bitmap(stream))
                {
                    // According to MSDN, one "must keep the stream open for the lifetime of the Bitmap."
                    // So we return a copy of the new bitmap, allowing us to dispose both the bitmap and the stream.
                    return new System.Drawing.Bitmap(tempBitmap);
                }
            }
        }

        public static BitmapSource ToWpfBitmap(this System.Drawing.Bitmap bitmap)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Bmp);

                stream.Position = 0;
                BitmapImage result = new BitmapImage();
                result.BeginInit();
                // According to MSDN, "The default OnDemand cache option retains access to the stream until the image is needed."
                // Force the bitmap to load right now so we can dispose the stream.
                result.CacheOption = BitmapCacheOption.OnLoad;
                result.StreamSource = stream;
                result.EndInit();
                result.Freeze();
                return result;
            }
        }

        public static BitmapSource ToWpfBitmap(this Stream stream)
        {
            stream.Position = 0;
            BitmapImage result = new BitmapImage();
            result.BeginInit();
            // According to MSDN, "The default OnDemand cache option retains access to the stream until the image is needed."
            // Force the bitmap to load right now so we can dispose the stream.
            result.CacheOption = BitmapCacheOption.OnLoad;
            result.StreamSource = stream;
            result.EndInit();
            result.Freeze();
            stream.Dispose();
            return result;
        }

        static public BitmapSource LoadAsEmbeddedResourceImage(this string name)
            => Assembly.GetExecutingAssembly()
                       .GetManifestResourceStream(name)
                       .ToWpfBitmap();
    }

    public class MemberInfoImages
    {
        static public Dictionary<MemberType, BitmapSource> Dark = new Dictionary<MemberType, BitmapSource>
        {
            { MemberType.Interface,  "CodeMap.Resources.icons.dark.interface.png".LoadAsEmbeddedResourceImage() },
            { MemberType.Property,   "CodeMap.Resources.icons.dark.property.png".LoadAsEmbeddedResourceImage()},
            { MemberType.Field,      "CodeMap.Resources.icons.dark.field.png".LoadAsEmbeddedResourceImage()},
            { MemberType.Class,      "CodeMap.Resources.icons.dark.class.png".LoadAsEmbeddedResourceImage()},
            { MemberType.Struct,     "CodeMap.Resources.icons.dark.class.png".LoadAsEmbeddedResourceImage()},
            { MemberType.Type,       "CodeMap.Resources.icons.dark.class.png".LoadAsEmbeddedResourceImage()},
            { MemberType.Method,     "CodeMap.Resources.icons.dark.method.png".LoadAsEmbeddedResourceImage()},
            { MemberType.Constructor,"CodeMap.Resources.icons.dark.methodconstructor.png".LoadAsEmbeddedResourceImage()},
            { MemberType.Region,     "CodeMap.Resources.icons.dark.public.png".LoadAsEmbeddedResourceImage()}
        };

        static public Dictionary<MemberType, BitmapSource> Light = new Dictionary<MemberType, BitmapSource>
        {
            { MemberType.Interface,  "CodeMap.Resources.icons.light.interface.png".LoadAsEmbeddedResourceImage() },
            { MemberType.Property,   "CodeMap.Resources.icons.light.property.png".LoadAsEmbeddedResourceImage()},
            { MemberType.Field,      "CodeMap.Resources.icons.light.field.png".LoadAsEmbeddedResourceImage()},
            { MemberType.Class,      "CodeMap.Resources.icons.light.class.png".LoadAsEmbeddedResourceImage()},
            { MemberType.Struct,     "CodeMap.Resources.icons.light.class.png".LoadAsEmbeddedResourceImage()},
            { MemberType.Type,       "CodeMap.Resources.icons.light.class.png".LoadAsEmbeddedResourceImage()},
            { MemberType.Method,     "CodeMap.Resources.icons.light.method.png".LoadAsEmbeddedResourceImage()},
            { MemberType.Constructor,"CodeMap.Resources.icons.light.methodconstructor.png".LoadAsEmbeddedResourceImage()},
            { MemberType.Region,     "CodeMap.Resources.icons.light.public.png".LoadAsEmbeddedResourceImage()}
        };
    }

    public enum MemberType
    {
        Interface,
        Class,
        Struct,
        Type,
        Constructor,
        Method,
        Property,
        Field,
        Region,
    }

    static class BookmarksStore
    {
        static Dictionary<string, Dictionary<string, string>> Items = new Dictionary<string, Dictionary<string, string>>();

        public static void Purge(string documentName, IEnumerable<string> validBookmarks)
        {
            lock (Items)
            {
                if (Items.ContainsKey(documentName))
                {
                    var oldBookmarks = Items[documentName].Keys.Where(x => !validBookmarks.Contains(x)).ToList();

                    oldBookmarks
                        .ForEach(y => Items[documentName].Remove(y));
                }

                Items.Keys.ToList()
                    .ForEach(doc =>
                    {
                        if (!File.Exists(doc))
                            Items.Remove(doc);
                        else if (!Items[doc].Any())
                            Items.Remove(doc);
                    });

                Save();
            }
        }

        public static void Store(string documentName, string bookmarkId, string bookmarkName)
        {
            lock (Items)
            {
                if (!string.IsNullOrEmpty(documentName))
                {
                    if (!Items.ContainsKey(documentName))
                        Items[documentName] = new Dictionary<string, string>();

                    if (string.IsNullOrEmpty(bookmarkName) || bookmarkName == "None")
                        Items[documentName].Remove(bookmarkId);
                    else
                        Items[documentName][bookmarkId] = bookmarkName;
                }
            }
        }

        public static string Read(string documentName, string location)
        {
            lock (Items)
            {
                if (!string.IsNullOrEmpty(documentName) && Items.ContainsKey(documentName))
                    if (Items[documentName].ContainsKey(location))
                        return Items[documentName][location];
                return null;
            }
        }

        public static void Clear(string documentName)
        {
            lock (Items)
            {
                if (!string.IsNullOrEmpty(documentName) && Items.ContainsKey(documentName))
                {
                    Items.Remove(documentName);
                }
            }
        }

        static string bookmarksFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodeMap.2022.VSIX", "codemap.vs.json");

        public static void Load()
        {
            try
            {
                if (File.Exists(bookmarksFile))
                {
                    var json = File.ReadAllText(bookmarksFile);
                    Items = JObject.Parse(json).ToObject<Dictionary<string, Dictionary<string, string>>>();
                }
            }
            catch { }
        }

        public static void Save()
        {
            lock (Items)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(bookmarksFile));
                    File.WriteAllText(bookmarksFile, JObject.FromObject(Items).ToString());
                }
                catch { }
            }
        }
    }

    public class MemberInfo : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string Name = "";
        public string ParentPath = "";

        public string Id
        {
            get
            {
                var path = ParentPath;
                switch (MemberType)
                {
                    case MemberType.Interface:
                    case MemberType.Class:
                    case MemberType.Struct:
                    case MemberType.Type:
                        {
                            if (!ParentPath.Any())
                                return Title; // global namespace
                            else
                            if (ParentPath.Count(x => x == '.') == 0 &&  // not nested
                                ParentPath.Count(x => x == '|') == 0)    //
                                return $"{ParentPath}|{Title}"; // ParentPath is a namespace
                            else
                                return $"{ParentPath}.{Title}";
                        }

                    case MemberType.Constructor:
                    case MemberType.Method:
                        return $"{ParentPath}.{Content}({MethodParameters})";

                    case MemberType.Property:
                    case MemberType.Field:
                        return $"{ParentPath}.{Content}";

                    case MemberType.Region:
                        return $"{ParentPath}.{MemberContext}";

                    default:
                        return Guid.NewGuid().ToString(); // giving up here
                }
            }
        }

        public int Line { set; get; } = -1;
        public int EndLine { set; get; } = -1;
        public int Column { set; get; } = -1;
        public string Content { set; get; } = "";
        public string ContentType { set; get; } = "";
        public string MemberContext { set; get; } = "";
        public string Title { set; get; } = "";

        public string NestingLevel { set; get; }
        public string MethodParameters { set; get; } = null;

        string colorContext;
        public string ColorContext { get => colorContext; set { colorContext = value; OnPropertyChanged(nameof(ColorContext)); } }
        public bool IsPublic { set; get; } = true;

        public MemberType MemberType { set; get; }

        public List<MemberInfo> Children = new List<MemberInfo>();

        public BitmapSource TypeIcon => (ExtensionHost.IsDarkTheme ? MemberInfoImages.Dark : MemberInfoImages.Light)[MemberType];

        public BitmapSource AccessType => (IsPublic || MemberType == MemberType.Interface || MemberType == MemberType.Class)
                                           ? AppImages.PublicOverlay
                                             : AppImages.PrivateOverlay;

        public override string ToString()
        {
            return $"{Content} {MemberContext}";
        }
    }

    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public Visibility TrueValue { get; set; }
        public Visibility FalseValue { get; set; }

        public BoolToVisibilityConverter()
        {
            // set defaults
            TrueValue = Visibility.Visible;
            FalseValue = Visibility.Collapsed;
        }

        public object Convert(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            if (!(value is bool))
                return null;
            return (bool)value ? TrueValue : FalseValue;
        }

        public object ConvertBack(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            if (Equals(value, TrueValue))
                return true;
            if (Equals(value, FalseValue))
                return false;
            return null;
        }
    }

    public sealed class TextLengthToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            if (!(value is string))
                return null;
            return string.IsNullOrWhiteSpace((string)value) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public sealed class IdeFontSizeToToolWindowsFontSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // IDE default font size is strangely larger than what items presenters like Solution Explorer
            // use at runtime (e.g. 12 vs 12.5).
            // So let's make it smaller by 5%
            if (value is double)
                return ((double)value) * 0.95;
            else
                return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class BookmarkToBackgroundConverter : IValueConverter
    {
        public SolidColorBrush Bookmark1 { get; set; } = LightYellow;
        public SolidColorBrush Bookmark2 { get; set; } = Brushes.LightBlue;
        public SolidColorBrush Bookmark3 { get; set; } = Brushes.LightGreen;
        public SolidColorBrush Bookmark4 { get; set; } = Brushes.LightPink;

        static SolidColorBrush LightYellow = new SolidColorBrush(Color.FromRgb(245, 204, 132));
        static SolidColorBrush NoBookmark = new SolidColorBrush(Color.FromArgb(0, 255, 255, 255));

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            try
            {
                if (value is string)
                {
                    var name = (value as string);
                    switch (name)
                    {
                        case "bookmark1": return Bookmark1;
                        case "bookmark2": return Bookmark2;
                        case "bookmark3": return Bookmark3;
                        case "bookmark4": return Bookmark4;
                        default: break;
                    }
                }
            }
            catch { }
            return NoBookmark;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BookmarkToForegroundConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var defaultForeground = values[0];
            var bookmarkForeground = Brushes.DarkBlue;

            if (values.Length > 1 && !string.IsNullOrEmpty(values[1] as string))
            {
                return bookmarkForeground;
            }
            else
            {
                return defaultForeground;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BookmarkToForegroundConverter1 : IValueConverter
    {
        public object Context { get; set; }

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            try
            {
                if (value is string && (value as string) != "")
                    return Colors.DarkGray;
                else
                    return (VsBrushes.WindowTextKey as SolidColorBrush).Color;
            }
            catch { }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}