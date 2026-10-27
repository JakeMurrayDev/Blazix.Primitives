using Microsoft.Extensions.Logging;

namespace Blazix.Primitives;

// TGL-EDGE-01, TGG-EDGE-01 (toggle/Toggle.tsx:48-59): "once, even when several toggles qualify". Base UI's logger keeps one
// set per page, so the flag is a scoped service (one per Server circuit, one per WebAssembly application), registered by
// `AddBlazixPrimitives()` like ButtonDevelopmentLog, never a static, which on Server would be one per process (chosen by the
// user 2026-10-26, M3 plan §Settled). The text is Base UI's with the port's prefix and parameter name (the PRG-EDGE-01
// precedent).
/// <summary>Writes the development message of a toggle without a value in an initialized toggle group, once per session.</summary>
internal sealed class ToggleDevelopmentLog
{
    // TGL-EDGE-01 in the port's words.
    internal const string MissingValueMessage =
        "Blazix: A `<Toggle>` component rendered in a `<ToggleGroup>` has no explicit `Value` parameter. "
        + "This will cause issues between the Toggle Group and Toggle values. "
        + "Provide the `<Toggle>` with a `Value` parameter matching the `<ToggleGroup>` values parameter type.";

    private bool missingValueWritten;

    /// <summary>Gets a value indicating whether this session already wrote the missing-value message.</summary>
    /// <value><see langword="true"/> once the message was written.</value>
    public bool MissingValueWritten => missingValueWritten;

    /// <summary>Writes the missing-value message, unless this session already has or the development warnings are off.</summary>
    /// <param name="logger">The logger of the toggle that has no value.</param>
    public void ReportMissingValue(ILogger logger)
    {
        if (missingValueWritten || !DevelopmentWarnings.IsEnabled)
        {
            return;
        }

        missingValueWritten = true;
        DevelopmentWarnings.Write(logger, MissingValueMessage);
    }
}
