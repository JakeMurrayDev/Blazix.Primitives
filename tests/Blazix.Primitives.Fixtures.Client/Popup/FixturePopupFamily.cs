using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives.Fixtures.Client.PopupFixtures;

/// <summary>
/// The test-only popup family's store record at L4, the counterpart of the L2 project's <c>TestPopupStoreState</c>: the
/// shared state as its one popup member.
/// </summary>
[StoreState]
internal sealed partial record FixturePopupStoreState<TPayload>(PopupStoreState<TPayload> Popup)
    : IStoreState<FixturePopupStoreState<TPayload>>, IPopupStoreState<FixturePopupStoreState<TPayload>, TPayload>
{
    public static FixturePopupStoreState<TPayload> WithPopup(FixturePopupStoreState<TPayload> state, PopupStoreState<TPayload> popup) => state with { Popup = popup };
}

/// <summary>The test-only popup family's store.</summary>
internal sealed class FixturePopupStore<TPayload>(string? floatingId, string? sessionToken, bool inert = false, ILogger? logger = null)
    : PopupStore<FixturePopupStoreState<TPayload>, TPayload>(new FixturePopupStoreState<TPayload>(PopupStoreState<TPayload>.Initial(floatingId)), "FixturePopup", sessionToken, inert, logger);

/// <summary>The test-only popup family's handle, created with <see langword="new"/>.</summary>
/// <typeparam name="TPayload">The type of the payload the popup's triggers carry.</typeparam>
public sealed class FixturePopupHandle<TPayload>(bool throwOnMissingTrigger = true) : PopupHandle<TPayload>("FixturePopup", throwOnMissingTrigger)
{
    private protected override IPopupStore<TPayload> CreateFallbackStore() => new FixturePopupStore<TPayload>(floatingId: null, sessionToken: null, inert: true);
}

/// <summary>The state of the test-only trigger: whether the popup is open and owned by it.</summary>
[PartState]
public sealed partial record FixturePopupTriggerState(bool Open);

/// <summary>The state of the test-only popup: whether it is open (the transitions module writes the status attributes).</summary>
[PartState]
public sealed partial record FixturePopupPopupState(bool Open);
