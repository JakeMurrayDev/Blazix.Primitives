using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// AVA-STA-02, AVA-STA-07; avatar/image/AvatarImage.tsx:182-187. ADR 04 §2: the transition status is a mirrored field
// (JS writes data-starting-style / data-ending-style); AVA-DOM-05: the loading status is never projected.
/// <summary>Represents the state of an avatar image, which the part passes to its class and style functions.</summary>
/// <param name="ImageLoadingStatus">The image's own loading status.</param>
/// <param name="TransitionStatus">
/// The phase of the image's enter or exit transition, or <see langword="null"/> when it is not transitioning; never
/// <see cref="Primitives.TransitionStatus.Ending"/> while the image is kept mounted.
/// </param>
[PartState]
public sealed partial record AvatarImageState(ImageLoadingStatus ImageLoadingStatus, [property: MirroredField] TransitionStatus? TransitionStatus);

// AVA §3.4; avatar/image/AvatarImageDataAttributes.ts. No such class exists for the root or the fallback.
/// <summary>Provides the names of the data attributes an avatar image renders.</summary>
public static class AvatarImageDataAttributes
{
    /// <summary>Represents the attribute present, with an empty value, while a kept-mounted image is loading: <c>data-loading</c>.</summary>
    public const string Loading = "data-loading";

    /// <summary>Represents the attribute present, with an empty value, while a kept-mounted image has failed to load: <c>data-error</c>.</summary>
    public const string Error = "data-error";

    /// <summary>Represents the attribute present, with an empty value, while the image plays its enter transition: <c>data-starting-style</c>.</summary>
    public const string StartingStyle = "data-starting-style";

    /// <summary>Represents the attribute present, with an empty value, while the image plays its exit transition: <c>data-ending-style</c>.</summary>
    public const string EndingStyle = "data-ending-style";
}

// Base UI's Avatar.Image (avatar/image/AvatarImage.tsx, useImageLoadingStatus.ts); the contract is
// docs/components/avatar.md.
//
// The status. Preload mode (AVA-STA-08..10, AVA-ALG-01): the `dom` module's detached probe, through this part's DomTwin
// attached to the root's element, because the <img> exists only once loaded. Kept-mounted mode (AVA-STA-11..14,
// AVA-ALG-02): the module derives the status from the attached <img> on attach and after a source change; the
// element's load and error events set it here, after the consumer's handlers, which can veto with PreventPartHandler()
// (ADR 04 Amendments 2026-10-18 item 1: their result is state). Without an element (a substitute callback, ADR 02's
// relaxation of RND-DOM-20..24) nothing is derived and only the events report (AVA-STA-13, AVA-EVT-03). The module sends
// one message per change and a cached image only its result (AVA-EVT-02); the part reports a change once, never `idle`
// (AVA-STA-04, AVA-EVT-01), calls OnLoadingStatusChange and then sets the root's status (AVA-STA-05), and resets the root
// to `idle` when it is removed (AVA-STA-06).
//
// The transition (AVA-ANI-01..05, TRN §6.1 and §6.3): open = status == Loaded, all machine options false, through this
// part's TransitionsTwin on the <img> (two twins on one element: the orchestrator's reading of Interop/CONTEXT.md,
// track 5 note). Preload: the element appears with data-starting-style rendered by C# (the handover; the value stays
// frozen in the render output after attach), the module flips it and runs the exit, and its completion message removes
// the element (ADR 01 TRN-ANI-03 row; the close itself reaches the module through `update`, the 2026-10-21 Avatar row).
// Kept-mounted: the module reads the open state from the applied batch through `:not([aria-hidden])` (ADR 04 Amendments
// 2026-10-21) unless the consumer supplies aria-hidden, suppresses data-ending-style (AVA-STA-07), and is attached once
// the first status is known, so that an image already loaded then is mounted without a 'starting' phase (AVA-ANI-04,
// TRN-ANI-35; Base UI's isInitialCommit).
//
// Attribute order (AVA-DOM-06): Base UI applies [status props, consumer props, source props]; the pipeline places part
// attributes before the consumer's, so the part hands the pipeline the consumer's attributes with `sizes`, `srcset` and
// `src` appended, each only when given.
/// <summary>
/// Represents the picture of an avatar: Base UI's <c>Avatar.Image</c>, rendered as an <c>img</c>. By default the image is
/// loaded in the background and rendered once it has loaded; with <see cref="KeepMounted"/> the element is always
/// rendered and loads in place.
/// </summary>
/// <remarks>The part must be placed within an <see cref="AvatarRoot"/>.</remarks>
public sealed class AvatarImage : PartBase<AvatarImageState>
{
    private const string DefaultTag = "img";
    private const string KeptMountedOpenSelector = ":not([aria-hidden])";

    private static readonly StateAttributesMapping<AvatarImageState> s_mapping = new StateAttributesMapping<AvatarImageState>()
        .Map<ImageLoadingStatus>(nameof(AvatarImageState.ImageLoadingStatus), static _ => null);

    private readonly EventCallback onLoad;
    private readonly EventCallback onError;
    private readonly Action<string> onImageStatus;
    private readonly Action<TransitionStatusChange> onTransitionStatus;
    private readonly Action<bool> onTransitionCompleted;
    private AvatarRootContext? context;
    private AvatarImageState? state;
    private ImageLoadingStatus status;
    private bool statusKnown;
    private bool mounted;
    private TransitionStatus? transitionStatus;
    private TransitionStatus? renderedTransition;
    private bool keptMounted;
    private IReadOnlyDictionary<string, object>? consumerAttributes;
    private Dictionary<string, object>? composedAttributes;
    private DomTwin? domTwin;
    private TwinHandle<DomOptions>? domHandle;
    private DomOptions? sentDomOptions;
    private TransitionsTwin? transitionsTwin;
    private TwinHandle<TransitionsOptions>? transitionsHandle;
    private TransitionsOptions? sentTransitionsOptions;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="AvatarImage"/> class.</summary>
    public AvatarImage()
    {
        onLoad = CreateHandler<ProgressEventArgs>("onload", _ => SetStatusAsync(ImageLoadingStatus.Loaded));
        onError = CreateHandler<Microsoft.AspNetCore.Components.Web.ErrorEventArgs>("onerror", _ => SetStatusAsync(ImageLoadingStatus.Error));
        onImageStatus = literal =>
        {
            // A report of the status the image already has (a re-derivation) changes nothing and costs nothing more.
            ImageLoadingStatus next = ParseStatus(literal);
            if (next != status)
            {
                PostStatus(next);
            }
        };
        onTransitionStatus = change => _ = InvokeAsync(() => MirrorTransition(change.Status, change.Mounted));
        onTransitionCompleted = open => _ = InvokeAsync(() => CompleteTransition(open));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    private AvatarRootContext Context => context!;

    private bool Shown => KeepMounted || mounted;

    [CascadingParameter]
    private AvatarRootContext? RootContext { get; set; }

    // AVA-API-02
    /// <summary>Gets or sets the URL of the image.</summary>
    /// <value>The <c>src</c> of the image and of its background load, or <see langword="null"/> for none.</value>
    [Parameter]
    public string? Src { get; set; }

    /// <summary>Gets or sets the responsive image candidates.</summary>
    /// <value>The <c>srcset</c> of the image and of its background load, or <see langword="null"/> for none.</value>
    /// <remarks>A source set alone is enough to load the image.</remarks>
    [Parameter]
    public string? SrcSet { get; set; }

    /// <summary>Gets or sets the responsive image sizes.</summary>
    /// <value>The <c>sizes</c> of the image and of its background load, or <see langword="null"/> for none.</value>
    [Parameter]
    public string? Sizes { get; set; }

    // AVA-API-03; `string?` per ADR 08 §3 (the empty literal is a value, so no enum).
    /// <summary>Gets or sets the CORS mode of the image request.</summary>
    /// <value>
    /// The <c>crossorigin</c> value, such as <c>anonymous</c> or <c>use-credentials</c>, applied to the image and to its
    /// background load; or <see langword="null"/> for a request without CORS.
    /// </value>
    [Parameter]
    public string? CrossOrigin { get; set; }

    /// <summary>Gets or sets the referrer policy of the image request.</summary>
    /// <value>The <c>referrerpolicy</c> value applied to the image and to its background load, or <see langword="null"/>.</value>
    [Parameter]
    public string? ReferrerPolicy { get; set; }

    // AVA-API-04
    /// <summary>Gets or sets a value indicating whether the image element is always rendered and loads in place.</summary>
    /// <value>
    /// <see langword="true"/> to render the <c>img</c> at once, which supports <c>loading="lazy"</c> and server-rendered
    /// images; it then carries <c>data-loading</c> or <c>data-error</c> and is hidden from assistive technology until it
    /// has loaded. <see langword="false"/>, the default, loads the image in the background and renders it once loaded.
    /// </value>
    [Parameter]
    public bool KeepMounted { get; set; }

    // AVA-API-05
    /// <summary>Gets or sets the callback invoked when the image's loading status changes.</summary>
    /// <value>A callback that receives the new status; it is never invoked with <see cref="ImageLoadingStatus.Idle"/>.</value>
    [Parameter]
    public EventCallback<ImageLoadingStatus> OnLoadingStatusChange { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override AvatarImageState State => state ??= new AvatarImageState(status, KeepMounted && transitionStatus == TransitionStatus.Ending ? null : transitionStatus);

    /// <inheritdoc />
    protected override StateAttributesMapping<AvatarImageState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    /// <remarks>The attributes of the previous render are not carried over: the part replaces them with the consumer's followed by the source attributes.</remarks>
    public override Task SetParametersAsync(ParameterView parameters)
    {
        AdditionalAttributes = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The image is not inside an <see cref="AvatarRoot"/>.</exception>
    protected override void OnInitialized()
    {
        context = AvatarRootContext.Require(RootContext, nameof(AvatarImage));
        base.OnInitialized();
        keptMounted = KeepMounted;
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (KeepMounted != keptMounted)
        {
            keptMounted = KeepMounted;

            // AVA-EDGE-07: the first status known after a switch to kept-mounted counts as the initial one; a switch to
            // preload keeps the element only while it is mounted.
            statusKnown = false;
            if (KeepMounted && status == ImageLoadingStatus.Loaded)
            {
                mounted = true;
            }
        }

        consumerAttributes = AdditionalAttributes;
        AdditionalAttributes = ComposeAttributes();
        state = null;
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (!Shown)
        {
            ((IPartHost)this).SetElement(null);
            return;
        }

        base.BuildRenderTree(builder);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (disposed)
        {
            return;
        }

        await SyncImageLoadAsync();
        await SyncTransitionAsync();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            if (context is not null)
            {
                // AVA-STA-06, AVA-EDGE-04: removing any image resets the root.
                context.SetStatus(ImageLoadingStatus.Idle);
            }

            if (domTwin is not null)
            {
                domTwin.ImageStatusChanged -= onImageStatus;
                _ = domTwin.DisposeAsync().AsTask();
                domTwin = null;
            }

            if (transitionsTwin is not null)
            {
                transitionsTwin.StatusChanged -= onTransitionStatus;
                transitionsTwin.TransitionCompleted -= onTransitionCompleted;
                _ = transitionsTwin.DisposeAsync().AsTask();
                transitionsTwin = null;
            }

            domHandle = null;
            transitionsHandle = null;
        }

        base.Dispose(disposing);
    }

    // Part-supplied attributes, before the consumer's (Base UI's renderedStatusProps, then crossOrigin and referrerPolicy
    // from elementProps). The C#-rendered transition attribute is the handover value of the current element.
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        if (renderedTransition == TransitionStatus.Starting)
        {
            attributes.Set(AvatarImageDataAttributes.StartingStyle, string.Empty);
        }

        if (KeepMounted)
        {
            // AVA-DOM-04, AVA-ARIA-01; a consumer aria-hidden replaces the value in place.
            if (status == ImageLoadingStatus.Loading)
            {
                attributes.Set(AvatarImageDataAttributes.Loading, string.Empty);
            }
            else if (status == ImageLoadingStatus.Error)
            {
                attributes.Set(AvatarImageDataAttributes.Error, string.Empty);
            }

            if (status != ImageLoadingStatus.Loaded)
            {
                attributes.Set("aria-hidden", "true");
            }

            attributes.Set("onload", onLoad);
            attributes.Set("onerror", onError);
        }

        attributes.Set("crossorigin", CrossOrigin);
        attributes.Set("referrerpolicy", ReferrerPolicy);
    }

    private static ImageLoadingStatus ParseStatus(string literal) => literal switch
    {
        "loading" => ImageLoadingStatus.Loading,
        "loaded" => ImageLoadingStatus.Loaded,
        "error" => ImageLoadingStatus.Error,
        _ => throw new ArgumentOutOfRangeException(nameof(literal), literal, "Blazix: an image loading status must be `loading`, `loaded` or `error`."),
    };

    private bool ConsumerHidesImage()
    {
        if (consumerAttributes is null)
        {
            return false;
        }

        foreach (KeyValuePair<string, object> pair in consumerAttributes)
        {
            if (string.Equals(pair.Key, "aria-hidden", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // AVA-DOM-06: the consumer's attributes, then sizes, srcset and src, each only when given. Rebuilt only when an input
    // changed, so a re-render with the same parameters allocates nothing.
    private IReadOnlyDictionary<string, object>? ComposeAttributes()
    {
        if (Sizes is null && SrcSet is null && Src is null)
        {
            composedAttributes = null;
            return consumerAttributes;
        }

        if (composedAttributes is not null && IsComposedFrom(consumerAttributes))
        {
            return composedAttributes;
        }

        Dictionary<string, object> composed = consumerAttributes is null ? new(3, StringComparer.Ordinal) : new(consumerAttributes, StringComparer.Ordinal);
        AppendSource(composed, "sizes", Sizes);
        AppendSource(composed, "srcset", SrcSet);
        AppendSource(composed, "src", Src);
        composedAttributes = composed;
        return composed;
    }

    private bool IsComposedFrom(IReadOnlyDictionary<string, object>? consumer)
    {
        int count = consumer?.Count ?? 0;
        int sources = (Sizes is null ? 0 : 1) + (SrcSet is null ? 0 : 1) + (Src is null ? 0 : 1);
        if (composedAttributes!.Count != count + sources
            || !SourceIs("sizes", Sizes) || !SourceIs("srcset", SrcSet) || !SourceIs("src", Src))
        {
            return false;
        }

        if (consumer is not null)
        {
            foreach (KeyValuePair<string, object> pair in consumer)
            {
                if (!composedAttributes.TryGetValue(pair.Key, out object? value) || !Equals(value, pair.Value))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool SourceIs(string name, string? value)
    {
        return value is null ? !composedAttributes!.ContainsKey(name) : composedAttributes!.TryGetValue(name, out object? present) && string.Equals(present as string, value, StringComparison.Ordinal);
    }

    private static void AppendSource(Dictionary<string, object> attributes, string name, string? value)
    {
        if (value is not null)
        {
            attributes.Remove(name);
            attributes[name] = value;
        }
    }

    // Separate from the callback so that the closure is allocated only for a change.
    private void PostStatus(ImageLoadingStatus next) => _ = InvokeAsync(() => SetStatusAsync(next));

    // One change of the image's own status, from the module or from the element's events.
    private async Task SetStatusAsync(ImageLoadingStatus next)
    {
        if (disposed || next == status)
        {
            return;
        }

        bool initial = KeepMounted && !statusKnown;
        statusKnown = true;
        status = next;
        ApplyOpenState(initial);
        state = null;
        StateHasChanged();

        await OnLoadingStatusChange.InvokeAsync(next);
        if (!disposed)
        {
            Context.SetStatus(next);
        }
    }

    // The C# half of the transition machine for this render (TransitionStatusRules): the element a preload image renders
    // when it loads starts in 'starting'; an element with no transitions twin to animate it leaves at once.
    private void ApplyOpenState(bool initial)
    {
        bool open = status == ImageLoadingStatus.Loaded;
        if (KeepMounted)
        {
            if (open && !mounted && (initial || transitionsHandle is null))
            {
                // AVA-ANI-04: an image already loaded when its status is first known is shown without 'starting'.
                mounted = true;
                transitionStatus = null;
            }
            else if (!open && transitionsHandle is null)
            {
                mounted = false;
                transitionStatus = null;
            }

            return;
        }

        (bool nextMounted, TransitionStatus? nextStatus) = TransitionStatusRules.Evaluate(open, mounted, transitionStatus, deferEndingState: false);
        if (open && !mounted)
        {
            renderedTransition = nextStatus;
        }
        else if (!open && mounted && transitionsHandle is null && Element is null)
        {
            // Nothing can play the exit: a substitute callback leaves no element to watch.
            nextMounted = false;
            nextStatus = null;
            renderedTransition = null;
        }

        mounted = nextMounted;
        transitionStatus = nextStatus;
    }

    private void MirrorTransition(TransitionStatus? next, bool nextMounted)
    {
        if (disposed || (next == transitionStatus && nextMounted == mounted))
        {
            return;
        }

        transitionStatus = next;
        mounted = nextMounted;
        state = null;
        StateHasChanged();
    }

    private void CompleteTransition(bool open)
    {
        if (disposed || open)
        {
            return;
        }

        // The exit finished: the element is unmounted (removed in preload mode) and its status unset.
        mounted = false;
        transitionStatus = null;
        renderedTransition = null;
        state = null;
        StateHasChanged();
    }

    private async Task SyncImageLoadAsync()
    {
        // Preload: the root's element hosts the probe; kept-mounted: the image's own element, if it has one.
        ElementReference? element = null;
        string? elementId = null;
        if (KeepMounted)
        {
            element = Element;
        }
        else
        {
            element = Context.Root.HostElement;
            elementId = element is null ? Context.Root.HostId : null;
        }

        string? key = element?.Id ?? elementId;
        if (key is null)
        {
            await ReleaseImageLoadAsync();
            return;
        }

        if (domHandle is not null && !string.Equals(domHandle.Key, key, StringComparison.Ordinal))
        {
            await ReleaseImageLoadAsync();
        }

        DomOptions? options = NextDomOptions();
        if (domHandle is not null)
        {
            if (options is not null)
            {
                sentDomOptions = options;
                await domHandle.UpdateAsync(options);
            }

            return;
        }

        if (domTwin is null)
        {
            domTwin = new DomTwin(JSRuntime);
            domTwin.ImageStatusChanged += onImageStatus;
        }

        options ??= sentDomOptions!;
        sentDomOptions = options;
        domHandle = element is { } reference ? await domTwin.AttachAsync(reference, options) : await domTwin.AttachAsync(elementId!, options);
    }

    private async Task ReleaseImageLoadAsync()
    {
        if (domHandle is { } released)
        {
            domHandle = null;
            sentDomOptions = null;
            await released.DisposeAsync();
        }
    }

    // Compares before constructing, so an unchanged render allocates no options record.
    private DomOptions? NextDomOptions()
    {
        if (sentDomOptions?.Image is { } sent
            && string.Equals(sent.Src, Src, StringComparison.Ordinal)
            && string.Equals(sent.SrcSet, SrcSet, StringComparison.Ordinal)
            && string.Equals(sent.Sizes, Sizes, StringComparison.Ordinal)
            && string.Equals(sent.CrossOrigin, CrossOrigin, StringComparison.Ordinal)
            && string.Equals(sent.ReferrerPolicy, ReferrerPolicy, StringComparison.Ordinal)
            && sent.KeepMounted == KeepMounted)
        {
            return null;
        }

        return new DomOptions
        {
            Image = new ImageLoadOptions
            {
                Src = Src,
                SrcSet = SrcSet,
                Sizes = Sizes,
                CrossOrigin = CrossOrigin,
                ReferrerPolicy = ReferrerPolicy,
                KeepMounted = KeepMounted,
            },
        };
    }

    private async Task SyncTransitionAsync()
    {
        // Preload: while the element is rendered; kept-mounted: once the first status is known (AVA-ANI-04).
        ElementReference? element = Shown && (KeepMounted ? statusKnown : mounted) ? Element : null;
        if (element is null || (transitionsHandle is not null && !string.Equals(transitionsHandle.Key, element.Value.Id, StringComparison.Ordinal)))
        {
            if (transitionsHandle is { } released)
            {
                transitionsHandle = null;
                sentTransitionsOptions = null;
                await released.DisposeAsync();
            }

            if (element is null)
            {
                return;
            }
        }

        TransitionsOptions? options = NextTransitionsOptions();
        if (transitionsHandle is not null)
        {
            if (options is not null)
            {
                sentTransitionsOptions = options;
                await transitionsHandle.UpdateAsync(options);
            }

            return;
        }

        if (transitionsTwin is null)
        {
            transitionsTwin = new TransitionsTwin(JSRuntime);
            transitionsTwin.StatusChanged += onTransitionStatus;
            transitionsTwin.TransitionCompleted += onTransitionCompleted;
        }

        options ??= sentTransitionsOptions!;
        sentTransitionsOptions = options;
        transitionsHandle = await transitionsTwin.AttachAsync(element.Value, options);
    }

    private TransitionsOptions? NextTransitionsOptions()
    {
        bool open = status == ImageLoadingStatus.Loaded;
        string? selector = KeepMounted && !ConsumerHidesImage() ? KeptMountedOpenSelector : null;
        if (sentTransitionsOptions is { } sent
            && sent.Open == open
            && sent.KeepMounted == KeepMounted
            && string.Equals(sent.OpenSelector, selector, StringComparison.Ordinal))
        {
            return null;
        }

        // TRN §6.1: Avatar.Image uses no idle state, no deferred ending and no initial animation; Mounted and Status are
        // what the element was rendered with, read on attach only.
        return new TransitionsOptions
        {
            Open = open,
            Mounted = mounted,
            Status = renderedTransition,
            KeepMounted = KeepMounted,
            SuppressEndingStyle = KeepMounted,
            OpenSelector = selector,
        };
    }
}
