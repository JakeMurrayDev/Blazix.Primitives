using System.Globalization;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Blazix.Primitives.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives;

// TST §3.3 state object (toast/viewport/ToastViewport.tsx:235-237); TST-STA-26.
/// <summary>Represents the state of a toast viewport, which the part exposes as data attributes.</summary>
/// <param name="Expanded">A value indicating whether the stack of toasts is expanded: hovered, or focused by keyboard.</param>
[PartState]
public sealed partial record ToastViewportState(bool Expanded);

// toast/viewport/ToastViewportDataAttributes.ts:5 (TST-DOM-02).
/// <summary>Provides the data attributes a toast viewport renders.</summary>
public static class ToastViewportDataAttributes
{
    /// <summary>Present while the stack of toasts is expanded.</summary>
    public const string Expanded = "data-expanded";
}

// toast/viewport/ToastViewportCssVars.ts:5 (TST-DOM-03).
/// <summary>Provides the CSS variables a toast viewport sets.</summary>
public static class ToastViewportCssVars
{
    /// <summary>The height of the first toast in the list, in pixels, once it is measured and while it is not closing.</summary>
    public const string FrontmostHeight = "--toast-frontmost-height";
}

// Base UI's Toast.Viewport (toast/viewport/ToastViewport.tsx); TST-STR-02, TST-STR-12, TST-DOM-01..05, TST-DOM-23,
// TST-ARIA-01, TST-A11Y-01, TST-A11Y-03. C# renders the host's role, live-region and label attributes, `data-expanded`
// (the M6 plan's decision 19 (a) keeps its C# writer) and `--toast-frontmost-height` (TST §16 Q2 replicated: the first
// toast's height, ending or not); the part carries no JS-owned style. The alert region for high-priority toasts follows
// the host (BuildAfterElement, ADR 02 Amendments 2026-10-22), ending toasts included (§16 Q7 replicated). The focus guards
// and the Viewport's listeners are the `toast` module's (ADR 04 Amendments 2026-11-18; M6 track 9b): no C# mouse, focus
// or key handler here (ADR 16 rule 6).
// The toast list is a template parameter beside the base's ChildContent: PartBase already declares
// `ChildContent : RenderFragment?`, so a `RenderFragment<…>` of that name throws at the first render (M6 track 9a, the DP5
// fact, pending the user).
/// <summary>
/// Represents the region that holds the toasts: a landmark and a polite live region that announces added toasts. Base
/// UI's <c>Toast.Viewport</c>; it renders a <c>div</c>.
/// </summary>
/// <typeparam name="TData">The type of the custom data the toasts carry.</typeparam>
/// <remarks>
/// Render the toasts with <see cref="ToastsContent"/>, which receives the toasts, newest first, and is rendered again
/// whenever they change.
/// </remarks>
public sealed class ToastViewport<TData> : PartBase<ToastViewportState>
{
    private ToastProviderContext<TData>? provider;
    private StoreSelection<ToastStoreState<TData>, IReadOnlyList<ToastObject<TData>>>? toasts;
    private StoreSelection<ToastStoreState<TData>, bool>? expanded;
    private StoreSelection<ToastStoreState<TData>, bool>? focused;
    private RenderFragment? supplied;
    private RenderFragment? listContent;
    private IReadOnlyList<ToastObject<TData>>? listFor;
    private RenderFragment<IReadOnlyList<ToastObject<TData>>>? listTemplate;
    private ToastViewportState state = new(false);

    [CascadingParameter]
    private ToastProviderContext<TData>? ProviderContext { get; set; }

    /// <summary>Gets or sets the template that renders the toasts.</summary>
    /// <value>
    /// The template, given the toasts, newest first; or <see langword="null"/> to render
    /// <see cref="PartBase{TState}.ChildContent"/> instead.
    /// </value>
    [Parameter]
    public RenderFragment<IReadOnlyList<ToastObject<TData>>>? ToastsContent { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override ToastViewportState State
    {
        get
        {
            if (state.Expanded != expanded!.Value)
            {
                state = new ToastViewportState(expanded.Value);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        provider = ToastProviderContext<TData>.Require(ProviderContext);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // A parameter set that does not supply ChildContent leaves this part's list content in it.
        supplied = listContent is not null && ReferenceEquals(ChildContent, listContent) ? supplied : ChildContent;
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (ToastsContent is { } template)
        {
            IReadOnlyList<ToastObject<TData>> list = toasts!.Value;
            if (listContent is null || !ReferenceEquals(listFor, list) || !ReferenceEquals(listTemplate, template))
            {
                listFor = list;
                listTemplate = template;
                listContent = template(list);
            }

            ChildContent = listContent;
        }
        else
        {
            ChildContent = supplied;
        }

        base.BuildRenderTree(builder);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors()
    {
        SelectorSubscription<ToastStoreState<TData>> subscription = SubscribeTo(provider!.Store);
        toasts = subscription.Select(ToastSelectors<TData>.Toasts);
        expanded = subscription.Select(ToastSelectors<TData>.Expanded);
        focused = subscription.Select(ToastSelectors<TData>.Focused);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        // TST-DOM-01, TST-ARIA-01: each overridable by the consumer (TST-DOM-23).
        attributes.Set("tabindex", "-1");
        attributes.Set("role", "region");
        attributes.Set("aria-live", "polite");
        attributes.Set("aria-atomic", "false");
        attributes.Set("aria-relevant", "additions text");
        attributes.Set("aria-label", "Notifications");

        // TST-DOM-03: the first toast's height when positive, before the consumer's style (RND-DOM-16).
        IReadOnlyList<ToastObject<TData>> list = toasts!.Value;
        if (list.Count > 0 && list[0].Height is > 0 and double height)
        {
            attributes.Set("style", string.Create(CultureInfo.InvariantCulture, $"{ToastViewportCssVars.FrontmostHeight}: {height}px"));
        }
    }

    // TST-DOM-05, TST-A11Y-01, TST-A11Y-03: while the viewport is not focused and a high-priority toast is in the list,
    // ending ones included, one alert per such toast in list order, with its title and description text.
    /// <inheritdoc />
    protected override void BuildAfterElement(RenderTreeBuilder builder)
    {
        if (focused!.Value)
        {
            return;
        }

        IReadOnlyList<ToastObject<TData>> list = toasts!.Value;
        bool any = false;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].Priority == ToastPriority.High)
            {
                any = true;
                break;
            }
        }

        if (!any)
        {
            return;
        }

        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "style", VisuallyHidden.Style);
        for (int i = 0; i < list.Count; i++)
        {
            ToastObject<TData> toast = list[i];
            if (toast.Priority != ToastPriority.High)
            {
                continue;
            }

            builder.OpenElement(2, "div");
            builder.SetKey(toast.Id);
            builder.AddAttribute(3, "role", "alert");
            builder.AddAttribute(4, "aria-atomic", "true");
            builder.OpenElement(5, "div");
            builder.AddContent(6, toast.Title);
            builder.CloseElement();
            builder.OpenElement(7, "div");
            builder.AddContent(8, toast.Description);
            builder.CloseElement();
            builder.CloseElement();
        }

        builder.CloseElement();
    }
}
