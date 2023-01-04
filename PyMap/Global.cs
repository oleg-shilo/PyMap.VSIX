using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Package;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// using PyMap.Resources.icons.dark;

namespace PyMap
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
            { MemberType.Interface,  "PyMap.Resources.icons.dark.interface.png".LoadAsEmbeddedResourceImage() },
            { MemberType.Property,   "PyMap.Resources.icons.dark.property.png".LoadAsEmbeddedResourceImage()},
            { MemberType.Field,      "PyMap.Resources.icons.dark.field.png".LoadAsEmbeddedResourceImage()},
            { MemberType.Class,      "PyMap.Resources.icons.dark.class.png".LoadAsEmbeddedResourceImage()},
            { MemberType.Method,     "PyMap.Resources.icons.dark.method.png".LoadAsEmbeddedResourceImage()},
            { MemberType.Constructor,"PyMap.Resources.icons.dark.methodconstructor.png".LoadAsEmbeddedResourceImage()}
        };

        static public Dictionary<MemberType, BitmapSource> Light = new Dictionary<MemberType, BitmapSource>
        {
            { MemberType.Interface,  "PyMap.Resources.icons.light.interface.png".LoadAsEmbeddedResourceImage() },
            { MemberType.Property,   "PyMap.Resources.icons.light.property.png".LoadAsEmbeddedResourceImage()},
            { MemberType.Field,      "PyMap.Resources.icons.light.field.png".LoadAsEmbeddedResourceImage()},
            { MemberType.Class,      "PyMap.Resources.icons.light.class.png".LoadAsEmbeddedResourceImage()},
            { MemberType.Method,     "PyMap.Resources.icons.light.method.png".LoadAsEmbeddedResourceImage()},
            { MemberType.Constructor,"PyMap.Resources.icons.light.methodconstructor.png".LoadAsEmbeddedResourceImage()}
        };
    }

    public enum MemberType
    {
        Interface,
        Class,
        Constructor,
        Method,
        Property,
        Field,
    }

    public class MemberInfo
    {
        public int Line { set; get; } = -1;
        public int Column { set; get; } = -1;
        public string Content { set; get; } = "";
        public string ContentType { set; get; } = "";
        public string MemberContext { set; get; } = "";
        public string Title { set; get; } = "";
        public bool IsPublic { set; get; } = true;

        public MemberType MemberType { set; get; }

        public MemberInfo[] Children;

        public BitmapSource TypeIcon => (ExtensionHost.IsDarkTheme ? MemberInfoImages.Dark : MemberInfoImages.Light)[MemberType];
        public BitmapSource AccessType => (IsPublic || MemberType == MemberType.Interface || MemberType == MemberType.Class)
                                            ? null
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

    public class KewordColorConverter : IValueConverter
    {
        static SolidColorBrush lightBlue = new SolidColorBrush(Color.FromRgb(59, 138, 210));

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var brush = value as SolidColorBrush;
            if (brush != null)
            {
                if (brush.Color.Brightness() < 50)
                    return Brushes.LightSkyBlue;
                //return lightBlue;
                else
                    return Brushes.Blue;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}