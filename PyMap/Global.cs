using System;
using System.Linq;
using System.Collections.Generic;
using EnvDTE80;
using EnvDTE;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Text;
using System.Windows.Data;
using System.Windows.Media;

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

        public static bool IsPythonFile(this string file)
        {
            return file.EndsWith(".py", StringComparison.InvariantCultureIgnoreCase) || file.EndsWith(".pyw", StringComparison.InvariantCultureIgnoreCase);
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

    public class MemberInfo
    {
        public int Line { set; get; } = -1;
        public string Content { set; get; } = "";
        public string ContentType { set; get; } = "";
        public string ContentIndent { set; get; } = "";

        public override string ToString()
        {
            return $"{ContentIndent} {ContentType} {Content}";
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