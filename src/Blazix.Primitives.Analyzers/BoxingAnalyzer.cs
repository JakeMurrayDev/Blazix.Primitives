using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Blazix.Primitives.Analyzers;

// ADR 05 §4 (no boxing where the port controls it; a union never travels through an `object` slot, Amendments
// 2026-10-20); ADR 16 §C. The allowed sites are Blazor's own `object` contracts, listed in CONTEXT.md.
/// <summary>
/// Rejects boxing in library code: a union converted to <c>object</c> or an interface anywhere, any other value type
/// converted to <c>object</c> or an interface outside Blazor's own <c>object</c> contracts, and the members of
/// <c>System.Enum</c> that box their receiver or argument.
/// </summary>
/// <remarks>
/// The allowed sites are the <c>object</c> value of <c>RenderTreeBuilder.AddAttribute</c> and
/// <c>RenderTreeBuilder.AddComponentParameter</c>, the argument of <c>RenderTreeBuilder.AddMultipleAttributes</c>, the
/// argument arrays of the <c>Invoke*</c> methods of <c>IJSRuntime</c> and <c>IJSObjectReference</c> (with the in-process
/// interfaces and the extension methods on them), and a value stored into a dictionary of <c>string</c> to
/// <c>object</c>, the type of the unmatched attribute bag.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BoxingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Represents the id of the diagnostics this analyzer reports, <c>BLZX2005</c>.</summary>
    public const string DiagnosticId = "BLZX2005";

    // ADR 05 §4, Amendments 2026-10-20 (union-binding spike)
    private static readonly DiagnosticDescriptor s_unionRule = new(
        DiagnosticId,
        "A union never converts to `object` or an interface",
        "Blazix: `{0}` converts the union `{1}` to `{2}`, which boxes it and hides its case; pass the case value instead",
        "Blazix.Primitives",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Boxing a union struct hides which case it holds, so a union never travels through an `object` slot, not even one of Blazor's: the unmatched attribute bag and the attribute methods of `RenderTreeBuilder` receive the case value.");

    // ADR 05 §4; ADR 16 §C
    private static readonly DiagnosticDescriptor s_valueRule = new(
        DiagnosticId,
        "Library code does not box value types",
        "Blazix: `{0}` boxes the `{1}` value into `{2}`; use a typed overload or a generic parameter instead",
        "Blazix.Primitives",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A value type converted to `object` or to an interface allocates on every call. Library code passes value types through typed overloads and generics; the only conversions allowed are into Blazor's own `object` contracts: the value of `RenderTreeBuilder.AddAttribute` and `AddComponentParameter`, the argument of `AddMultipleAttributes`, the argument arrays of the JavaScript interop `Invoke*` methods, and a value stored into a dictionary of `string` to `object`.");

    // ADR 05 §4; ADR 16 §C
    private static readonly DiagnosticDescriptor s_enumRule = new(
        DiagnosticId,
        "Library code does not call the boxing members of `Enum`",
        "Blazix: `{0}` boxes the enum `{1}`; {2}",
        "Blazix.Primitives",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "`Enum.ToString`, `Enum.HasFlag`, `Enum.Equals(object)`, `Enum.GetValues` and `Enum.GetNames` box their enum or work by reflection. Library code switches over the members, tests flags with `&`, compares with `==` and lists the members it needs.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(s_unionRule, s_valueRule, s_enumRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            AllowedSites sites = new(
                start.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder"),
                start.Compilation.GetTypeByMetadataName("Microsoft.JSInterop.IJSRuntime"),
                start.Compilation.GetTypeByMetadataName("Microsoft.JSInterop.IJSObjectReference"),
                start.Compilation.GetTypeByMetadataName("System.Collections.Generic.IDictionary`2"),
                start.Compilation.GetTypeByMetadataName("System.Collections.Generic.IReadOnlyDictionary`2"),
                start.Compilation.GetTypeByMetadataName("System.Collections.Generic.KeyValuePair`2"));
            start.RegisterOperationAction(operation => AnalyzeConversion(operation, sites), OperationKind.Conversion);
            start.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        });
    }

    private static void AnalyzeConversion(OperationAnalysisContext context, AllowedSites sites)
    {
        IConversionOperation conversion = (IConversionOperation)context.Operation;
        ITypeSymbol? source = conversion.Operand.Type;
        if (source is null || conversion.Type is null || !source.IsValueType || !conversion.GetConversion().IsBoxing)
        {
            return;
        }

        string operand = conversion.Operand.Syntax.ToString();
        string target = conversion.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        ITypeSymbol unwrapped = ConventionSymbols.UnwrapNullable(source);
        if (ConventionSymbols.IsUnion(unwrapped))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                s_unionRule,
                conversion.Syntax.GetLocation(),
                operand,
                unwrapped.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                target));
            return;
        }

        if (IsEnumMemberArgument(conversion) || sites.Allows(conversion))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            s_valueRule,
            conversion.Syntax.GetLocation(),
            operand,
            source.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            target));
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        IInvocationOperation invocation = (IInvocationOperation)context.Operation;
        if (EnumAdvice(invocation.TargetMethod) is not { } advice)
        {
            return;
        }

        ITypeSymbol? enumType = invocation.Instance?.Type
            ?? (invocation.TargetMethod.IsGenericMethod ? invocation.TargetMethod.TypeArguments[0] : null);
        string enumName = enumType is { TypeKind: TypeKind.Enum }
            ? enumType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
            : "Enum";
        context.ReportDiagnostic(Diagnostic.Create(s_enumRule, invocation.Syntax.GetLocation(), invocation.Syntax.ToString(), enumName, advice));
    }

    // The members of System.Enum that box (or reflect), with what to write instead.
    private static string? EnumAdvice(IMethodSymbol method)
    {
        if (method.ContainingType?.SpecialType != SpecialType.System_Enum)
        {
            return null;
        }

        return method.Name switch
        {
            "ToString" => "use a `switch` over its members instead",
            "HasFlag" => "test the flag with `(value & flag) != 0` instead",
            "Equals" when !method.IsStatic => "compare with `==` instead",
            "GetValues" or "GetNames" => "list the members it needs instead",
            _ => null,
        };
    }

    // The enum's own argument conversion is reported once, as the enum member call.
    private static bool IsEnumMemberArgument(IConversionOperation conversion)
    {
        return conversion.Parent is IArgumentOperation { Parent: IInvocationOperation invocation } && EnumAdvice(invocation.TargetMethod) is not null;
    }

    private sealed class AllowedSites(
        INamedTypeSymbol? renderTreeBuilder,
        INamedTypeSymbol? jsRuntime,
        INamedTypeSymbol? jsObjectReference,
        INamedTypeSymbol? dictionary,
        INamedTypeSymbol? readOnlyDictionary,
        INamedTypeSymbol? keyValuePair)
    {
        public bool Allows(IConversionOperation conversion)
        {
            IOperation? slot = conversion.Parent;

            // An element of an argument array, written out or built for a `params` parameter.
            if (slot is IArrayInitializerOperation { Parent: IArrayCreationOperation array })
            {
                slot = array.Parent;
            }

            if (slot is IArgumentOperation { Parent: IInvocationOperation invocation } && IsBlazorObjectContract(invocation.TargetMethod))
            {
                return true;
            }

            return IsBagValue(conversion);
        }

        private bool IsBlazorObjectContract(IMethodSymbol method)
        {
            if (renderTreeBuilder is not null
                && SymbolEqualityComparer.Default.Equals(method.ContainingType, renderTreeBuilder)
                && method.Name is "AddAttribute" or "AddComponentParameter" or "AddMultipleAttributes")
            {
                return true;
            }

            if (!method.Name.StartsWith("Invoke", StringComparison.Ordinal))
            {
                return false;
            }

            // An interface method, or an extension method whose receiver is one of the interfaces.
            ITypeSymbol? receiver = method.ReducedFrom is { } reduced ? reduced.Parameters[0].Type
                : method.IsExtensionMethod ? method.Parameters[0].Type
                : method.ContainingType;
            return IsOrImplements(receiver, jsRuntime) || IsOrImplements(receiver, jsObjectReference);
        }

        // A value stored into a dictionary of string to object: an indexer write, an Add or TryAdd argument, or the value of
        // a KeyValuePair<string, object>.
        private bool IsBagValue(IConversionOperation conversion)
        {
            switch (conversion.Parent)
            {
                case ISimpleAssignmentOperation assignment when ReferenceEquals(assignment.Value, conversion)
                    && assignment.Target is IPropertyReferenceOperation { Property.IsIndexer: true } indexer:
                    return IsStringToObject(indexer.Property.ContainingType);

                case IArgumentOperation { Parameter.Name: "value", Parent: IInvocationOperation invocation }
                    when invocation.TargetMethod.Name is "Add" or "TryAdd":
                    return IsStringToObject(invocation.TargetMethod.ContainingType);

                case IArgumentOperation { Parameter.Name: "value", Parent: IObjectCreationOperation creation }:
                    return creation.Type is INamedTypeSymbol created
                        && SymbolEqualityComparer.Default.Equals(created.OriginalDefinition, keyValuePair)
                        && IsStringAndObject(created);

                default:
                    return false;
            }
        }

        private bool IsStringToObject(ITypeSymbol? type)
        {
            if (type is not INamedTypeSymbol named)
            {
                return false;
            }

            foreach (INamedTypeSymbol candidate in named.AllInterfaces.Add(named))
            {
                INamedTypeSymbol definition = candidate.OriginalDefinition;
                if ((SymbolEqualityComparer.Default.Equals(definition, dictionary) || SymbolEqualityComparer.Default.Equals(definition, readOnlyDictionary))
                    && IsStringAndObject(candidate))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsStringAndObject(INamedTypeSymbol pair)
        {
            return pair.TypeArguments.Length == 2
                && pair.TypeArguments[0].SpecialType == SpecialType.System_String
                && pair.TypeArguments[1].SpecialType == SpecialType.System_Object;
        }

        private static bool IsOrImplements(ITypeSymbol? type, INamedTypeSymbol? contract)
        {
            if (type is null || contract is null)
            {
                return false;
            }

            if (SymbolEqualityComparer.Default.Equals(type, contract))
            {
                return true;
            }

            foreach (INamedTypeSymbol implemented in type.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(implemented, contract))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
