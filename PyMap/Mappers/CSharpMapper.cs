using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PyMap;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;

class CSharpMapper
{
    public static IEnumerable<MemberInfo> Generate(string file)
    {
        var map = new List<MemberInfo>();

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
                    Line = x.GetLocation().GetLineSpan().StartLinePosition.Line + lineOffset,
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
                Line = 1,
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

                //var paramList = string.Join(", ", method.ParameterList.Parameters.Select(x => x.ToString().Split(' ').LastOrDefault()));
                var paramList = method.ParameterList.Parameters.ToString().Deflate();

                members.Add(new MemberInfo
                {
                    Line = method.GetLocation().GetLineSpan().StartLinePosition.Line + lineOffset,
                    Column = method.GetLocation().GetLineSpan().StartLinePosition.Character,
                    Content = method.Identifier.Text,
                    MemberContext = " (" + paramList + ")",
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
                Line = type.GetLocation().GetLineSpan().StartLinePosition.Line + lineOffset,
                Column = type.GetLocation().GetLineSpan().StartLinePosition.Character,
                Title = type.Identifier.Text,
                MemberContext = ": enum",
                MemberType = MemberType.Class
            });
        }

        foreach (TypeDeclarationSyntax type in types)
        {
            MemberInfo parent;
            map.Add(parent = new MemberInfo
            {
                Line = type.GetLocation().GetLineSpan().StartLinePosition.Line + lineOffset,
                Column = type.GetLocation().GetLineSpan().StartLinePosition.Character,
                Title = type.Identifier.Text,
                MemberType = MemberType.Class,
                Children = new List<MemberInfo>(),

                MemberContext = ": " + type.Kind().ToString().Replace("Declaration", "").ToLower()
            });

            if (parent.ContentType == "interface")
            {
                parent.MemberContext = ": interface";
                parent.MemberType = MemberType.Interface;
            }

            List<MemberInfo> members = new List<MemberInfo>();

            foreach (var member in type.ChildNodes().OfType<MemberDeclarationSyntax>())
            {
                if (member is MethodDeclarationSyntax)
                {
                    var method = (member as MethodDeclarationSyntax);

                    //var paramList = string.Join(", ", method.ParameterList.Parameters.Select(x => x.ToString().Split(' ').LastOrDefault()));
                    var paramList = method.ParameterList.Parameters.ToString().Deflate();

                    members.Add(new MemberInfo
                    {
                        Line = method.GetLocation().GetLineSpan().StartLinePosition.Line + lineOffset,
                        Column = method.GetLocation().GetLineSpan().StartLinePosition.Character,
                        Content = method.Identifier.Text,
                        MemberContext = " (" + paramList + ")",
                        ContentType = "    ",
                        Children = new List<MemberInfo>(),
                        IsPublic = method.Modifiers.Any(x => x.ValueText == "public" || x.ValueText == "internal"),
                        MemberType = MemberType.Method
                    });
                }
                else if (member is ConstructorDeclarationSyntax)
                {
                    var method = (member as ConstructorDeclarationSyntax);

                    // var paramList = string.Join(", ", method.ParameterList.Parameters.Select(x => x.ToString().Split(' ').LastOrDefault()));
                    var paramList = method.ParameterList.Parameters.ToString().Deflate();

                    members.Add(new MemberInfo
                    {
                        Line = method.GetLocation().GetLineSpan().StartLinePosition.Line + lineOffset,
                        Column = method.GetLocation().GetLineSpan().StartLinePosition.Character,
                        Content = method.Identifier.Text,
                        IsPublic = method.Modifiers.Any(x => x.ValueText == "public" || x.ValueText == "internal"),
                        ContentType = "    ",
                        Children = new List<MemberInfo>(),
                        MemberContext = " (" + paramList + ")",
                        MemberType = MemberType.Constructor
                    });
                }
                else if (member is PropertyDeclarationSyntax)
                {
                    var prop = (member as PropertyDeclarationSyntax);
                    members.Add(new MemberInfo
                    {
                        Line = prop.GetLocation().GetLineSpan().StartLinePosition.Line + lineOffset,
                        Column = prop.GetLocation().GetLineSpan().StartLinePosition.Character,
                        Content = prop.Identifier.ValueText,
                        ContentType = "    ",
                        IsPublic = prop.Modifiers.Any(x => x.ValueText == "public" || x.ValueText == "internal"),
                        MemberType = MemberType.Property
                    });
                }
                else if (member is FieldDeclarationSyntax)
                {
                    var field = (member as FieldDeclarationSyntax);

                    members.Add(new MemberInfo
                    {
                        Line = field.GetLocation().GetLineSpan().StartLinePosition.Line + lineOffset,
                        Column = field.GetLocation().GetLineSpan().StartLinePosition.Character,
                        Content = field.Declaration.Variables.First().Identifier.Text,
                        ContentType = "    ",
                        IsPublic = field.Modifiers.Any(x => x.ValueText == "public" || x.ValueText == "internal"),
                        MemberType = MemberType.Field
                    });
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
    public static string Deflate(this string text)
        => string.Join(" ", text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()));
}