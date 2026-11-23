using Microsoft.JSInterop;

namespace Blazix.Primitives.Interop;

// ADR 04 §1 (portal row) and Amendments 2026-10-27, decision 5: the DOM half of FloatingPortal and FloatingPortalLite. The
// module moves the node and owns the outside guards' listeners; it reports nothing yet (the focus-out close of FLT-KEY-07
// arrives with track 4's routing) and asks no decision point. One twin per part.
/// <summary>Represents the C# half of the <c>portal</c> module.</summary>
internal sealed class PortalTwin(IJSRuntime jsRuntime)
    : TwinBase<PortalTwin, PortalOptions>(jsRuntime, LibraryModule("portal"), BlazixJsonContext.Default.PortalOptions)
{
    /// <inheritdoc />
    protected override DecisionPolicy OnDecide(string point, EventSnapshot snapshot) => DecisionPolicy.Run;
}
