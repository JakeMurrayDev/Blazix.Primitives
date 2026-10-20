using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Blazix.Primitives.Generators;

/// <summary>Provides the consumer-facing diagnostics that the <see cref="PartStateGenerator"/> reports.</summary>
/// <remarks>Every diagnostic id has a test in <c>Tests.Generators</c>.</remarks>
internal static class PartStateDiagnostics
{
    private const string Category = "Blazix.Primitives";

    /// <summary>The BLZX1001 diagnostic, reported when a <c>[PartState]</c> type is not a record class.</summary>
    public static readonly DiagnosticDescriptor NotARecordClass = new(
        "BLZX1001",
        "A [PartState] type must be a sealed partial record class",
        "'{0}' is marked [PartState] but is not a record class; declare it as a sealed partial record",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    // ADR 08 §3
    /// <summary>The BLZX1002 diagnostic, reported when a <c>[PartState]</c> record is not sealed.</summary>
    public static readonly DiagnosticDescriptor NotSealed = new(
        "BLZX1002",
        "A [PartState] record must be sealed",
        "'{0}' is marked [PartState] but is not sealed; a state record is a leaf type that nothing derives from",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>The BLZX1003 diagnostic, reported when a property of a <c>[PartState]</c> record has a type that cannot be projected.</summary>
    public static readonly DiagnosticDescriptor NotPrimitiveProperty = new(
        "BLZX1003",
        "A [PartState] property must be projectable",
        "Property '{0}' of [PartState] record '{1}' has type '{2}', which cannot be projected to a data-* attribute; use a bool, number, string or enum (or their nullable forms), project it through a custom mapping on a primitive field, or mark it [MirroredField] to keep it out of the projection",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>The BLZX1004 diagnostic, reported when a <c>[PartState]</c> record or one of its containing types is not partial.</summary>
    public static readonly DiagnosticDescriptor NotPartial = new(
        "BLZX1004",
        "A [PartState] record and its containing types must be partial",
        "'{0}' is marked [PartState] but it or one of its containing types is not partial, so the projection cannot be generated into it",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}

/// <summary>Represents a diagnostic to report, in an equatable form that the incremental pipeline can cache.</summary>
/// <param name="Id">The diagnostic id, such as <c>BLZX1001</c>.</param>
/// <param name="FilePath">The path of the source file that contains the diagnostic location.</param>
/// <param name="SpanStart">The start of the location's text span.</param>
/// <param name="SpanLength">The length of the location's text span.</param>
/// <param name="StartLine">The zero-based line on which the location starts.</param>
/// <param name="StartCharacter">The zero-based character on which the location starts.</param>
/// <param name="EndLine">The zero-based line on which the location ends.</param>
/// <param name="EndCharacter">The zero-based character on which the location ends.</param>
/// <param name="Argument0">The first message format argument.</param>
/// <param name="Argument1">The second message format argument, or an empty string.</param>
/// <param name="Argument2">The third message format argument, or an empty string.</param>
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
    /// <summary>Creates a <see cref="DiagnosticInfo"/> for the specified descriptor at the specified location.</summary>
    /// <param name="descriptor">The descriptor of the diagnostic to report.</param>
    /// <param name="location">The location to report the diagnostic at.</param>
    /// <param name="argument0">The first message format argument.</param>
    /// <param name="argument1">The second message format argument, if the message has one.</param>
    /// <param name="argument2">The third message format argument, if the message has one.</param>
    /// <returns>The equatable form of the diagnostic.</returns>
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

    /// <summary>Converts this instance back to a <see cref="Diagnostic"/> that can be reported.</summary>
    /// <returns>The diagnostic, with its descriptor, location and message arguments restored.</returns>
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
