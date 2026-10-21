using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Blazix.Primitives;

// AVA-STA-03; avatar/fallback/AvatarFallback.tsx:52-66. AVA-DOM-05: the status is never projected.
/// <summary>Represents the state of an avatar fallback, which the part passes to its class and style functions.</summary>
/// <param name="ImageLoadingStatus">The status of the avatar's image, as its root holds it.</param>
[PartState]
public sealed partial record AvatarFallbackState(ImageLoadingStatus ImageLoadingStatus);

// Base UI's Avatar.Fallback (avatar/fallback/AvatarFallback.tsx); the contract is docs/components/avatar.md.
// AVA-DOM-09: rendered exactly while the root's status is not `loaded` and (delay == 0 or the delay elapsed); no
// transition phase. AVA-ANI-07: the delay restarts after the render that mounts the part or changes `Delay`
// (OnAfterRender, "after paint" in ADR 01 decision 4's table); once elapsed it stays elapsed. AVA-ANI-08: `Delay == 0`
// shows the part in the render that applies it. AVA-EDGE-05: a negative delay hides the first render and shows the
// part after it. The generated id is what the `dom` module hides at load (AVA-DOM-10 against ADR 04 Amendments
// 2026-10-18: chosen by the user 2026-10-21, the AVA-DOM-10 port note).
/// <summary>
/// Represents the content an avatar shows while its image is not loaded, such as initials or an icon: Base UI's
/// <c>Avatar.Fallback</c>, rendered as a <c>span</c>. It can wait before it appears, so that a fast load never shows it.
/// </summary>
public sealed class AvatarFallback : PartBase<AvatarFallbackState>
{
    private const string DefaultTag = "span";

    private static readonly StateAttributesMapping<AvatarFallbackState> s_mapping = new StateAttributesMapping<AvatarFallbackState>()
        .Map<ImageLoadingStatus>(nameof(AvatarFallbackState.ImageLoadingStatus), static _ => null);

    private static readonly AvatarFallbackState[] s_states =
    [
        new(ImageLoadingStatus.Idle),
        new(ImageLoadingStatus.Loading),
        new(ImageLoadingStatus.Loaded),
        new(ImageLoadingStatus.Error),
    ];

    private readonly Action onStatusChanged;
    private readonly Action onDelayElapsed;
    private AvatarRootContext? context;
    private AvatarFallbackDelay? delay;
    private string? registeredId;
    private double? startedDelay;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="AvatarFallback"/> class.</summary>
    public AvatarFallback()
    {
        onStatusChanged = StateHasChanged;
        onDelayElapsed = () => _ = InvokeAsync(MarkDelayElapsed);
    }

    [Inject]
    private IServiceProvider Services { get; set; } = null!;

    private AvatarRootContext Context => context!;

    private double DelayMilliseconds => Delay?.ToMilliseconds() ?? 0;

    private bool Shown => Context.Status != ImageLoadingStatus.Loaded && (DelayMilliseconds == 0 || delay!.Passed);

    [CascadingParameter]
    private AvatarRootContext? RootContext { get; set; }

    // AVA-API-07; Duration? (a union of milliseconds and TimeSpan), chosen by the user 2026-10-21 (ADR 08 §3 note).
    /// <summary>Gets or sets how long to wait before the fallback may show.</summary>
    /// <value>
    /// The delay, counted from the first render of the fallback, as a number of milliseconds or a <see cref="TimeSpan"/>.
    /// <see langword="null"/>, the default, and zero show the fallback at once; a negative value shows it right after its
    /// first render.
    /// </value>
    [Parameter]
    public Duration? Delay { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override AvatarFallbackState State => s_states[(int)Context.Status];

    /// <inheritdoc />
    protected override StateAttributesMapping<AvatarFallbackState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The fallback is not inside an <see cref="AvatarRoot"/>.</exception>
    protected override void OnInitialized()
    {
        context = AvatarRootContext.Require(RootContext, nameof(AvatarFallback));
        base.OnInitialized();
        TimeProvider timeProvider = Services.GetService<TimeProvider>() ?? TimeProvider.System;
        delay = new AvatarFallbackDelay(timeProvider, DelayMilliseconds, onDelayElapsed);
        context.AddStatusListener(onStatusChanged);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        string? id = Id;
        if (!string.Equals(id, registeredId, StringComparison.Ordinal))
        {
            Context.SetFallbackId(registeredId, id);
            registeredId = id;
        }
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
    protected override void OnAfterRender(bool firstRender)
    {
        double milliseconds = DelayMilliseconds;
        if (startedDelay == milliseconds)
        {
            return;
        }

        startedDelay = milliseconds;
        if (delay!.Restart(milliseconds))
        {
            StateHasChanged();
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            delay?.Dispose();
            if (context is not null)
            {
                context.RemoveStatusListener(onStatusChanged);
                context.SetFallbackId(registeredId, null);
            }
        }

        base.Dispose(disposing);
    }

    private void MarkDelayElapsed()
    {
        if (!disposed && delay!.MarkPassed())
        {
            StateHasChanged();
        }
    }
}
