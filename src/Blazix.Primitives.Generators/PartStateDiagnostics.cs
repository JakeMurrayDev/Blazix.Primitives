using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Blazix.Primitives.Generators;

/// <summary>The consumer-facing diagnostics of the <c>[PartState]</c> generator. Every id has a test in <c>Tests.Generators</c>.</summary>
internal static class PartStateDiagnostics
{
    private const string Category = "Blazix.Primitives";

    public static readonly DiagnosticDescriptor NotARecordClass = new(
        "BLZX1001",
        "A [PartState] type must be a sealed partial record class",
        "'{0}' is marked [PartState] but is not a record class; declare it as a sealed partial record",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    // ADR 08 §3
    public static readonly DiagnosticDescriptor NotSealed = new(
        "BLZX1002",
        "A [PartState] record must be sealed",
        "'{0}' is marked [PartState] but is not sealed; a state record is a leaf type that nothing derives from",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NotPrimitiveProperty = new(
        "BLZX1003",
        "A [PartState] property must be projectable",
        "Property '{0}' of [PartState] record '{1}' has type '{2}', which cannot be projected to a data-* attribute; use a bool, number, string or enum (or their nullable forms), project it through a custom mapping on a primitive field, or mark it [MirroredField] to keep it out of the projection",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NotPartial = new(
        "BLZX1004",
        "A [PartState] record and its containing types must be partial",
        "'{0}' is marked [PartState] but it or one of its containing types is not partial, so the projection cannot be generated into it",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}

/// <summary>A diagnostic to report, in an equatable form the incremental pipeline can cache.</summary>
internal sealed record DiagnosticInfo(
    string Id,
    string FilePath,
    int SpanStart,
    int SpanLength,
    int StartLine,
    int StartCharacter,
    int EndLine,
    int EndCharacter,
    string Argument0,
    string Argument1,
    string Argument2)
{
    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, Location location, string argument0, string argument1 = "", string argument2 = "")
    {
        FileLinePositionSpan lineSpan = location.GetLineSpan();
        return new DiagnosticInfo(
            descriptor.Id,
            location.SourceTree?.FilePath ?? string.Empty,
            location.SourceSpan.Start,
            location.SourceSpan.Length,
            lineSpan.StartLinePosition.Line,
            lineSpan.StartLinePosition.Character,
            lineSpan.EndLinePosition.Line,
            lineSpan.EndLinePosition.Character,
            argument0,
            argument1,
            argument2);
    }

    public Diagnostic ToDiagnostic()
    {
        DiagnosticDescriptor descriptor = Id switch
        {
            "BLZX1001" => PartStateDiagnostics.NotARecordClass,
            "BLZX1002" => PartStateDiagnostics.NotSealed,
            "BLZX1003" => PartStateDiagnostics.NotPrimitiveProperty,
            _ => PartStateDiagnostics.NotPartial,
        };
        Location location = Location.Create(
            FilePath,
            new TextSpan(SpanStart, SpanLength),
            new LinePositionSpan(new LinePosition(StartLine, StartCharacter), new LinePosition(EndLine, EndCharacter)));
        return Id == "BLZX1003"
            ? Diagnostic.Create(descriptor, location, Argument0, Argument1, Argument2)
            : Diagnostic.Create(descriptor, location, Argument0);
    }
}
