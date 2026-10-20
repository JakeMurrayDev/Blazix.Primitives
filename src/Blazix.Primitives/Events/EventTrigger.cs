using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// EVD-API-07, ADR 05 §3
/// <summary>
/// Represents the element whose interaction started a request.
/// </summary>
/// <param name="Id">The trigger's <c>id</c>, or <see langword="null"/> when it has none.</param>
/// <param name="Element">The trigger's element reference when C# holds one, or <see langword="null"/> when only JavaScript knows the element.</param>
public sealed record EventTrigger(string? Id, ElementReference? Element);
