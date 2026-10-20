using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

/// <summary>
/// <c>details.trigger</c> (EVD-API-07, ADR 05 §3): the element whose interaction started a request. <see cref="Id"/> is the
/// trigger's <c>id</c> (<c>null</c> when it has none); <see cref="Element"/> is its reference when C# holds one, <c>null</c>
/// when only JavaScript knows the element.
/// </summary>
public sealed record EventTrigger(string? Id, ElementReference? Element);
