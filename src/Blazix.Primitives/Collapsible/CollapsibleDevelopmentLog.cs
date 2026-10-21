using Microsoft.Extensions.Logging;

namespace Blazix.Primitives;

// COL-EDGE-02 (TRN-EDGE-04) and COL-EDGE-03 (TRN-EDGE-07), in the port's words. Base UI's `warn` logs each message once per
// page, so the seen set is a scoped service (one per Server circuit, one per WebAssembly application), registered by
// `AddBlazixPrimitives()` as ButtonDevelopmentLog is; Accordion's panel (M3) reports through the same service.
/// <summary>Writes the development messages of collapsible panels, each distinct message once per session.</summary>
internal sealed class CollapsibleDevelopmentLog
{
    // COL-EDGE-02, TRN-EDGE-04.
    internal const string KeepMountedIgnoredMessage =
        "Blazix: The `KeepMounted` parameter set to `false` on `CollapsiblePanel` is ignored when `HiddenUntilFound` is enabled, "
        + "since the panel must remain mounted while closed.";

    // COL-EDGE-03, TRN-EDGE-07.
    internal const string MixedMotionMessage =
        "Blazix: CSS transitions and CSS animations both detected on a collapsible or accordion panel. "
        + "Only one of either animation type should be used.";

    private readonly HashSet<string> written = new(StringComparer.Ordinal);

    /// <summary>Writes a message, unless this session already has.</summary>
    /// <param name="logger">The logger of the part that found the problem.</param>
    /// <param name="message">One of the messages of this class.</param>
    public void Report(ILogger logger, string message)
    {
        if (DevelopmentWarnings.IsEnabled && written.Add(message))
        {
            DevelopmentWarnings.Write(logger, message);
        }
    }
}
