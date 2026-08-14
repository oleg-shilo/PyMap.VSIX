using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CodeMap;

static class TypeScriptMapper
{
    public static IEnumerable<MemberInfo> Generate(string file, bool showMethodParams)
    {
        return Generate(File.ReadAllLines(file), showMethodParams).StructureTs();
        // future: consider a proper TypeScript parser integration
    }



    static bool IsExported(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;
        return Regex.IsMatch(line, "\\bexport\\b");
    }

    static bool IsMemberPrivate(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;
        return Regex.IsMatch(line, "\\b(private|protected)\\b");
    }

    // TypeScript-specific Structure implementation to avoid splitting on dots inside
    // TypeScript type annotations (e.g., "UserCard: React.FC<Props>").
    public static IEnumerable<MemberInfo> StructureTs(this IEnumerable<MemberInfo> map)
    {
        var all = map.ToList();
        var roots = new List<MemberInfo>();
        // Build a lookup of names to original items for accurate parent matching
        var nameLookup = all.ToDictionary(x => $"{x.Name}:{x.Line}", x => x);

        foreach (var item in all)
        {
            var content = item.Name;
            // Try to find a parent by scanning dots from the end and checking for an existing name
            int scanPos = content.LastIndexOf('.');
            bool attached = false;
            while (scanPos > 0)
            {
                var candidateParentName = content.Substring(0, scanPos);
                var possibleParents = nameLookup.Keys.Where(k => k.StartsWith($"{candidateParentName}:"));

                var found = false;
                foreach (var possibleParentKey in possibleParents)
                {
                    var parent = nameLookup[possibleParentKey];
                    parent.Children.Add(item);
                    item.ParentPath = candidateParentName;
                    // Use the declared Name to derive the short display content safely.
                    // If Name is a dotted path like "Parent.child", prefer the substring after the last parent dot.
                    var dottedPrefix = candidateParentName + ".";
                    if (item.Name.StartsWith(dottedPrefix))
                    {
                        // preserve any parameter suffix already captured in Content (e.g., "name(arg: T)")
                        var suffix = "";
                        var parenIdx = item.Content.IndexOf('(');
                        if (parenIdx >= 0)
                            suffix = item.Content.Substring(parenIdx);
                        item.Content = item.Name.Substring(dottedPrefix.Length) + suffix;
                    }
                    else
                    {
                        // fallback: preserve existing content but trim leading dots
                        item.Content = item.Content.TrimStart('.');
                    }
                    item.ContentType = "    ";
                    attached = true;
                    found = true;
                    break;
                }
                if (found)
                    break;
                scanPos = content.LastIndexOf('.', scanPos - 1);
            }

            if (!attached)
            {
                // If this root item has a TypeScript-style annotation (':' in the name)
                // and is a top-level property (const/let component), preserve the full
                // annotated name as the display content so UI shows e.g. "UserCard: React.FC<...>".
                if (item.Name.IndexOf(':') != -1 && item.MemberType == MemberType.Property)
                    item.Content = item.Name;

                roots.Add(item);
            }
        }

        return roots;
    }


    // Find the line index where a block starting at startIndex ends by matching braces.
    static int FindBlockEnd(string[] code, int startIndex)
    {
        int braceDepth = 0;
        bool started = false;
        for (int i = startIndex; i < code.Length; i++)
        {
            var line = code[i];
            if (line.TrimStart().StartsWith("//"))
                continue;

            if (line.Contains("{"))
            {
                started = true;
                braceDepth += line.Count(c => c == '{');
            }
            if (started && line.Contains("}"))
            {
                braceDepth -= line.Count(c => c == '}');
                if (braceDepth <= 0)
                    return i;
            }
        }
        return startIndex;
    }

    // Find the approximate end line for an expression-bodied arrow function or JSX expression
    static int FindExpressionBodyEnd(string[] code, int declarationIndex)
    {
        if (declarationIndex < 0 || declarationIndex >= code.Length)
            return declarationIndex;

        // locate '=>' in the declaration or next few lines
        int arrowLine = -1;
        int arrowPos = -1;
        for (int j = declarationIndex; j < Math.Min(code.Length, declarationIndex + 6); j++)
        {
            var idx = code[j].IndexOf("=>");
            if (idx >= 0)
            {
                arrowLine = j;
                arrowPos = idx + 2;
                break;
            }
        }
        if (arrowLine == -1)
            return declarationIndex;

        // find first non-space char after '=>'
        int lineIdx = arrowLine;
        int charPos = arrowPos;
        while (lineIdx < code.Length)
        {
            var line = code[lineIdx];
            for (int k = charPos; k < line.Length; k++)
            {
                var c = line[k];
                if (char.IsWhiteSpace(c))
                    continue;

                // parentheses-based expression
                if (c == '(')
                {
                    int depth = 0;
                    for (int ii = lineIdx; ii < code.Length; ii++)
                    {
                        var s = code[ii];
                        for (int kk = (ii == lineIdx ? k : 0); kk < s.Length; kk++)
                        {
                            if (s[kk] == '(') depth++;
                            else if (s[kk] == ')')
                            {
                                depth--;
                                if (depth == 0)
                                    return ii;
                            }
                        }
                    }
                    return code.Length - 1;
                }

                // JSX-like expression starting with '<'
                if (c == '<')
                {
                    int depth = 0;
                    bool inString = false;
                    for (int ii = lineIdx; ii < code.Length; ii++)
                    {
                        var s = code[ii];
                        for (int kk = (ii == lineIdx ? k : 0); kk < s.Length; kk++)
                        {
                            var ch = s[kk];
                            if (ch == '"' || ch == '\'')
                                inString = !inString;
                            if (inString)
                                continue;
                            if (ch == '<') depth++;
                            else if (ch == '>')
                            {
                                depth--;
                                if (depth == 0)
                                    return ii;
                            }
                        }
                    }
                    return code.Length - 1;
                }

                // expression is a single value; end at the declaration line
                return arrowLine;
            }
            lineIdx++;
            charPos = 0;
        }

        return declarationIndex;
    }



    // TypeScript-specific regexes (more permissive: generics, export default, async)
    // interface Foo { ... }
    static Regex interfaceRegex = new Regex(@"^\s*(?:export\s+)?(?:default\s+)?interface\s+([A-Za-z_][\w_]*)(?:\s*<[^>]+>)?\b", RegexOptions.Compiled);

    // type Foo = ...
    static Regex typeRegex = new Regex(@"^\s*(?:export\s+)?(?:default\s+)?type\s+([A-Za-z_][\w_]*)(?:\s*<[^>]+>)?\b", RegexOptions.Compiled);

    // enum Foo { ... }
    static Regex enumRegex = new Regex(@"^\s*(?:export\s+)?(?:default\s+)?enum\s+([A-Za-z_][\w_]*)(?:\s*<[^>]+>)?\b", RegexOptions.Compiled);

    // class Foo<T> { ... } optionally exported/default
    static Regex classRegex = new Regex(@"^\s*(?:export\s+)?(?:default\s+)?class\s+([A-Za-z_][\w_]*)(?:\s*<[^>]+>)?\b", RegexOptions.Compiled);

    // function foo<T>(...) or export function foo(...)
    static Regex functionRegex = new Regex(@"^(?:export\s+)?(?:default\s+)?(?:async\s+)?function\s+([A-Za-z_][\w_]*)(?:\s*<[^>]+>)?\s*\(([^)]*)\)", RegexOptions.Compiled);

    // class method with optional visibility/async/readonly/static and optional return type and generics
    static Regex classMethod = new Regex(@"^(?:\s*(?:public|private|protected|static|async|readonly)\s+)*([A-Za-z_][\w_]*)(?:\s*<[^>]+>)?\s*\(([^)]*)\)\s*(?::\s*[^({]+)?\s*{", RegexOptions.Compiled);

    // var foo = function(...) {
    static Regex varFuncExpr = new Regex(@"^(?:export\s+)?(?:var|let|const)\s+(?:async\s+)?([A-Za-z_][\w_]*)\s*=\s*function\s*\(([^)]*)\)", RegexOptions.Compiled);

    // var foo = (...) => {   (handles export and async)
    // var foo = (...) => {   (handles export, async and optional TypeScript type annotation)
    static Regex varArrowFunc = new Regex(@"^(?:export\s+)?(?:var|let|const)\s+(?:async\s+)?([A-Za-z_][\w_]*)(?:\s*:\s*[^=]+)?\s*=\s*\(([^)]*)\)\s*=>", RegexOptions.Compiled);

    // var foo = x => {
    static Regex varArrowFuncSingle = new Regex(@"^(?:export\s+)?(?:var|let|const)\s+(?:async\s+)?([A-Za-z_][\w_]*)(?:\s*:\s*[^=]+)?\s*=\s*([A-Za-z_$][\w$]*)\s*=>", RegexOptions.Compiled);

    // class property arrow functions: increment = () => { } or increment = x => { }
    static Regex classPropArrow = new Regex(@"^(?:\s*(?:public|private|protected|readonly|static)\s+)*([A-Za-z_][\w_]*)\s*=\s*\(([^)]*)\)\s*=>", RegexOptions.Compiled);

    static Regex classPropArrowSingle = new Regex(@"^(?:\s*(?:public|private|protected|readonly|static)\s+)*([A-Za-z_][\w_]*)\s*=\s*([A-Za-z_$][\w$]*)\s*=>", RegexOptions.Compiled);

    // property: foo: Type; or foo?: Type; or readonly foo: Type = ...
    // Do not match method signatures (e.g., foo(): Type;) — ensure no '(' follows the name or optional '?'
    // Allow trailing comma for object literal properties.
    static Regex propertyRegex = new Regex(@"^(?:\s*(?:public|private|protected|readonly|static|export)\s+)*([A-Za-z_][\w_]*)(?:\?)?\s*(?!\()\s*[:?]\s*[^,;=\{]+[,;]?", RegexOptions.Compiled);

    // top-level const/let/var with optional type annotation
    static Regex topLevelVarRegex = new Regex(@"^(?:export\s+)?(?:const|let|var)\s+([A-Za-z_][\w_]*)(?:\s*:\s*[^=;]+)?\s*(?:=|;)", RegexOptions.Compiled);



    // Find the first 'return' line index within the function/block starting at parentIndex; -1 if not found
    static int FirstReturnIndexInFunction(string[] code, int parentIndex)
    {
        if (parentIndex < 0 || parentIndex >= code.Length)
            return -1;

        var decl = code[parentIndex].TrimStart();
        // only consider variable declarations that look like arrow functions or function expressions
        if (!(decl.StartsWith("const ") || decl.StartsWith("let ") || decl.StartsWith("var ") || decl.Contains("= function")))
            return -1;

        // quick check: must contain => or = function somewhere on the declaration or following lines
        if (!decl.Contains("=>") && !decl.Contains("= function"))
        {
            int limit = Math.Min(code.Length - 1, parentIndex + 3);
            bool foundArrow = false;
            for (int j = parentIndex; j <= limit; j++)
            {
                if (code[j].Contains("=>") || code[j].Contains("= function"))
                {
                    foundArrow = true;
                    break;
                }
            }
            if (!foundArrow)
                return -1;
        }

        // Detect concise arrow JSX returns (e.g., () => <div/> or () => (
        //   <div/>
        // ) ). If the declaration (or few following lines) contains '=>' and
        // the expression starts with '<' (JSX) then treat that line as the return index.
        int arrowLine = -1;
        for (int j = parentIndex; j < Math.Min(code.Length, parentIndex + 6); j++)
        {
            if (code[j].Contains("=>"))
            {
                arrowLine = j;
                // if the arrow and JSX are on same line
                var idx = code[j].IndexOf("=>");
                if (idx >= 0 && code[j].Substring(idx + 2).TrimStart().StartsWith("<"))
                    return j;
                break;
            }
        }
        if (arrowLine != -1)
        {
            // look for next non-empty, non-comment line after arrow
            int k = arrowLine + 1;
            while (k < code.Length && string.IsNullOrWhiteSpace(code[k])) k++;
            if (k < code.Length)
            {
                var next = code[k].TrimStart();
                if (next.StartsWith("<") || (next.StartsWith("(") && next.Contains("<")))
                    return k;
            }
        }

        int braceDepth = 0;
        bool started = false;
        for (int i = parentIndex; i < code.Length; i++)
        {
            var line = code[i];
            if (line.TrimStart().StartsWith("//"))
                continue;

            if (line.Contains("{"))
            {
                started = true;
                braceDepth += line.Count(c => c == '{');
            }
            if (started && line.Contains("return "))
                return i;

            if (started && line.Contains("}"))
            {
                braceDepth -= line.Count(c => c == '}');
                if (braceDepth <= 0)
                    break;
            }
        }

        return -1;
    }

    public static IEnumerable<MemberInfo> Generate(string[] code, bool showMethodParams)
    {
        var map = new List<MemberInfo>();

        // simple stack to track enclosing class/interface contexts along with their brace depth
        var ctxStack = new Stack<(string Name, MemberType Type, int Depth)>();
        // stack to track function scopes (we will ignore any declarations found inside these scopes)
        var funcStack = new Stack<int>();
        int braceDepth = 0;

        for (int i = 0; i < code.Length; i++)
        {
            var raw = code[i];
            var line = raw.Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // update brace depth after evaluating the line's declarations (we capture opening depth for contexts below)
            int opens = raw.Count(c => c == '{');
            int closes = raw.Count(c => c == '}');

            Match match;

            // If we are already inside a function scope (tracked by end-line indices), skip capturing declarations on this line.
            // First, drop any funcStack entries that have already ended before this line.
            while (funcStack.Count > 0 && i > funcStack.Peek())
                funcStack.Pop();

            if (funcStack.Count > 0 && i <= funcStack.Peek())
            {
                // update brace depth and pop contexts as usual, then skip
                braceDepth += opens - closes;
                while (ctxStack.Count > 0 && braceDepth < ctxStack.Peek().Depth)
                {
                    ctxStack.Pop();
                }
                // still inside a function expression/body: skip capturing
                continue;
            }

            // class declaration
            if ((match = classRegex.Match(line)).Success)
            {
                var name = match.Groups[1].Value;
                var info = new MemberInfo { Line = i, ParentPath = "", Name = name, MemberContext = "", MemberType = MemberType.Class, Content = name };
                info.IsPublic = IsExported(line);
                map.Add(info);
                // record context starting at current brace depth; ensure at least one depth so declarations
                // where the '{' is on the next line are tracked correctly
                ctxStack.Push((name, MemberType.Class, braceDepth + Math.Max(1, opens)));
            }

            // interface declaration
            else if ((match = interfaceRegex.Match(line)).Success)
            {
                var name = match.Groups[1].Value;
                var info = new MemberInfo { Line = i, ParentPath = "", Name = name, MemberContext = "", MemberType = MemberType.Interface, Content = name };
                info.IsPublic = IsExported(line);
                map.Add(info);
                // ensure at least one depth so interface bodies with opening brace on the next line are tracked
                ctxStack.Push((name, MemberType.Interface, braceDepth + Math.Max(1, opens)));
            }

            // enum declarations
            else if ((match = enumRegex.Match(line)).Success)
            {
                var name = match.Groups[1].Value;
                var info = new MemberInfo { Line = i, ParentPath = "", Name = name, MemberContext = "", MemberType = MemberType.Type, Content = name };
                info.IsPublic = IsExported(line);
                map.Add(info);
                // do not push enum as a context
            }

            // type alias declarations (record but do not push as context)
            else if ((match = typeRegex.Match(line)).Success)
            {
                var name = match.Groups[1].Value;
                var info = new MemberInfo { Line = i, ParentPath = "", Name = name, MemberContext = "", MemberType = MemberType.Type, Content = name };
                info.IsPublic = IsExported(line);
                map.Add(info);
            }

            // top-level function declarations (treated as methods)
            else if ((match = functionRegex.Match(line)).Success)
            {
                var name = match.Groups[1].Value;
                var parms = match.Groups[2].Value;
                var info = new MemberInfo { Line = i, MemberContext = "", MemberType = MemberType.Method };
                info.Name = name;
                if (ctxStack.Count > 0 && (ctxStack.Peek().Type == MemberType.Class || ctxStack.Peek().Type == MemberType.Interface))
                {
                    info.ParentPath = ctxStack.Peek().Name;
                    info.Name = info.ParentPath + "." + name;
                }
                else
                {
                    info.ParentPath = "";
                }
                info.MethodParameters = parms;
                info.Content = showMethodParams ? name + "(" + parms + ")" : name + "(...)";
                info.IsPublic = IsExported(line);
                map.Add(info);
                // mark function scope so we don't capture nested declarations inside its body
                var funcEnd = FindBlockEnd(code, i);
                funcStack.Push(funcEnd);
            }

            // top-level const/let/var treated as properties
            else if ((match = topLevelVarRegex.Match(line)).Success)
            {
                var name = match.Groups[1].Value;
                var info = new MemberInfo { Line = i, MemberContext = "", MemberType = MemberType.Property };
                if (ctxStack.Count > 0 && (ctxStack.Peek().Type == MemberType.Class || ctxStack.Peek().Type == MemberType.Interface))
                {
                    info.Name = ctxStack.Peek().Name + "." + name;
                    info.ParentPath = ctxStack.Peek().Name;
                }
                else
                {
                    info.Name = name;
                    info.ParentPath = "";
                }
                info.Content = name;
                info.IsPublic = IsExported(line);
                map.Add(info);
                // if this variable is a function expression or arrow function, mark its scope to avoid capturing inside it
                var rawLine = raw.TrimStart();
                if (rawLine.Contains("=>") || rawLine.Contains("= function"))
                {
                    // attempt to find the end line of the function/arrow expression
                    int endLine = i;
                    if (rawLine.Contains("{"))
                    {
                        endLine = FindBlockEnd(code, i);
                    }
                    else
                    {
                        var retIdx = FirstReturnIndexInFunction(code, i);
                        if (retIdx != -1)
                        {
                            endLine = FindExpressionBodyEnd(code, i);
                        }
                    }
                    funcStack.Push(endLine);
                }
            }

            // if inside a class or interface, look for simple method/property members
            if (ctxStack.Count > 0)
            {
                var ctx = ctxStack.Peek();
                // method inside class/interface
                if ((match = classMethod.Match(line)).Success)
                {
                    var mname = match.Groups[1].Value;
                    var parms = match.Groups[2].Value;
                    var info = new MemberInfo { Line = i, MemberContext = "", MemberType = mname == "constructor" ? MemberType.Constructor : MemberType.Method };
                    info.Name = ctx.Name + "." + mname;
                    info.ParentPath = ctx.Name;
                    info.MethodParameters = parms;
                    info.Content = showMethodParams ? mname + "(" + parms + ")" : mname + "(...)";
                    info.IsPublic = !IsMemberPrivate(line);
                    map.Add(info);
                    // entering a method body — don't capture nested declarations inside the method
                    var methodEnd = FindBlockEnd(code, i);
                    funcStack.Push(methodEnd);
                }
                else if ((match = propertyRegex.Match(line)).Success)
                {
                    var pname = match.Groups[1].Value;
                    var rawLine = raw.TrimStart();

                    // If the property's type is a function signature like: fn?: (arg: string) => void;
                    // treat it as a method and capture the parameter list.
                    if (rawLine.Contains("=>") || rawLine.Contains("function(") || rawLine.Contains("function ("))
                    {
                        int nameIdx = rawLine.IndexOf(pname, StringComparison.Ordinal);
                        if (nameIdx >= 0)
                        {
                            int pstart = rawLine.IndexOf('(', nameIdx + pname.Length);
                            if (pstart >= 0)
                            {
                                int pend = rawLine.IndexOf(')', pstart);
                                if (pend > pstart)
                                {
                                    var parms = rawLine.Substring(pstart + 1, pend - pstart - 1).Trim();
                                    var info = new MemberInfo { Line = i, MemberContext = "", MemberType = MemberType.Method };
                                    info.Name = ctx.Name + "." + pname;
                                    info.ParentPath = ctx.Name;
                                    info.MethodParameters = parms;
                                    info.Content = showMethodParams ? pname + "(" + parms + ")" : pname + "(...)";
                                    info.IsPublic = !IsMemberPrivate(line);
                                    map.Add(info);
                                    continue;
                                }
                            }
                        }
                    }

                    var infoProp = new MemberInfo { Line = i, MemberContext = "", MemberType = MemberType.Property };
                    infoProp.Name = ctx.Name + "." + pname;
                    infoProp.ParentPath = ctx.Name;
                    infoProp.Content = pname;
                    infoProp.IsPublic = !IsMemberPrivate(line);
                    map.Add(infoProp);
                }
            }

            // update brace depth and pop contexts that end
            braceDepth += opens - closes;
            while (ctxStack.Count > 0 && braceDepth < ctxStack.Peek().Depth)
            {
                ctxStack.Pop();
            }
        }

        return map;
    }

    // Note: Structure extension method is implemented in JavaScriptMapper to
    // avoid duplicate extension methods causing ambiguity. We rely on that
    // implementation here.
}