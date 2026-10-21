using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// AVA-STR-01/02, AVA-STA-05/06, AVA-EDGE-01/04; avatar/root/AvatarRootContext.ts. Cascaded fixed by AvatarRoot (ADR 16
// rule 9): a change reaches the Root and the Fallbacks through the subscriptions below, never through the cascade.
/// <summary>The state an avatar root shares with its image and fallback parts.</summary>
internal sealed class AvatarRootContext
{
    private readonly List<Action> statusListeners = [];

    /// <summary>Gets the status the image most recently reported.</summary>
    /// <value><see cref="ImageLoadingStatus.Idle"/> initially and after an image is removed.</value>
    public ImageLoadingStatus Status { get; private set; }

    /// <summary>Sets the status and notifies the status listeners when it changed.</summary>
    /// <param name="status">The new status.</param>
    public void SetStatus(ImageLoadingStatus status)
    {
        if (status == Status)
        {
            return;
        }

        Status = status;
        Notify(statusListeners);
    }

    /// <summary>Adds a listener that runs after every change of <see cref="Status"/>.</summary>
    /// <param name="listener">The listener.</param>
    public void AddStatusListener(Action listener) => statusListeners.Add(listener);

    /// <summary>Removes a listener added with <see cref="AddStatusListener"/>.</summary>
    /// <param name="listener">The listener.</param>
    public void RemoveStatusListener(Action listener) => statusListeners.Remove(listener);

    /// <summary>Throws the error of a part rendered outside an avatar root.</summary>
    /// <param name="context">The cascaded context, or <see langword="null"/>.</param>
    /// <param name="part">The name of the part.</param>
    /// <returns>The context.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static AvatarRootContext Require(AvatarRootContext? context, string part)
    {
        // AVA-EDGE-01, in every build; the port's words.
        return context ?? throw new InvalidOperationException(
            $"Blazix: `{part}` must be placed within an `AvatarRoot`. The avatar parts read the image loading status from the root.");
    }

    /// <summary>Gets the fragment that cascades this context to <paramref name="content"/>.</summary>
    /// <param name="content">The content that receives the context.</param>
    /// <returns>The fragment.</returns>
    internal RenderFragment Cascade(RenderFragment? content) => builder =>
    {
        builder.OpenComponent<CascadingValue<AvatarRootContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<AvatarRootContext>.Value), this);
        builder.AddComponentParameter(2, nameof(CascadingValue<AvatarRootContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<AvatarRootContext>.ChildContent), content);
        builder.CloseComponent();
    };

    private static void Notify(List<Action> listeners)
    {
        // A listener may remove itself or another (a part disposed by the render it triggers): iterating backwards with a
        // bounds check never skips a listener that is still registered.
        for (int i = listeners.Count - 1; i >= 0; i--)
        {
            if (i < listeners.Count)
            {
                listeners[i]();
            }
        }
    }
}
