using Blazix.Primitives.Rendering;

namespace Blazix.Primitives;

// PRG-DOM-02; progress/root/stateAttributesMapping.ts. ADR 02 decision 4: a custom mapping is hand-written. Every part's
// state is `{ status }`, so one projection serves the five state records: exactly one of data-indeterminate,
// data-progressing and data-complete (value ""), never data-status. The shared static arrays make a render allocate
// nothing (the projection's contract); the attribute names are the five identical *DataAttributes classes' constants.
/// <summary>Provides the mapping from the status of a progress part to its one status data attribute.</summary>
internal static class ProgressStatusMapping
{
    private const string StatusField = "Status";

    private static readonly StateDataAttribute[] s_indeterminate = [new(ProgressRootDataAttributes.Indeterminate, string.Empty)];
    private static readonly StateDataAttribute[] s_progressing = [new(ProgressRootDataAttributes.Progressing, string.Empty)];
    private static readonly StateDataAttribute[] s_complete = [new(ProgressRootDataAttributes.Complete, string.Empty)];
    private static readonly StateFieldProjection<ProgressStatus> s_projection = Project;

    /// <summary>Creates the mapping for a progress part's state record.</summary>
    /// <typeparam name="TState">The type of the state record, whose <c>Status</c> field the mapping replaces.</typeparam>
    /// <returns>The mapping.</returns>
    public static StateAttributesMapping<TState> Create<TState>() => new StateAttributesMapping<TState>().Map(StatusField, s_projection);

    private static StateDataAttribute[]? Project(ProgressStatus status) => status switch
    {
        ProgressStatus.Indeterminate => s_indeterminate,
        ProgressStatus.Progressing => s_progressing,
        ProgressStatus.Complete => s_complete,
        _ => null,
    };
}
