using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// AVA-STA-01; avatar/root/AvatarRoot.tsx:46-51. ADR 08 §3: sealed, partial, [PartState]. AVA-DOM-05: the status is never
// projected (the mapping below).
/// <summary>Represents the state of an avatar root, which the part passes to its class and style functions.</summary>
/// <param name="ImageLoadingStatus">The status the avatar's image most recently reported; <see cref="Primitives.ImageLoadingStatus.Idle"/> when there is no image.</param>
[PartState]
public sealed partial record AvatarRootState(ImageLoadingStatus ImageLoadingStatus);

// Base UI's Avatar.Root (avatar/root/AvatarRoot.tsx); the contract is docs/components/avatar.md. AVA-STR-01, AVA-API-01,
// AVA-DOM-08: a span with the consumer's attributes only; the context is cascaded fixed inside it (ADR 16 rule 9). Under a
// substitute (Render or RenderFunction) the root draws a generated id, the one attribute it may add: the image's detached
// probe attaches to the root's element and needs to find it without an element reference (ADR 02 decision 1, Button's
// pattern; the preload attach point chosen by the user 2026-10-21, ADR 04 §3 note).
/// <summary>
/// Represents the container of an avatar: Base UI's <c>Avatar.Root</c>, rendered as a <c>span</c>. It holds an
/// <see cref="AvatarImage"/> and any number of <see cref="AvatarFallback"/> parts and shares the image's loading status
/// with them.
/// </summary>
public sealed class AvatarRoot : PartBase<AvatarRootState>
{
    private const string DefaultTag = "span";

    // AVA-DOM-05, avatar/root/stateAttributesMapping.ts.
    private static readonly StateAttributesMapping<AvatarRootState> s_mapping = new StateAttributesMapping<AvatarRootState>()
        .Map<ImageLoadingStatus>(nameof(AvatarRootState.ImageLoadingStatus), static _ => null);

    private static readonly AvatarRootState[] s_states =
    [
        new(ImageLoadingStatus.Idle),
        new(ImageLoadingStatus.Loading),
        new(ImageLoadingStatus.Loaded),
        new(ImageLoadingStatus.Error),
    ];

    private readonly AvatarRootContext context;
    private readonly Action onStatusChanged;
    private RenderFragment? cascadedContent;
    private RenderFragment? cascade;

    /// <summary>Initializes a new instance of the <see cref="AvatarRoot"/> class.</summary>
    public AvatarRoot()
    {
        context = new AvatarRootContext(this);
        onStatusChanged = StateHasChanged;
        context.AddStatusListener(onStatusChanged);
    }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override AvatarRootState State => s_states[(int)context.Status];

    /// <inheritdoc />
    protected override StateAttributesMapping<AvatarRootState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => Render is not null || RenderFunction is not null;

    // The element the image's probe attaches to: the element reference, else the id (a substitute's root).
    /// <summary>Gets the element that hosts the image's detached probe.</summary>
    /// <value>The root's element, or <see langword="null"/> when it has none.</value>
    internal ElementReference? HostElement => Element;

    /// <summary>Gets the id that locates the root's element when it has no element reference.</summary>
    /// <value>The consumer's or the generated id, or <see langword="null"/>.</value>
    internal string? HostId => Id;

    /// <inheritdoc />
    /// <remarks>The child content of the previous render is not carried over: the root replaces it with the cascade of its context.</remarks>
    public override Task SetParametersAsync(ParameterView parameters)
    {
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // The cascade wraps the consumer's content inside the root's element; it is rebuilt only when the content changes.
        if (cascade is null || !ReferenceEquals(cascadedContent, ChildContent))
        {
            cascadedContent = ChildContent;
            cascade = context.Cascade(cascadedContent);
        }

        ChildContent = cascade;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            context.RemoveStatusListener(onStatusChanged);
        }

        base.Dispose(disposing);
    }
}
