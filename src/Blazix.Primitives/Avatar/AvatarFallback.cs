using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives;

// AVA-STA-03; avatar/fallback/AvatarFallback.tsx:52-66. AVA-DOM-05: the status is never projected.
/// <summary>Represents the state of an avatar fallback, which the part passes to its class and style functions.</summary>
/// <param name="ImageLoadingStatus">The status of the avatar's image, as its root holds it.</param>
[PartState]
public sealed partial record AvatarFallbackState(ImageLoadingStatus ImageLoadingStatus);

// Base UI's Avatar.Fallback (avatar/fallback/AvatarFallback.tsx); the contract is docs/components/avatar.md.
// AVA-DOM-09: rendered exactly while the root's status is not `loaded`; no transition phase.
/// <summary>
/// Represents the content an avatar shows while its image is not loaded, such as initials or an icon: Base UI's
/// <c>Avatar.Fallback</c>, rendered as a <c>span</c>.
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
    private AvatarRootContext? context;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="AvatarFallback"/> class.</summary>
    public AvatarFallback()
    {
        onStatusChanged = StateHasChanged;
    }

    private AvatarRootContext Context => context!;

    private bool Shown => Context.Status != ImageLoadingStatus.Loaded;

    [CascadingParameter]
    private AvatarRootContext? RootContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override AvatarFallbackState State => s_states[(int)Context.Status];

    /// <inheritdoc />
    protected override StateAttributesMapping<AvatarFallbackState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The fallback is not inside an <see cref="AvatarRoot"/>.</exception>
    protected override void OnInitialized()
    {
        context = AvatarRootContext.Require(RootContext, nameof(AvatarFallback));
        base.OnInitialized();
        context.AddStatusListener(onStatusChanged);
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
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            context?.RemoveStatusListener(onStatusChanged);
        }

        base.Dispose(disposing);
    }
}
