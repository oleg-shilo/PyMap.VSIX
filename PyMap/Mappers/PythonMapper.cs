using System.Collections.Generic;
using System.IO;
using System.Linq;
using PyMap;

class PythonMapper
{
    public static IEnumerable<MemberInfo> Generate(string file)
    {
        var map = new List<MemberInfo>();
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
                    info.Content = line.Substring("@".Length).Trim().Deflate();
                    if (info.Content == "property")
                    {
                        i++;
                        line = code[i].TrimStart();
                        info.Line = i;
                        info.Content = line.Substring("def ".Length).TrimEnd().Split('(').FirstOrDefault();
                    }
                    info.MemberType = MemberType.Property;
                }
                else if (line.StartsWith("class"))
                {
                    info.MemberContext = ": class";
                    info.MemberType = MemberType.Class;
                    info.Title = line.Substring("class ".Length).TrimEnd();
                }
                else
                {
                    info.MemberContext = "";
                    info.MemberType = MemberType.Method;
                    info.Content = line.Substring("def ".Length).TrimEnd().TrimEnd(':');
                }

                map.Add(info);
            }
        }
        return map;
    }
}