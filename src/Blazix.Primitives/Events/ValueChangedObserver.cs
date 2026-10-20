using Blazix.Primitives.Store;

namespace Blazix.Primitives;

// EVD-ALG-09, UTL-STA-36
/// <summary>
/// Provides Base UI's <c>useValueChanged</c>: calls a handler with the previous committed value whenever the committed value changes.
/// </summary>
/// <typeparam name="T">The type of the value.</typeparam>
/// <remarks>
/// Call <see cref="Observe"/> with the committed value after every render. The check runs only when the value differs from
/// the recorded one by <c>Object.is</c>, and the handler runs only on strict inequality, so <c>0 → -0</c> records <c>-0</c>
/// silently and <c>NaN → NaN</c> does nothing.
/// </remarks>
internal sealed class ValueChangedObserver<T>
{
    private readonly Action<T> onChange;
    private T recorded;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValueChangedObserver{T}"/> class.
    /// </summary>
    /// <param name="initialValue">The value committed at the first render.</param>
    /// <param name="onChange">The handler, called with the previously recorded value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="onChange"/> is <see langword="null"/>.</exception>
    public ValueChangedObserver(T initialValue, Action<T> onChange)
    {
        ArgumentNullException.ThrowIfNull(onChange);
        recorded = initialValue;
        this.onChange = onChange;
    }

    /// <summary>
    /// Compares <paramref name="value"/> with the recorded value, calls the handler when it changed, and records it.
    /// </summary>
    /// <param name="value">The value committed by the latest render.</param>
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
