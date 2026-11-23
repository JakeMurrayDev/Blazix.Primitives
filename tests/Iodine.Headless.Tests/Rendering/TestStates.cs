using Blazix.Primitives.Rendering;

namespace Blazix.Primitives.Tests.Rendering;

public enum Orientation
{
    Horizontal,
    Vertical,
}

/// <summary>The state of <c>getStateAttributesProps.test.ts</c>: <c>{ checked, orientation, count }</c>.</summary>
[PartState]
public sealed partial record CheckedState(bool Checked, Orientation Orientation, int Count);

/// <summary><c>{ readOnly }</c>.</summary>
[PartState]
public sealed partial record ReadOnlyState(bool ReadOnly);

/// <summary><c>{ required, disabled }</c>.</summary>
[PartState]
public sealed partial record RequiredState(bool Required, bool Disabled);

/// <summary><c>{ checked, orientation }</c> with an unmapped enum for the null-mapping case.</summary>
[PartState]
public sealed partial record MappedState(bool Checked, Orientation Orientation);

/// <summary><c>{ defined, notDefined }</c>.</summary>
[PartState]
public sealed partial record DefinedState(string? Defined, string? NotDefined);

/// <summary><c>{ count, index, percentage }</c>.</summary>
[PartState]
public sealed partial record NumberState(int Count, int Index, double Percentage);

/// <summary><c>{ isActive, itemCount, userName }</c>.</summary>
[PartState]
public sealed partial record MappingState(bool IsActive, int ItemCount, string? UserName);
