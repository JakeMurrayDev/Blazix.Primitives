using AngleSharp.Dom;
using Blazix.Primitives.Rendering;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The <c>[L2]</c> rows of docs/components/button.md: the attributes C# renders (UBT-ALG-03 with Button's explicit
/// <c>FocusableWhenDisabled</c>, BTN-DOM-01..05, BTN-ARIA-01/02, BTN-STA-01) and the disabled gate on the consumer's
/// handlers as Blazor dispatches them (BTN-API-05, BTN-PTR-01, UBT-EVT-01/03). Titles are <c>Button.test.tsx</c>'s; at
/// this layer each title asserts its attribute clauses and the handlers Blazor would invoke, while focus, keyboard
/// activation and the module's default-prevention are the same titles at L4 (tests/browser/button.spec.ts).
/// </summary>
public sealed class ButtonTests : IDisposable
{
    private static readonly RenderFragment s_span = builder =>
    {
        builder.OpenElement(0, "span");
        builder.CloseElement();
    };

    private readonly BunitContext context = new();

    public ButtonTests()
    {
        context.Services.AddBlazixPrimitives();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "native button: renders type=\"button\" and tabindex=\"0\" with no role, disabled or aria-disabled by default")]
    [Trait("Requirement", "BTN-DOM-02")]
    [Trait("Requirement", "BTN-DOM-03")]
    [Trait("Requirement", "BTN-STR-01")]
    public void Native_button_renders_type_button_and_tabindex_0_by_default()
    {
        IRenderedComponent<Button> cut = context.Render<Button>(p => p.AddChildContent("Save"));

        IElement button = cut.Find("button");
        Assert.Single(cut.Nodes);
        Assert.Equal("button", button.GetAttribute("type"));
        Assert.Equal("0", button.GetAttribute("tabindex"));
        Assert.False(button.HasAttribute("role"));
        Assert.False(button.HasAttribute("disabled"));
        Assert.False(button.HasAttribute("aria-disabled"));
        Assert.False(button.HasAttribute("data-disabled"));
        Assert.False(button.HasAttribute("id"));
        Assert.Equal("Save", button.TextContent);
    }

    [Fact(DisplayName = "native button: uses the disabled attribute and is not focusable")]
    [Trait("Requirement", "BTN-API-01")]
    [Trait("Requirement", "BTN-API-02")]
    [Trait("Requirement", "BTN-DOM-01")]
    [Trait("Requirement", "BTN-DOM-04")]
    [Trait("Requirement", "BTN-ARIA-02")]
    public void Native_button_uses_the_disabled_attribute_and_is_not_focusable()
    {
        HandlerLog log = new();

        IRenderedComponent<Button> cut = context.Render<Button>(p => log.AddGatedHandlers(p.Add(x => x.Disabled, true)));

        IElement button = cut.Find("button");
        Assert.True(button.HasAttribute("disabled"));
        Assert.Equal("", button.GetAttribute(ButtonDataAttributes.Disabled));
        Assert.False(button.HasAttribute("aria-disabled"));

        log.TriggerGated(cut, "button");
        Assert.Empty(log.Calls);
    }

    [Fact(DisplayName = "custom element: applies aria-disabled and is not focusable")]
    [Trait("Requirement", "BTN-DOM-01")]
    [Trait("Requirement", "BTN-DOM-03")]
    [Trait("Requirement", "BTN-DOM-04")]
    [Trait("Requirement", "BTN-ARIA-02")]
    public void Custom_element_applies_aria_disabled_and_is_not_focusable()
    {
        HandlerLog log = new();

        IRenderedComponent<Button> cut = context.Render<Button>(p => log.AddGatedHandlers(p
            .Add(x => x.Disabled, true)
            .Add(x => x.NativeButton, false)
            .Add(x => x.Render, s_span)));

        IElement button = cut.Find("span");
        Assert.False(button.HasAttribute("disabled"));
        Assert.Equal("", button.GetAttribute(ButtonDataAttributes.Disabled));
        Assert.Equal("true", button.GetAttribute("aria-disabled"));
        Assert.Equal("-1", button.GetAttribute("tabindex"));

        log.TriggerGated(cut, "span");
        Assert.Empty(log.Calls);
    }

    [Fact(DisplayName = "native button: prevents interactions but remains focusable")]
    [Trait("Requirement", "BTN-API-02")]
    [Trait("Requirement", "BTN-API-05")]
    [Trait("Requirement", "BTN-DOM-01")]
    [Trait("Requirement", "BTN-DOM-03")]
    [Trait("Requirement", "BTN-DOM-04")]
    [Trait("Requirement", "BTN-DOM-05")]
    [Trait("Requirement", "BTN-ARIA-02")]
    public void Native_button_prevents_interactions_but_remains_focusable()
    {
        HandlerLog log = new();

        IRenderedComponent<Button> cut = context.Render<Button>(p => log.AddGatedHandlers(p
            .Add(x => x.Disabled, true)
            .Add(x => x.FocusableWhenDisabled, true)));

        IElement button = cut.Find("button");
        Assert.False(button.HasAttribute("disabled"));
        Assert.Equal("", button.GetAttribute(ButtonDataAttributes.Disabled));
        Assert.Equal("true", button.GetAttribute("aria-disabled"));
        Assert.Equal("0", button.GetAttribute("tabindex"));

        // All five gated handlers: click, mousedown, pointerdown, keydown, keyup (UBT-EVT-01/03).
        log.TriggerGated(cut, "button");
        Assert.Empty(log.Calls);
    }

    [Fact(DisplayName = "native button: allows hover handlers while blocking activation")]
    [Trait("Requirement", "BTN-API-05")]
    [Trait("Requirement", "BTN-DOM-01")]
    public void Native_button_allows_hover_handlers_while_blocking_activation()
    {
        HandlerLog log = new();

        IRenderedComponent<Button> cut = context.Render<Button>(p => p
            .Add(x => x.Disabled, true)
            .Add(x => x.FocusableWhenDisabled, true)
            .AddUnmatched("onclick", log.Handler<MouseEventArgs>("click"))
            .AddUnmatched("onmousemove", log.Handler<MouseEventArgs>("mousemove")));

        IElement button = cut.Find("button");
        Assert.False(button.HasAttribute("disabled"));
        Assert.Equal("", button.GetAttribute(ButtonDataAttributes.Disabled));
        Assert.Equal("true", button.GetAttribute("aria-disabled"));

        button.MouseMove();
        HandlerLog.TryTrigger(() => cut.Find("button").Click());

        Assert.Equal(["mousemove"], log.Calls);
    }

    [Fact(DisplayName = "custom element: prevents interactions but remains focusable")]
    [Trait("Requirement", "BTN-DOM-01")]
    [Trait("Requirement", "BTN-DOM-03")]
    [Trait("Requirement", "BTN-DOM-04")]
    [Trait("Requirement", "BTN-ARIA-02")]
    public void Custom_element_prevents_interactions_but_remains_focusable()
    {
        HandlerLog log = new();

        IRenderedComponent<Button> cut = context.Render<Button>(p => log.AddGatedHandlers(p
            .Add(x => x.Disabled, true)
            .Add(x => x.FocusableWhenDisabled, true)
            .Add(x => x.NativeButton, false)
            .Add(x => x.Render, s_span)));

        IElement button = cut.Find("span");
        Assert.False(button.HasAttribute("disabled"));
        Assert.Equal("", button.GetAttribute(ButtonDataAttributes.Disabled));
        Assert.Equal("true", button.GetAttribute("aria-disabled"));
        Assert.Equal("0", button.GetAttribute("tabindex"));

        log.TriggerGated(cut, "span");
        Assert.Empty(log.Calls);
    }

    [Fact(DisplayName = "custom element: applies button semantics and dispatches real clicks from keyboard activation")]
    [Trait("Requirement", "BTN-STR-01")]
    [Trait("Requirement", "BTN-API-03")]
    [Trait("Requirement", "BTN-ARIA-01")]
    [Trait("Requirement", "BTN-DOM-03")]
    [Trait("Requirement", "BTN-DOM-05")]
    public void Custom_element_applies_button_semantics_and_dispatches_real_clicks_from_keyboard_activation()
    {
        // The attribute and tagName clauses; the keyboard clicks are L4. The substitute's own handler is not gated and
        // runs before the consumer's (RND §2.1 row 5).
        List<string> log = [];
        IRenderedComponent<Button> cut = context.Render<Button>(p => p
            .Add(x => x.NativeButton, false)
            .AddUnmatched("onclick", EventCallback.Factory.Create<MouseEventArgs>(this, () => log.Add("consumer")))
            .Add(x => x.Render, (RenderFragment)(builder =>
            {
                builder.OpenElement(0, "span");
                builder.AddAttribute(1, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, () => log.Add("render")));
                builder.CloseElement();
            }))
            .AddChildContent("Save"));

        IElement button = cut.Find("[role=button]");
        Assert.Equal("SPAN", button.TagName);
        Assert.Equal("button", button.GetAttribute("role"));
        Assert.Equal("0", button.GetAttribute("tabindex"));
        Assert.False(button.HasAttribute("type"));

        button.Click();
        Assert.Equal(["render", "consumer"], log);
    }

    [Fact(DisplayName = "custom link element: Space activates the link without scrolling the page")]
    [Trait("Requirement", "BTN-ARIA-01")]
    public void Custom_link_element_Space_activates_the_link_without_scrolling_the_page()
    {
        // The rendered-semantics clause: the <a> is exposed as a button. Activation and navigation are L4.
        IRenderedComponent<Button> cut = context.Render<Button>(p => p
            .Add(x => x.NativeButton, false)
            .Add(x => x.Render, (RenderFragment)(builder =>
            {
                builder.OpenElement(0, "a");
                builder.AddAttribute(1, "href", "#target");
                builder.CloseElement();
            }))
            .AddChildContent("Go"));

        IElement link = cut.Find("[role=button]");
        Assert.Equal("A", link.TagName);
        Assert.Equal("#target", link.GetAttribute("href"));
        Assert.Equal("0", link.GetAttribute("tabindex"));
    }

    [Fact(DisplayName = "keeps focus and suppresses interactions after becoming disabled")]
    [Trait("Requirement", "BTN-PTR-01")]
    [Trait("Requirement", "BTN-ARIA-02")]
    public void Keeps_focus_and_suppresses_interactions_after_becoming_disabled()
    {
        IRenderedComponent<DisablingConsumer> cut = context.Render<DisablingConsumer>();

        Assert.Equal("false", cut.Find("button").GetAttribute("aria-disabled"));
        cut.Find("button").Click();

        Assert.Equal(1, cut.Instance.Clicks);
        Assert.Equal("true", cut.Find("button").GetAttribute("aria-disabled"));
        Assert.False(cut.Find("button").HasAttribute("disabled"));

        HandlerLog.TryTrigger(() => cut.Find("button").Click());
        Assert.Equal(1, cut.Instance.Clicks);
    }

    [Fact(DisplayName = "an enabled native button with focusableWhenDisabled renders aria-disabled=\"false\"")]
    [Trait("Requirement", "BTN-ARIA-02")]
    public void An_enabled_native_button_with_focusableWhenDisabled_renders_aria_disabled_false()
    {
        IRenderedComponent<Button> cut = context.Render<Button>(p => p.Add(x => x.FocusableWhenDisabled, true));

        Assert.Equal("false", cut.Find("button").GetAttribute("aria-disabled"));
        Assert.False(cut.Find("button").HasAttribute("data-disabled"));
    }

    [Fact(DisplayName = "an enabled button calls the consumer's click, mousedown, pointerdown, keydown and keyup handlers once each")]
    [Trait("Requirement", "BTN-PTR-01")]
    [Trait("Requirement", "BTN-API-05")]
    public void An_enabled_button_calls_the_consumers_handlers_once_each()
    {
        HandlerLog log = new();
        IRenderedComponent<Button> cut = context.Render<Button>(p => log.AddGatedHandlers(p));

        log.TriggerGated(cut, "button");

        Assert.Equal(["click", "mousedown", "pointerdown", "keydown", "keyup"], log.Calls);
    }

    [Fact(DisplayName = "consumer attributes override the attributes the mechanism renders")]
    [Trait("Requirement", "BTN-API-05")]
    [Trait("Requirement", "BTN-DOM-05")]
    public void Consumer_attributes_override_the_attributes_the_mechanism_renders()
    {
        IRenderedComponent<Button> native = context.Render<Button>(p => p
            .Add(x => x.Disabled, true)
            .AddUnmatched("type", "submit")
            .AddUnmatched("tabindex", "-1")
            .AddUnmatched("aria-disabled", "false")
            .AddUnmatched("form", "f")
            .AddUnmatched("name", "n")
            .AddUnmatched("value", "v"));
        IRenderedComponent<Button> custom = context.Render<Button>(p => p
            .Add(x => x.NativeButton, false)
            .Add(x => x.Render, s_span)
            .AddUnmatched("role", "switch"));

        IElement button = native.Find("button");
        Assert.Equal("submit", button.GetAttribute("type"));
        Assert.Equal("-1", button.GetAttribute("tabindex"));
        Assert.Equal("false", button.GetAttribute("aria-disabled"));
        Assert.Equal(("f", "n", "v"), (button.GetAttribute("form"), button.GetAttribute("name"), button.GetAttribute("value")));
        Assert.Equal("switch", custom.Find("span").GetAttribute("role"));
    }

    [Fact(DisplayName = "type on a non-native button is forwarded")]
    [Trait("Requirement", "BTN-API-05")]
    public void Type_on_a_non_native_button_is_forwarded()
    {
        IRenderedComponent<Button> cut = context.Render<Button>(p => p
            .Add(x => x.NativeButton, false)
            .Add(x => x.Render, s_span)
            .AddUnmatched("type", "submit"));

        Assert.Equal("submit", cut.Find("span").GetAttribute("type"));
    }

    [Fact(DisplayName = "the state holds the disabled parameter alone")]
    [Trait("Requirement", "BTN-STA-01")]
    public void The_state_holds_the_disabled_parameter_alone()
    {
        List<ButtonState> seen = [];
        IRenderedComponent<Button> cut = context.Render<Button>(p => p
            .Add(x => x.Disabled, true)
            .Add(x => x.FocusableWhenDisabled, true)
            .Add(x => x.ClassValue, (Func<ButtonState, string?>)(state =>
            {
                seen.Add(state);
                return state.Disabled ? "is-disabled" : "is-enabled";
            })));

        Assert.Equal(new ButtonState(Disabled: true), seen[^1]);
        Assert.Contains("is-disabled", cut.Find("button").ClassList);

        cut.Render(p => p.Add(x => x.Disabled, false));
        Assert.Equal(new ButtonState(Disabled: false), seen[^1]);
        Assert.Contains("is-enabled", cut.Find("button").ClassList);
    }

    [Fact(DisplayName = "data-disabled follows disabled at runtime")]
    [Trait("Requirement", "BTN-DOM-01")]
    public void Data_disabled_follows_disabled_at_runtime()
    {
        IRenderedComponent<Button> cut = context.Render<Button>();
        Assert.False(cut.Find("button").HasAttribute("data-disabled"));

        cut.Render(p => p.Add(x => x.Disabled, true));
        Assert.True(cut.Find("button").HasAttribute("data-disabled"));
        Assert.True(cut.Find("button").HasAttribute("disabled"));

        cut.Render(p => p.Add(x => x.Disabled, false));
        Assert.False(cut.Find("button").HasAttribute("data-disabled"));
        Assert.False(cut.Find("button").HasAttribute("disabled"));
    }

    [Fact(DisplayName = "ButtonDataAttributes names data-disabled")]
    [Trait("Requirement", "BTN-DOM-01")]
    public void ButtonDataAttributes_names_data_disabled()
    {
        Assert.Equal("data-disabled", ButtonDataAttributes.Disabled);
    }

    /// <summary>Records the consumer handlers the part let Blazor invoke, by event name.</summary>
    private sealed class HandlerLog
    {
        public List<string> Calls { get; } = [];

        /// <summary>Tries a dispatch; a handler the part withheld from the element is a handler that was not invoked.</summary>
        public static void TryTrigger(Action trigger)
        {
            try
            {
                trigger();
            }
            catch (MissingEventHandlerException)
            {
            }
        }

        public EventCallback<TArgs> Handler<TArgs>(string name) => EventCallback.Factory.Create<TArgs>(this, () => Calls.Add(name));

        public ComponentParameterCollectionBuilder<Button> AddGatedHandlers(ComponentParameterCollectionBuilder<Button> p)
        {
            return p
                .AddUnmatched("onclick", Handler<MouseEventArgs>("click"))
                .AddUnmatched("onmousedown", Handler<MouseEventArgs>("mousedown"))
                .AddUnmatched("onpointerdown", Handler<PointerEventArgs>("pointerdown"))
                .AddUnmatched("onkeydown", Handler<KeyboardEventArgs>("keydown"))
                .AddUnmatched("onkeyup", Handler<KeyboardEventArgs>("keyup"));
        }

        public void TriggerGated(IRenderedComponent<Button> cut, string selector)
        {
            TryTrigger(() => cut.Find(selector).Click());
            TryTrigger(() => cut.Find(selector).MouseDown());
            TryTrigger(() => cut.Find(selector).PointerDown());
            TryTrigger(() => cut.Find(selector).KeyDown(new KeyboardEventArgs { Key = "Enter", Code = "Enter", Type = "keydown" }));
            TryTrigger(() => cut.Find(selector).KeyUp(new KeyboardEventArgs { Key = " ", Code = "Space", Type = "keyup" }));
        }
    }
}
