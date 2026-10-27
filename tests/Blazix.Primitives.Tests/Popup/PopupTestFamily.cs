using Blazix.Primitives.Interop;
using Blazix.Primitives.Store;
using Microsoft.Extensions.Logging;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests;

/// <summary>
/// A test-only popup family's store record (ADR 05 Amendments 2026-10-27): the shared state as its one popup member, plus
/// a family field the open-change sequence writes, which proves the shape with no derived record (BLZX1010 stays).
/// </summary>
[StoreState]
internal sealed partial record TestFamilyStoreState<TPayload>(PopupStoreState<TPayload> Popup, string? LastReason)
    : IStoreState<TestFamilyStoreState<TPayload>>, IPopupStoreState<TestFamilyStoreState<TPayload>, TPayload>
{
    public static TestFamilyStoreState<TPayload> Initial(string? floatingId = null) => new(PopupStoreState<TPayload>.Initial(floatingId), null);

    public static TestFamilyStoreState<TPayload> WithPopup(TestFamilyStoreState<TPayload> state, PopupStoreState<TPayload> popup) => state with { Popup = popup };
}

/// <summary>The test-only family's store: records the order of the open-change sequence's steps.</summary>
internal sealed class TestFamilyStore<TPayload>(string? floatingId = null, bool inert = false, ILogger? logger = null)
    : PopupStore<TestFamilyStoreState<TPayload>, TPayload>(TestFamilyStoreState<TPayload>.Initial(floatingId), "Test", "token-1", inert, logger)
{
    public List<string> Order { get; } = [];

    public Action? OnBeforeDispatch { get; set; }

    protected override void BeforeDispatch(bool open, OpenChangeEventDetails details)
    {
        Order.Add("onBeforeDispatch");
        OnBeforeDispatch?.Invoke();
    }

    protected override void OnOpenChangeDispatched(OpenChangeRelay relay) => Order.Add("dispatchOpenChange");

    protected override TestFamilyStoreState<TPayload> ApplyOpenChange(TestFamilyStoreState<TPayload> state, bool open, OpenChangeEventDetails details)
    {
        Order.Add("update");
        return state with { LastReason = details.Reason.ToLiteral() };
    }
}

/// <summary>A store a handle points at, recording the open-change requests it receives (Base UI's FakeStore).</summary>
internal sealed class FakePopupStore<TPayload>(ILogger? logger = null) : IPopupStore<TPayload>
{
    public List<(bool Open, OpenChangeEventDetails Details)> Requests { get; } = [];

    public List<TPayload?> Payloads { get; } = [];

    public bool IsOpen { get; set; }

    public PopupTriggerRegistry Triggers { get; } = new();

    public ILogger? Logger => logger;

    public Task<bool> RequestOpenAsync(bool open, OpenChangeEventDetails details)
    {
        Requests.Add((open, details));
        return Task.FromResult(true);
    }

    public void SetPayload(TPayload? payload) => Payloads.Add(payload);
}

/// <summary>A test-only handle over fake stores (Base UI's TestHandle): its fallback store is a <see cref="FakePopupStore{TPayload}"/>.</summary>
internal sealed class FakeStoreHandle<TPayload>(bool throwOnMissingTrigger = true, string componentName = "Test") : PopupHandle<TPayload>(componentName, throwOnMissingTrigger)
{
    public FakePopupStore<TPayload> Fallback => (FakePopupStore<TPayload>)FallbackStore;

    private protected override IPopupStore<TPayload> CreateFallbackStore() => new FakePopupStore<TPayload>();
}
