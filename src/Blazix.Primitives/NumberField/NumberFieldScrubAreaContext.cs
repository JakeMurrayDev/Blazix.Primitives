using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// NUM-STR-03, NUM-STR-05, NUM-STR-06, NUM-EDGE-02; number-field/scrub-area/NumberFieldScrubAreaContext.ts. What one scrub
// area shares with its cursor: its own scrubbing state (several areas per root, each with its own), whether the pointer can
// have a virtual cursor (Base UI's `isTouchInput` folded with the WebKit check the cursor makes), the pointer-lock denial,
// and its element, which the cursor's handle receives as its reference. Cascaded fixed; the cursor re-renders from the
// listener.
/// <summary>The scrubbing state a number field scrub area shares with its cursor.</summary>
internal sealed class NumberFieldScrubAreaContext
{
    private Action? listener;

    /// <summary>Gets a value indicating whether the scrub area is being scrubbed.</summary>
    public bool IsScrubbing { get; private set; }

    /// <summary>Gets a value indicating whether the scrubbing pointer can have a virtual cursor: neither touch nor WebKit.</summary>
    public bool CursorAllowed { get; private set; }

    /// <summary>Gets a value indicating whether the last pointer-lock request was denied.</summary>
    public bool IsPointerLockDenied { get; private set; }

    /// <summary>Gets or sets the scrub area's element.</summary>
    public ElementReference? Element { get; set; }

    /// <summary>Throws the error of a part rendered outside a number field scrub area.</summary>
    /// <param name="context">The cascaded context, or <see langword="null"/>.</param>
    /// <returns>The context.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static NumberFieldScrubAreaContext Require(NumberFieldScrubAreaContext? context)
    {
        // NUM-EDGE-02, in every build; Base UI's words with the `Blazix: ` prefix (the AlertDialog and Select precedent).
        return context ?? throw new InvalidOperationException(
            "Blazix: `NumberFieldScrubAreaContext` is missing. NumberFieldScrubArea parts must be placed within `<NumberFieldScrubArea>`.");
    }

    /// <summary>Sets the listener the cursor renders from, or <see langword="null"/> to remove it.</summary>
    /// <param name="next">The listener.</param>
    public void SetListener(Action? next) => listener = next;

    /// <summary>Records the start or the end of a scrub.</summary>
    /// <param name="scrubbing"><see langword="true"/> at the start.</param>
    /// <param name="cursorAllowed">Whether the pointer can have a virtual cursor; kept at the end.</param>
    public void SetScrubbing(bool scrubbing, bool cursorAllowed)
    {
        if (IsScrubbing != scrubbing || CursorAllowed != cursorAllowed)
        {
            IsScrubbing = scrubbing;
            CursorAllowed = cursorAllowed;
            listener?.Invoke();
        }
    }

    /// <summary>Records the outcome of a pointer-lock request.</summary>
    /// <param name="denied">Whether it was denied.</param>
    public void SetPointerLockDenied(bool denied)
    {
        if (IsPointerLockDenied != denied)
        {
            IsPointerLockDenied = denied;
            listener?.Invoke();
        }
    }

    /// <summary>Renders the cascade of this context around <paramref name="content"/>.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="content">The content that receives the context.</param>
    internal void RenderCascade(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder, RenderFragment? content)
    {
        builder.OpenComponent<CascadingValue<NumberFieldScrubAreaContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<NumberFieldScrubAreaContext>.Value), this);
        builder.AddComponentParameter(2, nameof(CascadingValue<NumberFieldScrubAreaContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<NumberFieldScrubAreaContext>.ChildContent), content);
        builder.CloseComponent();
    }
}
