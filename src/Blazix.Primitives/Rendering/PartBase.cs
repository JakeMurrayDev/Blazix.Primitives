using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives.Rendering;

/// <summary>
/// The base class of every library part (ADR 02 decision 4; glossary "Part base"): owns the state object, the unmatched
/// attribute bag, <see cref="ClassValue"/>/<see cref="StyleValue"/>, <see cref="Render"/>/<see cref="RenderFunction"/>,
/// <see cref="Enabled"/> and <see cref="Element"/>, and calls the static pipeline from <see cref="BuildRenderTree"/>.
/// Member order and the contributor template: <c>Rendering/PartBase.template.md</c> (ADR 08 §5).
/// </summary>
/// <typeparam name="TState">The part's state record, a <c>sealed partial record</c> marked <see cref="PartStateAttribute"/>.</typeparam>
public abstract class PartBase<TState> : ComponentBase, IHandleEvent, IDisposable
    where TState : class, IPartState<TState>
{
    private string? generatedId;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    /// <summary>Every attribute the consumer passed that is not a parameter: <c>id</c>, <c>class</c>, <c>style</c>, <c>aria-*</c>, <c>data-*</c>, handlers (RND-API-04; the consumer-supplied attributes of RND §2.1).</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>The content rendered inside the host element (Base UI's <c>children</c>).</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>The substitute element (RND-STR-03): a fragment whose single root element or component is re-emitted with the merged attributes.</summary>
    [Parameter]
    public RenderFragment? Render { get; set; }

    /// <summary>The substitute callback (RND-STR-04): receives the merged attributes, the state and the child content; nothing is merged after it. Exclusive with <see cref="Render"/>.</summary>
    [Parameter]
    public RenderFragment<RenderProps<TState>>? RenderFunction { get; set; }

    /// <summary>Base UI's <c>className</c> as a function of state; the plain <c>class</c> string arrives through the bag and comes first (ADR 02 decision 2, ADR 08 §2).</summary>
    [Parameter]
    public Func<TState, string?>? ClassValue { get; set; }

    /// <summary>Base UI's <c>style</c> as a function of state; the plain <c>style</c> string arrives through the bag and its keys are overridden by this one's.</summary>
    [Parameter]
    public Func<TState, string?>? StyleValue { get; set; }

    /// <summary><c>false</c> renders nothing and does no work (RND-DOM-25).</summary>
    [Parameter]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The host element (RND-DOM-20..24 as relaxed by ADR 02): set after the first render of the default or substitute
    /// element, <c>null</c> while disabled, under a substitute callback and under a substitute whose root is a component.
    /// </summary>
    public ElementReference? Element { get; private set; }

    /// <summary>The tag of the default element (RND-STR-02).</summary>
    protected abstract string DefaultTagName { get; }

    /// <summary>The state object of this render (RND §4.3). Return the same instance while nothing changed.</summary>
    protected abstract TState State { get; }

    /// <summary>The custom attribute mapping (RND-DOM-06..09), or <c>null</c> for the default projection only.</summary>
    protected virtual StateAttributesMapping<TState>? StateAttributesMapping => null;

    /// <summary>Whether this part draws a generated id at first render (RND-DOM-27). Parts that emit an <c>id</c> or an IDREF return <c>true</c>.</summary>
    protected virtual bool UsesGeneratedId => false;

    /// <summary>The generated id (<c>base-ui-&lt;n&gt;</c>), drawn once at first render when <see cref="UsesGeneratedId"/>; otherwise <c>null</c>.</summary>
    protected string? GeneratedId => generatedId;

    /// <summary>The id of RND-DOM-32 precedence (2)–(3): the consumer's <c>id</c> if supplied (including the empty string), else the generated one.</summary>
    protected string? Id => AdditionalAttributes is not null && AdditionalAttributes.TryGetValue("id", out object? supplied) && supplied is string text ? text : generatedId;

    protected override void OnInitialized()
    {
        if (UsesGeneratedId)
        {
            generatedId = IdGenerator.Next();
        }

        SubscribeSelectors();
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// ADR 16 rule 8: a part re-renders only when it calls <see cref="ComponentBase.StateHasChanged"/> itself; a
    /// cancelled request or a handler on a disabled part renders nothing. Exceptions route through
    /// <see cref="ComponentBase.DispatchExceptionAsync"/> so error boundaries keep working.
    /// </summary>
    Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem item, object? arg)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Adds the part-supplied attributes of this render (role, ARIA, handlers created by <see cref="CreateHandler{TArgs}(string, Func{TArgs, Task})"/>). The generated id is added before this is called.</summary>
    protected virtual void AddPartAttributes(AttributeList attributes)
    {
    }

    /// <summary>
    /// The seam for the store selector subscription of ADR 05 §1, wired by the store track: called once from
    /// <see cref="OnInitialized"/>; a subscribing part calls <see cref="ComponentBase.StateHasChanged"/> from its
    /// subscription when a selection changed and releases it in <see cref="Dispose(bool)"/>.
    /// </summary>
    protected virtual void SubscribeSelectors()
    {
    }

    /// <summary>
    /// Creates the one real handler for <paramref name="eventName"/> (ADR 02 decision 3, ADR 16 rule 2): call it once in
    /// the constructor or <see cref="OnInitialized"/>, keep the result in a field, and add it from
    /// <see cref="AddPartAttributes"/> every render. On dispatch it runs the substitute element's handler, then the
    /// consumer's, then <paramref name="handler"/>, stopping at a veto (RND-EVT-02/03).
    /// </summary>
    protected EventCallback CreateHandler<TArgs>(string eventName, Func<TArgs, Task> handler)
        where TArgs : EventArgs
    {
        throw new NotImplementedException();
    }

    /// <summary>The synchronous form of <see cref="CreateHandler{TArgs}(string, Func{TArgs, Task})"/>.</summary>
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

    protected virtual void Dispose(bool disposing)
    {
    }
}
