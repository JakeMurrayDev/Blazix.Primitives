using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components.Interop;

/// <summary>
/// Twin contract tests of the <c>portal</c> module (ADR 06 §1, L2; ADR 04 Amendments 2026-10-27, decision 5): its options
/// and references as bUnit's JS interop mock records them, and the behavior the portals compose, which attaches to the node
/// after the render that created it, sends the container as a reference (an element, a node a script holds, or the
/// enclosing portal's node) or a selector, updates only on change, detaches when no node renders, and completes the move.
/// The module reports nothing and asks no decision point.
/// </summary>
public sealed class PortalTwinContractTests
{
    private const string ModulePath = "./_content/Blazix.Primitives/js/portal.js";

    [Fact]
    [Trait("Requirement", "FLT-API-13")]
    public async Task Attach_imports_the_portal_module_and_sends_the_container_reference_the_selector_lite_and_the_style()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule(ModulePath);
        BunitJSModuleInterop handle = SetupHandle(module);
        await using PortalTwin twin = new(context.JSInterop.JSRuntime);
        ElementReference container = new("container-1");
        TwinReferences references = new();

        TwinHandle<PortalOptions> attached = await twin.AttachAsync(
            new ElementReference("portal-1"),
            new PortalOptions { ContainerRef = references.Add(container), Style = "color: red" },
            references);
        await attached.UpdateAsync(new PortalOptions { ContainerSelector = "#root", Lite = true });
        await attached.DisposeAsync();

        JSRuntimeInvocation attach = Assert.Single(module.Invocations["attach"]);
        Assert.Equal("""{"containerRef":0,"lite":false,"style":"color: red"}""", attach.Arguments[1]);
        Assert.IsType<DotNetObjectReference<PortalTwin>>(attach.Arguments[2]);
        Assert.Equal(container, Assert.Single(Assert.IsType<object[]>(attach.Arguments[3])));
        JSRuntimeInvocation update = Assert.Single(handle.Invocations["update"]);
        Assert.Equal("""{"containerSelector":"#root","lite":true}""", update.Arguments[0]);
        Assert.Single(update.Arguments);
        Assert.Single(handle.Invocations["dispose"]);
        Assert.Equal("run", twin.Decide("any", """{"kind":"Event","type":"base-ui"}"""));
    }

    [Fact]
    [Trait("Requirement", "FLT-API-13")]
    [Trait("Requirement", "FLT-DOM-11")]
    public async Task The_portal_behavior_attaches_after_the_render_that_created_the_node_follows_its_container_and_detaches_without_a_node()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule(ModulePath);
        BunitJSModuleInterop handle = SetupHandle(module);
        await using PopupPortalBehavior behavior = new(context.JSInterop.JSRuntime, () => { });
        ElementReference node = new("portal-2");
        ElementReference element = new("container-2");
        ScriptHeldReference scriptHeld = new();
        Task firstMove = behavior.Context.Moved;

        await behavior.OnAfterRenderAsync(node, container: null);
        Assert.True(firstMove.IsCompletedSuccessfully);
        Assert.Equal(node, behavior.Context.Node);
        await behavior.OnAfterRenderAsync(node, container: null);
        await behavior.OnAfterRenderAsync(node, element);
        await behavior.OnAfterRenderAsync(node, element);
        await behavior.OnAfterRenderAsync(node, new PortalContainerValue(scriptHeld));
        await behavior.OnAfterRenderAsync(node, "#root");

        JSRuntimeInvocation attach = Assert.Single(module.Invocations["attach"]);
        Assert.Equal(node, attach.Arguments[0]);
        Assert.Equal("""{"lite":false}""", attach.Arguments[1]);
        Assert.Equal(5, attach.Arguments.Count);
        Assert.Null(attach.Arguments[3]);
        Assert.Equal(new string?[] { node.Id }, Assert.IsType<string?[]>(attach.Arguments[4]));
        IReadOnlyList<JSRuntimeInvocation> updates = handle.Invocations["update"];
        Assert.Equal(3, updates.Count);
        Assert.Equal("""{"containerRef":0,"lite":false}""", updates[0].Arguments[0]);
        Assert.Equal(element, Assert.Single(Assert.IsType<object[]>(updates[0].Arguments[1])));
        Assert.Same(scriptHeld, Assert.Single(Assert.IsType<object[]>(updates[1].Arguments[1])));
        Assert.Equal("""{"containerSelector":"#root","lite":false}""", updates[2].Arguments[0]);
        Assert.Single(updates[2].Arguments);

        // FLT-API-13: a pending container renders no node, so the handle goes and the next node waits for a new move.
        await behavior.OnAfterRenderAsync(null, PortalContainerValue.Pending);
        Assert.Single(handle.Invocations["dispose"]);
        Assert.Null(behavior.Context.Node);
        Task secondMove = behavior.Context.Moved;
        Assert.False(secondMove.IsCompleted);

        await behavior.OnAfterRenderAsync(node, container: null);
        Assert.Equal(2, module.Invocations["attach"].Count);
        Assert.True(secondMove.IsCompletedSuccessfully);
    }

    [Fact]
    [Trait("Requirement", "FLT-API-13")]
    public async Task A_portal_without_a_container_inside_another_portal_sends_the_enclosing_portal_node_as_its_container()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule(ModulePath);
        SetupHandle(module);
        await using PopupPortalBehavior outer = new(context.JSInterop.JSRuntime, () => { });
        await using PopupPortalBehavior inner = new(context.JSInterop.JSRuntime, () => { }) { Enclosing = outer.Context, Lite = true };
        ElementReference outerNode = new("portal-outer");

        await outer.OnAfterRenderAsync(outerNode, container: null);
        await inner.OnAfterRenderAsync(new ElementReference("portal-inner"), container: null);

        JSRuntimeInvocation attach = module.Invocations["attach"][1];
        Assert.Equal("""{"containerRef":0,"lite":true}""", attach.Arguments[1]);
        Assert.Equal(outerNode, Assert.Single(Assert.IsType<object[]>(attach.Arguments[3])));
    }

    // ADR 16 rule 7: a render whose container, enclosing node, lite flag and style are unchanged builds no options, boxes
    // no reference and sends nothing; the options are built, and the container boxed once, per attach or update.
    [Fact]
    [Trait("Requirement", "ADR16-7")]
    public async Task A_render_with_unchanged_inputs_builds_no_options_and_allocates_nothing()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule(ModulePath);
        BunitJSModuleInterop handle = SetupHandle(module);
        await using PopupPortalBehavior behavior = new(context.JSInterop.JSRuntime, () => { });
        ElementReference node = new("portal-4");
        PortalContainerValue container = new ElementReference("container-4");
        await behavior.OnAfterRenderAsync(node, container);
        await behavior.OnAfterRenderAsync(node, container);

        long before = GC.GetAllocatedBytesForCurrentThread();
        Task render = behavior.OnAfterRenderAsync(node, container);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        await render;

        Assert.True(render.IsCompletedSuccessfully);
        Assert.Equal(0, allocated);
        Assert.Single(module.Invocations["attach"]);
        Assert.Empty(handle.Invocations["update"]);
    }

    // Design point 3 (ADR 04 Amendments 2026-10-27): the move completes only once the module ran. A render during the
    // pending first attach reaches TwinBase.AttachAsync, which hands back the pending handle at once.
    // Issue 141: the two renders run on the renderer's dispatcher, as a part's OnAfterRenderAsync always does. Without
    // that synchronization context the two continuations of the pending attach ran on the thread pool in either order, and
    // the older render's, finding its container already superseded, sent a second update (14 of 300 runs locally).
    [Fact]
    [Trait("Requirement", "FLT-API-13")]
    public async Task A_render_during_the_pending_first_attach_neither_attaches_again_nor_completes_the_move_early()
    {
        using BunitContext context = new();
        PendingModuleRuntime runtime = new();
        PopupPortalBehavior behavior = new(runtime, () => { });
        ElementReference node = new("portal-3");
        try
        {
            await context.Renderer.Dispatcher.InvokeAsync(async () =>
            {
                Task first = behavior.OnAfterRenderAsync(node, container: null);
                Task second = behavior.OnAfterRenderAsync(node, container: "#root");

                Assert.False(behavior.Context.Moved.IsCompleted);
                Assert.False(second.IsCompleted);

                runtime.Module.SetResult(new RecordingModule());
                await Task.WhenAll(first, second);
            });

            Assert.True(behavior.Context.Moved.IsCompletedSuccessfully);
            RecordingModule module = (RecordingModule)await runtime.Module.Task;
            Assert.Equal(["attach", "update"], module.Calls);
        }
        finally
        {
            // A failed assertion must not leave the dispose waiting on the held import.
            runtime.Module.TrySetResult(new RecordingModule());
            await behavior.DisposeAsync();
        }
    }

    private static BunitJSModuleInterop SetupHandle(BunitJSModuleInterop module)
    {
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        return handle;
    }

    // A runtime whose module import completes when the test says, so an attach stays pending.
    private sealed class PendingModuleRuntime : IJSRuntime
    {
        public TaskCompletionSource<IJSObjectReference> Module { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            new((Task<TValue>)(object)Module.Task);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            new((Task<TValue>)(object)Module.Task);
    }

    // The imported module and the handle in one: records attach and update, the calls the behavior makes.
    private sealed class RecordingModule : IJSObjectReference
    {
        public List<string> Calls { get; } = [];

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Calls.Add(identifier);
            return new(this is TValue reference ? reference : default!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Calls.Add(identifier);
            return new(this is TValue reference ? reference : default!);
        }
    }
}
