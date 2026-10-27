using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives.Rendering;

// ADR 02 decision 4; glossary "Part base"; ADR 08 §5
/// <summary>
/// Serves as the base class of every library part: it owns the parameters every part shares and renders the part's one
/// host element through the render pipeline.
/// </summary>
/// <typeparam name="TState">The type of the part's state record, a <c>sealed partial record</c> marked <see cref="PartStateAttribute"/>.</typeparam>
public abstract class PartBase<TState> : ComponentBase, IPartHost, IDisposable
    where TState : class, IPartState<TState>
{
    private readonly PartRenderState renderState;
    private string? generatedId;
    private IDisposable? selectorSubscription;

    /// <summary>Initializes a new instance of the <see cref="PartBase{TState}"/> class.</summary>
    protected PartBase()
    {
        renderState = new PartRenderState(this);
    }

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    // RND-API-04; the consumer-supplied attributes of RND §2.1
    /// <summary>Gets or sets every attribute the consumer passed that is not a parameter.</summary>
    /// <value>
    /// The unmatched attributes, such as <c>id</c>, <c>class</c>, <c>style</c>, <c>aria-*</c>, <c>data-*</c> and handlers;
    /// or <see langword="null"/>.
    /// </value>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>Gets or sets the content rendered inside the host element, Base UI's <c>children</c>.</summary>
    /// <value>The child content, or <see langword="null"/>.</value>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    // RND-STR-03
    /// <summary>Gets or sets the substitute element.</summary>
    /// <value>
    /// A fragment whose single root element or component is re-emitted with the merged attributes in place of the default
    /// element, or <see langword="null"/>.
    /// </value>
    [Parameter]
    public RenderFragment? Render { get; set; }

    // RND-STR-04
    /// <summary>Gets or sets the substitute callback.</summary>
    /// <value>
    /// A fragment that receives the merged attributes, the state and the child content, or <see langword="null"/>.
    /// </value>
    /// <remarks>
    /// Nothing is merged after the callback. This parameter is exclusive with <see cref="Render"/>; a render with both set
    /// throws <see cref="InvalidOperationException"/>.
    /// </remarks>
    [Parameter]
    public RenderFragment<RenderProps<TState>>? RenderFunction { get; set; }

    // ADR 02 decision 2, ADR 08 §2
    /// <summary>Gets or sets Base UI's <c>className</c> as a function of state.</summary>
    /// <value>A function from the state to a class string, or <see langword="null"/>.</value>
    /// <remarks>
    /// The plain <c>class</c> string arrives through <see cref="AdditionalAttributes"/> and comes first.
    /// </remarks>
    [Parameter]
    public Func<TState, string?>? ClassValue { get; set; }

    /// <summary>Gets or sets Base UI's <c>style</c> as a function of state.</summary>
    /// <value>A function from the state to an inline style string, or <see langword="null"/>.</value>
    /// <remarks>
    /// The plain <c>style</c> string arrives through <see cref="AdditionalAttributes"/>, and its keys are overridden by this
    /// function's.
    /// </remarks>
    [Parameter]
    public Func<TState, string?>? StyleValue { get; set; }

    // RND-DOM-25
    /// <summary>Gets or sets a value indicating whether the part renders.</summary>
    /// <value>
    /// <see langword="true"/> to render the part; <see langword="false"/> to render nothing and do no work. The default is
    /// <see langword="true"/>.
    /// </value>
    [Parameter]
    public bool Enabled { get; set; } = true;

    // RND-DOM-20..24 as relaxed by ADR 02
    /// <summary>Gets the host element.</summary>
    /// <value>
    /// The rendered element, set after the first render of the default or substitute element; <see langword="null"/>
    /// while disabled, under a substitute callback, and under a substitute whose root is a component.
    /// </value>
    public ElementReference? Element { get; private set; }

    // RND-STR-02
    /// <summary>Gets the tag of the default element.</summary>
    /// <value>The tag name of the element rendered when no substitute is supplied.</value>
    protected abstract string DefaultTagName { get; }

    // RND §4.3
    /// <summary>Gets the state object of this render.</summary>
    /// <value>The current state.</value>
    /// <remarks>An implementation returns the same instance while nothing changed.</remarks>
    protected abstract TState State { get; }

    // RND-DOM-06..09
    /// <summary>Gets the custom attribute mapping.</summary>
    /// <value>The mapping, or <see langword="null"/> for the default projection only. The default is <see langword="null"/>.</value>
    protected virtual StateAttributesMapping<TState>? StateAttributesMapping => null;

    // RND-DOM-27
    /// <summary>Gets a value indicating whether this part draws a generated id at first render.</summary>
    /// <value>
    /// <see langword="true"/> to draw a generated id; otherwise, <see langword="false"/>. The default is
    /// <see langword="false"/>.
    /// </value>
    /// <remarks>Parts that emit an <c>id</c> or an IDREF return <see langword="true"/>.</remarks>
    protected virtual bool UsesGeneratedId => false;

    /// <summary>Gets the generated id.</summary>
    /// <value>
    /// The id, in the form <c>base-ui-&lt;n&gt;</c>, drawn once at first render when <see cref="UsesGeneratedId"/> is
    /// <see langword="true"/>; otherwise, <see langword="null"/>.
    /// </value>
    protected string? GeneratedId => generatedId;

    // RND-DOM-32 precedence (2)–(3)
    /// <summary>Gets the part's id.</summary>
    /// <value>
    /// The consumer's <c>id</c> string if supplied, including the empty string; otherwise, the generated id, or
    /// <see langword="null"/> when the part draws no generated id.
    /// </value>
    protected string? Id => AdditionalAttributes is not null && AdditionalAttributes.TryGetValue("id", out object? supplied) && supplied is string text ? text : generatedId;

    // RND-DOM-27
    /// <inheritdoc />
    /// <remarks>Draws the generated id when <see cref="UsesGeneratedId"/> is <see langword="true"/> (drawn in render order) and calls <see cref="SubscribeSelectors"/>; a part that overrides this calls the base.</remarks>
    protected override void OnInitialized()
    {
        if (UsesGeneratedId)
        {
            generatedId = IdGenerator.Next();
        }

        SubscribeSelectors();
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Both <see cref="Render"/> and <see cref="RenderFunction"/> are set; the <see cref="Render"/> fragment does not have
    /// exactly one root element or component; or a Blazor event directive (<c>@on…:preventDefault</c> or
    /// <c>@on…:stopPropagation</c>) is applied to the part or its substitute element.
    /// </exception>
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        AttributeList partAttributes = renderState.PartAttributes;
        partAttributes.Clear();
        TState? state = null;
        if (Enabled)
        {
            state = State;
            if (generatedId is not null)
            {
                partAttributes.Set("id", generatedId);
            }

            AddPartAttributes(partAttributes);
        }

        RenderPipeline.Render(builder, renderState, this, new RenderInputs<TState>
        {
            State = state,
            Mapping = StateAttributesMapping,
            PartAttributes = partAttributes,
            ConsumerAttributes = AdditionalAttributes,
            ClassValue = ClassValue,
            StyleValue = StyleValue,
            Render = Render,
            RenderFunction = RenderFunction,
            ChildContent = ChildContent,
            DefaultTagName = DefaultTagName,
            Enabled = Enabled,
        });
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases the resources the part holds.</summary>
    /// <param name="disposing">
    /// <see langword="true"/> to release the store subscription and the render state; <see langword="false"/> to release
    /// nothing.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            selectorSubscription?.Dispose();
            selectorSubscription = null;
            renderState.Dispose();
        }
    }

    // ADR 16 rule 8
    /// <inheritdoc />
    /// <remarks>The part re-renders only when it calls <see cref="ComponentBase.StateHasChanged"/> itself, so a canceled request or a handler on a disabled part renders nothing; an exception, synchronous or from the returned task, routes through <see cref="ComponentBase.DispatchExceptionAsync"/> so error boundaries keep working.</remarks>
    Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem item, object? arg)
    {
        Task task;
        try
        {
            task = item.InvokeAsync(arg);
        }
        catch (Exception exception)
        {
            return DispatchExceptionAsync(exception);
        }

        return task.IsCompletedSuccessfully ? Task.CompletedTask : AwaitAndDispatchAsync(task);
    }

    /// <inheritdoc />
    void IPartHost.SetElement(ElementReference? element) => Element = element;

    /// <inheritdoc />
    bool IPartHost.ForwardsConsumerHandler(string eventName) => ForwardsConsumerHandler(eventName);

    /// <inheritdoc />
    void IPartHost.FinishAttributes(AttributeList attributes) => FinishAttributes(attributes);

    /// <inheritdoc />
    void IPartHost.BuildAfterElement(RenderTreeBuilder builder) => BuildAfterElement(builder);

    /// <inheritdoc />
    void IPartHost.BuildBeforeElement(RenderTreeBuilder builder) => BuildBeforeElement(builder);

    // ADR 02 Amendments 2026-10-21; UBT-EVT-01/03: a button-like part gates the consumer's handlers while disabled.
    /// <summary>Determines whether the consumer's handler for an event is rendered on the host element this render.</summary>
    /// <param name="eventName">The event's attribute name in lowercase, such as <c>onclick</c>, whatever casing the consumer used.</param>
    /// <returns>
    /// <see langword="true"/> to render the consumer's handler, chained with the substitute's and the part's;
    /// <see langword="false"/> to leave it off the element, so it is not invoked. The base implementation returns
    /// <see langword="true"/>.
    /// </returns>
    /// <remarks>
    /// The pipeline asks once per consumer handler on every render, and only for a value that is a handler. A substitute
    /// element's own handler for the same event is rendered either way. A part that declines a handler can still read it
    /// from <see cref="AdditionalAttributes"/> and invoke it itself.
    /// </remarks>
    protected virtual bool ForwardsConsumerHandler(string eventName) => true;

    // ADR 02 Amendments 2026-10-21 (M1 track 6); ADR 04 Amendments 2026-10-18 (handover at attach): a part that hands its
    // style over to a JavaScript module renders it once and then freezes it, and puts its own declarations above the
    // consumer's (COL-DOM-18); a value its document does not merge, such as a collapsible panel's empty id (COL-API-10).
    /// <summary>Adjusts the merged attributes of this render before the host element is emitted.</summary>
    /// <param name="attributes">
    /// The attributes the pipeline merged: the state attributes, the part's, the consumer's and the substitute's, with
    /// <c>class</c> and <c>style</c> merged and one handler per event.
    /// </param>
    /// <remarks>
    /// The pipeline calls this method once per render, after the merge and before the default element, the substitute
    /// element or the substitute callback receives the attributes. The base implementation changes nothing. Whatever a part
    /// sets here wins over the consumer, so a part changes only an attribute that it owns, such as a style it hands over to
    /// its browser-side behavior, or one whose fallback it defines, such as an empty <c>id</c>; every other attribute
    /// keeps the consumer's value.
    /// </remarks>
    protected virtual void FinishAttributes(AttributeList attributes)
    {
    }

    // ADR 02 Amendments 2026-10-22 item 2 (M2 track 4): the hidden inputs Switch, Checkbox and Radio render as the root's
    // next siblings (SWT-STR-01, CHK-STR-01, RAD-STR-01), which label activation needs outside the root.
    /// <summary>Renders the content that follows the host element, as its siblings.</summary>
    /// <param name="builder">The render tree builder of the part.</param>
    /// <remarks>
    /// The pipeline calls this method once per render, after the default element, the substitute element, the substitute
    /// component or the substitute callback, and not at all while <see cref="Enabled"/> is <see langword="false"/>. It
    /// opens a region of its own around the call, so the sequence numbers a part uses here start at 0 and never meet the
    /// pipeline's. The base implementation renders nothing.
    /// </remarks>
    protected virtual void BuildAfterElement(RenderTreeBuilder builder)
    {
    }

    // ADR 02 Amendments 2026-10-27 (M4 track 1): the focus guards a popup renders before an element, such as the portal's
    // leading outside guard (FLT-DOM-14), the mirror of BuildAfterElement.
    /// <summary>Renders the content that precedes the host element, as its siblings.</summary>
    /// <param name="builder">The render tree builder of the part.</param>
    /// <remarks>
    /// The pipeline calls this method once per render, before the default element, the substitute element, the substitute
    /// component or the substitute callback, and not at all while <see cref="Enabled"/> is <see langword="false"/>. It
    /// opens a region of its own around the call, so the sequence numbers a part uses here start at 0 and never meet the
    /// pipeline's. The base implementation renders nothing.
    /// </remarks>
    protected virtual void BuildBeforeElement(RenderTreeBuilder builder)
    {
    }

    /// <summary>Adds the part-supplied attributes of this render.</summary>
    /// <param name="attributes">The list to add to. The generated id is added to it before this method is called.</param>
    /// <remarks>
    /// Part-supplied attributes are the role, ARIA attributes and handlers created by
    /// <see cref="CreateHandler{TArgs}(string, Func{TArgs, Task})"/>. The base implementation adds nothing.
    /// </remarks>
    protected virtual void AddPartAttributes(AttributeList attributes)
    {
    }

    // ADR 05 §1
    /// <summary>
    /// Subscribes the part to the store it reads; called once from <see cref="OnInitialized"/>, before the first render.
    /// </summary>
    /// <remarks>
    /// The base implementation does nothing. A part that reads a store overrides this method, calls
    /// <see cref="SubscribeTo{TStoreState}"/> once and adds its selections to the returned subscription, which then
    /// re-renders the part only when a selection changed and is disposed with the part.
    /// </remarks>
    protected virtual void SubscribeSelectors()
    {
    }

    // ADR 05 §1, UTL-STA-25/27
    /// <summary>Subscribes this part to <paramref name="store"/> and returns the subscription to add selections to.</summary>
    /// <typeparam name="TStoreState">The type of the store's state record.</typeparam>
    /// <param name="store">The store to subscribe to.</param>
    /// <returns>
    /// The subscription; add selections to it with
    /// <see cref="SelectorSubscription{TStoreState}.Select{TValue}(Func{TStoreState, TValue})"/> or
    /// <see cref="SelectorSubscription{TStoreState}.Select{TArg, TValue}(Func{TStoreState, TArg, TValue}, TArg)"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">This part has already subscribed to a store.</exception>
    /// <remarks>
    /// On every store notification each selection is re-evaluated, and the part calls
    /// <see cref="ComponentBase.StateHasChanged"/> once when at least one changed by <see cref="Equality.Is{T}(T, T)"/>; a
    /// notification that changes no selection renders nothing. The subscription is disposed with the part. Call this method
    /// from <see cref="SubscribeSelectors"/>, at most once per part: a part has one store subscription.
    /// </remarks>
    protected SelectorSubscription<TStoreState> SubscribeTo<TStoreState>(Store<TStoreState> store)
        where TStoreState : class, IStoreState<TStoreState>
    {
        ArgumentNullException.ThrowIfNull(store);
        // ADR 05 §1: one subscription per part.
        if (selectorSubscription is not null)
        {
            throw new InvalidOperationException(
                $"Blazix: `{GetType().Name}` already subscribed to a store. A part has one store subscription: add every selection to the subscription `SubscribeTo` returned.");
        }

        SelectorSubscription<TStoreState> subscription = new(store, StateHasChanged);
        selectorSubscription = subscription;
        return subscription;
    }

    // ADR 02 decision 3, ADR 16 rule 2; RND-EVT-02/03
    /// <summary>Creates the one real handler for <paramref name="eventName"/>.</summary>
    /// <typeparam name="TArgs">The type of the event arguments.</typeparam>
    /// <param name="eventName">The event's attribute name, such as <c>onclick</c>; it is stored in lowercase.</param>
    /// <param name="handler">The part's own handler, which runs last.</param>
    /// <returns>The callback to add from <see cref="AddPartAttributes"/> every render.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="eventName"/> or <paramref name="handler"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="eventName"/> is empty.</exception>
    /// <remarks>
    /// Call this method once in the constructor or <see cref="OnInitialized"/>, keep the result in a field, and add it from
    /// <see cref="AddPartAttributes"/> every render. On dispatch the callback runs the substitute element's handler, then
    /// the consumer's, then <paramref name="handler"/>, stopping at a veto.
    /// </remarks>
    protected EventCallback CreateHandler<TArgs>(string eventName, Func<TArgs, Task> handler)
        where TArgs : EventArgs
        => renderState.CreateHandler(eventName, handler);

    /// <summary>Creates the one real handler for <paramref name="eventName"/> from a synchronous handler.</summary>
    /// <typeparam name="TArgs">The type of the event arguments.</typeparam>
    /// <param name="eventName">The event's attribute name, such as <c>onclick</c>; it is stored in lowercase.</param>
    /// <param name="handler">The part's own handler, which runs last.</param>
    /// <returns>The callback to add from <see cref="AddPartAttributes"/> every render.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="eventName"/> or <paramref name="handler"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="eventName"/> is empty.</exception>
    /// <remarks>This is the synchronous form of <see cref="CreateHandler{TArgs}(string, Func{TArgs, Task})"/>.</remarks>
    protected EventCallback CreateHandler<TArgs>(string eventName, Action<TArgs> handler)
        where TArgs : EventArgs
    {
        ArgumentNullException.ThrowIfNull(handler);
        return CreateHandler<TArgs>(eventName, args =>
        {
            handler(args);
            return Task.CompletedTask;
        });
    }

    private async Task AwaitAndDispatchAsync(Task task)
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
