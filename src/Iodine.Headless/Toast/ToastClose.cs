using Iodine.Headless.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Iodine.Headless;

// TST §3.9 state object (toast/close/ToastClose.tsx:38-40); TST-STA-26.
/// <summary>Represents the state of a toast's close button, which the part exposes as data attributes.</summary>
/// <param name="Type">The toast's type, or <see langword="null"/>.</param>
[PartState]
public sealed partial record ToastCloseState(string? Type);

// toast/close/ToastCloseDataAttributes.ts:5 (TST-DOM-18).
/// <summary>Provides the data attributes a toast's close button renders.</summary>
public static class ToastCloseDataAttributes
{
    /// <summary>The toast's type, when it has one.</summary>
    public const string Type = "data-type";
}

// Base UI's Toast.Close (toast/close/ToastClose.tsx); TST-STR-08, TST-API-16, TST-DOM-18, TST-ARIA-07, TST-PTR-26. Over
// ButtonBehavior with Native from NativeButton and no FocusableWhenDisabled (UBT §6.2's Toast.Close row), and the part's
// own C# onclick, the DialogClose composition: the consumer's onclick runs first through the pipeline's chain, and a
// disabled close is blocked by the button mechanism (UBT-PTR-01). `aria-hidden` is C#'s (the M6 plan's decision 19 (a)):
// "true" while the stack is not expanded and the button does not have focus, "false" otherwise, from the stack's expanded
// flag and the button's own C# focus and blur (a relaxation candidate on the server; ADR 01). A consumer `aria-hidden`
// wins, as every consumer attribute does.
/// <summary>
/// Represents a button that closes its toast. Base UI's <c>Toast.Close</c>; it renders a <c>button</c>.
/// </summary>
public sealed class ToastClose : PartBase<ToastCloseState>
{
    private readonly EventCallback onClick;
    private readonly EventCallback onKeyDown;
    private readonly EventCallback onKeyUp;
    private readonly EventCallback onFocus;
    private readonly EventCallback onBlur;
    private ToastRootContext? root;
    private ButtonBehavior? button;
    private ToastRootSubscription? subscription;
    private bool focused;
    private ToastCloseState state = new(null);

    /// <summary>Initializes a new instance of the <see cref="ToastClose"/> class.</summary>
    public ToastClose()
    {
        onClick = CreateHandler<MouseEventArgs>("onclick", HandleClick);
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => button!.HandleKeyDownAsync(e));
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", e => button!.HandleKeyUpAsync(e));
        onFocus = CreateHandler<FocusEventArgs>("onfocus", _ => SetFocused(true));
        onBlur = CreateHandler<FocusEventArgs>("onblur", _ => SetFocused(false));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<ToastClose> Logger { get; set; } = null!;

    [Inject]
    private ButtonDevelopmentLog DevelopmentLog { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private ToastRootContext? RootContext { get; set; }

    /// <summary>Gets or sets a value indicating whether the rendered element is a native button.</summary>
    /// <value>
    /// <see langword="true"/> (the default) for a native <c>button</c>; <see langword="false"/> when a substitute renders
    /// another element, which then gets the button role and keyboard activation.
    /// </value>
    [Parameter]
    public bool NativeButton { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the button ignores user interaction.</summary>
    /// <value><see langword="true"/> to disable the button; the default is <see langword="false"/>.</value>
    [Parameter]
    public bool Disabled { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "button";

    /// <inheritdoc />
    protected override ToastCloseState State
    {
        get
        {
            if (!string.Equals(state.Type, root!.Type, StringComparison.Ordinal))
            {
                state = new ToastCloseState(root.Type);
            }

            return state;
        }
    }

    // ADR 02 decision 1: under a substitute callback the button's twin finds the element by id.
    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    // For the fixtures: completes once the button's handle is attached.
    internal Task TwinAttached => button?.TwinAttached ?? Task.CompletedTask;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        root = ToastRootContext.Require(RootContext);
        button = new ButtonBehavior(JSRuntime, Logger, DevelopmentLog, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown, onKeyUp);
        subscription = root.Subscribe(ToastRootAspects.Type | ToastRootAspects.Expanded, StateHasChanged);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        button!.Native = NativeButton;
        button.FocusableWhenDisabled = null;
        button.Disabled = Disabled;
        button.SetConsumerAttributes(AdditionalAttributes);
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        button!.Disabled = Disabled;
        return button.AfterRenderAsync(Enabled, Element, Id, composite: false);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        subscription!.Rendered();
        base.BuildRenderTree(builder);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            subscription?.Dispose();
            subscription = null;
            button?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        // TST-ARIA-07: always present; the consumer's own aria-hidden overrides it.
        attributes.Set("aria-hidden", !root!.Expanded && !focused ? "true" : "false");
        attributes.Set("onfocus", onFocus);
        attributes.Set("onblur", onBlur);
        button!.Disabled = Disabled;
        attributes.Set("onclick", onClick);
        button.AddAttributes(attributes, Id, composite: false);
    }

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => button!.ForwardsConsumerHandler(eventName);

    // TST-PTR-26: a click on an enabled close closes its toast, by id.
    private void HandleClick(MouseEventArgs e)
    {
        if (!Disabled)
        {
            root!.Close();
        }
    }

    private void SetFocused(bool value)
    {
        if (focused != value)
        {
            focused = value;
            StateHasChanged();
        }
    }
}
