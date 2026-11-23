using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: a bar menu's touch guard (MNU-STA-05's bar clause, MBR-PTR-08) runs under the <see cref="TimeProvider"/> the
/// application registers, as every C# timer does (ADR 06 §2 "Clocks"), so the fixture host's session clock gates it as
/// Base UI's title gates the guard with fake timers (Menubar.test.tsx:51-89). The store's own window is MenuStoreTests'.
/// </summary>
public sealed class MenubarTouchGuardClockTests : BunitContext
{
    private readonly FakeTimeProvider time = new();

    public MenubarTouchGuardClockTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazixPrimitives();
        Services.AddSingleton<TimeProvider>(time);
    }

    private static OpenChangeEventDetails TouchClick() =>
        new(EventReason.TriggerPress, new EventSnapshot { Kind = EventKind.PointerEvent, Type = "click", Detail = 1, PointerType = "touch" });

    [Fact(DisplayName = "port: a bar menu's touch guard runs under the registered TimeProvider")]
    [Trait("Requirement", "MNU-STA-05")]
    [Trait("Requirement", "MBR-PTR-08")]
    public async Task The_touch_guard_runs_under_the_registered_clock()
    {
        IRenderedComponent<MenubarHost> cut = Render<MenubarHost>();
        MenuStore<NoPayload> store = cut.FindComponent<MenuRoot<NoPayload>>().Instance.Store;
        await cut.InvokeAsync(() => store.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.TriggerFocus, new EventSnapshot { Kind = EventKind.FocusEvent, Type = "focus" })));
        Assert.True(store.IsOpen);

        time.Advance(TimeSpan.FromMilliseconds(299));
        await cut.InvokeAsync(() => store.RequestOpenAsync(false, TouchClick()));
        Assert.True(store.IsOpen);

        time.Advance(TimeSpan.FromMilliseconds(11));
        await cut.InvokeAsync(() => store.RequestOpenAsync(false, TouchClick()));
        Assert.False(store.IsOpen);
    }
}
