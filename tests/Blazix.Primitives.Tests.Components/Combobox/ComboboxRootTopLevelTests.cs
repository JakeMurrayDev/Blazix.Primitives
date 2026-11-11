using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// ComboboxRoot.test.tsx's top-level `<Combobox.Root />` titles outside any nested describe, at L2 (batch A): the Separator's
// orientation, the internal dismiss buttons, the modal trap's structure, items unloading, browser autofill through the hidden
// input (the `form` module's report, FormTwin.OnAutofill) and the listbox's ARIA. A press, a typed change, a dismiss press
// and the end of an exit animation are the modules' reports (ComboboxRootBatchASteps); Base UI's tests disable animations,
// so a close is followed by the popup's exit report.
/// <summary>Combobox's root at L2: ComboboxRoot.test.tsx's top-level titles.</summary>
public sealed class ComboboxRootTopLevelTests : IDisposable
{
    private const string FocusManagerPath = "./_content/Blazix.Primitives/js/focus-manager.js";

    private static readonly string[] s_appleBanana = ["Apple", "Banana"];

    private static readonly string[] s_abc = ["a", "b", "c"];

    private static readonly string[] s_countries = ["US", "CA"];

    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "exposes the orientation attribute rendered by Separator")]
    [Trait("Requirement", "CMB-STR-08")]
    [Trait("Requirement", "CMB-API-64")]
    [Trait("Requirement", "CMB-STA-15")]
    [Trait("Requirement", "CMB-DOM-16")]
    public void Exposes_the_orientation_attribute_rendered_by_Separator()
    {
        var cut = test.Context.Render(builder =>
        {
            builder.OpenComponent<ComboboxSeparator>(0);
            builder.AddComponentParameter(1, nameof(ComboboxSeparator.Orientation), Orientation.Vertical);
            builder.CloseComponent();
        });

        IElement separator = cut.Find("[role=presentation]");
        Assert.Equal("vertical", separator.GetAttribute(ComboboxSeparatorDataAttributes.Orientation));
    }

    // The Tab presses are the `focus-manager` module's (L4, CMB-ARIA-14's reachability clause). What C# renders and sends for
    // the wrap: the modal trap's inside guards bracket the popup, so a Tab past the extra control lands on the end guard, which
    // the module sends to the first tabbable inside, the input (the start dismiss button has no tabindex); the focus manager is
    // modal, focuses the input initially, and counts both dismiss buttons inside.
    [Fact(DisplayName = "wraps modal focus from popup controls back to the input")]
    [Trait("Requirement", "CMB-ARIA-14")]
    public void Wraps_modal_focus_from_popup_controls_back_to_the_input()
    {
        BunitJSModuleInterop focusManager = test.Context.JSInterop.SetupModule(FocusManagerPath);
        BunitJSModuleInterop focusHandle = focusManager.SetupModule("attach", _ => true);
        focusHandle.SetupVoid("update", _ => true).SetVoidResult();
        focusHandle.SetupVoid("dispose", _ => true).SetVoidResult();
        IRenderedComponent<ComboboxRootBatchAHost> cut = Render(
            new() { ["DefaultOpen"] = true, ["Modal"] = true },
            p => p
                .Add(x => x.WithTrigger, true)
                .Add(x => x.InputInsidePopup, true)
                .Add(x => x.StaticItems, (IReadOnlyList<string>)["apple"])
                .Add(x => x.Labels, _ => "Apple")
                .Add(x => x.PopupExtra, (RenderFragment)ExtraControl));
        IElement input = cut.Find("[data-testid=input]");
        IElement popup = cut.Find("[data-testid=popup]");
        IElement endDismiss = popup.NextElementSibling!;

        IElement startGuard = popup.PreviousElementSibling!;
        IElement endGuard = endDismiss.NextElementSibling!;
        Assert.True(startGuard.HasAttribute("data-base-ui-focus-guard"));
        Assert.True(endGuard.HasAttribute("data-base-ui-focus-guard"));
        Assert.Equal("0", endGuard.GetAttribute("tabindex"));
        List<IElement> tabbables = popup.QuerySelectorAll("input, button, [tabindex]")
            .Where(element => element.GetAttribute("tabindex") != "-1" && element.GetAttribute("role") != "button")
            .ToList();
        Assert.Equal([input, cut.Find("[data-testid=extra]")], tabbables);

        string options = (string)focusManager.Invocations["attach"].Last().Arguments[1]!;
        Assert.Contains("\"modal\":true", options, StringComparison.Ordinal);
        Assert.Contains("\"initialFocusId\":\"" + input.Id + "\"", options, StringComparison.Ordinal);
        Assert.Contains(input.PreviousElementSibling!.Id!, options, StringComparison.Ordinal);
        Assert.Contains(endDismiss.Id!, options, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "does not cause infinite re-renders when items becomes undefined")]
    [Trait("Requirement", "CMB-EDGE-04")]
    public void Does_not_cause_infinite_re_renders_when_items_becomes_undefined()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = Render(new() { ["Items"] = (IReadOnlyList<string>)[], ["DefaultOpen"] = true });

        cut.Render(p => p.Add(x => x.RootParameters, new Dictionary<string, object?> { ["Items"] = null, ["DefaultOpen"] = true }));

        Assert.Equal("true", cut.Find("[data-testid=input]").GetAttribute("aria-expanded"));
    }

    [Fact(DisplayName = "does not render the start dismiss button while closed")]
    [Trait("Requirement", "CMB-STR-07")]
    [Trait("Requirement", "CMB-DOM-21")]
    public void Does_not_render_the_start_dismiss_button_while_closed()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = Render(new() { ["Items"] = (IReadOnlyList<string>)s_appleBanana });

        Assert.Empty(cut.FindAll("[role=button][aria-label=Dismiss]"));
    }

    // The outside button's inert marking is the focus manager module's (L4, CMB-ARIA-15).
    [Fact(DisplayName = "renders internal dismiss buttons before the input and after the popup")]
    [Trait("Requirement", "CMB-STR-07")]
    [Trait("Requirement", "CMB-DOM-22")]
    [Trait("Requirement", "CMB-ARIA-14")]
    public async Task Renders_internal_dismiss_buttons_before_the_input_and_after_the_popup()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = Render(
            new() { ["DefaultOpen"] = true, ["Modal"] = true, ["Items"] = (IReadOnlyList<string>)s_appleBanana },
            p => p.Add(x => x.WithTrigger, true).Add(x => x.InputInsidePopup, true));
        IElement popup = cut.Find("[data-testid=popup]");
        IElement input = cut.Find("[data-testid=input]");
        IReadOnlyList<IElement> dismissButtons = cut.FindAll("[role=button][aria-label=Dismiss]");
        Assert.Equal(2, dismissButtons.Count);

        Assert.Equal(dismissButtons[0], input.PreviousElementSibling);
        Assert.Equal(dismissButtons[1], popup.NextElementSibling);
        Assert.False(dismissButtons[0].HasAttribute("tabindex"));
        Assert.False(dismissButtons[1].HasAttribute("tabindex"));

        await cut.InvokeAsync(() => test.TwinOf(Blazix.Primitives.Interop.ComboboxTwin.PopupPart).OnDismiss(ComboboxTestContext.Click));
        await cut.CompleteCloseAsync();

        Assert.False(cut.HasListbox());
    }

    [Fact(DisplayName = "renders an internal dismiss button for the input-outside-popup pattern")]
    [Trait("Requirement", "CMB-STR-07")]
    public async Task Renders_an_internal_dismiss_button_for_the_input_outside_popup_pattern()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = Render(new() { ["Items"] = (IReadOnlyList<string>)s_appleBanana });

        await cut.PressInputAsync(test);

        IElement popup = cut.Find("[data-testid=popup]");
        IElement input = cut.Find("[data-testid=input]");
        IReadOnlyList<IElement> dismissButtons = cut.FindAll("[role=button][aria-label=Dismiss]");
        Assert.Equal(2, dismissButtons.Count);
        Assert.Equal(dismissButtons[0], input.PreviousElementSibling);
        Assert.Equal(dismissButtons[1], popup.NextElementSibling);
        Assert.False(dismissButtons[0].HasAttribute("tabindex"));
        Assert.False(dismissButtons[1].HasAttribute("tabindex"));

        await cut.InvokeAsync(() => test.TwinOf(Blazix.Primitives.Interop.ComboboxTwin.InputPart).OnDismiss(ComboboxTestContext.Click));
        await cut.CompleteCloseAsync();

        Assert.False(cut.HasListbox());
    }

    [Fact(DisplayName = "shows all items when opening after browser autofill")]
    [Trait("Requirement", "CMB-EVT-11")]
    public async Task Shows_all_items_when_opening_after_browser_autofill()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = Render(new() { ["Name"] = "test", ["Items"] = (IReadOnlyList<string>)s_abc });

        await cut.AutofillAsync(test, "b");
        await cut.PressInputAsync(test);

        Assert.True(cut.HasListbox());
        Assert.Equal(["a", "b", "c"], cut.OptionTexts());
    }

    // Firefox's insertReplacementText on the visible input: the `combobox` module reports it as an untyped change.
    [Fact(DisplayName = "does not open or filter the popup on browser autofill with insertReplacementText")]
    [Trait("Requirement", "CMB-EVT-11")]
    public async Task Does_not_open_or_filter_the_popup_on_browser_autofill_with_insertReplacementText()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = Render(new() { ["Name"] = "test", ["Items"] = (IReadOnlyList<string>)s_abc, ["DefaultValue"] = "b" });

        await cut.InputAsync(test, "B", typed: false);

        Assert.Equal("B", cut.InputText());
        Assert.False(cut.HasListbox());

        await cut.PressInputAsync(test);

        Assert.True(cut.HasListbox());
        Assert.Equal(["a", "b", "c"], cut.OptionTexts());
    }

    // The list mounts with the forced mount the autofill asks for, and its items' order is the list module's report.
    [Fact(DisplayName = "matches browser autofill by serialized value before an earlier rendered label")]
    [Trait("Requirement", "CMB-EVT-11")]
    public async Task Matches_browser_autofill_by_serialized_value_before_an_earlier_rendered_label()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = Render(
            new() { ["Name"] = "country" },
            p => p.Add(x => x.StaticItems, (IReadOnlyList<string>)["CA", "US"]).Add(x => x.Labels, code => code == "CA" ? "US" : "United States"));

        await cut.AutofillAsync(test, "US");
        await cut.ReportOrderAsync(test);
        await cut.PressInputAsync(test);
        await cut.ReportOrderAsync(test);

        Assert.Equal("true", cut.Option("United States").GetAttribute("aria-selected"));
        Assert.Equal("false", cut.Option("US").GetAttribute("aria-selected"));
    }

    [Fact(DisplayName = "matches browser autofill when an earlier item has no rendered label", Skip = "Port deviation (CMB-EVT-11 note, 2026-11-11, option (b)): C# never holds the items' rendered text, so autofill matches derived labels only.")]
    [Trait("Requirement", "CMB-STA-32")]
    [Trait("Requirement", "CMB-EVT-11")]
    public async Task Matches_browser_autofill_when_an_earlier_item_has_no_rendered_label()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = Render(
            new() { ["Name"] = "country" },
            p => p.Add(x => x.StaticItems, (IReadOnlyList<string>)s_countries).Add(x => x.Labels, code => code == "US" ? null : "Canada"));

        await cut.AutofillAsync(test, "Canada");
        await cut.ReportOrderAsync(test);
        await cut.PressInputAsync(test);
        await cut.ReportOrderAsync(test);

        Assert.Equal("true", cut.Option("Canada").GetAttribute("aria-selected"));
    }

    [Fact(DisplayName = "matches browser autofill against an item rendered label when using the items prop", Skip = "Port deviation (CMB-EVT-11 note, 2026-11-11, option (b)): C# never holds the items' rendered text, so autofill matches derived labels only.")]
    [Trait("Requirement", "CMB-EVT-11")]
    public async Task Matches_browser_autofill_against_an_item_rendered_label_when_using_the_items_prop()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = Render(
            new() { ["Name"] = "country", ["Items"] = (IReadOnlyList<string>)s_countries },
            p => p.Add(x => x.Labels, code => code == "US" ? "United States" : "Canada"));

        await cut.AutofillAsync(test, "Canada");
        await cut.PressInputAsync(test);

        Assert.Equal("true", cut.Option("Canada").GetAttribute("aria-selected"));
    }

    [Fact(DisplayName = "does not force-mount the list when autofill matches a serialized value with the items prop")]
    [Trait("Requirement", "CMB-STA-32")]
    public async Task Does_not_force_mount_the_list_when_autofill_matches_a_serialized_value_with_the_items_prop()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = Render(new() { ["Name"] = "country", ["Items"] = (IReadOnlyList<string>)s_countries });

        await cut.AutofillAsync(test, "CA");

        Assert.False(cut.HasListbox());

        await cut.PressInputAsync(test);

        Assert.Equal("true", cut.Option("CA").GetAttribute("aria-selected"));
    }

    [Fact(DisplayName = "marks the field dirty and validates after successful autofill")]
    [Trait("Requirement", "CMB-EVT-11")]
    public async Task Marks_the_field_dirty_and_validates_after_successful_autofill()
    {
        List<object?> validated = [];
        IRenderedComponent<FieldRoot> cut = test.Context.Render<FieldRoot>(p => p
            .Add(x => x.ValidationMode, ValidationMode.OnChange)
            .Add(x => x.Validate, args =>
            {
                validated.Add(args.Value);
                return ValueTask.FromResult<FieldValidationResult?>(args.Value is "CA" ? null : (FieldValidationResult?)"error");
            })
            .AddChildContent(CountryCombobox(new() { ["Name"] = "country" }, inputInsidePopup: false)));
        IRenderedComponent<ComboboxRootBatchAHost> host = cut.FindComponent<ComboboxRootBatchAHost>();
        Assert.False(cut.Find("[data-testid=input]").HasAttribute("data-dirty"));

        await host.AutofillAsync(test, "CA");
        await host.ReportOrderAsync(test);

        Assert.NotEmpty(validated);
        Assert.Equal("CA", validated[^1]);
        Assert.True(cut.Find("[data-testid=input]").HasAttribute("data-dirty"));
    }

    [Fact(DisplayName = "does not update field state when autofill is canceled")]
    [Trait("Requirement", "CMB-EVT-03")]
    [Trait("Requirement", "CMB-EVT-11")]
    public async Task Does_not_update_field_state_when_autofill_is_canceled()
    {
        void Field(RenderTreeBuilder builder)
        {
            builder.OpenComponent<FieldRoot>(0);
            builder.AddComponentParameter(1, nameof(FieldRoot.Name), "country");
            builder.AddComponentParameter(2, nameof(FieldRoot.ChildContent), (RenderFragment)(inner =>
            {
                CountryCombobox(new() { ["OnValueChange"] = CancelingValueChange() }, inputInsidePopup: true)(inner);
                inner.OpenComponent<FieldError>(10);
                inner.AddComponentParameter(11, "data-testid", "error");
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        }

        IRenderedComponent<Form> cut = test.Context.Render<Form>(p => p
            .Add(x => x.Errors, new FormErrors { ["country"] = "server error" })
            .AddChildContent((RenderFragment)Field));
        IRenderedComponent<ComboboxRootBatchAHost> host = cut.FindComponent<ComboboxRootBatchAHost>();
        Assert.False(cut.Find("[data-testid=trigger]").HasAttribute("data-dirty"));
        Assert.Equal("server error", cut.Find("[data-testid=error]").TextContent);

        await host.AutofillAsync(test, "CA");
        await host.ReportOrderAsync(test);

        Assert.False(cut.Find("[data-testid=trigger]").HasAttribute("data-dirty"));
        Assert.Equal("server error", cut.Find("[data-testid=error]").TextContent);
    }

    [Fact(DisplayName = "does not mark the field dirty when input autofill is canceled")]
    [Trait("Requirement", "CMB-EVT-11")]
    public async Task Does_not_mark_the_field_dirty_when_input_autofill_is_canceled()
    {
        IRenderedComponent<FieldRoot> cut = test.Context.Render<FieldRoot>(p => p
            .AddChildContent(CountryCombobox(new() { ["Name"] = "country", ["OnValueChange"] = CancelingValueChange() }, inputInsidePopup: true)));
        IRenderedComponent<ComboboxRootBatchAHost> host = cut.FindComponent<ComboboxRootBatchAHost>();
        Assert.False(cut.Find("[data-testid=trigger]").HasAttribute("data-dirty"));

        await host.AutofillAsync(test, "CA");
        await host.ReportOrderAsync(test);

        Assert.False(cut.Find("[data-testid=trigger]").HasAttribute("data-dirty"));
    }

    [Fact(DisplayName = "should pass autoComplete to the hidden input")]
    [Trait("Requirement", "CMB-API-31")]
    [Trait("Requirement", "CMB-DOM-18")]
    [Trait("Requirement", "CMB-FRM-08")]
    public void Should_pass_autoComplete_to_the_hidden_input()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = Render(
            new() { ["Name"] = "country", ["AutoComplete"] = "country" },
            p => p.Add(x => x.StaticItems, (IReadOnlyList<string>)s_countries).Add(x => x.Labels, CountryName));
        IElement input = cut.Find("[role=combobox]");
        IElement hiddenInput = cut.Find("input[aria-hidden=true]");

        Assert.Equal("off", input.GetAttribute("autocomplete"));
        Assert.False(input.HasAttribute("name"));
        Assert.Equal("country", hiddenInput.GetAttribute("name"));
        Assert.False(hiddenInput.HasAttribute("id"));
        Assert.Equal("country", hiddenInput.GetAttribute("autocomplete"));
    }

    [Fact(DisplayName = "does not render aria-orientation on the listbox role")]
    [Trait("Requirement", "CMB-ARIA-08")]
    public void Does_not_render_aria_orientation_on_the_listbox_role()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = Render(
            new() { ["DefaultOpen"] = true },
            p => p.Add(x => x.InlineList, true).Add(x => x.StaticItems, (IReadOnlyList<string>)["1"]));

        Assert.False(cut.Find("[role=listbox]").HasAttribute("aria-orientation"));
    }

    private static string CountryName(string code) => code == "US" ? "United States" : "Canada";

    private static void ExtraControl(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "button");
        builder.AddAttribute(1, "type", "button");
        builder.AddAttribute(2, "data-testid", "extra");
        builder.AddContent(3, "Extra control");
        builder.CloseElement();
    }

    private EventCallback<ValueChangeEventArgs<string>> CancelingValueChange() =>
        EventCallback.Factory.Create<ValueChangeEventArgs<string>>(this, args => args.Details.Cancel());

    // Base UI's Combobox over the static items United States (US) and Canada (CA), with a Trigger and the input inside the
    // popup, or the input outside.
    private static RenderFragment CountryCombobox(Dictionary<string, object?> root, bool inputInsidePopup) => builder =>
    {
        builder.OpenComponent<ComboboxRootBatchAHost>(0);
        builder.AddComponentParameter(1, nameof(ComboboxRootBatchAHost.RootParameters), root);
        builder.AddComponentParameter(2, nameof(ComboboxRootBatchAHost.StaticItems), (IReadOnlyList<string>)s_countries);
        builder.AddComponentParameter(3, nameof(ComboboxRootBatchAHost.Labels), (Func<string, string?>)CountryName);
        builder.AddComponentParameter(4, nameof(ComboboxRootBatchAHost.InputInsidePopup), inputInsidePopup);
        builder.AddComponentParameter(5, nameof(ComboboxRootBatchAHost.WithTrigger), inputInsidePopup);
        builder.CloseComponent();
    };

    private IRenderedComponent<ComboboxRootBatchAHost> Render(Dictionary<string, object?> root, Action<ComponentParameterCollectionBuilder<ComboboxRootBatchAHost>>? host = null) =>
        test.Context.Render<ComboboxRootBatchAHost>(p =>
        {
            p.Add(x => x.RootParameters, root);
            host?.Invoke(p);
        });
}
