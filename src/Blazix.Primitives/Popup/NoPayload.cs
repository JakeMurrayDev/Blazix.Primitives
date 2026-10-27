namespace Blazix.Primitives;

// ADR 08 Amendments 2026-10-27, the probe's finding (issue 116): the popup parts are generic only, and a popup whose
// triggers carry no payload names this marker, TPayload="NoPayload", on its Root; its Triggers inherit it through
// [CascadingTypeParameter].
/// <summary>Represents the payload type of a popup whose triggers carry no payload.</summary>
/// <remarks>Write <c>TPayload="NoPayload"</c> on a popup's root; its triggers take the type from it.</remarks>
public sealed record NoPayload;
