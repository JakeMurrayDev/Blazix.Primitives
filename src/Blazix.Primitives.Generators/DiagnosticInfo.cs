using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Blazix.Primitives.Generators;

/// <summary>Represents a diagnostic to report, in an equatable form that the incremental pipeline can cache.</summary>
/// <param name="Descriptor">The descriptor of the diagnostic.</param>
/// <param name="FilePath">The path of the source file that contains the diagnostic location.</param>
/// <param name="Span">The text span of the location.</param>
/// <param name="LineSpan">The line and character positions of the location.</param>
/// <param name="Arguments">The message format arguments, in order.</param>
/// <remarks>It holds no symbol and no syntax tree, so a cached model that contains it keeps neither alive.</remarks>
internal sealed record DiagnosticInfo(
    DiagnosticDescriptor Descriptor,
    string FilePath,
    TextSpan Span,
    LinePositionSpan LineSpan,
    EquatableArray<string> Arguments)
{
    /// <summary>Creates a <see cref="DiagnosticInfo"/> for the specified descriptor at the specified location.</summary>
    /// <param name="descriptor">The descriptor of the diagnostic to report.</param>
    /// <param name="location">The location to report the diagnostic at.</param>
    /// <param name="arguments">The message format arguments, in order.</param>
    /// <returns>The equatable form of the diagnostic.</returns>
    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, Location location, params string[] arguments)
    {
        return new DiagnosticInfo(
            descriptor,
            location.SourceTree?.FilePath ?? string.Empty,
            location.SourceSpan,
            location.GetLineSpan().Span,
            new EquatableArray<string>(ImmutableArray.Create(arguments)));
    }

    /// <summary>Converts this instance back to a <see cref="Diagnostic"/> that can be reported.</summary>
    /// <returns>The diagnostic, with its descriptor, location and message arguments restored.</returns>
    public Diagnostic ToDiagnostic()
    {
        object[] arguments = new object[Arguments.Count];
        for (int i = 0; i < arguments.Length; i++)
        {
            arguments[i] = Arguments[i];
        }

        return Diagnostic.Create(Descriptor, Location.Create(FilePath, Span, LineSpan), arguments);
    }
}
