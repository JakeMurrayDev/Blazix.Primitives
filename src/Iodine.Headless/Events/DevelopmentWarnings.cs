using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives;

// ADR 05 §3; EVD-EDGE-01/02, UTL-EDGE-25
/// <summary>
/// Provides the debug switch behind the library's development warnings (Base UI's <c>process.env.NODE_ENV !== 'production'</c>)
/// and the method that writes them.
/// </summary>
/// <remarks>
/// Warnings are off unless the app turns on the <see cref="SwitchName"/> runtime switch, with <see cref="AppContext.SetSwitch(string, bool)"/>
/// or with <c>&lt;RuntimeHostConfigurationOption Include="Blazix.Primitives.DevelopmentWarnings" Value="true" /&gt;</c> in a
/// Debug build. They are written to the logger the part was given, at <see cref="LogLevel.Error"/>, as Base UI writes them
/// to <c>console.error</c>.
/// </remarks>
internal static partial class DevelopmentWarnings
{
    /// <summary>
    /// Represents the name of the runtime switch that turns development warnings on.
    /// </summary>
    internal const string SwitchName = "Blazix.Primitives.DevelopmentWarnings";

    /// <summary>
    /// Gets a value indicating whether development warnings are written.
    /// </summary>
    /// <value><see langword="true"/> if the <see cref="SwitchName"/> switch is on; otherwise, <see langword="false"/>.</value>
    [FeatureSwitchDefinition(SwitchName)]
    internal static bool IsEnabled => AppContext.TryGetSwitch(SwitchName, out bool enabled) && enabled;

    /// <summary>
    /// Writes a development warning to <paramref name="logger"/> at <see cref="LogLevel.Error"/>.
    /// </summary>
    /// <param name="logger">The logger that receives the warning.</param>
    /// <param name="message">The warning's text.</param>
    [LoggerMessage(EventId = 1, EventName = "DevelopmentWarning", Level = LogLevel.Error, Message = "{Message}")]
    internal static partial void Write(ILogger logger, string message);
}
