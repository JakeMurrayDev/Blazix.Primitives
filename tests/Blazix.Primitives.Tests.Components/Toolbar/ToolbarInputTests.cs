using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using LibraryOrientation = Blazix.Primitives.Orientation;

// The namespace is the project's root: a namespace named after the family would hide the parts' types. `Orientation` is
// aliased because SampleRoot.cs declares a test-local `Orientation` in this namespace that would shadow the library's.
namespace Blazix.Primitives.Tests.Components;

// ToolbarInput.test.tsx's titles are browser titles (tests/browser/toolbar.spec.ts) or NumberField's (M5); these are port
// titles for the C# half: the element, the option the button twin carries, the handlers and the re-cited rows of an
// `Input` substitute (design point 14.3 (a), pending the user: attributes only).
/// <summary>The toolbar input: a composite item over the button mechanism's input item option, disabled through ARIA only.</summary>
public sealed class ToolbarInputTests : IDisposable
{
    private readonly ToolbarTestContext test = new();

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: an input is an item with tabindex -1, data-orientation and data-focusable, and no type, role, disabled or aria-disabled")]
    [Trait("Requirement", "TLB-DOM-07")]
    [Trait("Requirement", "TLB-API-13")]
    [Trait("Requirement", "TLB-API-14")]
    [Trait("Requirement", "TLB-STR-01")]
    [Trait("Requirement", "TLB-STA-01")]
    public void Port_an_input_is_an_item_with_its_attributes()
    {
        List<ToolbarInputState> states = [];
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>(p => p
            .Add(x => x.Orientation, LibraryOrientation.Vertical)
            .AddChildContent<ToolbarInput>(i => i.Add(x => x.ClassValue, state =>
            {
                states.Add(state);
                return null;
            })));

        IElement input = cut.Find("input");
        string id = input.GetAttribute("id")!;
        Assert.StartsWith("base-ui-", id, StringComparison.Ordinal);
        Assert.Equal([id], ToolbarTestContext.ItemIds(test.LastCompositeOptions));
        Assert.Equal("-1", input.GetAttribute("tabindex"));
        Assert.Equal("vertical", input.GetAttribute("data-orientation"));
        Assert.Equal(string.Empty, input.GetAttribute("data-focusable"));
        foreach (string attribute in new[] { "type", "role", "disabled", "aria-disabled", "data-disabled", "value" })
        {
            Assert.False(input.HasAttribute(attribute), attribute);
        }

        Assert.Equal(new ToolbarInputState(Disabled: false, Orientation: LibraryOrientation.Vertical, Focusable: true), states[^1]);

        // TLB-EDGE-11: a consumer type is forwarded.
        IRenderedComponent<ToolbarRoot> checkbox = Context.Render<ToolbarRoot>(p => p.AddChildContent<ToolbarInput>(i => i
            .Add(x => x.FocusableWhenDisabled, false)
            .AddUnmatched("type", "checkbox")));
        Assert.Equal("checkbox", checkbox.Find("input").GetAttribute("type"));
        Assert.False(checkbox.Find("input").HasAttribute("data-focusable"));
    }

    [Fact(DisplayName = "port: a disabled input renders aria-disabled=\"true\" over the consumer's and never disabled, from any disabled source")]
    [Trait("Requirement", "TLB-API-12")]
    [Trait("Requirement", "TLB-DOM-07")]
    [Trait("Requirement", "TLB-DOM-11")]
    [Trait("Requirement", "TLB-ARIA-04")]
    [Trait("Requirement", "TLB-STA-05")]
    public void Port_a_disabled_input_renders_aria_disabled_over_the_consumers()
    {
        List<ToolbarInputState> states = [];
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>(p => p.AddChildContent<ToolbarInput>(i => i
            .Add(x => x.Disabled, true)
            .AddUnmatched("aria-disabled", "false")
            .Add(x => x.ClassValue, state =>
            {
                states.Add(state);
                return null;
            })));

        IElement input = cut.Find("input");
        Assert.Equal("true", input.GetAttribute("aria-disabled"));
        Assert.Equal(string.Empty, input.GetAttribute("data-disabled"));
        Assert.False(input.HasAttribute("disabled"));
        Assert.Equal(new ToolbarInputState(Disabled: true, Orientation: LibraryOrientation.Horizontal, Focusable: true), states[^1]);

        // Enabled, the consumer's aria-disabled wins.
        cut.FindComponent<ToolbarInput>().Render(p => p.Add(x => x.Disabled, false));
        Assert.Equal("false", cut.Find("input").GetAttribute("aria-disabled"));
        Assert.False(cut.Find("input").HasAttribute("data-disabled"));

        // The toolbar's and the group's disabled states reach it.
        IRenderedComponent<ToolbarRoot> rootDisabled = Context.Render<ToolbarRoot>(p => p.Add(x => x.Disabled, true).AddChildContent<ToolbarInput>());
        Assert.Equal("true", rootDisabled.Find("input").GetAttribute("aria-disabled"));
        IRenderedComponent<ToolbarRoot> groupDisabled = Context.Render<ToolbarRoot>(p => p.AddChildContent<ToolbarGroup>(g => g
            .Add(x => x.Disabled, true)
            .AddChildContent<ToolbarInput>()));
        Assert.Equal("true", groupDisabled.Find("input").GetAttribute("aria-disabled"));
        Assert.False(groupDisabled.Find("input").HasAttribute("disabled"));
    }

    [Fact(DisplayName = "port: the input attaches one button twin with the input item option, not native, in composite mode")]
    [Trait("Requirement", "TLB-API-13")]
    [Trait("Requirement", "TLB-KEY-13")]
    [Trait("Requirement", "TLB-PTR-02")]
    public void Port_the_input_attaches_one_button_twin_with_the_input_item_option()
    {
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>(p => p.AddChildContent<ToolbarInput>());

        Assert.Equal(
            ["""{"disabled":false,"focusableWhenDisabled":true,"native":false,"composite":true,"inputItem":true,"reportInteractionType":false,"developmentChecks":true}"""],
            test.ButtonAttaches);

        cut.Render(p => p.Add(x => x.Disabled, true));

        Assert.Equal(
            ["""{"disabled":true,"focusableWhenDisabled":true,"native":false,"composite":true,"inputItem":true,"reportInteractionType":false,"developmentChecks":true}"""],
            test.ButtonUpdates);
    }

    [Fact(DisplayName = "port: DefaultValue is rendered once as the value attribute")]
    [Trait("Requirement", "TLB-API-14")]
    public void Port_default_value_is_rendered_once_as_the_value_attribute()
    {
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>(p => p.AddChildContent<ToolbarInput>(i => i.Add(x => x.DefaultValue, "abcd")));
        Assert.Equal("abcd", cut.Find("input").GetAttribute("value"));

        // A later DefaultValue renders nothing new: the element's value is the user's from the first render.
        cut.FindComponent<ToolbarInput>().Render(p => p.Add(x => x.DefaultValue, "efgh"));
        Assert.Equal("abcd", cut.Find("input").GetAttribute("value"));
    }

    [Fact(DisplayName = "port: the consumer's onclick, onpointerdown and onkeydown run while disabled, after the module's prevention")]
    [Trait("Requirement", "TLB-API-14")]
    [Trait("Requirement", "TLB-KEY-16")]
    public async Task Port_the_consumers_handlers_run_while_disabled()
    {
        // Design point 14.5 (a), pending the user: the button mechanism in its input item mode declines no handler, and the
        // item gate runs the consumer's onkeydown once.
        List<string> calls = [];
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>(p => p.AddChildContent<ToolbarInput>(i => i
            .Add(x => x.Disabled, true)
            .AddUnmatched("onclick", EventCallback.Factory.Create<MouseEventArgs>(this, () => calls.Add("click")))
            .AddUnmatched("onpointerdown", EventCallback.Factory.Create<PointerEventArgs>(this, () => calls.Add("pointerdown")))
            .AddUnmatched("onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, e =>
            {
                calls.Add(e.Key);
                e.PreventPartHandler();
            }))));
        string id = cut.Find("input").GetAttribute("id")!;

        cut.Find("input").Click();
        cut.Find("input").PointerDown();
        cut.Find("input").KeyDown(new KeyboardEventArgs { Key = "a", Code = "KeyA" });
        Assert.Equal(["click", "pointerdown", "a"], calls);

        // WebAssembly: a key the root handles runs at navigate; its veto leaves the key to Blazor, whose dispatch skips it.
        Assert.Equal("stop", await test.DecideNavigateAsync(cut, "ArrowRight", id, "INPUT"));
        cut.Find("input").KeyDown(new KeyboardEventArgs { Key = "ArrowRight", Code = "ArrowRight" });
        Assert.Equal(["click", "pointerdown", "a", "ArrowRight"], calls);
    }

    [Fact(DisplayName = "port: a substituted Input receives the toolbar input's attributes and no disabled")]
    [Trait("Requirement", "TLB-API-14")]
    [Trait("Requirement", "TLB-API-12")]
    [Trait("Requirement", "TLB-DOM-07")]
    public void Port_a_substituted_input_receives_the_toolbar_inputs_attributes()
    {
        // ToolbarInput.test.tsx "renders NumberField.Input" re-cited over the port's Input, for its attributes only (design
        // point 14.3 (a), pending the user). The field module Input attaches is not this test's subject.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        RenderFragment substitute = builder =>
        {
            builder.OpenComponent<Input>(0);
            builder.CloseComponent();
        };
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>(p => p.AddChildContent<ToolbarInput>(i => i
            .Add(x => x.Disabled, true)
            .Add(x => x.Render, substitute)));

        IElement input = cut.Find("input");
        Assert.Equal("true", input.GetAttribute("aria-disabled"));
        Assert.Equal(string.Empty, input.GetAttribute("data-disabled"));
        Assert.Equal("horizontal", input.GetAttribute("data-orientation"));
        Assert.Equal("-1", input.GetAttribute("tabindex"));
        Assert.False(input.HasAttribute("disabled"));
        Assert.False(cut.FindComponent<Input>().Instance.Disabled);
    }

    [Fact(DisplayName = "port: the data attribute constants are Base UI's")]
    [Trait("Requirement", "TLB-DOM-07")]
    public void Port_the_data_attribute_constants_are_base_uis()
    {
        Assert.Equal("data-disabled", ToolbarInputDataAttributes.Disabled);
        Assert.Equal("data-orientation", ToolbarInputDataAttributes.Orientation);
        Assert.Equal("data-focusable", ToolbarInputDataAttributes.Focusable);
    }
}
