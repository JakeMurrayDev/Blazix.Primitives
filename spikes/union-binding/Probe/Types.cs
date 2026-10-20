using Microsoft.AspNetCore.Components;

namespace Probe;

/// <summary>Stand-in for the pipeline's render props (ADR 02).</summary>
public sealed record RenderProps<TState>(TState State);

/// <summary>Stand-in for a part's state record.</summary>
public sealed record CheckedState(bool Checked);

/// <summary>Case A/E: the compiler-generated union of ADR 05 §4.</summary>
public union RenderValue<TState>(RenderFragment, RenderFragment<RenderProps<TState>>);

/// <summary>Case B: ADR 05 §4 / ADR 08 §2.</summary>
public union ClassValue<TState>(string, Func<TState, string?>);

/// <summary>Same shape as <see cref="ClassValue{TState}"/>; declared only to show it compiles.</summary>
public union StyleValue<TState>(string, Func<TState, string?>);
