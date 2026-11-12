using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives;

// Base UI's Menu.FilterProvider (menu/filter-provider/MenuFilterProvider.tsx); the contract is docs/components/menu.md
// §3.21 (MNU-STR-14, API-54..58, I18N-03). Filter's shape is track 10's DP3 (d) (settled by the orchestrator 2026-11-04):
// a supplied null turns matching off, an omitted Filter is the collator's contains.
/// <summary>
/// Makes the <see cref="MenuRoot{TPayload}"/> or <see cref="MenuSubmenuRoot"/> rendered directly inside it filterable,
/// Base UI's <c>Menu.FilterProvider</c>. It renders no element of its own.
/// </summary>
/// <remarks>
/// Add a <see cref="MenuInput"/> to the popup and place the items in a <see cref="MenuList"/>. A plain submenu inside a
/// filterable menu stays plain; wrap each searchable submenu in its own provider.
/// </remarks>
public sealed class MenuFilterProvider : ComponentBase
{
    private MenuFilterProviderContext? context;
    private bool filterSupplied;
    private bool valueSupplied;

    [Inject]
    private ILogger<MenuFilterProvider> Logger { get; set; } = null!;

    // MNU-API-54
    /// <summary>Gets or sets the function that decides whether an item matches the query: the item's text and the trimmed query.</summary>
    /// <value>
    /// Leave it unset for the default, which matches the query anywhere in an item's label or rendered text, ignoring case,
    /// marks and punctuation under <see cref="Culture"/>. Set it to <see langword="null"/> to keep every item visible when
    /// the items are filtered by the consumer.
    /// </value>
    [Parameter]
    public Func<string, string, bool>? Filter { get; set; }

    // MNU-API-55
    /// <summary>Gets or sets a value indicating whether filtering highlights the first matching item automatically.</summary>
    /// <value>
    /// <see langword="null"/> or <see langword="false"/>, the default, for no automatic highlight; <see langword="true"/> to
    /// highlight the first match while the query is not empty; <see cref="AutoHighlightValue.Always"/> to highlight it even
    /// when the query is empty.
    /// </value>
    [Parameter]
    public AutoHighlightValue? AutoHighlight { get; set; }

    // MNU-API-56
    /// <summary>Gets or sets the culture of the default filter.</summary>
    /// <value><see langword="null"/>, the default, for <see cref="CultureInfo.CurrentCulture"/>. A custom <see cref="Filter"/> ignores it.</value>
    [Parameter]
    public CultureInfo? Culture { get; set; }

    // MNU-API-57
    /// <summary>Gets or sets the filter query when the consumer owns it.</summary>
    /// <value>
    /// The query the input shows. Supplying it at the first render makes the query controlled for the provider's lifetime:
    /// typing only requests a change, reported through <see cref="ValueChanged"/> and <see cref="OnValueChange"/>.
    /// </value>
    [Parameter]
    public string? Value { get; set; }

    // ADR 05 §2
    /// <summary>Gets or sets the callback that receives the new query, for <c>@bind-Value</c>.</summary>
    /// <value>The callback, invoked after <see cref="OnValueChange"/> unless the change was canceled.</value>
    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    // MNU-API-57
    /// <summary>Gets or sets the initial query when the provider owns it.</summary>
    /// <value>The default is an empty string.</value>
    [Parameter]
    public string DefaultValue { get; set; } = "";

    // MNU-API-58, EVT-16..18
    /// <summary>Gets or sets the callback invoked on every query change, with the new query and the change's details.</summary>
    /// <value>
    /// The callback. Its reason is <see cref="EventReason.InputChange"/>, <see cref="EventReason.InputClear"/>,
    /// <see cref="EventReason.ClearPress"/> or <see cref="EventReason.PopupClose"/>; canceling the details keeps the
    /// current query and highlight.
    /// </value>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<string>> OnValueChange { get; set; }

    /// <summary>Gets or sets the menu root to make filterable.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // Track 10's DP3 (d): a supplied null turns matching off, an omitted Filter is the collator's contains.
        filterSupplied = parameters.TryGetValue<Func<string, string, bool>?>(nameof(Filter), out _);
        valueSupplied = parameters.TryGetValue<string?>(nameof(Value), out _);
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        context ??= new MenuFilterProviderContext(Logger);
        context.Apply(Filter, filterSupplied, AutoHighlight ?? false, Culture, valueSupplied, Value ?? "", DefaultValue, ValueChanged, OnValueChange);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<MenuFilterProviderContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<MenuFilterProviderContext>.Value), context);
        builder.AddComponentParameter(2, nameof(CascadingValue<MenuFilterProviderContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<MenuFilterProviderContext>.ChildContent), ChildContent);
        builder.CloseComponent();
    }
}

// MNU-STR-14 (MenuRoot.tsx:768-783, MenuSubmenuRoot.tsx:22-32): what the provider cascades, IsFixed. The MenuRoot or
// MenuSubmenuRoot directly inside reads it and cascades Consumed in its place, so a plain submenu below stays plain.
/// <summary>Represents the options a filter provider hands to the menu root directly inside it.</summary>
internal sealed class MenuFilterProviderContext
{
    private readonly Controlled<string>? query;
    private readonly List<Action> subscribers = [];
    private EventCallback<string> valueChanged;
    private EventCallback<ValueChangeEventArgs<string>> onValueChange;

    /// <summary>Initializes a new instance of the <see cref="MenuFilterProviderContext"/> class.</summary>
    /// <param name="logger">The logger of the query's development warnings, or <see langword="null"/>.</param>
    public MenuFilterProviderContext(ILogger? logger) => query = new Controlled<string>("MenuFilterProvider", "value", logger);

    private MenuFilterProviderContext()
    {
    }

    /// <summary>Gets the instance a root cascades after consuming its provider: no provider.</summary>
    public static MenuFilterProviderContext Consumed { get; } = new();

    /// <summary>Gets a value indicating whether this instance stands for no provider.</summary>
    public bool IsConsumed => query is null;

    /// <summary>Gets the consumer's filter, or <see langword="null"/>.</summary>
    public Func<string, string, bool>? Filter { get; private set; }

    /// <summary>Gets a value indicating whether <see cref="Filter"/> was supplied, so that a <see langword="null"/> filter turns matching off.</summary>
    public bool FilterSupplied { get; private set; }

    /// <summary>Gets the automatic highlight mode.</summary>
    public AutoHighlightValue AutoHighlight { get; private set; }

    /// <summary>Gets the culture of the default filter.</summary>
    public CultureInfo? Culture { get; private set; }

    /// <summary>Gets the query's committed value: the controlled parameter, or the provider's own.</summary>
    public string Value => query?.Value ?? "";

    /// <summary>Applies one parameter set of the provider and notifies the subscribers.</summary>
    /// <param name="filter">The consumer's filter.</param>
    /// <param name="filterSupplied">Whether the filter parameter was supplied.</param>
    /// <param name="autoHighlight">The automatic highlight mode.</param>
    /// <param name="culture">The culture of the default filter.</param>
    /// <param name="valueSupplied">Whether the query parameter was supplied.</param>
    /// <param name="value">The controlled query.</param>
    /// <param name="defaultValue">The initial query.</param>
    /// <param name="valueChanged">The callback of <c>@bind-Value</c>.</param>
    /// <param name="onValueChange">The cancelable change callback.</param>
    public void Apply(
        Func<string, string, bool>? filter,
        bool filterSupplied,
        AutoHighlightValue autoHighlight,
        CultureInfo? culture,
        bool valueSupplied,
        string value,
        string defaultValue,
        EventCallback<string> valueChanged,
        EventCallback<ValueChangeEventArgs<string>> onValueChange)
    {
        Filter = filter;
        FilterSupplied = filterSupplied;
        AutoHighlight = autoHighlight;
        Culture = culture;
        query!.Update(valueSupplied, value, defaultValueSupplied: true, defaultValue);
        this.valueChanged = valueChanged;
        this.onValueChange = onValueChange;
        Notify();
    }

    /// <summary>Subscribes to changes of the provider's options and query.</summary>
    /// <param name="changed">The callback run after a change.</param>
    /// <returns>The subscription; disposing it unsubscribes.</returns>
    public IDisposable Subscribe(Action changed)
    {
        subscribers.Add(changed);
        return new Subscription(this, changed);
    }

    /// <summary>Requests a query change: <c>OnValueChange</c>, then <c>ValueChanged</c> and the commit unless canceled.</summary>
    /// <param name="value">The new query.</param>
    /// <param name="reason">The reason: input change, input clear, clear press or popup close.</param>
    /// <param name="snapshot">The event the change came from, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the change was not canceled.</returns>
    public async Task<bool> RequestValueAsync(string value, EventReason reason, EventSnapshot? snapshot)
    {
        bool committed = await query!.RequestAsync(value, new ChangeEventDetails(reason, snapshot), onValueChange, valueChanged);
        Notify();
        return committed;
    }

    private void Notify()
    {
        for (int i = 0; i < subscribers.Count; i++)
        {
            subscribers[i]();
        }
    }

    private sealed class Subscription(MenuFilterProviderContext owner, Action changed) : IDisposable
    {
        /// <inheritdoc />
        public void Dispose() => owner.subscribers.Remove(changed);
    }
}
