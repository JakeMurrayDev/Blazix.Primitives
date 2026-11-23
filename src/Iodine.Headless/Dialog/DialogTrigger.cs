using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// DLG §3.2 state object (dialog/trigger/DialogTrigger.tsx:76-79), DLG-STA-12: Open is isOpenedByTrigger, not the dialog's
// open state.
/// <summary>Represents the state of a dialog trigger, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the trigger is disabled; projected as <c>data-disabled</c>.</param>
/// <param name="Open">
/// A value indicating whether the dialog is open and this trigger opened it; projected as <c>data-popup-open</c>. It is
/// <see langword="false"/> while the dialog is open through another trigger or without one.
/// </param>
[PartState]
public sealed partial record DialogTriggerState(bool Disabled, bool Open);

// dialog/trigger/DialogTriggerDataAttributes.ts (DLG-DOM-03/04).
/// <summary>Provides the names of the data attributes a dialog trigger renders.</summary>
public static class DialogTriggerDataAttributes
{
    /// <summary>Represents the attribute present while the trigger is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";

    /// <summary>Represents the attribute present while the dialog is open and this trigger opened it: <c>data-popup-open</c>.</summary>
    public const string PopupOpen = "data-popup-open";
}

// Base UI's Dialog.Trigger (dialog/trigger/DialogTrigger.tsx); the contract is docs/components/dialog.md (§3.2, §5.1, §5.8,
// §6.2). A thin part over DialogTriggerBehavior, which AlertDialogTrigger shares (ADR 08 Amendments 2026-10-27). The bag's
// id is the trigger id, kept on the trigger's own element (issue 115); it registers at parameter set (PSH-ALG-04).
/// <summary>
/// Represents the button that opens a dialog, Base UI's <c>Dialog.Trigger</c>, rendered as a <c>button</c>.
/// </summary>
/// <typeparam name="TPayload">The type of the payload the trigger carries; it is inferred from the enclosing root.</typeparam>
/// <remarks>
/// Place it inside a <see cref="DialogRoot{TPayload}"/>, or anywhere on the page with a <see cref="Handle"/> the root also
/// uses. Its <c>id</c>, given or generated, identifies it to the root's <c>TriggerId</c> and to
/// <see cref="PopupHandle{TPayload}.OpenAsync"/>.
/// </remarks>
[CascadingTypeParameter(nameof(TPayload))]
public sealed class DialogTrigger<TPayload> : PartBase<DialogTriggerState>
{
    private static readonly StateAttributesMapping<DialogTriggerState> s_mapping = PopupStateMapping.CreateTrigger<DialogTriggerState>();

    private readonly EventCallback onClick;
    private readonly EventCallback onKeyDown;
    private readonly EventCallback onKeyUp;
    private DialogTriggerBehavior<TPayload>? behavior;
    private DialogTriggerState state = new(false, false);
    private bool payloadSupplied;

    /// <summary>Initializes a new instance of the <see cref="DialogTrigger{TPayload}"/> class.</summary>
    public DialogTrigger()
    {
        onClick = CreateHandler<MouseEventArgs>("onclick", e => behavior!.HandleClickAsync(e));
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => behavior!.HandleKeyDownAsync(e));
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", e => behavior!.HandleKeyUpAsync(e));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<DialogTrigger<TPayload>> Logger { get; set; } = null!;

    [Inject]
    private ButtonDevelopmentLog DevelopmentLog { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    private string? ConsumerId => AdditionalAttributes is not null && AdditionalAttributes.TryGetValue("id", out object? value) && value is string text ? text : null;

    [CascadingParameter]
    private DialogStore<TPayload>? RootStore { get; set; }

    // DLG-API-12
    /// <summary>Gets or sets the handle of the dialog this trigger opens, when the trigger is outside its root.</summary>
    [Parameter]
    public DialogHandle<TPayload>? Handle { get; set; }

    // DLG-API-13
    /// <summary>Gets or sets the payload the dialog receives while this trigger is the one that opened it.</summary>
    [Parameter]
    public TPayload? Payload { get; set; }

    // DLG-API-15
    /// <summary>Gets or sets a value indicating whether the rendered element is a native <c>button</c>.</summary>
    /// <value>The default is <see langword="true"/>; set it to <see langword="false"/> when <c>Render</c> substitutes another element.</value>
    [Parameter]
    public bool NativeButton { get; set; } = true;

    // DLG-API-16
    /// <summary>Gets or sets a value indicating whether the trigger is disabled.</summary>
    /// <value>The default is <see langword="false"/>.</value>
    [Parameter]
    public bool Disabled { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "button";

    // The pipeline reads the state before the part's attributes, so the record follows the store here.
    /// <inheritdoc />
    protected override DialogTriggerState State
    {
        get
        {
            bool open = behavior!.IsOpenedByTrigger;
            if (state.Open != open || state.Disabled != Disabled)
            {
                state = new DialogTriggerState(Disabled, open);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<DialogTriggerState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    // For the fixtures: completes once the button and press handles are attached to the trigger's element.
    internal Task TwinAttached => behavior?.TwinAttached ?? Task.CompletedTask;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        payloadSupplied = parameters.TryGetValue<TPayload>(nameof(Payload), out _);
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new DialogTriggerBehavior<TPayload>(this, "DialogTrigger", "DialogRoot", JSRuntime, Logger, DevelopmentLog, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown, onKeyUp, onClick);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        behavior!.Apply(RootStore, Handle, Id, Element, payloadSupplied, Payload, NativeButton, Disabled, AdditionalAttributes);
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.OnAfterRenderAsync(Enabled, Element, Id, Disabled);

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
    protected override void AddPartAttributes(AttributeList attributes)
    {
        behavior!.AddAttributes(attributes, ConsumerId, Disabled);
    }

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => behavior!.ForwardsConsumerHandler(eventName);

}
