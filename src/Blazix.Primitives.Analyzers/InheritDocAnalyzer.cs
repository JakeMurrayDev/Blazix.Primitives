using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Blazix.Primitives.Analyzers;

// ADR 08 §5, Amendments 2026-10-20 (documentation comments on overriding and implementing members; the clarification of
// issue 42 that allows <exception> beside <remarks>)
/// <summary>
/// Requires a member that overrides a base member or implements an interface member to document itself with
/// <c>&lt;inheritdoc /&gt;</c>.
/// </summary>
/// <remarks>
/// The contract is written once, on the member that declares it. The overriding or implementing member's documentation
/// comment is <c>&lt;inheritdoc /&gt;</c>, optionally followed by <c>&lt;remarks&gt;</c> for behavior it adds and
/// <c>&lt;exception&gt;</c> for exceptions it adds, and nothing else. A syntax tree parsed without documentation comments
/// is not checked.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InheritDocAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Represents the id of the diagnostic this analyzer reports, <c>BLZX2007</c>.</summary>
    public const string DiagnosticId = "BLZX2007";

    // ADR 08 Amendments 2026-10-20
    private static readonly DiagnosticDescriptor s_rule = new(
        DiagnosticId,
        "An overriding or implementing member inherits its documentation",
        "Blazix: `{0}` overrides or implements an inherited member, so its documentation comment is `<inheritdoc />` with at most `<remarks>` and `<exception>` added; {1}",
        "Blazix.Primitives",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A member that overrides a base member or implements an interface member, implicitly or explicitly, inherits the documentation of the member it overrides or implements. Its comment is `/// <inheritdoc />`, optionally with `<remarks>` for added behavior and `<exception>` for added exceptions; `<summary>`, `<param>`, `<returns>`, `<value>` and every other element restate the contract and are rejected.");

    private static readonly ImmutableHashSet<string> s_allowedElements = ImmutableHashSet.Create(StringComparer.Ordinal, "inheritdoc", "remarks", "exception");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(s_rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeMember, SymbolKind.Method, SymbolKind.Property, SymbolKind.Event);
    }

    private static void AnalyzeMember(SymbolAnalysisContext context)
    {
        ISymbol member = context.Symbol;
        if (member.IsImplicitlyDeclared
            || member is IMethodSymbol { MethodKind: not (MethodKind.Ordinary or MethodKind.ExplicitInterfaceImplementation) }
            || !OverridesOrImplements(member))
        {
            return;
        }

        foreach (SyntaxReference reference in member.DeclaringSyntaxReferences)
        {
            SyntaxNode declaration = reference.GetSyntax(context.CancellationToken);
            if (declaration is VariableDeclaratorSyntax { Parent.Parent: EventFieldDeclarationSyntax eventField })
            {
                declaration = eventField;
            }

            if (declaration.SyntaxTree.Options.DocumentationMode == DocumentationMode.None)
            {
                continue;
            }

            if (Problem(declaration) is { } problem)
            {
                Location location = member.Locations.FirstOrDefault(l => l.SourceTree == declaration.SyntaxTree) ?? declaration.GetLocation();
                context.ReportDiagnostic(Diagnostic.Create(s_rule, location, DisplayName(member), problem));
            }
        }
    }

    // An explicit implementation reads `IDisposable.Dispose`; every other member its own name.
    private static string DisplayName(ISymbol member)
    {
        ISymbol? implemented = member switch
        {
            IMethodSymbol method => method.ExplicitInterfaceImplementations.FirstOrDefault(),
            IPropertySymbol property => property.ExplicitInterfaceImplementations.FirstOrDefault(),
            IEventSymbol @event => @event.ExplicitInterfaceImplementations.FirstOrDefault(),
            _ => null,
        };
        return implemented is null
            ? member.Name
            : implemented.ContainingType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) + "." + implemented.Name;
    }

    private static bool OverridesOrImplements(ISymbol member)
    {
        if (member.IsOverride)
        {
            return true;
        }

        bool isExplicit = member switch
        {
            IMethodSymbol method => !method.ExplicitInterfaceImplementations.IsEmpty,
            IPropertySymbol property => !property.ExplicitInterfaceImplementations.IsEmpty,
            IEventSymbol @event => !@event.ExplicitInterfaceImplementations.IsEmpty,
            _ => false,
        };
        if (isExplicit)
        {
            return true;
        }

        INamedTypeSymbol? type = member.ContainingType;
        if (type is null)
        {
            return false;
        }

        foreach (INamedTypeSymbol implemented in type.AllInterfaces)
        {
            foreach (ISymbol interfaceMember in implemented.GetMembers(member.Name))
            {
                if (SymbolEqualityComparer.Default.Equals(type.FindImplementationForInterfaceMember(interfaceMember), member))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // What is wrong with the declaration's documentation comment, or null when it is <inheritdoc /> with only the allowed
    // additions.
    private static string? Problem(SyntaxNode declaration)
    {
        DocumentationCommentTriviaSyntax? comment = null;
        foreach (SyntaxTrivia trivia in declaration.GetLeadingTrivia())
        {
            if (trivia.GetStructure() is DocumentationCommentTriviaSyntax structure)
            {
                comment = structure;
            }
        }

        if (comment is null)
        {
            return "add `/// <inheritdoc />`";
        }

        bool inherits = false;
        foreach (XmlNodeSyntax node in comment.Content)
        {
            string? name = node switch
            {
                XmlElementSyntax element => element.StartTag.Name.LocalName.ValueText,
                XmlEmptyElementSyntax empty => empty.Name.LocalName.ValueText,
                _ => null,
            };
            if (name is null)
            {
                if (node is XmlTextSyntax text && !IsBlank(text))
                {
                    return "move its text into `<remarks>` or remove it";
                }

                continue;
            }

            if (!s_allowedElements.Contains(name))
            {
                return $"remove `<{name}>`";
            }

            inherits |= name == "inheritdoc";
        }

        return inherits ? null : "add `<inheritdoc />`";
    }

    private static bool IsBlank(XmlTextSyntax text)
    {
        foreach (SyntaxToken token in text.TextTokens)
        {
            if (token.IsKind(SyntaxKind.XmlTextLiteralToken) && !string.IsNullOrWhiteSpace(token.ValueText))
            {
                return false;
            }
        }

        return true;
    }
}
