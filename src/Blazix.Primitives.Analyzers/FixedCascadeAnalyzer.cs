using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Blazix.Primitives.Analyzers;

// ADR 05 §1; ADR 02 decision 4; ADR 16 rule 9
/// <summary>
/// Requires every <c>CascadingValue&lt;T&gt;</c> the library renders to set <c>IsFixed</c> to <see langword="true"/>, so that
/// change reaches parts by subscription only.
/// </summary>
/// <remarks>
/// The library renders from C#, so a cascade is an <c>OpenComponent&lt;CascadingValue&lt;T&gt;&gt;</c> (or
/// <c>OpenComponent(seq, typeof(CascadingValue&lt;T&gt;))</c>) on a <c>RenderTreeBuilder</c>. Between it and its
/// <c>CloseComponent</c>, at the same nesting depth and in the same block, an <c>AddComponentParameter</c> or
/// <c>AddAttribute</c> must set <c>IsFixed</c> to the constant <see langword="true"/>.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FixedCascadeAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Represents the id of the diagnostic this analyzer reports, <c>BLZX2001</c>.</summary>
    public const string DiagnosticId = "BLZX2001";

    // ADR 16 rule 9
    private static readonly DiagnosticDescriptor s_rule = new(
        DiagnosticId,
        "A library cascade must be fixed",
        "Blazix: `CascadingValue<{0}>` is rendered without `IsFixed` set to `true`; library cascades are fixed and change reaches parts by subscription",
        "Blazix.Primitives",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A CascadingValue whose value changes re-renders every subscriber, so the library cascades only fixed values (IsFixed set to true) and parts subscribe to the store or context they need.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(s_rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            INamedTypeSymbol? builder = start.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder");
            INamedTypeSymbol? cascadingValue = start.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.CascadingValue`1");
            if (builder is null || cascadingValue is null)
            {
                return;
            }

            start.RegisterOperationAction(operation => AnalyzeInvocation(operation, builder, cascadingValue), OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, INamedTypeSymbol builder, INamedTypeSymbol cascadingValue)
    {
        IInvocationOperation invocation = (IInvocationOperation)context.Operation;
        IMethodSymbol method = invocation.TargetMethod;
        if (method.Name != "OpenComponent" || !SymbolEqualityComparer.Default.Equals(method.ContainingType, builder))
        {
            return;
        }

        ITypeSymbol? valueType = CascadedValueType(invocation, method, cascadingValue);
        if (valueType is null || SetsIsFixedToTrue(invocation, builder))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            s_rule,
            invocation.Syntax.GetLocation(),
            valueType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }

    // The T of the CascadingValue<T> being opened, from OpenComponent<CascadingValue<T>>(seq) or OpenComponent(seq, typeof(CascadingValue<T>)).
    private static ITypeSymbol? CascadedValueType(IInvocationOperation invocation, IMethodSymbol method, INamedTypeSymbol cascadingValue)
    {
        ITypeSymbol? component = null;
        if (method.IsGenericMethod)
        {
            component = method.TypeArguments[0];
        }
        else
        {
            foreach (IArgumentOperation argument in invocation.Arguments)
            {
                if (Unwrap(argument.Value) is ITypeOfOperation typeOf)
                {
                    component = typeOf.TypeOperand;
                }
            }
        }

        return component is INamedTypeSymbol named && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, cascadingValue)
            ? named.TypeArguments[0]
            : null;
    }

    // Walks the statements after the OpenComponent in its block, on the same builder, until the matching CloseComponent.
    private static bool SetsIsFixedToTrue(IInvocationOperation open, INamedTypeSymbol builder)
    {
        if (open.Parent is not IExpressionStatementOperation statement || statement.Parent is not IBlockOperation block)
        {
            return false;
        }

        ISymbol? receiver = ReceiverSymbol(open.Instance);
        int depth = 0;
        bool after = false;
        foreach (IOperation operation in block.Operations)
        {
            if (!after)
            {
                after = ReferenceEquals(operation, statement);
                continue;
            }

            if (operation is not IExpressionStatementOperation { Operation: IInvocationOperation call }
                || !SymbolEqualityComparer.Default.Equals(call.TargetMethod.ContainingType, builder)
                || !SymbolEqualityComparer.Default.Equals(ReceiverSymbol(call.Instance), receiver))
            {
                continue;
            }

            string name = call.TargetMethod.Name;
            if (name.StartsWith("Open", StringComparison.Ordinal))
            {
                depth++;
            }
            else if (name.StartsWith("Close", StringComparison.Ordinal))
            {
                if (depth == 0)
                {
                    return false;
                }

                depth--;
            }
            else if (depth == 0 && (name == "AddComponentParameter" || name == "AddAttribute") && IsFixedTrue(call))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFixedTrue(IInvocationOperation call)
    {
        bool namesIsFixed = false;
        bool valueIsTrue = false;
        foreach (IArgumentOperation argument in call.Arguments)
        {
            Optional<object?> constant = Unwrap(argument.Value).ConstantValue;
            switch (argument.Parameter?.Name)
            {
                case "name":
                    namesIsFixed = constant.HasValue && constant.Value is "IsFixed";
                    break;
                case "value":
                    valueIsTrue = constant.HasValue && constant.Value is true;
                    break;
            }
        }

        return namesIsFixed && valueIsTrue;
    }

    private static ISymbol? ReceiverSymbol(IOperation? instance) => (instance is null ? null : Unwrap(instance)) switch
    {
        ILocalReferenceOperation local => local.Local,
        IParameterReferenceOperation parameter => parameter.Parameter,
        IFieldReferenceOperation field => field.Field,
        IPropertyReferenceOperation property => property.Property,
        _ => null,
    };

    private static IOperation Unwrap(IOperation operation)
    {
        while (operation is IConversionOperation conversion)
        {
            operation = conversion.Operand;
        }

        return operation;
    }
}
