using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PyMap;

class CSharpMapper
{
    public static IEnumerable<MemberInfo> Generate(string file)
    {
        var map = new List<MemberInfo>();

        var code = File.ReadAllText(file);
        SyntaxTree tree = CSharpSyntaxTree.ParseText(code);

        var root = tree.GetRoot();

        var nodes = root.DescendantNodes();

        var types = nodes.OfType<TypeDeclarationSyntax>()
                         .OrderBy(x => x.FullSpan.End)
                         .ToArray();

        foreach (EnumDeclarationSyntax type in nodes.OfType<EnumDeclarationSyntax>())
        {
            var parentType = type.Parent as BaseTypeDeclarationSyntax;

            map.Add(new MemberInfo
            {
                Line = type.GetLocation().GetLineSpan().StartLinePosition.Line,
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
                Line = type.GetLocation().GetLineSpan().StartLinePosition.Line,
                Column = type.GetLocation().GetLineSpan().StartLinePosition.Character,
                Title = type.Identifier.Text,
                MemberType = MemberType.Class,

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
                    var paramList = method.ParameterList.Parameters.ToString();

                    members.Add(new MemberInfo
                    {
                        Line = method.GetLocation().GetLineSpan().StartLinePosition.Line,
                        Column = method.GetLocation().GetLineSpan().StartLinePosition.Character,
                        Content = method.Identifier.Text,
                        MemberContext = " (" + paramList + ")",
                        ContentType = "    ",
                        IsPublic = method.Modifiers.Any(x => x.ValueText == "public" || x.ValueText == "internal"),
                        MemberType = MemberType.Method
                    });
                }
                else if (member is ConstructorDeclarationSyntax)
                {
                    var method = (member as ConstructorDeclarationSyntax);

                    // var paramList = string.Join(", ", method.ParameterList.Parameters.Select(x => x.ToString().Split(' ').LastOrDefault()));
                    var paramList = method.ParameterList.Parameters.ToString();

                    members.Add(new MemberInfo
                    {
                        Line = method.GetLocation().GetLineSpan().StartLinePosition.Line,
                        Column = method.GetLocation().GetLineSpan().StartLinePosition.Character,
                        Content = method.Identifier.Text,
                        IsPublic = method.Modifiers.Any(x => x.ValueText == "public" || x.ValueText == "internal"),
                        ContentType = "    ",
                        MemberContext = " (" + paramList + ")",
                        MemberType = MemberType.Constructor
                    });
                }
                else if (member is PropertyDeclarationSyntax)
                {
                    var prop = (member as PropertyDeclarationSyntax);
                    members.Add(new MemberInfo
                    {
                        Line = prop.GetLocation().GetLineSpan().StartLinePosition.Line,
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
                        Line = field.GetLocation().GetLineSpan().StartLinePosition.Line,
                        Column = field.GetLocation().GetLineSpan().StartLinePosition.Character,
                        Content = field.Declaration.Variables.First().Identifier.Text,
                        ContentType = "    ",
                        IsPublic = field.Modifiers.Any(x => x.ValueText == "public" || x.ValueText == "internal"),
                        MemberType = MemberType.Field
                    });
                }
            }

            members = members.Where(x =>
                                    {
                                        if (x.MemberType == MemberType.Property || x.MemberType == MemberType.Field)
                                            return x.IsPublic;
                                        return true;
                                    })
                             .OrderBy(x => x.MemberType)
                             .ToList();

            parent.Children = members.ToArray();
        }

        return map;
    }
}