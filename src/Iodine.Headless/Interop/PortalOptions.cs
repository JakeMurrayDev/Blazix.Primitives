namespace Blazix.Primitives.Interop;

// ADR 04 §1 (portal row), §5 and Amendments 2026-10-27, decision 5 (chosen by the user 2026-10-27): the container crosses as
// the index of its reference in the references array (an ElementReference or a script-held IJSObjectReference, the
// element-typed options of the same amendment, settled by the orchestrator 2026-10-27), or as a selector; with neither, the
// module resolves the wrapper document's body. Style is the consumer's, which the module owns from attach (the handover).
/// <summary>Holds the options of the <c>portal</c> module's handle.</summary>
internal sealed record PortalOptions : TwinOptions
{
    /// <summary>Gets the index of the container in the references array, or <see langword="null"/> for none.</summary>
    public int? ContainerRef { get; init; }

    /// <summary>Gets the CSS selector of the container, or <see langword="null"/> for none.</summary>
    public string? ContainerSelector { get; init; }

    /// <summary>Gets a value indicating whether the portal is the lite portal, with no outside guards and no owner.</summary>
    public bool Lite { get; init; }
}
