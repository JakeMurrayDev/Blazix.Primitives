using AngleSharp.Dom;
using Blazix.Primitives.Rendering;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using LibraryOrientation = Blazix.Primitives.Orientation;

// The namespace is the project's root: a namespace named after the family would hide the parts' types. `Orientation` is
// aliased because SampleRoot.cs declares a test-local `Orientation` in this namespace that would shadow the library's.
namespace Blazix.Primitives.Tests.Components;

// ToolbarButton.test.tsx's [L2] titles verbatim and port titles (marked as such) for the button's element, its two
// behaviours and the disabled cascade. The browser halves (keyboard activation, hover, the substituted parts' blocks) are
// tests/browser/toolbar.spec.ts.
/// <summary>The toolbar button: a composite item over the button mechanism, always in composite mode.</summary>
public sealed class ToolbarButtonTests : IDisposable
{
    private const string MismatchMessage =
        "Blazix: A component that acts as a button expected a native `<button>` because the `NativeButton` parameter is `true`.";

    private readonly ToolbarTestContext test = new();

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "disables the button")]
    [Trait("Requirement", "TLB-API-06")]
    [Trait("Requirement", "TLB-API-09")]
    [Trait("Requirement", "TLB-API-10")]
    [Trait("Requirement", "TLB-DOM-03")]
    [Trait("Requirement", "TLB-DOM-04")]
    [Trait("Requirement", "TLB-ARIA-03")]
    [Trait("Requirement", "TLB-EDGE-04")]
    public void Disables_the_button()
    {
        List<string> calls = [];
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>(p => p.AddChildContent<ToolbarButton>(b => b
            .Add(x => x.Disabled, true)
            .AddUnmatched("onclick", EventCallback.Factory.Create<MouseEventArgs>(this, () => calls.Add("click")))
            .AddUnmatched("onmousedown", EventCallback.Factory.Create<MouseEventArgs>(this, () => calls.Add("mousedown")))
            .AddUnmatched("onpointerdown", EventCallback.Factory.Create<PointerEventArgs>(this, () => calls.Add("pointerdown")))
            .AddUnmatched("onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, () => calls.Add("keydown")))));

        IElement button = cut.Find("button");
        Assert.False(button.HasAttribute("disabled"));
        Assert.True(button.HasAttribute("data-disabled"));
        Assert.Equal("true", button.GetAttribute("aria-disabled"));

        // The module default-prevents the click and pointerdown in a browser (L4); the part leaves the four handlers off
        // the element, so a dispatch has nothing to invoke.
        TryTrigger(() => cut.Find("button").Click());
        TryTrigger(() => cut.Find("button").MouseDown());
        TryTrigger(() => cut.Find("button").PointerDown());
        TryTrigger(() => cut.Find("button").KeyDown(new KeyboardEventArgs { Key = " ", Code = "Space" }));
        TryTrigger(() => cut.Find("button").KeyDown(new KeyboardEventArgs { Key = "Enter", Code = "Enter" }));
        Assert.Empty(calls);
    }

    [Fact(DisplayName = "uses the disabled attribute when focusableWhenDisabled is false")]
    [Trait("Requirement", "TLB-API-07")]
    [Trait("Requirement", "TLB-DOM-04")]
    [Trait("Requirement", "TLB-ARIA-03")]
    public void Uses_the_disabled_attribute_when_focusable_when_disabled_is_false()
    {
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>(p => p.AddChildContent<ToolbarButton>(b => b
            .Add(x => x.Disabled, true)
            .Add(x => x.FocusableWhenDisabled, false)));

        IElement button = cut.Find("button");
        Assert.True(button.HasAttribute("disabled"));
        Assert.True(button.HasAttribute("data-disabled"));
        Assert.False(button.HasAttribute("aria-disabled"));
    }

    [Theory(DisplayName = "port: type or role, disabled and aria-disabled follow the composite rows of NativeButton, FocusableWhenDisabled and Disabled")]
    [Trait("Requirement", "TLB-DOM-04")]
    [Trait("Requirement", "TLB-ARIA-03")]
    [Trait("Requirement", "TLB-API-08")]
    [Trait("Requirement", "TLB-DOM-05")]
    [InlineData(true, true, false, false, "false")]
    [InlineData(true, true, true, false, "true")]
    [InlineData(true, false, false, false, null)]
    [InlineData(true, false, true, true, null)]
    [InlineData(false, true, false, false, null)]
    [InlineData(false, true, true, false, "true")]
    [InlineData(false, false, false, false, null)]
    [InlineData(false, false, true, false, "true")]
    public void Port_the_attributes_follow_the_composite_rows(bool native, bool focusableWhenDisabled, bool disabled, bool disabledAttribute, string? ariaDisabled)
    {
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>(p => p.AddChildContent<ToolbarButton>(b => b
            .Add(x => x.NativeButton, native)
            .Add(x => x.FocusableWhenDisabled, focusableWhenDisabled)
            .Add(x => x.Disabled, disabled)));

        // A non-native row on the default element still gets the default element's type="button" (RND-STR-05).
        IElement button = cut.Find("button");
        Assert.Equal("button", button.GetAttribute("type"));
        Assert.Equal(native ? null : "button", button.GetAttribute("role"));
        Assert.Equal(disabledAttribute, button.HasAttribute("disabled"));
        Assert.Equal(ariaDisabled, button.GetAttribute("aria-disabled"));

        // The button writer renders no tabindex in composite mode; the item's "-1" is the one rendered.
        Assert.Equal("-1", button.GetAttribute("tabindex"));
    }

    [Fact(DisplayName = "port: a toolbar button is an item with a generated id and one button twin in composite mode with its FocusableWhenDisabled")]
    [Trait("Requirement", "TLB-STR-01")]
    [Trait("Requirement", "TLB-API-07")]
    [Trait("Requirement", "TLB-API-08")]
    [Trait("Requirement", "TLB-DOM-05")]
    public void Port_a_toolbar_button_is_an_item_with_one_button_twin_in_composite_mode()
    {
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>(p => p.AddChildContent<ToolbarButton>());

        IElement button = cut.Find("button");
        string id = button.GetAttribute("id")!;
        Assert.StartsWith(PartIdGenerator.Prefix, id, StringComparison.Ordinal);
        Assert.Equal([id], ToolbarTestContext.ItemIds(test.LastCompositeOptions));
        Assert.Equal(
            ["""{"disabled":false,"focusableWhenDisabled":true,"native":true,"composite":true,"reportInteractionType":false,"developmentChecks":true}"""],
            test.ButtonAttaches);

        // The toolbar's disabled state reaches the button through the context, and the button sends it.
        cut.Render(p => p.Add(x => x.Disabled, true));

        Assert.Equal(
            ["""{"disabled":true,"focusableWhenDisabled":true,"native":true,"composite":true,"reportInteractionType":false,"developmentChecks":true}"""],
            test.ButtonUpdates);
        Assert.Equal("true", cut.Find("button").GetAttribute("aria-disabled"));
    }

    [Fact(DisplayName = "port: the button's disabled state is the toolbar's, its group's or its own, and its state carries the orientation and Focusable")]
    [Trait("Requirement", "TLB-API-06")]
    [Trait("Requirement", "TLB-STA-01")]
    [Trait("Requirement", "TLB-STA-05")]
    [Trait("Requirement", "TLB-DOM-03")]
    public void Port_the_buttons_disabled_state_is_the_toolbars_its_groups_or_its_own()
    {
        List<ToolbarButtonState> states = [];
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>(p => p
            .Add(x => x.Orientation, LibraryOrientation.Vertical)
            .AddChildContent(builder =>
            {
                builder.OpenComponent<ToolbarGroup>(0);
                builder.AddComponentParameter(1, nameof(ToolbarGroup.ChildContent), (RenderFragment)(content =>
                {
                    content.OpenComponent<ToolbarButton>(0);
                    content.AddAttribute(1, "data-testid", "grouped");
                    content.AddComponentParameter(2, nameof(ToolbarButton.ClassValue), (Func<ToolbarButtonState, string?>)(state =>
                    {
                        states.Add(state);
                        return null;
                    }));
                    content.CloseComponent();
                }));
                builder.CloseComponent();
                builder.OpenComponent<ToolbarButton>(2);
                builder.AddAttribute(3, "data-testid", "opted-out");
                builder.AddComponentParameter(4, nameof(ToolbarButton.FocusableWhenDisabled), false);
                builder.CloseComponent();
            }));

        IElement grouped = cut.Find("[data-testid=grouped]");
        Assert.False(grouped.HasAttribute("data-disabled"));
        Assert.Equal("vertical", grouped.GetAttribute("data-orientation"));
        Assert.Equal(string.Empty, grouped.GetAttribute("data-focusable"));
        Assert.False(cut.Find("[data-testid=opted-out]").HasAttribute("data-focusable"));
        Assert.Equal(new ToolbarButtonState(Disabled: false, Orientation: LibraryOrientation.Vertical, Focusable: true), states[^1]);

        // The group's own Disabled.
        cut.FindComponent<ToolbarGroup>().Render(p => p.Add(x => x.Disabled, true));
        Assert.Equal(string.Empty, cut.Find("[data-testid=grouped]").GetAttribute("data-disabled"));
        Assert.False(cut.Find("[data-testid=opted-out]").HasAttribute("data-disabled"));
        Assert.Equal(new ToolbarButtonState(Disabled: true, Orientation: LibraryOrientation.Vertical, Focusable: true), states[^1]);

        cut.FindComponent<ToolbarGroup>().Render(p => p.Add(x => x.Disabled, false));
        Assert.False(cut.Find("[data-testid=grouped]").HasAttribute("data-disabled"));

        // The toolbar's Disabled reaches both, and the opted-out button keeps data-focusable off while disabled.
        cut.Render(p => p.Add(x => x.Orientation, LibraryOrientation.Vertical).Add(x => x.Disabled, true));
        Assert.Equal(string.Empty, cut.Find("[data-testid=grouped]").GetAttribute("data-disabled"));
        Assert.Equal(string.Empty, cut.Find("[data-testid=opted-out]").GetAttribute("data-disabled"));
        Assert.False(cut.Find("[data-testid=opted-out]").HasAttribute("data-focusable"));
        Assert.True(cut.Find("[data-testid=opted-out]").HasAttribute("disabled"));

        // The button's own Disabled.
        IRenderedComponent<ToolbarRoot> own = Context.Render<ToolbarRoot>(p => p.AddChildContent<ToolbarButton>(b => b.Add(x => x.Disabled, true)));
        Assert.Equal(string.Empty, own.Find("button").GetAttribute("data-disabled"));
    }

    [Fact(DisplayName = "port: Disabled reaches a substitute only, as an element's attribute and a component's parameter, true and false")]
    [Trait("Requirement", "TLB-API-09")]
    [Trait("Requirement", "TLB-EDGE-04")]
    public void Port_disabled_reaches_a_substitute_only()
    {
        // Design point 14.2 (a), settled by the orchestrator 2026-10-27. No substitute: no disabled attribute while
        // focusable when disabled.
        IRenderedComponent<ToolbarRoot> plain = Context.Render<ToolbarRoot>(p => p.AddChildContent<ToolbarButton>(b => b.Add(x => x.Disabled, true)));
        Assert.False(plain.Find("button").HasAttribute("disabled"));

        // An element substitute receives the attribute.
        RenderFragment element = builder =>
        {
            builder.OpenElement(0, "button");
            builder.CloseElement();
        };
        IRenderedComponent<ToolbarRoot> substituted = Context.Render<ToolbarRoot>(p => p.AddChildContent<ToolbarButton>(b => b
            .Add(x => x.Disabled, true)
            .Add(x => x.Render, element)));
        Assert.True(substituted.Find("button").HasAttribute("disabled"));
        Assert.Equal("true", substituted.Find("button").GetAttribute("aria-disabled"));

        // A component substitute receives the parameter, false included.
        RenderFragment component = builder =>
        {
            builder.OpenComponent<DisabledProbe>(0);
            builder.CloseComponent();
        };
        IRenderedComponent<ToolbarRoot> probed = Context.Render<ToolbarRoot>(p => p.AddChildContent<ToolbarButton>(b => b
            .Add(x => x.Disabled, true)
            .Add(x => x.Render, component)));
        Assert.True(probed.FindComponent<DisabledProbe>().Instance.Disabled);

        probed.FindComponent<ToolbarButton>().Render(p => p.Add(x => x.Disabled, false));
        Assert.False(probed.FindComponent<DisabledProbe>().Instance.Disabled);

        // A substitute callback receives it among the attributes.
        IReadOnlyDictionary<string, object>? attributes = null;
        RenderFragment<RenderProps<ToolbarButtonState>> callback = props => builder =>
        {
            attributes = props.Attributes;
            builder.OpenElement(0, "button");
            builder.AddMultipleAttributes(1, props.Attributes);
            builder.CloseElement();
        };
        Context.Render<ToolbarRoot>(p => p.AddChildContent<ToolbarButton>(b => b.Add(x => x.Disabled, true).Add(x => x.RenderFunction, callback)));
        Assert.Equal(true, attributes!["disabled"]);
    }

    [Fact(DisplayName = "port: a native SwitchRoot substituted into a toolbar button keeps its role and ARIA and is the toolbar's item")]
    [Trait("Requirement", "TLB-ARIA-09")]
    [Trait("Requirement", "TLB-STR-05")]
    [Trait("Requirement", "TLB-DOM-08")]
    public void Port_a_native_switch_root_substituted_into_a_toolbar_button_is_the_toolbars_item()
    {
        // ToolbarButton.test.tsx "renders a switch" (L2 half) over the supported shape, NativeButton="true" over a button
        // (design point 14.7, option (c), chosen by the user 2026-10-27; the non-native shape is open to issue 115): a
        // native switch puts the id it receives on its root. The switch's field and form modules are not this test's
        // subject.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        RenderFragment button = builder =>
        {
            builder.OpenElement(0, "button");
            builder.CloseElement();
        };
        RenderFragment substitute = builder =>
        {
            builder.OpenComponent<SwitchRoot>(0);
            builder.AddComponentParameter(1, nameof(SwitchRoot.NativeButton), true);
            builder.AddComponentParameter(2, nameof(SwitchRoot.DefaultChecked), true);
            builder.AddComponentParameter(3, nameof(SwitchRoot.Render), button);
            builder.CloseComponent();
        };
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>(p => p.AddChildContent<ToolbarButton>(b => b
            .Add(x => x.Disabled, true)
            .Add(x => x.Render, substitute)
            .AddUnmatched("data-testid", "button")));

        IElement element = cut.Find("[data-testid=button]");
        Assert.Equal("BUTTON", element.TagName);
        Assert.Equal("switch", element.GetAttribute("role"));
        Assert.Equal("true", element.GetAttribute("aria-checked"));
        Assert.Equal("true", element.GetAttribute("aria-disabled"));
        Assert.Equal("-1", element.GetAttribute("tabindex"));
        Assert.True(cut.FindComponent<SwitchRoot>().Instance.Disabled);
        Assert.Equal([element.GetAttribute("id")!], ToolbarTestContext.ItemIds(test.LastCompositeOptions));
    }

    [Fact(DisplayName = "port: the consumer's onkeydown runs once, the arrows from navigate and other keys from activate or the button's handler")]
    [Trait("Requirement", "TLB-API-10")]
    [Trait("Requirement", "TLB-KEY-16")]
    public async Task Port_the_consumers_onkeydown_runs_once()
    {
        List<string> keys = [];
        EventCallback<KeyboardEventArgs> onKeyDown = EventCallback.Factory.Create<KeyboardEventArgs>(this, e =>
        {
            keys.Add(e.Key);
            e.PreventPartHandler();
        });
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>(p => p.AddChildContent<ToolbarButton>(b => b.AddUnmatched("onkeydown", onKeyDown)));
        string id = cut.Find("button").GetAttribute("id")!;

        // WebAssembly: the decision runs the handler inside the dispatch; its veto leaves the key to Blazor, which dispatches it.
        Assert.Equal("stop", await test.DecideNavigateAsync(cut, "ArrowRight", id));
        cut.Find("button").KeyDown(new KeyboardEventArgs { Key = "ArrowRight", Code = "ArrowRight" });
        Assert.Equal(["ArrowRight"], keys);

        Assert.Equal("stop", await test.DecideActivateAsync(cut, "Enter"));
        cut.Find("button").KeyDown(new KeyboardEventArgs { Key = "Enter", Code = "Enter" });
        Assert.Equal(["ArrowRight", "Enter"], keys);

        // Home is not the toolbar's and no decision asked about it: the button's handler runs it.
        cut.Find("button").KeyDown(new KeyboardEventArgs { Key = "Home", Code = "Home" });
        Assert.Equal(["ArrowRight", "Enter", "Home"], keys);
    }

    [Fact(DisplayName = "port: a declared-mode mismatch of toolbar buttons is written once per session")]
    [Trait("Requirement", "TLB-EDGE-02")]
    public void Port_a_declared_mode_mismatch_is_written_once_per_session()
    {
        Context.Render<ToolbarRoot>(p => p.AddChildContent(builder =>
        {
            builder.OpenComponent<ToolbarButton>(0);
            builder.CloseComponent();
            builder.OpenComponent<ToolbarButton>(1);
            builder.CloseComponent();
        }));

        test.ButtonTwin(0).OnNativeButtonMismatch("""{"expectedNative":true}""");
        test.ButtonTwin(1).OnNativeButtonMismatch("""{"expectedNative":true}""");

        Assert.Single(test.ButtonMessages, message => message.StartsWith(MismatchMessage, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "port: the data attribute constants are Base UI's")]
    [Trait("Requirement", "TLB-DOM-03")]
    public void Port_the_data_attribute_constants_are_base_uis()
    {
        Assert.Equal("data-disabled", ToolbarButtonDataAttributes.Disabled);
        Assert.Equal("data-orientation", ToolbarButtonDataAttributes.Orientation);
        Assert.Equal("data-focusable", ToolbarButtonDataAttributes.Focusable);
    }

    /// <summary>Tries a dispatch; a handler the part withheld from the element is a handler that was not invoked.</summary>
    private static void TryTrigger(Action trigger)
    {
        try
        {
            trigger();
        }
        catch (MissingEventHandlerException)
        {
        }
    }

    /// <summary>A component substitute that records the <c>Disabled</c> parameter it receives.</summary>
    private sealed class DisabledProbe : ComponentBase
    {
        [Parameter]
        public bool? Disabled { get; set; }

        [Parameter(CaptureUnmatchedValues = true)]
        public IReadOnlyDictionary<string, object>? Attributes { get; set; }

        /// <inheritdoc />
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "button");
            builder.AddMultipleAttributes(1, Attributes);
            builder.CloseElement();
        }
    }
}
