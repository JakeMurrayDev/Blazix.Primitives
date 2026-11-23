using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// Button's twin contract (ADR 06 §1, L2): the one <see cref="ButtonTwin"/> per part, its <c>attach</c>, <c>update</c>
/// and <c>dispose</c> as bUnit's JS interop mock records them (BTN-API-01..03: <c>focusableWhenDisabled</c> always an
/// explicit boolean), the activation decider that runs the consumer's key handler inside the dispatch (UBT-KEY-06,
/// ADR 04 §3), and the development messages of the declared-mode check, logged once per session (UBT-EDGE-01..03).
/// </summary>
public sealed class ButtonTwinWiringTests : IDisposable
{
    private const string ModulePath = "./_content/Blazix.Primitives/js/button.js";

    private const string NativeMismatchMessage =
        "Blazix: A component that acts as a button expected a native `<button>` because the `NativeButton` parameter is `true`. "
        + "Rendering a non-`<button>` removes native button semantics, which can impact forms and accessibility. "
        + "Use a real `<button>` in the `Render` parameter, or set `NativeButton` to `false`.";

    private const string NonNativeMismatchMessage =
        "Blazix: A component that acts as a button expected a non-`<button>` because the `NativeButton` parameter is `false`. "
        + "Rendering a `<button>` keeps native behavior while the library applies non-native attributes and handlers, which can add "
        + "unintended extra attributes (such as `role` or `aria-disabled`). "
        + "Use a non-`<button>` in the `Render` parameter, or set `NativeButton` to `true`.";

    private readonly BunitContext context = new();
    private readonly BunitJSModuleInterop module;
    private readonly BunitJSModuleInterop handle;
    private readonly CapturingLogger<Button> logger = new();

    public ButtonTwinWiringTests()
    {
        context.Services.AddBlazixPrimitives();
        context.Services.AddSingleton<ILogger<Button>>(logger);
        module = context.JSInterop.SetupModule(ModulePath);
        handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "attaches one button twin to the element after the first render with the explicit options")]
    [Trait("Verifies", "ADR06-3")]
    [Trait("Requirement", "BTN-API-01")]
    [Trait("Requirement", "BTN-API-02")]
    [Trait("Requirement", "BTN-API-03")]
    public void Attaches_one_button_twin_to_the_element_after_the_first_render_with_the_explicit_options()
    {
        IRenderedComponent<Button> cut = context.Render<Button>();

        JSRuntimeInvocation attach = Assert.Single(module.Invocations["attach"]);
        Assert.Equal(cut.Instance.Element, Assert.IsType<ElementReference>(attach.Arguments[0]));
        Assert.Equal(
            """{"disabled":false,"focusableWhenDisabled":false,"native":true,"composite":false,"reportInteractionType":false,"developmentChecks":true}""",
            attach.Arguments[1]);
        Assert.IsType<DotNetObjectReference<ButtonTwin>>(attach.Arguments[2]);

        cut.Render();
        Assert.Single(module.Invocations["attach"]);
    }

    [Fact(DisplayName = "sends the options again when Disabled, FocusableWhenDisabled or NativeButton changes, and only then")]
    [Trait("Requirement", "BTN-API-01")]
    [Trait("Requirement", "BTN-API-02")]
    [Trait("Requirement", "BTN-API-03")]
    public void Sends_the_options_again_when_a_mechanism_parameter_changes_and_only_then()
    {
        IRenderedComponent<Button> cut = context.Render<Button>();

        cut.Render(p => p.Add(x => x.Disabled, true));
        cut.Render(p => p.Add(x => x.Disabled, true).AddUnmatched("data-x", "1"));
        cut.Render(p => p.Add(x => x.Disabled, true).Add(x => x.FocusableWhenDisabled, true));
        cut.Render(p => p.Add(x => x.Disabled, true).Add(x => x.FocusableWhenDisabled, true).Add(x => x.NativeButton, false));

        Assert.Equal(
            [
                """{"disabled":true,"focusableWhenDisabled":false,"native":true,"composite":false,"reportInteractionType":false,"developmentChecks":true}""",
                """{"disabled":true,"focusableWhenDisabled":true,"native":true,"composite":false,"reportInteractionType":false,"developmentChecks":true}""",
                """{"disabled":true,"focusableWhenDisabled":true,"native":false,"composite":false,"reportInteractionType":false,"developmentChecks":true}""",
            ],
            handle.Invocations["update"].Select(invocation => (string?)invocation.Arguments[0]));
    }

    [Fact(DisplayName = "disposes the handle when the part is disposed")]
    [Trait("Verifies", "ADR06-3")]
    public async Task Disposes_the_handle_when_the_part_is_disposed()
    {
        context.Render<Button>();

        await context.DisposeComponentsAsync();

        Assert.Single(handle.Invocations["dispose"]);
    }

    [Fact(DisplayName = "under a substitute callback the twin attaches by the generated id, and a consumer id wins")]
    [Trait("Requirement", "BTN-API-04")]
    public void Under_a_substitute_callback_the_twin_attaches_by_the_generated_id_and_a_consumer_id_wins()
    {
        RenderFragment<RenderProps<ButtonState>> callback = props => builder =>
        {
            builder.OpenElement(0, "button");
            builder.AddMultipleAttributes(1, props.Attributes);
            builder.CloseElement();
        };

        IRenderedComponent<Button> generated = context.Render<Button>(p => p.Add(x => x.RenderFunction, callback));
        IRenderedComponent<Button> supplied = context.Render<Button>(p => p.Add(x => x.RenderFunction, callback).AddUnmatched("id", "mine"));

        string generatedId = generated.Find("button").GetAttribute("id")!;
        Assert.StartsWith(PartIdGenerator.Prefix, generatedId);
        Assert.Equal("mine", supplied.Find("button").GetAttribute("id"));
        Assert.Equal([generatedId, "mine"], module.Invocations["attach"].Select(invocation => invocation.Arguments[0] as string));
    }

    [Fact(DisplayName = "a substitute whose root is a component is attached by an id once it has rendered")]
    [Trait("Requirement", "BTN-API-04")]
    public void A_substitute_whose_root_is_a_component_is_attached_by_an_id_once_it_has_rendered()
    {
        IRenderedComponent<Button> cut = context.Render<Button>(p => p
            .Add(x => x.NativeButton, false)
            .Add(x => x.Render, (RenderFragment)(builder =>
            {
                builder.OpenComponent<SplattingSpan>(0);
                builder.CloseComponent();
            })));

        string id = cut.Find("span").GetAttribute("id")!;
        Assert.StartsWith(PartIdGenerator.Prefix, id);
        Assert.Equal(id, Assert.Single(module.Invocations["attach"]).Arguments[0]);
    }

    [Fact(DisplayName = "errors if nativeButton=true but ref is not a button")]
    [Trait("Requirement", "BTN-EDGE-01")]
    [Trait("Requirement", "UBT-EDGE-01")]
    [Trait("Requirement", "UBT-EDGE-03")]
    public void Errors_if_nativeButton_true_but_ref_is_not_a_button()
    {
        ButtonTwin first = TwinOf(context.Render<Button>());
        ButtonTwin second = TwinOf(context.Render<Button>());

        first.OnNativeButtonMismatch("""{"expectedNative":true}""");
        first.OnNativeButtonMismatch("""{"expectedNative":true}""");
        second.OnNativeButtonMismatch("""{"expectedNative":true}""");

        (LogLevel level, string message) = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, level);
        Assert.Equal(NativeMismatchMessage, message);
    }

    [Fact(DisplayName = "errors if nativeButton=false but ref is a button")]
    [Trait("Requirement", "BTN-EDGE-01")]
    [Trait("Requirement", "UBT-EDGE-02")]
    [Trait("Requirement", "UBT-EDGE-03")]
    public void Errors_if_nativeButton_false_but_ref_is_a_button()
    {
        ButtonTwin twin = TwinOf(context.Render<Button>(p => p.Add(x => x.NativeButton, false)));

        twin.OnNativeButtonMismatch("""{"expectedNative":false}""");
        twin.OnNativeButtonMismatch("""{"expectedNative":false}""");
        twin.OnNativeButtonMismatch("""{"expectedNative":true}""");

        Assert.Equal([NonNativeMismatchMessage, NativeMismatchMessage], logger.Entries.Select(entry => entry.Message));
    }

    [Fact(DisplayName = "each development message is logged once per session, not once per process")]
    [Trait("Requirement", "UBT-EDGE-03")]
    public void Each_development_message_is_logged_once_per_session_not_once_per_process()
    {
        CapturingLogger<Button> otherLogger = new();
        using BunitContext otherSession = new();
        otherSession.Services.AddBlazixPrimitives();
        otherSession.Services.AddSingleton<ILogger<Button>>(otherLogger);
        BunitJSModuleInterop otherModule = otherSession.JSInterop.SetupModule(ModulePath);
        otherModule.SetupModule("attach", _ => true);

        TwinOf(context.Render<Button>()).OnNativeButtonMismatch("""{"expectedNative":true}""");
        otherSession.Render<Button>();
        ButtonTwin otherTwin = ((DotNetObjectReference<ButtonTwin>)Assert.Single(otherModule.Invocations["attach"]).Arguments[2]!).Value;
        otherTwin.OnNativeButtonMismatch("""{"expectedNative":true}""");

        Assert.Single(logger.Entries);
        Assert.Single(otherLogger.Entries);
    }

    [Fact(DisplayName = "does not click non-composite buttons when keydown/keyup calls preventBaseUIHandler")]
    [Trait("Requirement", "BTN-KEY-01")]
    [Trait("Requirement", "UBT-KEY-06")]
    [Trait("Requirement", "UBT-EVT-01")]
    public void Does_not_click_non_composite_buttons_when_keydown_or_keyup_calls_preventBaseUIHandler()
    {
        // The decider runs the consumer's handler synchronously inside the dispatch (WebAssembly): a veto answers "stop".
        List<string> calls = [];
        IRenderedComponent<Button> cut = context.Render<Button>(p => p
            .Add(x => x.NativeButton, false)
            .Add(x => x.Render, (RenderFragment)(builder =>
            {
                builder.OpenElement(0, "span");
                builder.CloseElement();
            }))
            .AddUnmatched("onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, e =>
            {
                calls.Add($"keydown:{e.Key}:{e.ShiftKey}");
                e.PreventPartHandler();
            }))
            .AddUnmatched("onkeyup", EventCallback.Factory.Create<KeyboardEventArgs>(this, e => calls.Add($"keyup:{e.Key}"))));
        ButtonTwin twin = TwinOf(cut);

        Assert.Equal("stop", twin.Decide(ButtonTwin.ActivatePoint, KeyJson("keydown", "Enter", "Enter", shiftKey: true)));
        Assert.Equal("run", twin.Decide(ButtonTwin.ActivatePoint, KeyJson("keyup", " ", "Space", shiftKey: false)));

        Assert.Equal(["keydown:Enter:True", "keyup: "], calls);
    }

    [Fact(DisplayName = "a key handler the decider ran is not run again by the dispatch of the same event")]
    [Trait("Requirement", "UBT-EVT-01")]
    [Trait("Requirement", "BTN-PTR-01")]
    public void A_key_handler_the_decider_ran_is_not_run_again_by_the_dispatch_of_the_same_event()
    {
        List<string> calls = [];
        IRenderedComponent<Button> cut = context.Render<Button>(p => p
            .Add(x => x.NativeButton, false)
            .Add(x => x.Render, (RenderFragment)(builder =>
            {
                builder.OpenElement(0, "span");
                builder.CloseElement();
            }))
            .AddUnmatched("onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, e => calls.Add($"keydown:{e.Key}"))));
        ButtonTwin twin = TwinOf(cut);

        // On WebAssembly the module asks inside the dispatch, then Blazor dispatches the same keydown.
        Assert.Equal("run", twin.Decide(ButtonTwin.ActivatePoint, KeyJson("keydown", "Enter", "Enter", shiftKey: false)));
        cut.Find("span").KeyDown(new KeyboardEventArgs { Type = "keydown", Key = "Enter", Code = "Enter" });
        // A key the module does not ask about, and a later press of the same key, reach the handler through the dispatch.
        cut.Find("span").KeyDown(new KeyboardEventArgs { Type = "keydown", Key = "a", Code = "KeyA" });
        cut.Find("span").KeyDown(new KeyboardEventArgs { Type = "keydown", Key = "Enter", Code = "Enter" });

        Assert.Equal(["keydown:Enter", "keydown:a", "keydown:Enter"], calls);
    }

    [Fact(DisplayName = "the decider answers run when the consumer has no handler for the key event")]
    [Trait("Requirement", "UBT-KEY-06")]
    public void The_decider_answers_run_when_the_consumer_has_no_handler_for_the_key_event()
    {
        ButtonTwin twin = TwinOf(context.Render<Button>(p => p.Add(x => x.NativeButton, false)));

        Assert.Equal("run", twin.Decide(ButtonTwin.ActivatePoint, KeyJson("keydown", "Enter", "Enter", shiftKey: false)));
        Assert.Equal("run", twin.Decide(ButtonTwin.ActivatePoint, KeyJson("keyup", " ", "Space", shiftKey: false)));
    }

    private static string KeyJson(string type, string key, string code, bool shiftKey)
    {
        string shift = shiftKey ? "true" : "false";
        return $$"""
            {"kind":"KeyboardEvent","type":"{{type}}","key":"{{key}}","code":"{{code}}","altKey":false,"ctrlKey":false,
             "metaKey":false,"shiftKey":{{shift}},"pointerType":null,"detail":0,"buttons":0,
             "target":{"id":null,"tagName":"SPAN"},"defaultPrevented":false,"timeStamp":12.5}
            """;
    }

    private ButtonTwin TwinOf(IRenderedComponent<Button> cut)
    {
        // The attach of this part is the last one recorded; its third argument is the twin's object reference.
        _ = cut;
        JSRuntimeInvocation attach = module.Invocations["attach"][^1];
        return ((DotNetObjectReference<ButtonTwin>)attach.Arguments[2]!).Value;
    }

    /// <summary>A substitute component that splats what it receives onto a span, as a consumer's wrapper does.</summary>
    private sealed class SplattingSpan : ComponentBase
    {
        [Parameter(CaptureUnmatchedValues = true)]
        public IReadOnlyDictionary<string, object>? Attributes { get; set; }

        /// <inheritdoc />
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "span");
            builder.AddMultipleAttributes(1, Attributes);
            builder.CloseElement();
        }
    }
}
