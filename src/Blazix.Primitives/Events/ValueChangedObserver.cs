namespace Blazix.Primitives;

/// <summary>
/// Base UI's <c>useValueChanged</c> (EVD-ALG-09, UTL-STA-36): call <see cref="Observe"/> with the committed value after
/// every render; it calls the handler with the previously recorded value when the value changed. The check runs only when
/// the value differs from the recorded one by <c>Object.is</c>, and the handler fires only on strict inequality, so
/// <c>0 → -0</c> records <c>-0</c> silently and <c>NaN → NaN</c> does nothing.
/// </summary>
internal sealed class ValueChangedObserver<T>
{
    public ValueChangedObserver(T initialValue, Action<T> onChange)
    {
        throw new NotImplementedException();
    }

    /// <summary>Compares <paramref name="value"/> with the recorded value, notifies, and records it.</summary>
    public void Observe(T value)
    {
        throw new NotImplementedException();
    }
}
