using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Collapsible;

/// <summary>
/// docs/components/collapsible.md §3.4's <c>[L1]</c> line, <c>CollapsibleRoot.spec.tsx</c> (type assertions only): the
/// state records, the change callback's argument and details, the data attribute and CSS variable names. The compiler
/// checks the types; the assertions pin the names.
/// </summary>
public sealed class CollapsibleTypeTests
{
    [Fact(DisplayName = "exports the state, change details and attribute names of the three parts")]
    [Trait("Requirement", "COL-STA-07")]
    public void Exports_the_state_change_details_and_attribute_names_of_the_three_parts()
    {
        CollapsibleRootState rootState = new(Open: true, Disabled: false, TransitionStatus: TransitionStatus.Idle);
        CollapsibleTriggerState triggerState = new(Open: true, Disabled: false, TransitionStatus: null);
        CollapsiblePanelState panelState = new(Open: false, Disabled: true, TransitionStatus: TransitionStatus.Ending);
        IPartState<CollapsibleRootState> projected = rootState;

        EventCallback<ValueChangeEventArgs<bool>> onOpenChange = default;
        ValueChangeEventArgs<bool> args = new(true, new ChangeEventDetails(EventReason.TriggerPress));
        bool open = args.Value;
        ChangeEventDetails details = args.Details;

        Assert.NotNull(projected);
        Assert.False(onOpenChange.HasDelegate);
        Assert.True(open);
        Assert.Equal(EventReason.TriggerPress, details.Reason);
        Assert.Equal((true, false, (TransitionStatus?)null), (triggerState.Open, triggerState.Disabled, triggerState.TransitionStatus));
        Assert.Equal(TransitionStatus.Ending, panelState.TransitionStatus);
        Assert.Equal(["data-open", "data-closed", "data-starting-style", "data-ending-style"], [CollapsibleRootDataAttributes.Open, CollapsibleRootDataAttributes.Closed, CollapsibleRootDataAttributes.StartingStyle, CollapsibleRootDataAttributes.EndingStyle]);
        Assert.Equal("data-panel-open", CollapsibleTriggerDataAttributes.PanelOpen);
        Assert.Equal(["data-open", "data-closed", "data-starting-style", "data-ending-style"], [CollapsiblePanelDataAttributes.Open, CollapsiblePanelDataAttributes.Closed, CollapsiblePanelDataAttributes.StartingStyle, CollapsiblePanelDataAttributes.EndingStyle]);
        Assert.Equal(["--collapsible-panel-height", "--collapsible-panel-width"], [CollapsiblePanelCssVars.CollapsiblePanelHeight, CollapsiblePanelCssVars.CollapsiblePanelWidth]);
    }
}
