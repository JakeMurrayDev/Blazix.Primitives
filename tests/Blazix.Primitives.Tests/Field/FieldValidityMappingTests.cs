using Blazix.Primitives.Rendering;

namespace Blazix.Primitives.Tests;

// stateAttributesMapping.test.ts "fieldValidityMapping" (FFI-DOM-01, FLD-DOM-03): the names are pinned against literals,
// as Base UI's test does, so a rename of the constants cannot pass through.
/// <summary>The mapping of a field's verdict to <c>data-valid</c> and <c>data-invalid</c>.</summary>
public sealed class FieldValidityMappingTests
{
    [Fact(DisplayName = "emits the validity data attributes")]
    [Trait("Requirement", "FFI-DOM-01")]
    [Trait("Requirement", "FLD-DOM-03")]
    public void Emits_the_validity_data_attributes()
    {
        Assert.Equal([("data-valid", string.Empty)], Project(true));
        Assert.Equal([("data-invalid", string.Empty)], Project(false));
    }

    [Fact(DisplayName = "emits nothing while validity is unknown")]
    [Trait("Requirement", "FFI-DOM-01")]
    [Trait("Requirement", "FLD-DOM-03")]
    public void Emits_nothing_while_validity_is_unknown()
    {
        Assert.Empty(Project(null));
    }

    private static (string, string)[] Project(bool? valid)
    {
        AttributeList list = new();
        Assert.True(FieldValidityMapping.Create<FieldRootState>().TryProject(nameof(FieldRootState.Valid), valid, new StateAttributeWriter(list)));
        (string, string)[] pairs = new (string, string)[list.Count];
        for (int i = 0; i < list.Count; i++)
        {
            Assert.True(list[i].Value.TryGetValue(out string text), $"{list[i].Name} is not a string");
            pairs[i] = (list[i].Name, text);
        }

        return pairs;
    }
}
