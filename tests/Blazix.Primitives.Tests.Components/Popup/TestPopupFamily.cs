using Blazix.Primitives.Store;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The test-only popup family's store record (ADR 05 Amendments 2026-10-27): the shared state as its one popup member, so
/// no derived record is needed (BLZX1010 stays).
/// </summary>
[StoreState]
internal sealed partial record TestPopupStoreState<TPayload>(PopupStoreState<TPayload> Popup)
    : IStoreState<TestPopupStoreState<TPayload>>, IPopupStoreState<TestPopupStoreState<TPayload>, TPayload>
{
    public static TestPopupStoreState<TPayload> WithPopup(TestPopupStoreState<TPayload> state, PopupStoreState<TPayload> popup) => state with { Popup = popup };
}

/// <summary>The test-only family's store, the root's node in the family-agnostic floating tree (ADR 04 Amendments 2026-11-03 (5)).</summary>
internal sealed class TestPopupStore<TPayload>(string? floatingId, string? sessionToken, bool inert = false, ILogger? logger = null)
    : PopupStore<TestPopupStoreState<TPayload>, TPayload>(new TestPopupStoreState<TPayload>(PopupStoreState<TPayload>.Initial(floatingId)), "Test", sessionToken, inert, logger), IFloatingNodeContext;

/// <summary>The test-only family's handle, created with <see langword="new"/>; its fallback store is an inert test store.</summary>
public sealed class TestPopupHandle<TPayload>(bool throwOnMissingTrigger = true) : PopupHandle<TPayload>("Test", throwOnMissingTrigger)
{
    private protected override IPopupStore<TPayload> CreateFallbackStore() => new TestPopupStore<TPayload>(floatingId: null, sessionToken: null, inert: true);
}
