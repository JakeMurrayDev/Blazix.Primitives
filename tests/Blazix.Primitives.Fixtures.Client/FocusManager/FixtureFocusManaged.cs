using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Fixtures.Client.FocusManagerFixtures;

/// <summary>
/// A test-only focus-managed popup for the <c>focus-manager-*</c> fixtures (tests/browser/focus-manager.spec.ts): a
/// <c>div</c> with the Popup parts' focusable props (<c>role="dialog"</c>, <c>tabindex="-1"</c>,
/// <c>data-base-ui-focusable</c>) and <c>data-open</c> while <see cref="Open"/>, the trailing inside focus guard, and the
/// <see cref="FocusManagerTwin"/> handle with every option as a parameter. No public part manages focus before Popover
/// and Dialog (the M2 and M3 composite precedent: ADR 12 Amendments 2026-10-27 item 1).
/// </summary>
public sealed class FixtureFocusManaged : PartBase<EmptyState>
{
    private readonly TaskCompletionSource attached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private FocusManagerTwin? twin;
    private TwinHandle<FocusManagerOptions>? handle;
    private FocusManagerOptions? sent;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Parameter]
    public bool Open { get; set; }

    [Parameter]
    public bool Modal { get; set; } = true;

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    public bool InitialFocus { get; set; } = true;

    [Parameter]
    public string? InitialFocusId { get; set; }

    [Parameter]
    public bool ReturnFocus { get; set; } = true;

    [Parameter]
    public string? ReturnFocusId { get; set; }

    [Parameter]
    public bool? ExplicitReturnFocus { get; set; }

    [Parameter]
    public string? CloseInteractionType { get; set; }

    [Parameter]
    public string RestoreFocus { get; set; } = FocusManagerTwin.RestoreNone;

    [Parameter]
    public bool CloseOnFocusOut { get; set; } = true;

    [Parameter]
    public string? OpenInteractionType { get; set; } = "";

    [Parameter]
    public string? DomReferenceId { get; set; }

    /// <summary>Gets or sets a value indicating whether the popup renders <c>tabindex="-1"</c>, as every Popup part does.</summary>
    [Parameter]
    public bool RenderTabIndex { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether changed options are sent before the render that applies them (design point
    /// 1 (d)) rather than only from <c>OnAfterRender</c>.
    /// </summary>
    [Parameter]
    public bool PushBeforeRender { get; set; } = true;

    [Parameter]
    public EventCallback<EventSnapshot> OnFocusOutClose { get; set; }

    [Parameter]
    public EventCallback<string> OnInteractionTypeChange { get; set; }

    public Task TwinAttached => attached.Task;

    /// <summary>Gets a value indicating whether C# renders the inside guards: enabled and modal (no portal before track 1).</summary>
    public bool RendersInsideGuards => !Disabled && Modal;

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // Sent while the render that applies them is still being built, so the interop message precedes the batch.
        if (PushBeforeRender && handle is not null)
        {
            _ = PushAsync();
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!RendererInfo.IsInteractive)
        {
            return;
        }

        if (twin is null)
        {
            twin = new FocusManagerTwin(JSRuntime);
            twin.FocusOutCloseRequested += request => _ = InvokeAsync(() => OnFocusOutClose.InvokeAsync(request.Event));
            twin.InteractionTypeChanged += type => _ = InvokeAsync(() => OnInteractionTypeChange.InvokeAsync(type));
            sent = BuildOptions();
            handle = Element is { } element
                ? await twin.AttachAsync(element, sent)
                : await twin.AttachAsync(Id!, sent);
            attached.TrySetResult();
            StateHasChanged();
            return;
        }

        await PushAsync();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && twin is not null)
        {
            _ = twin.DisposeAsync();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("role", "dialog");
        if (RenderTabIndex)
        {
            attributes.Set("tabindex", "-1");
        }

        attributes.Set("data-base-ui-focusable", "");
        if (attached.Task.IsCompleted)
        {
            attributes.Set("data-handle-attached", "");
        }

        if (Open)
        {
            attributes.Set("data-open", "");
        }
    }

    /// <inheritdoc />
    protected override void BuildAfterElement(RenderTreeBuilder builder)
    {
        if (RendersInsideGuards)
        {
            builder.AddContent(0, FocusManagerMarkup.InsideGuard);
        }
    }

    private async Task PushAsync()
    {
        FocusManagerOptions next = BuildOptions();
        if (handle is null || next.Equals(sent))
        {
            return;
        }

        sent = next;
        await handle.UpdateAsync(next);
    }

    private FocusManagerOptions BuildOptions() => new()
    {
        Modal = Modal,
        Disabled = Disabled,
        InitialFocus = InitialFocus,
        InitialFocusId = InitialFocusId,
        ReturnFocus = ReturnFocus,
        ReturnFocusId = ReturnFocusId,
        ExplicitReturnFocus = ExplicitReturnFocus,
        CloseInteractionType = CloseInteractionType,
        RestoreFocus = RestoreFocus,
        CloseOnFocusOut = CloseOnFocusOut,
        OpenInteractionType = OpenInteractionType,
        TreatPopupAsFloatingElement = true,
        DomReferenceId = DomReferenceId,
    };
}

/// <summary>The inside focus guard as C# renders it (the Popup parts render it through the pipeline once track 1 lands).</summary>
public static class FocusManagerMarkup
{
    /// <summary>Gets the inside focus guard: a visually hidden <c>span</c> with <c>tabindex="0"</c>, <c>data-base-ui-focus-guard</c>, <c>data-type="inside"</c> and <c>aria-hidden="true"</c>.</summary>
    public static RenderFragment InsideGuard { get; } = builder =>
    {
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "tabindex", "0");
        builder.AddAttribute(2, "data-base-ui-focus-guard", "");
        builder.AddAttribute(3, "data-type", "inside");
        builder.AddAttribute(4, "aria-hidden", "true");
        builder.AddAttribute(5, "style", "position: fixed; top: 0; left: 0; clip-path: inset(50%); overflow: hidden; white-space: nowrap; border: 0; padding: 0; width: 1px; height: 1px; margin: 0");
        builder.CloseElement();
    };
}
