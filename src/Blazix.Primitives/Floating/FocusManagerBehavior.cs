using Blazix.Primitives.Interop;
using Blazix.Primitives.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// The C# half of FloatingFocusManager a family's Popup part composes (FLT §4.8; design point 6 of the focus-manager track,
// settled by the orchestrator 2026-10-27: Floating/, composed into the part as ButtonBehavior is, never a nested
// component, ADR 16 rule 10). The `focus-manager` module does the focusing; this behavior:
// - takes the part's inputs on every parameter set and, once attached, sends changed options at once, before the render
//   that applies them (design point 1 (d), chosen by the user 2026-10-27; ADR 01 Amendments 2026-10-27, the commit-path
//   update), so the batch finds this change's Disabled, targets and relay;
// - sends element targets through TwinReferences (ADR 04 Amendments 2026-10-27, element-typed option values); an element
//   the render has not captured yet names nothing, so the module takes the default, and it is sent once captured;
// - renders the inside guards beside the popup while enabled and modal or inside a portal (FLT-DOM-15, FLT-DOM-13; the
//   untrapped typeable combobox's clause is M5's);
// - publishes the manager's state to the enclosing portal while enabled (FLT-FOC-18), from which the portal renders its
//   outside guards (FLT-DOM-14);
// - attaches after the first render that shows the popup, inside a portal once the portal has moved its node (an
//   undisplayed popup cannot take focus; PopupPortalContext.WhenMoved, the Toolbar first-attach precedent);
// - routes the module's focus-out close request to the part and raises Answered for every request, closed or not, so a
//   canceled close re-arms the module (ADR 04 Amendments 2026-10-27);
// - classifies the close type when a close commits, from its snapshot and the type the module last recorded (FLT-FOC-10).
/// <summary>
/// The focus manager of one popup part: its <see cref="FocusManagerTwin"/>, the inside focus guards, the publication to
/// an enclosing portal and the routing of the module's focus-out close.
/// </summary>
internal sealed class FocusManagerBehavior : IAsyncDisposable
{
    private readonly IJSRuntime jsRuntime;
    private readonly Func<Func<Task>, Task> invokeAsync;
    private readonly Func<EventSnapshot, Task> requestClose;
    private readonly TaskCompletionSource attachedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private FocusManagerInputs inputs = new();
    private PopupPortalContext? portal;
    private PortalFocusPublication? published;
    private FocusManagerTwin? twin;
    private TwinHandle<FocusManagerOptions>? handle;
    private FocusManagerOptions? sentOptions;
    private ElementReference[] sentElements = [];
    private string? closeInteractionType;
    private bool attaching;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="FocusManagerBehavior"/> class.</summary>
    /// <param name="jsRuntime">The runtime the twin imports its module through.</param>
    /// <param name="invokeAsync">Runs work on the part's renderer, as <c>ComponentBase.InvokeAsync</c>.</param>
    /// <param name="requestClose">Asks the popup to close with the reason <c>focus-out</c> and the snapshot of the event.</param>
    public FocusManagerBehavior(IJSRuntime jsRuntime, Func<Func<Task>, Task> invokeAsync, Func<EventSnapshot, Task> requestClose)
    {
        ArgumentNullException.ThrowIfNull(jsRuntime);
        ArgumentNullException.ThrowIfNull(invokeAsync);
        ArgumentNullException.ThrowIfNull(requestClose);
        this.jsRuntime = jsRuntime;
        this.invokeAsync = invokeAsync;
        this.requestClose = requestClose;
    }

    /// <summary>Gets or sets the context of the portal the popup is rendered inside.</summary>
    /// <value>The cascaded context, or <see langword="null"/> outside any portal. Set it before <see cref="SetInputs"/>.</value>
    public PopupPortalContext? Portal
    {
        get => portal;
        set
        {
            if (ReferenceEquals(value, portal))
            {
                return;
            }

            if (published is not null)
            {
                published = null;
                portal?.Publish(null);
            }

            portal = value;
            Publish();
        }
    }

    /// <summary>Gets a value indicating whether the inside focus guards render beside the popup.</summary>
    /// <value><see langword="true"/> while the manager is enabled and modal or inside a portal.</value>
    public bool RendersInsideGuards => !inputs.Disabled && (inputs.Modal || portal is not null);

    /// <summary>Gets the interaction type the module last recorded while the popup was open.</summary>
    /// <value>The type; <see cref="InteractionType.Unknown"/> after an open reset it; <see langword="null"/> before any report.</value>
    public InteractionType? RecordedInteractionType { get; private set; }

    /// <summary>Gets how many focus-out close requests the behavior has routed, closed or not.</summary>
    /// <value>The count; the module asks again for a focus-out close once it moves.</value>
    public int Answered { get; private set; }

    /// <summary>Gets a task that completes when the behavior is first attached to the popup's element.</summary>
    /// <value>A task that completes after the first interactive render that showed the popup, inside a portal once it moved.</value>
    public Task TwinAttached => attachedSource.Task;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (published is not null)
        {
            published = null;
            portal?.Publish(null);
        }

        handle = null;
        if (twin is not null)
        {
            FocusManagerTwin previous = twin;
            twin = null;
            await previous.DisposeAsync();
        }
    }

    /// <summary>Sets the inputs of this parameter set; changed options are sent at once to an attached module.</summary>
    /// <param name="next">The inputs.</param>
    /// <remarks>Call it from the part's <c>OnParametersSet</c>, after <see cref="Portal"/>.</remarks>
    public void SetInputs(FocusManagerInputs next)
    {
        ArgumentNullException.ThrowIfNull(next);
        if (disposed || next.Equals(inputs))
        {
            return;
        }

        if (!Equals(next.CloseEvent, inputs.CloseEvent))
        {
            // FLT-FOC-10: classified when the close commits, with the type recorded until then.
            closeInteractionType = next.CloseEvent is null
                ? null
                : InteractionTypeLiterals.ToLiteral(CloseTypeClassifier.Classify(next.CloseEvent, RecordedInteractionType));
        }

        inputs = next;
        Publish();
        if (handle is not null)
        {
            _ = PushAsync();
        }
    }

    /// <summary>Renders the inside guard before the popup while the guards render.</summary>
    /// <param name="builder">The part's render tree builder, inside the pipeline's region.</param>
    public void BuildBeforeElement(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (RendersInsideGuards)
        {
            RenderGuard(builder);
        }
    }

    /// <summary>Renders the inside guard after the popup while the guards render.</summary>
    /// <param name="builder">The part's render tree builder, inside the pipeline's region.</param>
    public void BuildAfterElement(RenderTreeBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (RendersInsideGuards)
        {
            RenderGuard(builder);
        }
    }

    /// <summary>Attaches the module after the first render that showed the popup, then sends options that changed.</summary>
    /// <param name="element">The popup's element, or <see langword="null"/> under a substitute callback.</param>
    /// <param name="id">The popup's id, which the twin attaches by under a substitute callback.</param>
    /// <returns>A task that completes when the module has the options.</returns>
    /// <remarks>Call it from the part's <c>OnAfterRenderAsync</c>.</remarks>
    public async Task AfterRenderAsync(ElementReference? element, string? id)
    {
        string? key = element?.Id ?? id;
        if (disposed || key is null)
        {
            return;
        }

        if (handle is not null && !string.Equals(handle.Key, key, StringComparison.Ordinal))
        {
            // The element was replaced: the module let go of the old one.
            TwinHandle<FocusManagerOptions> previous = handle;
            handle = null;
            sentOptions = null;
            sentElements = [];
            await previous.DisposeAsync();
        }

        if (handle is not null)
        {
            // An element target the render captured just now.
            await PushAsync();
            return;
        }

        if (attaching)
        {
            return;
        }

        attaching = true;
        await PopupPortalContext.WhenMoved(portal);
        if (disposed)
        {
            return;
        }

        twin ??= CreateTwin();
        (FocusManagerOptions options, TwinReferences? references, ElementReference[] elements) = Build();
        TwinHandle<FocusManagerOptions> attached = element is { } reference
            ? await twin.AttachAsync(reference, options, references)
            : await twin.AttachAsync(id!, options, references);
        attaching = false;
        if (disposed)
        {
            return;
        }

        handle = attached;
        sentOptions = options;
        sentElements = elements;
        attachedSource.TrySetResult();
        await PushAsync();
    }

    // FLT-DOM-13: the guard renders in a region of its own, so both guards share its sequence numbers.
    private static void RenderGuard(RenderTreeBuilder builder)
    {
        builder.OpenRegion(0);
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "tabindex", "0");
        builder.AddAttribute(2, PopupMarkerAttributes.FocusGuard, string.Empty);
        builder.AddAttribute(3, "data-type", "inside");
        builder.AddAttribute(4, "aria-hidden", "true");
        builder.AddAttribute(5, "style", VisuallyHidden.Style);
        builder.CloseElement();
        builder.CloseRegion();
    }

    // An element reference is captured by the render that creates the element; before that it names nothing.
    private static bool IsCaptured(ElementReference element) => element.Id is not null;

    private static bool SameElements(ElementReference[] left, ElementReference[] right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (int i = 0; i < left.Length; i++)
        {
            if (!FocusManagerInputs.SameElement(left[i], right[i]))
            {
                return false;
            }
        }

        return true;
    }

    private FocusManagerTwin CreateTwin()
    {
        FocusManagerTwin created = new(jsRuntime);
        created.FocusOutCloseRequested += request => _ = invokeAsync(() => RouteFocusOutCloseAsync(request));
        created.InteractionTypeChanged += type => RecordedInteractionType = InteractionTypeLiterals.FromLiteral(type);
        return created;
    }

    // FLT-FOC-18: { modal, open, closeOnFocusOut } while enabled; nothing while disabled.
    private void Publish()
    {
        if (portal is null)
        {
            return;
        }

        if (disposed || inputs.Disabled)
        {
            if (published is not null)
            {
                published = null;
                portal.Publish(null);
            }

            return;
        }

        if (published is { } current && current.Modal == inputs.Modal && current.Open == inputs.Open && current.CloseOnFocusOut == inputs.CloseOnFocusOut)
        {
            return;
        }

        published = new PortalFocusPublication(inputs.Modal, inputs.Open, inputs.CloseOnFocusOut);
        portal.Publish(published);
    }

    private async Task RouteFocusOutCloseAsync(FocusOutCloseRequest request)
    {
        if (disposed)
        {
            return;
        }

        Answered++;
        await requestClose(request.Event);
        await PushAsync();
    }

    private async Task PushAsync()
    {
        if (handle is null || disposed)
        {
            return;
        }

        (FocusManagerOptions options, TwinReferences? references, ElementReference[] elements) = Build();
        if (options.Equals(sentOptions) && SameElements(elements, sentElements))
        {
            return;
        }

        sentOptions = options;
        sentElements = elements;
        await handle.UpdateAsync(options, references);
    }

    // The options of an attach or an update, the references they name by index, and those elements for comparison.
    private (FocusManagerOptions Options, TwinReferences? References, ElementReference[] Elements) Build()
    {
        List<ElementReference>? elements = null;
        int? Add(ElementReference? element)
        {
            if (element is not { } captured || !IsCaptured(captured))
            {
                return null;
            }

            elements ??= [];
            elements.Add(captured);
            return elements.Count - 1;
        }

        (bool initialFocus, int? initialElement, string? initialId) = Target(inputs.InitialFocus, Add);
        (bool returnFocus, int? returnElement, string? returnId) = Target(inputs.ReturnFocus, Add);
        int? previous = Add(inputs.PreviousFocusableElement);
        int? next = Add(inputs.NextFocusableElement);
        List<int>? inside = null;
        if (inputs.InsideElements is { Count: > 0 } insideElements)
        {
            foreach (ElementReference element in insideElements)
            {
                if (Add(element) is int index)
                {
                    (inside ??= []).Add(index);
                }
            }
        }

        FocusManagerOptions options = new()
        {
            Modal = inputs.Modal,
            Disabled = inputs.Disabled,
            InitialFocus = initialFocus,
            InitialFocusElement = initialElement,
            InitialFocusId = initialId,
            ReturnFocus = returnFocus,
            ReturnFocusElement = returnElement,
            ReturnFocusId = returnId,
            ExplicitReturnFocus = inputs.ExplicitReturnFocus,
            CloseInteractionType = closeInteractionType,
            RestoreFocus = inputs.RestoreFocus,
            CloseOnFocusOut = inputs.CloseOnFocusOut,
            OpenInteractionType = inputs.OpenInteractionType is { } type ? InteractionTypeLiterals.ToLiteral(type) : null,
            OpenSelector = inputs.OpenSelector,
            TreatPopupAsFloatingElement = inputs.TreatPopupAsFloatingElement,
            DomReferenceId = inputs.DomReferenceId,
            TriggerFocusGuards = inputs.TriggerFocusGuards,
            PreviousFocusableElement = previous,
            NextFocusableElement = next,
            InsideElements = inside,
            InsideElementIds = inputs.InsideElementIds,
            Relay = inputs.Relay,
            Answered = Answered,
        };
        if (elements is null)
        {
            return (options, null, []);
        }

        TwinReferences references = new();
        foreach (ElementReference element in elements)
        {
            references.Add(element);
        }

        return (options, references, [.. elements]);
    }

    // FLT-API-05: false moves no focus; true, an uncaptured element or no value takes the default target.
    private static (bool Focus, int? Element, string? Id) Target(FocusTargetValue? value, Func<ElementReference?, int?> add)
    {
        if (value is not { } target)
        {
            return (true, null, null);
        }

        if (target.TryGetValue(out bool focus))
        {
            return (focus, null, null);
        }

        if (target.TryGetValue(out ElementReference element))
        {
            return (true, add(element), null);
        }

        return target.TryGetValue(out string? id) ? (true, null, id) : (true, null, null);
    }
}
