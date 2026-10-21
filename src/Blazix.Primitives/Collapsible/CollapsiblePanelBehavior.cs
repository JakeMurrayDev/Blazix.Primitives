using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// The C# half of Base UI's useCollapsiblePanel (collapsible/panel/useCollapsiblePanel.ts) and of CollapsiblePanel.tsx's
// id registration and style; the measurement passes are the `collapsible` module's (ADR 04 §1, the component module of
// 2026-10-21). Accordion's panel (M3) owns one too. What lives here:
// - the render predicate and hidden (COL-STA-08, COL-DOM-13/16) and the panel id (COL-API-10, COL-DOM-10, COL-STA-09);
// - the handover at attach (ADR 04 Amendments 2026-10-18; COL-DOM-17/18, COL-ANI-14): until the twin is attached the
//   part renders the element's status attributes and its style (the variables at `auto`, the consumer's merged style,
//   `animation-name: none` while the suppression is active), and from attach it renders the same values again (frozen),
//   so the renderer never writes them while the module owns them; the consumer's string travels as Style;
// - the one-shot suppressions of an initially open panel and a re-attached one (COL-ANI-13/15/20);
// - the forced idle the module reports (COL-STA-06), the end of an exit (COL-STA-04), the detected type (COL-ANI-20,
//   COL-EDGE-03) and the reveal (COL-EVT-04/05: the decision point `reveal`, fallback run, ADR 04 Amendments 2026-10-21).
/// <summary>The behavior of a collapsible panel that its part renders through.</summary>
internal sealed class CollapsiblePanelBehavior : IDisposable
{
    private const string OpenAttributeSelector = "[data-open]";
    private const string StartingStyle = "data-starting-style";
    private const string EndingStyle = "data-ending-style";
    private const string CssAnimation = "css-animation";
    private const string AutoSizeStyle = "--collapsible-panel-height: auto; --collapsible-panel-width: auto";
    private const string SuppressionStyle = "animation-name: none";

    private readonly CollapsibleRootContext root;
    private readonly IJSRuntime jsRuntime;
    private readonly CollapsibleDevelopmentLog developmentLog;
    private readonly ILogger logger;
    private readonly Action stateChanged;
    private readonly Func<Exception, Task> dispatchException;
    private readonly TaskCompletionSource attachedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CollapsibleTwin? twin;
    private TwinHandle<CollapsibleOptions>? handle;
    private string? handleKey;
    private CollapsibleOptions? sentOptions;
    private string? registeredId;
    private bool registered;
    private bool preventMountAnimation;
    private bool preventActivityResumeAnimation;
    private string? animationType;
    private string? consumerStyle;
    private string? composedFor;
    private bool composedSuppressed;
    private string? composed;
    private string? renderedStyle;
    private bool renderedStarting;
    private bool renderedEnding;
    private TransitionStatus? renderedStatus;
    private bool frozen;
    private string? frozenStyle;
    private bool frozenStarting;
    private bool frozenEnding;

    /// <summary>Initializes a new instance of the <see cref="CollapsiblePanelBehavior"/> class.</summary>
    /// <param name="root">The shared state of the panel's collapsible.</param>
    /// <param name="jsRuntime">The runtime the twin imports its module through.</param>
    /// <param name="developmentLog">The once-per-session set of development messages.</param>
    /// <param name="logger">The logger of the part.</param>
    /// <param name="stateChanged">Renders the part again; called when the forced idle changes.</param>
    /// <param name="dispatchException">Routes an exception of a consumer handler that ran in a reveal to the part.</param>
    public CollapsiblePanelBehavior(
        CollapsibleRootContext root,
        IJSRuntime jsRuntime,
        CollapsibleDevelopmentLog developmentLog,
        ILogger logger,
        Action stateChanged,
        Func<Exception, Task> dispatchException)
    {
        this.root = root;
        this.jsRuntime = jsRuntime;
        this.developmentLog = developmentLog;
        this.logger = logger;
        this.stateChanged = stateChanged;
        this.dispatchException = dispatchException;
        // COL-ANI-13: a panel open on its first render suppresses its opening keyframe animation until it closes.
        preventMountAnimation = root.State.Open;
    }

    /// <summary>Gets or sets a value indicating whether the element stays in the document while the panel is closed.</summary>
    /// <value>The panel's <c>KeepMounted</c> parameter.</value>
    public bool KeepMounted { get; set; }

    /// <summary>Gets or sets a value indicating whether the closed element is hidden with <c>hidden="until-found"</c>.</summary>
    /// <value>The panel's <c>HiddenUntilFound</c> parameter.</value>
    public bool HiddenUntilFound { get; set; }

    /// <summary>Gets a value indicating whether the panel shows itself settled while the shared status is still starting.</summary>
    /// <value><see langword="true"/> during a motion-skipping open, as the module reported it.</value>
    public bool ForcedIdle { get; private set; }

    /// <summary>Gets the id the panel renders.</summary>
    /// <value>The panel's non-empty id, or the root's default panel id.</value>
    public string PanelId => registeredId ?? root.DefaultPanelId;

    /// <summary>Gets a task that completes when the module is first attached to the panel's element.</summary>
    /// <value>A task for the fixtures.</value>
    public Task TwinAttached => attachedSource.Task;

    /// <inheritdoc />
    public void Dispose()
    {
        if (registered)
        {
            registered = false;
            root.UnregisterPanel(registeredId);
        }

        if (twin is not null)
        {
            twin.ForcedIdleChanged -= OnForcedIdleChanged;
            twin.ExitCompleted -= root.CompleteExit;
            twin.AnimationTypeChanged -= OnAnimationTypeChanged;
            twin.RevealDecider = null;
            twin.RevealRequestHandler = null;
            _ = twin.DisposeAsync().AsTask();
            twin = null;
            handle = null;
        }
    }

    // COL-STA-06: the panel's status is the root's, forced to idle during a motion-skipping open.
    /// <summary>Returns the panel's transition status.</summary>
    /// <param name="rootStatus">The root's status.</param>
    /// <returns><see cref="TransitionStatus.Idle"/> while forced idle; otherwise, <paramref name="rootStatus"/>.</returns>
    public TransitionStatus? Status(TransitionStatus? rootStatus) => ForcedIdle ? TransitionStatus.Idle : rootStatus;

    // COL-DOM-16
    /// <summary>Determines whether the panel renders its element.</summary>
    /// <param name="open">Whether the panel is open.</param>
    /// <param name="mounted">Whether the panel is mounted.</param>
    /// <returns><see langword="true"/> when the panel is kept mounted, hidden until found, mounted or open.</returns>
    public bool RendersElement(bool open, bool mounted) => RendersElement(KeepMounted, HiddenUntilFound, open, mounted);

    // COL-DOM-16, as a pure rule (the L1 test of COL-STA-08 reads it).
    /// <summary>Determines whether a panel renders its element.</summary>
    /// <param name="keepMounted">The panel's <c>KeepMounted</c>.</param>
    /// <param name="hiddenUntilFound">The panel's <c>HiddenUntilFound</c>.</param>
    /// <param name="open">Whether the panel is open.</param>
    /// <param name="mounted">Whether the panel is mounted.</param>
    /// <returns><see langword="true"/> when the panel is kept mounted, hidden until found, mounted or open.</returns>
    public static bool RendersElement(bool keepMounted, bool hiddenUntilFound, bool open, bool mounted) => keepMounted || hiddenUntilFound || mounted || open;

    // COL-STA-08
    /// <summary>Determines whether a panel is hidden.</summary>
    /// <param name="open">Whether the panel is open.</param>
    /// <param name="mounted">Whether the panel is mounted.</param>
    /// <returns><see langword="true"/> while the panel is closed and no longer mounted.</returns>
    public static bool IsHidden(bool open, bool mounted) => !open && !mounted;

    // COL-STA-09, COL-EDGE-09; CollapsiblePanel.tsx:61-69 (registeredId = idProp || undefined).
    /// <summary>Registers the panel's id with the root, or re-registers it when it changed.</summary>
    /// <param name="id">The panel's <c>id</c> attribute; an empty string counts as none.</param>
    public void SetId(string? id)
    {
        string? next = string.IsNullOrEmpty(id) ? null : id;
        if (registered && string.Equals(next, registeredId, StringComparison.Ordinal))
        {
            return;
        }

        if (registered)
        {
            root.UnregisterPanel(registeredId);
        }

        registeredId = next;
        registered = true;
        root.RegisterPanel(next);
    }

    /// <summary>Reports the development message of a <c>KeepMounted</c> that <c>HiddenUntilFound</c> overrides.</summary>
    public void ReportKeepMountedIgnored() => developmentLog.Report(logger, CollapsibleDevelopmentLog.KeepMountedIgnoredMessage);

    /// <summary>Adds the attributes the part renders on the panel's element in this render.</summary>
    /// <param name="attributes">The part-supplied attributes.</param>
    /// <param name="open">Whether the panel is open.</param>
    /// <param name="mounted">Whether the panel is mounted.</param>
    /// <param name="status">The panel's transition status.</param>
    public void AddAttributes(AttributeList attributes, bool open, bool mounted, TransitionStatus? status)
    {
        // COL-ANI-13/15/20: the one-shot suppressions end the first time the panel is closed, with or without an element.
        if (!open)
        {
            preventMountAnimation = false;
            preventActivityResumeAnimation = false;
        }

        bool hidden = IsHidden(open, mounted);
        attributes.Set("id", PanelId);
        if (hidden)
        {
            // The boolean; the module turns it into hidden="until-found" before paint (COL-DOM-13/14).
            attributes.Set("hidden", true);
        }

        bool starting;
        bool ending;
        if (frozen)
        {
            starting = frozenStarting;
            ending = frozenEnding;
        }
        else
        {
            // The handover's initial values (COL-DOM-12, COL-DOM-15 for the persisted starting style).
            starting = status == TransitionStatus.Starting || (HiddenUntilFound && hidden && animationType != CssAnimation);
            ending = status == TransitionStatus.Ending;
        }

        renderedStarting = starting;
        renderedEnding = ending;
        renderedStatus = status;
        if (starting)
        {
            attributes.Set(StartingStyle, string.Empty);
        }

        if (ending)
        {
            attributes.Set(EndingStyle, string.Empty);
        }
    }

    /// <summary>Puts the panel's own values over the merged attributes: the id it does not merge and the handed-over style.</summary>
    /// <param name="attributes">The merged attributes.</param>
    /// <param name="open">Whether the panel is open.</param>
    public void FinishAttributes(AttributeList attributes, bool open)
    {
        // COL-API-10: an empty id is not the panel's id.
        if (attributes.TryGetValue("id", out AttributeValue id) && id.TryGetValue(out string text) && text.Length == 0)
        {
            attributes.Set("id", PanelId);
        }

        consumerStyle = attributes.TryGetValue(MergeProps.StyleAttribute, out AttributeValue merged) && merged.TryGetValue(out string style) ? style : null;
        string? rendered = frozen ? frozenStyle : ComposeMemoized(consumerStyle, open && (preventMountAnimation || preventActivityResumeAnimation));
        renderedStyle = rendered;
        if (rendered is null)
        {
            attributes.Remove(MergeProps.StyleAttribute);
        }
        else
        {
            attributes.Set(MergeProps.StyleAttribute, rendered);
        }
    }

    /// <summary>Attaches the module after a render that showed the element, updates it, or releases it.</summary>
    /// <param name="rendered">Whether this render rendered the element.</param>
    /// <param name="element">The element, or <see langword="null"/> when a substitute callback or component rendered it.</param>
    /// <returns>A task that completes when the module has the new options.</returns>
    public async Task AfterRenderAsync(bool rendered, ElementReference? element)
    {
        if (!rendered)
        {
            await DetachAsync();
            return;
        }

        string key = element?.Id ?? PanelId;
        if (handle is not null && !string.Equals(handleKey, key, StringComparison.Ordinal))
        {
            await DetachAsync();
        }

        if (handle is not null)
        {
            CollapsibleOptions next = Options();
            // Without an element reference the module re-resolves its element on every update (COL-ANI-15/18/19).
            if (element is null || next != sentOptions)
            {
                sentOptions = next;
                await handle.UpdateAsync(next);
            }

            return;
        }

        twin ??= CreateTwin();
        CollapsibleOptions options = Options();
        // Frozen from the attach call on: a render that lands before the module answers keeps the values it attached with.
        Freeze();
        try
        {
            handle = element is { } reference ? await twin.AttachAsync(reference, options) : await twin.AttachAsync(PanelId, options);
        }
        catch (JSException)
        {
            // A render function withheld the element: an exit in its ending phase has nothing left to animate (COL-ANI-18).
            Unfreeze();
            root.CompleteExitIfEnding();
            return;
        }

        handleKey = key;
        sentOptions = options;
        attachedSource.TrySetResult();
    }

    // COL-DOM-18: the variables, then the consumer's style, then the suppression.
    private static string ComposeStyle(string? consumer, bool suppressed)
    {
        string withConsumer = MergeProps.MergeStyles(AutoSizeStyle, consumer)!;
        return suppressed ? MergeProps.MergeStyles(withConsumer, SuppressionStyle)! : withConsumer;
    }

    // ADR 16 rule 5: composed once per distinct consumer string.
    private string ComposeMemoized(string? consumer, bool suppressed)
    {
        if (composed is not null && ReferenceEquals(consumer, composedFor) && suppressed == composedSuppressed)
        {
            return composed;
        }

        composedFor = consumer;
        composedSuppressed = suppressed;
        composed = ComposeStyle(consumer, suppressed);
        return composed;
    }

    private CollapsibleOptions Options()
    {
        CollapsibleRootStoreState state = root.State;
        return new CollapsibleOptions
        {
            Open = state.Open,
            Mounted = state.Mounted,
            Status = renderedStatus,
            KeepMounted = KeepMounted,
            HiddenUntilFound = HiddenUntilFound,
            SuppressOpenAnimation = state.Open && (preventMountAnimation || preventActivityResumeAnimation),
            PanelId = PanelId,
            OpenSelector = OpenAttributeSelector,
            Style = consumerStyle,
        };
    }

    private void Freeze()
    {
        frozen = true;
        frozenStyle = renderedStyle;
        frozenStarting = renderedStarting;
        frozenEnding = renderedEnding;
    }

    private void Unfreeze()
    {
        frozen = false;
        frozenStyle = null;
    }

    private async Task DetachAsync()
    {
        if (handle is not { } released)
        {
            return;
        }

        // COL-ANI-20: a keyframe panel detached while open does not replay its opening animation when attached again.
        CollapsibleRootStoreState state = root.State;
        if (state.Open && state.Mounted && animationType == CssAnimation)
        {
            preventActivityResumeAnimation = true;
        }

        handle = null;
        handleKey = null;
        sentOptions = null;
        Unfreeze();
        if (ForcedIdle)
        {
            ForcedIdle = false;
            stateChanged();
        }

        await released.DisposeAsync();
    }

    private CollapsibleTwin CreateTwin()
    {
        CollapsibleTwin created = new(jsRuntime)
        {
            RevealDecider = DecideReveal,
            RevealRequestHandler = RequestRevealAsync,
        };
        created.ForcedIdleChanged += OnForcedIdleChanged;
        created.ExitCompleted += root.CompleteExit;
        created.AnimationTypeChanged += OnAnimationTypeChanged;
        return created;
    }

    private void OnForcedIdleChanged(bool forcedIdle)
    {
        if (ForcedIdle != forcedIdle)
        {
            ForcedIdle = forcedIdle;
            stateChanged();
        }
    }

    private void OnAnimationTypeChanged(PanelAnimationTypeChange change)
    {
        animationType = change.Type;
        if (change.Mixed)
        {
            developmentLog.Report(logger, CollapsibleDevelopmentLog.MixedMotionMessage);
        }
    }

    // The reveal decision point on WebAssembly, inside the dispatch: the request with reason none and the beforematch
    // snapshot; a canceled request answers stop (COL-EVT-04/05). Only the synchronous part of a handler can cancel.
    private DecisionPolicy DecideReveal(EventSnapshot snapshot)
    {
        ChangeEventDetails details = new(EventReason.None, snapshot);
        Task<bool> request;
        try
        {
            request = root.RequestOpenAsync(true, details);
        }
        catch (Exception exception)
        {
            _ = dispatchException(exception);
            return DecisionPolicy.Run;
        }

        if (!request.IsCompleted)
        {
            _ = ObserveAsync(request);
        }
        else if (request.IsFaulted)
        {
            _ = dispatchException(request.Exception!.GetBaseException());
        }

        return details.IsCanceled ? DecisionPolicy.Stop : DecisionPolicy.Run;
    }

    // The reveal on Server, a round trip after the dispatch: the request, then whether the panel is open.
    private async ValueTask<bool> RequestRevealAsync(EventSnapshot snapshot)
    {
        try
        {
            await root.RequestOpenAsync(true, new ChangeEventDetails(EventReason.None, snapshot));
        }
        catch (Exception exception)
        {
            await dispatchException(exception);
        }

        return root.State.Open;
    }

    private async Task ObserveAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception exception) when (!task.IsCanceled)
        {
            await dispatchException(exception);
        }
    }
}
