using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives.Rendering;

// RND §3.2, ADR 02 decision 4
/// <summary>
/// Renders an element through the same static render pipeline as the library's parts, for consumers building their own
/// components; this is Base UI's <c>useRender</c>.
/// </summary>
/// <typeparam name="TState">The type of the state record, a <c>sealed partial record</c> marked <see cref="PartStateAttribute"/>.</typeparam>
/// <remarks><see cref="ChildContent"/> is the Razor form of Base UI's <c>props.children</c>.</remarks>
public sealed class RenderElement<TState> : ComponentBase, IPartHost, IDisposable
    where TState : class, IPartState<TState>
{
    private readonly PartRenderState renderState;
    private TState? state;

    /// <summary>Initializes a new instance of the <see cref="RenderElement{TState}"/> class.</summary>
    public RenderElement()
    {
        renderState = new PartRenderState(this);
    }

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
    /// This parameter is exclusive with <see cref="Render"/>; a render with both set throws
    /// <see cref="InvalidOperationException"/>.
    /// </remarks>
    [Parameter]
    public RenderFragment<RenderProps<TState>>? RenderFunction { get; set; }

    // RND-STA-02/03
    /// <summary>Gets or sets the state object.</summary>
    /// <value>
    /// The state projected to <c>data-*</c> attributes and passed to <see cref="RenderFunction"/>. It is required unless
    /// <typeparamref name="TState"/> is <see cref="EmptyState"/>, in which case an omitted state is
    /// <see cref="EmptyState.Instance"/>.
    /// </value>
    [Parameter]
    public TState State { get; set; } = null!;

    // RND-DOM-06..09
    /// <summary>Gets or sets the custom mapping from state fields to <c>data-*</c> attributes.</summary>
    /// <value>The mapping, or <see langword="null"/> for the default projection only.</value>
    [Parameter]
    public StateAttributesMapping<TState>? StateAttributesMapping { get; set; }

    // RND §2.1 row 2
    /// <summary>Gets or sets the attributes to put on the element.</summary>
    /// <value>
    /// A single dictionary, not a list, merged over the state attributes; or <see langword="null"/>.
    /// </value>
    /// <remarks>Values are case values of <see cref="AttributeValue"/>, boxed callbacks included.</remarks>
    [Parameter]
    public IReadOnlyDictionary<string, object>? Props { get; set; }

    // RND-DOM-25
    /// <summary>Gets or sets a value indicating whether the element renders.</summary>
    /// <value>
    /// <see langword="true"/> to render the element; <see langword="false"/> to render nothing. The default is
    /// <see langword="true"/>.
    /// </value>
    [Parameter]
    public bool Enabled { get; set; } = true;

    // RND-STR-08/09
    /// <summary>Gets or sets the tag rendered when <see cref="Render"/> and <see cref="RenderFunction"/> are absent.</summary>
    /// <value>The tag name. The default is <c>div</c>.</value>
    [Parameter]
    public string DefaultTagName { get; set; } = "div";

    /// <summary>Gets or sets the content rendered inside the element.</summary>
    /// <value>The child content, or <see langword="null"/>.</value>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Gets the rendered element.</summary>
    /// <value>The element, under the same rules as <see cref="PartBase{TState}.Element"/>.</value>
    public ElementReference? Element { get; private set; }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// <see cref="State"/> is <see langword="null"/> and <typeparamref name="TState"/> is not <see cref="EmptyState"/>.
    /// </exception>
    protected override void OnParametersSet()
    {
        if (State is not null)
        {
            state = State;
        }
        else if (typeof(TState) == typeof(EmptyState))
        {
            // Base UI's useRender defaults the state to {}; EmptyState is that record and projects nothing.
            state = (TState)(object)EmptyState.Instance;
        }
        else
        {
            throw new InvalidOperationException(
                $"Blazix: `RenderElement<{typeof(TState).Name}>` needs a `State`. Only `RenderElement<EmptyState>` may omit it.");
        }
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Both <see cref="Render"/> and <see cref="RenderFunction"/> are set; the <see cref="Render"/> fragment does not have
    /// exactly one root element or component; or a Blazor event directive (<c>@on…:preventDefault</c> or
    /// <c>@on…:stopPropagation</c>) is applied to the part or its substitute element.
    /// </exception>
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        RenderPipeline.Render(builder, renderState, this, new RenderInputs<TState>
        {
            State = state,
            Mapping = StateAttributesMapping,
            ConsumerAttributes = Props,
            Render = Render,
            RenderFunction = RenderFunction,
            ChildContent = ChildContent,
            DefaultTagName = DefaultTagName,
            Enabled = Enabled,
        });
    }

    /// <inheritdoc />
    public void Dispose() => renderState.Dispose();

    // ADR 16 rule 8
    /// <inheritdoc />
    /// <remarks>
    /// As for <see cref="PartBase{TState}"/>, the component re-renders only when it requests a render itself, and an
    /// exception routes to the nearest error boundary.
    /// </remarks>
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
    bool IPartHost.ForwardsConsumerHandler(string eventName) => true;

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
