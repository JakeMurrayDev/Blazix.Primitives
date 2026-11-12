using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// MNU-API-60: <c>MenuFilterParts.test.tsx</c>'s <c>describeConformance(&lt;Menu.Clear /&gt;, button: true)</c>, inside the
/// popup of an open filterable menu whose query is "query" (renderInPopup).
/// </summary>
public sealed class MenuClearConformanceTests : PartConformanceTests<MenuClear>
{
    public MenuClearConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<MenuFilterProvider>(p => p.Add(x => x.DefaultValue, "query"));
        Context.RenderTree.Add<MenuRoot<NoPayload>>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<MenuPortal>();
        Context.RenderTree.Add<MenuPositioner>();
        Context.RenderTree.Add<MenuPopup>();

        // The focus manager's publication renders the portal again (Popover's helper, the same bUnit markup fact).
        PopoverConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<MenuClear> Options { get; } = new("button") { Button = true, TestRenderPropWith = "button" };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(MenuClear part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<MenuClear> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<MenuClearState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<MenuClear> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<MenuClearState, string?>)(_ => classValue()));
    }
}

/// <summary>
/// MNU-DOM-27: <c>MenuFilterParts.test.tsx</c>'s <c>describeConformance(&lt;Menu.Separator /&gt;)</c>, inside the popup of an
/// open filterable menu (renderInPopup).
/// </summary>
public sealed class MenuFilterSeparatorConformanceTests : PartConformanceTests<MenuSeparator>
{
    public MenuFilterSeparatorConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<MenuFilterProvider>();
        Context.RenderTree.Add<MenuRoot<NoPayload>>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<MenuPortal>();
        Context.RenderTree.Add<MenuPositioner>();
        Context.RenderTree.Add<MenuPopup>();
        PopoverConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<MenuSeparator> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(MenuSeparator part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<MenuSeparator> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<MenuSeparatorState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<MenuSeparator> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<MenuSeparatorState, string?>)(_ => classValue()));
    }
}
