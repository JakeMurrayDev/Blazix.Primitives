using Microsoft.Extensions.Logging;

namespace Blazix.Primitives;

// OTP-EDGE-02..05 (Base UI's `warn`, UTL-EDGE-11/12: each message once per page). The seen set is a scoped service (one per
// Server circuit, one per WebAssembly application), registered by `AddBlazixPrimitives()` like ButtonDevelopmentLog, never a
// static, which on Server would be one per process (chosen by the user 2026-10-26, M3 plan §Settled). M6 track 7, issue
// 278: granted by the orchestrator 2026-11-19 (option (a)).
/// <summary>Writes the development warnings of one-time code fields, each text once per session.</summary>
internal sealed class OtpFieldDevelopmentLog
{
    private readonly HashSet<string> written = new(StringComparer.Ordinal);

    /// <summary>Writes a warning, unless this session already wrote the same text or the development warnings are off.</summary>
    /// <param name="logger">The logger of the part that warns.</param>
    /// <param name="message">The warning's text.</param>
    public void Report(ILogger logger, string message)
    {
        if (DevelopmentWarnings.IsEnabled && written.Add(message))
        {
            DevelopmentWarnings.Write(logger, message);
        }
    }
}
