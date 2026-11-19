using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives.Tests.Components.Popup;

/// <summary>
/// L2 titles of the focus guard renderer shared by the portal, the navigation menu's viewport and the toast viewport
/// (PSH-STR-08, PSH-DOM-13; the Toast track's DP3 (a), chosen by the user 2026-11-19).
/// </summary>
public sealed class FocusGuardTests
{
    [Fact]
    [Trait("Requirement", "PSH-STR-08")]
    [Trait("Requirement", "TST-DOM-04")]
    public void Port_renders_a_visually_hidden_tabbable_guard_with_an_optional_type()
    {
        using BunitContext context = new();

        IRenderedComponent<GuardHost> cut = context.Render<GuardHost>();

        cut.MarkupMatches(
            """
            <span tabindex="0" data-base-ui-focus-guard="" aria-hidden="true" style="clip-path: inset(50%); overflow: hidden; white-space: nowrap; border: 0; padding: 0; width: 1px; height: 1px; margin: 0; position: fixed; top: 0; left: 0"></span>
            <span tabindex="0" data-base-ui-focus-guard="" data-type="outside" aria-hidden="true" style="clip-path: inset(50%); overflow: hidden; white-space: nowrap; border: 0; padding: 0; width: 1px; height: 1px; margin: 0; position: fixed; top: 0; left: 0"></span>
            """);
    }

    private sealed class GuardHost : ComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            FocusGuard.Render(builder);
            FocusGuard.Render(builder, "outside");
        }
    }
}
