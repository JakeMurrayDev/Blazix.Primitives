using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// CMB §3.8 at L2 (ComboboxClear.test.tsx's titles whose assertions are what C# renders and requests): the visibility, the
// clear-press requests, the disabled and read-only no-ops, the dismissal exemption, and the enter transition's handover
// attribute. The focus move and the press default are the `combobox` module's (L3); the exit animation is L4.
/// <summary>Combobox's Clear at L2.</summary>
public sealed class ComboboxClearTests : IDisposable
{
    private static readonly string[] s_fruits = ["apple", "banana", "cherry"];

    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "renders in single mode when a value is selected")]
    [Trait("Requirement", "CMB-STA-06")]
    [Trait("Requirement", "CMB-DOM-06")]
    public void Renders_in_single_mode_when_a_value_is_selected()
    {
        IElement clear = Render(new() { ["DefaultValue"] = "apple" }).Find("[data-testid=clear]");

        Assert.True(clear.HasAttribute("data-visible"));
        Assert.Equal("-1", clear.GetAttribute("tabindex"));
        Assert.Equal("x", clear.TextContent);
    }

    [Fact(DisplayName = "does not render without a value by default")]
    [Trait("Requirement", "CMB-STA-06")]
    public void Does_not_render_without_a_value_by_default()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new());

        Assert.NotNull(cut.Find("[data-testid=input]"));
        Assert.Empty(cut.FindAll("[data-testid=clear]"));
    }

    [Fact(DisplayName = "port: KeepMounted renders the button while there is nothing to clear")]
    [Trait("Requirement", "CMB-ANI-04")]
    public void KeepMounted_renders_the_button_while_there_is_nothing_to_clear()
    {
        IElement clear = Render(new(), ("KeepMounted", true)).Find("[data-testid=clear]");

        Assert.False(clear.HasAttribute("data-visible"));
    }

    [Fact(DisplayName = "click clears selected value and focuses input")]
    [Trait("Requirement", "CMB-EVT-05")]
    public async Task Click_clears_the_selected_value()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["DefaultValue"] = "apple" });

        await cut.InvokeAsync(() => cut.Find("[data-testid=clear]").Click());

        (string? value, EventReason reason) = Assert.Single(cut.Instance.ValueChanges);
        Assert.Null(value);
        Assert.Equal(EventReason.ClearPress, reason);
        Assert.Contains(cut.Instance.InputChanges, change => change.Text.Length == 0 && change.Reason == EventReason.ClearPress);
        Assert.False(cut.Instance.Root!.Store.State.HasSelectedValue);
    }

    [Fact(DisplayName = "renders and clears chips in multiple mode")]
    [Trait("Requirement", "CMB-EVT-05")]
    [Trait("Requirement", "CMB-STA-06")]
    public async Task Renders_and_clears_chips_in_multiple_mode()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Multiple"] = true, ["DefaultValues"] = (IReadOnlyList<string>)["apple", "cherry"] });

        await cut.InvokeAsync(() => cut.Find("[data-testid=clear]").Click());

        (IReadOnlyList<string> values, EventReason reason) = Assert.Single(cut.Instance.ValuesChanges);
        Assert.Empty(values);
        Assert.Equal(EventReason.ClearPress, reason);
    }

    [Fact(DisplayName = "port: a canceled input text change does not cancel the clear's value change")]
    [Trait("Requirement", "CMB-EVT-05")]
    public async Task A_canceled_text_change_does_not_cancel_the_value_change()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["DefaultValue"] = "apple" });
        cut.Instance.OnInput = args => args.Details.Cancel();

        await cut.InvokeAsync(() => cut.Find("[data-testid=clear]").Click());

        (string? value, EventReason reason) = Assert.Single(cut.Instance.ValueChanges);
        Assert.Null(value);
        Assert.Equal(EventReason.ClearPress, reason);
        Assert.False(cut.Instance.Root!.Store.State.HasSelectedValue);
    }

    [Fact(DisplayName = "is disabled when root disabled and does nothing on click")]
    [Trait("Requirement", "CMB-API-30")]
    public async Task Is_disabled_when_root_disabled_and_does_nothing_on_click()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Disabled"] = true, ["DefaultValue"] = "apple" });
        IElement clear = cut.Find("[data-testid=clear]");
        Assert.True(clear.HasAttribute("disabled"));
        Assert.True(clear.HasAttribute("data-disabled"));

        await cut.InvokeAsync(() => clear.Click());

        Assert.Empty(cut.Instance.ValueChanges);
    }

    [Fact(DisplayName = "when root is readOnly it does nothing on click")]
    [Trait("Requirement", "CMB-API-29")]
    public async Task When_root_is_readOnly_it_does_nothing_on_click()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["ReadOnly"] = true, ["DefaultValue"] = "apple" });

        await cut.InvokeAsync(() => cut.Find("[data-testid=clear]").Click());

        Assert.Empty(cut.Instance.ValueChanges);
        Assert.True(cut.Instance.Root!.Store.State.HasSelectedValue);
    }

    [Fact(DisplayName = "clears selection without closing and restores popup input focus")]
    [Trait("Requirement", "CMB-EVT-05")]
    public async Task Clears_without_closing()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["DefaultOpen"] = true, ["DefaultValue"] = "apple", ["Items"] = (IReadOnlyList<string>)s_fruits });

        await cut.InvokeAsync(() => cut.Find("[data-testid=clear]").Click());

        Assert.Single(cut.Instance.ValueChanges);
        Assert.Empty(cut.Instance.OpenChanges);
        Assert.True(cut.Instance.Root!.Store.IsOpen);
    }

    [Fact(DisplayName = "does not dismiss the popup on click (outsidePress is blocked)")]
    [Trait("Requirement", "CMB-DIS-01")]
    public void Joins_the_outside_press_exemptions()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["DefaultOpen"] = true, ["DefaultValue"] = "apple" }, ("id", "the-clear"));

        Assert.Equal("the-clear", cut.Find("[data-testid=clear]").Id);
        Assert.Contains(SessionOptions(), options => options.Contains("\"outsidePressExemptIds\":[", StringComparison.Ordinal) && options.Contains("\"the-clear\"", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "triggers enter animation via data-starting-style when becoming visible")]
    [Trait("Requirement", "CMB-ANI-04")]
    public async Task Renders_the_starting_style_when_becoming_visible()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["DefaultOpen"] = true, ["Items"] = (IReadOnlyList<string>)s_fruits });
        Assert.Empty(cut.FindAll("[data-testid=clear]"));

        await cut.InvokeAsync(() => cut.Find("[data-testid=item-banana]").Click());

        IElement clear = cut.Find("[data-testid=clear]");
        Assert.True(clear.HasAttribute("data-visible"));
        Assert.True(clear.HasAttribute("data-starting-style"));
    }

    // ComboboxRoot.test.tsx "reports no keyboard reason when the clear button clears the highlight" reads the same at L4; here
    // the store's order: the click's own clear is the one reported, not the cleared text's post-commit reset.
    [Fact(DisplayName = "port: the clear button's highlight clear is reported with its click")]
    [Trait("Requirement", "CMB-API-37")]
    [Trait("Requirement", "CMB-EVT-05")]
    public async Task The_clear_buttons_highlight_clear_is_reported_with_its_click()
    {
        List<ComboboxItemHighlightedEventArgs<string>> highlights = [];
        IRenderedComponent<ComboboxTypedHarness<string>> cut = test.Context.Render<ComboboxTypedHarness<string>>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?>
            {
                ["Items"] = (IReadOnlyList<string>)s_fruits,
                ["DefaultOpen"] = true,
                ["OnItemHighlighted"] = EventCallback.Factory.Create<ComboboxItemHighlightedEventArgs<string>>(this, highlights.Add),
            })
            .Add(x => x.Extra, (RenderFragment)(builder =>
            {
                builder.OpenComponent<ComboboxClear>(0);
                builder.AddAttribute(1, "data-testid", "clear");
                builder.AddAttribute(2, nameof(ComboboxClear.KeepMounted), true);
                builder.CloseComponent();
            })));
        await cut.TypeAsync(test, "ap");
        await cut.HighlightAsync(test, 0, "apple");
        highlights.Clear();

        await cut.InvokeAsync(() => cut.Find("[data-testid=clear]").Click(new Microsoft.AspNetCore.Components.Web.MouseEventArgs { Type = "click" }));

        ComboboxItemHighlightedEventArgs<string> cleared = Assert.Single(highlights);
        Assert.Equal((-1, EventReason.None, "click"), (cleared.Details.Index, cleared.Details.Reason, cleared.Details.Event?.Type));
    }

    private List<string> SessionOptions()
    {
        List<string> options = [];
        foreach (JSRuntimeInvocation attach in test.Interactions.Invocations["attach"])
        {
            if (attach.Arguments[0] is null && attach.Arguments[1] is string json)
            {
                options.Add(json);
            }
        }

        foreach (JSRuntimeInvocation update in test.InteractionsHandle.Invocations["update"])
        {
            if (update.Arguments[0] is string json)
            {
                options.Add(json);
            }
        }

        return options;
    }

    private IRenderedComponent<ComboboxHarness> Render(Dictionary<string, object?> root, params (string Name, object? Value)[] clear)
    {
        void Extra(RenderTreeBuilder builder)
        {
            builder.OpenComponent<ComboboxClear>(0);
            builder.AddComponentParameter(1, "data-testid", "clear");
            foreach ((string name, object? parameter) in clear)
            {
                builder.AddComponentParameter(2, name, parameter);
            }

            builder.CloseComponent();
        }

        return test.Context.Render<ComboboxHarness>(p => p.Add(x => x.RootParameters, root).Add(x => x.Extra, (RenderFragment)Extra));
    }
}
