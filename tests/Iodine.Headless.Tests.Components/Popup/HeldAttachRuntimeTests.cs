using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

// A test double, not interop: the analyzer reads its members as JS interop calls.
#pragma warning disable BL0016

// Family tests use the project's root namespace (tests/Iodine.Headless.Tests.Components/CONTEXT.md).
namespace Iodine.Headless.Tests.Components;

/// <summary>
/// The test double <see cref="HeldAttachRuntime"/> itself (issue 389): bUnit records interop calls in lists a test reads on
/// the renderer, so the double runs a held module's calls there. A twin handle sends its updates after a
/// <c>ConfigureAwait(false)</c> on the attach, which resumes off the renderer, so two handles' updates could be recorded at
/// once and one record lost (<c>SelectFieldLabelAttachTests</c>' CI sightings).
/// </summary>
public sealed class HeldAttachRuntimeTests : BunitContext
{
    [Fact(DisplayName = "port: the handle a released attach returns runs its calls on the renderer, where bUnit records them")]
    [Trait("Coverage", "issue-389")]
    public async Task The_handle_a_released_attach_returns_runs_its_calls_on_the_renderer()
    {
        RecordingRuntime inner = new(Renderer.Dispatcher);
        HeldAttachRuntime runtime = new(inner, "probe.js", dispatcher: () => Renderer.Dispatcher);
        IJSObjectReference module = await runtime.InvokeAsync<IJSObjectReference>("import", "./_content/Iodine.Headless/js/probe.js");
        ValueTask<IJSObjectReference> attaching = module.InvokeAsync<IJSObjectReference>("attach", "element", "{}");

        runtime.Release();
        IJSObjectReference handle = await attaching;
        await Task.Run(async () => await handle.InvokeVoidAsync("update", "{}"), Xunit.TestContext.Current.CancellationToken);

        Assert.Equal([true], inner.UpdatesOnRenderer);
    }

    // The inner runtime: its module's attach answers a handle that records, for each update, whether it ran on the renderer.
    private sealed class RecordingRuntime(Dispatcher dispatcher) : IJSRuntime
    {
        public List<bool> UpdatesOnRenderer { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            new((TValue)(object)new Reference(this, dispatcher));

        private sealed class Reference(RecordingRuntime runtime, Dispatcher dispatcher) : IJSObjectReference
        {
            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            {
                if (identifier == "attach")
                {
                    return new((TValue)(object)new Reference(runtime, dispatcher));
                }

                lock (runtime.UpdatesOnRenderer)
                {
                    runtime.UpdatesOnRenderer.Add(dispatcher.CheckAccess());
                }

                return new(default(TValue)!);
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}

#pragma warning restore BL0016
