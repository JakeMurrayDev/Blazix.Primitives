using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives.Rendering;

/// <summary>
/// The public rendering pipeline, Base UI's <c>useRender</c> (RND §3.2, ADR 02 decision 4), for consumers building
/// their own components over the same static function as the library's parts: a <see cref="Render"/> element or
/// <see cref="RenderFunction"/> callback, a <see cref="State"/> projected to <c>data-*</c> attributes with an optional
/// <see cref="StateAttributesMapping"/>, the <see cref="Props"/> to put on the element, <see cref="Enabled"/> and
/// <see cref="DefaultTagName"/>. <see cref="ChildContent"/> is the Razor form of <c>props.children</c>.
/// </summary>
public sealed class RenderElement<TState> : ComponentBase, IHandleEvent, IDisposable
    where TState : class, IPartState<TState>
{
    /// <summary>The substitute element (RND-STR-03).</summary>
    [Parameter]
    public RenderFragment? Render { get; set; }

    /// <summary>The substitute callback (RND-STR-04). Exclusive with <see cref="Render"/>.</summary>
    [Parameter]
    public RenderFragment<RenderProps<TState>>? RenderFunction { get; set; }

    /// <summary>The state object; <c>null</c> behaves as an empty state (RND-STA-02/03).</summary>
    [Parameter]
    public TState? State { get; set; }

    /// <summary>The custom projection per state field (RND-DOM-06..09).</summary>
    [Parameter]
    public StateAttributesMapping<TState>? StateAttributesMapping { get; set; }

    /// <summary>The attributes to put on the element, merged over the state attributes (RND §2.1 row 2); a single object, not a list. Values are case values of <see cref="AttributeValue"/>, boxed callbacks included.</summary>
    [Parameter]
    public IReadOnlyDictionary<string, object>? Props { get; set; }

    /// <summary><c>false</c> renders nothing (RND-DOM-25).</summary>
    [Parameter]
    public bool Enabled { get; set; } = true;

    /// <summary>The tag rendered when <see cref="Render"/> and <see cref="RenderFunction"/> are absent (RND-STR-08/09).</summary>
    [Parameter]
    public string DefaultTagName { get; set; } = "div";

    /// <summary>The content rendered inside the element.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>The rendered element, under the same rules as <see cref="PartBase{TState}.Element"/>.</summary>
    public ElementReference? Element { get; private set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        throw new NotImplementedException();
    }

    Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem item, object? arg)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
    }
}
