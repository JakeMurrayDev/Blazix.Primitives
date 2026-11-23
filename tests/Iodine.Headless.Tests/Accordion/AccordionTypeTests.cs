using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Accordion;

/// <summary>
/// docs/components/accordion.md §3.6's <c>[L1]</c> line, <c>AccordionRoot.spec.tsx</c> (type assertions only), as compile-time
/// checks (the <c>CollapsibleTypeTests</c> precedent): the value type flows from <c>TValue</c> to <c>OnValueChange</c> for
/// <c>string</c>, <c>int</c>, an enum standing for a literal union and <c>string?</c> with <c>null</c>; the state records, the
/// data attribute and CSS variable names. The compiler checks the types; the assertions pin the names.
/// </summary>
public sealed class AccordionTypeTests
{
    private enum Section
    {
        A,
        B,
    }

    [Fact(DisplayName = "infers the value type of onValueChange from the root's TValue")]
    [Trait("Requirement", "ACC-API-01")]
    [Trait("Requirement", "ACC-API-03")]
    public void Infers_the_value_type_of_onValueChange_from_the_roots_TValue()
    {
        IReadOnlyList<string> strings = Changed<string>(["a"]);
        IReadOnlyList<int> numbers = Changed<int>([1]);
        IReadOnlyList<Section> union = Changed<Section>([Section.A, Section.B]);
        IReadOnlyList<string?> nullable = Changed<string?>([null, "a"]);

        Assert.Equal(["a"], strings);
        Assert.Equal([1], numbers);
        Assert.Equal([Section.A, Section.B], union);
        Assert.Equal([null, "a"], nullable);

        static IReadOnlyList<TValue> Changed<TValue>(IReadOnlyList<TValue> value)
        {
            AccordionRoot<TValue> root = new();
            EventCallback<ValueChangeEventArgs<IReadOnlyList<TValue>>> onValueChange = root.OnValueChange;
            EventCallback<IReadOnlyList<TValue>> valueChanged = root.ValueChanged;
            IReadOnlyList<TValue>? controlled = root.Value;
            ValueChangeEventArgs<IReadOnlyList<TValue>> args = new(controlled ?? value, new ChangeEventDetails(EventReason.TriggerPress));
            Assert.False(valueChanged.HasDelegate);
            Assert.False(onValueChange.HasDelegate);
            return args.Value;
        }
    }

    [Fact(DisplayName = "exports the state, attribute and variable names of the five parts")]
    [Trait("Requirement", "ACC-STA-01")]
    [Trait("Requirement", "ACC-STA-02")]
    [Trait("Requirement", "ACC-STA-03")]
    public void Exports_the_state_attribute_and_variable_names_of_the_five_parts()
    {
        IReadOnlyList<string> value = ["a"];
        IPartState<AccordionRootState<string>> root = new AccordionRootState<string>(value, Disabled: false, Orientation.Vertical);
        IPartState<AccordionItemState<string>> item = new AccordionItemState<string>(value, false, Orientation.Vertical, Hidden: false, Index: 0, Open: true);
        IPartState<AccordionHeaderState<string>> header = new AccordionHeaderState<string>(value, false, Orientation.Vertical, false, 0, true);
        IPartState<AccordionTriggerState<string>> trigger = new AccordionTriggerState<string>(value, false, Orientation.Vertical, false, 0, true);
        AccordionPanelState<string> panel = new(value, false, Orientation.Vertical, false, 0, true, TransitionStatus.Idle);

        Assert.NotNull(root);
        Assert.NotNull(item);
        Assert.NotNull(header);
        Assert.NotNull(trigger);
        Assert.Same(value, panel.Value);
        Assert.Equal(["data-disabled", "data-orientation"], [AccordionRootDataAttributes.Disabled, AccordionRootDataAttributes.Orientation]);
        Assert.Equal(["data-index", "data-disabled", "data-open"], [AccordionItemDataAttributes.Index, AccordionItemDataAttributes.Disabled, AccordionItemDataAttributes.Open]);
        Assert.Equal(["data-index", "data-disabled", "data-open"], [AccordionHeaderDataAttributes.Index, AccordionHeaderDataAttributes.Disabled, AccordionHeaderDataAttributes.Open]);
        Assert.Equal(["data-index", "data-panel-open", "data-disabled"], [AccordionTriggerDataAttributes.Index, AccordionTriggerDataAttributes.PanelOpen, AccordionTriggerDataAttributes.Disabled]);
        Assert.Equal(
            ["data-index", "data-open", "data-orientation", "data-disabled", "data-starting-style", "data-ending-style"],
            [AccordionPanelDataAttributes.Index, AccordionPanelDataAttributes.Open, AccordionPanelDataAttributes.Orientation, AccordionPanelDataAttributes.Disabled, AccordionPanelDataAttributes.StartingStyle, AccordionPanelDataAttributes.EndingStyle]);
        Assert.Equal(["--accordion-panel-height", "--accordion-panel-width"], [AccordionPanelCssVars.AccordionPanelHeight, AccordionPanelCssVars.AccordionPanelWidth]);
    }
}
