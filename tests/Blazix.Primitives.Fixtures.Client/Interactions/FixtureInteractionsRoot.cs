using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Fixtures.Client.Interactions;

/// <summary>
/// A test-only popup root (M4 track 3; the composite fixture precedent): it renders no element, composes
/// <see cref="InteractionsSessionBehavior"/> as a popup root will, keeps an uncontrolled open state and runs the open-change
/// sequence (the consumer's callback first, a cancel honored, then the write), and cascades itself to its triggers. Track
/// 1's popup host replaces it once on <c>main</c>.
/// </summary>
public sealed class FixtureInteractionsRoot : ComponentBase, IAsyncDisposable
{
    private InteractionsSessionBehavior? session;
    private string? activeTriggerId;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    /// <summary>Gets or sets a value indicating whether the session handle attaches; a page sets it late to attach its trigger first.</summary>
    [Parameter]
    public bool SessionReady { get; set; } = true;

    /// <summary>Gets or sets the callback raised after every committed change, so the page renders the state.</summary>
    [Parameter]
    public EventCallback OnChanged { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Gets a value indicating whether the popup is open.</summary>
    public bool Open { get; private set; }

    /// <summary>Gets the number of requests the store received.</summary>
    public int Requests { get; private set; }

    internal InteractionsSessionBehavior Session => session!;

    /// <summary>Gets a task that completes when the session handle is attached.</summary>
    public Task SessionAttached => session!.TwinAttached;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (session is not null)
        {
            await session.DisposeAsync();
        }
    }

    internal async Task RequestOpenChange(OpenChangeRequest request)
    {
        Requests++;
        if (request.Details.IsCanceled)
        {
            return;
        }

        Open = request.Open;
        if (request.Open && request.Details.Trigger?.Id is { } triggerId)
        {
            activeTriggerId = triggerId;
        }

        StateHasChanged();
        await OnChanged.InvokeAsync();
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        session = new InteractionsSessionBehavior(JSRuntime, IdGenerator, RequestOpenChange, exception => throw exception);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        session!.Open = Open;
        session.ActiveTriggerId = activeTriggerId;
        builder.OpenComponent<CascadingValue<FixtureInteractionsRoot>>(0);
        builder.AddComponentParameter(1, "Value", this);
        builder.AddComponentParameter(2, "IsFixed", true);
        builder.AddComponentParameter(3, "ChildContent", ChildContent);
        builder.CloseComponent();
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => SessionReady && RendererInfo.IsInteractive ? session!.AfterRenderAsync() : Task.CompletedTask;
}
