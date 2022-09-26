using System.Collections.Generic;
using System.IO;
using System.Linq;
using PyMap;

class PythonMapper
{
    public static IEnumerable<MemberInfo> Generate(string file)
    {
        var result = new List<MemberInfo>();
        var code = File.ReadAllLines(file);

        for (int i = 0; i < code.Length; i++)
        {
            var line = code[i].TrimStart();

            if (line.StartsWithAny("def ", "class ", "@"))
            {
                var info = new MemberInfo();
                info.Line = i;
                var contentIndent = new string(' ', (code[i].Length - line.Length));

                if (line.StartsWith("@"))
                {
                    info.ContentType = "@";
                    info.Content = contentIndent + line.Substring("@".Length).Trim();
                }
                else if (line.StartsWith("class"))
                {
                    if (result.Any())
                        result.Add(new MemberInfo { Line = -1 });
                    info.MemberContext = ": class";
                    info.MemberType = MemberType.Class;
                    info.Content = contentIndent + line.Substring("class ".Length).TrimEnd();
                }
                else
                {
                    info.MemberContext = "";
                    info.MemberType = MemberType.Method;
                    info.ContentType = "def";
                    info.Content = line.Substring("def ".Length).TrimEnd();
                }

                result.Add(info);
            }
        }
        return result;
    }
}