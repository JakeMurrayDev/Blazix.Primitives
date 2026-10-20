namespace Blazix.Primitives.Interop;

// ADR 04 §3 (options records), ADR 04 Amendments 2026-10-18 (handover at attach).
/// <summary>
/// The base of every twin options record. A twin serialises its options record to JSON and hands it to the
/// JavaScript module on <c>attach</c> and on every <c>update</c>.
/// </summary>
internal abstract record TwinOptions
{
    /// <summary>
    /// Gets the consumer's merged style string. Once <c>attach</c> has succeeded, JavaScript owns
    /// <c>element.style</c> and composes it from this value plus the module's own declarations.
    /// </summary>
    /// <value>The style string to apply to the host element, or <see langword="null"/> for none.</value>
    public string? Style { get; init; }
}
