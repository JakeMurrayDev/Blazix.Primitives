using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// DLG §3.1 (dialog/root/DialogRoot.tsx), ADR 08 Amendments 2026-10-27 (the shared Root parameters; the payload type
// parameter and issue 116's finding: generic only, ChildContent beside PayloadContent; chosen by the user 2026-10-27). It
// renders no element (DLG-STR-01, the DirectionProvider precedent): only its store's fixed cascade. DLG-API-07's actions
// are @ref methods (ADR 08 Amendments 2026-10-22).
/// <summary>
/// Groups all parts of a dialog, owns its open state and connects its triggers. It renders no element of its own.
/// </summary>
/// <typeparam name="TPayload">
/// The type of the payload the dialog's triggers carry; write <c>TPayload="NoPayload"</c> for a dialog without payloads.
/// </typeparam>
/// <remarks>
/// <para>
/// <see cref="ChildContent"/> renders the dialog's parts. <see cref="PayloadContent"/> renders with the active trigger's
/// payload, or the payload given to <see cref="DialogHandle{TPayload}.OpenWithPayloadAsync"/>. Two nested roots that both use
/// <see cref="PayloadContent"/> without naming its parameter fail to compile with <c>RZ9999</c>, because both name it
/// <c>context</c>; name it with <c>Context="payload"</c> on one of them.
/// </para>
/// <para>
/// <see cref="OnOpenChange"/> receives an <see cref="OpenChangeEventDetails"/> as its arguments' <c>Details</c>; cast it to
/// call <see cref="OpenChangeEventDetails.PreventUnmountOnClose"/>:
/// <c>((OpenChangeEventDetails)args.Details).PreventUnmountOnClose()</c>.
/// </para>
/// </remarks>
[CascadingTypeParameter(nameof(TPayload))]
public sealed class DialogRoot<TPayload> : ComponentBase, IAsyncDisposable
{
    private DialogRootBehavior<TPayload>? behavior;

    // Created at the first parameter set, once the services are injected.
    private DialogRootBehavior<TPayload> Behavior => behavior ?? throw new InvalidOperationException("Blazix: `DialogRoot` is used before its first parameter set.");

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [Inject]
    private ILogger<DialogRoot<TPayload>> Logger { get; set; } = null!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private IDialogRootContext? ParentContext { get; set; }

    // DLG-API-01
    /// <summary>Gets or sets a value indicating whether the dialog is open, for <c>@bind-Open</c>.</summary>
    /// <value>Leave it unset for an uncontrolled dialog; once set at the first render, the dialog follows it.</value>
    [Parameter]
    public bool Open { get; set; }

    /// <summary>Gets or sets the callback <c>@bind-Open</c> uses.</summary>
    [Parameter]
    public EventCallback<bool> OpenChanged { get; set; }

    // DLG-API-02
    /// <summary>Gets or sets a value indicating whether an uncontrolled dialog is open initially.</summary>
    /// <value>The default is <see langword="false"/>.</value>
    [Parameter]
    public bool DefaultOpen { get; set; }

    // DLG-API-03
    /// <summary>Gets or sets the modality of the dialog.</summary>
    /// <value>
    /// <see langword="true"/>, the default when <see langword="null"/>: focus is trapped, the rest of the page is hidden from
    /// assistive technology and covered, and page scroll is locked. <see cref="ModalValue.TrapFocus"/>: focus is trapped and
    /// the page hidden, but it stays scrollable and clickable. <see langword="false"/>: nothing is trapped or hidden, and
    /// focus leaving the dialog closes it.
    /// </value>
    [Parameter]
    public ModalValue? Modal { get; set; }

    // DLG-API-04
    /// <summary>Gets or sets the callback called when the dialog is about to open or close, before the change is applied.</summary>
    /// <value>
    /// The arguments' <c>Value</c> is the requested open state; their <c>Details</c>, an <see cref="OpenChangeEventDetails"/>,
    /// carry the reason, the event and the trigger, and can cancel the change.
    /// </value>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<bool>> OnOpenChange { get; set; }

    // DLG-API-05
    /// <summary>Gets or sets the callback called after the dialog's open or close animations finish.</summary>
    [Parameter]
    public EventCallback<bool> OnOpenChangeComplete { get; set; }

    // DLG-API-06
    /// <summary>Gets or sets a value indicating whether pointer presses outside the dialog, and focus leaving it, do not close it.</summary>
    /// <value>The default is <see langword="false"/>. Escape, close buttons and triggers still close the dialog.</value>
    [Parameter]
    public bool DisablePointerDismissal { get; set; }

    // DLG-API-08
    /// <summary>Gets or sets the handle that connects this root with detached triggers and opens it from code.</summary>
    [Parameter]
    public DialogHandle<TPayload>? Handle { get; set; }

    // DLG-API-10
    /// <summary>Gets or sets the id of the active trigger, to control it together with <see cref="Open"/>.</summary>
    /// <value>Leave it unset for an uncontrolled active trigger; once set at the first render, <see langword="null"/> means no active trigger.</value>
    [Parameter]
    public string? TriggerId { get; set; }

    // DLG-API-11
    /// <summary>Gets or sets the id of the trigger that is active initially, together with <see cref="DefaultOpen"/>.</summary>
    [Parameter]
    public string? DefaultTriggerId { get; set; }

    /// <summary>Gets or sets the dialog's parts.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    // DLG-API-09
    /// <summary>Gets or sets the content rendered with the dialog's payload.</summary>
    /// <value>The parameter is the payload, or <see langword="default"/> when none was given.</value>
    [Parameter]
    public RenderFragment<TPayload?>? PayloadContent { get; set; }

    // For the fixtures: completes once the session handle is attached.
    internal Task SessionAttached => behavior?.SessionAttached ?? Task.CompletedTask;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        behavior ??= new DialogRootBehavior<TPayload>(DialogMode.Dialog, JSRuntime, StateHasChanged, DispatchExceptionAsync);
        behavior.SetParameters(parameters, IdGenerator, Logger);
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        Behavior.ApplyParameters(
            new PopupRootParameters<TPayload>(DefaultOpen, DefaultTriggerId, Handle, OnOpenChange, OpenChanged, OnOpenChangeComplete, CloseOnActiveTriggerUnmount: false, ChildContent, PayloadContent),
            ToModality(Modal),
            DisablePointerDismissal,
            ParentContext);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder) => Behavior.BuildRenderTree(builder);

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => Behavior.OnAfterRenderAsync();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => behavior?.DisposeAsync() ?? ValueTask.CompletedTask;

    /// <summary>Closes the dialog, as a close button does, with the reason <see cref="EventReason.ImperativeAction"/>.</summary>
    /// <returns>A task that completes when the close request has run.</returns>
    public Task CloseAsync() => Behavior.CloseAsync();

    /// <summary>
    /// Removes the dialog's popup at once, ending its close animation or a close kept mounted by
    /// <see cref="OpenChangeEventDetails.PreventUnmountOnClose"/>.
    /// </summary>
    public void Unmount()
    {
        Task unmounting = Behavior.UnmountAsync();
        if (!unmounting.IsCompleted)
        {
            _ = ObserveAsync(unmounting);
        }
    }

    /// <summary>Maps a root's <c>Modal</c> parameter onto the store's regime.</summary>
    /// <param name="modal">The parameter; <see langword="null"/> is <see langword="true"/>.</param>
    /// <returns>The regime.</returns>
    internal static DialogModality ToModality(ModalValue? modal)
    {
        if (modal is not { } value || !value.HasValue)
        {
            return DialogModality.Modal;
        }

        if (value.IsTrapFocus)
        {
            return DialogModality.TrapFocus;
        }

        return value.TryGetValue(out bool modalFlag) && !modalFlag ? DialogModality.NonModal : DialogModality.Modal;
    }

    private async Task ObserveAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception exception) when (!task.IsCanceled)
        {
            await DispatchExceptionAsync(exception);
        }
    }
}
