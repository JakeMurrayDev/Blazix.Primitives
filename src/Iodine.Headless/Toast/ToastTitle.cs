using Iodine.Headless.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Iodine.Headless;

// TST §3.6 state object (toast/title/ToastTitle.tsx); TST-STA-26.
/// <summary>Represents the state of a toast's title, which the part exposes as data attributes.</summary>
/// <param name="Type">The toast's type, or <see langword="null"/>.</param>
[PartState]
public sealed partial record ToastTitleState(string? Type);

// toast/title/ToastTitleDataAttributes.ts:5 (TST-DOM-17).
/// <summary>Provides the data attributes a toast's title renders.</summary>
public static class ToastTitleDataAttributes
{
    /// <summary>The toast's type, when it has one.</summary>
    public const string Type = "data-type";
}

// Base UI's Toast.Title (toast/title/ToastTitle.tsx, toast/utils/useToastLabelPart.ts); TST-STR-05, TST-API-15,
// TST-DOM-16, TST-DOM-17, TST-ARIA-04, TST-ARIA-10. The id the Root's `aria-labelledby` names is the consumer's `id`, else a
// generated one (the port's prefixed form, ADR 02 decision 5; TST §16 Q9). It renders only with content: its own, the
// toast's, or a substitute element's (RequiresContent, ADR 02 Amendments 2026-11-18).
/// <summary>
/// Represents a toast's title, which labels its root for assistive technology. Base UI's <c>Toast.Title</c>; it
/// renders an <c>h2</c>, and nothing when it has no content.
/// </summary>
/// <remarks>Its content is <see cref="PartBase{TState}.ChildContent"/>, else the toast's title fragment, else its title text.</remarks>
public sealed class ToastTitle : PartBase<ToastTitleState>
{
    private ToastRootContext? root;
    private ToastLabelBehavior? label;
    private ToastRootSubscription? subscription;
    private RenderFragment? supplied;
    private RenderFragment? resolved;
    private ToastTitleState state = new(null);

    [CascadingParameter]
    private ToastRootContext? RootContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "h2";

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    // ADR 02 Amendments 2026-11-18 (Toast DP11, settled by the orchestrator 2026-11-18): TST-DOM-16's renderable check, a
    // substitute element's own children included.
    /// <inheritdoc />
    protected override bool RequiresContent => true;

    /// <inheritdoc />
    protected override ToastTitleState State
    {
        get
        {
            if (!string.Equals(state.Type, root!.Type, StringComparison.Ordinal))
            {
                state = new ToastTitleState(root.Type);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        root = ToastRootContext.Require(RootContext);
        label = new ToastLabelBehavior(root.TitleId);
        subscription = root.Subscribe(ToastRootAspects.Type | ToastRootAspects.Title, StateHasChanged);
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
        resolved = label!.Content(supplied, root!.TitleContent, root.Title);
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
