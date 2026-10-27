using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// The C# half of FloatingPortal and FloatingPortalLite (ADR 04 Amendments 2026-10-27, decision 5, chosen by the user
// 2026-10-27; FLT-API-13, FLT-DOM-11..14, FLT-ARIA-04/05, PSH-STR-11/12), composed by the families' Portal parts and the
// test-only portals (ADR 16 rule 10). The part's host is the portal node `<div id data-base-ui-portal>` with the consumer's
// attributes and Render (FLT-DOM-11, PSH-STR-12); it opts into the pipeline's host wrapper (`display: contents`, ADR 02
// Amendments 2026-10-27, the host wrapper settled by the orchestrator 2026-10-27), so the node is the wrapper's only child
// and the outside guards and the owner, rendered through BuildBeforeElement and BuildAfterElement, are its siblings.
// - The node renders `display: none` (after the consumer's style) until the module moves it, and that string is frozen
//   from the first render: the module owns `style` from attach (ADR 04 Amendments 2026-10-18), and a prerendered
//   `defaultOpen` popup's node stays hidden in place until then (ADR 04 §5).
// - The container (PortalContainerValue): an element or a script-held node crosses as a reference, a selector as a
//   string; not supplied resolves to the enclosing portal's node, else the body; Pending renders no node (the part checks
//   RendersNode), so the module never sees it.
// - The guards and the owner render while the context holds a non-modal, open publication and the node exists (FLT-DOM-14),
//   never in the lite portal. The guards' focus routing is track 4's (design point 5).
// - The move completes the context's Moved task (design point 3, settled by the orchestrator 2026-10-27), and only the
//   attach that moved the node completes it: TwinBase hands a repeated attach the pending handle at once, so a render
//   during the first attach awaits that attach instead of attaching again.
// - The options are built only for an attach or when their inputs changed (the container, the enclosing node it resolves
//   to, the lite flag, the consumer's style; ADR 16 rule 7, the Collapsible precedent), so a render that changes nothing
//   builds nothing and sends nothing, and the container crosses through TwinReferences, its one boxing site.
/// <summary>Renders a portal's wrapper, guards and owner, and moves its node through the <c>portal</c> module.</summary>
internal sealed class PopupPortalBehavior : IAsyncDisposable
{
    private const string WrapperStyle = "display: contents";
    private const string Hidden = "display: none";

    private readonly PortalTwin twin;
    private readonly Action requestRender;
    private readonly IDisposable publicationSubscription;
    private TwinHandle<PortalOptions>? handle;
    private Task? attaching;
    private int generation;
    private ElementReference? attachedNode;
    private bool built;
    private PortalContainerValue? builtContainer;
    private ElementReference? builtEnclosing;
    private bool builtLite;
    private string? builtStyle;
    private string? consumerStyle;
    private string? frozenStyle;
    private RenderFragment? cascade;
    private RenderFragment? cascadedContent;

    /// <summary>Initializes a new instance of the <see cref="PopupPortalBehavior"/> class.</summary>
    /// <param name="jsRuntime">The runtime of the part's circuit or WebAssembly application.</param>
    /// <param name="requestRender">Asks the part to render, its <c>StateHasChanged</c>, when the publication changes.</param>
    public PopupPortalBehavior(IJSRuntime jsRuntime, Action requestRender)
    {
        ArgumentNullException.ThrowIfNull(requestRender);
        this.requestRender = requestRender;
        twin = new PortalTwin(jsRuntime);
        publicationSubscription = Context.OnPublicationChanged(requestRender);
    }

    // FLT-DOM-14.
    private bool RendersGuards => !Lite && Context.Node is not null && Context.Publication is { Modal: false, Open: true };

    /// <summary>Gets the context the portal cascades to the parts inside its node.</summary>
    public PopupPortalContext Context { get; } = new();

    /// <summary>Gets or sets the context of the portal this one is rendered inside, or <see langword="null"/>.</summary>
    public PopupPortalContext? Enclosing { get; set; }

    /// <summary>Gets or sets a value indicating whether this is the lite portal, with no outside guards and no owner.</summary>
    public bool Lite { get; set; }

    /// <summary>Gets or sets the role of the <c>aria-owns</c> owner, or <see langword="null"/> for none (Menu passes one).</summary>
    public string? OwnerRole { get; set; }

    /// <summary>Gets or sets the id the portal node renders.</summary>
    public string? NodeId { get; set; }

    /// <summary>Attaches the module to the node after the render that created it, follows its container, or detaches.</summary>
    /// <param name="node">The node after this render, or <see langword="null"/> when none was rendered.</param>
    /// <param name="container">The part's <c>Container</c>.</param>
    /// <returns>A task that completes when the module has moved the node or has the new container.</returns>
    /// <remarks>Call it from the part's <c>OnAfterRenderAsync</c>.</remarks>
    public Task OnAfterRenderAsync(ElementReference? node, PortalContainerValue? container)
    {
        Context.NodeId = NodeId;
        if (node is null || !RendersNode(container))
        {
            return DetachNodeAsync();
        }

        // FLT-API-13: not supplied resolves to the enclosing portal's node.
        ElementReference? enclosing = container is null ? Enclosing?.Node : null;
        if (handle is not null && attachedNode is { } attached && SameElement(attached, node.Value) && SameInputs(container, enclosing))
        {
            // ADR 16 rule 7: nothing the module reads changed, so nothing is built or sent, and no async work starts.
            return Task.CompletedTask;
        }

        return SyncNodeAsync(node.Value, container, enclosing);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        publicationSubscription.Dispose();
        Context.CompleteMove();
        await DetachAsync();
        await twin.DisposeAsync();
    }

    // FLT-API-13: a pending container renders no node.
    /// <summary>Determines whether the portal renders its node for <paramref name="container"/>.</summary>
    /// <param name="container">The part's <c>Container</c>.</param>
    /// <returns><see langword="false"/> for the pending case; otherwise, <see langword="true"/>.</returns>
    public static bool RendersNode(PortalContainerValue? container) => container is not { IsPending: true };

    // The cascade wraps the consumer's content inside the node; it is rebuilt only when the consumer supplied other content
    // (the TabsRoot and Meter precedent).
    /// <summary>Gets the content the node renders: the consumer's, with <see cref="Context"/> cascaded to it.</summary>
    /// <param name="childContent">The part's <c>ChildContent</c> after this parameter set.</param>
    /// <returns>The fragment the part renders as its <c>ChildContent</c>.</returns>
    public RenderFragment CascadeContent(RenderFragment? childContent)
    {
        if (cascade is null || (!ReferenceEquals(childContent, cascade) && !ReferenceEquals(cascadedContent, childContent)))
        {
            cascadedContent = childContent;
            cascade = Context.Cascade(cascadedContent);
        }

        return cascade;
    }

    /// <summary>Adds the wrapper's attributes.</summary>
    /// <param name="attributes">The wrapper's attributes.</param>
    public static void AddWrapperAttributes(AttributeList attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        attributes.Set("style", WrapperStyle);
    }

    /// <summary>Hides the node until it moves and freezes its style from then on, keeping the consumer's for the module.</summary>
    /// <param name="attributes">The node's merged attributes.</param>
    public void FinishAttributes(AttributeList attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        consumerStyle = attributes.TryGetValue(MergeProps.StyleAttribute, out AttributeValue merged) && merged.TryGetValue(out string style) && style.Length > 0 ? style : null;
        frozenStyle ??= consumerStyle is null ? Hidden : consumerStyle.TrimEnd().TrimEnd(';') + "; " + Hidden;
        attributes.Set(MergeProps.StyleAttribute, frozenStyle);
    }

    // FLT-DOM-13/14, FLT-ARIA-04, PSH-DOM-13: the before-outside guard, then the owner.
    /// <summary>Renders the before-outside guard and the <c>aria-owns</c> owner while the guards render.</summary>
    /// <param name="builder">The part's render tree builder, inside the pipeline's region.</param>
    public void BuildBeforeElement(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (!RendersGuards)
        {
            return;
        }

        RenderGuard(builder);
        builder.OpenElement(10, "span");
        if (OwnerRole is not null)
        {
            builder.AddAttribute(11, "role", OwnerRole);
        }

        builder.AddAttribute(12, "aria-owns", NodeId);
        builder.AddAttribute(13, "style", VisuallyHidden.OwnerStyle);
        builder.CloseElement();
    }

    /// <summary>Renders the after-outside guard while the guards render.</summary>
    /// <param name="builder">The part's render tree builder, inside the pipeline's region.</param>
    public void BuildAfterElement(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (RendersGuards)
        {
            RenderGuard(builder);
        }
    }

    // FLT-DOM-13: the guard renders in a region of its own, so both guards share its sequence numbers.
    private static void RenderGuard(RenderTreeBuilder builder)
    {
        builder.OpenRegion(0);
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "tabindex", "0");
        builder.AddAttribute(2, PopupMarkerAttributes.FocusGuard, string.Empty);
        builder.AddAttribute(3, "data-type", "outside");
        builder.AddAttribute(4, "aria-hidden", "true");
        builder.AddAttribute(5, "style", VisuallyHidden.Style);
        builder.CloseElement();
        builder.CloseRegion();
    }

    private static bool SameElement(ElementReference left, ElementReference right) =>
        string.Equals(left.Id, right.Id, StringComparison.Ordinal) && ReferenceEquals(left.Context, right.Context);

    private static bool SameElement(ElementReference? left, ElementReference? right) =>
        left is { } l ? right is { } r && SameElement(l, r) : right is null;

    private static bool SameContainer(PortalContainerValue? left, PortalContainerValue? right)
    {
        if (left is not { } l || right is not { } r)
        {
            return left is null && right is null;
        }

        if (l.TryGetValue(out ElementReference leftElement))
        {
            return r.TryGetValue(out ElementReference rightElement) && SameElement(leftElement, rightElement);
        }

        if (l.TryGetValue(out IJSObjectReference? leftNode))
        {
            return r.TryGetValue(out IJSObjectReference? rightNode) && ReferenceEquals(leftNode, rightNode);
        }

        return l.TryGetValue(out string? leftSelector) && r.TryGetValue(out string? rightSelector) && string.Equals(leftSelector, rightSelector, StringComparison.Ordinal);
    }

    private bool SameInputs(PortalContainerValue? container, ElementReference? enclosing) =>
        built
        && builtLite == Lite
        && string.Equals(builtStyle, consumerStyle, StringComparison.Ordinal)
        && SameContainer(builtContainer, container)
        && SameElement(builtEnclosing, enclosing);

    // The options of an attach or an update, and the inputs they were built from.
    private (PortalOptions Options, TwinReferences? References) Build(PortalContainerValue? container, ElementReference? enclosing)
    {
        built = true;
        builtContainer = container;
        builtEnclosing = enclosing;
        builtLite = Lite;
        builtStyle = consumerStyle;
        TwinReferences? references = null;
        string? selector = null;
        if (container is { } value)
        {
            if (value.TryGetValue(out ElementReference element))
            {
                references = new();
                references.Add(element);
            }
            else if (value.TryGetValue(out IJSObjectReference? node))
            {
                references = new();
                references.Add(node);
            }
            else if (value.TryGetValue(out string? css))
            {
                selector = css;
            }
        }
        else if (enclosing is { } enclosingNode)
        {
            references = new();
            references.Add(enclosingNode);
        }

        return (new PortalOptions { ContainerRef = references is null ? null : 0, ContainerSelector = selector, Lite = Lite, Style = consumerStyle }, references);
    }

    private async Task DetachNodeAsync()
    {
        Context.Node = null;
        frozenStyle = null;
        Context.ResetMove();
        await DetachAsync();
    }

    private async Task SyncNodeAsync(ElementReference node, PortalContainerValue? container, ElementReference? enclosing)
    {
        if (attachedNode is { } attached && !SameElement(attached, node))
        {
            await DetachAsync();
            Context.ResetMove();
        }

        bool guardsBefore = RendersGuards;
        Context.Node = node;
        if (handle is null)
        {
            if (attaching is null)
            {
                (PortalOptions options, TwinReferences? references) = Build(container, enclosing);
                attaching = AttachAsync(node, options, references);
            }

            await attaching;
        }

        if (handle is not null && !SameInputs(container, enclosing))
        {
            (PortalOptions options, TwinReferences? references) = Build(container, enclosing);
            await handle.UpdateAsync(options, references);
        }

        if (!guardsBefore && RendersGuards)
        {
            // The node exists only from this render on, so a publication made before it shows its guards now.
            requestRender();
        }
    }

    private async Task AttachAsync(ElementReference node, PortalOptions options, TwinReferences? references)
    {
        int attachedGeneration = generation;
        attachedNode = node;
        TwinHandle<PortalOptions> attached = await twin.AttachAsync(node, options, references);
        if (attachedGeneration != generation)
        {
            // The node went while the module loaded: the handle is not this portal's any more.
            await attached.DisposeAsync();
            return;
        }

        handle = attached;
        attaching = null;
        Context.CompleteMove();
    }

    private async Task DetachAsync()
    {
        generation++;
        attaching = null;
        attachedNode = null;
        built = false;
        if (handle is not null)
        {
            TwinHandle<PortalOptions> previous = handle;
            handle = null;
            await previous.DisposeAsync();
        }
    }
}
