using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: the missing-context titles of <c>MenuCheckboxItemIndicator.test.tsx</c> and <c>MenuRadioItemIndicator.test.tsx</c>
/// (MNU-STR-09, EDGE-03); their animation titles are L4 (MNU-DOM-17, ANI-13), with a port title here for the indicator's
/// transitions handle.
/// </summary>
public sealed class MenuItemIndicatorTests : BunitContext
{
    public MenuItemIndicatorTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazixPrimitives();
    }

    [Fact(DisplayName = "throws when rendered outside Menu.CheckboxItem")]
    [Trait("Requirement", "MNU-STR-09")]
    [Trait("Requirement", "MNU-EDGE-03")]
    public void Throws_when_rendered_outside_a_checkbox_item()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Render<MenuCheckboxItemIndicator>());

        Assert.Equal("Blazix: The `MenuCheckboxItem` context is missing. MenuCheckboxItem parts must be placed within `<MenuCheckboxItem>`.", error.Message);
    }

    [Fact(DisplayName = "throws when rendered outside Menu.RadioItem")]
    [Trait("Requirement", "MNU-STR-09")]
    [Trait("Requirement", "MNU-EDGE-03")]
    public void Throws_when_rendered_outside_a_radio_item()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Render<MenuRadioItemIndicator>());

        Assert.Equal("Blazix: The `MenuRadioItem` context is missing. MenuRadioItem parts must be placed within `<MenuRadioItem>`.", error.Message);
    }

    // The held runtime is a test double, not interop: the analyzer reads its members as JS interop calls.
#pragma warning disable BL0016

    // MNU-ANI-13's L4 title "should remove the indicator when the animation finishes" failed in CI run 42829719126 (Server)
    // and 42772047527 (WebAssembly): an uncheck rendered while the indicator's transitions handle was attaching, and the
    // module never received the close, so the indicator kept neither `data-ending-style` nor `hidden`.
    [Fact(DisplayName = "port: an uncheck while the indicator's transitions handle attaches reaches the module once it attaches")]
    [Trait("Requirement", "MNU-ANI-13")]
    public async Task An_uncheck_while_the_handle_attaches_reaches_the_module()
    {
        BunitJSModuleInterop transitions = JSInterop.SetupModule("./_content/Blazix.Primitives/js/transitions.js");
        BunitJSModuleInterop handle = transitions.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        HeldTransitionsAttachRuntime runtime = new(JSInterop.JSRuntime);
        Services.AddSingleton<IJSRuntime>(runtime);
        IRenderedComponent<MenuCheckableHost> cut = Render<MenuCheckableHost>(p => p.Add(x => x.Checked, true).Add(x => x.IndicatorKeepMounted, true));
        await runtime.WaitForIndicatorAttachAsync();

        cut.Render(p => p.Add(x => x.Checked, false).Add(x => x.IndicatorKeepMounted, true));
        runtime.Release();

        // No render follows the attach, and bUnit's waits check on renders: the record is polled on the renderer.
        bool closed = false;
        for (int poll = 0; poll < 200 && !closed; poll++)
        {
            closed = await cut.InvokeAsync(() => handle.Invocations["update"].Any(invocation =>
                invocation.Arguments[0] is string json && json.Contains("\"openSelector\":\"[data-checked]\"", StringComparison.Ordinal)
                && json.Contains("\"open\":false", StringComparison.Ordinal)));
            if (!closed)
            {
                await Task.Delay(10, Xunit.TestContext.Current.CancellationToken);
            }
        }

        Assert.True(closed);
    }

    // Holds the transitions module's `attach` until Release, as a slow module load or a Server round trip holds it, and
    // signals when the indicator's attach (its options carry the `[data-checked]` open selector) is held.
    private sealed class HeldTransitionsAttachRuntime(IJSRuntime inner) : IJSRuntime
    {
        private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource indicatorHeld = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release() => released.TrySetResult();

        public Task WaitForIndicatorAttachAsync() => indicatorHeld.Task.WaitAsync(TimeSpan.FromSeconds(5));

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            ValueTask<TValue> invocation = inner.InvokeAsync<TValue>(identifier, cancellationToken, args);
            return identifier == "import" && args is [string path] && path.EndsWith("/transitions.js", StringComparison.Ordinal)
                ? new ValueTask<TValue>(WrapAsync(invocation))
                : invocation;
        }

        private async Task<TValue> WrapAsync<TValue>(ValueTask<TValue> import) => (TValue)(object)new HeldModule(this, (IJSObjectReference)(await import)!);

        private sealed class HeldModule(HeldTransitionsAttachRuntime runtime, IJSObjectReference inner) : IJSObjectReference
        {
            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
                identifier == "attach" ? new ValueTask<TValue>(HoldAsync<TValue>(identifier, cancellationToken, args)) : inner.InvokeAsync<TValue>(identifier, cancellationToken, args);

            public ValueTask DisposeAsync() => inner.DisposeAsync();

            private async Task<TValue> HoldAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            {
                if (args is [_, string json, ..] && json.Contains("\"openSelector\":\"[data-checked]\"", StringComparison.Ordinal))
                {
                    runtime.indicatorHeld.TrySetResult();
                }

                await runtime.released.Task;
                return await inner.InvokeAsync<TValue>(identifier, cancellationToken, args);
            }
        }
    }

#pragma warning restore BL0016
}
