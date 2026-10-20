using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Blazix.Primitives.Analyzers;

// ADR 08 §5 (the eleven groups); the placements of constructors, events, operators, explicit interface implementations,
// protected members and nested types are this analyzer's, listed in CONTEXT.md
/// <summary>
/// Requires the members of every type to appear in the library's member order.
/// </summary>
/// <remarks>
/// The order is constants, fields, constructors, private properties, parameter properties, public properties, internal
/// properties, lifecycle methods, dispose methods, public methods, internal methods, private methods, nested types. A member
/// is out of order when a member of a later group comes before it in the same declaration.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MemberOrderAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Represents the id of the diagnostic this analyzer reports, <c>BLZX2003</c>.</summary>
    public const string DiagnosticId = "BLZX2003";

    // ADR 08 §5
    private static readonly DiagnosticDescriptor s_rule = new(
        DiagnosticId,
        "Members follow the library's member order",
        "Blazix: `{0}` is one of the {1} and must come before `{2}`, one of the {3}",
        "Blazix.Primitives",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Members appear in this order: constants; fields (field-like events included); constructors (static constructor and finalizer included); private properties; parameter properties ([Parameter] and cascading parameters, whatever their accessibility); public properties; internal properties; lifecycle methods (SetParametersAsync, OnInitialized*, OnParametersSet*, OnAfterRender*, ShouldRender, BuildRenderTree); dispose methods (Dispose, DisposeAsync, DisposeAsyncCore); public methods (operators included); internal methods; private methods; nested types. Protected and protected internal members order as public, private protected members as internal, and an explicit interface implementation as public unless its name makes it a lifecycle or dispose method.");

    private static readonly ImmutableArray<string> s_groupNames = ImmutableArray.Create(
        "constants",
        "fields",
        "constructors",
        "private properties",
        "parameter properties",
        "public properties",
        "internal properties",
        "lifecycle methods",
        "dispose methods",
        "public methods",
        "internal methods",
        "private methods",
        "nested types");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(s_rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            AnalyzeType,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.RecordStructDeclaration,
            SyntaxKind.InterfaceDeclaration);
    }

    private static void AnalyzeType(SyntaxNodeAnalysisContext context)
    {
        TypeDeclarationSyntax type = (TypeDeclarationSyntax)context.Node;

        // The first member seen of each group, by group index.
        Member?[] firstOfGroup = new Member?[s_groupNames.Length];
        foreach (MemberDeclarationSyntax declaration in type.Members)
        {
            Member? member = Classify(declaration, context.SemanticModel, context.CancellationToken);
            if (member is not { } current)
            {
                continue;
            }

            Member? later = null;
            for (int group = current.Group + 1; group < firstOfGroup.Length; group++)
            {
                if (firstOfGroup[group] is { } candidate && (later is null || candidate.Position < later.Value.Position))
                {
                    later = candidate;
                }
            }

            if (later is { } preceding)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    s_rule,
                    current.Location,
                    current.Name,
                    s_groupNames[current.Group],
                    preceding.Name,
                    s_groupNames[preceding.Group]));
            }

            firstOfGroup[current.Group] ??= current;
        }
    }

    private static Member? Classify(MemberDeclarationSyntax declaration, SemanticModel model, CancellationToken cancellationToken)
    {
        switch (declaration)
        {
            case FieldDeclarationSyntax field when field.Declaration.Variables.Count > 0:
                {
                    SyntaxToken identifier = field.Declaration.Variables[0].Identifier;
                    int group = field.Modifiers.Any(SyntaxKind.ConstKeyword) ? Group.Constants : Group.Fields;
                    return new Member(group, identifier.ValueText, identifier.GetLocation(), declaration.SpanStart);
                }

            case EventFieldDeclarationSyntax eventField when eventField.Declaration.Variables.Count > 0:
                {
                    SyntaxToken identifier = eventField.Declaration.Variables[0].Identifier;
                    return new Member(Group.Fields, identifier.ValueText, identifier.GetLocation(), declaration.SpanStart);
                }

            case EventDeclarationSyntax eventDeclaration:
                return new Member(Group.Fields, eventDeclaration.Identifier.ValueText, eventDeclaration.Identifier.GetLocation(), declaration.SpanStart);

            case ConstructorDeclarationSyntax constructor:
                {
                    string name = constructor.Modifiers.Any(SyntaxKind.StaticKeyword) ? "static " + constructor.Identifier.ValueText : constructor.Identifier.ValueText;
                    return new Member(Group.Constructors, name, constructor.Identifier.GetLocation(), declaration.SpanStart);
                }

            case DestructorDeclarationSyntax destructor:
                return new Member(Group.Constructors, "~" + destructor.Identifier.ValueText, destructor.Identifier.GetLocation(), declaration.SpanStart);

            case PropertyDeclarationSyntax property:
                return Property(property.Identifier.ValueText, property.Identifier.GetLocation(), declaration, model, cancellationToken);

            case IndexerDeclarationSyntax indexer:
                return Property("this[]", indexer.ThisKeyword.GetLocation(), declaration, model, cancellationToken);

            case MethodDeclarationSyntax method:
                {
                    string name = method.Identifier.ValueText;
                    string display = method.ExplicitInterfaceSpecifier is { } explicitSpecifier
                        ? explicitSpecifier.Name.ToString() + "." + name
                        : name;
                    int group = MethodGroup(name, method.ExplicitInterfaceSpecifier is not null, declaration, model, cancellationToken);
                    return new Member(group, display, method.Identifier.GetLocation(), declaration.SpanStart);
                }

            case OperatorDeclarationSyntax operatorDeclaration:
                return new Member(Group.PublicMethods, "operator " + operatorDeclaration.OperatorToken.ValueText, operatorDeclaration.OperatorToken.GetLocation(), declaration.SpanStart);

            case ConversionOperatorDeclarationSyntax conversion:
                return new Member(
                    Group.PublicMethods,
                    conversion.ImplicitOrExplicitKeyword.ValueText + " operator " + conversion.Type,
                    conversion.Type.GetLocation(),
                    declaration.SpanStart);

            case BaseTypeDeclarationSyntax nested:
                return new Member(Group.NestedTypes, nested.Identifier.ValueText, nested.Identifier.GetLocation(), declaration.SpanStart);

            case DelegateDeclarationSyntax nestedDelegate:
                return new Member(Group.NestedTypes, nestedDelegate.Identifier.ValueText, nestedDelegate.Identifier.GetLocation(), declaration.SpanStart);

            default:
                return null;
        }
    }

    private static Member Property(string name, Location location, MemberDeclarationSyntax declaration, SemanticModel model, CancellationToken cancellationToken)
    {
        ISymbol? symbol = model.GetDeclaredSymbol(declaration, cancellationToken);
        int group;
        if (symbol is not null && ConventionSymbols.IsComponentParameter(symbol))
        {
            group = Group.ParameterProperties;
        }
        else
        {
            group = VisibilityOf(symbol, declaration) switch
            {
                Visibility.Public => Group.PublicProperties,
                Visibility.Internal => Group.InternalProperties,
                _ => Group.PrivateProperties,
            };
        }

        string display = declaration is PropertyDeclarationSyntax { ExplicitInterfaceSpecifier: { } specifier } ? specifier.Name + "." + name : name;
        return new Member(group, display, location, declaration.SpanStart);
    }

    private static int MethodGroup(string name, bool isExplicit, MemberDeclarationSyntax declaration, SemanticModel model, CancellationToken cancellationToken)
    {
        if (IsLifecycleName(name))
        {
            return Group.LifecycleMethods;
        }

        if (name is "Dispose" or "DisposeAsync" or "DisposeAsyncCore")
        {
            return Group.DisposeMethods;
        }

        if (isExplicit)
        {
            return Group.PublicMethods;
        }

        return VisibilityOf(model.GetDeclaredSymbol(declaration, cancellationToken), declaration) switch
        {
            Visibility.Public => Group.PublicMethods,
            Visibility.Internal => Group.InternalMethods,
            _ => Group.PrivateMethods,
        };
    }

    private static bool IsLifecycleName(string name)
    {
        return name is "SetParametersAsync" or "ShouldRender" or "BuildRenderTree"
            || name.StartsWith("OnInitialized", StringComparison.Ordinal)
            || name.StartsWith("OnParametersSet", StringComparison.Ordinal)
            || name.StartsWith("OnAfterRender", StringComparison.Ordinal);
    }

    // Protected and protected internal order as public; private protected as internal. An explicit interface implementation
    // is reached through the interface, so it orders as public.
    private static Visibility VisibilityOf(ISymbol? symbol, MemberDeclarationSyntax declaration)
    {
        if (declaration is PropertyDeclarationSyntax { ExplicitInterfaceSpecifier: not null } or IndexerDeclarationSyntax { ExplicitInterfaceSpecifier: not null })
        {
            return Visibility.Public;
        }

        if (declaration.Parent is InterfaceDeclarationSyntax && !declaration.Modifiers.Any(SyntaxKind.PrivateKeyword) && !declaration.Modifiers.Any(SyntaxKind.InternalKeyword))
        {
            return Visibility.Public;
        }

        return symbol?.DeclaredAccessibility switch
        {
            Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal => Visibility.Public,
            Accessibility.Internal or Accessibility.ProtectedAndInternal => Visibility.Internal,
            _ => Visibility.Private,
        };
    }

    private enum Visibility
    {
        Public,
        Internal,
        Private,
    }

    private readonly struct Member(int group, string name, Location location, int position)
    {
        public int Group { get; } = group;

        public string Name { get; } = name;

        public Location Location { get; } = location;

        public int Position { get; } = position;
    }

    private static class Group
    {
        public const int Constants = 0;
        public const int Fields = 1;
        public const int Constructors = 2;
        public const int PrivateProperties = 3;
        public const int ParameterProperties = 4;
        public const int PublicProperties = 5;
        public const int InternalProperties = 6;
        public const int LifecycleMethods = 7;
        public const int DisposeMethods = 8;
        public const int PublicMethods = 9;
        public const int InternalMethods = 10;
        public const int PrivateMethods = 11;
        public const int NestedTypes = 12;
    }
}
