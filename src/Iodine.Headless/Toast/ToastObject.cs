using Microsoft.AspNetCore.Components;

namespace Iodine.Headless;

// TST §3.2, the toast object (useToastManager.ts:26-107); TST-API-05, TST-API-11, TST-STA-09, TST-STA-27; ADR 08
// Amendments 2026-11-18 (the Toast bullet, chosen by the user 2026-11-18: ToastObject<TData> with the non-generic default
// over NoPayload, Title and Description as strings with an optional fragment, updates as a record `with`, Timeout a
// Duration?).
// The record is also the add input (TST-API-11's ToastManagerAddOptions) and the result of an update. Base UI spreads
// only the keys an options object carries (store.ts:183-188, 246-252), so every consumer property's init accessor sets
// its bit in a presence mask (M6 track 9a design point 6 as extended, pending the user): the store clears the mask on
// the copies it keeps, so a `with` on a stored toast carries exactly the keys it assigned, and an add input exactly the
// keys its initializer set. The mask is invisible to equality, hashing and printing (ToastFieldMask). The store-owned
// fields (transition status, limited, height, update key) have internal init accessors: a consumer can neither pass nor
// change them (TST-API-11), and the store ignores whatever an input carries for them.
/// <summary>Represents one toast: its content, its timing and the state the toast system keeps for it.</summary>
/// <typeparam name="TData">The type of the custom data the toast carries.</typeparam>
/// <remarks>
/// <para>
/// Pass an instance to <see cref="ToastManager{TData}.Add(ToastObject{TData})"/> to show a toast, setting only the
/// properties the toast needs. To change a toast, return a <c>with</c> expression from the function passed to
/// <see cref="ToastManager{TData}.Update(string, Func{ToastObject{TData}, ToastObject{TData}})"/>: the properties the
/// expression assigns are applied, and every other property keeps its current value. Adding a toast whose
/// <see cref="Id"/> is already shown updates it the same way, with the properties its initializer set.
/// </para>
/// <para>
/// <see cref="TransitionStatus"/>, <see cref="Limited"/>, <see cref="Height"/> and <see cref="UpdateKey"/> are kept by
/// the toast system and are read-only.
/// </para>
/// </remarks>
public record ToastObject<TData>
{
    private ToastFieldMask assigned;
    private string id = string.Empty;
    private string? title;
    private RenderFragment? titleContent;
    private string? description;
    private RenderFragment? descriptionContent;
    private string? type;
    private Duration? timeout;
    private ToastPriority? priority;
    private Action? onClose;
    private Action? onRemove;
    private IReadOnlyDictionary<string, object>? actionAttributes;
    private RenderFragment? actionContent;
    private ToastPositionerSettings? positioner;
    private TData? data;

    /// <summary>Gets the toast's unique id.</summary>
    /// <value>
    /// The id. When an empty string, the default, is added, the toast system generates one of the form
    /// <c>toast-&lt;four random characters&gt;-&lt;counter&gt;</c>; adding a toast whose id is already shown updates that toast.
    /// </value>
    public string Id
    {
        get => id;
        init => id = value ?? string.Empty;
    }

    /// <summary>Gets the toast's title.</summary>
    /// <value>
    /// The text rendered by the toast's title part, and announced for a high-priority toast; or <see langword="null"/>.
    /// </value>
    public string? Title
    {
        get => title;
        init
        {
            title = value;
            assigned.Fields |= ToastFields.Title;
        }
    }

    /// <summary>Gets rich content rendered by the toast's title part instead of <see cref="Title"/>.</summary>
    /// <value>The content, or <see langword="null"/> to render <see cref="Title"/>.</value>
    public RenderFragment? TitleContent
    {
        get => titleContent;
        init
        {
            titleContent = value;
            assigned.Fields |= ToastFields.TitleContent;
        }
    }

    /// <summary>Gets the toast's description.</summary>
    /// <value>
    /// The text rendered by the toast's description part, and announced for a high-priority toast; or
    /// <see langword="null"/>.
    /// </value>
    public string? Description
    {
        get => description;
        init
        {
            description = value;
            assigned.Fields |= ToastFields.Description;
        }
    }

    /// <summary>Gets rich content rendered by the toast's description part instead of <see cref="Description"/>.</summary>
    /// <value>The content, or <see langword="null"/> to render <see cref="Description"/>.</value>
    public RenderFragment? DescriptionContent
    {
        get => descriptionContent;
        init
        {
            descriptionContent = value;
            assigned.Fields |= ToastFields.DescriptionContent;
        }
    }

    /// <summary>Gets the toast's type, which the parts render as <c>data-type</c>.</summary>
    /// <value>
    /// A free-form type such as <c>"success"</c>, or <see langword="null"/>. A <c>"loading"</c> toast is never dismissed
    /// automatically.
    /// </value>
    public string? Type
    {
        get => type;
        init
        {
            type = value;
            assigned.Fields |= ToastFields.Type;
        }
    }

    /// <summary>Gets how long the toast stays before it is dismissed automatically.</summary>
    /// <value>
    /// The delay, or <see langword="null"/> (the default) for the provider's. A delay of zero keeps the toast until it is
    /// closed.
    /// </value>
    public Duration? Timeout
    {
        get => timeout;
        init
        {
            timeout = value;
            assigned.Fields |= ToastFields.Timeout;
        }
    }

    /// <summary>Gets how urgently the toast is announced.</summary>
    /// <value>The priority, or <see langword="null"/> (the default) for <see cref="ToastPriority.Low"/>.</value>
    public ToastPriority? Priority
    {
        get => priority;
        init
        {
            priority = value;
            assigned.Fields |= ToastFields.Priority;
        }
    }

    /// <summary>Gets the callback invoked when the toast starts closing.</summary>
    /// <value>The callback, or <see langword="null"/>.</value>
    public Action? OnClose
    {
        get => onClose;
        init
        {
            onClose = value;
            assigned.Fields |= ToastFields.OnClose;
        }
    }

    /// <summary>Gets the callback invoked when the toast is removed, after its exit animation.</summary>
    /// <value>The callback, or <see langword="null"/>.</value>
    public Action? OnRemove
    {
        get => onRemove;
        init
        {
            onRemove = value;
            assigned.Fields |= ToastFields.OnRemove;
        }
    }

    /// <summary>Gets the attributes applied to the toast's action button.</summary>
    /// <value>
    /// The attributes, which take precedence over those written on the action part, with handlers of the same event run
    /// together; or <see langword="null"/>.
    /// </value>
    public IReadOnlyDictionary<string, object>? ActionAttributes
    {
        get => actionAttributes;
        init
        {
            actionAttributes = value;
            assigned.Fields |= ToastFields.ActionAttributes;
        }
    }

    /// <summary>Gets the content of the toast's action button.</summary>
    /// <value>
    /// The content, which takes precedence over the action part's own; or <see langword="null"/>. An action part with no
    /// content renders nothing.
    /// </value>
    public RenderFragment? ActionContent
    {
        get => actionContent;
        init
        {
            actionContent = value;
            assigned.Fields |= ToastFields.ActionContent;
        }
    }

    /// <summary>Gets the positioning defaults of an anchored toast.</summary>
    /// <value>
    /// The defaults a toast positioner uses for every parameter it is not given, or <see langword="null"/>. A toast whose
    /// settings set an anchor, even to none, cannot be swiped away.
    /// </value>
    public ToastPositionerSettings? Positioner
    {
        get => positioner;
        init
        {
            positioner = value;
            assigned.Fields |= ToastFields.Positioner;
        }
    }

    /// <summary>Gets the toast's custom data.</summary>
    /// <value>The data, replaced as a whole by an update that assigns it; or the default value.</value>
    public TData? Data
    {
        get => data;
        init
        {
            data = value;
            assigned.Fields |= ToastFields.Data;
        }
    }

    /// <summary>Gets the toast's enter or exit phase.</summary>
    /// <value>
    /// <see cref="Iodine.Headless.TransitionStatus.Starting"/> from the add until the toast is first measured,
    /// <see cref="Iodine.Headless.TransitionStatus.Ending"/> from the close until the toast is removed, and
    /// <see langword="null"/> otherwise.
    /// </value>
    public TransitionStatus? TransitionStatus { get; internal init; }

    /// <summary>Gets a value indicating whether the toast is beyond the provider's limit.</summary>
    /// <value><see langword="true"/> while newer toasts fill the limit; the toast stays rendered but inert.</value>
    public bool Limited { get; internal init; }

    /// <summary>Gets the toast's measured height, in CSS pixels.</summary>
    /// <value>The height, <c>0</c> while the toast is closing, or <see langword="null"/> before it is measured.</value>
    public double? Height { get; internal init; }

    /// <summary>Gets a counter that increments whenever the toast is updated or added again under its id.</summary>
    /// <value>Zero for a new toast.</value>
    public int UpdateKey { get; internal init; }

    // The store's copy of a consumer's toast: the presence mask cleared, so a later `with` on it carries only what it assigns.
    internal ToastObject<TData> Stored(string storedId, int updateKey, TransitionStatus? status)
    {
        ToastObject<TData> stored = this with { Id = storedId, UpdateKey = updateKey, TransitionStatus = status };
        stored.assigned = default;
        return stored;
    }

    internal bool Assigns(ToastFields field) => (assigned.Fields & field) != 0;

    // TST-STA-09: `{ ...previous, ...updates }`, key by key. The store-owned fields stay this toast's, whatever the updates hold.
    internal ToastObject<TData> Apply(ToastObject<TData> updates, int updateKey)
    {
        ToastObject<TData> next = this with { UpdateKey = updateKey };
        next.assigned = default;
        ToastFields fields = updates.assigned.Fields;
        if (fields == 0)
        {
            return next;
        }

        if ((fields & ToastFields.Title) != 0)
        {
            next.title = updates.title;
        }

        if ((fields & ToastFields.TitleContent) != 0)
        {
            next.titleContent = updates.titleContent;
        }

        if ((fields & ToastFields.Description) != 0)
        {
            next.description = updates.description;
        }

        if ((fields & ToastFields.DescriptionContent) != 0)
        {
            next.descriptionContent = updates.descriptionContent;
        }

        if ((fields & ToastFields.Type) != 0)
        {
            next.type = updates.type;
        }

        if ((fields & ToastFields.Timeout) != 0)
        {
            next.timeout = updates.timeout;
        }

        if ((fields & ToastFields.Priority) != 0)
        {
            next.priority = updates.priority;
        }

        if ((fields & ToastFields.OnClose) != 0)
        {
            next.onClose = updates.onClose;
        }

        if ((fields & ToastFields.OnRemove) != 0)
        {
            next.onRemove = updates.onRemove;
        }

        if ((fields & ToastFields.ActionAttributes) != 0)
        {
            next.actionAttributes = updates.actionAttributes;
        }

        if ((fields & ToastFields.ActionContent) != 0)
        {
            next.actionContent = updates.actionContent;
        }

        if ((fields & ToastFields.Positioner) != 0)
        {
            next.positioner = updates.positioner;
        }

        if ((fields & ToastFields.Data) != 0)
        {
            next.data = updates.data;
        }

        return next;
    }
}

// ADR 08 Amendments 2026-11-18 (the non-generic default over NoPayload, chosen by the user 2026-11-18); its shape is
// M6 track 9a design point 5 (a derived record, so ToastRoot<TData> infers NoPayload through the base class).
/// <summary>Represents one toast that carries no custom data.</summary>
/// <remarks>This is the toast type of <see cref="ToastManager"/>.</remarks>
public sealed record ToastObject : ToastObject<NoPayload>;

// TST-STA-05, TST-STA-09, TST-STA-19: which keys an add input or an update assigned.
[Flags]
internal enum ToastFields
{
    None = 0,
    Title = 1 << 0,
    TitleContent = 1 << 1,
    Description = 1 << 2,
    DescriptionContent = 1 << 3,
    Type = 1 << 4,
    Timeout = 1 << 5,
    Priority = 1 << 6,
    OnClose = 1 << 7,
    OnRemove = 1 << 8,
    ActionAttributes = 1 << 9,
    ActionContent = 1 << 10,
    Positioner = 1 << 11,
    Data = 1 << 12,
}

// Always equal and hashed alike, so the record's synthesized equality and hash ignore which keys were assigned.
internal struct ToastFieldMask : IEquatable<ToastFieldMask>
{
    public ToastFields Fields;

    /// <inheritdoc />
    public readonly bool Equals(ToastFieldMask other) => true;

    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is ToastFieldMask;

    /// <inheritdoc />
    public override readonly int GetHashCode() => 0;
}
