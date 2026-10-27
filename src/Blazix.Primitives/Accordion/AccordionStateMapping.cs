using System.Globalization;
using Blazix.Primitives.Rendering;

namespace Blazix.Primitives;

// ACC §5.2, §11; accordion/item/stateAttributesMapping.ts:7-12 (collapsibleOpenStateMapping, index → data-index,
// transitionStatusMapping, value → nothing) and accordion/trigger/AccordionTrigger.tsx:14-17 (triggerOpenStateMapping):
// - Item, Header and Panel: Open is data-open or data-closed (ACC-DOM-04);
// - Trigger: Open is data-panel-open while open and nothing otherwise, never data-open or data-closed (ACC-DOM-11);
// - every item part: Index is data-index always, "0" and "-1" included (ACC-DOM-05; settled by the orchestrator 2026-10-25,
//   M3 plan: the generator's integer default omits 0);
// - the value is [NotProjected] (ACC-DOM-09) and the transition status is the collapsible module's (ACC-DOM-14); Disabled,
//   Orientation and Hidden take the default projection (ACC-DOM-06..08, §16 Q3 kept as observed).
// The shared static arrays make a render allocate nothing for the indexes a page shows.
/// <summary>Provides the mappings from the open and index fields of an accordion part's state to their data attributes.</summary>
internal static class AccordionStateMapping
{
    private const string OpenField = "Open";
    private const string IndexField = "Index";
    private const int CachedIndexes = 64;

    private static readonly StateDataAttribute[] s_open = [new(AccordionItemDataAttributes.Open, string.Empty)];
    private static readonly StateDataAttribute[] s_closed = [new("data-closed", string.Empty)];
    private static readonly StateDataAttribute[] s_panelOpen = [new(AccordionTriggerDataAttributes.PanelOpen, string.Empty)];
    private static readonly StateDataAttribute[][] s_indexes = CreateIndexes();
    private static readonly StateFieldProjection<bool> s_openProjection = ProjectOpen;
    private static readonly StateFieldProjection<bool> s_panelOpenProjection = ProjectPanelOpen;
    private static readonly StateFieldProjection<int> s_indexProjection = ProjectIndex;

    /// <summary>Creates the mapping of an item, header or panel state record.</summary>
    /// <typeparam name="TState">The type of the state record, whose <c>Open</c> and <c>Index</c> fields the mapping replaces.</typeparam>
    /// <returns>The mapping.</returns>
    public static StateAttributesMapping<TState> CreateItem<TState>() =>
        new StateAttributesMapping<TState>()
            .Map(OpenField, s_openProjection)
            .Map(IndexField, s_indexProjection);

    /// <summary>Creates the mapping of a trigger state record.</summary>
    /// <typeparam name="TState">The type of the state record, whose <c>Open</c> and <c>Index</c> fields the mapping replaces.</typeparam>
    /// <returns>The mapping.</returns>
    public static StateAttributesMapping<TState> CreateTrigger<TState>() =>
        new StateAttributesMapping<TState>()
            .Map(OpenField, s_panelOpenProjection)
            .Map(IndexField, s_indexProjection);

    private static StateDataAttribute[] ProjectOpen(bool open) => open ? s_open : s_closed;

    private static StateDataAttribute[]? ProjectPanelOpen(bool open) => open ? s_panelOpen : null;

    private static StateDataAttribute[] ProjectIndex(int index) =>
        index >= -1 && index < CachedIndexes - 1
            ? s_indexes[index + 1]
            : [new(AccordionItemDataAttributes.Index, index.ToString(CultureInfo.InvariantCulture))];

    private static StateDataAttribute[][] CreateIndexes()
    {
        StateDataAttribute[][] indexes = new StateDataAttribute[CachedIndexes][];
        for (int i = 0; i < CachedIndexes; i++)
        {
            indexes[i] = [new(AccordionItemDataAttributes.Index, (i - 1).ToString(CultureInfo.InvariantCulture))];
        }

        return indexes;
    }
}
