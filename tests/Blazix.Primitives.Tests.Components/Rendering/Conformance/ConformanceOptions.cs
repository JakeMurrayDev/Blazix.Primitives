using Bunit;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Components.Rendering.Conformance;

/// <summary>
/// The options of Base UI's <c>describeConformance(minimalElement, () => options)</c> (RND §4.9, ADR 06 §1):
/// <see cref="TagName"/> stands in for <c>refInstanceof</c> (RND-DOM-41 as relaxed by ADR 02: the captured element's
/// tag), <see cref="TestRenderPropWith"/> is the tag the substitution tests render, <see cref="Button"/> says the part
/// accepts <c>NativeButton</c>, <see cref="WrappingAllowed"/> whether a wrapper may surround the substituted element,
/// and <see cref="Required"/> adds whatever the minimal element needs (ancestors, required parameters).
/// </summary>
public sealed record ConformanceOptions<TPart>(string TagName)
    where TPart : IComponent
{
    public string TestRenderPropWith { get; init; } = "div";

    public bool Button { get; init; }

    public bool WrappingAllowed { get; init; } = true;

    public Action<ComponentParameterCollectionBuilder<TPart>>? Required { get; init; }
}
