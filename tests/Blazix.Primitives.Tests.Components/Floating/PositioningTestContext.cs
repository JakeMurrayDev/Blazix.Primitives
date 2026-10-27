using System.Text.Json;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// A bUnit context with the <c>positioning</c> module mocked: the attach, update and dispose calls are recorded and the
/// twin of each attach is reachable, so a test drives its report as the module would.
/// </summary>
internal sealed class PositioningTestContext : IDisposable
{
    public const string ModulePath = "./_content/Blazix.Primitives/js/positioning.js";

    public PositioningTestContext()
    {
        Context.Services.AddBlazixPrimitives();
        Module = Context.JSInterop.SetupModule(ModulePath);
        Handle = Module.SetupModule("attach", _ => true);
        Handle.SetupVoid("update", _ => true).SetVoidResult();
        Handle.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    public BunitContext Context { get; } = new();

    public BunitJSModuleInterop Module { get; }

    public BunitJSModuleInterop Handle { get; }

    public IReadOnlyList<JSRuntimeInvocation> Attaches => Module.Invocations["attach"];

    public IReadOnlyList<string> UpdateOptions => [.. Handle.Invocations["update"].Select(invocation => (string)invocation.Arguments[0]!)];

    public int Disposes => Handle.Invocations["dispose"].Count;

    /// <summary>Gets the options JSON of the last attach.</summary>
    public string AttachOptions => (string)Attaches[^1].Arguments[1]!;

    /// <summary>Gets the twin of the last attach, which the module would call back.</summary>
    public PositioningTwin Twin => ((DotNetObjectReference<PositioningTwin>)Attaches[^1].Arguments[2]!).Value;

    public static JsonElement Parse(string optionsJson) => JsonDocument.Parse(optionsJson).RootElement;

    public void Dispose() => Context.Dispose();
}
