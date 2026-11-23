using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace (tests/Iodine.Headless.Tests.Components/CONTEXT.md).
namespace Iodine.Headless.Tests.Components;

/// <summary>
/// A test-only consumer of the bar context in its required mode (MBR-EDGE-01; Menubar.test.tsx:34-48 reaches the message
/// the same way): Menu's parts read the context optionally and never throw.
/// </summary>
internal sealed class MenubarContextReader : ComponentBase
{
    [CascadingParameter]
    private IMenuParentContext? Parent { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet() => _ = MenubarContext.Require(Parent);
}
