using Microsoft.JSInterop;

namespace Blazix.Primitives.Interop;

// ADR 04 §1 (scroll-lock row) and Amendments 2026-10-27: Base UI's useScrollLock (UTL §4.8). The module keeps the
// page-global lock count; a handle holds one acquisition while enabled. No report and no decision point. One twin per part.
/// <summary>Represents the C# half of the <c>scroll-lock</c> module.</summary>
internal sealed class ScrollLockTwin(IJSRuntime jsRuntime)
    : TwinBase<ScrollLockTwin, ScrollLockOptions>(jsRuntime, LibraryModule("scroll-lock"), BlazixJsonContext.Default.ScrollLockOptions)
{
    /// <inheritdoc />
    protected override DecisionPolicy OnDecide(string point, EventSnapshot snapshot) => DecisionPolicy.Run;
}
