using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives;

// Base UI's NavigationMenu.Item (navigation-menu/item/NavigationMenuItem.tsx); the contract is
// docs/components/navigation-menu.md §3.3, §5.3.
// - An `li` with no attribute of its own (NAV-DOM-05); its effective value is its Value unless absent or null, then a
//   generated id stable for the Item's lifetime, under TValue = string only (NAV-API-11, NAV-STA-10; the Accordion rule
//   of ADR 08 Amendments 2026-10-26: an always-on error under any other TValue).
// - It cascades the non-generic NavigationMenuItemContext, fixed, to its Trigger, Icon and Content, which render only when
//   the Item's own active state flips (NAV-STA-11; ADR 16 rules 8 and 9). Duplicate values make every matching Item active
//   (NAV-EDGE-07, §16 Q6 replicated).
/// <summary>
/// Represents an item of a navigation menu: Base UI's <c>NavigationMenu.Item</c>, rendered as an <c>li</c>. It groups a
/// trigger and its content, or holds a link.
/// </summary>
/// <typeparam name="TValue">The type of the value of the root.</typeparam>
public sealed class NavigationMenuItem<TValue> : PartBase<EmptyState>
{
    private const string MissingValueMessage =
        "Blazix: A `NavigationMenuItem` without a `Value` is identified by a generated id, which needs a `TValue` of `string`. "
        + "Give the item a `Value`, or set `TValue=\"string\"` on the `NavigationMenuRoot`.";

    private const string TypeMismatchMessage =
        "Blazix: A `NavigationMenuItem` must have the `TValue` of its `NavigationMenuRoot`. Set `TValue` on the item, or give `Value` the root's type.";

    private readonly RenderFragment renderCascade;
    private NavigationMenuItemContext<TValue>? context;
    private string? fallbackValue;
    private bool valueSupplied;

    /// <summary>Initializes a new instance of the <see cref="NavigationMenuItem{TValue}"/> class.</summary>
    public NavigationMenuItem()
    {
        renderCascade = RenderCascade;
    }

    [Inject]
    private PartIdGenerator Ids { get; set; } = null!;

    [CascadingParameter]
    private NavigationMenuRootContext? RootContext { get; set; }

    // NAV-API-11
    /// <summary>Gets or sets the value that identifies the item, which the root's value selects.</summary>
    /// <value>The value; <see langword="null"/> or absent identifies the item by a generated id, which needs a <c>TValue</c> of <see cref="string"/>.</value>
    [Parameter]
    public TValue? Value { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "li";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        valueSupplied = parameters.TryGetValue<TValue>(nameof(Value), out _);
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// The item is not inside a <see cref="NavigationMenuRoot{TValue}"/>, or its root's value type differs from
    /// <typeparamref name="TValue"/>.
    /// </exception>
    protected override void OnInitialized()
    {
        NavigationMenuRootContext root = NavigationMenuRootContext.Require(RootContext);
        NavigationMenuRootContext<TValue> typed = root as NavigationMenuRootContext<TValue> ?? throw new InvalidOperationException(TypeMismatchMessage);
        fallbackValue = Ids.Next();
        context = new NavigationMenuItemContext<TValue>(typed);
        context.SetValue(Identity());
        root.RegisterItem(context);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        TValue? identity = Identity();
        if (!Equality.StrictEquals(context!.Value, identity))
        {
            context.SetValue(identity);
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        NavigationMenuCascades.Render<NavigationMenuItemContext>(builder, context!, renderCascade);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && context is not null)
        {
            context.Root.UnregisterItem(context);
        }

        base.Dispose(disposing);
    }

    private void RenderCascade(RenderTreeBuilder builder) => base.BuildRenderTree(builder);

    // NAV-API-11; ADR 08 Amendments 2026-10-26 (the Accordion rule).
    private TValue? Identity()
    {
        if (valueSupplied && Value is not null)
        {
            return Value;
        }

        if (typeof(TValue) == typeof(string))
        {
            return (TValue)(object)fallbackValue!;
        }

        throw new InvalidOperationException(MissingValueMessage);
    }
}
