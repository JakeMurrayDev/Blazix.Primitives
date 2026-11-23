using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives;

// AUT-STR-02, STR-05 (M5 track 11; decision 12, ADR 08 Amendments 2026-11-03 (3)): the Autocomplete parts of this file
// are Combobox's, as sealed shells over the same internal behaviours with the family `Autocomplete`; the comments below
// cite the Combobox rows the Autocomplete document adopts (autocomplete.md §2.1).

// Base UI's Combobox.Icon (combobox/icon/AutocompleteIcon.tsx; CMB §3.11, STR-08): a span hidden from assistive technology with
// "▼" by default.
/// <summary>Represents the icon of an autocomplete's trigger, Base UI's <c>Autocomplete.Icon</c>, rendered as a <c>span</c>.</summary>
public sealed class AutocompleteIcon : PartBase<EmptyState>
{
    [CascadingParameter]
    private ComboboxStore? Root { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "span";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        ComboboxParts.RequireRoot(Root, "Autocomplete");
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => ChildContent ??= ComboboxStaticAttributes.IconContent;

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => ComboboxStaticAttributes.WriteIcon(attributes);
}

// Base UI's Combobox.Row (combobox/row/AutocompleteRow.tsx; CMB §3.23): role="row", its items gridcells.
/// <summary>Represents a row of an autocomplete's grid, Base UI's <c>Autocomplete.Row</c>, rendered as a <c>div</c>.</summary>
public sealed class AutocompleteRow : PartBase<EmptyState>
{
    private readonly ComboboxRowBehavior behavior = new();

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // The part replaces ChildContent with its own cascade, so a parameter set without content must not keep it.
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => ChildContent = behavior.Cascade(ChildContent);

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => ComboboxStaticAttributes.WriteRow(attributes);
}

// Base UI's Combobox.Collection (combobox/collection/AutocompleteCollection.tsx; CMB §3.20, STR-06): renders no element; maps the
// enclosing Group's items, else the root's filtered items, through its template.
/// <summary>Renders an item template once per item of the enclosing group or of the root's filtered items, Base UI's <c>Autocomplete.Collection</c>.</summary>
/// <typeparam name="TValue">The type of the items' values; inferred from the enclosing root.</typeparam>
[CascadingTypeParameter(nameof(TValue))]
public sealed class AutocompleteCollection<TValue> : ComponentBase, IDisposable
{
    private ComboboxCollectionBehavior<TValue>? behavior;

    [CascadingParameter]
    private ComboboxStore? Root { get; set; }

    [CascadingParameter]
    private ComboboxGroupContext<TValue>? Group { get; set; }

    /// <summary>Gets or sets the template of an item, rendered once per item with its value and index.</summary>
    [Parameter]
    public RenderFragment<ComboboxListItem<TValue>>? ItemContent { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new ComboboxCollectionBehavior<TValue>("Autocomplete", StateHasChanged);
        behavior.Subscribe(Root, Group);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder) => behavior!.BuildItems(builder, ItemContent);

    /// <inheritdoc />
    public void Dispose() => behavior?.Dispose();
}

// Base UI's Combobox.Group (combobox/group/AutocompleteGroup.tsx; CMB §3.21): role group (rowgroup in a grid) labelled by its
// GroupLabel; its Items feed a Collection inside.
/// <summary>Represents a group of an autocomplete's items, Base UI's <c>Autocomplete.Group</c>, rendered as a <c>div</c>.</summary>
/// <typeparam name="TValue">The type of the items' values; inferred from the enclosing root.</typeparam>
[CascadingTypeParameter(nameof(TValue))]
public sealed class AutocompleteGroup<TValue> : PartBase<EmptyState>
{
    private ComboboxGroupBehavior<TValue>? behavior;

    [Inject]
    private Microsoft.JSInterop.IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private Microsoft.Extensions.Logging.ILogger<AutocompleteGroup<TValue>> Logger { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private ComboboxStore? Root { get; set; }

    // CMB-API-62
    /// <summary>Gets or sets the group's items, which a <see cref="AutocompleteCollection{TValue}"/> inside the group renders.</summary>
    [Parameter]
    public IReadOnlyList<TValue>? Items { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // The part replaces ChildContent with its own cascade, so a parameter set without content must not keep it.
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new ComboboxGroupBehavior<TValue>(new ComboboxPartServices("Autocomplete", JSRuntime, Logger, IdGenerator, StateHasChanged, InvokeAsync, DispatchExceptionAsync));
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        behavior!.Apply(Items);
        ChildContent = behavior.Cascade(ChildContent);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            behavior?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors() => behavior!.Subscribe(Root, SubscribeTo);

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddPartAttributes(attributes);
}

// Base UI's Combobox.GroupLabel (combobox/group-label/AutocompleteGroupLabel.tsx; CMB §3.22): aria-hidden, the group's label.
/// <summary>Represents the label of an autocomplete group, Base UI's <c>Autocomplete.GroupLabel</c>, rendered as a <c>div</c>.</summary>
public sealed class AutocompleteGroupLabel : PartBase<EmptyState>
{
    private ComboboxGroupLabelBehavior? behavior;

    [CascadingParameter]
    private IComboboxGroupContext? Group { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new ComboboxGroupLabelBehavior("Autocomplete");
        behavior.Subscribe(Group);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => behavior!.Apply(Id);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            behavior?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddPartAttributes(attributes);
}
