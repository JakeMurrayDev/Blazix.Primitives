using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// A test-only popup root (settled by the orchestrator 2026-10-27, plan §Settled "Test-only hosts"): renders no element and
/// composes <see cref="PopupRootBehavior{TStore, TState, TPayload}"/> with every shared root parameter, as the M4 families
/// will.
/// </summary>
[CascadingTypeParameter(nameof(TPayload))]
internal sealed class TestPopupRoot<TPayload> : ComponentBase, IDisposable
{
    private readonly PopupRootBehavior<TestPopupStore<TPayload>, TestPopupStoreState<TPayload>, TPayload> behavior;

    public TestPopupRoot()
    {
        behavior = new((popupId, token, logger) => new TestPopupStore<TPayload>(popupId, token, logger: logger), StateHasChanged);
    }

    [Inject]
    private PartIdGenerator Ids { get; set; } = null!;

    [Inject]
    private ILogger<TestPopupRoot<TPayload>> Logger { get; set; } = null!;

    [Parameter]
    public bool Open { get; set; }

    [Parameter]
    public EventCallback<bool> OpenChanged { get; set; }

    [Parameter]
    public bool DefaultOpen { get; set; }

    [Parameter]
    public string? TriggerId { get; set; }

    [Parameter]
    public string? DefaultTriggerId { get; set; }

    [Parameter]
    public TestPopupHandle<TPayload>? Handle { get; set; }

    [Parameter]
    public EventCallback<ValueChangeEventArgs<bool>> OnOpenChange { get; set; }

    [Parameter]
    public EventCallback<bool> OnOpenChangeComplete { get; set; }

    [Parameter]
    public bool CloseOnActiveTriggerUnmount { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public RenderFragment<TPayload?>? PayloadContent { get; set; }

    public TestPopupStore<TPayload> Store => behavior.Store;

    public int Renders { get; private set; }

    public override Task SetParametersAsync(ParameterView parameters)
    {
        behavior.SetParameters(parameters, Ids, Logger);
        return base.SetParametersAsync(parameters);
    }

    public void Dispose() => behavior.Dispose();

    public Task CloseAsync() => Store.CloseAsync();

    public void Unmount() => _ = Store.UnmountAsync();

    protected override void OnParametersSet()
    {
        behavior.ApplyParameters(new PopupRootParameters<TPayload>(
            DefaultOpen,
            DefaultTriggerId,
            Handle,
            OnOpenChange,
            OpenChanged,
            OnOpenChangeComplete,
            CloseOnActiveTriggerUnmount,
            ChildContent,
            PayloadContent));
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        Renders++;
        behavior.BuildRenderTree(builder);
    }

    protected override Task OnAfterRenderAsync(bool firstRender) => behavior.OnAfterRenderAsync();
}
