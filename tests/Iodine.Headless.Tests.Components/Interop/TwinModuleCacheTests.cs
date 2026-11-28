using Iodine.Headless.Interop;
using Microsoft.JSInterop;

namespace Iodine.Headless.Tests.Components.Interop;

// The runtime here is a test double, not interop: the analyzer reads its members as JS interop calls.
#pragma warning disable BL0016

/// <summary>
/// Issue 179 (design point 10-179a (a), chosen by the user 2026-11-28): a module is imported once per runtime (one circuit's
/// on Server, the page's on WebAssembly), not once per twin; the import belongs to the runtime, so disposing a twin leaves
/// it for the next twin; a failed import is not kept.
/// </summary>
public sealed class TwinModuleCacheTests
{
    [Fact(DisplayName = "port: two twins of one module on one runtime import it once, and a second runtime imports it once more")]
    [Trait("Requirement", "ADR04-import-per-runtime")]
    public async Task Twins_of_one_module_import_it_once_per_runtime()
    {
        RecordingRuntime first = new();
        RecordingRuntime second = new();
        await using TestTwin a = new(first);
        await using TestTwin b = new(first);
        await using TestTwin c = new(second);

        await a.AttachAsync("base-ui-1", new TestTwinOptions());
        await b.AttachAsync("base-ui-2", new TestTwinOptions());
        await c.AttachAsync("base-ui-1", new TestTwinOptions());

        Assert.Equal(["import", "attach", "attach"], first.Calls);
        Assert.Equal(["import", "attach"], second.Calls);
    }

    [Fact(DisplayName = "port: disposing a twin keeps the runtime's import, so the next twin of the module attaches without importing")]
    [Trait("Requirement", "ADR04-import-per-runtime")]
    public async Task Disposing_a_twin_keeps_the_runtime_import()
    {
        RecordingRuntime runtime = new();
        TestTwin a = new(runtime);
        await a.AttachAsync("base-ui-1", new TestTwinOptions());
        await a.DisposeAsync();

        await using TestTwin b = new(runtime);
        await b.PrepareAsync();
        await b.AttachAsync("base-ui-2", new TestTwinOptions());

        Assert.Equal(["import", "attach", "dispose", "attach"], runtime.Calls);
        Assert.Equal(0, runtime.ModuleDisposals);
    }

    [Fact(DisplayName = "port: a failed import fails its twin's attach, and the runtime's next twin imports again, once for the twins after it")]
    [Trait("Requirement", "ADR04-import-per-runtime")]
    public async Task A_failed_import_is_not_kept()
    {
        RecordingRuntime runtime = new() { FailedImports = 1 };
        await using TestTwin a = new(runtime);
        await using TestTwin b = new(runtime);
        await using TestTwin c = new(runtime);

        await Assert.ThrowsAsync<JSException>(async () => await a.AttachAsync("base-ui-1", new TestTwinOptions()));
        await b.AttachAsync("base-ui-2", new TestTwinOptions());
        await c.AttachAsync("base-ui-3", new TestTwinOptions());

        Assert.Equal(["import", "import", "attach", "attach"], runtime.Calls);
    }

    // Imports and attaches complete at once; the module counts its disposals and its handles record `dispose`. The first
    // `FailedImports` imports fail.
    private sealed class RecordingRuntime : IJSRuntime, IJSObjectReference
    {
        private readonly ModuleHandle handle;

        public RecordingRuntime()
        {
            handle = new ModuleHandle(this);
        }

        public List<string> Calls { get; } = [];

        public int FailedImports { get; set; }

        public int ModuleDisposals { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Calls.Add(identifier);
            if (identifier == "import" && FailedImports > 0)
            {
                FailedImports--;
                return ValueTask.FromException<TValue>(new JSException("Iodine: the module failed to load."));
            }

            return identifier switch
            {
                "import" => ValueTask.FromResult((TValue)(object)this),
                "attach" => ValueTask.FromResult((TValue)(object)handle),
                _ => ValueTask.FromResult(default(TValue)!),
            };
        }

        public ValueTask DisposeAsync()
        {
            ModuleDisposals++;
            return ValueTask.CompletedTask;
        }

        private sealed class ModuleHandle(RecordingRuntime runtime) : IJSObjectReference
        {
            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            {
                runtime.Calls.Add(identifier);
                return ValueTask.FromResult(default(TValue)!);
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}

#pragma warning restore BL0016
