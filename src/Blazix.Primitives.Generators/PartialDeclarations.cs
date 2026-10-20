using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Blazix.Primitives.Generators;

/// <summary>Provides what the generators share to write a partial declaration back into the type that carries their attribute.</summary>
internal static class PartialDeclarations
{
    /// <summary>Gets the type declarations that contain a type declaration.</summary>
    /// <param name="node">The type declaration.</param>
    /// <returns>The containing type declarations, outermost first; empty for a top-level type.</returns>
    public static ImmutableArray<TypeDeclarationSyntax> Containers(TypeDeclarationSyntax node)
    {
        ImmutableArray<TypeDeclarationSyntax>.Builder containers = ImmutableArray.CreateBuilder<TypeDeclarationSyntax>();
        SyntaxNode? parent = node.Parent;
        while (parent is TypeDeclarationSyntax container)
        {
            containers.Insert(0, container);
            parent = container.Parent;
        }

        return containers.ToImmutable();
    }

    /// <summary>Determines whether a type declaration and every one of its containing declarations is declared <c>partial</c>.</summary>
    /// <param name="node">The type declaration.</param>
    /// <param name="containers">The declarations that contain <paramref name="node"/>.</param>
    /// <returns><see langword="true"/> if all of them are partial; otherwise, <see langword="false"/>.</returns>
    public static bool AllPartial(TypeDeclarationSyntax node, ImmutableArray<TypeDeclarationSyntax> containers)
    {
        bool partial = node.Modifiers.Any(SyntaxKind.PartialKeyword);
        foreach (TypeDeclarationSyntax container in containers)
        {
            partial &= container.Modifiers.Any(SyntaxKind.PartialKeyword);
        }

        return partial;
    }

    /// <summary>Gets the keyword that redeclares a type: <c>class</c>, <c>struct</c>, <c>record</c>, <c>record struct</c> or <c>interface</c>.</summary>
    /// <param name="type">The type declaration.</param>
    /// <returns>The keyword, including <c>struct</c> for a record struct.</returns>
    public static string Keyword(TypeDeclarationSyntax type)
    {
        if (type is RecordDeclarationSyntax record)
        {
            return record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword) ? "record struct" : "record";
        }

        return type.Keyword.ValueText;
    }

    /// <summary>Gets the hint name of a type's generated source.</summary>
    /// <param name="displayName">The display name of the type, such as <c>Outer.ValueState&lt;TValue&gt;</c>.</param>
    /// <param name="suffix">The suffix that names the generator, such as <c>.StoreState.g.cs</c>.</param>
    /// <returns>The display name with braces for the angle brackets of type parameters, which a hint name cannot hold, followed by <paramref name="suffix"/>.</returns>
    public static string HintName(string displayName, string suffix) =>
        displayName.Replace('<', '{').Replace('>', '}') + suffix;

    /// <summary>Indents every non-empty line of a text.</summary>
    /// <param name="text">The text, with lines separated by <c>\n</c>.</param>
    /// <param name="indent">The indentation to put before each non-empty line.</param>
    /// <returns>The indented text.</returns>
    public static string Indent(string text, string indent)
    {
        StringBuilder result = new(text.Length + 64);
        int start = 0;
        while (start < text.Length)
        {
            int end = text.IndexOf('\n', start);
            if (end < 0)
            {
                end = text.Length;
            }

            if (end > start)
            {
                result.Append(indent).Append(text, start, end - start);
            }

            result.Append('\n');
            start = end + 1;
        }

        return result.ToString();
    }

    /// <summary>Writes the namespace line and opens the partial declarations of the containing types, outermost first.</summary>
    /// <param name="source">The generated source to write to.</param>
    /// <param name="symbol">The type the generated declaration belongs to.</param>
    /// <param name="containers">The declarations that contain the type, outermost first.</param>
    /// <returns>The indentation inside the innermost containing declaration.</returns>
    /// <remarks><see cref="CloseContainers"/> closes the declarations again.</remarks>
    public static string OpenContainers(StringBuilder source, INamedTypeSymbol symbol, ImmutableArray<TypeDeclarationSyntax> containers)
    {
        if (!symbol.ContainingNamespace.IsGlobalNamespace)
        {
            source.Append("namespace ").Append(symbol.ContainingNamespace.ToDisplayString()).Append(";\n\n");
        }

        string indent = string.Empty;
        foreach (TypeDeclarationSyntax container in containers)
        {
            source.Append(indent).Append("partial ").Append(Keyword(container)).Append(' ').Append(container.Identifier.ValueText).Append(container.TypeParameterList?.ToString() ?? string.Empty).Append('\n');
            source.Append(indent).Append("{\n");
            indent += "    ";
        }

        return indent;
    }

    /// <summary>Closes the declarations that <see cref="OpenContainers"/> opened.</summary>
    /// <param name="source">The generated source to write to.</param>
    /// <param name="containers">The declarations that contain the type, outermost first.</param>
    public static void CloseContainers(StringBuilder source, ImmutableArray<TypeDeclarationSyntax> containers)
    {
        for (int i = containers.Length - 1; i >= 0; i--)
        {
            source.Append(new string(' ', i * 4)).Append("}\n");
        }
    }
}
