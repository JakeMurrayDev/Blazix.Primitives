using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// List navigation's C# half (M5 track 1, issue 159; ADR 04 Amendments 2026-11-03 (1)-(3), chosen by the user): the list
/// behaviour and its handles, composite roots in list mode, on the test-only list hosts. The handles send the session
/// token, the list's options and their role, after the session's handle; the items render <c>tabindex="-1"</c> and no
/// <c>data-highlighted</c> (the module writes both) and mirror the module's highlight; C# asks for its own highlights by
/// revision; the list's requests run the family's sequence.
/// </summary>
public sealed class ListNavigationTests : IDisposable
{
    private readonly CompositeTestContext host = new();

    private BunitContext Context => host.Context;

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "a list handle waits for the session's handle, then sends the session token, the list's options and its role")]
    [Trait("Requirement", "CMP-STR-06")]
    [Trait("Requirement", "CMP-STA-37")]
    public void A_list_handle_waits_for_the_session_then_sends_the_token_the_options_and_its_role()
    {
        TaskCompletionSource ready = new();
        IRenderedComponent<TestListRoot> cut = Context.Render<TestListRoot>(p => p
            .Add(x => x.SessionReady, ready.Task)
            .Add(x => x.LoopFocus, true)
            .AddChildContent(Handles(3)));

        Assert.Empty(host.Attaches);

        cut.InvokeAsync(ready.SetResult);
        cut.WaitForAssertion(() => Assert.Equal(2, host.Attaches.Count));

        string floating = FloatingOptions();
        string reference = host.Attaches.Select(attach => (string)attach.Arguments[1]!).Single(json => json.Contains("\"reference\"", StringComparison.Ordinal));
        Assert.EndsWith(""","sessionToken":"test-session","list":{"loopFocus":true},"floating":{}}""", floating, StringComparison.Ordinal);
        Assert.Equal(3, CompositeTestContext.ItemIds(floating).Length);
        Assert.EndsWith(""","sessionToken":"test-session","list":{"loopFocus":true},"reference":{}}""", reference, StringComparison.Ordinal);
        Assert.Empty(CompositeTestContext.ItemIds(reference));
    }

    [Fact(DisplayName = "an unchanged render of a list sends nothing, and a changed option sends one update to each handle")]
    [Trait("Requirement", "CMP-STA-10")]
    public void An_unchanged_render_sends_nothing_and_a_changed_option_sends_one_update_per_handle()
    {
        IRenderedComponent<TestListRoot> cut = Context.Render<TestListRoot>(p => p.AddChildContent(Handles(3)));
        cut.WaitForAssertion(() => Assert.Equal(2, host.Attaches.Count));

        cut.Render(p => p.AddChildContent(Handles(3)));
        Assert.Empty(host.UpdateOptions);

        cut.Render(p => p.Add(x => x.Orientation, "horizontal").AddChildContent(Handles(3)));
        Assert.Equal(2, host.UpdateOptions.Count);
        Assert.All(host.UpdateOptions, json => Assert.Contains("""
            "list":{"orientation":"horizontal"}
            """.Trim(), json, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "every item renders tabindex=\"-1\" and no data-highlighted, and its Highlighted mirror follows the module's report")]
    [Trait("Requirement", "CMP-STA-31")]
    [Trait("Requirement", "CMP-DOM-04")]
    public void Items_render_the_handover_values_and_mirror_the_reported_highlight()
    {
        IRenderedComponent<TestListRoot> cut = Context.Render<TestListRoot>(p => p.AddChildContent(Handles(3)));
        cut.WaitForAssertion(() => Assert.Equal(2, host.Attaches.Count));
        string[] ids = CompositeTestContext.RenderedItemIds(cut);

        cut.InvokeAsync(() => FloatingTwin().OnListHighlight($$"""{"index":1,"itemId":"{{ids[1]}}","event":null,"revision":0}"""));

        IReadOnlyList<IRenderedComponent<TestListItem>> items = cut.FindComponents<TestListItem>();
        Assert.Equal([false, true, false], items.Select(item => item.Instance.Highlighted));
        Assert.Equal((1, ids[1]), (cut.Instance.List.HighlightedIndex, cut.Instance.List.HighlightedItemId));
        Assert.All(cut.FindAll("[data-item]"), element =>
        {
            Assert.Equal("-1", element.GetAttribute("tabindex"));
            Assert.False(element.HasAttribute("data-highlighted"));
        });
    }

    // ADR 04 Amendments 2026-11-03 (2) and the lesson of issue 110: a move renders the item that lost the highlight and the
    // one that gained it, counted through ClassValue.
    [Fact(DisplayName = "a highlight move renders exactly the two items whose mirror changed, at 100 items")]
    [Trait("Requirement", "CMP-STA-31")]
    public void A_highlight_move_renders_exactly_two_items_at_100_items()
    {
        int[] renders = new int[100];
        IRenderedComponent<TestListRoot> cut = Context.Render<TestListRoot>(p => p.AddChildContent(Handles(100, renders)));
        cut.WaitForAssertion(() => Assert.Equal(2, host.Attaches.Count));
        string[] ids = CompositeTestContext.RenderedItemIds(cut);
        cut.InvokeAsync(() => FloatingTwin().OnListHighlight($$"""{"index":5,"itemId":"{{ids[5]}}","event":null,"revision":0}"""));
        int[] before = [.. renders];

        cut.InvokeAsync(() => FloatingTwin().OnListHighlight($$"""{"index":6,"itemId":"{{ids[6]}}","event":null,"revision":0}"""));

        int[] rendered = [.. Enumerable.Range(0, 100).Where(i => renders[i] != before[i])];
        Assert.Equal([5, 6], rendered);
        Assert.Equal(1, renders[5] - before[5]);
        Assert.Equal(1, renders[6] - before[6]);
    }

    [Fact(DisplayName = "a C# highlight request goes out at once with a revision, and a report that answers an older revision is ignored")]
    [Trait("Requirement", "CMP-STA-32")]
    public void A_csharp_highlight_request_goes_out_with_a_revision_and_a_stale_report_is_ignored()
    {
        IRenderedComponent<TestListRoot> cut = Context.Render<TestListRoot>(p => p.AddChildContent(Handles(3)));
        cut.WaitForAssertion(() => Assert.Equal(2, host.Attaches.Count));
        string[] ids = CompositeTestContext.RenderedItemIds(cut);

        cut.InvokeAsync(() => cut.Instance.List.RequestHighlightAsync(2));

        Assert.Equal(2, host.UpdateOptions.Count);
        Assert.All(host.UpdateOptions, json => Assert.Contains("\"highlight\":{\"index\":2,\"revision\":1}", json, StringComparison.Ordinal));

        // A move the module reported before it had the request answers revision 0: stale.
        cut.InvokeAsync(() => FloatingTwin().OnListHighlight($$"""{"index":0,"itemId":"{{ids[0]}}","event":null,"revision":0}"""));
        Assert.Null(cut.Instance.List.HighlightedIndex);

        cut.InvokeAsync(() => FloatingTwin().OnListHighlight($$"""{"index":2,"itemId":"{{ids[2]}}","event":null,"revision":1}"""));
        Assert.Equal(2, cut.Instance.List.HighlightedIndex);
        Assert.True(cut.FindComponents<TestListItem>()[2].Instance.Highlighted);
    }

    [Fact(DisplayName = "a list's open and close requests run the family's sequence with their reason and trigger, and a closed match raises Matched")]
    [Trait("Requirement", "CMP-EVT-06")]
    [Trait("Requirement", "CMP-EVT-07")]
    [Trait("Requirement", "CMP-EVT-08")]
    [Trait("Requirement", "CMP-EVT-10")]
    public void Requests_run_the_family_sequence_and_a_closed_match_raises_Matched()
    {
        IRenderedComponent<TestListRoot> cut = Context.Render<TestListRoot>(p => p.AddChildContent(Handles(3)));
        cut.WaitForAssertion(() => Assert.Equal(2, host.Attaches.Count));
        List<int> matches = [];
        cut.Instance.List.Matched += match => matches.Add(match.Index);

        cut.InvokeAsync(() => FloatingTwin().OnListOpenRequest("""{"open":true,"reason":"list-navigation","event":{"kind":"KeyboardEvent","type":"keydown","key":"ArrowDown"},"triggerId":"trigger-1"}"""));
        cut.InvokeAsync(() => FloatingTwin().OnListOpenRequest("""{"open":false,"reason":"focus-out","event":{"kind":"KeyboardEvent","type":"keydown","key":"Tab"},"triggerId":null}"""));
        cut.InvokeAsync(() => FloatingTwin().OnListMatch("""{"index":2,"event":{"kind":"KeyboardEvent","type":"keydown","key":"t"}}"""));

        Assert.Collection(
            cut.Instance.Requests,
            open => Assert.Equal((true, EventReason.ListNavigation, "trigger-1", "ArrowDown"), (open.Open, open.Details.Reason, open.Details.Trigger?.Id, open.Details.Event?.Key)),
            close => Assert.Equal((false, EventReason.FocusOut, null, "Tab"), (close.Open, close.Details.Reason, close.Details.Trigger?.Id, close.Details.Event?.Key)));
        Assert.Equal([2], matches);
    }

    // ADR 04 Amendments 2026-11-03 (3): on WebAssembly `leave` runs the close inside the keydown, and its cancel keeps the
    // focus where it was.
    [Fact(DisplayName = "on WebAssembly leave runs the Shift+Tab close inside the decision and answers stop when it was canceled")]
    [Trait("Requirement", "CMP-KEY-24")]
    [Trait("Requirement", "CMP-EVT-08")]
    public void Leave_runs_the_close_inside_the_decision_and_answers_stop_when_canceled()
    {
        bool cancel = false;
        IRenderedComponent<TestListRoot> cut = Context.Render<TestListRoot>(p => p
            .Add(x => x.OnRequest, request =>
            {
                if (cancel)
                {
                    request.Details.Cancel();
                }

                return Task.CompletedTask;
            })
            .AddChildContent(Handles(3)));
        cut.WaitForAssertion(() => Assert.Equal(2, host.Attaches.Count));
        const string tab = """{"kind":"KeyboardEvent","type":"keydown","key":"Tab","shiftKey":true}""";

        Assert.Equal("run", FloatingTwin().Decide(CompositeTwin.LeavePoint, tab));
        cancel = true;
        Assert.Equal("stop", FloatingTwin().Decide(CompositeTwin.LeavePoint, tab));

        Assert.All(cut.Instance.Requests, request => Assert.Equal((false, EventReason.FocusOut), (request.Open, request.Details.Reason)));
        Assert.Equal(2, cut.Instance.Requests.Count);
    }

    private static RenderFragment Handles(int count, int[]? renders = null) => builder =>
    {
        builder.OpenComponent<TestListHandle>(0);
        builder.AddAttribute(1, nameof(TestListHandle.Role), CompositeListRole.Reference);
        builder.CloseComponent();

        builder.OpenComponent<TestListHandle>(2);
        builder.AddAttribute(3, nameof(TestListHandle.Role), CompositeListRole.Floating);
        builder.AddAttribute(4, nameof(TestListHandle.ChildContent), (RenderFragment)(content =>
        {
            for (int i = 0; i < count; i++)
            {
                int index = i;
                content.OpenComponent<TestListItem>(5);
                content.SetKey(index);
                content.AddAttribute(6, "data-item", index);
                content.AddAttribute(7, nameof(TestListItem.Label), "Item " + index.ToString(System.Globalization.CultureInfo.InvariantCulture));
                if (renders is not null)
                {
                    content.AddAttribute(8, nameof(TestListItem.ClassValue), (Func<TestListItemState, string?>)(_ =>
                    {
                        renders[index]++;
                        return null;
                    }));
                }

                content.CloseComponent();
            }
        }));
        builder.CloseComponent();
    };

    private string FloatingOptions() => host.Attaches.Select(attach => (string)attach.Arguments[1]!).Single(json => json.Contains("\"floating\"", StringComparison.Ordinal));

    private CompositeTwin FloatingTwin()
    {
        JSRuntimeInvocation floating = host.Attaches.Single(attach => ((string)attach.Arguments[1]!).Contains("\"floating\"", StringComparison.Ordinal));
        return ((Microsoft.JSInterop.DotNetObjectReference<CompositeTwin>)floating.Arguments[2]!).Value;
    }
}
