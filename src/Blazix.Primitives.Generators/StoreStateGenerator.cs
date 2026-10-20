using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Blazix.Primitives.Generators;

// UTL-STA-10..12; ADR 05 §1, ADR 16 rule 1
/// <summary>
/// Generates, for every <c>sealed partial record</c> marked <c>[StoreState]</c>, the <c>Copy</c> and <c>ShallowEquals</c>
/// members that a store needs on its state record, and reports the diagnostics for records they cannot be generated into.
/// </summary>
/// <remarks>
/// <c>Copy</c> returns <c>state with { }</c>. <c>ShallowEquals</c> compares, in declaration order and stopping at the first
/// difference, every instance member that holds a value of the record, with a comparison written for the member's own type
/// so that nothing is boxed and no generic method is called. The consumer-facing diagnostics are BLZX1005 through BLZX1011.
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class StoreStateGenerator : IIncrementalGenerator
{
    private const string StoreStateAttributeName = "Blazix.Primitives.Store.StoreStateAttribute";
    private const string StoreStateInterfaceName = "Blazix.Primitives.Store.IStoreState<TSelf>";
    private const string Equality = "global::Blazix.Primitives.Store.Equality";
    private const string CopyName = "Copy";
    private const string ShallowEqualsName = "ShallowEquals";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<StoreStateOutput> outputs = context.SyntaxProvider.ForAttributeWithMetadataName(
            StoreStateAttributeName,
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

    private static StoreStateOutput Analyze(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        ImmutableArray<DiagnosticInfo>.Builder diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        TypeDeclarationSyntax node = (TypeDeclarationSyntax)context.TargetNode;
        INamedTypeSymbol symbol = (INamedTypeSymbol)context.TargetSymbol;
        string name = symbol.Name;
        Location identifierLocation = node.Identifier.GetLocation();

        if (!symbol.IsRecord || symbol.TypeKind != TypeKind.Class)
        {
            diagnostics.Add(DiagnosticInfo.Create(StoreStateDiagnostics.NotARecordClass, identifierLocation, name));
            return new StoreStateOutput(null, null, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));
        }

        ImmutableArray<TypeDeclarationSyntax> containers = PartialDeclarations.Containers(node);
        if (!PartialDeclarations.AllPartial(node, containers))
        {
            diagnostics.Add(DiagnosticInfo.Create(StoreStateDiagnostics.NotPartial, identifierLocation, name));
            return new StoreStateOutput(null, null, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));
        }

        // The remaining conditions are errors too, but the members are still generated so that the record's own
        // diagnostic is the only one reported rather than a cascade of unimplemented-member errors.
        if (!symbol.IsSealed)
        {
            diagnostics.Add(DiagnosticInfo.Create(StoreStateDiagnostics.NotSealed, identifierLocation, name));
        }

        if (symbol.BaseType is { SpecialType: not SpecialType.System_Object } baseType)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                StoreStateDiagnostics.DerivesFromRecord,
                identifierLocation,
                name,
                baseType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
        }

        if (!ImplementsStoreStateOfItself(symbol))
        {
            diagnostics.Add(DiagnosticInfo.Create(StoreStateDiagnostics.NotStoreState, identifierLocation, name));
        }

        bool emitCopy = true;
        bool emitShallowEquals = true;
        foreach (ISymbol member in symbol.GetMembers())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (member is not IMethodSymbol method || method.IsImplicitlyDeclared)
            {
                continue;
            }

            string? declared = IsDeclaration(method, symbol, CopyName, 1) ? CopyName
                : IsDeclaration(method, symbol, ShallowEqualsName, 2) ? ShallowEqualsName
                : null;
            if (declared is null)
            {
                continue;
            }

            emitCopy &= declared != CopyName;
            emitShallowEquals &= declared != ShallowEqualsName;
            Location location = method.Locations.Length > 0 ? method.Locations[0] : identifierLocation;
            diagnostics.Add(DiagnosticInfo.Create(StoreStateDiagnostics.DeclaresGeneratedMember, location, name, declared));
        }

        string fullName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        string displayName = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        StringBuilder members = new();
        if (emitCopy)
        {
            members.Append("/// <inheritdoc />\n");
            members.Append("public static ").Append(fullName).Append(" Copy(").Append(fullName).Append(" state) => state with { };\n");
        }

        if (emitShallowEquals)
        {
            if (emitCopy)
            {
                members.Append('\n');
            }

            members.Append("/// <inheritdoc />\n");
            members.Append("public static bool ShallowEquals(").Append(fullName).Append(" left, ").Append(fullName).Append(" right) =>");
            ImmutableArray<ISymbol> compared = ComparedMembers(symbol);
            if (compared.IsEmpty)
            {
                members.Append(" true;\n");
            }
            else
            {
                for (int i = 0; i < compared.Length; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ISymbol member = compared[i];
                    ITypeSymbol type = MemberType(member);
                    string memberName = Identifier(member.Name);
                    string? comparison = Comparison(type, "left." + memberName, "right." + memberName);
                    if (comparison is null)
                    {
                        Location location = member.Locations.Length > 0 ? member.Locations[0] : identifierLocation;
                        diagnostics.Add(DiagnosticInfo.Create(
                            StoreStateDiagnostics.BoxedMember,
                            location,
                            member.Name,
                            name,
                            type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
                        comparison = Equality + ".Is(left." + memberName + ", right." + memberName + ")";
                    }

                    members.Append('\n').Append(i == 0 ? "    " : "    && ").Append(comparison);
                }

                members.Append(";\n");
            }
        }

        string source = Compose(symbol, node, containers, displayName, members.ToString());
        string hintName = PartialDeclarations.HintName(displayName, ".StoreState.g.cs");
        return new StoreStateOutput(hintName, source, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));
    }

    private static bool ImplementsStoreStateOfItself(INamedTypeSymbol symbol)
    {
        foreach (INamedTypeSymbol implemented in symbol.AllInterfaces)
        {
            if (IsStoreStateInterface(implemented) && SymbolEqualityComparer.Default.Equals(implemented.TypeArguments[0], symbol))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsStoreStateInterface(INamedTypeSymbol type) =>
        type.OriginalDefinition.ToDisplayString() == StoreStateInterfaceName;

    // A method the generated member would collide with: same name and parameter types (static or not, CS0111), or an
    // explicit implementation of the interface member.
    private static bool IsDeclaration(IMethodSymbol method, INamedTypeSymbol record, string memberName, int parameterCount)
    {
        foreach (IMethodSymbol implemented in method.ExplicitInterfaceImplementations)
        {
            if (implemented.Name == memberName && IsStoreStateInterface(implemented.ContainingType))
            {
                return true;
            }
        }

        if (method.Name != memberName || method.Parameters.Length != parameterCount)
        {
            return false;
        }

        foreach (IParameterSymbol parameter in method.Parameters)
        {
            if (!SymbolEqualityComparer.Default.Equals(parameter.Type, record))
            {
                return false;
            }
        }

        return true;
    }

    // Every instance field of the record, which is what `with` copies, in declaration order. The backing field of a
    // positional, auto-implemented or init-only property is read through its property; a computed property has no field
    // and a static or constant field holds no per-record value, so neither takes part.
    private static ImmutableArray<ISymbol> ComparedMembers(INamedTypeSymbol symbol)
    {
        return symbol.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(static field => !field.IsStatic && !field.IsConst)
            .Select(static field => field.AssociatedSymbol ?? field)
            .OrderBy(static member => member.Locations.Length > 0 ? member.Locations[0].SourceTree?.FilePath ?? string.Empty : string.Empty, StringComparer.Ordinal)
            .ThenBy(static member => member.Locations.Length > 0 ? member.Locations[0].SourceSpan.Start : 0)
            .ToImmutableArray();
    }

    private static ITypeSymbol MemberType(ISymbol member) => member switch
    {
        IPropertySymbol property => property.Type,
        IEventSymbol @event => @event.Type,
        _ => ((IFieldSymbol)member).Type,
    };

    // The store's equality (JavaScript's Object.is by the static type: value types by value with NaN equal to NaN and 0
    // different from -0, strings by ordinal value, every other reference type by reference) written for the member's own
    // type, so that the comparison is neither a generic call nor an EqualityComparer<T> call, both of which box a value
    // type on the WebAssembly interpreter. Null when a struct has neither an == operator nor IEquatable<T>.
    private static string? Comparison(ITypeSymbol type, string left, string right)
    {
        if (type is ITypeParameterSymbol)
        {
            // How a type parameter compares is decided only at instantiation.
            return Equality + ".Is(" + left + ", " + right + ")";
        }

        if (type.SpecialType == SpecialType.System_String)
        {
            return "string.Equals(" + left + ", " + right + ")";
        }

        if (type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer)
        {
            return left + " == " + right;
        }

        if (!type.IsValueType)
        {
            return "object.ReferenceEquals(" + left + ", " + right + ")";
        }

        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
        {
            ITypeSymbol underlying = nullable.TypeArguments[0];
            string leftValue = left + ".GetValueOrDefault()";
            string rightValue = right + ".GetValueOrDefault()";
            string? values = SameFloatingPoint(underlying, leftValue, rightValue);
            if (values is null)
            {
                if (HasEqualityOperator(underlying))
                {
                    return left + " == " + right;
                }

                if (!IsEquatable(underlying))
                {
                    return null;
                }

                values = leftValue + ".Equals(" + rightValue + ")";
            }

            return "(" + left + ".HasValue ? " + right + ".HasValue && " + values + " : !" + right + ".HasValue)";
        }

        return SameFloatingPoint(type, left, right)
            ?? (HasEqualityOperator(type) ? left + " == " + right
            : IsEquatable(type) ? left + ".Equals(" + right + ")"
            : null);
    }

    // Object.is on IEEE values: identical bits, or both NaN (JavaScript has one NaN, so the payload does not matter).
    private static string? SameFloatingPoint(ITypeSymbol type, string left, string right)
    {
        (string Bits, string IsNaN)? functions = type.SpecialType switch
        {
            SpecialType.System_Double => ("global::System.BitConverter.DoubleToInt64Bits", "double.IsNaN"),
            SpecialType.System_Single => ("global::System.BitConverter.SingleToInt32Bits", "float.IsNaN"),
            _ when type.ToDisplayString() == "System.Half" => ("global::System.BitConverter.HalfToInt16Bits", "global::System.Half.IsNaN"),
            _ => null,
        };
        if (functions is not { } f)
        {
            return null;
        }

        return "(" + f.Bits + "(" + left + ") == " + f.Bits + "(" + right + ") || (" + f.IsNaN + "(" + left + ") && " + f.IsNaN + "(" + right + ")))";
    }

    private static bool HasEqualityOperator(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Enum)
        {
            return true;
        }

        switch (type.SpecialType)
        {
            case SpecialType.System_Boolean:
            case SpecialType.System_Char:
            case SpecialType.System_SByte:
            case SpecialType.System_Byte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_Decimal:
            case SpecialType.System_IntPtr:
            case SpecialType.System_UIntPtr:
                return true;
        }

        foreach (ISymbol member in type.GetMembers("op_Equality"))
        {
            if (member is IMethodSymbol { MethodKind: MethodKind.UserDefinedOperator, Parameters.Length: 2 } method
                && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, type)
                && SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, type))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEquatable(ITypeSymbol type)
    {
        foreach (INamedTypeSymbol implemented in type.AllInterfaces)
        {
            if (implemented.OriginalDefinition.ToDisplayString() == "System.IEquatable<T>"
                && SymbolEqualityComparer.Default.Equals(WithoutElementNames(implemented.TypeArguments[0]), WithoutElementNames(type)))
            {
                return true;
            }
        }

        return false;
    }

    // A tuple type with element names, (int Row, int Column), implements IEquatable of the unnamed ValueTuple<int, int>.
    private static ITypeSymbol WithoutElementNames(ITypeSymbol type) =>
        type is INamedTypeSymbol { IsTupleType: true, TupleUnderlyingType: { } underlying } ? underlying : type;

    private static string Identifier(string name) =>
        SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;

    private static string Compose(INamedTypeSymbol symbol, TypeDeclarationSyntax node, ImmutableArray<TypeDeclarationSyntax> containers, string displayName, string members)
    {
        StringBuilder source = new();
        source.Append("// <auto-generated/>\n");
        source.Append("// Copy and ShallowEquals of ").Append(displayName).Append(" for its store, emitted by Blazix.Primitives.Generators.\n");
        source.Append("#nullable enable\n\n");
        string indent = PartialDeclarations.OpenContainers(source, symbol, containers);
        source.Append(indent).Append("partial record ").Append(node.Identifier.ValueText).Append(node.TypeParameterList?.ToString() ?? string.Empty).Append('\n');
        source.Append(indent).Append("{\n");
        source.Append(PartialDeclarations.Indent(members, indent + "    "));
        source.Append(indent).Append("}\n");
        PartialDeclarations.CloseContainers(source, containers);
        return source.ToString();
    }

    private sealed record StoreStateOutput(string? HintName, string? Source, EquatableArray<DiagnosticInfo> Diagnostics);
}
