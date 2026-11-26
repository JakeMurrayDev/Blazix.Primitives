using Bunit;

namespace Iodine.Headless.Tests.Components;

/// <summary>
/// L2: the Razor form of a union parameter's string case. A bare attribute value on a non-string parameter is C# on
/// SDK 11.0.100, so the documented form is <c>@("…")</c>; the harness compiling with it and the parts holding the
/// string case pin that form.
/// </summary>
public sealed class UnionStringCaseTests : IDisposable
{
    private readonly PopoverTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    // Issue 282, ADR 08 Amendments 2026-11-18: the XML documentation of AnchorValue and PortalContainerValue states this
    // form. Coverage: the form already compiled on the base; its red is a planted fault quoted in the pull request.
    [Fact(DisplayName = "port: an element id and a selector written as @(\"…\") reach Anchor and Container as their string case")]
    [Trait("Coverage", "issue-282")]
    public void A_string_written_as_an_expression_binds_to_the_string_case()
    {
        IRenderedComponent<UnionStringCaseHarness> cut = test.Context.Render<UnionStringCaseHarness>();

        AnchorValue? anchor = cut.FindComponent<PopoverPositioner>().Instance.Anchor;
        Assert.True(anchor.HasValue);
        Assert.True(anchor.Value.TryGetValue(out string? id));
        Assert.Equal("my-anchor", id);

        PortalContainerValue? container = cut.FindComponent<PopoverPortal>().Instance.Container;
        Assert.True(container.HasValue);
        Assert.True(container.Value.TryGetValue(out string? selector));
        Assert.Equal("#target", selector);
    }
}
