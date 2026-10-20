namespace Blazix.Primitives.Interop;

// UBT-ALG-06, UBT-EVT-07/08
/// <summary>The payload of the <c>button</c> module's interaction report: how a press happened.</summary>
/// <param name="InteractionType">
/// <c>mouse</c>, <c>touch</c>, <c>pen</c>, <c>keyboard</c>, or the empty string when the pointer type is unknown.
/// </param>
/// <param name="Event">The snapshot of the <c>pointerdown</c> or <c>click</c> that was classified.</param>
internal sealed record ButtonInteraction(string InteractionType, EventSnapshot Event);

// UBT-EVT-10, UBT-PTR-12, UBT-PTR-17
/// <summary>The payload of the press-and-hold reports: the event of one tick, of the release or of a single-action click.</summary>
/// <param name="Event">
/// For a tick, the snapshot of the event that started the sequence; for the release, the <c>pointerup</c>; for a
/// click, the click.
/// </param>
internal sealed record ButtonHoldEvent(EventSnapshot Event);

// UBT-EDGE-01, UBT-EDGE-02
/// <summary>The payload of the development check that found the element not matching the declared mode.</summary>
/// <param name="ExpectedNative">
/// <see langword="true"/> when native mode was declared and the element is not a <c>&lt;button&gt;</c>;
/// <see langword="false"/> when non-native mode was declared and the element is one.
/// </param>
internal sealed record NativeButtonMismatch(bool ExpectedNative);
