using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Iodine.Headless.Tests.Components;

/// <summary>
/// The platform flags at L2 (ADR 04 Amendments 2026-11-04, design point 4, settled by the orchestrator 2026-11-04;
/// MNU-A11Y-01): a scoped service <c>AddIodineHeadless()</c> registers, which reads the VoiceOver hint and macOS once per
/// circuit or runtime from the <c>interactions</c> module's <c>readPlatformFlags</c> export, and reads <see langword="null"/>
/// until then (the non-VoiceOver, non-macOS default of prerender and the first render).
/// </summary>
public sealed class PlatformFlagsTests : IDisposable
{
    private readonly BunitContext context = new();

    public PlatformFlagsTests()
    {
        context.Services.AddIodineHeadless();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "port: the platform flags read null until filled, then the module's values, read once per scope")]
    [Trait("Requirement", "MNU-A11Y-01")]
    public async Task The_platform_flags_read_null_until_filled_then_the_modules_values_read_once_per_scope()
    {
        BunitJSModuleInterop module = context.JSInterop.SetupModule(InteractionsTestContext.ModulePath);
        module.Setup<string>("readPlatformFlags").SetResult("""{"voiceOver":true,"mac":true}""");
        PlatformFlags flags = context.Services.GetRequiredService<PlatformFlags>();
        IJSRuntime jsRuntime = context.Services.GetRequiredService<IJSRuntime>();

        Assert.Null(flags.VoiceOver);
        Assert.Null(flags.Mac);

        await flags.EnsureAsync(jsRuntime);
        await flags.EnsureAsync(jsRuntime);

        Assert.True(flags.VoiceOver);
        Assert.True(flags.Mac);
        Assert.Single(module.Invocations["readPlatformFlags"]);
        Assert.Same(flags, context.Services.GetRequiredService<PlatformFlags>());
    }

    // NUM-STA-19; issue 277 design point 4 (a), granted by the orchestrator 2026-11-18.
    [Fact(DisplayName = "port: the iOS flag reads null until filled, then the module's value")]
    [Trait("Requirement", "NUM-STA-19")]
    public async Task The_iOS_flag_reads_null_until_filled_then_the_modules_value()
    {
        BunitJSModuleInterop module = context.JSInterop.SetupModule(InteractionsTestContext.ModulePath);
        module.Setup<string>("readPlatformFlags").SetResult("""{"voiceOver":false,"mac":false,"ios":true}""");
        PlatformFlags flags = context.Services.GetRequiredService<PlatformFlags>();

        Assert.Null(flags.Ios);

        await flags.EnsureAsync(context.Services.GetRequiredService<IJSRuntime>());

        Assert.True(flags.Ios);
    }
}
