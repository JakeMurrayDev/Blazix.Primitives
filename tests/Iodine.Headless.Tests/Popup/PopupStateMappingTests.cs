using Blazix.Primitives.Rendering;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests;

/// <summary>
/// The four shared state mappings at L1 (utils/popupStateMapping.test.ts, titles verbatim; PSH-DOM-01..04).
/// </summary>
public sealed class PopupStateMappingTests
{
    private static Dictionary<string, string>? Project(StateFieldProjection<bool> projection, bool value) =>
        projection(value)?.ToDictionary(attribute => attribute.Name, attribute => attribute.Value?.ToString() ?? string.Empty, StringComparer.Ordinal);

    [Fact(DisplayName = "emits the open and closed data attributes")]
    [Trait("Requirement", "PSH-DOM-03")]
    public void Emits_the_open_and_closed_data_attributes()
    {
        Assert.Equal(new Dictionary<string, string> { ["data-open"] = "" }, Project(PopupStateMapping.PopupOpen, true));
        Assert.Equal(new Dictionary<string, string> { ["data-closed"] = "" }, Project(PopupStateMapping.PopupOpen, false));
    }

    [Fact(DisplayName = "emits the anchor-hidden data attribute only when the anchor is hidden")]
    [Trait("Requirement", "PSH-DOM-04")]
    public void Emits_the_anchor_hidden_data_attribute_only_when_the_anchor_is_hidden()
    {
        Assert.Equal(new Dictionary<string, string> { ["data-anchor-hidden"] = "" }, Project(PopupStateMapping.AnchorHidden, true));
        Assert.Null(Project(PopupStateMapping.AnchorHidden, false));
    }

    [Fact(DisplayName = "emits the trigger data attributes only while open")]
    [Trait("Requirement", "PSH-DOM-01")]
    public void Emits_the_trigger_data_attributes_only_while_open()
    {
        Assert.Equal(new Dictionary<string, string> { ["data-popup-open"] = "" }, Project(PopupStateMapping.TriggerOpen, true));
        Assert.Null(Project(PopupStateMapping.TriggerOpen, false));
    }

    [Fact(DisplayName = "emits the pressed data attribute on pressable triggers only while open")]
    [Trait("Requirement", "PSH-DOM-02")]
    public void Emits_the_pressed_data_attribute_on_pressable_triggers_only_while_open()
    {
        Assert.Equal(new Dictionary<string, string> { ["data-popup-open"] = "", ["data-pressed"] = "" }, Project(PopupStateMapping.PressableTriggerOpen, true));
        Assert.Null(Project(PopupStateMapping.PressableTriggerOpen, false));
    }
}
