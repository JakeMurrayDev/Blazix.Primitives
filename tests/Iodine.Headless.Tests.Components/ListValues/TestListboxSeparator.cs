using Blazix.Primitives.Rendering;
using Blazix.Primitives.Utilities;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Components;

/// <summary>The state of <see cref="TestListboxSeparator"/>: ListboxSeparator's <c>{ orientation }</c>.</summary>
/// <param name="Orientation">The separator's orientation, projected as <c>data-orientation</c>.</param>
[PartState]
public sealed partial record TestListboxSeparatorState(Orientation Orientation);

/// <summary>
/// A test-only listbox separator (RND-STR-11, API-13): a <c>div</c> over <see cref="ListboxSeparatorAttributes"/>, as
/// <c>Select.Separator</c>, <c>Combobox.Separator</c> and <c>Autocomplete.Separator</c> compose it in wave 2.
/// </summary>
public sealed class TestListboxSeparator : PartBase<TestListboxSeparatorState>
{
    private TestListboxSeparatorState? state;

    [Parameter]
    public Orientation Orientation { get; set; } = Orientation.Horizontal;

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override TestListboxSeparatorState State => state!;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (state is null || state.Orientation != Orientation)
        {
            state = new TestListboxSeparatorState(Orientation);
        }
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => ListboxSeparatorAttributes.Write(attributes);
}
