using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>MBR-STR-01, MBR-STR-05, MBR-API-06: <c>Menubar.test.tsx</c>'s conformance suite (describeConformance, <c>refInstanceof: HTMLDivElement</c>).</summary>
public sealed class MenubarConformanceTests : PartConformanceTests<Menubar>
{
    public MenubarConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <inheritdoc />
    protected override ConformanceOptions<Menubar> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(Menubar part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<Menubar> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<MenubarState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<Menubar> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<MenubarState, string?>)(_ => classValue()));
    }
}
