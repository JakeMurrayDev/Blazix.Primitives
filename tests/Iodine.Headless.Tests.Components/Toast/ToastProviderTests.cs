using Bunit;
using Microsoft.AspNetCore.Components;

namespace Iodine.Headless.Tests.Components;

/// <summary>
/// The toast provider at L2 (TST §3.1, TST §3.2): <c>ToastProvider.test.tsx</c>'s titles, the provider-level titles of
/// <c>useToastManager.test.tsx</c> and <c>createToastManager.test.tsx</c> over rendered markup, and the port's titles for
/// the registered manager, the subscription's timing and prerendering (ADR 05 Amendments 2026-11-18 item 3).
/// </summary>
public sealed class ToastProviderTests : IDisposable
{
    private readonly ToastTestContext test = new();

    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "syncs a changed timeout before descendant layout effects")]
    [Trait("Requirement", "TST-API-04")]
    public void Syncs_a_changed_timeout_before_descendant_layout_effects()
    {
        int closed = 0;
        ToastObject toast = new() { Id = "toast", Title = "Toast", OnClose = () => closed++ };
        IRenderedComponent<ToastSyncHost> cut = test.Context.Render<ToastSyncHost>(p => p.Add(x => x.Timeout, 5000).Add(x => x.Toasts, [toast]));

        cut.Render(p => p.Add(x => x.Timeout, 1000).Add(x => x.Active, true).Add(x => x.Toasts, [toast]));

        test.Advance(cut, 999);
        Assert.Equal(0, closed);
        test.Advance(cut, 2);
        Assert.Equal(1, closed);
    }

    [Fact(DisplayName = "syncs a changed limit before descendant layout effects")]
    [Trait("Requirement", "TST-API-04")]
    public void Syncs_a_changed_limit_before_descendant_layout_effects()
    {
        List<IReadOnlyList<(string Id, bool Limited)>> observed = [];
        ToastObject[] toasts = [new() { Id = "first", Title = "First", Timeout = 0 }, new() { Id = "second", Title = "Second", Timeout = 0 }];
        IRenderedComponent<ToastSyncHost> cut = test.Context.Render<ToastSyncHost>(p => p.Add(x => x.Limit, 3).Add(x => x.Toasts, toasts).Add(x => x.Observed, observed));

        cut.Render(p => p.Add(x => x.Limit, 1).Add(x => x.Active, true).Add(x => x.Toasts, toasts).Add(x => x.Observed, observed));

        Assert.Equal([("second", false), ("first", true)], observed[0]);
    }

    [Fact(DisplayName = "port: does not sync provider props from a parameter set that fails before OnParametersSet")]
    [Trait("Requirement", "TST-API-04")]
    [Trait("Requirement", "TST-EDGE-09")]
    public void Does_not_sync_provider_props_from_a_parameter_set_that_fails_before_OnParametersSet()
    {
        // Base UI's "does not sync provider props from an abandoned render" (a suspended transition) has no Blazor phase; the
        // port's analogue is a parameter set that throws before OnParametersSet (M6 track 9a design point 9, settled by the
        // orchestrator 2026-11-18). A Provider that wrote its Timeout from a setter would fail here.
        int closed = 0;
        IRenderedComponent<ToastProvider<NoPayload>> cut = test.Context.Render<ToastProvider<NoPayload>>(p => p.Add(x => x.Timeout, 1000));

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() => cut.InvokeAsync(() => cut.Instance.SetParametersAsync(
            ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(ToastProvider<NoPayload>.Timeout)] = (Duration?)2000, [nameof(ToastProvider<NoPayload>.Limit)] = "two" }))).GetAwaiter().GetResult());
        Assert.Contains("Limit", failure.Message, StringComparison.Ordinal);

        test.Run(cut, () => test.Manager.Add(new ToastObject { Title = "Toast", OnClose = () => closed++ }));
        test.Advance(cut, 1000);
        Assert.Equal(1, closed);
    }

    [Fact(DisplayName = "keeps multiple providers isolated when one provider updates")]
    [Trait("Requirement", "TST-STR-01")]
    public void Keeps_multiple_providers_isolated_when_one_provider_updates()
    {
        ToastManager first = new();
        ToastManager second = new();
        IRenderedComponent<ToastHost> one = test.RenderHost(p => p.Add(x => x.Manager, first));
        IRenderedComponent<ToastHost> two = test.RenderHost(p => p.Add(x => x.Manager, second));
        test.Run(one, () => first.Add(new ToastObject { Id = "a", Title = "first-a" }));
        test.Run(two, () => second.Add(new ToastObject { Id = "a", Title = "second-a" }));

        test.Run(one, () => first.Update("a", toast => toast with { Title = "first-updated" }));

        Assert.Equal("first-updated", one.Find("[data-testid=title]").TextContent);
        Assert.Equal("second-a", two.Find("[data-testid=title]").TextContent);
    }

    [Fact(DisplayName = "adds a toast to the viewport that auto-dismisses after 5s by default")]
    [Trait("Requirement", "TST-API-01")]
    [Trait("Requirement", "TST-API-05")]
    public void Adds_a_toast_to_the_viewport_that_auto_dismisses_after_5s_by_default()
    {
        IRenderedComponent<ToastHost> cut = test.RenderHost();
        Assert.Empty(ToastTestContext.Roots(cut));

        test.Run(cut, () => test.Manager.Add(new ToastObject { Title = "title" }));
        Assert.Equal("title", cut.Find("[data-testid=title]").TextContent);

        test.Advance(cut, 4999);
        Assert.Null(ToastTestContext.Roots(cut)[0].GetAttribute("data-ending-style"));
        Assert.Equal(TransitionStatus.Starting, test.Manager.Toasts[0].TransitionStatus);

        test.Advance(cut, 1);
        Assert.Equal(TransitionStatus.Ending, test.Manager.Toasts[0].TransitionStatus);
    }

    [Fact(DisplayName = "applies a changed timeout to toasts added afterwards")]
    [Trait("Requirement", "TST-API-01")]
    public void Applies_a_changed_timeout_to_toasts_added_afterwards()
    {
        IRenderedComponent<ToastHost> cut = test.RenderHost(p => p.Add(x => x.Timeout, 1000));
        test.Run(cut, () => test.Manager.Add(new ToastObject { Id = "before" }));

        cut.Render(p => p.Add(x => x.Timeout, 3000));
        test.Run(cut, () => test.Manager.Add(new ToastObject { Id = "after" }));

        test.Advance(cut, 1000);
        Assert.Equal(TransitionStatus.Ending, Find("before")?.TransitionStatus);
        Assert.NotEqual(TransitionStatus.Ending, Find("after")?.TransitionStatus);
        test.Advance(cut, 2000);
        Assert.Equal(TransitionStatus.Ending, Find("after")?.TransitionStatus);
    }

    [Fact(DisplayName = "marks toasts as limited when the limit is exceeded")]
    [Trait("Requirement", "TST-API-02")]
    [Trait("Requirement", "TST-DOM-06")]
    [Trait("Requirement", "TST-DOM-09")]
    public void Marks_toasts_as_limited_when_the_limit_is_exceeded()
    {
        IRenderedComponent<ToastHost> cut = test.RenderHost(p => p.Add(x => x.Limit, 2));
        for (int i = 1; i <= 3; i++)
        {
            int count = i;
            test.Run(cut, () => test.Manager.Add(new ToastObject { Id = $"toast-{count}", Title = $"toast-{count}" }));
        }

        // Newest first: toast-3, toast-2, then the limited toast-1, still rendered and inert.
        IReadOnlyList<AngleSharp.Dom.IElement> roots = ToastTestContext.Roots(cut);
        Assert.Equal(3, roots.Count);
        Assert.False(roots[0].HasAttribute("data-limited"));
        Assert.False(roots[1].HasAttribute("data-limited"));
        Assert.True(roots[2].HasAttribute("data-limited"));
        Assert.Equal(string.Empty, roots[2].GetAttribute("inert"));
        Assert.False(roots[0].HasAttribute("inert"));
    }

    [Fact(DisplayName = "recomputes limited toasts when the limit prop changes")]
    [Trait("Requirement", "TST-API-02")]
    public void Recomputes_limited_toasts_when_the_limit_prop_changes()
    {
        IRenderedComponent<ToastHost> cut = test.RenderHost(p => p.Add(x => x.Limit, 1));
        test.Run(cut, () => test.Manager.Add(new ToastObject { Id = "toast-1" }));
        test.Run(cut, () => test.Manager.Add(new ToastObject { Id = "toast-2" }));
        Assert.True(ToastTestContext.Roots(cut)[1].HasAttribute("data-limited"));

        cut.Render(p => p.Add(x => x.Limit, 2));
        Assert.False(ToastTestContext.Roots(cut)[1].HasAttribute("data-limited"));

        cut.Render(p => p.Add(x => x.Limit, 1));
        Assert.True(ToastTestContext.Roots(cut)[1].HasAttribute("data-limited"));
    }

    [Fact(DisplayName = "port: the provider acts on a manager passed to it, and moves to a new one when the parameter changes")]
    [Trait("Requirement", "TST-API-03")]
    public void The_provider_acts_on_a_manager_passed_to_it_and_moves_to_a_new_one_when_the_parameter_changes()
    {
        ToastManager first = new();
        ToastManager second = new();
        IRenderedComponent<ToastHost> cut = test.RenderHost(p => p.Add(x => x.Manager, first));

        test.Run(cut, () => first.Add(new ToastObject { Id = "a", Title = "from first" }));
        test.Run(cut, () => test.Manager.Add(new ToastObject { Id = "ignored", Title = "registered" }));
        Assert.Equal(["from first"], cut.FindAll("[data-testid=title]").Select(title => title.TextContent));

        cut.Render(p => p.Add(x => x.Manager, second));
        test.Run(cut, () => first.Add(new ToastObject { Id = "b", Title = "after unsubscribe" }));
        test.Run(cut, () => second.Add(new ToastObject { Id = "c", Title = "from second" }));

        Assert.Equal(["from second", "from first"], cut.FindAll("[data-testid=title]").Select(title => title.TextContent));
    }

    [Fact(DisplayName = "port: a provider without a manager uses the one registered for the scope")]
    [Trait("Requirement", "TST-API-03")]
    [Trait("Requirement", "TST-API-05")]
    public void A_provider_without_a_manager_uses_the_one_registered_for_the_scope()
    {
        IRenderedComponent<ToastHost> cut = test.RenderHost();

        test.Run(cut, () => test.Manager.Add(new ToastObject { Title = "registered" }));

        Assert.Equal("registered", cut.Find("[data-testid=title]").TextContent);
        Assert.Same(test.Manager.Toasts, cut.FindComponent<ToastProvider<NoPayload>>().Instance.Store.State.Toasts);
    }

    [Fact(DisplayName = "port: a prerendered provider does not subscribe, so a toast added then is not shown")]
    [Trait("Requirement", "TST-API-03")]
    [Trait("Requirement", "TST-EVT-06")]
    public void A_prerendered_provider_does_not_subscribe_so_a_toast_added_then_is_not_shown()
    {
        using ToastTestContext prerender = new(interactive: false);
        IRenderedComponent<ToastHost> cut = prerender.RenderHost();

        prerender.Run(cut, () => prerender.Manager.Add(new ToastObject { Title = "lost" }));

        Assert.Empty(ToastTestContext.Roots(cut));
        Assert.Empty(prerender.Manager.Toasts);
        Assert.NotNull(cut.Find("[data-testid=viewport]"));
    }

    [Fact(DisplayName = "replaces a closing toast when adding again with the same id")]
    [Trait("Requirement", "TST-EVT-11")]
    public void Replaces_a_closing_toast_when_adding_again_with_the_same_id()
    {
        IRenderedComponent<ToastHost> cut = test.RenderHost();
        test.Run(cut, () => test.Manager.Add(new ToastObject { Id = "same", Title = "first" }));
        test.Run(cut, () => test.Manager.Close("same"));

        test.Run(cut, () => test.Manager.Add(new ToastObject { Id = "same", Title = "second" }));

        Assert.Single(ToastTestContext.Roots(cut));
        Assert.Equal("second", cut.Find("[data-testid=title]").TextContent);
    }

    [Fact(DisplayName = "increments updateKey when updating a toast")]
    [Trait("Requirement", "TST-EVT-11")]
    public void Increments_updateKey_when_updating_a_toast()
    {
        IRenderedComponent<ToastHost> cut = test.RenderHost();
        test.Run(cut, () => test.Manager.Add(new ToastObject { Id = "a", Title = "title" }));

        test.Run(cut, () => test.Manager.Update("a", toast => toast with { Title = "one" }));
        test.Run(cut, () => test.Manager.Update("a", toast => toast with { Title = "two" }));

        Assert.Equal("two", cut.Find("[data-testid=title]").TextContent);
        Assert.Equal(2, test.Manager.Toasts[0].UpdateKey);
    }

    [Fact(DisplayName = "calls onRemove once after replacing an ending toast and later removing the replacement")]
    [Trait("Requirement", "TST-EVT-05")]
    public void Calls_onRemove_once_after_replacing_an_ending_toast_and_later_removing_the_replacement()
    {
        // The removal is the root's completion watcher (M6 track 9b); a toast that no root renders is never removed, so
        // its onRemove never fires however long the close lasts.
        int removed = 0;
        IRenderedComponent<ToastProvider<NoPayload>> cut = test.Context.Render<ToastProvider<NoPayload>>();
        test.Run(cut, () => test.Manager.Add(new ToastObject { Id = "same", OnRemove = () => removed++ }));
        test.Run(cut, () => test.Manager.Close("same"));

        test.Advance(cut, 60_000);

        Assert.Equal(0, removed);
        Assert.Equal(TransitionStatus.Ending, test.Manager.Toasts[0].TransitionStatus);
    }

    [Fact(DisplayName = "port: disposing the provider clears its pending timers")]
    [Trait("Requirement", "TST-EDGE-06")]
    public async Task Disposing_the_provider_clears_its_pending_timers()
    {
        int closed = 0;
        IRenderedComponent<ToastHost> cut = test.RenderHost();
        test.Run(cut, () => test.Manager.Add(new ToastObject { Title = "title", OnClose = () => closed++ }));

        await test.Context.DisposeComponentsAsync();
        test.Clock.Advance(TimeSpan.FromMilliseconds(10_000));

        Assert.Equal(0, closed);
        Assert.Empty(test.Manager.Toasts);
    }

    private ToastObject<NoPayload>? Find(string id) => test.Manager.Toasts.FirstOrDefault(toast => toast.Id == id);
}
