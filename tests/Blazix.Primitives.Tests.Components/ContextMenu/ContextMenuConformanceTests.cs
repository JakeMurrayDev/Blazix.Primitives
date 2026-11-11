using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>CTX-STR-04, CTX-STR-08, CTX-API-06: <c>ContextMenuTrigger.test.tsx</c>'s conformance suite inside a root (describeConformance, <c>refInstanceof: HTMLDivElement</c>).</summary>
public sealed class ContextMenuTriggerConformanceTests : PartConformanceTests<ContextMenuTrigger>
{
    public ContextMenuTriggerConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<ContextMenuRoot>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ContextMenuTrigger> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ContextMenuTrigger part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ContextMenuTrigger> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ContextMenuTriggerState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ContextMenuTrigger> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ContextMenuTriggerState, string?>)(_ => classValue()));
    }
}

/// <summary>CTX-STR-01, CTX-STR-02 (adopting MNU-STR-04): <c>MenuPortal.test.tsx</c>'s conformance suite under <c>Root open</c> (describeConformance).</summary>
public sealed class ContextMenuPortalConformanceTests : PartConformanceTests<ContextMenuPortal>
{
    public ContextMenuPortalConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<ContextMenuRoot>(p => p.Add(x => x.Open, true));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ContextMenuPortal> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ContextMenuPortal part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ContextMenuPortal> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ContextMenuPortal> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}

/// <summary>CTX-STR-01, CTX-STR-02 (adopting MNU-STR-06): <c>MenuBackdrop.test.tsx</c>'s conformance suite under <c>Root open</c> (describeConformance).</summary>
public sealed class ContextMenuBackdropConformanceTests : PartConformanceTests<ContextMenuBackdrop>
{
    public ContextMenuBackdropConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<ContextMenuRoot>(p => p.Add(x => x.Open, true));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ContextMenuBackdrop> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ContextMenuBackdrop part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ContextMenuBackdrop> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ContextMenuBackdropState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ContextMenuBackdrop> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ContextMenuBackdropState, string?>)(_ => classValue()));
    }
}

/// <summary>CTX-STR-01, CTX-STR-02 (adopting MNU-STR-05): <c>MenuPositioner.test.tsx</c>'s conformance suite under <c>Root open</c> and a Portal (describeConformance).</summary>
public sealed class ContextMenuPositionerConformanceTests : PartConformanceTests<ContextMenuPositioner>
{
    public ContextMenuPositionerConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<ContextMenuRoot>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<ContextMenuPortal>();

        // The focus manager's publication renders the portal again (Popover's helper, the same bUnit markup fact).
        PopoverConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ContextMenuPositioner> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ContextMenuPositioner part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ContextMenuPositioner> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ContextMenuPositionerState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ContextMenuPositioner> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ContextMenuPositionerState, string?>)(_ => classValue()));
    }
}

/// <summary>CTX-STR-01, CTX-STR-02 (adopting MNU-STR-06): <c>MenuPopup.test.tsx</c>'s conformance suite inside an open Positioner (describeConformance).</summary>
public sealed class ContextMenuPopupConformanceTests : PartConformanceTests<ContextMenuPopup>
{
    public ContextMenuPopupConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<ContextMenuRoot>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<ContextMenuPortal>();
        Context.RenderTree.Add<ContextMenuPositioner>();

        // The focus manager's publication renders the portal again (Popover's helper, the same bUnit markup fact).
        PopoverConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ContextMenuPopup> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ContextMenuPopup part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ContextMenuPopup> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ContextMenuPopupState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ContextMenuPopup> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ContextMenuPopupState, string?>)(_ => classValue()));
    }
}

/// <summary>CTX-STR-01, CTX-STR-02 (adopting MNU-DOM-11): <c>MenuArrow.test.tsx</c>'s conformance suite inside an open Popup (describeConformance).</summary>
public sealed class ContextMenuArrowConformanceTests : PartConformanceTests<ContextMenuArrow>
{
    public ContextMenuArrowConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<ContextMenuRoot>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<ContextMenuPortal>();
        Context.RenderTree.Add<ContextMenuPositioner>();
        Context.RenderTree.Add<ContextMenuPopup>();

        // The focus manager's publication renders the portal again (Popover's helper, the same bUnit markup fact).
        PopoverConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ContextMenuArrow> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ContextMenuArrow part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ContextMenuArrow> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ContextMenuArrowState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ContextMenuArrow> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ContextMenuArrowState, string?>)(_ => classValue()));
    }
}

/// <summary>CTX-STR-01, CTX-STR-02 (adopting MNU-STR-08): <c>MenuItem.test.tsx</c>'s conformance suite inside an open Popup (describeConformance).</summary>
public sealed class ContextMenuItemConformanceTests : PartConformanceTests<ContextMenuItem>
{
    public ContextMenuItemConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<ContextMenuRoot>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<ContextMenuPortal>();
        Context.RenderTree.Add<ContextMenuPositioner>();
        Context.RenderTree.Add<ContextMenuPopup>();

        // The focus manager's publication renders the portal again (Popover's helper, the same bUnit markup fact).
        PopoverConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ContextMenuItem> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ContextMenuItem part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ContextMenuItem> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ContextMenuItemState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ContextMenuItem> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ContextMenuItemState, string?>)(_ => classValue()));
    }
}

/// <summary>CTX-STR-01, CTX-STR-02 (adopting MNU-API-40, MNU-DOM-15): <c>MenuLinkItem.test.tsx</c>'s conformance suite inside an open Popup (describeConformance).</summary>
public sealed class ContextMenuLinkItemConformanceTests : PartConformanceTests<ContextMenuLinkItem>
{
    public ContextMenuLinkItemConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<ContextMenuRoot>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<ContextMenuPortal>();
        Context.RenderTree.Add<ContextMenuPositioner>();
        Context.RenderTree.Add<ContextMenuPopup>();

        // The focus manager's publication renders the portal again (Popover's helper, the same bUnit markup fact).
        PopoverConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ContextMenuLinkItem> Options { get; } = new("a");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ContextMenuLinkItem part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ContextMenuLinkItem> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ContextMenuLinkItemState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ContextMenuLinkItem> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ContextMenuLinkItemState, string?>)(_ => classValue()));
    }
}

/// <summary>CTX-STR-01, CTX-STR-02 (adopting MNU-API-41): <c>MenuCheckboxItem.test.tsx</c>'s conformance suite inside an open Popup (describeConformance).</summary>
public sealed class ContextMenuCheckboxItemConformanceTests : PartConformanceTests<ContextMenuCheckboxItem>
{
    public ContextMenuCheckboxItemConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<ContextMenuRoot>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<ContextMenuPortal>();
        Context.RenderTree.Add<ContextMenuPositioner>();
        Context.RenderTree.Add<ContextMenuPopup>();

        // The focus manager's publication renders the portal again (Popover's helper, the same bUnit markup fact).
        PopoverConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ContextMenuCheckboxItem> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ContextMenuCheckboxItem part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ContextMenuCheckboxItem> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ContextMenuCheckboxItemState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ContextMenuCheckboxItem> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ContextMenuCheckboxItemState, string?>)(_ => classValue()));
    }
}

/// <summary>CTX-STR-01, CTX-STR-02 (adopting MNU-API-43): <c>MenuCheckboxItemIndicator.test.tsx</c>'s conformance suite inside a checked item (describeConformance).</summary>
public sealed class ContextMenuCheckboxItemIndicatorConformanceTests : PartConformanceTests<ContextMenuCheckboxItemIndicator>
{
    public ContextMenuCheckboxItemIndicatorConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<ContextMenuRoot>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<ContextMenuPortal>();
        Context.RenderTree.Add<ContextMenuPositioner>();
        Context.RenderTree.Add<ContextMenuPopup>();
        Context.RenderTree.Add<ContextMenuCheckboxItem>(p => p.Add(x => x.DefaultChecked, true));

        // The focus manager's publication renders the portal again (Popover's helper, the same bUnit markup fact).
        PopoverConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ContextMenuCheckboxItemIndicator> Options { get; } = new("span");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ContextMenuCheckboxItemIndicator part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ContextMenuCheckboxItemIndicator> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ContextMenuCheckboxItemIndicatorState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ContextMenuCheckboxItemIndicator> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ContextMenuCheckboxItemIndicatorState, string?>)(_ => classValue()));
    }
}

/// <summary>CTX-STR-01, CTX-STR-02 (adopting MNU-ARIA-10): <c>MenuRadioGroup.test.tsx</c>'s conformance suite inside an open Popup (describeConformance).</summary>
public sealed class ContextMenuRadioGroupConformanceTests : PartConformanceTests<ContextMenuRadioGroup<string>>
{
    public ContextMenuRadioGroupConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<ContextMenuRoot>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<ContextMenuPortal>();
        Context.RenderTree.Add<ContextMenuPositioner>();
        Context.RenderTree.Add<ContextMenuPopup>();

        // The focus manager's publication renders the portal again (Popover's helper, the same bUnit markup fact).
        PopoverConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ContextMenuRadioGroup<string>> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ContextMenuRadioGroup<string> part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ContextMenuRadioGroup<string>> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ContextMenuRadioGroupState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ContextMenuRadioGroup<string>> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ContextMenuRadioGroupState, string?>)(_ => classValue()));
    }
}

/// <summary>CTX-STR-01, CTX-STR-02 (adopting MNU-API-47): <c>MenuRadioItem.test.tsx</c>'s conformance suite inside a radio group (describeConformance).</summary>
public sealed class ContextMenuRadioItemConformanceTests : PartConformanceTests<ContextMenuRadioItem<string>>
{
    public ContextMenuRadioItemConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<ContextMenuRoot>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<ContextMenuPortal>();
        Context.RenderTree.Add<ContextMenuPositioner>();
        Context.RenderTree.Add<ContextMenuPopup>();
        Context.RenderTree.Add<ContextMenuRadioGroup<string>>(p => p.Add(x => x.DefaultValue, "a"));

        // The focus manager's publication renders the portal again (Popover's helper, the same bUnit markup fact).
        PopoverConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ContextMenuRadioItem<string>> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ContextMenuRadioItem<string> part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ContextMenuRadioItem<string>> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ContextMenuRadioItemState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ContextMenuRadioItem<string>> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ContextMenuRadioItemState, string?>)(_ => classValue()));
    }
}

/// <summary>CTX-STR-01, CTX-STR-02 (adopting MNU-API-43): <c>MenuRadioItemIndicator.test.tsx</c>'s conformance suite inside a checked radio item (describeConformance).</summary>
public sealed class ContextMenuRadioItemIndicatorConformanceTests : PartConformanceTests<ContextMenuRadioItemIndicator>
{
    public ContextMenuRadioItemIndicatorConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<ContextMenuRoot>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<ContextMenuPortal>();
        Context.RenderTree.Add<ContextMenuPositioner>();
        Context.RenderTree.Add<ContextMenuPopup>();
        Context.RenderTree.Add<ContextMenuRadioGroup<string>>(p => p.Add(x => x.DefaultValue, "a"));
        Context.RenderTree.Add<ContextMenuRadioItem<string>>(p => p.Add(x => x.Value, "a"));

        // The focus manager's publication renders the portal again (Popover's helper, the same bUnit markup fact).
        PopoverConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ContextMenuRadioItemIndicator> Options { get; } = new("span");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ContextMenuRadioItemIndicator part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ContextMenuRadioItemIndicator> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ContextMenuRadioItemIndicatorState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ContextMenuRadioItemIndicator> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ContextMenuRadioItemIndicatorState, string?>)(_ => classValue()));
    }
}

/// <summary>CTX-STR-01, CTX-STR-02 (adopting MNU-ARIA-10): <c>MenuGroup.test.tsx</c>'s conformance suite inside an open Popup (describeConformance).</summary>
public sealed class ContextMenuGroupConformanceTests : PartConformanceTests<ContextMenuGroup>
{
    public ContextMenuGroupConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<ContextMenuRoot>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<ContextMenuPortal>();
        Context.RenderTree.Add<ContextMenuPositioner>();
        Context.RenderTree.Add<ContextMenuPopup>();

        // The focus manager's publication renders the portal again (Popover's helper, the same bUnit markup fact).
        PopoverConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ContextMenuGroup> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ContextMenuGroup part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ContextMenuGroup> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ContextMenuGroup> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}

/// <summary>CTX-STR-01, CTX-STR-02 (adopting MNU-API-48): <c>MenuGroupLabel.test.tsx</c>'s conformance suite inside a group (describeConformance).</summary>
public sealed class ContextMenuGroupLabelConformanceTests : PartConformanceTests<ContextMenuGroupLabel>
{
    public ContextMenuGroupLabelConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<ContextMenuRoot>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<ContextMenuPortal>();
        Context.RenderTree.Add<ContextMenuPositioner>();
        Context.RenderTree.Add<ContextMenuPopup>();
        Context.RenderTree.Add<ContextMenuGroup>();

        // The focus manager's publication renders the portal again (Popover's helper, the same bUnit markup fact).
        PopoverConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ContextMenuGroupLabel> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ContextMenuGroupLabel part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ContextMenuGroupLabel> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ContextMenuGroupLabel> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}

/// <summary>CTX-STR-01, CTX-STR-02 (adopting MNU-STR-10): the Separator conformance suite on <c>ContextMenuSeparator</c> inside an open Popup (describeConformance).</summary>
public sealed class ContextMenuSeparatorConformanceTests : PartConformanceTests<ContextMenuSeparator>
{
    public ContextMenuSeparatorConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<ContextMenuRoot>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<ContextMenuPortal>();
        Context.RenderTree.Add<ContextMenuPositioner>();
        Context.RenderTree.Add<ContextMenuPopup>();

        // The focus manager's publication renders the portal again (Popover's helper, the same bUnit markup fact).
        PopoverConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ContextMenuSeparator> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ContextMenuSeparator part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ContextMenuSeparator> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ContextMenuSeparatorState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ContextMenuSeparator> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ContextMenuSeparatorState, string?>)(_ => classValue()));
    }
}

/// <summary>CTX-STR-01, CTX-STR-02 (adopting MNU-STR-11): <c>MenuSubmenuTrigger.test.tsx</c>'s conformance suite inside a submenu root (describeConformance).</summary>
public sealed class ContextMenuSubmenuTriggerConformanceTests : PartConformanceTests<ContextMenuSubmenuTrigger>
{
    public ContextMenuSubmenuTriggerConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<ContextMenuRoot>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<ContextMenuPortal>();
        Context.RenderTree.Add<ContextMenuPositioner>();
        Context.RenderTree.Add<ContextMenuPopup>();
        Context.RenderTree.Add<ContextMenuSubmenuRoot>();

        // The focus manager's publication renders the portal again (Popover's helper, the same bUnit markup fact).
        PopoverConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<ContextMenuSubmenuTrigger> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(ContextMenuSubmenuTrigger part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<ContextMenuSubmenuTrigger> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<ContextMenuSubmenuTriggerState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<ContextMenuSubmenuTrigger> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<ContextMenuSubmenuTriggerState, string?>)(_ => classValue()));
    }
}
