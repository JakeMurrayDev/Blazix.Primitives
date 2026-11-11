using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// CMB §3.8, STA-06 (combobox/clear/ComboboxClear.tsx): the plain trigger mapping (§16 Q8 replicated), the visibility and its transition (mirrored, ANI-04).
/// <summary>Represents the state of a combobox clear button, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Open">A value indicating whether the popup is open; projected as <c>data-popup-open</c>.</param>
/// <param name="Disabled">A value indicating whether the button is disabled; projected as <c>data-disabled</c>.</param>
/// <param name="Visible">A value indicating whether there is something to clear; projected as <c>data-visible</c>.</param>
/// <param name="TransitionStatus">The enter or exit status; its <c>data-starting-style</c> and <c>data-ending-style</c> are written by the browser-side behavior.</param>
[PartState]
public sealed partial record ComboboxClearState(
    bool Open,
    bool Disabled,
    bool Visible,
    [property: MirroredField] TransitionStatus? TransitionStatus);

// combobox/clear/ComboboxClearDataAttributes.ts (CMB-DOM-06).
/// <summary>Provides the names of the data attributes a combobox clear button renders.</summary>
public static class ComboboxClearDataAttributes
{
    /// <summary>Represents the attribute present while the popup is open: <c>data-popup-open</c>.</summary>
    public const string PopupOpen = PopupTriggerDataAttributes.PopupOpen;

    /// <summary>Represents the attribute present while disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";

    /// <summary>Represents the attribute present while there is something to clear: <c>data-visible</c>.</summary>
    public const string Visible = "data-visible";

    /// <summary>Represents the attribute present while the button animates in: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = PopupDataAttributes.StartingStyle;

    /// <summary>Represents the attribute present while the button animates out: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = PopupDataAttributes.EndingStyle;
}

// Base UI's Combobox.Clear (combobox/clear/ComboboxClear.tsx; CMB §3.8). Over ComboboxClearBehavior; rendered while there is
// something to clear, through its exit transition, or always with KeepMounted (CMB-ANI-04).
/// <summary>Represents the button that clears a combobox's value and input text, Base UI's <c>Combobox.Clear</c>, rendered as a <c>button</c>.</summary>
/// <remarks>It renders while a value is selected (or, without a selection, while the input has text) and is not a tab stop; its default content is <c>x</c>.</remarks>
public sealed class ComboboxClear : PartBase<ComboboxClearState>
{
    private static readonly StateAttributesMapping<ComboboxClearState> s_mapping = PopupStateMapping.CreateTrigger<ComboboxClearState>();

    private static readonly RenderFragment s_defaultContent = static builder => builder.AddContent(0, "x");

    private readonly EventCallback onKeyDown;

    private readonly EventCallback onKeyUp;

    private readonly EventCallback onClick;

    private ComboboxClearBehavior? behavior;

    private ComboboxClearState state = new(false, false, false, null);

    /// <summary>Initializes a new instance of the <see cref="ComboboxClear"/> class.</summary>
    public ComboboxClear()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => behavior!.HandleKeyDownAsync(e));
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", e => behavior!.HandleKeyUpAsync(e));
        onClick = CreateHandler<MouseEventArgs>("onclick", e => behavior!.HandleClickAsync(e));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<ComboboxClear> Logger { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [Inject]
    private ButtonDevelopmentLog DevelopmentLog { get; set; } = null!;

    [CascadingParameter]
    private ComboboxStore? Root { get; set; }

    [CascadingParameter]
    private FieldRootContext? Field { get; set; }

    /// <summary>Gets or sets a value indicating whether the rendered element is a native <c>button</c>; the default is <see langword="true"/>.</summary>
    [Parameter]
    public bool NativeButton { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the button is disabled; the combobox's and the field's disabled state also apply.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Gets or sets a value indicating whether the button stays in the DOM while there is nothing to clear.</summary>
    [Parameter]
    public bool KeepMounted { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "button";

    /// <inheritdoc />
    protected override ComboboxClearState State
    {
        get
        {
            ComboboxClearBehavior clear = behavior!;
            bool open = clear.Store.State.Lifecycle.EffectiveOpen;
            bool disabled = clear.Disabled;
            if (state.Open != open || state.Disabled != disabled || state.Visible != clear.Visible || state.TransitionStatus != clear.TransitionStatus)
            {
                state = new ComboboxClearState(open, disabled, clear.Visible, clear.TransitionStatus);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<ComboboxClearState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    // For the fixtures: completes once the transitions handle is attached.
    internal Task TwinAttached => behavior?.TwinAttached ?? Task.CompletedTask;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new ComboboxClearBehavior(new ComboboxPartServices("Combobox", JSRuntime, Logger, IdGenerator, StateHasChanged, InvokeAsync, DispatchExceptionAsync), DevelopmentLog, onKeyDown, onKeyUp);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ChildContent ??= s_defaultContent;
        behavior!.Apply(NativeButton, Disabled, KeepMounted, Id, AdditionalAttributes);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        ComboboxClearBehavior clear = behavior!;
        clear.BeforeRender();
        if (!clear.Shown)
        {
            ((IPartHost)this).SetElement(null);
            return;
        }

        base.BuildRenderTree(builder);
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.AfterRenderAsync(behavior.Shown ? Element : null);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && behavior is not null)
        {
            _ = behavior.DisposeAsync().AsTask();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors() => behavior!.Subscribe(Root, Field, SubscribeTo);

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => behavior!.ForwardsConsumerHandler(eventName);

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddPartAttributes(attributes, onClick);
}
