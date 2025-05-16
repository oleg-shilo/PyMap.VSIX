using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using CodeMap;
using Esprima;
using Esprima.Ast;

static class JavaScriptMapper
{
    public static IEnumerable<MemberInfo> Generate(string file, bool showMethodParams)
    {
        return Generate(File.ReadAllLines(file), showMethodParams).Structure();
        // return GenerateWithEsPrima(file, showMethodParams);
    }

    // window.setRightPanelWidth = function (newLeftWidth) {
    static Regex winFunc = new Regex(@"window\.(\w+)\s*=\s*function\b", RegexOptions.Compiled);

    // duplicateSelectionOrLine: function (cm) {
    static Regex nakedFunc = new Regex(@"(\w+)\s*:\s*function\b", RegexOptions.Compiled);

    // function onMouseUp() {
    static Regex pureFunc = new Regex(@"function\s+(\w+)\s*\(", RegexOptions.Compiled);

    // class methods: e.g., myMethod(param) {
    static Regex classMethod = new Regex(@"^(\w+)\s*\(([^)]*)\)\s*{", RegexOptions.Compiled);

    // variable function expressions: var bar = function(...) {
    static Regex varFuncExpr = new Regex(@"^(?:var|let|const)\s+(\w+)\s*=\s*function\s*\(([^)]*)\)", RegexOptions.Compiled);

    // arrow functions: var baz = (...) => {
    static Regex varArrowFunc = new Regex(@"^(?:var|let|const)\s+(\w+)\s*=\s*\(([^)]*)\)\s*=>", RegexOptions.Compiled);

    // single-parameter arrow functions: var baz = x => {
    static Regex varArrowFuncSingle = new Regex(@"^(?:var|let|const)\s+(\w+)\s*=\s*([a-zA-Z_$][\w$]*)\s*=>", RegexOptions.Compiled);

    // window.property.property = function() {
    static Regex winPropFunc = new Regex(@"window\.([\w\.]+)\s*=\s*function\s*\(([^)]*)\)", RegexOptions.Compiled);

    // TypeScript class method: read_all_lines(file: string): string[] {
    // static Regex tsClassMethod = new Regex(@"^(\w+)\s*\(([^)]*)\)\s*:\s*[\w\[\]\<\>\|]+(\s*\[\])?\s*{", RegexOptions.Compiled);
    static Regex tsClassMethod = new Regex(@"^(?:\s*(?:public|private|protected|static|async)\s+)*(\w+)\s*\(([^)]*)\)\s*:\s*[\w\[\]\<\>\|]+(\s*\[\])?\s*{",
    RegexOptions.Compiled);

    static string ParentLineOf(string[] code, int childIndex)
    {
        // calculate child line indent and find nearest line with a lesser indent above the child line
        var childIndent = code[childIndex].GetIndent();

        for (int i = childIndex - 1; i >= 0; i--)
        {
            if (code[i].Length > 0)
            {
                var currentIndent = code[i].GetIndent();
                if (currentIndent < childIndent)
                    return code[i];
            }
        }
        return null;
    }

    public static IEnumerable<MemberInfo> Generate(string[] code, bool showMethodParams)
    {
        var map = new List<MemberInfo>();
        // List of JS control flow keywords to exclude
        var controlKeywords = new HashSet<string> { "if", "for", "while", "switch", "catch", "with", "else", "do", "try" };

        for (int i = 0; i < code.Length; i++)
        {
            var line = code[i].TrimStart();

            var name = "";
            var invokeParams = "";

            Match match;
            if ((match = winFunc.Match(line)).Success)
            {
                name = match.Groups[1].Value;
                invokeParams = line.Replace("window.", "").Trim().Substring(match.Groups[1].Length).Replace("function", "").Trim(" {:=".ToCharArray());
            }
            else if ((match = winPropFunc.Match(line)).Success)
            {
                name = match.Groups[1].Value;
                invokeParams = "(" + match.Groups[2].Value + ")";
            }
            else if ((match = pureFunc.Match(line)).Success)
            {
                name = match.Groups[1].Value;
                invokeParams = line.Replace("function", "").Trim().Substring(match.Groups[1].Length).Trim(" {:".ToCharArray());
            }
            else if ((match = nakedFunc.Match(line)).Success)
            {
                name = match.Groups[1].Value;
                invokeParams = line.Substring(match.Groups[1].Length).Replace("function", "").Trim(" {:".ToCharArray());
            }
            // Exclude control flow keywords before matching class methods
            else if (!controlKeywords.Contains(line.Split(" (".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "") &&
                     (match = classMethod.Match(line)).Success)
            {
                name = match.Groups[1].Value;
                invokeParams = "(" + match.Groups[2].Value + ")";
            }
            else if ((match = tsClassMethod.Match(line)).Success)
            {
                name = match.Groups[1].Value;
                invokeParams = "(" + match.Groups[2].Value + ")";
            }
            else if ((match = varFuncExpr.Match(line)).Success)
            {
                name = match.Groups[1].Value;
                invokeParams = "(" + match.Groups[2].Value + ")";
            }
            else if ((match = varArrowFunc.Match(line)).Success)
            {
                name = match.Groups[1].Value;
                invokeParams = "(" + match.Groups[2].Value + ")";
            }
            else if ((match = varArrowFuncSingle.Match(line)).Success)
            {
                name = match.Groups[1].Value;
                invokeParams = "(" + match.Groups[2].Value + ")";
            }
            else
            {
                continue;
            }

            // name = name.Split('.').Last();

            // var contentIndent = new string(' ', (code[i].Length - line.Length));
            var parent = ParentLineOf(code, i);
            if (parent != null)
            {
                name = parent.Split('=').First()
                    .Replace("window.", "")
                    .Replace("export", "")
                    .Replace("public", "")
                    .Replace("static", "")
                    .Replace("class ", "")
                    .Replace("{", "")
                    .Replace("(", "")
                    .Trim() + "." + name;
            }

            var info = new MemberInfo();
            info.Line = i;

            info.ParentPath = "";
            info.Name = name;
            info.MemberContext = "";
            info.MemberType = MemberType.Method;

            info.Content = showMethodParams ?
                name + invokeParams :
                name + "(...)";

            map.Add(info);
        }
        return map;
    }

    public static IEnumerable<MemberInfo> Structure(this IEnumerable<MemberInfo> map)
    {
        var all = map.ToList();
        var roots = new List<MemberInfo>();

        foreach (var item in all)
        {
            var content = item.Name;
            var lastDot = content.LastIndexOf('.');
            if (lastDot > 0)
            {
                var parentPath = content.Substring(0, lastDot);

                // find or create parentInfo in roots
                var parent = roots.FirstOrDefault(x => x.Name == parentPath);
                if (parent == null)
                    roots.Add(parent = new MemberInfo
                    {
                        Name = parentPath,
                        MemberType = MemberType.Class,
                        Content = parentPath,
                        ParentPath = "",
                        Line = item.Line
                    });

                parent.Children.Add(item);
                item.ParentPath = parentPath;
                item.Content = item.Content.Substring(parentPath.Length).TrimStart('.');
                item.ContentType = "    "; // will create the indent in the tree for children display
                continue;
            }
            else
                roots.Add(item); // If no parent found, it's a root node
        }
        return roots;
    }

    public static IEnumerable<MemberInfo> GenerateWithEsPrima(string file, bool showMethodParams)
    {
        var map = new List<MemberInfo>();
        var code = File.ReadAllText(file);
        var parserOptions = new ParserOptions();
        var parser = new JavaScriptParser();
        var program = parser.ParseScript(code);
        var members = new List<JsMemberInfo>();
        ExtractMembers(program.Body, members);
        foreach (var member in members)
        {
            var info = new MemberInfo
            {
                Line = member.LineNumber - 1,
                MemberType = member.Type == "function" ? MemberType.Method : MemberType.Property,
                Content = showMethodParams ? $"{member.Name}(...)" : member.Name,
                Title = member.Name
            };
            map.Add(info);
        }
        return map;
    }

    class JsMemberInfo
    {
        public string Name { get; set; }
        public string Type { get; set; } // function or property
        public int LineNumber { get; set; }

        public override string ToString() =>
            $"{Type} '{Name}' at line {LineNumber}";
    }

    static void ExtractMembers(NodeList<Statement> statements, List<JsMemberInfo> members)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case FunctionDeclaration func:
                    members.Add(new JsMemberInfo
                    {
                        Name = func.Id?.Name ?? "<anonymous>",
                        Type = "function",
                        LineNumber = func.Location.Start.Line
                    });
                    break;

                case VariableDeclaration varDecl:
                    foreach (var decl in varDecl.Declarations)
                    {
                        // Check if initializer is a function or not
                        string type;
                        if (decl.Init is FunctionExpression || decl.Init is ArrowFunctionExpression)
                        {
                            type = "function";
                        }
                        else if (decl.Init is ObjectExpression)
                        {
                            type = "object";
                        }
                        else
                        {
                            type = "property";
                        }

                        members.Add(new JsMemberInfo
                        {
                            Name = decl.Id.ToString(),
                            Type = type,
                            LineNumber = decl.Location.Start.Line
                        });

                        // Handle object literal properties
                        if (decl.Init is ObjectExpression objInit)
                        {
                            foreach (var prop in objInit.Properties)
                            {
                                if (prop is Property p)
                                {
                                    members.Add(new JsMemberInfo
                                    {
                                        Name = p.Key.ToString(),
                                        Type = p.Value is FunctionExpression ? "function" : "property",
                                        LineNumber = p.Location.Start.Line
                                    });
                                }
                            }
                        }
                    }
                    break;
            }
        }
    }
}