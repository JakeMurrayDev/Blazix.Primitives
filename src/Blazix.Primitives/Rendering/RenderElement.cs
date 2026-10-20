using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives.Rendering;

// RND §3.2, ADR 02 decision 4
/// <summary>
/// The public rendering pipeline, Base UI's <c>useRender</c>, for consumers building
/// their own components over the same static function as the library's parts: a <see cref="Render"/> element or
/// <see cref="RenderFunction"/> callback, a <see cref="State"/> projected to <c>data-*</c> attributes with an optional
/// <see cref="StateAttributesMapping"/>, the <see cref="Props"/> to put on the element, <see cref="Enabled"/> and
/// <see cref="DefaultTagName"/>. <see cref="ChildContent"/> is the Razor form of <c>props.children</c>.
/// </summary>
public sealed class RenderElement<TState> : ComponentBase, IPartHost, IDisposable
    where TState : class, IPartState<TState>
{
    private readonly PartRenderState renderState;

    public RenderElement()
    {
        renderState = new PartRenderState(this);
    }

    // RND-STR-03
    /// <summary>The substitute element.</summary>
    [Parameter]
    public RenderFragment? Render { get; set; }

    // RND-STR-04
    /// <summary>The substitute callback. Exclusive with <see cref="Render"/>.</summary>
    [Parameter]
    public RenderFragment<RenderProps<TState>>? RenderFunction { get; set; }

    // RND-STA-02/03
    /// <summary>The state object; <c>null</c> behaves as an empty state: nothing is projected and a substitute callback receives a <c>null</c> state.</summary>
    [Parameter]
    public TState? State { get; set; }

    // RND-DOM-06..09
    /// <summary>The custom projection per state field.</summary>
    [Parameter]
    public StateAttributesMapping<TState>? StateAttributesMapping { get; set; }

    // RND §2.1 row 2
    /// <summary>The attributes to put on the element, merged over the state attributes; a single object, not a list. Values are case values of <see cref="AttributeValue"/>, boxed callbacks included.</summary>
    [Parameter]
    public IReadOnlyDictionary<string, object>? Props { get; set; }

    // RND-DOM-25
    /// <summary><c>false</c> renders nothing.</summary>
    [Parameter]
    public bool Enabled { get; set; } = true;

    // RND-STR-08/09
    /// <summary>The tag rendered when <see cref="Render"/> and <see cref="RenderFunction"/> are absent.</summary>
    [Parameter]
    public string DefaultTagName { get; set; } = "div";

    /// <summary>The content rendered inside the element.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>The rendered element, under the same rules as <see cref="PartBase{TState}.Element"/>.</summary>
    public ElementReference? Element { get; private set; }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        RenderPipeline.Render(builder, renderState, this, new RenderInputs<TState>
        {
            State = State,
            Mapping = StateAttributesMapping,
            ConsumerAttributes = Props,
            Render = Render,
            RenderFunction = RenderFunction,
            ChildContent = ChildContent,
            DefaultTagName = DefaultTagName,
            Enabled = Enabled,
        });
    }

    // ADR 16 rule 8
    /// <inheritdoc />
    /// <remarks>As for <see cref="PartBase{TState}"/>: no render unless requested; exceptions route to the error boundary.</remarks>
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
    public void Dispose() => renderState.Dispose();

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
