using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// A bUnit context with the <c>interactions</c> module mocked: every attach, update and dispose is recorded, and the twin
/// of each attach is reachable, so a test drives its <c>[JSInvokable]</c> callbacks and decision points as the module would.
/// </summary>
internal sealed class InteractionsTestContext : IDisposable
{
    public const string ModulePath = "./_content/Blazix.Primitives/js/interactions.js";

    public InteractionsTestContext()
    {
        Context.Services.AddBlazixPrimitives();
        Module = Context.JSInterop.SetupModule(ModulePath);
        Handle = Module.SetupModule("attach", _ => true);
        Handle.SetupVoid("update", _ => true).SetVoidResult();
        Handle.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    public BunitContext Context { get; } = new();

    public BunitJSModuleInterop Module { get; }

    public BunitJSModuleInterop Handle { get; }

    public IReadOnlyList<JSRuntimeInvocation> Attaches => Module.Invocations["attach"];

    public IReadOnlyList<string> UpdateOptions => [.. Handle.Invocations["update"].Select(invocation => (string)invocation.Arguments[0]!)];

    /// <summary>Gets the twin of the session handle (the attach whose target is <see langword="null"/>).</summary>
    public InteractionsTwin SessionTwin => TwinOf(Attaches.Single(attach => attach.Arguments[0] is null));

    /// <summary>Gets the twin of the trigger handle (the attach whose options carry a trigger).</summary>
    public InteractionsTwin TriggerTwin => TwinOf(Attaches.Single(attach => ((string)attach.Arguments[1]!).Contains("\"trigger\"", StringComparison.Ordinal)));

    public void Dispose() => Context.Dispose();

    private static InteractionsTwin TwinOf(JSRuntimeInvocation attach) => ((DotNetObjectReference<InteractionsTwin>)attach.Arguments[2]!).Value;
}

/// <summary>
/// A test-only popup root (the composite precedent): it renders no element, composes <see cref="InteractionsSessionBehavior"/>
/// as a popup root will, keeps an uncontrolled open state and runs the open-change sequence of ADR 05 §2 (the consumer's
/// callback first, a cancel honored, then the write), and cascades itself to its triggers. Track 1's popup store replaces
/// it in the family tracks.
/// </summary>
internal sealed class TestInteractionsRoot : ComponentBase, IAsyncDisposable
{
    private InteractionsSessionBehavior? session;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    /// <summary>The consumer's open-change callback, run synchronously as a popup store runs a synchronous handler.</summary>
    [Parameter]
    public Action<bool, OpenChangeEventDetails>? OnOpenChange { get; set; }

    [Parameter]
    public InteractionsDismissOptions? Dismiss { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    public bool Open { get; private set; }

    public string? ActiveTriggerId { get; private set; }

    public List<OpenChangeRequest> Requests { get; } = [];

    public InteractionsSessionBehavior Session => session!;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (session is not null)
        {
            await session.DisposeAsync();
        }
    }

    /// <summary>The popup store's open-change sequence for one request.</summary>
    public Task RequestOpenChange(OpenChangeRequest request)
    {
        Requests.Add(request);
        OnOpenChange?.Invoke(request.Open, request.Details);
        if (request.Details.IsCanceled)
        {
            return Task.CompletedTask;
        }

        Open = request.Open;
        if (request.Open && request.Details.Trigger?.Id is { } triggerId)
        {
            ActiveTriggerId = triggerId;
        }

        return InvokeAsync(StateHasChanged);
    }

    /// <summary>Requests a render with the current parameters, for the update and allocation tests.</summary>
    public void Refresh() => StateHasChanged();

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        session = new InteractionsSessionBehavior(JSRuntime, IdGenerator, RequestOpenChange, exception => throw exception);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        session!.Dismiss = Dismiss;
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        session!.Open = Open;
        session.ActiveTriggerId = ActiveTriggerId;
        builder.OpenComponent<CascadingValue<TestInteractionsRoot>>(0);
        builder.AddComponentParameter(1, "Value", this);
        builder.AddComponentParameter(2, "IsFixed", true);
        builder.AddComponentParameter(3, "ChildContent", ChildContent);
        builder.CloseComponent();
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => session!.AfterRenderAsync();
}

/// <summary>
/// A test-only popup trigger: a <c>button</c> that composes <see cref="InteractionsTriggerBehavior"/> as Popover's and
/// Dialog's triggers will, with press-to-toggle on; a test counts its renders through its class function.
/// </summary>
internal sealed class TestInteractionsTrigger : PartBase<EmptyState>
{
    private readonly EventCallback onClick;
    private InteractionsTriggerBehavior? behavior;

    public TestInteractionsTrigger()
    {
        onClick = CreateHandler<MouseEventArgs>("onclick", e => behavior!.HandleClickAsync(e));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private TestInteractionsRoot Root { get; set; } = null!;

    [Parameter]
    public InteractionsClickOptions? Click { get; set; } = new();

    public InteractionsTriggerBehavior Behavior => behavior!;

    /// <summary>Occurs after each render of the trigger, so a test proves that the part rendered.</summary>
    public event Action? Rendered;

    /// <inheritdoc />
    protected override string DefaultTagName => "button";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new InteractionsTriggerBehavior(JSRuntime, request => Root.RequestOpenChange(request), exception => throw exception, onClick);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        behavior!.SessionToken = Root.Session.Token;
        if (!Equals(behavior.Options.Click, Click))
        {
            behavior.Options = behavior.Options with { Click = Click };
        }

        behavior.SetConsumerAttributes(AdditionalAttributes);
    }

    /// <summary>Requests a render with the current parameters, for the update and allocation tests.</summary>
    public void Refresh() => StateHasChanged();

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        Rendered?.Invoke();
        return behavior!.AfterRenderAsync(Enabled, Element, Id);
    }

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
    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddAttributes(attributes);

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => behavior!.ForwardsConsumerHandler(eventName);
}
