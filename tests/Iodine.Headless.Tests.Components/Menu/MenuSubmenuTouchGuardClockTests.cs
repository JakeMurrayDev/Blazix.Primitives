using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: a submenu's store runs its touch guard (MNU-STA-05) under the <see cref="TimeProvider"/> the application registers,
/// as <c>MenuRoot</c>'s does (ADR 06 §2 "Clocks"; M6 track 11, issue 281, design point 4). No interaction opens a
/// submenu on <c>trigger-focus</c>, so the title drives the part's store: a touch click 310 ms of the registered clock
/// after a focus open closes it, where the system clock would still be inside the 300 ms guard.
/// </summary>
public sealed class MenuSubmenuTouchGuardClockTests : BunitContext
{
    private readonly FakeTimeProvider time = new();

    public MenuSubmenuTouchGuardClockTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazixPrimitives();
        Services.AddSingleton<TimeProvider>(time);
    }

    internal static OpenChangeEventDetails FocusOpen() =>
        new(EventReason.TriggerFocus, new EventSnapshot { Kind = EventKind.FocusEvent, Type = "focus" });

    internal static OpenChangeEventDetails TouchClick() =>
        new(EventReason.TriggerPress, new EventSnapshot { Kind = EventKind.PointerEvent, Type = "click", Detail = 1, PointerType = "touch" });

    internal static OpenChangeEventDetails MouseClick() =>
        new(EventReason.TriggerPress, new EventSnapshot { Kind = EventKind.PointerEvent, Type = "click", Detail = 1, PointerType = "mouse" });

    [Fact(DisplayName = "port: a submenu's touch guard runs under the registered TimeProvider")]
    [Trait("Requirement", "MNU-STA-05")]
    public async Task The_submenu_touch_guard_runs_under_the_registered_clock()
    {
        IRenderedComponent<MenuControlledParentHost> cut = Render<MenuControlledParentHost>();
        MenuStore<NoPayload> store = cut.FindComponent<MenuSubmenuRoot>().Instance.Store;
        await cut.InvokeAsync(() => store.RequestOpenAsync(false, MouseClick()));
        Assert.False(store.IsOpen);

        await cut.InvokeAsync(() => store.RequestOpenAsync(true, FocusOpen()));
        Assert.True(store.IsOpen);

        time.Advance(TimeSpan.FromMilliseconds(310));
        await cut.InvokeAsync(() => store.RequestOpenAsync(false, TouchClick()));
        Assert.False(store.IsOpen);
    }
}
