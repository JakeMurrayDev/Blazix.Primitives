using Microsoft.JSInterop;

// A test double, not interop: the analyzer reads its members as JS interop calls.
#pragma warning disable BL0016

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// An <see cref="IJSRuntime"/> over bUnit's that holds one module's <c>attach</c> (or, with <paramref name="holdImport"/>, its
/// import) until <see cref="Release"/>, as a Server round trip holds it (the portal's move, issue 149; the transitions
/// module's import, issue 147), and records every module the parts import; every other call goes to bUnit's runtime and its
/// modules.
/// </summary>
internal sealed class HeldAttachRuntime(IJSRuntime inner, string module, bool holdImport = false) : IJSRuntime
{
    private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets the path of every module imported, in order.</summary>
    public List<string> Imports { get; } = [];

    /// <summary>Lets the held attaches go to bUnit's module.</summary>
    public void Release() => released.TrySetResult();

    /// <inheritdoc />
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    /// <inheritdoc />
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        if (identifier == "import" && args is [string path])
        {
            Imports.Add(path);
        }

        ValueTask<TValue> invocation = inner.InvokeAsync<TValue>(identifier, cancellationToken, args);
        return identifier == "import" && args is [string imported] && imported.EndsWith("/" + module, StringComparison.Ordinal)
            ? new ValueTask<TValue>(holdImport ? HoldImportAsync(invocation) : WrapAsync(invocation))
            : invocation;
    }

    private async Task<TValue> HoldImportAsync<TValue>(ValueTask<TValue> import)
    {
        await released.Task;
        return await import;
    }

    private async Task<TValue> WrapAsync<TValue>(ValueTask<TValue> import) => (TValue)(object)new HeldModule(this, (IJSObjectReference)(await import)!);

    private sealed class HeldModule(HeldAttachRuntime runtime, IJSObjectReference inner) : IJSObjectReference
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            identifier == "attach" ? new ValueTask<TValue>(HoldAsync<TValue>(identifier, cancellationToken, args)) : inner.InvokeAsync<TValue>(identifier, cancellationToken, args);

        public ValueTask DisposeAsync() => inner.DisposeAsync();

        private async Task<TValue> HoldAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            await runtime.released.Task;
            return await inner.InvokeAsync<TValue>(identifier, cancellationToken, args);
        }
    }
}

#pragma warning restore BL0016
