using Bunit;
using Blazix.Primitives.Store;
using Microsoft.Extensions.DependencyInjection;

namespace Blazix.Primitives.Tests.Components.Store;

/// <summary>
/// The part subscription of ADR 05 §1 wired into <c>PartBase</c> (UTL-STA-25/27), observed through bUnit render counts:
/// one subscription per part, every selection re-evaluated per notification, a render only when a selection changed, the
/// subscription disposed with the part.
/// </summary>
public sealed class PartSubscriptionTests : IDisposable
{
    private readonly BunitContext context = new();

    public PartSubscriptionTests()
    {
        context.Services.AddBlazixPrimitives();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "a part re-renders only when one of its selections changes")]
    [Trait("Requirement", "UTL-STA-25")]
    public async Task A_part_re_renders_only_when_one_of_its_selections_changes()
    {
        ComponentStore<CounterStoreState> store = new(new CounterStoreState(5, 3));
        IRenderedComponent<StoreSubscriberPart> cut = context.Render<StoreSubscriberPart>(p => p.Add(x => x.Store, store));
        Assert.Equal(1, cut.RenderCount);
        Assert.Equal("5", cut.Find("output").TextContent);

        // A write that changes no selection: no render.
        await cut.InvokeAsync(() => store.Set(CounterStoreState.MultiplierField, 4));
        Assert.Equal(1, cut.RenderCount);

        // A write that changes a selection: one render with the new value.
        await cut.InvokeAsync(() => store.Set(CounterStoreState.CountField, 7));
        Assert.Equal(2, cut.RenderCount);
        Assert.Equal("7", cut.Find("output").TextContent);
    }

    [Fact(DisplayName = "a write that changes several selections renders once")]
    [Trait("Requirement", "UTL-STA-27")]
    public async Task A_write_that_changes_several_selections_renders_once()
    {
        ComponentStore<CounterStoreState> store = new(new CounterStoreState(5, 3));
        IRenderedComponent<StoreSubscriberPart> cut = context.Render<StoreSubscriberPart>(p => p.Add(x => x.Store, store));

        // count 5 → 6 changes both `count` and `even`.
        await cut.InvokeAsync(() => store.Set(CounterStoreState.CountField, 6));

        Assert.Equal(2, cut.RenderCount);
        Assert.Equal("6", cut.Find("output").TextContent);
        Assert.True(cut.Find("output").HasAttribute("data-even"));
    }

    [Fact(DisplayName = "the subscription is disposed with the part")]
    [Trait("Requirement", "UTL-STA-25")]
    public async Task The_subscription_is_disposed_with_the_part()
    {
        ComponentStore<CounterStoreState> store = new(new CounterStoreState(5, 3));
        IRenderedComponent<StoreSubscriberPart> cut = context.Render<StoreSubscriberPart>(p => p.Add(x => x.Store, store));
        StoreSubscriberPart part = cut.Instance;
        await cut.InvokeAsync(() => store.Set(CounterStoreState.CountField, 6));
        Assert.Equal(2, part.CountEvaluations);

        await context.DisposeComponentsAsync();
        await context.Renderer.Dispatcher.InvokeAsync(() => store.Set(CounterStoreState.CountField, 8));

        Assert.Equal(2, part.CountEvaluations);
    }

    [Fact(DisplayName = "a part subscribes at most once")]
    [Trait("Requirement", "UTL-STA-27")]
    public void A_part_subscribes_at_most_once()
    {
        ComponentStore<CounterStoreState> store = new(new CounterStoreState(5, 3));

        Assert.Throws<InvalidOperationException>(() => context.Render<StoreSubscriberPart>(p => p
            .Add(x => x.Store, store)
            .Add(x => x.SubscribeTwice, true)));
    }
}
