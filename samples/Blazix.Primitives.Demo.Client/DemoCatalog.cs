using Blazix.Primitives.Demo.Client.Demos;

namespace Blazix.Primitives.Demo.Client;

/// <summary>One demo page: the address segment, the heading, one line about it and the component the host renders.</summary>
public sealed record DemoEntry(string Slug, string Title, string Summary, Type Component);

/// <summary>Every family with a demo page, in the order the pages list them.</summary>
public static class DemoCatalog
{
    public static IReadOnlyList<DemoEntry> Entries { get; } =
    [
        new("separator", "Separator", "A separator between the links of a navigation bar.", typeof(SeparatorDemo)),
        new("meter", "Meter", "A graphical display of a numeric value within a known range.", typeof(MeterDemo)),
        new("progress", "Progress", "The progress of a task, here a simulated export.", typeof(ProgressDemo)),
        new("button", "Button", "A button that works as a native button or as another element.", typeof(ButtonDemo)),
        new("avatar", "Avatar", "A user's image with a fallback of their initials.", typeof(AvatarDemo)),
        new("collapsible", "Collapsible", "A panel that a trigger shows and hides.", typeof(CollapsibleDemo)),
        new("accordion", "Accordion", "A stack of sections whose panels open and close.", typeof(AccordionDemo)),
        new("input", "Input", "A native input element that works with a field.", typeof(InputDemo)),
        new("field", "Field", "A form control with a label, a description and its validity.", typeof(FieldDemo)),
        new("fieldset", "Fieldset", "Related fields grouped under a legend.", typeof(FieldsetDemo)),
        new("form", "Form", "A form that validates its fields and shows errors from a server.", typeof(FormDemo)),
        new("switch", "Switch", "A control that turns a setting on or off.", typeof(SwitchDemo)),
        new("checkbox", "Checkbox", "A box that the user ticks or clears, and that can be shown as mixed.", typeof(CheckboxDemo)),
        new("checkbox-group", "Checkbox Group", "A set of checkboxes that share one value, with an optional parent box.", typeof(CheckboxGroupDemo)),
        new("radio-group", "Radio Group", "Radio buttons (Radio) in a group (Radio Group) that holds the one value selected.", typeof(RadioGroupDemo)),
        new("toggle", "Toggle", "A two-state button that is pressed or not.", typeof(ToggleDemo)),
        new("toggle-group", "Toggle Group", "A set of toggles that share one value, here the text alignment.", typeof(ToggleGroupDemo)),
        new("tabs", "Tabs", "Tabs that switch between related panels on the same page.", typeof(TabsDemo)),
        new("toolbar", "Toolbar", "A set of buttons, toggles, an input and a link that the arrow keys move between.", typeof(ToolbarDemo)),
        new("popover", "Popover", "An accessible popup anchored to a button, here the notifications of an account.", typeof(PopoverDemo)),
        new("tooltip", "Tooltip", "A popup that names a button when the pointer rests on it or the keyboard focuses it.", typeof(TooltipDemo)),
    ];

    public static DemoEntry? Find(string slug) =>
        Entries.FirstOrDefault(entry => string.Equals(entry.Slug, slug, StringComparison.OrdinalIgnoreCase));
}
