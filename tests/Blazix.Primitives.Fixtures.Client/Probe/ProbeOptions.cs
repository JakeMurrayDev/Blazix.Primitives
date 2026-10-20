using Blazix.Primitives.Interop;

namespace Blazix.Primitives.Fixtures.Client.Probe;

/// <summary>The probe module's options: the shared style string plus a label the module writes to the element.</summary>
internal sealed record ProbeOptions : TwinOptions
{
    public string Label { get; init; } = "";
}

/// <summary>The payload of the probe module's click callback.</summary>
internal sealed record ProbeClick(string Source, int Count);
