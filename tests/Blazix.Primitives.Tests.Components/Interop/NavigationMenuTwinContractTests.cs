using System.Text.Json;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components.Interop;

/// <summary>
/// Twin contract tests of the <c>navigation-menu</c> module (ADR 06 §1, L2; ADR 04 Amendments 2026-11-03 (4)): bUnit's JS
/// interop mock records every field C# sends on <c>attach</c> and <c>update</c>, the Root element crossing as the first
/// reference, and the tests drive <see cref="NavigationMenuTwin"/>'s <c>[JSInvokable]</c> callbacks.
/// </summary>
public sealed class NavigationMenuTwinContractTests
{
    private const string ModulePath = "./_content/Blazix.Primitives/js/navigation-menu.js";

    private const string MouseEnterJson = """
        {"kind":"MouseEvent","type":"mouseenter","key":null,"code":null,"altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":null,"detail":0,"buttons":0,
         "target":{"id":"base-ui-3","tagName":"BUTTON"},"defaultPrevented":false,"timeStamp":7.25}
        """;

    [Fact]
    [Trait("Verifies", "ADR06-3")]
    [Trait("Requirement", "NAV-STA-08")]
    public async Task Attach_imports_the_navigation_menu_module_and_sends_every_field_and_the_root_reference()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule(ModulePath);
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        await using NavigationMenuTwin twin = new(context.JSInterop.JSRuntime);
        ElementReference root = new("root-ref");
        TwinReferences references = new();
        references.Add(root);

        TwinHandle<NavigationMenuOptions> attached = await twin.AttachAsync(new ElementReference("trigger-ref"), new NavigationMenuOptions { Part = NavigationMenuTwin.TriggerPart, RootRef = 0 }, references);
        await attached.UpdateAsync(new NavigationMenuOptions
        {
            Part = NavigationMenuTwin.RootPart,
            Orientation = "vertical",
            Rtl = true,
            Nested = true,
            Mounted = true,
            CloseReason = "escape-key",
            SessionToken = "base-ui-9",
            Answered = 2,
            HasContent = false,
            HasPositioner = true,
        });
        await attached.DisposeAsync();

        JSRuntimeInvocation attach = Assert.Single(module.Invocations["attach"]);
        Assert.Equal("""{"part":"trigger","rootRef":0}""", attach.Arguments[1]);
        Assert.IsType<DotNetObjectReference<NavigationMenuTwin>>(attach.Arguments[2]);
        object[] sent = Assert.IsType<object[]>(attach.Arguments[3]);
        Assert.Equal(root, Assert.Single(sent));

        JSRuntimeInvocation update = Assert.Single(handle.Invocations["update"]);
        Assert.Equal(
            """{"part":"root","orientation":"vertical","rtl":true,"nested":true,"mounted":true,"closeReason":"escape-key","sessionToken":"base-ui-9","answered":2,"hasContent":false,"hasPositioner":true}""",
            update.Arguments[0]);
        Assert.Single(handle.Invocations["dispose"]);
    }

    [Fact]
    [Trait("Requirement", "NAV-PTR-18")]
    [Trait("Requirement", "NAV-STA-06")]
    public async Task The_activation_report_carries_the_reason_the_direction_and_the_event_and_arrives_on_either_call()
    {
        await using NavigationMenuTwin twin = new(new NoRuntime());
        List<NavigationMenuActivation> activations = [];
        twin.Activated += activations.Add;

        twin.OnActivate($$"""{"reason":"trigger-hover","direction":"right","event":{{MouseEnterJson}}}""");
        twin.OnActivate("""{"reason":null,"direction":"left","event":null}""");

        Assert.Equal(2, activations.Count);
        Assert.Equal("trigger-hover", activations[0].Reason);
        Assert.Equal("right", activations[0].Direction);
        Assert.Equal("mouseenter", activations[0].Event!.Type);
        Assert.Equal(7.25, activations[0].Event!.TimeStamp);
        Assert.Null(activations[1].Reason);
        Assert.Equal("left", activations[1].Direction);
    }

    [Fact]
    [Trait("Requirement", "NAV-FOC-10")]
    [Trait("Requirement", "NAV-FOC-07")]
    [Trait("Requirement", "NAV-ANI-14")]
    [Trait("Requirement", "NAV-ANI-02")]
    public async Task Each_report_raises_its_event_once_with_its_payload()
    {
        await using NavigationMenuTwin twin = new(new NoRuntime());
        List<NavigationMenuFocusOut> focusOuts = [];
        List<bool> inert = [];
        int resizes = 0;
        int exits = 0;
        twin.ExitCompleted += () => exits++;
        twin.FocusedOut += focusOuts.Add;
        twin.ViewportInertChanged += inert.Add;
        twin.Resized += () => resizes++;

        twin.OnFocusOut($$"""{"event":{{MouseEnterJson.Replace("\"mouseenter\"", "\"focusout\"", StringComparison.Ordinal)}}}""");
        twin.OnViewportInertChange("""{"inert":true}""");
        twin.OnViewportInertChange("""{"inert":false}""");
        twin.OnResize("{}");
        twin.OnResize("{}");
        twin.OnExitComplete("{}");

        Assert.Equal("focusout", Assert.Single(focusOuts).Event!.Type);
        Assert.Equal([true, false], inert);
        Assert.Equal(2, resizes);
        Assert.Equal(1, exits);
        Assert.Equal("run", twin.Decide("escape", MouseEnterJson));
    }

    [Fact]
    [Trait("Requirement", "NAV-PTR-18")]
    public void Every_payload_round_trips_through_the_source_generated_context()
    {
        Assert.Equal(
            """{"reason":"list-navigation","direction":"down"}""",
            JsonSerializer.Serialize(new NavigationMenuActivation("list-navigation", "down", null), BlazixJsonContext.Default.NavigationMenuActivation));
        Assert.Equal("""{"inert":true}""", JsonSerializer.Serialize(new NavigationMenuViewportInertChange(true), BlazixJsonContext.Default.NavigationMenuViewportInertChange));
        Assert.Equal("{}", JsonSerializer.Serialize(new NavigationMenuFocusOut(null), BlazixJsonContext.Default.NavigationMenuFocusOut));
        Assert.Equal("""{"part":"root"}""", JsonSerializer.Serialize(new NavigationMenuOptions(), BlazixJsonContext.Default.NavigationMenuOptions));
    }

    /// <summary>A runtime the callback tests never reach: they call the twin's methods directly.</summary>
    private sealed class NoRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new NotSupportedException();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => throw new NotSupportedException();
    }
}
