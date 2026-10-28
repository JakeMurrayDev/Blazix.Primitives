using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// Base UI's Popover.Close (popover/close/PopoverClose.tsx); the contract is docs/components/popover.md §3.11, §5.8.
// - ButtonBehavior { Native = NativeButton, Disabled, FocusableWhenDisabled = false } (POP-API-24/25; §16 Q5 mirrored:
//   Disabled is Base UI's native attribute reused as a parameter).
// - A C# onclick requests a close with the reason close-press and the click's snapshot after the consumer's handler (ADR 04
//   Amendments 2026-10-21, the CollapsibleTrigger shape); the store back-fills the active trigger (POP-EVT-06).
// - Inside the Popup it registers with the Popup's close-part count, which makes a modal popover trap focus (POP-STA-10).
/// <summary>
/// Represents a button that closes a popover, Base UI's <c>Popover.Close</c>, rendered as a <c>button</c>.
/// </summary>
/// <remarks>
/// A close button inside the popup also lets a modal popover trap focus; one outside the popup still closes it.
/// </remarks>
public sealed class PopoverClose : PartBase<EmptyState>
{
    private readonly EventCallback onClick;
    private readonly EventCallback onKeyDown;
    private readonly EventCallback onKeyUp;
    private ButtonBehavior? button;
    private IDisposable? closePart;

    /// <summary>Initializes a new instance of the <see cref="PopoverClose"/> class.</summary>
    public PopoverClose()
    {
        onClick = CreateHandler<MouseEventArgs>("onclick", HandleClickAsync);
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => button!.HandleKeyDownAsync(e));
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", e => button!.HandleKeyUpAsync(e));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<PopoverClose> Logger { get; set; } = null!;

    [Inject]
    private ButtonDevelopmentLog DevelopmentLog { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private PopoverRootContext? RootContext { get; set; }

    [CascadingParameter]
    private PopoverClosePartCount? ClosePartCount { get; set; }

    // POP-API-24
    /// <summary>Gets or sets a value indicating whether the rendered element is a native <c>button</c>.</summary>
    /// <value>
    /// <see langword="true"/>, the default, for a <c>button</c> element; <see langword="false"/> when
    /// <see cref="PartBase{TState}.Render"/> supplies another element, which then gets <c>role="button"</c>, a
    /// <c>tabindex</c>, and clicks from Enter and Space.
    /// </value>
    [Parameter]
    public bool NativeButton { get; set; } = true;

    // POP-API-25
    /// <summary>Gets or sets a value indicating whether the button ignores user interaction.</summary>
    /// <value><see langword="false"/> by default; a disabled close button is removed from the tab order.</value>
    [Parameter]
    public bool Disabled { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "button";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    /// <summary>Gets a task that completes when the button's behavior is attached.</summary>
    internal Task TwinAttached => button!.TwinAttached;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The close button is not inside a <see cref="PopoverRoot{TPayload}"/>.</exception>
    protected override void OnInitialized()
    {
        PopoverRootContext.Require(RootContext);
        button = new ButtonBehavior(JSRuntime, Logger, DevelopmentLog, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown, onKeyUp)
        {
            FocusableWhenDisabled = false,
        };
        closePart = ClosePartCount?.Register();
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        button!.Native = NativeButton;
        button.Disabled = Disabled;
        button.SetConsumerAttributes(AdditionalAttributes);
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => button!.AfterRenderAsync(Enabled, Element, Id, composite: false);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            closePart?.Dispose();
            closePart = null;
            button?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        button!.Disabled = Disabled;
        attributes.Set("onclick", onClick);
        button.AddAttributes(attributes, Id, composite: false);
    }

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => button!.ForwardsConsumerHandler(eventName);

    // POP-PTR-22, POP-EVT-06: a disabled close button requests nothing.
    private Task HandleClickAsync(MouseEventArgs e)
    {
        if (Disabled)
        {
            return Task.CompletedTask;
        }

        return RootContext!.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.ClosePress, EventSnapshot.FromEventArgs(e)));
    }
}
