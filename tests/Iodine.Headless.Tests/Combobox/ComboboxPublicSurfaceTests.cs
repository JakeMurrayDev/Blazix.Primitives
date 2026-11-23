using Blazix.Primitives.Rendering;

namespace Blazix.Primitives.Tests;

// CMB-API-69 (combobox/index.ts, index.parts.ts; `Test: none (type-level)`, Deliverable 10's L1 proposal; PR 193 delta review
// B1 (d)): every public part has a public state record, which is what Base UI's `Combobox.<Part>.State` exports, and the
// data-attribute and CSS-variable classes the row names are public. Parameters are the port's `Props`.
/// <summary>Combobox's public surface at L1.</summary>
public sealed class ComboboxPublicSurfaceTests
{
    [Fact(DisplayName = "port: every public Combobox part has a public state type, and the data-attribute and CSS-variable classes are public")]
    [Trait("Requirement", "CMB-API-69")]
    public void Every_public_part_has_a_public_state_type()
    {
        Type[] parts = [.. typeof(ComboboxInput).Assembly.GetExportedTypes()
            .Where(type => type.Name.StartsWith("Combobox", StringComparison.Ordinal) && PartStateOf(type) is not null)];

        Assert.True(parts.Length >= 20, $"{parts.Length} public Combobox parts");
        Assert.All(parts, part => Assert.True(PartStateOf(part)!.IsPublic || PartStateOf(part) == typeof(EmptyState), part.Name));
        Assert.All(
            new[]
            {
                typeof(ComboboxInputDataAttributes), typeof(ComboboxInputGroupDataAttributes), typeof(ComboboxTriggerDataAttributes),
                typeof(ComboboxBackdropDataAttributes), typeof(ComboboxPositionerCssVars), typeof(ComboboxPositionerDataAttributes),
                typeof(ComboboxPopupDataAttributes), typeof(ComboboxArrowDataAttributes), typeof(ComboboxItemDataAttributes),
                typeof(ComboboxItemIndicatorDataAttributes), typeof(ComboboxClearDataAttributes), typeof(ComboboxSeparatorDataAttributes),
            },
            type => Assert.True(type.IsPublic, type.Name));
    }

    private static Type? PartStateOf(Type type)
    {
        for (Type? current = type; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(PartBase<>))
            {
                return current.GetGenericArguments()[0];
            }
        }

        return null;
    }
}
