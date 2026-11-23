namespace Iodine.Headless;

// NUM-API-06: the `'any'` case of StepValue, a case type as AutoHighlightAlways is for AutoHighlightValue.
/// <summary>Represents the <c>any</c> step of a number field, which steps by 1 and turns off the browser's step validation.</summary>
/// <remarks>Use <see cref="StepValue.Any"/> to pass it.</remarks>
public readonly struct StepAny : IEquatable<StepAny>
{
    /// <summary>Determines whether two literals are equal; they always are.</summary>
    /// <param name="left">The first literal.</param>
    /// <param name="right">The second literal.</param>
    /// <returns><see langword="true"/>.</returns>
    public static bool operator ==(StepAny left, StepAny right) => left.Equals(right);

    /// <summary>Determines whether two literals differ; they never do.</summary>
    /// <param name="left">The first literal.</param>
    /// <param name="right">The second literal.</param>
    /// <returns><see langword="false"/>.</returns>
    public static bool operator !=(StepAny left, StepAny right) => !left.Equals(right);

    /// <inheritdoc />
    public bool Equals(StepAny other) => true;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is StepAny;

    /// <inheritdoc />
    public override int GetHashCode() => 0;

    /// <inheritdoc />
    public override string ToString() => "any";
}
