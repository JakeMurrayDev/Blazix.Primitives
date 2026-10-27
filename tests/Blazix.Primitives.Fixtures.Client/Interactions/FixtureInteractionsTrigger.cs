using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using ButtonMechanism = Blazix.Primitives.ButtonBehavior;

namespace Blazix.Primitives.Fixtures.Client.Interactions;

/// <summary>
/// A test-only popup trigger (M4 track 3): a <c>button</c> carrying two handles, the <c>button</c> module's
/// (<see cref="ButtonMechanism"/>) and the <c>interactions</c> module's with press-to-toggle
/// (<see cref="InteractionsTriggerBehavior"/>), as Popover's and Dialog's triggers will.
/// </summary>
public sealed class FixtureInteractionsTrigger : PartBase<EmptyState>
{
    private readonly EventCallback onClick;
    private readonly EventCallback onKeyDown;
    private readonly EventCallback onKeyUp;
    private ButtonMechanism? button;
    private InteractionsTriggerBehavior? interactions;

    public FixtureInteractionsTrigger()
    {
        onClick = CreateHandler<MouseEventArgs>("onclick", e => interactions!.HandleClickAsync(e));
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => button!.HandleKeyDownAsync(e));
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", e => button!.HandleKeyUpAsync(e));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<FixtureInteractionsTrigger> Logger { get; set; } = null!;

    [Inject]
    private ButtonDevelopmentLog DevelopmentLog { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private FixtureInteractionsRoot Root { get; set; } = null!;

    /// <summary>Gets a task that completes when both handles are attached.</summary>
    public Task TwinsAttached => Task.WhenAll(button!.TwinAttached, interactions!.TwinAttached);

    /// <inheritdoc />
    protected override string DefaultTagName => "button";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        button = new ButtonMechanism(JSRuntime, Logger, DevelopmentLog, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown, onKeyUp);
        interactions = new InteractionsTriggerBehavior(JSRuntime, request => Root.RequestOpenChange(request), DispatchExceptionAsync, onClick)
        {
            Options = new InteractionsTriggerOptions { Click = new InteractionsClickOptions() },
        };
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        interactions!.SessionToken = Root.Session.Token;
        button!.SetConsumerAttributes(AdditionalAttributes);
        interactions.SetConsumerAttributes(AdditionalAttributes);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!RendererInfo.IsInteractive)
        {
            return;
        }

        await button!.AfterRenderAsync(Enabled, Element, Id, composite: false);
        await interactions!.AfterRenderAsync(Enabled, Element, Id);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            button?.Dispose();
            if (interactions is not null)
            {
                _ = interactions.DisposeAsync().AsTask();
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        button!.AddAttributes(attributes, Id, composite: false);
        interactions!.AddAttributes(attributes);
    }

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => button!.ForwardsConsumerHandler(eventName) && interactions!.ForwardsConsumerHandler(eventName);
}
