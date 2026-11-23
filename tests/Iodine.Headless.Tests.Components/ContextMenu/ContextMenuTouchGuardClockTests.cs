using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

// Family tests use the project's root namespace (tests/Iodine.Headless.Tests.Components/CONTEXT.md).
namespace Iodine.Headless.Tests.Components;

/// <summary>
/// L2: a context menu's root and submenu stores run their touch guard (MNU-STA-05) under the <see cref="TimeProvider"/>
/// the application registers, as <c>MenuRoot</c>'s does (ADR 06 §2 "Clocks"; M6 track 11, issue 281, design point 4). No
/// interaction opens them on <c>trigger-focus</c>, so each title drives the part's store: a touch click 310 ms of the
/// registered clock after a focus open closes it, where the system clock would still be inside the 300 ms guard.
/// </summary>
public sealed class ContextMenuTouchGuardClockTests : BunitContext
{
    private readonly FakeTimeProvider time = new();

    public ContextMenuTouchGuardClockTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddIodineHeadless();
        Services.AddSingleton<TimeProvider>(time);
    }

    [Fact(DisplayName = "port: a context menu's touch guard runs under the registered TimeProvider")]
    [Trait("Requirement", "MNU-STA-05")]
    public async Task The_root_touch_guard_runs_under_the_registered_clock()
    {
        IRenderedComponent<ContextMenuHost> cut = Render<ContextMenuHost>();
        MenuStore<NoPayload> store = cut.FindComponent<ContextMenuRoot>().Instance.Store;

        await cut.InvokeAsync(() => store.RequestOpenAsync(true, MenuSubmenuTouchGuardClockTests.FocusOpen()));
        Assert.True(store.IsOpen);

        time.Advance(TimeSpan.FromMilliseconds(310));
        await cut.InvokeAsync(() => store.RequestOpenAsync(false, MenuSubmenuTouchGuardClockTests.TouchClick()));
        Assert.False(store.IsOpen);
    }

    [Fact(DisplayName = "port: a context menu submenu's touch guard runs under the registered TimeProvider")]
    [Trait("Requirement", "MNU-STA-05")]
    public async Task The_submenu_touch_guard_runs_under_the_registered_clock()
    {
        IRenderedComponent<ContextMenuHost> cut = Render<ContextMenuHost>(parameters => parameters.Add(host => host.DefaultOpen, true));
        MenuStore<NoPayload> store = cut.FindComponent<ContextMenuSubmenuRoot>().Instance.Store;

        await cut.InvokeAsync(() => store.RequestOpenAsync(true, MenuSubmenuTouchGuardClockTests.FocusOpen()));
        Assert.True(store.IsOpen);

        time.Advance(TimeSpan.FromMilliseconds(310));
        await cut.InvokeAsync(() => store.RequestOpenAsync(false, MenuSubmenuTouchGuardClockTests.TouchClick()));
        Assert.False(store.IsOpen);
    }
}
