using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives;

/// <summary>
/// The debug switch behind the development warnings (ADR 05 §3; EVD-EDGE-01/02, UTL-EDGE-25), Base UI's
/// <c>process.env.NODE_ENV !== 'production'</c>. Off unless the app turns on the <see cref="SwitchName"/> runtime switch
/// (<c>AppContext.SetSwitch</c>, or <c>&lt;RuntimeHostConfigurationOption Include="Blazix.Primitives.DevelopmentWarnings" Value="true" /&gt;</c>
/// in a Debug build). Warnings are written to the logger the part was given, at <see cref="LogLevel.Error"/> as Base UI
/// writes them to <c>console.error</c>.
/// </summary>
internal static partial class DevelopmentWarnings
{
    internal const string SwitchName = "Blazix.Primitives.DevelopmentWarnings";

    /// <summary>Whether development warnings are written.</summary>
    [FeatureSwitchDefinition(SwitchName)]
    internal static bool IsEnabled => AppContext.TryGetSwitch(SwitchName, out bool enabled) && enabled;

    [LoggerMessage(EventId = 1, EventName = "DevelopmentWarning", Level = LogLevel.Error, Message = "{Message}")]
    internal static partial void Write(ILogger logger, string message);
}
