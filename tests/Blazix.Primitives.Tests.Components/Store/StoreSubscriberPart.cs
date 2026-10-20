using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Components.Store;

/// <summary>A store record with two members, so a write can change one selection and leave the other alone.</summary>
public sealed record CounterStoreState(int Count, int Multiplier) : IStoreState<CounterStoreState>
{
    public static readonly StoreField<CounterStoreState, int> CountField = new(nameof(Count), s => s.Count, (s, v) => s with { Count = v });
    public static readonly StoreField<CounterStoreState, int> MultiplierField = new(nameof(Multiplier), s => s.Multiplier, (s, v) => s with { Multiplier = v });

    public static readonly Func<CounterStoreState, int> CountSelector = s => s.Count;
    public static readonly Func<CounterStoreState, bool> IsEven = s => s.Count % 2 == 0;

    /// <inheritdoc />
    public static CounterStoreState Copy(CounterStoreState state) => state with { };

    /// <inheritdoc />
    public static bool ShallowEquals(CounterStoreState left, CounterStoreState right) =>
        Equality.Is(left.Count, right.Count) && Equality.Is(left.Multiplier, right.Multiplier);
}

/// <summary>The state of <see cref="StoreSubscriberPart"/>: the two selections it reads.</summary>
[PartState]
public sealed partial record StoreSubscriberPartState(int Count, bool Even);

/// <summary>
/// A part that reads two selections of a cascaded-style store passed as a parameter (<c>count</c> and <c>count is even</c>)
/// through <see cref="PartBase{TState}.SubscribeTo{TStoreState}"/>, and renders the count as its content.
/// </summary>
public sealed class StoreSubscriberPart : PartBase<StoreSubscriberPartState>
{
    private StoreSelection<CounterStoreState, int>? count;
    private StoreSelection<CounterStoreState, bool>? even;
    private StoreSubscriberPartState? state;

    [Parameter, EditorRequired]
    public ComponentStore<CounterStoreState> Store { get; set; } = null!;

    /// <summary>When set, <see cref="SubscribeSelectors"/> subscribes twice, which must throw.</summary>
    [Parameter]
    public bool SubscribeTwice { get; set; }

    /// <summary>How many times the <c>count</c> selection was evaluated: once at subscription, then once per store notification.</summary>
    public int CountEvaluations { get; private set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "output";

    /// <inheritdoc />
    protected override StoreSubscriberPartState State
    {
        get
        {
            if (state is null || state.Count != count!.Value || state.Even != even!.Value)
            {
                state = new StoreSubscriberPartState(count!.Value, even!.Value);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors()
    {
        SelectorSubscription<CounterStoreState> subscription = SubscribeTo(Store);
        count = subscription.Select((CounterStoreState s) =>
        {
            CountEvaluations++;
            return CounterStoreState.CountSelector(s);
        });
        even = subscription.Select(CounterStoreState.IsEven);
        if (SubscribeTwice)
        {
            SubscribeTo(Store);
        }
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ChildContent = builder => builder.AddContent(0, count!.Value);
    }
}
