using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives;

// TST §3.7 state object (toast/description/ToastDescription.tsx); TST-STA-26.
/// <summary>Represents the state of a toast's description, which the part exposes as data attributes.</summary>
/// <param name="Type">The toast's type, or <see langword="null"/>.</param>
[PartState]
public sealed partial record ToastDescriptionState(string? Type);

// toast/description/ToastDescriptionDataAttributes.ts:5 (TST-DOM-17).
/// <summary>Provides the data attributes a toast's description renders.</summary>
public static class ToastDescriptionDataAttributes
{
    /// <summary>The toast's type, when it has one.</summary>
    public const string Type = "data-type";
}

// Base UI's Toast.Description (toast/description/ToastDescription.tsx, toast/utils/useToastLabelPart.ts); TST-STR-06, TST-API-15,
// TST-DOM-16, TST-DOM-17, TST-ARIA-05, TST-ARIA-10. The id the Root's `aria-describedby` names is the consumer's `id`, else a
// generated one (the port's prefixed form, ADR 02 decision 5; TST §16 Q9). It renders only with content: its own, the
// toast's, or a substitute element's (RequiresContent, ADR 02 Amendments 2026-11-18).
/// <summary>
/// Represents a toast's description, which labels its root for assistive technology. Base UI's <c>Toast.Description</c>; it
/// renders a <c>p</c>, and nothing when it has no content.
/// </summary>
/// <remarks>Its content is <see cref="PartBase{TState}.ChildContent"/>, else the toast's description fragment, else its description text.</remarks>
public sealed class ToastDescription : PartBase<ToastDescriptionState>
{
    private ToastRootContext? root;
    private ToastLabelBehavior? label;
    private ToastRootSubscription? subscription;
    private RenderFragment? supplied;
    private RenderFragment? resolved;
    private ToastDescriptionState state = new(null);

    [CascadingParameter]
    private ToastRootContext? RootContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "p";

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    // ADR 02 Amendments 2026-11-18 (Toast DP11, settled by the orchestrator 2026-11-18): TST-DOM-16's renderable check, a
    // substitute element's own children included.
    /// <inheritdoc />
    protected override bool RequiresContent => true;

    /// <inheritdoc />
    protected override ToastDescriptionState State
    {
        get
        {
            if (!string.Equals(state.Type, root!.Type, StringComparison.Ordinal))
            {
                state = new ToastDescriptionState(root.Type);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        root = ToastRootContext.Require(RootContext);
        label = new ToastLabelBehavior(root.DescriptionId);
        subscription = root.Subscribe(ToastRootAspects.Type | ToastRootAspects.Description, StateHasChanged);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // A parameter set that does not supply ChildContent leaves this part's resolved content in it.
        supplied = resolved is not null && ReferenceEquals(ChildContent, resolved) ? supplied : ChildContent;
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        subscription!.Rendered();
        resolved = label!.Content(supplied, root!.DescriptionContent, root.Description);
        ChildContent = resolved;
        // The pipeline renders nothing without content unless a substitute element brings children of its own, and always
        // renders a substitute callback (RequiresContent). Only the substitute element's case is known after the render.
        if (resolved is not null || RenderFunction is not null)
        {
            label.Publish(true, Id);
        }
        else if (Render is null)
        {
            label.Publish(false, Id);
        }

        base.BuildRenderTree(builder);
    }

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        if (resolved is null && RenderFunction is null && Render is not null)
        {
            label!.Publish(Element is not null, Id);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            subscription?.Dispose();
            subscription = null;
            label?.Withdraw();
        }

        base.Dispose(disposing);
    }
}
