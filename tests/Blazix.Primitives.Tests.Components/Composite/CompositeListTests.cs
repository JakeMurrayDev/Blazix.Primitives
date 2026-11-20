using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The registration-only list (M3 track 1, E1; ADR 04 Amendments 2026-10-26, chosen by the user): a root-less
/// <c>CompositeList</c> (TabsRoot's panels, AccordionRoot's items) attaches the <c>composite</c> module in its
/// registration-only mode and reads its items' indexes from the order report. An item's index is <c>-1</c> until the
/// first report and in prerender (CMP-STA-15); its part re-renders only when its index changes (ADR 16 rule 8). An item
/// without a list is inert (CMP-STR-03). Every list item renders an id (design point 2) and the list cascades its own
/// context (design point 6), option (a) both; settled by the orchestrator 2026-10-26.
/// </summary>
public sealed class CompositeListTests : IDisposable
{
    private readonly CompositeTestContext host = new();

    private BunitContext Context => host.Context;

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "renders an item that is not wrapped in a list")]
    [Trait("Requirement", "CMP-STR-03")]
    public void Renders_an_item_that_is_not_wrapped_in_a_list()
    {
        IRenderedComponent<TestCompositeListItem> cut = Context.Render<TestCompositeListItem>(p => p.AddUnmatched("data-testid", "orphan"));

        Assert.Equal("-1", cut.Find("[data-testid=\"orphan\"]").GetAttribute("data-index"));
        Assert.Empty(Context.JSInterop.Invocations);
        Exception? error = Record.Exception(() => Context.DisposeComponentsAsync().GetAwaiter().GetResult());
        Assert.Null(error);
    }

    [Fact(DisplayName = "an item's index is -1 until the report, then the reported index, re-rendering only the items whose index changed")]
    [Trait("Requirement", "CMP-STR-03")]
    [Trait("Requirement", "CMP-STA-15")]
    [Trait("Requirement", "CMP-STA-10")]
    public void An_items_index_is_minus_one_until_the_report_then_the_reported_index()
    {
        IRenderedComponent<TestCompositeList> cut = Context.Render<TestCompositeList>(p => p.AddChildContent(Items("a", "b", "c")));
        string[] ids = RenderedIds(cut);
        string attached = (string)Assert.Single(host.Attaches).Arguments[1]!;
        Assert.True(CompositeTestContext.Parse(attached).GetProperty("registrationOnly").GetBoolean());
        Assert.Equal(ids, CompositeTestContext.ItemIds(attached));
        Assert.Equal(["-1", "-1", "-1"], Indexes(cut));
        IReadOnlyList<IRenderedComponent<TestCompositeListItem>> items = cut.FindComponents<TestCompositeListItem>();
        int[] before = [.. items.Select(item => item.Instance.Renders)];

        cut.InvokeAsync(() => host.Twin.OnItemsChange($$"""{"ids":["{{ids[0]}}","{{ids[1]}}","{{ids[2]}}"]}"""));
        Assert.Equal(["0", "1", "2"], Indexes(cut));

        cut.InvokeAsync(() => host.Twin.OnItemsChange($$"""{"ids":["{{ids[1]}}","{{ids[0]}}","{{ids[2]}}"]}"""));
        Assert.Equal(["1", "0", "2"], Indexes(cut));
        Assert.Equal([before[0] + 2, before[1] + 2, before[2] + 1], items.Select(item => item.Instance.Renders));
        Assert.Empty(host.UpdateOptions);
    }

    [Fact(DisplayName = "a batch of items sends one update, an unreported item keeps -1, and an unknown id is ignored")]
    [Trait("Requirement", "CMP-STR-03")]
    [Trait("Requirement", "CMP-STA-10")]
    public void A_batch_of_items_sends_one_update()
    {
        IRenderedComponent<TestCompositeList> cut = Context.Render<TestCompositeList>(p => p.AddChildContent(Items("a")));
        string a = RenderedIds(cut)[0];
        cut.InvokeAsync(() => host.Twin.OnItemsChange($$"""{"ids":["{{a}}"]}"""));

        cut.Render(p => p.AddChildContent(Items("a", "b", "c")));
        string update = Assert.Single(host.UpdateOptions);
        string[] ids = RenderedIds(cut);
        Assert.Equal(ids, CompositeTestContext.ItemIds(update));
        Assert.Equal(["0", "-1", "-1"], Indexes(cut));

        cut.InvokeAsync(() => host.Twin.OnItemsChange($$"""{"ids":["unknown","{{ids[2]}}","{{ids[0]}}"]}"""));
        Assert.Equal(["2", "-1", "1"], Indexes(cut));

        cut.Render(p => p.AddChildContent(Items("a", "b", "c")));
        Assert.Single(host.UpdateOptions);
    }

    // M6 track 4 (decision 23 of the M6 plan, chosen by the user 2026-11-18; the Composite/ grant to Slider): an item that
    // registers with an explicit index sends it, so the module reserves that slot (CMP-ALG-17's explicit slots).
    [Fact(DisplayName = "port: an item registered with an explicit index sends the index with its id")]
    [Trait("Requirement", "CMP-STA-01")]
    [Trait("Requirement", "SLD-EDGE-14")]
    public void Port_an_item_registered_with_an_explicit_index_sends_the_index_with_its_id()
    {
        IRenderedComponent<TestCompositeList> cut = Context.Render<TestCompositeList>(p => p.AddChildContent(builder =>
        {
            builder.OpenComponent<TestCompositeListItem>(0);
            builder.AddComponentParameter(1, nameof(TestCompositeListItem.ExplicitIndex), 1);
            builder.AddAttribute(2, "data-entry", "second");
            builder.CloseComponent();
            builder.OpenComponent<TestCompositeListItem>(3);
            builder.AddComponentParameter(4, nameof(TestCompositeListItem.ExplicitIndex), 0);
            builder.AddAttribute(5, "data-entry", "first");
            builder.CloseComponent();
        }));

        JsonElement items = CompositeTestContext.Parse((string)Assert.Single(host.Attaches).Arguments[1]!).GetProperty("items");
        Assert.Equal(1, items[0].GetProperty("index").GetInt32());
        Assert.Equal(0, items[1].GetProperty("index").GetInt32());

        cut.Render(p => p.AddChildContent(builder =>
        {
            builder.OpenComponent<TestCompositeListItem>(0);
            builder.AddComponentParameter(1, nameof(TestCompositeListItem.ExplicitIndex), 2);
            builder.AddAttribute(2, "data-entry", "second");
            builder.CloseComponent();
            builder.OpenComponent<TestCompositeListItem>(3);
            builder.AddComponentParameter(4, nameof(TestCompositeListItem.ExplicitIndex), 0);
            builder.AddAttribute(5, "data-entry", "first");
            builder.CloseComponent();
        }));

        JsonElement updated = CompositeTestContext.Parse(Assert.Single(host.UpdateOptions)).GetProperty("items");
        Assert.Equal(2, updated[0].GetProperty("index").GetInt32());
    }

    // M6 track 7 (Q-OTP-2 (b) of the M6 plan, chosen by the user 2026-11-18; the Composite/ grant to OtpField, settled by the
    // orchestrator 2026-11-19): an item that opts into a guess takes the next index of the list's render-order counter on its
    // first render, so prerender and the first interactive render carry it (OTP-EDGE-10); the first report corrects it.
    [Fact(DisplayName = "port: items that guess carry their render-order index from the first render, and the report keeps it without a render (CMP-STA-16)")]
    [Trait("Requirement", "CMP-STA-16")]
    [Trait("Requirement", "OTP-STR-03")]
    public void Port_items_that_guess_carry_their_render_order_index_from_the_first_render()
    {
        IRenderedComponent<TestCompositeList> cut = Context.Render<TestCompositeList>(p => p.AddChildContent(Items(true, "a", "b", "c")));
        Assert.Equal(["0", "1", "2"], Indexes(cut));
        IReadOnlyList<IRenderedComponent<TestCompositeListItem>> items = cut.FindComponents<TestCompositeListItem>();
        int[] before = [.. items.Select(item => item.Instance.Renders)];

        string[] ids = RenderedIds(cut);
        cut.InvokeAsync(() => host.Twin.OnItemsChange($$"""{"ids":["{{ids[0]}}","{{ids[1]}}","{{ids[2]}}"]}"""));

        Assert.Equal(["0", "1", "2"], Indexes(cut));
        Assert.Equal(before, items.Select(item => item.Instance.Renders));
    }

    [Fact(DisplayName = "port: an explicitly indexed item consumes no guess (CMP-STA-16)")]
    [Trait("Requirement", "CMP-STA-16")]
    public void Port_an_explicitly_indexed_item_consumes_no_guess()
    {
        IRenderedComponent<TestCompositeList> cut = Context.Render<TestCompositeList>(p => p.AddChildContent(builder =>
        {
            builder.OpenComponent<TestCompositeListItem>(0);
            builder.AddComponentParameter(1, nameof(TestCompositeListItem.Guess), true);
            builder.AddAttribute(2, "data-entry", "first");
            builder.CloseComponent();
            builder.OpenComponent<TestCompositeListItem>(3);
            builder.AddComponentParameter(4, nameof(TestCompositeListItem.Guess), true);
            builder.AddComponentParameter(5, nameof(TestCompositeListItem.ExplicitIndex), 7);
            builder.AddAttribute(6, "data-entry", "explicit");
            builder.CloseComponent();
            builder.OpenComponent<TestCompositeListItem>(7);
            builder.AddComponentParameter(8, nameof(TestCompositeListItem.Guess), true);
            builder.AddAttribute(9, "data-entry", "second");
            builder.CloseComponent();
        }));

        Assert.Equal(["0", "-1", "1"], Indexes(cut));
    }

    [Fact(DisplayName = "port: an item a later render inserts guesses the index after the registered items, and the report corrects it (CMP-STA-16, CMP-STA-18)")]
    [Trait("Requirement", "CMP-STA-16")]
    [Trait("Requirement", "CMP-STA-18")]
    [Trait("Requirement", "OTP-EDGE-11")]
    public void Port_an_item_a_later_render_inserts_guesses_the_index_after_the_registered_items()
    {
        IRenderedComponent<TestCompositeList> cut = Context.Render<TestCompositeList>(p => p.AddChildContent(Items(true, "a", "b")));
        string[] first = RenderedIds(cut);
        cut.InvokeAsync(() => host.Twin.OnItemsChange($$"""{"ids":["{{first[0]}}","{{first[1]}}"]}"""));

        cut.Render(p => p.AddChildContent(Items(true, "x", "a", "b")));
        Assert.Equal(["2", "0", "1"], Indexes(cut));

        string[] ids = RenderedIds(cut);
        cut.InvokeAsync(() => host.Twin.OnItemsChange($$"""{"ids":["{{ids[0]}}","{{ids[1]}}","{{ids[2]}}"]}"""));
        Assert.Equal(["0", "1", "2"], Indexes(cut));
    }

    [Fact(DisplayName = "port: an item can take its guess before it registers, and registering takes no second guess (CMP-STA-16)")]
    [Trait("Requirement", "CMP-STA-16")]
    [Trait("Requirement", "OTP-DOM-07")]
    public void Port_an_item_can_take_its_guess_before_it_registers()
    {
        IRenderedComponent<TestCompositeList> cut = Context.Render<TestCompositeList>(p => p.AddChildContent(builder =>
        {
            for (int i = 0; i < 3; i++)
            {
                builder.OpenComponent<TestCompositeListItem>(0);
                builder.SetKey(i);
                builder.AddComponentParameter(1, nameof(TestCompositeListItem.Guess), true);
                builder.AddComponentParameter(2, nameof(TestCompositeListItem.GuessBeforeRegistering), true);
                builder.AddAttribute(3, "data-entry", i);
                builder.CloseComponent();
            }
        }));

        Assert.Equal([0, 1, 2], cut.FindComponents<TestCompositeListItem>().Select(item => item.Instance.GuessedBeforeRegistering));
        Assert.Equal(["0", "1", "2"], Indexes(cut));
    }

    private static string[] RenderedIds(IRenderedComponent<TestCompositeList> cut) => [.. cut.FindAll("[data-entry]").Select(element => element.Id!)];

    private static string[] Indexes(IRenderedComponent<TestCompositeList> cut) => [.. cut.FindAll("[data-entry]").Select(element => element.GetAttribute("data-index")!)];

    private static RenderFragment Items(params string[] labels) => Items(false, labels);

    private static RenderFragment Items(bool guess, params string[] labels) => builder =>
    {
        foreach (string label in labels)
        {
            builder.OpenComponent<TestCompositeListItem>(0);
            builder.SetKey(label);
            builder.AddComponentParameter(1, nameof(TestCompositeListItem.Guess), guess);
            builder.AddAttribute(2, "data-entry", label);
            builder.CloseComponent();
        }
    };
}
