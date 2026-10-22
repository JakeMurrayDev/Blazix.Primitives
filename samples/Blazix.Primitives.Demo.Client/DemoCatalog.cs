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
        new("input", "Input", "A native input element that works with a field.", typeof(InputDemo)),
        new("field", "Field", "A form control with a label, a description and its validity.", typeof(FieldDemo)),
        new("fieldset", "Fieldset", "Related fields grouped under a legend.", typeof(FieldsetDemo)),
    ];

    public static DemoEntry? Find(string slug) =>
        Entries.FirstOrDefault(entry => string.Equals(entry.Slug, slug, StringComparison.OrdinalIgnoreCase));
}
