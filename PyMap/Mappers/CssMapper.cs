using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using CodeMap;

class CssMapper
{
    public static IEnumerable<MemberInfo> Generate(string file, bool showMethodParams)
    {
        var map = new List<MemberInfo>();
        var code = File.ReadAllLines(file);

        for (int i = 0; i < code.Length; i++)
        {
            var line = code[i].TrimStart();

            if (line.TrimEnd().EndsWith("{"))
            {
                var info = new MemberInfo();
                info.Line = i;
                info.MemberContext = "";
                info.MemberType = MemberType.Field;
                info.Content = line.Trim().TrimEnd('{').Trim();

                if (info.Content.Length > 25)
                    info.Content = info.Content.Substring(0, 24) + "...";

                map.Add(info);
            }
        }
        return map;
    }
}