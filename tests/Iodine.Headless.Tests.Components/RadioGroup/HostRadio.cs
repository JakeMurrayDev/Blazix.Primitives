namespace Blazix.Primitives.Tests.Components;

/// <summary>One radio a <see cref="RadioGroupFieldHost"/> renders.</summary>
public sealed class HostRadio(string testId, string? value = null)
{
    public string TestId { get; } = testId;

    public string? Value { get; } = value ?? testId;

    public bool Disabled { get; set; }

    public bool ItemDisabled { get; set; }

    public string? Id { get; init; }

    public string? DescribedBy { get; init; }

    public bool Mounted { get; set; } = true;
}
