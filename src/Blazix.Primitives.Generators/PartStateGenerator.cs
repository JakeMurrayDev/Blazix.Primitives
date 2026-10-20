using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Blazix.Primitives.Generators;

/// <summary>
/// Emits, for every <c>sealed partial record</c> marked <c>[PartState]</c>, the typed projection of its own public
/// primitive properties to <c>data-*</c> attributes in declaration order (RND-ALG-07; ADR 02 decision 4 as amended by
/// ADR 05: no reflection at render time), and the consumer-facing diagnostics BLZX1001–BLZX1004.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class PartStateGenerator : IIncrementalGenerator
{
    private const string PartStateAttributeName = "Blazix.Primitives.Rendering.PartStateAttribute";
    private const string MirroredFieldAttributeName = "Blazix.Primitives.Rendering.MirroredFieldAttribute";
    private const string Rendering = "global::Blazix.Primitives.Rendering";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<PartStateOutput> outputs = context.SyntaxProvider.ForAttributeWithMetadataName(
            PartStateAttributeName,
            static (node, _) => node is TypeDeclarationSyntax,
            static (syntaxContext, cancellationToken) => Analyze(syntaxContext, cancellationToken));

        context.RegisterSourceOutput(outputs, static (productionContext, output) =>
        {
            foreach (DiagnosticInfo diagnostic in output.Diagnostics)
            {
                productionContext.ReportDiagnostic(diagnostic.ToDiagnostic());
            }

            if (output.HintName is not null && output.Source is not null)
            {
                productionContext.AddSource(output.HintName, SourceText.From(output.Source, Encoding.UTF8));
            }
        });
    }

    private static PartStateOutput Analyze(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        ImmutableArray<DiagnosticInfo>.Builder diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        TypeDeclarationSyntax node = (TypeDeclarationSyntax)context.TargetNode;
        INamedTypeSymbol symbol = (INamedTypeSymbol)context.TargetSymbol;
        string name = symbol.Name;
        Location identifierLocation = node.Identifier.GetLocation();

        if (!symbol.IsRecord || symbol.TypeKind != TypeKind.Class)
        {
            diagnostics.Add(DiagnosticInfo.Create(PartStateDiagnostics.NotARecordClass, identifierLocation, name));
            return new PartStateOutput(null, null, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));
        }

        ImmutableArray<TypeDeclarationSyntax> containers = Containers(node);
        bool partial = node.Modifiers.Any(SyntaxKind.PartialKeyword);
        foreach (TypeDeclarationSyntax container in containers)
        {
            partial &= container.Modifiers.Any(SyntaxKind.PartialKeyword);
        }

        if (!partial)
        {
            diagnostics.Add(DiagnosticInfo.Create(PartStateDiagnostics.NotPartial, identifierLocation, name));
            return new PartStateOutput(null, null, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));
        }

        if (!symbol.IsSealed)
        {
            diagnostics.Add(DiagnosticInfo.Create(PartStateDiagnostics.NotSealed, identifierLocation, name));
        }

        string fullName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        string displayName = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        StringBuilder body = new();
        foreach (IPropertySymbol property in OwnPublicProperties(symbol))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (HasAttribute(property, MirroredFieldAttributeName))
            {
                continue;
            }

            if (!TryEmitProperty(body, property))
            {
                Location location = property.Locations.Length > 0 ? property.Locations[0] : identifierLocation;
                diagnostics.Add(DiagnosticInfo.Create(
                    PartStateDiagnostics.NotPrimitiveProperty,
                    location,
                    property.Name,
                    name,
                    property.Type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
            }
        }

        string source = Compose(symbol, node, containers, fullName, displayName, body.ToString());
        string hintName = displayName + ".PartState.g.cs";
        return new PartStateOutput(hintName, source, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));
    }

    private static ImmutableArray<TypeDeclarationSyntax> Containers(TypeDeclarationSyntax node)
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

    private static IEnumerable<IPropertySymbol> OwnPublicProperties(INamedTypeSymbol symbol)
    {
        return symbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(static p => p.DeclaredAccessibility == Accessibility.Public && !p.IsStatic && !p.IsIndexer && p.GetMethod is not null && !p.IsImplicitlyDeclared)
            .OrderBy(static p => p.Locations.Length > 0 ? p.Locations[0].SourceTree?.FilePath ?? string.Empty : string.Empty, StringComparer.Ordinal)
            .ThenBy(static p => p.Locations.Length > 0 ? p.Locations[0].SourceSpan.Start : 0);
    }

    private static bool HasAttribute(ISymbol symbol, string metadataName)
    {
        foreach (AttributeData attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == metadataName)
            {
                return true;
            }
        }

        return false;
    }

    // Emits one property's block into the method body (indentation is applied when the body is composed).
    private static bool TryEmitProperty(StringBuilder body, IPropertySymbol property)
    {
        ITypeSymbol type = property.Type;
        bool nullable = false;
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullableType)
        {
            nullable = true;
            type = nullableType.TypeArguments[0];
        }

        string name = property.Name;
        string attribute = "data-" + name.ToLowerInvariant();
        string access = "this." + name;
        StringBuilder block = new();

        if (type.TypeKind == TypeKind.Enum)
        {
            string enumName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            StringBuilder arms = new();
            HashSet<object> seen = [];
            foreach (IFieldSymbol member in type.GetMembers().OfType<IFieldSymbol>())
            {
                if (!member.IsConst || !member.HasConstantValue || member.ConstantValue is null || !seen.Add(member.ConstantValue))
                {
                    continue;
                }

                arms.Append("        ").Append(enumName).Append('.').Append(member.Name).Append(" => \"").Append(ToKebabCase(member.Name)).Append("\",\n");
            }

            if (nullable)
            {
                block.Append("    if (").Append(access).Append(" is ").Append(enumName).Append(" enumValue)\n    {\n");
                block.Append("        string? value = enumValue switch\n        {\n");
                block.Append(Indent(arms.ToString(), "    "));
                block.Append("            _ => null,\n        };\n");
                block.Append("        if (value is not null)\n        {\n            writer.Set(\"").Append(attribute).Append("\", value);\n        }\n    }\n");
            }
            else
            {
                block.Append("    string? value = ").Append(access).Append(" switch\n    {\n");
                block.Append(arms);
                block.Append("        _ => null,\n    };\n");
                block.Append("    if (value is not null)\n    {\n        writer.Set(\"").Append(attribute).Append("\", value);\n    }\n");
            }
        }
        else
        {
            switch (type.SpecialType)
            {
                case SpecialType.System_Boolean:
                    block.Append("    if (").Append(access).Append(nullable ? " == true" : string.Empty).Append(")\n    {\n        writer.Set(\"").Append(attribute).Append("\", \"\");\n    }\n");
                    break;
                case SpecialType.System_String:
                    block.Append("    if (!string.IsNullOrEmpty(").Append(access).Append("))\n    {\n        writer.Set(\"").Append(attribute).Append("\", ").Append(access).Append(");\n    }\n");
                    break;
                case SpecialType.System_Char:
                    AppendValueBlock(block, access, attribute, nullable, "value != '\\0'", "value.ToString()");
                    break;
                case SpecialType.System_Single:
                    AppendValueBlock(block, access, attribute, nullable, "value != 0 && !float.IsNaN(value)", "value.ToString(global::System.Globalization.CultureInfo.InvariantCulture)");
                    break;
                case SpecialType.System_Double:
                    AppendValueBlock(block, access, attribute, nullable, "value != 0 && !double.IsNaN(value)", "value.ToString(global::System.Globalization.CultureInfo.InvariantCulture)");
                    break;
                case SpecialType.System_SByte:
                case SpecialType.System_Byte:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                case SpecialType.System_Decimal:
                    AppendValueBlock(block, access, attribute, nullable, "value != 0", "value.ToString(global::System.Globalization.CultureInfo.InvariantCulture)");
                    break;
                default:
                    return false;
            }
        }

        body.Append("if (mapping is null || !mapping.TryProject(\"").Append(name).Append("\", ").Append(access).Append(", writer))\n{\n");
        body.Append(block);
        body.Append("}\n\n");
        return true;
    }

    private static void AppendValueBlock(StringBuilder block, string access, string attribute, bool nullable, string condition, string conversion)
    {
        if (nullable)
        {
            block.Append("    if (").Append(access).Append(" is { } value && ").Append(condition).Append(")\n    {\n        writer.Set(\"").Append(attribute).Append("\", ").Append(conversion).Append(");\n    }\n");
        }
        else
        {
            block.Append("    if (").Append(condition.Replace("value", access)).Append(")\n    {\n        writer.Set(\"").Append(attribute).Append("\", ").Append(conversion.Replace("value", access)).Append(");\n    }\n");
        }
    }

    private static string Compose(INamedTypeSymbol symbol, TypeDeclarationSyntax node, ImmutableArray<TypeDeclarationSyntax> containers, string fullName, string displayName, string body)
    {
        StringBuilder source = new();
        source.Append("// <auto-generated/>\n");
        source.Append("// Projection of ").Append(displayName).Append(" to data-* attributes (RND-ALG-07), emitted by Blazix.Primitives.Generators.\n");
        source.Append("#nullable enable\n\n");
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

        string interfaceName = Rendering + ".IPartState<" + fullName + ">";
        source.Append(indent).Append("partial record ").Append(node.Identifier.ValueText).Append(node.TypeParameterList?.ToString() ?? string.Empty).Append(" : ").Append(interfaceName).Append('\n');
        source.Append(indent).Append("{\n");
        source.Append(indent).Append("    void ").Append(interfaceName).Append(".ProjectStateAttributes(").Append(Rendering).Append(".StateAttributesMapping<").Append(fullName).Append(">? mapping, ").Append(Rendering).Append(".StateAttributeWriter writer)\n");
        source.Append(indent).Append("    {\n");
        if (body.Length > 0)
        {
            source.Append(Indent(body.TrimEnd('\n') + "\n", indent + "        "));
        }

        source.Append(indent).Append("    }\n");
        source.Append(indent).Append("}\n");

        for (int i = containers.Length - 1; i >= 0; i--)
        {
            indent = indent.Substring(0, indent.Length - 4);
            source.Append(indent).Append("}\n");
        }

        return source.ToString();
    }

    private static string Keyword(TypeDeclarationSyntax type)
    {
        if (type is RecordDeclarationSyntax record)
        {
            return record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword) ? "record struct" : "record";
        }

        return type.Keyword.ValueText;
    }

    private static string Indent(string text, string indent)
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

    // PascalCase member → Base UI's kebab-case literal (ADR 08 §3): InlineStart → inline-start, CloseWatcher → close-watcher.
    private static string ToKebabCase(string name)
    {
        StringBuilder result = new(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (char.IsUpper(c) && i > 0)
            {
                char previous = name[i - 1];
                bool boundary = char.IsLower(previous) || char.IsDigit(previous) || (char.IsUpper(previous) && i + 1 < name.Length && char.IsLower(name[i + 1]));
                if (boundary)
                {
                    result.Append('-');
                }
            }

            result.Append(char.ToLowerInvariant(c));
        }

        return result.ToString();
    }

    private sealed record PartStateOutput(string? HintName, string? Source, EquatableArray<DiagnosticInfo> Diagnostics);
}
