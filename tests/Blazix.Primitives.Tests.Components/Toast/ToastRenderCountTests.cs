using Bunit;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// Render counts of the toast parts, counted through their class functions (the standing render-count rule; issue 110).
/// Blazor renders again every child that takes a delegate or fragment parameter when its parent renders (the measurement
/// of ADR 16 Amendments 2026-10-27), and every part below takes its class function; a write that renders the viewport
/// (the list, the expanded flag, the focused flag) therefore reaches every root and part once. What the port guarantees,
/// and what is counted here, is that each renders exactly once per write: a root's own selection does not render it a
/// second time, and a part reached by its root's render is not notified again after the batch.
/// </summary>
public sealed class ToastRenderCountTests : IDisposable
{
    private readonly ToastTestContext test = new();

    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: a hover renders the viewport and every toast part exactly once")]
    [Trait("Requirement", "TST-DOM-02")]
    [Trait("Requirement", "TST-DOM-15")]
    [Trait("Requirement", "TST-ARIA-07")]
    public void A_hover_renders_the_viewport_and_every_toast_part_exactly_once()
    {
        Dictionary<string, int> renders = [];
        IRenderedComponent<ToastCountingHost> cut = Seed(renders);
        ToastStore<NoPayload> store = cut.FindComponent<ToastProvider<NoPayload>>().Instance.Store;
        renders.Clear();

        test.Run(cut, () => store.SetHovering(true));

        Assert.Equal(Once("viewport", "a", "b"), renders);
    }

    [Fact(DisplayName = "port: an update of one toast renders the viewport and every toast part exactly once")]
    [Trait("Requirement", "TST-EVT-11")]
    public void An_update_of_one_toast_renders_the_viewport_and_every_toast_part_exactly_once()
    {
        Dictionary<string, int> renders = [];
        IRenderedComponent<ToastCountingHost> cut = Seed(renders);
        renders.Clear();

        test.Run(cut, () => test.Manager.Update("a", toast => toast with { Title = "a2" }));

        Assert.Equal("a2", cut.FindAll("h2")[1].TextContent);
        Assert.Equal(Once("viewport", "a", "b"), renders);
    }

    [Fact(DisplayName = "port: a write that renders no viewport selection renders no part")]
    [Trait("Requirement", "TST-EVT-11")]
    public void A_write_that_renders_no_viewport_selection_renders_no_part()
    {
        Dictionary<string, int> renders = [];
        IRenderedComponent<ToastCountingHost> cut = Seed(renders);
        ToastStore<NoPayload> store = cut.FindComponent<ToastProvider<NoPayload>>().Instance.Store;
        renders.Clear();

        // The window's focus is read only by the timers.
        test.Run(cut, () => store.SetWindowFocused(false));

        Assert.Empty(renders);
    }

    [Fact(DisplayName = "port: a part with no parameters, which its root's render does not reach, is notified after the batch")]
    [Trait("Requirement", "TST-ARIA-07")]
    public void A_part_with_no_parameters_which_its_roots_render_does_not_reach_is_notified_after_the_batch()
    {
        IRenderedComponent<ToastHost> cut = test.RenderHost(p => p.Add(x => x.RootContent, _ => builder =>
        {
            builder.OpenComponent<ToastClose>(0);
            builder.CloseComponent();
        }));
        test.Run(cut, () => test.Manager.Add(new ToastObject { Title = "title" }));
        ToastStore<NoPayload> store = cut.FindComponent<ToastProvider<NoPayload>>().Instance.Store;
        Assert.Equal("true", cut.Find("button").GetAttribute("aria-hidden"));

        test.Run(cut, () => store.SetHovering(true));

        Assert.Equal("false", cut.Find("button").GetAttribute("aria-hidden"));
    }

    private static Dictionary<string, int> Once(string viewport, params string[] toasts)
    {
        Dictionary<string, int> expected = new() { [viewport] = 1 };
        foreach (string id in toasts)
        {
            foreach (string part in (string[])["root", "content", "title", "description", "action", "close"])
            {
                expected[$"{part}:{id}"] = 1;
            }
        }

        return expected;
    }

    private IRenderedComponent<ToastCountingHost> Seed(Dictionary<string, int> renders)
    {
        IRenderedComponent<ToastCountingHost> cut = test.Context.Render<ToastCountingHost>(p => p.Add(x => x.Renders, renders));
        test.Run(cut, () => test.Manager.Add(new ToastObject { Id = "a", Title = "a", Description = "a", ActionContent = b => b.AddContent(0, "go") }));
        test.Run(cut, () => test.Manager.Add(new ToastObject { Id = "b", Title = "b", Description = "b", ActionContent = b => b.AddContent(0, "go") }));
        return cut;
    }
}
