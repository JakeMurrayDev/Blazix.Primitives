using Blazix.Primitives.Store;

namespace Blazix.Primitives;

/// <summary>
/// Base UI's <c>useValueChanged</c> (EVD-ALG-09, UTL-STA-36): call <see cref="Observe"/> with the committed value after
/// every render; it calls the handler with the previously recorded value when the value changed. The check runs only when
/// the value differs from the recorded one by <c>Object.is</c>, and the handler fires only on strict inequality, so
/// <c>0 → -0</c> records <c>-0</c> silently and <c>NaN → NaN</c> does nothing.
/// </summary>
internal sealed class ValueChangedObserver<T>
{
    private readonly Action<T> onChange;
    private T recorded;

    public ValueChangedObserver(T initialValue, Action<T> onChange)
    {
        ArgumentNullException.ThrowIfNull(onChange);
        recorded = initialValue;
        this.onChange = onChange;
    }

    /// <summary>Compares <paramref name="value"/> with the recorded value, notifies, and records it.</summary>
    public void Observe(T value)
    {
        // Base UI's effect re-runs only when the value changed by Object.is (its dependency list), then compares with !==.
        if (Equality.Is(recorded, value))
        {
            return;
        }

        if (!Equality.StrictEquals(recorded, value))
        {
            onChange(recorded);
        }

        recorded = value;
    }
}
