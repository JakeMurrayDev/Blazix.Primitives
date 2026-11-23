using Bunit;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Components.Rendering.Conformance;

/// <summary>
/// The options of Base UI's <c>describeConformance(minimalElement, () => options)</c> (RND §4.9, ADR 06 §1):
/// <see cref="TagName"/> stands in for <c>refInstanceof</c> (RND-DOM-41 as relaxed by ADR 02: the captured element's
/// tag), <see cref="TestRenderPropWith"/> is the tag the substitution tests render, <see cref="Button"/> says the part
/// accepts <c>NativeButton</c>, <see cref="WrappingAllowed"/> whether a wrapper may surround the substituted element,
/// <see cref="Required"/> adds whatever the minimal element needs (ancestors, required parameters), and
/// <see cref="RendersAgainOnMount"/> marks a part that renders a second time when it mounts.
/// </summary>
public sealed record ConformanceOptions<TPart>(string TagName)
    where TPart : IComponent
{
    public string TestRenderPropWith { get; init; } = "div";

    public bool Button { get; init; }

    public bool WrappingAllowed { get; init; } = true;

    public Action<ComponentParameterCollectionBuilder<TPart>>? Required { get; init; }

    // ADR 06 Amendments 2026-11-18 (Toast 9a, granted and settled by the orchestrator 2026-11-18): a part whose root renders
    // again when the part mounts (Toast's Title and Description publish their id, and the root writes aria-labelledby) is
    // rendered a second time, and bUnit's markup keeps an element's reference capture only from its first render, so the
    // ref titles (RND-DOM-44, RND-DOM-45) find the element by its data-testid and assert the captures non-null.
    public bool RendersAgainOnMount { get; init; }
}
