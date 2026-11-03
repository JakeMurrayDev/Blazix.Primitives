using Blazix.Primitives.Interop;
using Blazix.Primitives.Store;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The standalone test host's store record (ADR 05 Amendments 2026-11-03): the open lifecycle as its one member, with no
/// trigger registry or payload, as Select's, Combobox's and NavigationMenu's records will hold it.
/// </summary>
[StoreState]
internal sealed partial record TestLifecycleStoreState(PopupLifecycleState Lifecycle)
    : IStoreState<TestLifecycleStoreState>, IPopupLifecycleState<TestLifecycleStoreState>
{
    public static readonly Func<TestLifecycleStoreState, bool> Open = static state => state.Lifecycle.EffectiveOpen;

    public static readonly Func<TestLifecycleStoreState, bool> Mounted = static state => state.Lifecycle.Mounted;

    public static readonly Func<TestLifecycleStoreState, TransitionStatus?> Status = static state => state.Lifecycle.DerivedTransitionStatus;

    public static readonly Func<TestLifecycleStoreState, int> UnmountRequests = static state => state.Lifecycle.UnmountRequests;

    public static readonly Func<TestLifecycleStoreState, OpenChangeRelay?> OpenChange = static state => state.Lifecycle.OpenChange;

    public static TestLifecycleStoreState WithLifecycle(TestLifecycleStoreState state, in PopupLifecycleState lifecycle) => state with { Lifecycle = lifecycle };
}

/// <summary>
/// The standalone host's store. With <see cref="ValueStep"/> set, its open state is derived from a family-owned value, as
/// NavigationMenu's is: the step replaces the lifecycle's <see cref="Controlled{T}"/> request and may cancel.
/// </summary>
internal sealed class TestLifecycleStore(string? floatingId, string? sessionToken, ILogger? logger = null)
    : PopupLifecycleStore<TestLifecycleStoreState>(new TestLifecycleStoreState(PopupLifecycleState.Initial(floatingId)), "Test", sessionToken, inert: false, logger)
{
    public Func<bool, OpenChangeEventDetails, bool>? ValueStep { get; set; }

    protected override Task<bool> RunConsumerStepAsync(bool open, OpenChangeEventDetails details)
    {
        if (ValueStep is null)
        {
            return base.RunConsumerStepAsync(open, details);
        }

        if (!ValueStep(open, details))
        {
            details.Cancel();
        }

        return Task.FromResult(!details.IsCanceled);
    }
}
