using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>NAV-STR-01, NAV-DOM-03: <c>NavigationMenuRoot.test.tsx</c>'s conformance suite.</summary>
public sealed class NavigationMenuRootConformanceTests : PartConformanceTests<NavigationMenuRoot<string>>
{
    public NavigationMenuRootConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <inheritdoc />
    protected override ConformanceOptions<NavigationMenuRoot<string>> Options { get; } = new("nav");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(NavigationMenuRoot<string> part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<NavigationMenuRoot<string>> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<NavigationMenuRootState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<NavigationMenuRoot<string>> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<NavigationMenuRootState, string?>)(_ => classValue()));
    }
}

/// <summary>NAV-STR-02, NAV-DOM-04: <c>NavigationMenuList.test.tsx</c>'s conformance suite inside a root.</summary>
public sealed class NavigationMenuListConformanceTests : PartConformanceTests<NavigationMenuList>
{
    public NavigationMenuListConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<NavigationMenuRoot<string>>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<NavigationMenuList> Options { get; } = new("ul");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(NavigationMenuList part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<NavigationMenuList> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<NavigationMenuListState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<NavigationMenuList> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<NavigationMenuListState, string?>)(_ => classValue()));
    }
}

/// <summary>NAV-STR-03: <c>NavigationMenuItem.test.tsx</c>'s conformance suite inside a root.</summary>
public sealed class NavigationMenuItemConformanceTests : PartConformanceTests<NavigationMenuItem<string>>
{
    public NavigationMenuItemConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<NavigationMenuRoot<string>>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<NavigationMenuItem<string>> Options { get; } = new("li");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(NavigationMenuItem<string> part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<NavigationMenuItem<string>> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<NavigationMenuItem<string>> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}

/// <summary>NAV-STR-04, NAV-API-09: <c>NavigationMenuTrigger.test.tsx</c>'s conformance suite inside <c>Root &gt; List &gt; Item</c>, <c>button: true</c>.</summary>
public sealed class NavigationMenuTriggerConformanceTests : PartConformanceTests<NavigationMenuTrigger>
{
    public NavigationMenuTriggerConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<NavigationMenuRoot<string>>();
        Context.RenderTree.Add<NavigationMenuList>();
        Context.RenderTree.Add<NavigationMenuItem<string>>(p => p.Add(x => x.Value, "item"));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<NavigationMenuTrigger> Options { get; } = new("button") { Button = true, TestRenderPropWith = "button" };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(NavigationMenuTrigger part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<NavigationMenuTrigger> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<NavigationMenuTriggerState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<NavigationMenuTrigger> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<NavigationMenuTriggerState, string?>)(_ => classValue()));
    }
}

/// <summary>NAV-STR-05, NAV-DOM-12: <c>NavigationMenuIcon.test.tsx</c>'s conformance suite inside an item.</summary>
public sealed class NavigationMenuIconConformanceTests : PartConformanceTests<NavigationMenuIcon>
{
    public NavigationMenuIconConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<NavigationMenuRoot<string>>();
        Context.RenderTree.Add<NavigationMenuItem<string>>(p => p.Add(x => x.Value, "item"));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<NavigationMenuIcon> Options { get; } = new("span");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(NavigationMenuIcon part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<NavigationMenuIcon> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<NavigationMenuIconState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<NavigationMenuIcon> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<NavigationMenuIconState, string?>)(_ => classValue()));
    }
}

/// <summary>NAV-STR-06, §16 Q1: <c>NavigationMenuContent.test.tsx</c>'s skipped conformance suite, run on a kept-mounted content rendered inline (no viewport target), the case Base UI's skip leaves out.</summary>
public sealed class NavigationMenuContentConformanceTests : PartConformanceTests<NavigationMenuContent>
{
    public NavigationMenuContentConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<NavigationMenuRoot<string>>();
        Context.RenderTree.Add<NavigationMenuItem<string>>(p => p.Add(x => x.Value, "item"));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<NavigationMenuContent> Options { get; } = new("div") { Required = p => p.Add(x => x.KeepMounted, true) };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(NavigationMenuContent part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<NavigationMenuContent> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<NavigationMenuContentState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<NavigationMenuContent> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<NavigationMenuContentState, string?>)(_ => classValue()));
    }

}

/// <summary>NAV-STR-07, NAV-DOM-20: <c>NavigationMenuLink.test.tsx</c>'s conformance suite inside <c>Root &gt; List</c>.</summary>
public sealed class NavigationMenuLinkConformanceTests : PartConformanceTests<NavigationMenuLink>
{
    public NavigationMenuLinkConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<NavigationMenuRoot<string>>();
        Context.RenderTree.Add<NavigationMenuList>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<NavigationMenuLink> Options { get; } = new("a");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(NavigationMenuLink part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<NavigationMenuLink> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<NavigationMenuLinkState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<NavigationMenuLink> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<NavigationMenuLinkState, string?>)(_ => classValue()));
    }
}

/// <summary>NAV-STR-08, NAV-DOM-22: <c>NavigationMenuPortal.test.tsx</c>'s conformance suite under <c>Root value="item"</c>.</summary>
public sealed class NavigationMenuPortalConformanceTests : PartConformanceTests<NavigationMenuPortal>
{
    public NavigationMenuPortalConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<NavigationMenuRoot<string>>(p => p.Add(x => x.Value, "item"));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<NavigationMenuPortal> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(NavigationMenuPortal part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<NavigationMenuPortal> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<NavigationMenuPortal> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}

/// <summary>NAV-STR-09, NAV-DOM-23: <c>NavigationMenuBackdrop.test.tsx</c>'s conformance suite directly under a root.</summary>
public sealed class NavigationMenuBackdropConformanceTests : PartConformanceTests<NavigationMenuBackdrop>
{
    public NavigationMenuBackdropConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<NavigationMenuRoot<string>>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<NavigationMenuBackdrop> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(NavigationMenuBackdrop part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<NavigationMenuBackdrop> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<NavigationMenuBackdropState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<NavigationMenuBackdrop> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<NavigationMenuBackdropState, string?>)(_ => classValue()));
    }
}

/// <summary>NAV-STR-10, NAV-POS-01: <c>NavigationMenuPositioner.test.tsx</c>'s conformance suite under a closed root and a kept-mounted portal.</summary>
public sealed class NavigationMenuPositionerConformanceTests : PartConformanceTests<NavigationMenuPositioner>
{
    public NavigationMenuPositionerConformanceTests()
    {
        // A closed root with a kept-mounted portal: an initially open positioner renders again 0 ms after mount to clear its
        // instant marker (NAV-ANI-13), which the suite's element-reference titles do not allow for.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<NavigationMenuRoot<string>>();
        Context.RenderTree.Add<NavigationMenuPortal>(p => p.Add(x => x.KeepMounted, true));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<NavigationMenuPositioner> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(NavigationMenuPositioner part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<NavigationMenuPositioner> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<NavigationMenuPositionerState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<NavigationMenuPositioner> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<NavigationMenuPositionerState, string?>)(_ => classValue()));
    }
}

/// <summary>NAV-STR-11, NAV-DOM-30: <c>NavigationMenuPopup.test.tsx</c>'s conformance suite under a positioner.</summary>
public sealed class NavigationMenuPopupConformanceTests : PartConformanceTests<NavigationMenuPopup>
{
    public NavigationMenuPopupConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<NavigationMenuRoot<string>>(p => p.Add(x => x.Value, "item"));
        Context.RenderTree.Add<NavigationMenuPortal>();
        Context.RenderTree.Add<NavigationMenuPositioner>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<NavigationMenuPopup> Options { get; } = new("nav");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(NavigationMenuPopup part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<NavigationMenuPopup> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<NavigationMenuPopupState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<NavigationMenuPopup> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<NavigationMenuPopupState, string?>)(_ => classValue()));
    }
}

/// <summary>NAV-STR-12: <c>NavigationMenuViewport.test.tsx</c>'s conformance suite directly under a root.</summary>
public sealed class NavigationMenuViewportConformanceTests : PartConformanceTests<NavigationMenuViewport>
{
    public NavigationMenuViewportConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<NavigationMenuRoot<string>>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<NavigationMenuViewport> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(NavigationMenuViewport part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<NavigationMenuViewport> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<NavigationMenuViewport> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}

/// <summary>NAV-STR-13, NAV-DOM-37: <c>NavigationMenuArrow.test.tsx</c>'s conformance suite under a positioner.</summary>
public sealed class NavigationMenuArrowConformanceTests : PartConformanceTests<NavigationMenuArrow>
{
    public NavigationMenuArrowConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<NavigationMenuRoot<string>>(p => p.Add(x => x.Value, "item"));
        Context.RenderTree.Add<NavigationMenuPortal>();
        Context.RenderTree.Add<NavigationMenuPositioner>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<NavigationMenuArrow> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(NavigationMenuArrow part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<NavigationMenuArrow> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<NavigationMenuArrowState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<NavigationMenuArrow> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<NavigationMenuArrowState, string?>)(_ => classValue()));
    }
}
