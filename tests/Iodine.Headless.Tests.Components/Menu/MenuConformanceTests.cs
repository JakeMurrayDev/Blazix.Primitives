using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>MNU-STR-02, MNU-API-17: <c>MenuTrigger.test.tsx</c>'s conformance suite inside a root (describeConformance).</summary>
public sealed class MenuTriggerConformanceTests : PartConformanceTests<MenuTrigger<NoPayload>>
{
    public MenuTriggerConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<MenuRoot<NoPayload>>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<MenuTrigger<NoPayload>> Options { get; } = new("button") { Button = true, TestRenderPropWith = "button" };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(MenuTrigger<NoPayload> part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<MenuTrigger<NoPayload>> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<MenuTriggerState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<MenuTrigger<NoPayload>> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<MenuTriggerState, string?>)(_ => classValue()));
    }
}

/// <summary>MNU-STR-04: <c>MenuPortal.test.tsx</c>'s conformance suite under <c>Root open</c> (describeConformance).</summary>
public sealed class MenuPortalConformanceTests : PartConformanceTests<MenuPortal>
{
    public MenuPortalConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<MenuRoot<NoPayload>>(p => p.Add(x => x.Open, true));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<MenuPortal> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(MenuPortal part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<MenuPortal> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<MenuPortal> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}

/// <summary>MNU-STR-06: <c>MenuBackdrop.test.tsx</c>'s conformance suite under <c>Root open</c> (describeConformance).</summary>
public sealed class MenuBackdropConformanceTests : PartConformanceTests<MenuBackdrop>
{
    public MenuBackdropConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<MenuRoot<NoPayload>>(p => p.Add(x => x.Open, true));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<MenuBackdrop> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(MenuBackdrop part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<MenuBackdrop> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<MenuBackdropState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<MenuBackdrop> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<MenuBackdropState, string?>)(_ => classValue()));
    }
}

/// <summary>MNU-STR-05: <c>MenuPositioner.test.tsx</c>'s conformance suite under <c>Root open</c> and a Portal (describeConformance).</summary>
public sealed class MenuPositionerConformanceTests : PartConformanceTests<MenuPositioner>
{
    public MenuPositionerConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<MenuRoot<NoPayload>>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<MenuPortal>();

        // The focus manager's publication renders the portal again (Popover's helper, the same bUnit markup fact).
        PopoverConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<MenuPositioner> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(MenuPositioner part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<MenuPositioner> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<MenuPositionerState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<MenuPositioner> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<MenuPositionerState, string?>)(_ => classValue()));
    }
}

/// <summary>MNU-STR-06: <c>MenuPopup.test.tsx</c>'s conformance suite inside an open Positioner (describeConformance).</summary>
public sealed class MenuPopupConformanceTests : PartConformanceTests<MenuPopup>
{
    public MenuPopupConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<MenuRoot<NoPayload>>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<MenuPortal>();
        Context.RenderTree.Add<MenuPositioner>();

        // The focus manager's publication renders the portal again (Popover's helper, the same bUnit markup fact).
        PopoverConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<MenuPopup> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(MenuPopup part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<MenuPopup> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<MenuPopupState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<MenuPopup> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<MenuPopupState, string?>)(_ => classValue()));
    }
}

/// <summary>MNU-API-33: <c>MenuViewport.test.tsx</c>'s conformance suite inside an open Popup (describeConformance).</summary>
public sealed class MenuViewportConformanceTests : PartConformanceTests<MenuViewport>
{
    public MenuViewportConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<MenuRoot<NoPayload>>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<MenuPortal>();
        Context.RenderTree.Add<MenuPositioner>();
        Context.RenderTree.Add<MenuPopup>();

        // The focus manager's publication renders the portal again (Popover's helper, the same bUnit markup fact).
        PopoverConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<MenuViewport> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(MenuViewport part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<MenuViewport> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<MenuViewportState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<MenuViewport> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<MenuViewportState, string?>)(_ => classValue()));
    }
}

/// <summary>MNU-DOM-11: <c>MenuArrow.test.tsx</c>'s conformance suite inside an open Popup (describeConformance).</summary>
public sealed class MenuArrowConformanceTests : PartConformanceTests<MenuArrow>
{
    public MenuArrowConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<MenuRoot<NoPayload>>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<MenuPortal>();
        Context.RenderTree.Add<MenuPositioner>();
        Context.RenderTree.Add<MenuPopup>();

        // The focus manager's publication renders the portal again (Popover's helper, the same bUnit markup fact).
        PopoverConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<MenuArrow> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(MenuArrow part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<MenuArrow> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<MenuArrowState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<MenuArrow> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<MenuArrowState, string?>)(_ => classValue()));
    }
}

/// <summary>MNU-STR-13: <c>MenuList.test.tsx</c>'s conformance suite inside an open Popup (describeConformance).</summary>
public sealed class MenuListConformanceTests : PartConformanceTests<MenuList>
{
    public MenuListConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<MenuRoot<NoPayload>>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<MenuPortal>();
        Context.RenderTree.Add<MenuPositioner>();
        Context.RenderTree.Add<MenuPopup>();

        // The focus manager's publication renders the portal again (Popover's helper, the same bUnit markup fact).
        PopoverConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<MenuList> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(MenuList part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<MenuList> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<MenuList> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}

/// <summary>MNU-STR-08: <c>MenuItem.test.tsx</c>'s conformance suite inside an open Popup (describeConformance).</summary>
public sealed class MenuItemConformanceTests : PartConformanceTests<MenuItem>
{
    public MenuItemConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<MenuRoot<NoPayload>>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<MenuPortal>();
        Context.RenderTree.Add<MenuPositioner>();
        Context.RenderTree.Add<MenuPopup>();

        // The focus manager's publication renders the portal again (Popover's helper, the same bUnit markup fact).
        PopoverConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<MenuItem> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(MenuItem part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<MenuItem> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<MenuItemState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<MenuItem> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<MenuItemState, string?>)(_ => classValue()));
    }
}

/// <summary>MNU-API-40, MNU-DOM-15: <c>MenuLinkItem.test.tsx</c>'s conformance suite inside an open Popup (describeConformance).</summary>
public sealed class MenuLinkItemConformanceTests : PartConformanceTests<MenuLinkItem>
{
    public MenuLinkItemConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<MenuRoot<NoPayload>>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<MenuPortal>();
        Context.RenderTree.Add<MenuPositioner>();
        Context.RenderTree.Add<MenuPopup>();

        // The focus manager's publication renders the portal again (Popover's helper, the same bUnit markup fact).
        PopoverConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<MenuLinkItem> Options { get; } = new("a");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(MenuLinkItem part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<MenuLinkItem> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<MenuLinkItemState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<MenuLinkItem> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<MenuLinkItemState, string?>)(_ => classValue()));
    }
}

/// <summary>MNU-API-41: <c>MenuCheckboxItem.test.tsx</c>'s conformance suite inside an open Popup (describeConformance).</summary>
public sealed class MenuCheckboxItemConformanceTests : PartConformanceTests<MenuCheckboxItem>
{
    public MenuCheckboxItemConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<MenuRoot<NoPayload>>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<MenuPortal>();
        Context.RenderTree.Add<MenuPositioner>();
        Context.RenderTree.Add<MenuPopup>();

        // The focus manager's publication renders the portal again (Popover's helper, the same bUnit markup fact).
        PopoverConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<MenuCheckboxItem> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(MenuCheckboxItem part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<MenuCheckboxItem> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<MenuCheckboxItemState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<MenuCheckboxItem> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<MenuCheckboxItemState, string?>)(_ => classValue()));
    }
}

/// <summary>MNU-API-43: <c>MenuCheckboxItemIndicator.test.tsx</c>'s conformance suite inside a checked item (describeConformance).</summary>
public sealed class MenuCheckboxItemIndicatorConformanceTests : PartConformanceTests<MenuCheckboxItemIndicator>
{
    public MenuCheckboxItemIndicatorConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<MenuRoot<NoPayload>>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<MenuPortal>();
        Context.RenderTree.Add<MenuPositioner>();
        Context.RenderTree.Add<MenuPopup>();
        Context.RenderTree.Add<MenuCheckboxItem>(p => p.Add(x => x.DefaultChecked, true));

        // The focus manager's publication renders the portal again (Popover's helper, the same bUnit markup fact).
        PopoverConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<MenuCheckboxItemIndicator> Options { get; } = new("span");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(MenuCheckboxItemIndicator part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<MenuCheckboxItemIndicator> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<MenuCheckboxItemIndicatorState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<MenuCheckboxItemIndicator> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<MenuCheckboxItemIndicatorState, string?>)(_ => classValue()));
    }
}

/// <summary>MNU-ARIA-10: <c>MenuRadioGroup.test.tsx</c>'s conformance suite inside an open Popup (describeConformance).</summary>
public sealed class MenuRadioGroupConformanceTests : PartConformanceTests<MenuRadioGroup<string>>
{
    public MenuRadioGroupConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<MenuRoot<NoPayload>>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<MenuPortal>();
        Context.RenderTree.Add<MenuPositioner>();
        Context.RenderTree.Add<MenuPopup>();

        // The focus manager's publication renders the portal again (Popover's helper, the same bUnit markup fact).
        PopoverConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<MenuRadioGroup<string>> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(MenuRadioGroup<string> part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<MenuRadioGroup<string>> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<MenuRadioGroupState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<MenuRadioGroup<string>> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<MenuRadioGroupState, string?>)(_ => classValue()));
    }
}

/// <summary>MNU-API-47: <c>MenuRadioItem.test.tsx</c>'s conformance suite inside a radio group (describeConformance).</summary>
public sealed class MenuRadioItemConformanceTests : PartConformanceTests<MenuRadioItem<string>>
{
    public MenuRadioItemConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<MenuRoot<NoPayload>>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<MenuPortal>();
        Context.RenderTree.Add<MenuPositioner>();
        Context.RenderTree.Add<MenuPopup>();
        Context.RenderTree.Add<MenuRadioGroup<string>>(p => p.Add(x => x.DefaultValue, "a"));

        // The focus manager's publication renders the portal again (Popover's helper, the same bUnit markup fact).
        PopoverConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<MenuRadioItem<string>> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(MenuRadioItem<string> part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<MenuRadioItem<string>> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<MenuRadioItemState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<MenuRadioItem<string>> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<MenuRadioItemState, string?>)(_ => classValue()));
    }
}

/// <summary>MNU-API-43: <c>MenuRadioItemIndicator.test.tsx</c>'s conformance suite inside a checked radio item (describeConformance).</summary>
public sealed class MenuRadioItemIndicatorConformanceTests : PartConformanceTests<MenuRadioItemIndicator>
{
    public MenuRadioItemIndicatorConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<MenuRoot<NoPayload>>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<MenuPortal>();
        Context.RenderTree.Add<MenuPositioner>();
        Context.RenderTree.Add<MenuPopup>();
        Context.RenderTree.Add<MenuRadioGroup<string>>(p => p.Add(x => x.DefaultValue, "a"));
        Context.RenderTree.Add<MenuRadioItem<string>>(p => p.Add(x => x.Value, "a"));

        // The focus manager's publication renders the portal again (Popover's helper, the same bUnit markup fact).
        PopoverConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<MenuRadioItemIndicator> Options { get; } = new("span");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(MenuRadioItemIndicator part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<MenuRadioItemIndicator> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<MenuRadioItemIndicatorState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<MenuRadioItemIndicator> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<MenuRadioItemIndicatorState, string?>)(_ => classValue()));
    }
}

/// <summary>MNU-ARIA-10: <c>MenuGroup.test.tsx</c>'s conformance suite inside an open Popup (describeConformance).</summary>
public sealed class MenuGroupConformanceTests : PartConformanceTests<MenuGroup>
{
    public MenuGroupConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<MenuRoot<NoPayload>>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<MenuPortal>();
        Context.RenderTree.Add<MenuPositioner>();
        Context.RenderTree.Add<MenuPopup>();

        // The focus manager's publication renders the portal again (Popover's helper, the same bUnit markup fact).
        PopoverConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<MenuGroup> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(MenuGroup part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<MenuGroup> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<MenuGroup> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}

/// <summary>MNU-API-48: <c>MenuGroupLabel.test.tsx</c>'s conformance suite inside a group (describeConformance).</summary>
public sealed class MenuGroupLabelConformanceTests : PartConformanceTests<MenuGroupLabel>
{
    public MenuGroupLabelConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<MenuRoot<NoPayload>>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<MenuPortal>();
        Context.RenderTree.Add<MenuPositioner>();
        Context.RenderTree.Add<MenuPopup>();
        Context.RenderTree.Add<MenuGroup>();

        // The focus manager's publication renders the portal again (Popover's helper, the same bUnit markup fact).
        PopoverConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<MenuGroupLabel> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(MenuGroupLabel part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<MenuGroupLabel> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<MenuGroupLabel> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}

/// <summary>MNU-STR-10: the Separator conformance suite on <c>MenuSeparator</c> inside an open Popup (describeConformance).</summary>
public sealed class MenuSeparatorConformanceTests : PartConformanceTests<MenuSeparator>
{
    public MenuSeparatorConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<MenuRoot<NoPayload>>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<MenuPortal>();
        Context.RenderTree.Add<MenuPositioner>();
        Context.RenderTree.Add<MenuPopup>();

        // The focus manager's publication renders the portal again (Popover's helper, the same bUnit markup fact).
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

/// <summary>MNU-STR-11: <c>MenuSubmenuTrigger.test.tsx</c>'s conformance suite inside a submenu root (describeConformance).</summary>
public sealed class MenuSubmenuTriggerConformanceTests : PartConformanceTests<MenuSubmenuTrigger>
{
    public MenuSubmenuTriggerConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<MenuRoot<NoPayload>>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<MenuPortal>();
        Context.RenderTree.Add<MenuPositioner>();
        Context.RenderTree.Add<MenuPopup>();
        Context.RenderTree.Add<MenuSubmenuRoot>();

        // The focus manager's publication renders the portal again (Popover's helper, the same bUnit markup fact).
        PopoverConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<MenuSubmenuTrigger> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(MenuSubmenuTrigger part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<MenuSubmenuTrigger> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<MenuSubmenuTriggerState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<MenuSubmenuTrigger> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<MenuSubmenuTriggerState, string?>)(_ => classValue()));
    }
}
