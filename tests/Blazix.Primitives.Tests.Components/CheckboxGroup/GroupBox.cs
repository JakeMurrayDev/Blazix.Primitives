using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Components;

/// <summary>One box a <see cref="CheckboxGroupHost"/> renders, with the record of its own change callback.</summary>
public sealed class GroupBox(string testId)
{
    public string TestId { get; } = testId;

    public string? Name { get; init; }

    public string? Value { get; init; }

    public bool Parent { get; init; }

    public bool Disabled { get; set; }

    public bool Indeterminate { get; init; }

    public bool NativeButton { get; init; }

    public string? Id { get; init; }

    public RenderFragment? Render { get; init; }

    public bool Mounted { get; set; } = true;

    public bool Cancel { get; set; }

    public List<bool> Changes { get; } = [];

    public List<ChangeEventDetails> Details { get; } = [];
}
