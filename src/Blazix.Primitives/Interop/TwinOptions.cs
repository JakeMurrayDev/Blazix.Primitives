namespace Blazix.Primitives.Interop;

/// <summary>
/// The base of every twin options record (ADR 04 §3). Options are plain records serialised to JSON by the
/// library's source-generated context and handed to the module on <c>attach</c> and on every <c>update</c>.
/// </summary>
internal abstract record TwinOptions
{
    /// <summary>
    /// The consumer's merged style string. After <c>attach</c> succeeds JavaScript owns <c>element.style</c> and
    /// composes it from this value plus its own declarations (handover at attach, ADR 04 Amendments 2026-10-18).
    /// </summary>
    public string? Style { get; init; }
}
