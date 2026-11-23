using Blazix.Primitives.Utilities;

namespace Blazix.Primitives.Tests;

// UTL-DOM-13, UTL-DOM-14, UTL-DOM-15: the declarations are exact strings the documents quote (MTR-DOM-01).
/// <summary>The visually hidden declarations shared by every part that renders a hidden helper element.</summary>
public sealed class VisuallyHiddenTests
{
    [Fact(DisplayName = "visuallyHidden is exactly the declarations of the document")]
    [Trait("Requirement", "UTL-DOM-13")]
    public void VisuallyHidden_is_exactly_the_declarations_of_the_document()
    {
        Assert.Equal(
            "clip-path: inset(50%); overflow: hidden; white-space: nowrap; border: 0; padding: 0; width: 1px; height: 1px; margin: 0; position: fixed; top: 0; left: 0",
            VisuallyHidden.Style);
    }

    [Fact(DisplayName = "visuallyHiddenInput is exactly the declarations of the document")]
    [Trait("Requirement", "UTL-DOM-14")]
    public void VisuallyHiddenInput_is_exactly_the_declarations_of_the_document()
    {
        Assert.Equal(
            "clip-path: inset(50%); overflow: hidden; white-space: nowrap; border: 0; padding: 0; width: 1px; height: 1px; margin: -1px; position: absolute",
            VisuallyHidden.InputStyle);
    }

    [Fact(DisplayName = "ownerVisuallyHidden is exactly the declarations of the document")]
    [Trait("Requirement", "UTL-DOM-15")]
    public void OwnerVisuallyHidden_is_exactly_the_declarations_of_the_document()
    {
        Assert.Equal("clip-path: inset(50%); position: fixed; top: 0; left: 0", VisuallyHidden.OwnerStyle);
    }
}
