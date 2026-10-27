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

/// <summary>The test-only family's store.</summary>
internal sealed class TestPopupStore<TPayload>(string? floatingId, string? sessionToken, bool inert = false, ILogger? logger = null)
    : PopupStore<TestPopupStoreState<TPayload>, TPayload>(new TestPopupStoreState<TPayload>(PopupStoreState<TPayload>.Initial(floatingId)), "Test", sessionToken, inert, logger);

/// <summary>The test-only family's handle, created with <see langword="new"/>; its fallback store is an inert test store.</summary>
internal sealed class TestPopupHandle<TPayload>(bool throwOnMissingTrigger = true) : PopupHandle<TPayload>("Test", throwOnMissingTrigger)
{
    private protected override IPopupStore<TPayload> CreateFallbackStore() => new TestPopupStore<TPayload>(floatingId: null, sessionToken: null, inert: true);
}
