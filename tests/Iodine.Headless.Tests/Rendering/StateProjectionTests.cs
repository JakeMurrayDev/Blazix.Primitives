using Blazix.Primitives.Rendering;

namespace Blazix.Primitives.Tests.Rendering;

/// <summary>
/// The generated state projection (RND-ALG-07, RND-DOM-01..07), with the titles of
/// <c>internals/getStateAttributesProps.test.ts</c> and the [L1] titles of <c>use-render/useRender.test.tsx</c>.
/// </summary>
public sealed class StateProjectionTests
{
    [Fact(DisplayName = "converts the state fields to data attributes")]
    [Trait("Requirement", "RND-DOM-03")]
    public void Converts_the_state_fields_to_data_attributes()
    {
        AttributeList result = Project(new CheckedState(Checked: true, Orientation.Vertical, Count: 42));

        Assert.Equal([("data-checked", ""), ("data-orientation", "vertical"), ("data-count", "42")], Pairs(result));
    }

    [Fact(DisplayName = "changes the fields names to lowercase")]
    [Trait("Requirement", "RND-DOM-05")]
    public void Changes_the_fields_names_to_lowercase()
    {
        AttributeList result = Project(new ReadOnlyState(ReadOnly: true));

        Assert.Equal([("data-readonly", "")], Pairs(result));
    }

    [Fact(DisplayName = "changes true values to a data-attribute without a value")]
    [Trait("Requirement", "RND-DOM-01")]
    public void Changes_true_values_to_a_data_attribute_without_a_value()
    {
        AttributeList result = Project(new RequiredState(Required: true, Disabled: false));

        Assert.Equal([("data-required", "")], Pairs(result));
    }

    [Fact(DisplayName = "does not include false values")]
    [Trait("Requirement", "RND-DOM-02")]
    public void Does_not_include_false_values()
    {
        AttributeList result = Project(new RequiredState(Required: true, Disabled: false));

        Assert.False(result.Contains("data-disabled"));
    }

    [Fact(DisplayName = "supports custom mapping")]
    [Trait("Requirement", "RND-DOM-06")]
    public void Supports_custom_mapping()
    {
        StateDataAttribute[] isChecked = [new("data-state", "checked")];
        StateDataAttribute[] isUnchecked = [new("data-state", "unchecked")];
        StateAttributesMapping<CheckedState> mapping = new StateAttributesMapping<CheckedState>()
            .Map<bool>(nameof(CheckedState.Checked), value => value ? isChecked : isUnchecked);

        AttributeList result = Project(new CheckedState(Checked: true, Orientation.Vertical, Count: 42), mapping);

        Assert.Equal([("data-state", "checked"), ("data-orientation", "vertical"), ("data-count", "42")], Pairs(result));
    }

    [Fact(DisplayName = "supports nulls returned from custom mapping")]
    [Trait("Requirement", "RND-DOM-07")]
    public void Supports_nulls_returned_from_custom_mapping()
    {
        StateDataAttribute[] isChecked = [new("data-state", "checked")];
        StateAttributesMapping<MappedState> mapping = new StateAttributesMapping<MappedState>()
            .Map<bool>(nameof(MappedState.Checked), value => value ? isChecked : null);

        AttributeList result = Project(new MappedState(Checked: false, Orientation.Vertical), mapping);

        Assert.Equal([("data-orientation", "vertical")], Pairs(result));
    }

    [Fact(DisplayName = "converts boolean values in state to data attributes")]
    [Trait("Requirement", "RND-DOM-01")]
    public void Converts_boolean_values_in_state_to_data_attributes()
    {
        AttributeList result = Project(new RequiredState(Required: true, Disabled: false));

        Assert.True(result.TryGetValue("data-required", out AttributeValue required) && required.TryGetValue(out string text) && text == "");
        Assert.False(result.Contains("data-disabled"));
    }

    [Fact(DisplayName = "handles undefined values in state")]
    [Trait("Requirement", "RND-DOM-04")]
    public void Handles_undefined_values_in_state()
    {
        AttributeList result = Project(new DefinedState(Defined: "value", NotDefined: null));

        Assert.Equal([("data-defined", "value")], Pairs(result));
    }

    [Fact(DisplayName = "converts number values in state to data attributes")]
    [Trait("Requirement", "RND-DOM-03")]
    public void Converts_number_values_in_state_to_data_attributes()
    {
        AttributeList result = Project(new NumberState(Count: 0, Index: 42, Percentage: 99.9));

        Assert.Equal([("data-index", "42"), ("data-percentage", "99.9")], Pairs(result));
    }

    [Fact(DisplayName = "supports custom stateAttributesMapping for kebab-case conversion")]
    [Trait("Requirement", "RND-DOM-06")]
    public void Supports_custom_stateAttributesMapping_for_kebab_case_conversion()
    {
        StateDataAttribute[] isActive = [new("data-is-active", "")];
        StateAttributesMapping<MappingState> mapping = new StateAttributesMapping<MappingState>()
            .Map<bool>(nameof(MappingState.IsActive), value => value ? isActive : null)
            .Map<int>(nameof(MappingState.ItemCount), value => [new StateDataAttribute("data-item-count", value.ToString())])
            .Map<string?>(nameof(MappingState.UserName), value => [new StateDataAttribute("data-user-name", value ?? "")]);

        AttributeList result = Project(new MappingState(IsActive: true, ItemCount: 5, UserName: "John"), mapping);

        Assert.Equal([("data-is-active", ""), ("data-item-count", "5"), ("data-user-name", "John")], Pairs(result));
    }

    [Fact(DisplayName = "a later mapping result wins on an attribute name clash")]
    [Trait("Requirement", "RND-DOM-09")]
    public void A_later_mapping_result_wins_on_an_attribute_name_clash()
    {
        StateDataAttribute[] fromChecked = [new("data-shared", "from-checked")];
        StateDataAttribute[] fromOrientation = [new("data-shared", "from-orientation")];
        StateAttributesMapping<MappedState> mapping = new StateAttributesMapping<MappedState>()
            .Map<bool>(nameof(MappedState.Checked), _ => fromChecked)
            .Map<Orientation>(nameof(MappedState.Orientation), _ => fromOrientation);

        AttributeList result = Project(new MappedState(Checked: true, Orientation.Horizontal), mapping);

        Assert.Equal([("data-shared", "from-orientation")], Pairs(result));
    }

    private static AttributeList Project<TState>(TState state, StateAttributesMapping<TState>? mapping = null)
        where TState : class, IPartState<TState>
    {
        AttributeList list = new();
        state.ProjectStateAttributes(mapping, new StateAttributeWriter(list));
        return list;
    }

    private static (string, string)[] Pairs(AttributeList list)
    {
        (string, string)[] pairs = new (string, string)[list.Count];
        for (int i = 0; i < list.Count; i++)
        {
            Assert.True(list[i].Value.TryGetValue(out string text), $"{list[i].Name} is not a string");
            pairs[i] = (list[i].Name, text);
        }

        return pairs;
    }
}
