using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives.Fixtures.Client.PopupFixtures;

/// <summary>
/// The test-only popup root at L4 (settled by the orchestrator 2026-10-27, plan §Settled "Test-only hosts"), the
/// counterpart of the L2 project's <c>TestPopupRoot</c>: renders no element and composes
/// <see cref="PopupRootBehavior{TStore, TState, TPayload}"/> with every shared root parameter.
/// </summary>
/// <typeparam name="TPayload">The type of the payload the popup's triggers carry.</typeparam>
[CascadingTypeParameter(nameof(TPayload))]
public sealed class FixturePopupRoot<TPayload> : ComponentBase, IDisposable
{
    private readonly PopupRootBehavior<FixturePopupStore<TPayload>, FixturePopupStoreState<TPayload>, TPayload> behavior;

    public FixturePopupRoot()
    {
        behavior = new((popupId, token, logger) => new FixturePopupStore<TPayload>(popupId, token, logger: logger), StateHasChanged);
    }

    [Inject]
    private PartIdGenerator Ids { get; set; } = null!;

    [Inject]
    private ILogger<FixturePopupRoot<TPayload>> Logger { get; set; } = null!;

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
    public FixturePopupHandle<TPayload>? Handle { get; set; }

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

    /// <summary>Gets a value indicating whether the popup is open.</summary>
    public bool IsOpen => behavior.Store.IsOpen;

    public override Task SetParametersAsync(ParameterView parameters)
    {
        behavior.SetParameters(parameters, Ids, Logger);
        return base.SetParametersAsync(parameters);
    }

    public void Dispose() => behavior.Dispose();

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

    protected override void BuildRenderTree(RenderTreeBuilder builder) => behavior.BuildRenderTree(builder);

    protected override Task OnAfterRenderAsync(bool firstRender) => behavior.OnAfterRenderAsync();
}
