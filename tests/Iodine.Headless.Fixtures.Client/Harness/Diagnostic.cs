namespace Blazix.Primitives.Fixtures.Client.Harness;

/// <summary>One log entry the harness captured for a session (ADR 06 §2 "Diagnostics").</summary>
public sealed record Diagnostic(string Level, string Category, string Message);
