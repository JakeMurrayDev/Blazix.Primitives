using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// Base UI's NumberField.ScrubAreaCursor (number-field/scrub-area-cursor/NumberFieldScrubAreaCursor.tsx); the contract is
// docs/components/number-field.md §3.3 and §5. A `span` with `role="presentation"` and the inline `position: fixed; top: 0;
// left: 0; pointer-events: none` under the consumer's style (NUM-DOM-13), rendered only while its scrub area is scrubbed
// by a pointer that can have a virtual cursor (neither touch nor WebKit) and the pointer lock was not denied (NUM-STR-06),
// in a body portal (NUM-DOM-14). On Server it appears a round trip after the press (design point 8 (a), settled by the
// orchestrator 2026-11-18; ADR 01's rows for NUM-STR-06, NUM-DOM-13/14).
// - The `number-field` module writes its `transform` in the `'component'` band (ADR 04 Amendments 2026-11-18); C# merges
//   the base declarations under the consumer's style and sends the result as the consumer string, rendering it until the
//   attach and freezing it from then on (the handover, ADR 04 Amendments 2026-10-18; the ScrollArea precedent).
// - Each element is a new handle: the cursor leaves the DOM when a scrub ends, so the next scrub attaches again.
/// <summary>
/// Represents the virtual cursor shown while a number field scrub area is scrubbed: Base UI's
/// <c>NumberField.ScrubAreaCursor</c>, rendered as a <c>span</c> in the document body. It must be placed within a
/// <see cref="NumberFieldScrubArea"/>.
/// </summary>
public sealed class NumberFieldScrubAreaCursor : PartBase<NumberFieldScrubAreaCursorState>
{
    private const string DefaultTag = "span";

    // NumberFieldScrubAreaCursor.tsx:13-18.
    private const string PartStyle = "position: fixed; top: 0; left: 0; pointer-events: none";

    private static readonly StateAttributesMapping<NumberFieldScrubAreaCursorState> s_mapping = FieldValidityMapping.Create<NumberFieldScrubAreaCursorState>();

    private readonly Action stateChanged;
    private readonly RenderFragment renderElement;
    private NumberFieldRootContext? context;
    private NumberFieldScrubAreaContext? scrubContext;
    private NumberFieldScrubTwin? twin;
    private TwinHandle<NumberFieldScrubOptions>? handle;
    private Task? attaching;
    private bool rendering;
    private bool frozen;
    private string? frozenStyle;
    private string? renderedStyle;
    private string? sentStyle;
    private string? style;
    private NumberFieldScrubAreaCursorState? state;
    private int stateVersion = -1;

    /// <summary>Initializes a new instance of the <see cref="NumberFieldScrubAreaCursor"/> class.</summary>
    public NumberFieldScrubAreaCursor()
    {
        stateChanged = StateHasChanged;
        renderElement = base.BuildRenderTree;
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private NumberFieldRootContext? RootContext { get; set; }

    [CascadingParameter]
    private NumberFieldScrubAreaContext? ScrubContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override NumberFieldScrubAreaCursorState State
    {
        get
        {
            // NUM-STA-02: the root's state, rebuilt only when the root made a new one.
            if (state is null || stateVersion != context!.StateVersion)
            {
                NumberFieldRootState root = context!.State;
                stateVersion = context.StateVersion;
                state = new NumberFieldScrubAreaCursorState(root.Value, root.InputValue, root.Required, root.Disabled, root.ReadOnly, root.Scrubbing, root.Touched, root.Dirty, root.Valid, root.Filled, root.Focused);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<NumberFieldScrubAreaCursorState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// The cursor is not placed within a <see cref="NumberFieldRoot"/>, or not within a <see cref="NumberFieldScrubArea"/>.
    /// </exception>
    protected override void OnInitialized()
    {
        context = NumberFieldRootContext.Require(RootContext);
        scrubContext = NumberFieldScrubAreaContext.Require(ScrubContext);
        context.AddListener(stateChanged);
        scrubContext.SetListener(stateChanged);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        // NUM-STR-06 (NumberFieldScrubAreaCursor.tsx:30-31): nothing at all unless these hold.
        rendering = scrubContext!.IsScrubbing && scrubContext.CursorAllowed && !scrubContext.IsPointerLockDenied;
        if (!rendering)
        {
            return;
        }

        builder.OpenComponent<NumberFieldCursorPortal>(0);
        builder.AddComponentParameter(1, nameof(ChildContent), renderElement);
        builder.CloseComponent();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!rendering || Element is not { } element)
        {
            // NUM-DOM-14: the element left the DOM; the next scrub's element is a new handle.
            await ReleaseAsync();
            return;
        }

        if (handle is null)
        {
            if (attaching is null)
            {
                frozen = true;
                frozenStyle = renderedStyle;
                style = sentStyle;
                attaching = AttachAsync(element);
            }

            await attaching;
            return;
        }

        if (!string.Equals(style, sentStyle, StringComparison.Ordinal))
        {
            style = sentStyle;
            await handle.UpdateAsync(new NumberFieldScrubOptions { Part = NumberFieldScrubTwin.CursorPart, Style = style });
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            context?.RemoveListener(stateChanged);
            scrubContext?.SetListener(null);
            if (twin is not null)
            {
                _ = twin.DisposeAsync().AsTask();
                twin = null;
                handle = null;
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("role", "presentation");
    }

    // The handover: C#'s declarations under the consumer's, rendered until the attach and frozen from then on.
    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes)
    {
        string? consumer = attributes.TryGetValue(MergeProps.StyleAttribute, out AttributeValue value) && value.TryGetValue(out string text) ? text : null;
        sentStyle = MergeProps.MergeStyles(PartStyle, consumer);
        string? shown = frozen ? frozenStyle : sentStyle;
        renderedStyle = sentStyle;
        if (shown is null)
        {
            attributes.Remove(MergeProps.StyleAttribute);
        }
        else
        {
            attributes.Set(MergeProps.StyleAttribute, shown);
        }
    }

    private async Task AttachAsync(ElementReference element)
    {
        // Attached beside the portal's move, not after it: the module centres the cursor again once it is laid out.
        if (!rendering || scrubContext!.Element is not { } area)
        {
            attaching = null;
            frozen = false;
            return;
        }

        twin ??= new NumberFieldScrubTwin(JSRuntime);
        TwinReferences references = new();
        references.Add(area);
        handle = await twin.AttachAsync(element, new NumberFieldScrubOptions { Part = NumberFieldScrubTwin.CursorPart, Style = style }, references);
    }

    private async Task ReleaseAsync()
    {
        TwinHandle<NumberFieldScrubOptions>? released = handle;
        handle = null;
        attaching = null;
        frozen = false;
        frozenStyle = null;
        if (released is not null)
        {
            await released.DisposeAsync();
        }
    }
}
