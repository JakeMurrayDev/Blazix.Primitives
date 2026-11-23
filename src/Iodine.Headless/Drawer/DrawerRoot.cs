using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// DRW §3.4 (drawer/root/DrawerRoot.tsx), decision 20 (a) of the M6 plan (chosen by the user 2026-11-18; ADR 08 Amendments
// 2026-11-18): the Dialog root routine in drawer mode (useRenderDialogRoot('drawer')), a sealed part over
// DialogRootBehavior on the AlertDialog template, with Dialog's whole parameter list; drawer mode forces nothing
// (DRW-STR-01, DLG-DIS-10). It renders no element: the Dialog store's fixed cascade inside the Drawer root's own fixed
// cascade (DrawerRootContext), whose parent is the nearest enclosing DrawerRoot (DRW-STR-10). DLG-API-07's actions are @ref
// methods (ADR 08 Amendments 2026-10-22).
// DRW-STA-14 (DrawerRoot.tsx:218-231, 419-443): the root reports its effective open state to the nearest provider from the
// store's commit path, and removes itself when it leaves.
// DRW-STA-02..04, API-02..06 (DrawerRoot.tsx:65-113, 150-169; decision 7 (a) of the M6 plan, chosen by the user 2026-11-18):
// C# holds only the active snap point and its request sequence; the list's geometry is the `drawer` module's. The open
// change runs through a wrapper that, after a close that was not canceled, requests the default snap point with the close's
// reason, event and trigger before the store commits.
/// <summary>
/// Groups all parts of a drawer, owns its open state and connects its triggers. It renders no element of its own.
/// </summary>
/// <typeparam name="TPayload">
/// The type of the payload the drawer's triggers carry; write <c>TPayload="NoPayload"</c> for a drawer without payloads.
/// </typeparam>
/// <remarks>
/// <para>
/// A drawer is a dialog that slides in from an edge of the screen. <see cref="ChildContent"/> renders the drawer's parts.
/// <see cref="PayloadContent"/> renders with the active trigger's payload, or the payload given to
/// <see cref="DialogHandle{TPayload}.OpenWithPayloadAsync"/>. Two nested roots that both use <see cref="PayloadContent"/>
/// without naming its parameter fail to compile with <c>RZ9999</c>, because both name it <c>context</c>; name it with
/// <c>Context="payload"</c> on one of them.
/// </para>
/// <para>
/// <see cref="OnOpenChange"/> receives an <see cref="OpenChangeEventDetails"/> as its arguments' <c>Details</c>; cast it to
/// call <see cref="OpenChangeEventDetails.PreventUnmountOnClose"/>:
/// <c>((OpenChangeEventDetails)args.Details).PreventUnmountOnClose()</c>.
/// </para>
/// </remarks>
[CascadingTypeParameter(nameof(TPayload))]
public sealed class DrawerRoot<TPayload> : ComponentBase, IAsyncDisposable
{
    private DialogRootBehavior<TPayload>? behavior;
    private DrawerRootContext? drawer;
    private RenderFragment? dialogContent;
    private RenderFragment? cascade;
    private IDisposable? openSubscription;
    private DrawerProviderContext? reportedProvider;
    private bool reportedOpen;
    private bool disposed;
    private Controlled<SnapPointValue?>? snapPointState;
    private bool snapPointSupplied;
    private bool defaultSnapPointSupplied;
    private EventCallback<ValueChangeEventArgs<bool>> openChangeWrapper;

    // Created at the first parameter set, once the services are injected.
    private DialogRootBehavior<TPayload> Behavior => behavior ?? throw new InvalidOperationException("Blazix: `DrawerRoot` is used before its first parameter set.");

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [Inject]
    private ILogger<DrawerRoot<TPayload>> Logger { get; set; } = null!;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private IDialogRootContext? ParentContext { get; set; }

    // ADR 04 Amendments 2026-11-03 (5): the session's parent is the enclosing popup of any family.
    [CascadingParameter]
    private IFloatingNodeContext? FloatingParent { get; set; }

    // DRW-STR-10: the nearest enclosing Drawer root, through any intermediate dialog.
    [CascadingParameter]
    private DrawerRootContext? ParentDrawer { get; set; }

    [CascadingParameter]
    private DrawerProviderContext? Provider { get; set; }

    // DLG-API-01
    /// <summary>Gets or sets a value indicating whether the drawer is open, for <c>@bind-Open</c>.</summary>
    /// <value>Leave it unset for an uncontrolled drawer; once set at the first render, the drawer follows it.</value>
    [Parameter]
    public bool Open { get; set; }

    /// <summary>Gets or sets the callback <c>@bind-Open</c> uses.</summary>
    [Parameter]
    public EventCallback<bool> OpenChanged { get; set; }

    // DLG-API-02
    /// <summary>Gets or sets a value indicating whether an uncontrolled drawer is open initially.</summary>
    /// <value>The default is <see langword="false"/>.</value>
    [Parameter]
    public bool DefaultOpen { get; set; }

    // DLG-API-03
    /// <summary>Gets or sets the modality of the drawer.</summary>
    /// <value>
    /// <see langword="true"/>, the default when <see langword="null"/>: focus is trapped, the rest of the page is hidden from
    /// assistive technology and covered, and page scroll is locked. <see cref="ModalValue.TrapFocus"/>: focus is trapped and
    /// the page hidden, but it stays scrollable and clickable. <see langword="false"/>: nothing is trapped or hidden, and
    /// focus leaving the drawer closes it.
    /// </value>
    [Parameter]
    public ModalValue? Modal { get; set; }

    // DLG-API-04, DRW-EVT-01
    /// <summary>Gets or sets the callback called when the drawer is about to open or close, before the change is applied.</summary>
    /// <value>
    /// The arguments' <c>Value</c> is the requested open state; their <c>Details</c>, an <see cref="OpenChangeEventDetails"/>,
    /// carry the reason, the event and the trigger, and can cancel the change. A swipe reports
    /// <see cref="EventReason.Swipe"/> and the platform's back gesture <see cref="EventReason.CloseWatcher"/>.
    /// </value>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<bool>> OnOpenChange { get; set; }

    // DLG-API-05
    /// <summary>Gets or sets the callback called after the drawer's open or close animations finish.</summary>
    [Parameter]
    public EventCallback<bool> OnOpenChangeComplete { get; set; }

    // DLG-API-06
    /// <summary>Gets or sets a value indicating whether a press outside the drawer and focus leaving it never close it.</summary>
    /// <value>The default is <see langword="false"/>.</value>
    [Parameter]
    public bool DisablePointerDismissal { get; set; }

    // DRW-API-12
    /// <summary>Gets or sets the handle that connects this root with detached triggers and opens it from code.</summary>
    [Parameter]
    public DrawerHandle<TPayload>? Handle { get; set; }

    // DLG-API-10
    /// <summary>Gets or sets the id of the active trigger, to control it together with <see cref="Open"/>.</summary>
    /// <value>Leave it unset for an uncontrolled active trigger; once set at the first render, <see langword="null"/> means no active trigger.</value>
    [Parameter]
    public string? TriggerId { get; set; }

    // DLG-API-11
    /// <summary>Gets or sets the id of the trigger that is active initially, together with <see cref="DefaultOpen"/>.</summary>
    [Parameter]
    public string? DefaultTriggerId { get; set; }

    // DRW-API-01
    /// <summary>Gets or sets the direction that dismisses the drawer.</summary>
    /// <value>
    /// <see langword="null"/>, the default, for <see cref="Primitives.SwipeDirection.Down"/>. It also sets the axis of the
    /// gesture and, opposite, the direction a <see cref="DrawerSwipeArea"/> opens the drawer by default.
    /// </value>
    [Parameter]
    public SwipeDirection? SwipeDirection { get; set; }


    // DRW-API-02
    /// <summary>Gets or sets the snap points: the heights a vertical drawer rests at.</summary>
    /// <value>
    /// <see langword="null"/>, the default, for none. A number from 0 to 1 is a fraction of the viewport's height, a number
    /// greater than 1 a height in pixels, a string ending in <c>px</c> or <c>rem</c> a height in those units; any other value
    /// is ignored. Write the list as an expression, such as <c>SnapPoints="@([0.5, "200px", 1])"</c>. A list equal to the
    /// previous one, element by element, changes nothing.
    /// </value>
    [Parameter]
    public IReadOnlyList<SnapPointValue>? SnapPoints { get; set; }

    // DRW-API-03
    /// <summary>Gets or sets the active snap point, for <c>@bind-SnapPoint</c>.</summary>
    /// <value>
    /// Leave it unset for an uncontrolled snap point; once set at the first render, the drawer follows it, and
    /// <see langword="null"/> means no snap point. Write a number as a literal, such as <c>SnapPoint="0.5"</c>, and a string as
    /// an expression, such as <c>SnapPoint="@("200px")"</c>.
    /// </value>
    [Parameter]
    public SnapPointValue? SnapPoint { get; set; }

    /// <summary>Gets or sets the callback <c>@bind-SnapPoint</c> uses.</summary>
    [Parameter]
    public EventCallback<SnapPointValue?> SnapPointChanged { get; set; }

    // DRW-API-04
    /// <summary>Gets or sets the snap point an uncontrolled drawer starts at, and returns to when it closes.</summary>
    /// <value>
    /// When it is not set, the first of <see cref="SnapPoints"/>, or none. Once set, <see langword="null"/> means no snap
    /// point. Write a string as an expression, such as <c>DefaultSnapPoint="@("200px")"</c>.
    /// </value>
    [Parameter]
    public SnapPointValue? DefaultSnapPoint { get; set; }

    // DRW-API-05
    /// <summary>Gets or sets the callback called before the active snap point changes.</summary>
    /// <value>
    /// The arguments' <c>Value</c> is the requested snap point; their <c>Details</c> carry the reason, the event and the
    /// trigger, and can cancel the change.
    /// </value>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<SnapPointValue?>> OnSnapPointChange { get; set; }

    // DRW-API-06
    /// <summary>Gets or sets a value indicating whether a release moves at most one snap point, whatever its velocity.</summary>
    /// <value>The default is <see langword="false"/>.</value>
    [Parameter]
    public bool SnapToSequentialPoints { get; set; }

    /// <summary>Gets or sets the drawer's parts.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    // DLG-API-09
    /// <summary>Gets or sets the content rendered with the drawer's payload.</summary>
    /// <value>The parameter is the payload, or <see langword="default"/> when none was given.</value>
    [Parameter]
    public RenderFragment<TPayload?>? PayloadContent { get; set; }

    // For the fixtures: completes once the session handle is attached.
    internal Task SessionAttached => behavior?.SessionAttached ?? Task.CompletedTask;

    // For the tests: the root's dialog store.
    internal DialogStore<TPayload> Store => Behavior.Store;

    // For the tests and the parts: the Drawer-only state.
    internal DrawerRootContext DrawerContext => drawer ?? throw new InvalidOperationException("Blazix: `DrawerRoot` is used before its first parameter set.");

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        behavior ??= new DialogRootBehavior<TPayload>(DialogMode.Drawer, JSRuntime, StateHasChanged, DispatchExceptionAsync);
        behavior.SetParameters(parameters, IdGenerator, Logger);

        // DRW-API-03/04: a supplied null is a value, so supplied-ness is read from the parameter set (the TriggerId precedent).
        snapPointSupplied = parameters.TryGetValue<SnapPointValue?>(nameof(SnapPoint), out _);
        defaultSnapPointSupplied = parameters.TryGetValue<SnapPointValue?>(nameof(DefaultSnapPoint), out _);
        if (drawer is null)
        {
            snapPointState = new Controlled<SnapPointValue?>("Drawer", "snapPoint", Logger);
            openChangeWrapper = new EventCallback<ValueChangeEventArgs<bool>>(null, (Func<ValueChangeEventArgs<bool>, Task>)OnOpenChangeAsync);
            DialogStore<TPayload> store = behavior.Store;
            drawer = new DrawerRootContext(store, RequestSnapPointAsync, store.RegisterTrigger, store.UnregisterTrigger);
            dialogContent = behavior.BuildRenderTree;
            cascade = drawer.Cascade(dialogContent);
            openSubscription = behavior.Store.Subscribe(DialogFields.Open, ReportToProvider);
        }

        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        snapPointState!.Update(snapPointSupplied, SnapPoint, defaultValueSupplied: true, ResolvedDefaultSnapPoint());
        drawer!.SetSwipeDirection(SwipeDirection ?? Primitives.SwipeDirection.Down);
        drawer.SetSnapSettings(ActiveSnapPoint(), SnapPoints, SnapToSequentialPoints);
        Behavior.ApplyParameters(
            new PopupRootParameters<TPayload>(DefaultOpen, DefaultTriggerId, Handle, openChangeWrapper, OpenChanged, OnOpenChangeComplete, CloseOnActiveTriggerUnmount: false, ChildContent, PayloadContent),
            DialogRoot<TPayload>.ToModality(Modal),
            DisablePointerDismissal,
            ParentContext,
            FloatingParent);
        drawer!.SetAncestors(ParentDrawer, Provider);
        ReportToProvider();
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, cascade);

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => Behavior.OnAfterRenderAsync();

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (!disposed)
        {
            disposed = true;
            openSubscription?.Dispose();

            // DRW-STA-14: a root that leaves unregisters as closed, before after-paint work.
            reportedProvider?.Remove(this);
            reportedProvider = null;
        }

        return behavior?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    /// <summary>Closes the drawer, as a close button does, with the reason <see cref="EventReason.ImperativeAction"/>.</summary>
    /// <returns>A task that completes when the close request has run.</returns>
    public Task CloseAsync() => Behavior.CloseAsync();

    /// <summary>
    /// Removes the drawer's popup at once, ending its close animation or a close kept mounted by
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

    // DRW-STA-14: the open state to the nearest provider, once per change; a provider swap moves the registration.
    private void ReportToProvider()
    {
        if (disposed || drawer is null)
        {
            return;
        }

        bool open = Behavior.Store.Open;
        DrawerProviderContext? provider = drawer.Provider;
        if (!ReferenceEquals(provider, reportedProvider))
        {
            reportedProvider?.Remove(this);
            reportedProvider = provider;
            reportedOpen = false;
        }

        if (provider is not null && open != reportedOpen)
        {
            reportedOpen = open;
            provider.Report(this, open);
        }
    }

    // DRW-API-04 (DrawerRoot.tsx:65-66): DefaultSnapPoint when supplied (null included), else the first snap point, else none.
    private SnapPointValue? ResolvedDefaultSnapPoint() =>
        defaultSnapPointSupplied ? DefaultSnapPoint : SnapPoints is { Count: > 0 } points ? (SnapPointValue?)points[0] : null;

    // DRW-STA-02 (DrawerRoot.tsx:96-113): a controlled value as given; an uncontrolled value outside a non-empty list, or
    // none, falls back to the default (compared by Object.is on the number).
    private SnapPointValue? ActiveSnapPoint()
    {
        SnapPointValue? value = snapPointState!.Value;
        if (snapPointState.IsControlled || SnapPoints is not { Count: > 0 } points)
        {
            return value;
        }

        if (value is { } current)
        {
            for (int i = 0; i < points.Count; i++)
            {
                if (points[i] == current)
                {
                    return value;
                }
            }
        }

        return ResolvedDefaultSnapPoint();
    }

    // DRW-STA-03 (DrawerRoot.tsx:79-94): details, OnSnapPointChange, stop when canceled, write; no equality guard.
    private async Task RequestSnapPointAsync(SnapPointValue? next, ChangeEventDetails? details)
    {
        if (disposed)
        {
            return;
        }

        if (await snapPointState!.RequestAsync(next, details ?? new ChangeEventDetails(EventReason.None), OnSnapPointChange, SnapPointChanged))
        {
            drawer!.SetSnapSettings(ActiveSnapPoint(), SnapPoints, SnapToSequentialPoints);
        }
    }

    // DRW-STA-04 (DrawerRoot.tsx:150-169): the consumer's OnOpenChange first; after a close that was not canceled, with snap
    // points, the default snap point with the close's reason, event and trigger, before the store commits the close. The
    // callback has no receiver, so invoking it renders nothing of the root's.
    private async Task OnOpenChangeAsync(ValueChangeEventArgs<bool> args)
    {
        await OnOpenChange.InvokeAsync(args);
        if (args.Details.IsCanceled || args.Value || SnapPoints is not { Count: > 0 })
        {
            return;
        }

        ChangeEventDetails details = args.Details;
        await RequestSnapPointAsync(ResolvedDefaultSnapPoint(), new ChangeEventDetails(details.Reason, details.Event, details.Trigger));
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
