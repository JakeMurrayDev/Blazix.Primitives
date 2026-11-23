using Microsoft.CodeAnalysis;

namespace Blazix.Primitives.Analyzers;

/// <summary>
/// Provides the symbol tests the convention analyzers share.
/// </summary>
internal static class ConventionSymbols
{
    /// <summary>
    /// Determines whether <paramref name="type"/> is a union: a declared <c>union</c> type or a struct marked
    /// <c>[Union]</c>.
    /// </summary>
    /// <param name="type">The type to test, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if <paramref name="type"/> is a union; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// A declared <c>union</c> compiles to a struct that implements <c>IUnion</c>; a hand-written union carries
    /// <c>[Union]</c>. Both are matched by name in <c>System.Runtime.CompilerServices</c>, which keeps the test off the
    /// compiler's experimental union API.
    /// </remarks>
    public static bool IsUnion(ITypeSymbol? type)
    {
        if (type is not { TypeKind: TypeKind.Struct })
        {
            return false;
        }

        foreach (AttributeData attribute in type.GetAttributes())
        {
            if (attribute.AttributeClass is { Name: "UnionAttribute" } attributeClass && IsCompilerServices(attributeClass))
            {
                return true;
            }
        }

        foreach (INamedTypeSymbol implemented in type.AllInterfaces)
        {
            if (implemented.Name == "IUnion" && IsCompilerServices(implemented))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets the type a <c>Nullable&lt;T&gt;</c> wraps, or the type itself when it is not nullable.</summary>
    /// <param name="type">The type to unwrap.</param>
    /// <returns>The <c>T</c> of <c>Nullable&lt;T&gt;</c>, or <paramref name="type"/>.</returns>
    public static ITypeSymbol UnwrapNullable(ITypeSymbol type)
    {
        return type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? nullable.TypeArguments[0]
            : type;
    }

    /// <summary>
    /// Determines whether <paramref name="symbol"/> is a Blazor component parameter: it carries <c>[Parameter]</c> or a
    /// cascading parameter attribute such as <c>[CascadingParameter]</c>.
    /// </summary>
    /// <param name="symbol">The property to test.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="symbol"/> is a component parameter; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool IsComponentParameter(ISymbol symbol)
    {
        foreach (AttributeData attribute in symbol.GetAttributes())
        {
            for (INamedTypeSymbol? type = attribute.AttributeClass; type is not null; type = type.BaseType)
            {
                if (type.Name is "ParameterAttribute" or "CascadingParameterAttributeBase"
                    && type.ContainingNamespace?.ToDisplayString() == "Microsoft.AspNetCore.Components")
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsCompilerServices(INamedTypeSymbol type)
    {
        return type.ContainingNamespace?.ToDisplayString() == "System.Runtime.CompilerServices";
    }
}
