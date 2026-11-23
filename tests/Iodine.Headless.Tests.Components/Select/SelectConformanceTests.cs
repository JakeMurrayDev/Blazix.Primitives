using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>SEL-DOM-55, SEL-DOM-01, SEL-API-25, SEL-API-27: <c>SelectTrigger.test.tsx</c>'s conformance suite inside <c>Root open</c> (<c>refInstanceof: HTMLButtonElement</c>, <c>button: true</c>, so the substitution titles render a <c>div</c> with <c>NativeButton="false"</c>).</summary>
public sealed class SelectTriggerConformanceTests : PartConformanceTests<SelectTrigger<string>>
{
    public SelectTriggerConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<SelectRoot<string>>(p => p.Add(x => x.Open, true));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<SelectTrigger<string>> Options { get; } = new("button") { Button = true };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(SelectTrigger<string> part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<SelectTrigger<string>> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<SelectTriggerState<string>>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<SelectTrigger<string>> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<SelectTriggerState<string>, string?>)(_ => classValue()));
    }
}

/// <summary>SEL-DOM-55, SEL-DOM-10: <c>SelectValue.test.tsx</c>'s conformance suite inside <c>Root open</c> (<c>refInstanceof: HTMLSpanElement</c>).</summary>
public sealed class SelectValueConformanceTests : PartConformanceTests<SelectValue<string>>
{
    public SelectValueConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<SelectRoot<string>>(p => p.Add(x => x.Open, true));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<SelectValue<string>> Options { get; } = new("span");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(SelectValue<string> part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<SelectValue<string>> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<SelectValueState<string>>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<SelectValue<string>> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<SelectValueState<string>, string?>)(_ => classValue()));
    }
}

/// <summary>SEL-DOM-55, SEL-DOM-13, SEL-STR-12: <c>SelectIcon.test.tsx</c>'s conformance suite inside <c>Root open</c> (<c>refInstanceof: HTMLSpanElement</c>).</summary>
public sealed class SelectIconConformanceTests : PartConformanceTests<SelectIcon>
{
    public SelectIconConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<SelectRoot<string>>(p => p.Add(x => x.Open, true));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<SelectIcon> Options { get; } = new("span");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(SelectIcon part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<SelectIcon> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<SelectIconState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<SelectIcon> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<SelectIconState, string?>)(_ => classValue()));
    }
}

/// <summary>SEL-DOM-55, SEL-DOM-16, SEL-STR-06: <c>SelectPortal.test.tsx</c>'s conformance suite inside <c>Root open</c>.</summary>
public sealed class SelectPortalConformanceTests : PartConformanceTests<SelectPortal>
{
    public SelectPortalConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<SelectRoot<string>>(p => p.Add(x => x.Open, true));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<SelectPortal> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(SelectPortal part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<SelectPortal> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<SelectPortal> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}

/// <summary>SEL-DOM-55, SEL-DOM-17, SEL-STR-12: <c>SelectBackdrop.test.tsx</c>'s conformance suite inside <c>Root open</c>.</summary>
public sealed class SelectBackdropConformanceTests : PartConformanceTests<SelectBackdrop>
{
    public SelectBackdropConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<SelectRoot<string>>(p => p.Add(x => x.Open, true));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<SelectBackdrop> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(SelectBackdrop part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<SelectBackdrop> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<SelectBackdropState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<SelectBackdrop> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<SelectBackdropState, string?>)(_ => classValue()));
    }
}

/// <summary>SEL-DOM-55, SEL-DOM-18, SEL-STR-07: <c>SelectPositioner.test.tsx</c>'s conformance suite inside <c>Root open</c> and a Portal (its <c>positionerConformanceTests</c> are L4).</summary>
public sealed class SelectPositionerConformanceTests : PartConformanceTests<SelectPositioner>
{
    public SelectPositionerConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<SelectRoot<string>>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<SelectPortal>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<SelectPositioner> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(SelectPositioner part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<SelectPositioner> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<SelectPositionerState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<SelectPositioner> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<SelectPositionerState, string?>)(_ => classValue()));
    }
}

/// <summary>SEL-DOM-55, SEL-DOM-23, SEL-STR-08: <c>SelectPopup.test.tsx</c>'s conformance suite inside <c>Root open</c>, a Portal and a Positioner.</summary>
public sealed class SelectPopupConformanceTests : PartConformanceTests<SelectPopup>
{
    public SelectPopupConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<SelectRoot<string>>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<SelectPortal>();
        Context.RenderTree.Add<SelectPositioner>();

        // The part renders again in its mount batch (the focus manager's publication, the group label's id); Blazor drops the element
        // reference capture of a re-rendered frame, so the two ref titles look it up in a part that renders once.
        SelectConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<SelectPopup> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(SelectPopup part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<SelectPopup> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<SelectPopupState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<SelectPopup> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<SelectPopupState, string?>)(_ => classValue()));
    }
}

/// <summary>SEL-DOM-55, SEL-DOM-31, SEL-STR-08: <c>SelectList.test.tsx</c>'s conformance suite inside <c>Root open</c>, a Portal, a Positioner and a Popup.</summary>
public sealed class SelectListConformanceTests : PartConformanceTests<SelectList>
{
    public SelectListConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<SelectRoot<string>>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<SelectPortal>();
        Context.RenderTree.Add<SelectPositioner>();
        Context.RenderTree.Add<SelectPopup>();

        // The part renders again in its mount batch (the focus manager's publication, the group label's id); Blazor drops the element
        // reference capture of a re-rendered frame, so the two ref titles look it up in a part that renders once.
        SelectConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<SelectList> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(SelectList part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<SelectList> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<SelectList> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}

/// <summary>SEL-DOM-55, SEL-DOM-40, SEL-STR-09: <c>SelectArrow.test.tsx</c>'s conformance suite inside <c>Root open</c> and a Positioner with <c>AlignItemWithTrigger="false"</c>, because the Arrow renders nothing while the popup is aligned.</summary>
public sealed class SelectArrowConformanceTests : PartConformanceTests<SelectArrow>
{
    public SelectArrowConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<SelectRoot<string>>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<SelectPositioner>(p => p.Add(x => x.AlignItemWithTrigger, false));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<SelectArrow> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(SelectArrow part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<SelectArrow> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<SelectArrowState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<SelectArrow> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<SelectArrowState, string?>)(_ => classValue()));
    }
}

/// <summary>SEL-DOM-55, SEL-DOM-34, SEL-API-43: <c>SelectItem.test.tsx</c>'s conformance suite for <c>&lt;Select.Item value="" /&gt;</c> inside <c>Root open</c> (<c>refInstanceof: HTMLDivElement</c>, <c>button: true</c>, so the substitution titles render a <c>div</c> with <c>NativeButton="false"</c>).</summary>
public sealed class SelectItemConformanceTests : PartConformanceTests<SelectItem<string>>
{
    public SelectItemConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<SelectRoot<string>>(p => p.Add(x => x.Open, true));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<SelectItem<string>> Options { get; } = new("div") { Button = true, Required = p => p.Add(x => x.Value, "") };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(SelectItem<string> part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<SelectItem<string>> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<SelectItemState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<SelectItem<string>> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<SelectItemState, string?>)(_ => classValue()));
    }
}

/// <summary>SEL-DOM-55, SEL-DOM-38, SEL-STR-12: <c>SelectItemText.test.tsx</c>'s conformance suite inside <c>Root open</c>, a Positioner and an Item with <c>value=""</c>.</summary>
public sealed class SelectItemTextConformanceTests : PartConformanceTests<SelectItemText>
{
    public SelectItemTextConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<SelectRoot<string>>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<SelectPositioner>();
        Context.RenderTree.Add<SelectItem<string>>(p => p.Add(x => x.Value, ""));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<SelectItemText> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(SelectItemText part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<SelectItemText> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<SelectItemText> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}

/// <summary>SEL-DOM-55, SEL-DOM-39, SEL-STR-11: <c>SelectItemIndicator.test.tsx</c>'s conformance suite inside <c>Root open</c>, a Trigger holding a Value beside a Positioner, and an Item without a value.</summary>
public sealed class SelectItemIndicatorConformanceTests : PartConformanceTests<SelectItemIndicator>
{
    public SelectItemIndicatorConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<SelectItemIndicatorConformanceTree>();

        // The item renders the indicator again in its mount batch; Blazor drops the element reference capture of a re-rendered
        // frame, so the two ref titles look it up in a part that renders once.
        SelectConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<SelectItemIndicator> Options { get; } = new("span");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(SelectItemIndicator part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<SelectItemIndicator> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<SelectItemIndicatorState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<SelectItemIndicator> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<SelectItemIndicatorState, string?>)(_ => classValue()));
    }
}

/// <summary>SEL-DOM-55, SEL-DOM-41, SEL-STR-12: <c>SelectGroup.test.tsx</c>'s conformance suite inside <c>Root open</c>.</summary>
public sealed class SelectGroupConformanceTests : PartConformanceTests<SelectGroup>
{
    public SelectGroupConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<SelectRoot<string>>(p => p.Add(x => x.Open, true));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<SelectGroup> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(SelectGroup part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<SelectGroup> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<SelectGroup> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}

/// <summary>SEL-DOM-55, SEL-DOM-42, SEL-STR-12: <c>SelectGroupLabel.test.tsx</c>'s conformance suite inside <c>Root open</c> and a Group.</summary>
public sealed class SelectGroupLabelConformanceTests : PartConformanceTests<SelectGroupLabel>
{
    public SelectGroupLabelConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<SelectRoot<string>>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<SelectGroup>();

        // The part renders again in its mount batch (the focus manager's publication, the group label's id); Blazor drops the element
        // reference capture of a re-rendered frame, so the two ref titles look it up in a part that renders once.
        SelectConformance.AddRenderedOnceForRefTitles(Context);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<SelectGroupLabel> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(SelectGroupLabel part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<SelectGroupLabel> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<SelectGroupLabel> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<EmptyState, string?>)(_ => classValue()));
    }
}

/// <summary>SEL-DOM-55, SEL-DOM-14: <c>SelectLabel.test.tsx</c>'s conformance suite inside a closed Root, beside a Trigger and a Portal holding a Positioner.</summary>
public sealed class SelectLabelConformanceTests : PartConformanceTests<SelectLabel>
{
    public SelectLabelConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<SelectLabelConformanceTree>();

        // The label publishes its id to the Root, which renders the label again in its mount batch; Blazor drops the element
        // reference capture of a re-rendered frame, so the three titles that look the captured element up in bUnit's markup
        // run on a label that renders once.
        SelectConformance.AddRenderedOnceForRefTitles(Context, includeAttachesTheRef: true);
    }

    /// <inheritdoc />
    protected override ConformanceOptions<SelectLabel> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(SelectLabel part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<SelectLabel> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<SelectLabelState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<SelectLabel> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<SelectLabelState, string?>)(_ => classValue()));
    }
}

/// <summary>SEL-DOM-55, SEL-DOM-43, SEL-STR-10: <c>SelectScrollUpArrow.test.tsx</c>'s conformance suite for <c>&lt;Select.ScrollUpArrow keepMounted /&gt;</c> inside <c>Root open</c> and a Positioner.</summary>
public sealed class SelectScrollUpArrowConformanceTests : PartConformanceTests<SelectScrollUpArrow>
{
    public SelectScrollUpArrowConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<SelectRoot<string>>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<SelectPositioner>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<SelectScrollUpArrow> Options { get; } = new("div") { Required = p => p.Add(x => x.KeepMounted, true) };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(SelectScrollUpArrow part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<SelectScrollUpArrow> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<SelectScrollArrowState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<SelectScrollUpArrow> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<SelectScrollArrowState, string?>)(_ => classValue()));
    }
}

/// <summary>SEL-DOM-55, SEL-DOM-43, SEL-STR-10: <c>SelectScrollDownArrow.test.tsx</c>'s conformance suite for <c>&lt;Select.ScrollDownArrow keepMounted /&gt;</c> inside <c>Root open</c> and a Positioner.</summary>
public sealed class SelectScrollDownArrowConformanceTests : PartConformanceTests<SelectScrollDownArrow>
{
    public SelectScrollDownArrowConformanceTests()
    {
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<SelectRoot<string>>(p => p.Add(x => x.Open, true));
        Context.RenderTree.Add<SelectPositioner>();
    }

    /// <inheritdoc />
    protected override ConformanceOptions<SelectScrollDownArrow> Options { get; } = new("div") { Required = p => p.Add(x => x.KeepMounted, true) };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(SelectScrollDownArrow part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<SelectScrollDownArrow> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<SelectScrollArrowState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<SelectScrollDownArrow> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<SelectScrollArrowState, string?>)(_ => classValue()));
    }
}

/// <summary>
/// The tree around the part of <c>SelectItemIndicator.test.tsx</c>'s conformance suite: <c>Root open</c> holding a Trigger
/// with a Value, then a Positioner holding an Item with the part inside. bUnit's render tree wraps a part in one chain,
/// so the sibling Trigger needs this component.
/// </summary>
internal sealed class SelectItemIndicatorConformanceTree : ComponentBase
{
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<SelectRoot<string>>(0);
        builder.AddComponentParameter(1, nameof(SelectRoot<string>.Open), true);
        builder.AddComponentParameter(2, nameof(SelectRoot<string>.ChildContent), (RenderFragment)(root =>
        {
            root.OpenComponent<SelectTrigger<string>>(0);
            root.AddComponentParameter(1, nameof(SelectTrigger<string>.ChildContent), (RenderFragment)(trigger =>
            {
                trigger.OpenComponent<SelectValue<string>>(0);
                trigger.CloseComponent();
            }));
            root.CloseComponent();
            root.OpenComponent<SelectPositioner>(2);
            root.AddComponentParameter(3, nameof(SelectPositioner.ChildContent), (RenderFragment)(positioner =>
            {
                positioner.OpenComponent<SelectItem<string>>(0);
                positioner.AddComponentParameter(1, nameof(SelectItem<string>.ChildContent), ChildContent);
                positioner.CloseComponent();
            }));
            root.CloseComponent();
        }));
        builder.CloseComponent();
    }
}

/// <summary>
/// The tree around the part of <c>SelectLabel.test.tsx</c>'s conformance suite: a closed Root holding the Label, a Trigger
/// and a Portal with a Positioner. bUnit's render tree wraps a part in one chain, so the siblings need this component.
/// </summary>
internal sealed class SelectLabelConformanceTree : ComponentBase
{
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<SelectRoot<string>>(0);
        builder.AddComponentParameter(1, nameof(SelectRoot<string>.ChildContent), (RenderFragment)(root =>
        {
            root.AddContent(0, ChildContent);
            root.OpenComponent<SelectTrigger<string>>(1);
            root.CloseComponent();
            root.OpenComponent<SelectPortal>(2);
            root.AddComponentParameter(3, nameof(SelectPortal.ChildContent), (RenderFragment)(portal =>
            {
                portal.OpenComponent<SelectPositioner>(0);
                portal.CloseComponent();
            }));
            root.CloseComponent();
        }));
        builder.CloseComponent();
    }
}

/// <summary>The wrappers the Select conformance suites share.</summary>
internal static class SelectConformance
{
    /// <summary>
    /// Adds <see cref="RenderedOnce"/> for the titles that look the captured element up in bUnit's markup, around the part
    /// under test (the innermost wrapper).
    /// </summary>
    /// <param name="context">The test's context.</param>
    /// <param name="includeAttachesTheRef">Whether the part renders again before the first of those titles looks, so that "attaches the ref" needs it too.</param>
    public static void AddRenderedOnceForRefTitles(BunitContext context, bool includeAttachesTheRef = false)
    {
        string? method = Xunit.TestContext.Current.TestMethod?.MethodName;
        if (method is "Should_pass_the_ref_to_the_custom_component" or "Should_merge_the_rendering_element_ref_with_the_custom_component_ref"
            || (includeAttachesTheRef && method is "Attaches_the_ref"))
        {
            context.RenderTree.Add<RenderedOnce>();
        }
    }
}
