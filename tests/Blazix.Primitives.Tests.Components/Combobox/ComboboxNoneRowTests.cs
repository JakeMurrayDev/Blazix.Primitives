using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// The rows combobox.md writes `Test: none` for, with the tests Deliverable 10 proposed (PR 193 delta review B1 (d)): the
// internal dismiss buttons' close (CMB-DIS-07) and the ids an opaque render callback drops (CMB-EDGE-13, EDGE-14), at L2.
/// <summary>Combobox's `none` rows at L2.</summary>
public sealed class ComboboxNoneRowTests : IDisposable
{
    private static readonly IReadOnlyList<string> s_items = ["apple", "banana"];

    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    // ComboboxInternalDismissButton.tsx:25-34: the start button is the input's (before it), the end one the popup's (after it).
    [Theory(DisplayName = "port: activating an internal dismiss button closes the popup with close-press and that button as the trigger")]
    [InlineData(ComboboxTwin.InputPart)]
    [InlineData(ComboboxTwin.PopupPart)]
    [Trait("Requirement", "CMB-DIS-07")]
    public async Task Activating_an_internal_dismiss_button_closes_with_close_press(string part)
    {
        List<(EventReason Reason, string? Trigger)> closes = [];
        IRenderedComponent<ComboboxHarness> cut = test.Context.Render<ComboboxHarness>(p => p.Add(x => x.RootParameters, new Dictionary<string, object?>
        {
            ["Items"] = s_items,
            ["DefaultOpen"] = true,
            ["OnOpenChange"] = EventCallback.Factory.Create<ValueChangeEventArgs<bool>>(this, args => closes.Add((args.Details.Reason, args.Details.Trigger?.Id))),
        }));
        string[] dismissIds = [.. cut.FindAll("span[role=button][aria-label=Dismiss]").Select(element => element.Id!)];
        Assert.Equal(2, dismissIds.Length);

        await cut.InvokeAsync(() => test.TwinOf(part).OnDismiss(ComboboxTestContext.Click));

        (EventReason reason, string? trigger) = Assert.Single(closes);
        Assert.Equal(EventReason.ClosePress, reason);
        Assert.Equal(part == ComboboxTwin.InputPart ? dismissIds[0] : dismissIds[1], trigger);
    }

    // CMB-EDGE-13 (resolveRenderedId.ts:12-14; RND-DOM-32): a Popup callback that drops the id it is handed leaves the Trigger's
    // aria-controls naming the computed popup id, which no element carries.
    [Fact(DisplayName = "port: a Popup render callback that drops its id leaves the Trigger's aria-controls naming the computed id")]
    [Trait("Requirement", "CMB-EDGE-13")]
    public void A_popup_callback_that_drops_its_id_leaves_aria_controls_on_the_computed_id()
    {
        RenderFragment<RenderProps<ComboboxPopupState>> dropsId = props => builder =>
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "data-testid", "popup-substitute");
            builder.AddContent(2, props.ChildContent);
            builder.CloseElement();
        };
        void Popup(RenderTreeBuilder builder)
        {
            builder.OpenComponent<ComboboxTrigger>(0);
            builder.AddComponentParameter(1, "data-testid", "trigger");
            builder.CloseComponent();
            builder.OpenComponent<ComboboxPortal>(2);
            builder.AddComponentParameter(3, "ChildContent", (RenderFragment)(portal =>
            {
                portal.OpenComponent<ComboboxPositioner>(0);
                portal.AddComponentParameter(1, "ChildContent", (RenderFragment)(positioner =>
                {
                    positioner.OpenComponent<ComboboxPopup>(0);
                    positioner.AddComponentParameter(1, nameof(ComboboxPopup.RenderFunction), dropsId);
                    positioner.AddComponentParameter(2, "ChildContent", (RenderFragment)(popup =>
                    {
                        popup.OpenComponent<ComboboxInput>(0);
                        popup.CloseComponent();
                    }));
                    positioner.CloseComponent();
                }));
                portal.CloseComponent();
            }));
            builder.CloseComponent();
        }

        IRenderedComponent<ComboboxRoot<string>> cut = test.Context.Render<ComboboxRoot<string>>(p => p
            .Add(x => x.Id, "fruit")
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.Items, s_items)
            .Add(x => x.ChildContent, (RenderFragment)Popup));

        Assert.Equal("fruit-popup", cut.Find("[data-testid=trigger]").GetAttribute("aria-controls"));
        Assert.Empty(cut.FindAll("#fruit-popup"));
    }

    // CMB-EDGE-14 (ComboboxLabel.tsx:24-38; RND-DOM-54): the Label registers the id it computes, `{rootId}-label`, so the
    // Trigger's aria-labelledby names it even when a render callback drops it from the element.
    [Fact(DisplayName = "port: a Label render callback that drops its id leaves the Trigger's aria-labelledby on the computed id")]
    [Trait("Requirement", "CMB-EDGE-14")]
    public void A_label_callback_that_drops_its_id_leaves_aria_labelledby_on_the_computed_id()
    {
        RenderFragment<RenderProps<ComboboxLabelState>> dropsId = props => builder =>
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "data-testid", "label-substitute");
            builder.AddContent(2, props.ChildContent);
            builder.CloseElement();
        };
        void Parts(RenderTreeBuilder builder)
        {
            builder.OpenComponent<ComboboxLabel>(0);
            builder.AddComponentParameter(1, nameof(ComboboxLabel.RenderFunction), dropsId);
            builder.CloseComponent();
            builder.OpenComponent<ComboboxTrigger>(2);
            builder.AddComponentParameter(3, "data-testid", "trigger");
            builder.CloseComponent();
        }

        IRenderedComponent<ComboboxRoot<string>> cut = test.Context.Render<ComboboxRoot<string>>(p => p
            .Add(x => x.Id, "fruit")
            .Add(x => x.Items, s_items)
            .Add(x => x.ChildContent, (RenderFragment)Parts));

        Assert.Equal("fruit-label", cut.Find("[data-testid=trigger]").GetAttribute("aria-labelledby"));
        Assert.Empty(cut.FindAll("#fruit-label"));
    }
}
