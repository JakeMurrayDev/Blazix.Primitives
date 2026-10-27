using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components.Interop;

/// <summary>
/// Twin contract tests of the <c>scroll-lock</c> module (ADR 06 §1, L2; ADR 04 Amendments 2026-10-27, the scroll-lock
/// bullet): its one option as bUnit's JS interop mock records it, and the behavior the popup families compose, which
/// attaches to the reference element once the lock is enabled and sends <c>Enabled</c> only when it changes. The module
/// reports nothing and asks no decision point.
/// </summary>
public sealed class ScrollLockTwinContractTests
{
    private const string ModulePath = "./_content/Blazix.Primitives/js/scroll-lock.js";

    [Fact]
    [Trait("Requirement", "UTL-DOM-12")]
    public async Task Attach_imports_the_scroll_lock_module_and_sends_enabled_on_attach_and_update()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule(ModulePath);
        BunitJSModuleInterop handle = SetupHandle(module);
        await using ScrollLockTwin twin = new(context.JSInterop.JSRuntime);

        TwinHandle<ScrollLockOptions> attached = await twin.AttachAsync(new ElementReference("popup-1"), new ScrollLockOptions { Enabled = true });
        await attached.UpdateAsync(new ScrollLockOptions { Enabled = false });
        await attached.DisposeAsync();

        JSRuntimeInvocation attach = Assert.Single(module.Invocations["attach"]);
        Assert.Equal("""{"enabled":true}""", attach.Arguments[1]);
        Assert.IsType<DotNetObjectReference<ScrollLockTwin>>(attach.Arguments[2]);
        Assert.Equal("""{"enabled":false}""", Assert.Single(handle.Invocations["update"]).Arguments[0]);
        Assert.Single(handle.Invocations["dispose"]);
        Assert.Equal("run", twin.Decide("any", """{"kind":"Event","type":"base-ui"}"""));
    }

    [Fact]
    [Trait("Requirement", "UTL-DOM-12")]
    public async Task The_scroll_lock_behavior_attaches_once_enabled_sends_Enabled_only_on_change_and_disposes_the_handle()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule(ModulePath);
        BunitJSModuleInterop handle = SetupHandle(module);
        await using PopupScrollLockBehavior behavior = new(context.JSInterop.JSRuntime);
        ElementReference popup = new("popup-2");

        await behavior.ApplyAsync(popup, enabled: false);
        Assert.Empty(module.Invocations["attach"]);

        await behavior.ApplyAsync(popup, enabled: true);
        await behavior.ApplyAsync(popup, enabled: true);
        await behavior.ApplyAsync(popup, enabled: false);
        await behavior.ApplyAsync(null, enabled: false);

        JSRuntimeInvocation attach = Assert.Single(module.Invocations["attach"]);
        Assert.Equal(popup, attach.Arguments[0]);
        Assert.Equal("""{"enabled":true}""", attach.Arguments[1]);
        Assert.Equal(["""{"enabled":false}"""], handle.Invocations["update"].Select(invocation => invocation.Arguments[0] as string));

        await behavior.DisposeAsync();
        Assert.Single(handle.Invocations["dispose"]);
    }

    private static BunitJSModuleInterop SetupHandle(BunitJSModuleInterop module)
    {
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        return handle;
    }
}
