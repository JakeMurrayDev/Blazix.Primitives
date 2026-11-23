using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Iodine.Headless;

// toast/provider/ToastProviderContext.ts:9-15 (TST-EDGE-01): the Provider's store, which the Viewport, Root, Close and
// Positioner read. Base UI's message names `useToastManager`, a hook the port does not have; the port's names the Provider
// (a dated deviation note on TST-EDGE-01).
internal sealed class ToastProviderContext<TData>(ToastStore<TData> store)
{
    internal const string MissingMessage = "Iodine: `ToastProviderContext` is missing. Toast parts must be used within `<ToastProvider>`.";

    public ToastStore<TData> Store { get; } = store;

    public static ToastProviderContext<TData> Require(ToastProviderContext<TData>? context) => context ?? throw new InvalidOperationException(MissingMessage);
}

// Base UI's Toast.Provider (toast/provider/ToastProvider.tsx); TST-STR-01, TST-API-01..04, TST-EDGE-05, TST-EDGE-06,
// TST-EDGE-09. It renders no element (the TooltipProvider shape). ADR 05 Amendments 2026-11-18 item 3 (chosen by the user
// 2026-11-18):
// - one store per Provider instance, created once (DP2, settled by the orchestrator 2026-11-18);
// - the manager is the Manager parameter, else the one registered for the scope, else one of its own, and is cascaded as
//   the useToastManager equivalent;
// - it subscribes in OnInitialized when the render is interactive, earlier than Base UI's after-paint, so an add made by a
//   descendant's first render is seen; it does not subscribe during prerendering, where an add is lost;
// - a change of Manager unsubscribes from the previous manager and subscribes to the new one (TST-API-03);
// - Timeout and Limit are written to the store in OnParametersSet, before any descendant renders (TST-API-04; ADR 01's
//   UTL-STA-19..22 row), and a parameter set that fails before OnParametersSet writes nothing (TST-EDGE-09, design point
//   9, settled by the orchestrator 2026-11-18);
// - the timers run on the scope's TimeProvider, else the system's, and its disposal clears them (TST-EDGE-06).
// Its generic shape is M6 track 9a design point 5, pending the user.
/// <summary>
/// Provides toasts to its content: it holds the toasts a manager shows and cascades that manager. Base UI's
/// <c>Toast.Provider</c>; it renders no element.
/// </summary>
/// <typeparam name="TData">The type of the custom data the toasts carry.</typeparam>
/// <remarks>
/// <para>
/// The provider acts on the calls of <see cref="Manager"/>, else of the manager registered for the application
/// (<c>AddIodineHeadless</c> registers a <see cref="ToastManager"/>), else of a manager of its own, which its content
/// receives as a cascading value.
/// </para>
/// <para>
/// It starts receiving calls when it is first rendered interactively, so a toast added by its content's first render is
/// shown. While it is prerendered it receives none, and a toast added then is not shown.
/// </para>
/// </remarks>
[CascadingTypeParameter(nameof(TData))]
public sealed class ToastProvider<TData> : ComponentBase, IDisposable
{
    private ToastStore<TData>? store;
    private ToastProviderContext<TData>? context;
    private ToastManager<TData>? manager;
    private ToastManager<TData>? fallback;
    private ToastProviderLink<TData>? link;
    private RenderFragment? cascadedContent;
    private RenderFragment? cascade;

    [Inject]
    private IServiceProvider Services { get; set; } = null!;

    /// <summary>Gets or sets how long a toast stays before it is dismissed automatically.</summary>
    /// <value>
    /// The delay used by every toast that has no <c>Timeout</c> of its own, or <see langword="null"/> (the default) for
    /// 5000 milliseconds. A delay of zero keeps such toasts until they are closed. A change applies to timers started after it.
    /// </value>
    [Parameter]
    public Duration? Timeout { get; set; }

    /// <summary>Gets or sets the number of toasts shown at once.</summary>
    /// <value>
    /// The limit; the default is 3. Older toasts beyond it stay rendered but are marked limited and made inert. Zero
    /// limits every toast.
    /// </value>
    [Parameter]
    public int Limit { get; set; } = 3;

    /// <summary>Gets or sets the manager whose calls the provider acts on.</summary>
    /// <value>
    /// The manager, or <see langword="null"/> (the default) for the one registered for the application, else a manager of
    /// the provider's own.
    /// </value>
    [Parameter]
    public ToastManager<TData>? Manager { get; set; }

    /// <summary>Gets or sets the content.</summary>
    /// <value>The child content, or <see langword="null"/>.</value>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    // For the tests and the fixtures: the provider's store.
    internal ToastStore<TData> Store => store!;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        TimeProvider time = Services.GetService<TimeProvider>() ?? TimeProvider.System;
        store = new ToastStore<TData>(ToastStore<TData>.ProviderTimeout(Timeout), Limit, time, InvokeAsync);
        context = new ToastProviderContext<TData>(store);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        store!.SyncProviderProps(ToastStore<TData>.ProviderTimeout(Timeout), Limit);

        ToastManager<TData> next = Manager ?? (fallback ??= Registered() ?? new ToastManager<TData>());
        if (!ReferenceEquals(next, manager))
        {
            manager = next;
            link?.Dispose();
            link = RendererInfo.IsInteractive ? new ToastProviderLink<TData>(manager, store, InvokeAsync) : null;
            cascade = null;
        }

        if (cascade is null || !ReferenceEquals(cascadedContent, ChildContent))
        {
            cascadedContent = ChildContent;
            cascade = Cascade(context!, manager, cascadedContent);
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, cascade);

    /// <inheritdoc />
    public void Dispose()
    {
        link?.Dispose();
        link = null;
        store?.Dispose();
    }

    // The provider context and the manager, both fixed (IOD2001). A change of manager builds a new cascade under a new key,
    // so its content receives the new manager.
    private static RenderFragment Cascade(ToastProviderContext<TData> context, ToastManager<TData> manager, RenderFragment? content) => builder =>
    {
        builder.OpenComponent<CascadingValue<ToastProviderContext<TData>>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<ToastProviderContext<TData>>.Value), context);
        builder.AddComponentParameter(2, nameof(CascadingValue<ToastProviderContext<TData>>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<ToastProviderContext<TData>>.ChildContent), (RenderFragment)(inner =>
        {
            inner.OpenComponent<CascadingValue<ToastManager<TData>>>(0);
            inner.SetKey(manager);
            inner.AddComponentParameter(1, nameof(CascadingValue<ToastManager<TData>>.Value), manager);
            inner.AddComponentParameter(2, nameof(CascadingValue<ToastManager<TData>>.IsFixed), true);
            inner.AddComponentParameter(3, nameof(CascadingValue<ToastManager<TData>>.ChildContent), content);
            inner.CloseComponent();
        }));
        builder.CloseComponent();
    };

    // The scope's manager for this data type: the default ToastManager that AddIodineHeadless registers, or a consumer's
    // own registration of ToastManager<TData>.
    private ToastManager<TData>? Registered()
    {
        if (typeof(TData) == typeof(NoPayload) && Services.GetService<ToastManager>() is ToastManager<TData> registered)
        {
            return registered;
        }

        return Services.GetService<ToastManager<TData>>();
    }
}
