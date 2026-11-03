using Blazix.Primitives.Interop;
using Blazix.Primitives.Store;
using Microsoft.Extensions.Logging;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests;

/// <summary>
/// A test-only store record built on the open lifecycle alone (ADR 05 Amendments 2026-11-03): the lifecycle as its one
/// member, plus a family field the open-change sequence writes, as Select's, Combobox's and NavigationMenu's records will.
/// </summary>
[StoreState]
internal sealed partial record TestLifecycleStoreState(PopupLifecycleState Lifecycle, string? LastReason)
    : IStoreState<TestLifecycleStoreState>, IPopupLifecycleState<TestLifecycleStoreState>
{
    public static TestLifecycleStoreState Initial(string? floatingId = null) => new(PopupLifecycleState.Initial(floatingId), null);

    public static TestLifecycleStoreState WithLifecycle(TestLifecycleStoreState state, in PopupLifecycleState lifecycle) => state with { Lifecycle = lifecycle };
}

/// <summary>The test-only lifecycle store: records the order of the open-change sequence's steps.</summary>
internal class TestLifecycleStore(string? floatingId = null, bool inert = false, ILogger? logger = null)
    : PopupLifecycleStore<TestLifecycleStoreState>(TestLifecycleStoreState.Initial(floatingId), "Test", "token-1", inert, logger)
{
    public List<string> Order { get; } = [];

    protected override void BeforeDispatch(bool open, OpenChangeEventDetails details) => Order.Add("onBeforeDispatch");

    protected override void OnOpenChangeDispatched(OpenChangeRelay relay) => Order.Add("dispatchOpenChange");

    protected override TestLifecycleStoreState ApplyOpenChange(TestLifecycleStoreState state, bool open, OpenChangeEventDetails details)
    {
        Order.Add("update");
        return state with { LastReason = details.Reason.ToLiteral() };
    }

    protected override void OnUnmount() => Order.Add("onUnmount");
}

/// <summary>
/// A test-only store whose open state is derived from its own value, as NavigationMenu's is (<c>open = value != null</c>,
/// ADR 08 Amendments 2026-11-03 (2)): its consumer step runs the family's own value callback, with cancel, and no
/// <see cref="Controlled{T}"/> of the lifecycle's.
/// </summary>
internal sealed class TestDerivedLifecycleStore() : TestLifecycleStore()
{
    public string? Value { get; private set; }

    public string? RequestedValue { get; set; }

    public Func<string?, OpenChangeEventDetails, bool>? OnValueChange { get; set; }

    protected override Task<bool> RunConsumerStepAsync(bool open, OpenChangeEventDetails details)
    {
        Order.Add("onValueChange");
        string? next = open ? RequestedValue : null;
        if (OnValueChange is not null && !OnValueChange(next, details))
        {
            details.Cancel();
        }

        if (details.IsCanceled)
        {
            return Task.FromResult(false);
        }

        Value = next;
        return Task.FromResult(true);
    }
}
