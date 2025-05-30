using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;
using System.Windows.Media;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using CodeMap;

static class Ide
{
    public static string GetActiveDocumentText()
    {
        try
        {
            var txtMgr = (IVsTextManager)PyMapPackage.GetService(typeof(SVsTextManager));
            IWpfTextView textView = txtMgr.GetTextView();

            var code = textView.TextBuffer.CurrentSnapshot.GetText();

            return code;
        }
        catch { }
        return null;
    }

    static public IWpfTextView GetTextView(this IVsTextManager txtMgr)
    {
        return txtMgr.GetViewHost().TextView;
    }

    static public IWpfTextViewHost GetViewHost(this IVsTextManager txtMgr)
    {
        object holder;
        Guid guidViewHost = DefGuidList.guidIWpfTextViewHost;
        txtMgr.GetUserData().GetData(ref guidViewHost, out holder);
        return (IWpfTextViewHost)holder;
    }

    public static IVsUserData GetUserData(this IVsTextManager txtMgr)
    {
        int mustHaveFocus = 1;//means true
        IVsTextView currentTextView;
        txtMgr.GetActiveView(mustHaveFocus, null, out currentTextView);

        if (currentTextView is IVsUserData)
            return currentTextView as IVsUserData;
        else
            throw new ApplicationException("No text view is currently open");
        // Console.WriteLine("No text view is currently open"); return;
    }
}

class CSharpMapper
{
    public static IEnumerable<MemberInfo> Generate(string file, bool showMethodParams)
    {
        string code;

        int lineOffset = 0;

        if (file.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
        {
            var className = Path.GetFileNameWithoutExtension(file).Replace("-", "_");
            var lines = File.ReadAllLines(file);
            var htmlLines = lines.TakeWhile(x => !x.TrimStart().StartsWith("@code {"));
            lineOffset = htmlLines.Count();
            code = string.Join(Environment.NewLine, lines.Skip(lineOffset).Take(lines.Count() - lineOffset)); // first and last are to be removed
            code = code.Replace("@code {", $"public class {className} {{");
        }
        else
            code = File.ReadAllText(file);

        if (string.IsNullOrEmpty(code))
            code = Ide.GetActiveDocumentText(); // if file is empty, try to get the active document text

        return GenerateForCode(code, showMethodParams, lineOffset);
    }

    public static IEnumerable<MemberInfo> GenerateForCode(string code, bool showMethodParams, int lineOffset = 0)
    {
        var map = new List<MemberInfo>();

        if (string.IsNullOrEmpty(code))
            return map;

        SyntaxTree tree = CSharpSyntaxTree.ParseText(code);

        var root = tree.GetRoot();

        var nodes = root.DescendantNodes();

        var types = nodes.OfType<TypeDeclarationSyntax>()
                         .OrderBy(x => x.FullSpan.End)
                         .ToArray();

        var globalMethods = nodes.OfType<GlobalStatementSyntax>()
                                .Where(x => x.Statement.Kind() == SyntaxKind.LocalFunctionStatement)
                                .OrderBy(x => x.FullSpan.End)
                                .ToArray();

        var regions = root
            .DescendantTrivia()
            .Where(x => x.IsKind(SyntaxKind.RegionDirectiveTrivia))
            .Select(x =>
                new MemberInfo
                {
                    ParentPath = "",
                    Line = x.GetLocation().GetLineSpan().StartLinePosition.Line + lineOffset,
                    EndLine = x.GetLocation().GetLineSpan().EndLinePosition.Line + lineOffset,
                    Column = x.GetLocation().GetLineSpan().StartLinePosition.Character,
                    Content = "",
                    MemberContext = "region: " + x.ToString().Replace("#region", "").Trim() + "",
                    ContentType = "",
                    IsPublic = true,
                    Children = new List<MemberInfo>(),
                    MemberType = MemberType.Region
                }).ToArray();

        if (globalMethods.Any())
        {
            MemberInfo parent;
            map.Add(parent = new MemberInfo
            {
                ParentPath = "",
                Line = 1,
                EndLine = 1,
                Column = 1,
                Title = "<global>",
                Children = new List<MemberInfo>(),
                MemberType = MemberType.Class,
                MemberContext = ""
            });

            var members = new List<MemberInfo>();
            foreach (var member in globalMethods)
            {
                var method = (member.Statement as LocalFunctionStatementSyntax);

                var paramList = showMethodParams ?
                    method.ParameterList.Parameters.ToString().Deflate() :
                    "...";

                members.Add(new MemberInfo
                {
                    ParentPath = member.GetParentPath(),
                    Line = method.GetLocation().GetLineSpan().StartLinePosition.Line + lineOffset,
                    EndLine = method.GetLocation().GetLineSpan().EndLinePosition.Line + lineOffset,
                    Column = method.GetLocation().GetLineSpan().StartLinePosition.Character,
                    Content = method.Identifier.Text,
                    MemberContext = "(" + paramList + ")",
                    ContentType = "    ",
                    Children = new List<MemberInfo>(),
                    IsPublic = true,
                    MemberType = MemberType.Method
                });
            }

            parent.Children = members.ToList();
        }

        //////////////////////////////////////////////

        foreach (EnumDeclarationSyntax type in nodes.OfType<EnumDeclarationSyntax>())
        {
            var parentType = type.Parent as BaseTypeDeclarationSyntax;

            map.Add(new MemberInfo
            {
                ParentPath = type.GetParentPath(),
                Line = type.GetLocation().GetLineSpan().StartLinePosition.Line + lineOffset,
                EndLine = type.GetLocation().GetLineSpan().EndLinePosition.Line + lineOffset,
                Column = type.GetLocation().GetLineSpan().StartLinePosition.Character,
                Title = type.Identifier.Text,
                MemberContext = ": enum",
                MemberType = MemberType.Type
            });
        }

        foreach (TypeDeclarationSyntax type in types)
        {
            var parent = new MemberInfo
            {
                ParentPath = type.GetParentPath(),
                Line = type.GetLocation().GetLineSpan().StartLinePosition.Line + lineOffset,
                EndLine = type.GetLocation().GetLineSpan().EndLinePosition.Line + lineOffset,
                Column = type.GetLocation().GetLineSpan().StartLinePosition.Character,
                Title = type.Identifier.Text,
                MemberType = MemberType.Type,
                Children = new List<MemberInfo>(),

                MemberContext = ": " + type.Kind().ToString().Replace("Declaration", "").ToLower()
            };

            map.Add(parent);

            if (parent.MemberContext == ": class")
            {
                parent.MemberType = MemberType.Class;
            }

            if (parent.MemberContext == ": interface")
            {
                parent.MemberType = MemberType.Interface;
            }

            if (parent.MemberContext == ": struct")
            {
                parent.MemberType = MemberType.Struct;
            }

            List<MemberInfo> members = new List<MemberInfo>();

            foreach (var member in type.ChildNodes().OfType<MemberDeclarationSyntax>())
            {
                MemberInfo info = null;

                if (member is MethodDeclarationSyntax)
                {
                    var method = (member as MethodDeclarationSyntax);

                    var paramList = method.ParameterList.Parameters.ToString().Deflate();

                    info = new MemberInfo
                    {
                        ParentPath = member.GetParentPath(),
                        MethodParameters = method.ParameterList.Parameters.ToString().Deflate(),
                        Line = method.GetLocation().GetLineSpan().StartLinePosition.Line + lineOffset,
                        EndLine = method.GetLocation().GetLineSpan().EndLinePosition.Line + lineOffset,
                        Column = method.GetLocation().GetLineSpan().StartLinePosition.Character,
                        Content = method.Identifier.Text,
                        MemberContext = "(" + (showMethodParams ? paramList : "...") + ")",
                        ContentType = "    ",
                        Children = new List<MemberInfo>(),
                        IsPublic = method.Modifiers.Any(x => x.ValueText == "public" || x.ValueText == "internal"),
                        MemberType = MemberType.Method
                    };
                }
                else if (member is ConstructorDeclarationSyntax)
                {
                    var method = (member as ConstructorDeclarationSyntax);

                    var paramList = method.ParameterList.Parameters.ToString().Deflate();

                    info = new MemberInfo
                    {
                        ParentPath = member.GetParentPath(),
                        Line = method.GetLocation().GetLineSpan().StartLinePosition.Line + lineOffset,
                        EndLine = method.GetLocation().GetLineSpan().EndLinePosition.Line + lineOffset,
                        MethodParameters = method.ParameterList.Parameters.ToString().Deflate(),
                        Column = method.GetLocation().GetLineSpan().StartLinePosition.Character,
                        Content = method.Identifier.Text,
                        IsPublic = method.Modifiers.Any(x => x.ValueText == "public" || x.ValueText == "internal"),
                        ContentType = "    ",
                        Children = new List<MemberInfo>(),
                        MemberContext = "(" + (showMethodParams ? paramList : "...") + ")",
                        MemberType = MemberType.Constructor
                    };
                }
                else if (member is PropertyDeclarationSyntax)
                {
                    var prop = (member as PropertyDeclarationSyntax);
                    info = new MemberInfo
                    {
                        ParentPath = member.GetParentPath(),
                        Line = prop.GetLocation().GetLineSpan().StartLinePosition.Line + lineOffset,
                        EndLine = prop.GetLocation().GetLineSpan().EndLinePosition.Line + lineOffset,
                        Column = prop.GetLocation().GetLineSpan().StartLinePosition.Character,
                        Content = prop.Identifier.ValueText,
                        ContentType = "    ",
                        IsPublic = prop.Modifiers.Any(x => x.ValueText == "public" || x.ValueText == "internal"),
                        MemberType = MemberType.Property
                    };
                }
                else if (member is FieldDeclarationSyntax)
                {
                    var field = (member as FieldDeclarationSyntax);

                    info = new MemberInfo
                    {
                        ParentPath = member.GetParentPath(),
                        Line = field.GetLocation().GetLineSpan().StartLinePosition.Line + lineOffset,
                        EndLine = field.GetLocation().GetLineSpan().EndLinePosition.Line + lineOffset,
                        Column = field.GetLocation().GetLineSpan().StartLinePosition.Character,
                        Content = field.Declaration.Variables.First().Identifier.Text,
                        ContentType = "    ",
                        IsPublic = field.Modifiers.Any(x => x.ValueText == "public" || x.ValueText == "internal"),
                        MemberType = MemberType.Field
                    };
                }

                if (info != null)
                {
                    if (!info.ParentPath.Any())
                        info.ParentPath = type.GetParentPath();
                    members.Add(info);
                }
            }

            // members = members.OrderBy(x => x.MemberType).ToList();

            // members.ForEach(x => x.Line += lineOffset);

            parent.Children = members.ToList();
        }

        foreach (var region in regions)
        {
            var inserted = false;

            foreach (var type in map.ToArray())
            {
                if (region.Line < type.Line)
                {
                    var index = map.IndexOf(type);
                    map.Insert(index, region);
                    inserted = true;
                    break;
                }
                else
                {
                    if (type.Children.Any() && region.Line < type.Children.OrderBy(x => x.Line).LastOrDefault().Line)
                    {
                        var index = type.Children.TakeWhile(x => x.Line < region.Line).Count();
                        type.Children.Insert(index, region);
                        inserted = true;
                        break;
                    }
                }
            }
            if (!inserted)
                map.Add(region);
        }

        return map;
    }
}

static class Extensions
{
    public static string GetParentPath(this SyntaxNode type)
    {
        var statements = new List<string>();

        var parent = type.Parent;

        while (parent != null)
        {
            if (parent is NamespaceDeclarationSyntax ns)
                statements.Add(ns.Name.ToString());

            if (parent is FileScopedNamespaceDeclarationSyntax fsns)
                statements.Add(fsns.Name.ToString());

            if (parent is TypeDeclarationSyntax ts)
                statements.Add(ts.Identifier.Text);
            // statements.Add(ts.ToString());

            parent = parent.Parent;
        }

        statements.Reverse();
        return string.Join(".", statements);
    }

    public static string Deflate(this string text)
           => string.Join(" ", text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()));
}