namespace Blazix.Primitives.Interop;

// ADR 04 Amendments 2026-10-27 (the `popup` row): the Viewport's direction and transitioning flag are the module's writes,
// mirrored in C#'s state record (the RND-STA-01 row of ADR 01). PSH-DOM-07, PSH-DOM-08.
/// <summary>The viewport's mirrored values, reported once per change.</summary>
/// <param name="ActivationDirection">The direction from the previous trigger to the new one, or <see langword="null"/>.</param>
/// <param name="Transitioning">Whether the previous content is shown.</param>
internal sealed record PopupViewportReport(string? ActivationDirection, bool Transitioning);

/// <summary>A focused trigger guard, reported on Server after the module moved focus.</summary>
/// <param name="Guard">Which guard: <c>before</c> or <c>after</c> the trigger.</param>
/// <param name="Event">The snapshot of the focus event.</param>
internal sealed record PopupGuardFocus(string Guard, EventSnapshot Event);

/// <summary>The guard handed over just before the <c>guard</c> decision on WebAssembly.</summary>
/// <param name="Guard">Which guard: <c>before</c> or <c>after</c> the trigger.</param>
internal sealed record PopupGuardHandover(string Guard);
