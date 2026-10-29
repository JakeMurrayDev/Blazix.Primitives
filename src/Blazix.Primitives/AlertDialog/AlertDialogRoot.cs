using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// ADL §3.1 (alert-dialog/root/AlertDialogRoot.tsx): the Dialog root routine in alert mode (useRenderDialogRoot('alert-dialog')).
// ADR 08 Amendments 2026-10-27 (chosen by the user 2026-10-27): a sealed part over DialogRootBehavior with its own parameter
// list, Dialog's minus Modal and DisablePointerDismissal (ADL-API-01; ADL §14, a strict parameter list does not accept
// them). The store forces modal, pointer dismissal disabled and the alertdialog role at every parameter set (ADL-STA-01,
// API-02/03; DialogStore.ApplyRootSettings in alert mode). It renders no element (DLG-STR-01); DLG-API-07's actions are
// @ref methods (ADR 08 Amendments 2026-10-22).
/// <summary>
/// Groups all parts of an alert dialog, owns its open state and connects its triggers. It renders no element of its own.
/// </summary>
/// <typeparam name="TPayload">
/// The type of the payload the alert dialog's triggers carry; write <c>TPayload="NoPayload"</c> for an alert dialog without
/// payloads.
/// </typeparam>
/// <remarks>
/// <para>
/// An alert dialog is always modal: focus stays inside its popup, the rest of the page is hidden from assistive technology
/// and covered, and page scroll is locked. A press outside it and focus leaving it never close it; Escape, a close button,
/// its active trigger and <see cref="CloseAsync"/> do.
/// </para>
/// <para>
/// <see cref="ChildContent"/> renders the alert dialog's parts. <see cref="PayloadContent"/> renders with the active
/// trigger's payload, or the payload given to <see cref="DialogHandle{TPayload}.OpenWithPayloadAsync"/>. Two nested roots
/// that both use <see cref="PayloadContent"/> without naming its parameter fail to compile with <c>RZ9999</c>, because both
/// name it <c>context</c>; name it with <c>Context="payload"</c> on one of them.
/// </para>
/// <para>
/// <see cref="OnOpenChange"/> receives an <see cref="OpenChangeEventDetails"/> as its arguments' <c>Details</c>; cast it to
/// call <see cref="OpenChangeEventDetails.PreventUnmountOnClose"/>:
/// <c>((OpenChangeEventDetails)args.Details).PreventUnmountOnClose()</c>.
/// </para>
/// </remarks>
[CascadingTypeParameter(nameof(TPayload))]
public sealed class AlertDialogRoot<TPayload> : ComponentBase, IAsyncDisposable
{
    private DialogRootBehavior<TPayload>? behavior;

    // Created at the first parameter set, once the services are injected.
    private DialogRootBehavior<TPayload> Behavior => behavior ?? throw new InvalidOperationException("Blazix: `AlertDialogRoot` is used before its first parameter set.");

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [Inject]
    private ILogger<AlertDialogRoot<TPayload>> Logger { get; set; } = null!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private IDialogRootContext? ParentContext { get; set; }

    // DLG-API-01
    /// <summary>Gets or sets a value indicating whether the alert dialog is open, for <c>@bind-Open</c>.</summary>
    /// <value>Leave it unset for an uncontrolled alert dialog; once set at the first render, the alert dialog follows it.</value>
    [Parameter]
    public bool Open { get; set; }

    /// <summary>Gets or sets the callback <c>@bind-Open</c> uses.</summary>
    [Parameter]
    public EventCallback<bool> OpenChanged { get; set; }

    // DLG-API-02
    /// <summary>Gets or sets a value indicating whether an uncontrolled alert dialog is open initially.</summary>
    /// <value>The default is <see langword="false"/>.</value>
    [Parameter]
    public bool DefaultOpen { get; set; }

    // ADL-EVT-01 (DLG-API-04)
    /// <summary>Gets or sets the callback called when the alert dialog is about to open or close, before the change is applied.</summary>
    /// <value>
    /// The arguments' <c>Value</c> is the requested open state; their <c>Details</c>, an <see cref="OpenChangeEventDetails"/>,
    /// carry the reason, the event and the trigger, and can cancel the change.
    /// </value>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<bool>> OnOpenChange { get; set; }

    // DLG-API-05
    /// <summary>Gets or sets the callback called after the alert dialog's open or close animations finish.</summary>
    [Parameter]
    public EventCallback<bool> OnOpenChangeComplete { get; set; }

    // ADL-API-04
    /// <summary>Gets or sets the handle that connects this root with detached triggers and opens it from code.</summary>
    [Parameter]
    public AlertDialogHandle<TPayload>? Handle { get; set; }

    // DLG-API-10
    /// <summary>Gets or sets the id of the active trigger, to control it together with <see cref="Open"/>.</summary>
    /// <value>Leave it unset for an uncontrolled active trigger; once set at the first render, <see langword="null"/> means no active trigger.</value>
    [Parameter]
    public string? TriggerId { get; set; }

    // DLG-API-11
    /// <summary>Gets or sets the id of the trigger that is active initially, together with <see cref="DefaultOpen"/>.</summary>
    [Parameter]
    public string? DefaultTriggerId { get; set; }

    /// <summary>Gets or sets the alert dialog's parts.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    // DLG-API-09
    /// <summary>Gets or sets the content rendered with the alert dialog's payload.</summary>
    /// <value>The parameter is the payload, or <see langword="default"/> when none was given.</value>
    [Parameter]
    public RenderFragment<TPayload?>? PayloadContent { get; set; }

    // For the fixtures: completes once the session handle is attached.
    internal Task SessionAttached => behavior?.SessionAttached ?? Task.CompletedTask;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        behavior ??= new DialogRootBehavior<TPayload>(DialogMode.AlertDialog, JSRuntime, StateHasChanged, DispatchExceptionAsync);
        behavior.SetParameters(parameters, IdGenerator, Logger);
        return base.SetParametersAsync(parameters);
    }

    // The regime passed here is the one the store forces in alert mode (ADL-API-02/03); the store re-applies it whatever it is.
    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        Behavior.ApplyParameters(
            new PopupRootParameters<TPayload>(DefaultOpen, DefaultTriggerId, Handle, OnOpenChange, OpenChanged, OnOpenChangeComplete, CloseOnActiveTriggerUnmount: false, ChildContent, PayloadContent),
            DialogModality.Modal,
            disablePointerDismissal: true,
            ParentContext);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder) => Behavior.BuildRenderTree(builder);

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => Behavior.OnAfterRenderAsync();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => behavior?.DisposeAsync() ?? ValueTask.CompletedTask;

    /// <summary>Closes the alert dialog, as a close button does, with the reason <see cref="EventReason.ImperativeAction"/>.</summary>
    /// <returns>A task that completes when the close request has run.</returns>
    public Task CloseAsync() => Behavior.CloseAsync();

    /// <summary>
    /// Removes the alert dialog's popup at once, ending its close animation or a close kept mounted by
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
