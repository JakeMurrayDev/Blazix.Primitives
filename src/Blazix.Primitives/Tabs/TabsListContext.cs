using Blazix.Primitives.Interop;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// TAB §11 (TabsListContext); tabs/list/TabsListContext.ts. The List's registry of its Tabs by element id: the `tabs`
// module reports an activation, or a click or focus it did not activate, by the Tab's id (ADR 04 Amendments 2026-10-26;
// values never cross to JavaScript), and the List asks for the reports only while a Tab has a consumer handler (design
// point 5 of the Tabs track, settled by the orchestrator 2026-10-26). Cascaded fixed (ADR 16 rule 9).
/// <summary>The registry a tab list shares with the tabs and indicators rendered inside it.</summary>
internal sealed class TabsListContext
{
    private readonly List<TabsTabEntry> tabs = [];
    private readonly Action handlersChanged;

    /// <summary>Initializes a new instance of the <see cref="TabsListContext"/> class.</summary>
    /// <param name="handlersChanged">Called when a tab gained or lost a consumer click or focus handler.</param>
    public TabsListContext(Action handlersChanged)
    {
        this.handlersChanged = handlersChanged;
    }

    /// <summary>Gets a value indicating whether a tab of the list has a consumer <c>onclick</c>.</summary>
    /// <value><see langword="true"/> while at least one registered tab has one.</value>
    public bool AnyClickHandler
    {
        get
        {
            for (int i = 0; i < tabs.Count; i++)
            {
                if (tabs[i].HasConsumerClick)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Gets a value indicating whether a tab of the list has a consumer <c>onfocus</c>.</summary>
    /// <value><see langword="true"/> while at least one registered tab has one.</value>
    public bool AnyFocusHandler
    {
        get
        {
            for (int i = 0; i < tabs.Count; i++)
            {
                if (tabs[i].HasConsumerFocus)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Throws the error of a tab or indicator rendered outside a tab list.</summary>
    /// <param name="context">The cascaded context, or <see langword="null"/>.</param>
    /// <returns>The context.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static TabsListContext Require(TabsListContext? context)
    {
        // TAB-EDGE-02: thrown in every build (ADR 02 Amendments 2026-10-20), the RadioIndicator rewrite as precedent.
        return context ?? throw new InvalidOperationException(
            "Blazix: The `TabsList` context is missing. `TabsTab` and `TabsIndicator` must be placed within `<TabsList>`.");
    }

    /// <summary>Adds a tab to the registry.</summary>
    /// <param name="tab">The tab's entry.</param>
    public void Register(TabsTabEntry tab)
    {
        tabs.Add(tab);
        if (tab.HasConsumerClick || tab.HasConsumerFocus)
        {
            handlersChanged();
        }
    }

    /// <summary>Removes a tab from the registry.</summary>
    /// <param name="tab">The tab's entry.</param>
    public void Unregister(TabsTabEntry tab)
    {
        if (tabs.Remove(tab) && (tab.HasConsumerClick || tab.HasConsumerFocus))
        {
            handlersChanged();
        }
    }

    /// <summary>Records that a registered tab gained or lost a consumer click or focus handler.</summary>
    public void HandlersChanged() => handlersChanged();

    /// <summary>Finds the tab whose element has the given id.</summary>
    /// <param name="id">The element id, or <see langword="null"/>.</param>
    /// <returns>The tab's entry, or <see langword="null"/>.</returns>
    public TabsTabEntry? Find(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        for (int i = 0; i < tabs.Count; i++)
        {
            if (string.Equals(tabs[i].ElementId, id, StringComparison.Ordinal))
            {
                return tabs[i];
            }
        }

        return null;
    }

    /// <summary>Gets the fragment that cascades this context to <paramref name="content"/>.</summary>
    /// <param name="content">The content that receives the context.</param>
    /// <returns>The fragment.</returns>
    internal RenderFragment Cascade(RenderFragment? content) => builder =>
    {
        builder.OpenComponent<CascadingValue<TabsListContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<TabsListContext>.Value), this);
        builder.AddComponentParameter(2, nameof(CascadingValue<TabsListContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<TabsListContext>.ChildContent), content);
        builder.CloseComponent();
    };
}

// The List reaches a generic Tab through this entry: the activation the module reports names the Tab by its element id.
/// <summary>A tab as its list's registry sees it, independent of the type of its value.</summary>
internal abstract class TabsTabEntry
{
    /// <summary>Gets the id of the tab's element.</summary>
    /// <value>The consumer's id or the generated one.</value>
    public abstract string ElementId { get; }

    /// <summary>Gets a value indicating whether the consumer gave the tab an <c>onclick</c>.</summary>
    /// <value><see langword="true"/> while the tab has one.</value>
    public abstract bool HasConsumerClick { get; }

    /// <summary>Gets a value indicating whether the consumer gave the tab an <c>onfocus</c>.</summary>
    /// <value><see langword="true"/> while the tab has one.</value>
    public abstract bool HasConsumerFocus { get; }

    /// <summary>Runs an activation inside the dispatch: the consumer's handler for the event, then the request.</summary>
    /// <param name="snapshot">The <c>click</c> or <c>focusin</c>.</param>
    /// <param name="direction">The direction the browser-side behavior measured.</param>
    /// <returns><see cref="DecisionPolicy.Stop"/> when the consumer's handler called <c>PreventPartHandler()</c>; otherwise <see cref="DecisionPolicy.Run"/>.</returns>
    public abstract DecisionPolicy DecideActivation(EventSnapshot snapshot, TabsActivationDirection direction);

    /// <summary>Runs an activation the browser-side behavior reported: the consumer's handler for the event, then the request.</summary>
    /// <param name="snapshot">The <c>click</c> or <c>focusin</c>.</param>
    /// <param name="direction">The direction the browser-side behavior measured.</param>
    /// <returns>A task that completes when the request has run.</returns>
    public abstract Task ActivateAsync(EventSnapshot snapshot, TabsActivationDirection direction);

    /// <summary>Runs the consumer's handler for a click or focus that activated nothing.</summary>
    /// <param name="snapshot">The <c>click</c> or <c>focusin</c>.</param>
    /// <returns>The consumer handler's task.</returns>
    public abstract Task RunConsumerHandlerAsync(EventSnapshot snapshot);
}
