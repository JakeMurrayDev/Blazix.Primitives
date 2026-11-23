using System.Runtime.CompilerServices;

namespace Iodine.Headless.Tests.Components;

/// <summary>
/// Turns the library's development warnings on for the whole L2 run, as a Debug build of an app would, so the button
/// twin's development check is requested and its messages reach the captured logger (UBT-EDGE-01..03).
/// </summary>
internal static class DevelopmentWarningsSwitch
{
    [ModuleInitializer]
    internal static void TurnOn() => AppContext.SetSwitch("Iodine.Headless.DevelopmentWarnings", true);
}
