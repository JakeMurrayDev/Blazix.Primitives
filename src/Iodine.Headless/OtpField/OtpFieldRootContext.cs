using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// OTP-STR-02, OTP-EDGE-01; otp-field/root/OTPFieldRootContext.ts. The context the root cascades fixed (ADR 16 rule 9): the
// slots read the root's state and parameters through it and re-render from its listeners, so a change reaches every slot in
// the root's render batch. The slot registry is the root's CompositeListBehavior, cascaded beside it.
/// <summary>The state and the parameters a one-time code field root shares with its slots.</summary>
internal sealed class OtpFieldRootContext
{
    private readonly List<Action> listeners = [];

    /// <summary>Initializes a new instance of the <see cref="OtpFieldRootContext"/> class.</summary>
    /// <param name="root">The root.</param>
    public OtpFieldRootContext(OtpFieldRoot root)
    {
        Root = root;
    }

    /// <summary>Gets the root, whose parameters and store the slots read.</summary>
    public OtpFieldRoot Root { get; }

    /// <summary>Gets the root's state, a new instance only when a field changed.</summary>
    public OtpFieldRootState State { get; private set; } = new(string.Empty, 0, false, false, false, null, false, false, false, false, false);

    /// <summary>Gets a counter that grows with every new <see cref="State"/>, so a slot rebuilds its own record only then.</summary>
    public int StateVersion { get; private set; }

    /// <summary>Throws the error of a part rendered outside a one-time code field root.</summary>
    /// <param name="context">The cascaded context, or <see langword="null"/>.</param>
    /// <returns>The context.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static OtpFieldRootContext Require(OtpFieldRootContext? context)
    {
        // OTP-EDGE-01, in every build; Base UI's words with the `Blazix: ` prefix and backticks (a dated deviation).
        return context ?? throw new InvalidOperationException(
            "Blazix: `OtpFieldRootContext` is missing. OtpField parts must be placed within `<OtpFieldRoot>`.");
    }

    /// <summary>Takes the root's new state and notifies the slots.</summary>
    /// <param name="state">The root's state; the caller passes a new instance only when a field changed.</param>
    public void SetState(OtpFieldRootState state)
    {
        State = state;
        StateVersion++;
        Notify();
    }

    /// <summary>Notifies the slots without a state change, as when the root's parameters that a slot renders changed.</summary>
    public void Notify()
    {
        // A listener may remove itself or another (a slot disposed by the render it triggers): iterating backwards with a
        // bounds check never skips a listener that is still registered.
        for (int i = listeners.Count - 1; i >= 0; i--)
        {
            if (i < listeners.Count)
            {
                listeners[i]();
            }
        }
    }

    /// <summary>Adds a listener that runs after every change the slots render.</summary>
    /// <param name="listener">The listener.</param>
    public void AddListener(Action listener) => listeners.Add(listener);

    /// <summary>Removes a listener added with <see cref="AddListener"/>.</summary>
    /// <param name="listener">The listener.</param>
    public void RemoveListener(Action listener) => listeners.Remove(listener);

    /// <summary>Gets the fragment that cascades this context and the slot registry to <paramref name="content"/>.</summary>
    /// <param name="slots">The slot registry.</param>
    /// <param name="content">The content that receives the contexts.</param>
    /// <returns>The fragment.</returns>
    internal RenderFragment Cascade(CompositeListContext slots, RenderFragment? content) => builder =>
    {
        builder.OpenComponent<CascadingValue<OtpFieldRootContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<OtpFieldRootContext>.Value), this);
        builder.AddComponentParameter(2, nameof(CascadingValue<OtpFieldRootContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<OtpFieldRootContext>.ChildContent), slots.Cascade(content));
        builder.CloseComponent();
    };
}
