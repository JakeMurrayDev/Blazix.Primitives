namespace Blazix.Primitives.Interop;

// ADR 04 §1 (the `context-menu` component module) and Amendments 2026-10-18 and 2026-11-03 (4) (chosen by the user
// 2026-11-03); design points 1-3 of M5 track 7. Two handles per context menu, joined through the menu's interactions
// session token: the Root's, attached by token (no element), whose references are the consumer Backdrop, the internal
// backdrop and the root Positioner; and each Trigger's, on its element. Fields a part does not use are left null and so
// not sent. Handlers read the options of the latest update.
/// <summary>Holds the options of the <c>context-menu</c> module's handle.</summary>
internal sealed record ContextMenuOptions : TwinOptions
{
    /// <summary>Gets the part the handle is attached to, one of the <see cref="ContextMenuTwin"/> part names.</summary>
    public string Part { get; init; } = ContextMenuTwin.RootPart;

    /// <summary>Gets the token of the menu's interactions session, which both parts send.</summary>
    public string? Token { get; init; }

    /// <summary>Gets the id of the menu tree's root, which every popup of the tree renders as <c>data-rootownerid</c>; the Trigger's.</summary>
    public string? RootId { get; init; }

    /// <summary>Gets a value indicating whether the menu is disabled; the Trigger's.</summary>
    public bool? Disabled { get; init; }

    /// <summary>
    /// Gets the names of the events the consumer supplied a handler for on the Trigger, of <c>contextmenu</c>,
    /// <c>touchstart</c>, <c>touchmove</c>, <c>touchend</c> and <c>touchcancel</c>; the Trigger's.
    /// </summary>
    public IReadOnlyList<string>? Handlers { get; init; }

    /// <summary>Gets the index of the consumer Backdrop's element in the references; the Root's.</summary>
    public int? Backdrop { get; init; }

    /// <summary>Gets the index of the internal backdrop's element in the references; the Root's.</summary>
    public int? InternalBackdrop { get; init; }

    /// <summary>Gets the index of the root Positioner's element in the references; the Root's.</summary>
    public int? Positioner { get; init; }

    /// <inheritdoc />
    /// <remarks>The handler names are compared by their items, so an unchanged render compares equal and sends no <c>update</c>.</remarks>
    public bool Equals(ContextMenuOptions? other)
    {
        return other is not null
            && string.Equals(Style, other.Style, StringComparison.Ordinal)
            && string.Equals(Part, other.Part, StringComparison.Ordinal)
            && string.Equals(Token, other.Token, StringComparison.Ordinal)
            && string.Equals(RootId, other.RootId, StringComparison.Ordinal)
            && Disabled == other.Disabled
            && SameHandlers(Handlers, other.Handlers)
            && Backdrop == other.Backdrop
            && InternalBackdrop == other.InternalBackdrop
            && Positioner == other.Positioner;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hash = default;
        hash.Add(Style, StringComparer.Ordinal);
        hash.Add(Part, StringComparer.Ordinal);
        hash.Add(Token, StringComparer.Ordinal);
        hash.Add(RootId, StringComparer.Ordinal);
        hash.Add(Disabled);
        hash.Add(Handlers?.Count ?? -1);
        hash.Add(Backdrop);
        hash.Add(InternalBackdrop);
        hash.Add(Positioner);
        return hash.ToHashCode();
    }

    private static bool SameHandlers(IReadOnlyList<string>? a, IReadOnlyList<string>? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        if (a.Count != b.Count)
        {
            return false;
        }

        for (int i = 0; i < a.Count; i++)
        {
            if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
