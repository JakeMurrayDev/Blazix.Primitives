using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// Two registrations on one element (M3 track 1, E4; ADR 04 Amendments 2026-10-26, chosen by the user): an outer
/// composite item whose <c>Render</c> is an inner composite item, as a <c>Toolbar.Button</c> substituting a <c>Toggle</c>.
/// Base UI's outer item attaches its ref last and the last registration wins (<c>CompositeItem.tsx:28-31</c>); Blazor sets
/// parameters parent before child, so the root sends the first registration per id, and the inner item's registration,
/// its flags and the <c>tabindex="-1"</c> the outer item put in its bag are never sent for that id. The key path still
/// runs the outer item's consumer <c>onkeydown</c> once (option (i) of ADR 04's note of 2026-10-22).
/// </summary>
public sealed class CompositeOuterRegistrationTests : IDisposable
{
    private readonly CompositeTestContext host = new();
    private readonly List<string> runs = [];

    private BunitContext Context => host.Context;

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    private static string KeyDownJson(string key, string targetId) => $$"""
        {"kind":"KeyboardEvent","type":"keydown","key":"{{key}}","code":"{{key}}","altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":null,"detail":0,"buttons":0,
         "target":{"id":"{{targetId}}","tagName":"BUTTON"},"defaultPrevented":false,"timeStamp":3}
        """;

    [Fact(DisplayName = "sends one registration per id, the outer item's, without the tabindex the outer item put in the inner item's bag")]
    [Trait("Requirement", "CMP-STR-04")]
    public void Sends_one_registration_per_id_the_outer_items()
    {
        IRenderedComponent<TestCompositeRoot> cut = Context.Render<TestCompositeRoot>(p => p
            .Add(x => x.DeriveDisabledIndices, true)
            .AddChildContent(Nested(innerFocusable: false)));
        string[] ids = CompositeTestContext.RenderedItemIds(cut);
        string attached = (string)Assert.Single(host.Attaches).Arguments[1]!;

        Assert.Equal(2, ids.Length);
        Assert.Equal(ids, CompositeTestContext.ItemIds(attached));
        Assert.Contains($$"""
            {"id":"{{ids[1]}}","disabled":true,"focusableWhenDisabled":true}
            """.Trim(), attached);
        Assert.DoesNotContain("tabIndex", attached);
    }

    // Restated from CompositeRoot.test.tsx (dated 2026-10-26, M3 track 1): what Base UI publishes in the item map is what
    // the root sends in its registrations, and the rule is C#'s (the first registration per id, parameters being set
    // parent before child), so the title runs at L2 over the options sent.
    [Fact(DisplayName = "keeps the outer registration when only the inner item updates")]
    [Trait("Requirement", "CMP-STR-04")]
    [Trait("Requirement", "CMP-STA-10")]
    public void Keeps_the_outer_registration_when_only_the_inner_item_updates()
    {
        IRenderedComponent<TestCompositeRoot> cut = Context.Render<TestCompositeRoot>(p => p.AddChildContent(Nested(innerFocusable: false)));
        string shared = CompositeTestContext.RenderedItemIds(cut)[1];

        cut.Render(p => p.AddChildContent(Nested(innerFocusable: true)));
        Assert.Empty(host.UpdateOptions);

        cut.Render(p => p.AddChildContent(Nested(innerFocusable: true, outerFocusable: false)));
        string updated = Assert.Single(host.UpdateOptions);
        Assert.Contains($$"""
            {"id":"{{shared}}","disabled":true}
            """.Trim(), updated);
        Assert.Equal(CompositeTestContext.RenderedItemIds(cut), CompositeTestContext.ItemIds(updated));
    }

    [Fact(DisplayName = "the navigate decision runs the outer item's consumer onkeydown once for a key on a shared element, and its veto stops the key")]
    [Trait("Requirement", "CMP-STR-04")]
    [Trait("Requirement", "CMP-EVT-12")]
    [Trait("Requirement", "EVD-EVT-26")]
    public void The_navigate_decision_runs_the_outer_items_handler_once()
    {
        IRenderedComponent<TestCompositeRoot> cut = Context.Render<TestCompositeRoot>(p => p.AddChildContent(Nested(innerFocusable: false, outerHandler: true)));
        string shared = CompositeTestContext.RenderedItemIds(cut)[1];

        Assert.Equal("run", host.Twin.Decide(CompositeTwin.NavigatePoint, KeyDownJson("ArrowRight", shared)));
        Assert.Equal(["outer: ArrowRight"], runs);

        cut.Render(p => p.AddChildContent(Nested(innerFocusable: false, outerHandler: true, veto: true)));
        Assert.Equal("stop", host.Twin.Decide(CompositeTwin.NavigatePoint, KeyDownJson("ArrowLeft", shared)));
        Assert.Equal(["outer: ArrowRight", "outer: ArrowLeft"], runs);

        // The vetoed key is not stopped, so Blazor dispatches it to the inner item's handler, which runs the outer item's
        // handler as its consumer's; the one-shot marker skips it.
        cut.FindAll("[data-item]")[1].KeyDown(new KeyboardEventArgs { Key = "ArrowLeft", Code = "ArrowLeft" });
        Assert.Equal(["outer: ArrowRight", "outer: ArrowLeft"], runs);
    }

    [Fact(DisplayName = "the handled-key report runs the outer item's consumer onkeydown once for a key on a shared element (Server)")]
    [Trait("Requirement", "CMP-STR-04")]
    [Trait("Requirement", "CMP-EVT-12")]
    public void The_handled_key_report_runs_the_outer_items_handler_once()
    {
        IRenderedComponent<TestCompositeRoot> cut = Context.Render<TestCompositeRoot>(p => p.AddChildContent(Nested(innerFocusable: false, outerHandler: true)));
        string shared = CompositeTestContext.RenderedItemIds(cut)[1];
        Assert.True(CompositeTestContext.Parse((string)Assert.Single(host.Attaches).Arguments[1]!).GetProperty("reportHandledKeys").GetBoolean());

        cut.InvokeAsync(() => host.Twin.OnKeyHandled($$"""{"event":{{KeyDownJson("ArrowRight", shared)}}}"""));

        Assert.Equal(["outer: ArrowRight"], runs);
    }

    // Base UI's NestedItemsRoot: a first item, then an outer item focusable when disabled whose element is an inner item
    // that is not.
    private RenderFragment Nested(bool innerFocusable, bool outerFocusable = true, bool outerHandler = false, bool veto = false) => builder =>
    {
        builder.OpenComponent<TestCompositeItem>(0);
        builder.SetKey("first");
        builder.AddAttribute(1, "data-item", "first");
        builder.CloseComponent();

        builder.OpenComponent<TestCompositeItem>(2);
        builder.SetKey("shared");
        builder.AddAttribute(3, "data-item", "shared");
        builder.AddAttribute(4, nameof(TestCompositeItem.Disabled), true);
        builder.AddAttribute(5, nameof(TestCompositeItem.FocusableWhenDisabled), outerFocusable);
        if (outerHandler)
        {
            builder.AddAttribute(6, "onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, (KeyboardEventArgs e) =>
            {
                runs.Add($"outer: {e.Key}");
                if (veto)
                {
                    e.PreventPartHandler();
                }
            }));
        }

        builder.AddAttribute(7, nameof(TestCompositeItem.Render), (RenderFragment)(inner =>
        {
            inner.OpenComponent<TestCompositeItem>(0);
            inner.AddAttribute(1, nameof(TestCompositeItem.Disabled), true);
            inner.AddAttribute(2, nameof(TestCompositeItem.FocusableWhenDisabled), innerFocusable);
            inner.CloseComponent();
        }));
        builder.CloseComponent();
    };
}
