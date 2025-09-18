using static System.Environment;
using System.Runtime.CompilerServices;
using Esprima.Ast;

namespace CodeMap.Test
{
    static public class TestExtensions
    {
        public static string ToTestInputFile(this string text, [CallerMemberName] string callerMethodName = "")
        {
            var file = Path.GetFullPath($"unit-test-{callerMethodName}.cs");
            File.WriteAllText(file, text);
            return file;
        }

        public static string ToView(this IEnumerable<MemberInfo> items)
        {
            var subTrees = items.Select(i => i.ToView()).ToList();

            return string.Join(Environment.NewLine, subTrees);
        }

        public static string ToView(this MemberInfo info)
        {
            var indent = info.NestingIndent;
            if (info.MemberType != MemberType.Interface &&
                info.MemberType != MemberType.Type &&
                info.MemberType != MemberType.Class &&
                info.MemberType != MemberType.Struct)
            {
                indent += MemberInfo.SingleIndent;
            }

            var type = $"{indent}{(info.Title.Any() ?
                    info.Title :
                    info.Content.Any() ?
                       info.Content :
                       info.MemberContext)}";
            return type;
        }
    }
}