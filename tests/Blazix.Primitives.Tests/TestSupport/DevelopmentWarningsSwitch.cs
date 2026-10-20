using System.Runtime.CompilerServices;

namespace Blazix.Primitives.Tests.TestSupport;

/// <summary>Turns the library's development warnings on for the whole L1 run, as a Debug build of an app would.</summary>
internal static class DevelopmentWarningsSwitch
{
    [ModuleInitializer]
    internal static void TurnOn() => AppContext.SetSwitch("Blazix.Primitives.DevelopmentWarnings", true);
}
