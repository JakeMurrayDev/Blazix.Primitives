using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: <see cref="MenuInput"/>'s <c>menu-filter</c> handle on the C# side, the twin contract (M5 track 12): the options it
/// attaches and updates with, and its reports reaching the provider (MNU-EVT-16, API-58) and the part's state
/// (MNU-STA-22), over <see cref="MenuFilterInputHost"/>. The module's own half is js/menu-filter.test.ts.
/// </summary>
public sealed class MenuFilterInputHandleTests : BunitContext
{
    private const string ModulePath = "./_content/Blazix.Primitives/js/menu-filter.js";

    private readonly BunitJSModuleInterop module;
    private readonly BunitJSModuleInterop handle;

    public MenuFilterInputHandleTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddBlazixPrimitives();
        module = JSInterop.SetupModule(ModulePath);
        handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
    }

    [Fact(DisplayName = "port: attaches the input's handle with its menu key, the committed query, its revision and the open state")]
    [Trait("Requirement", "MNU-DOM-21")]
    [Trait("Requirement", "MNU-STA-22")]
    public void Attaches_the_input_handle_with_its_options()
    {
        IRenderedComponent<MenuFilterInputHost> cut = Render<MenuFilterInputHost>(p => p.Add(x => x.DefaultQuery, "re"));

        JSRuntimeInvocation attach = Assert.Single(module.Invocations["attach"]);
        Assert.Equal(cut.Find("input").GetAttribute("blazor:elementreference"), ((ElementReference)attach.Arguments[0]!).Id);
        string options = (string)attach.Arguments[1]!;
        Assert.Contains("\"part\":\"input\"", options, StringComparison.Ordinal);
        Assert.Contains("\"value\":\"re\"", options, StringComparison.Ordinal);
        Assert.Contains("\"valueRevision\":0", options, StringComparison.Ordinal);
        Assert.Contains("\"open\":true", options, StringComparison.Ordinal);
        Assert.Contains("\"keyboardOpen\":false", options, StringComparison.Ordinal);
        Assert.Matches("\"menuKey\":\"[^\"]+\"", options);
    }

    [Fact(DisplayName = "port: a typed edit requests the query with input-change and an emptied input with input-clear")]
    [Trait("Requirement", "MNU-EVT-16")]
    [Trait("Requirement", "MNU-API-58")]
    public async Task A_typed_edit_requests_the_query()
    {
        List<(string Value, EventReason Reason)> requests = [];
        IRenderedComponent<MenuFilterInputHost> cut = Render<MenuFilterInputHost>(p => p
            .Add(x => x.OnValueChange, (ValueChangeEventArgs<string> args) => requests.Add((args.Value, args.Details.Reason))));
        MenuFilterTwin twin = Twin();

        await cut.InvokeAsync(() => twin.OnValueChange("""{"value":"ap","reason":"input-change","event":null}"""));
        await cut.InvokeAsync(() => twin.OnValueChange("""{"value":"","reason":"input-clear","event":null}"""));

        Assert.Equal([("ap", EventReason.InputChange), ("", EventReason.InputClear)], requests);
    }

    [Fact(DisplayName = "port: a committed edit sends the query under the same revision, so a later keystroke is not overwritten")]
    [Trait("Requirement", "MNU-EVT-16")]
    [Trait("Requirement", "MNU-DOM-21")]
    public async Task A_committed_edit_keeps_the_revision()
    {
        IRenderedComponent<MenuFilterInputHost> cut = Render<MenuFilterInputHost>();
        MenuFilterTwin twin = Twin();

        await cut.InvokeAsync(() => twin.OnValueChange("""{"value":"ap","reason":"input-change","event":null}"""));

        string update = LastUpdate();
        Assert.Contains("\"value\":\"ap\"", update, StringComparison.Ordinal);
        Assert.Contains("\"valueRevision\":0", update, StringComparison.Ordinal);
        Assert.Equal("", cut.Find("input").GetAttribute("value"));
    }

    [Fact(DisplayName = "port: a canceled edit re-sends the committed query under a new revision")]
    [Trait("Requirement", "MNU-EVT-16")]
    [Trait("Requirement", "MNU-API-58")]
    public async Task A_canceled_edit_resends_the_query()
    {
        IRenderedComponent<MenuFilterInputHost> cut = Render<MenuFilterInputHost>(p => p
            .Add(x => x.DefaultQuery, "a")
            .Add(x => x.OnValueChange, (ValueChangeEventArgs<string> args) => args.Details.Cancel()));
        MenuFilterTwin twin = Twin();

        await cut.InvokeAsync(() => twin.OnValueChange("""{"value":"ab","reason":"input-change","event":null}"""));

        string update = LastUpdate();
        Assert.Contains("\"value\":\"a\"", update, StringComparison.Ordinal);
        Assert.Contains("\"valueRevision\":1", update, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "port: mirrors the module's focus ring into the input's state")]
    [Trait("Requirement", "MNU-STA-22")]
    public async Task Mirrors_the_focus_ring_into_the_state()
    {
        IRenderedComponent<MenuFilterInputHost> cut = Render<MenuFilterInputHost>(p => p
            .Add(x => x.InputClass, state => state.Highlighted ? "ring" : "plain"));
        MenuFilterTwin twin = Twin();
        Assert.Equal("plain", cut.Find("input").ClassName);

        await cut.InvokeAsync(() => twin.OnHighlightedChange("""{"highlighted":true}"""));

        Assert.Equal("ring", cut.Find("input").ClassName);
        Assert.False(cut.Find("input").HasAttribute(MenuInputDataAttributes.Highlighted));
    }

    [Fact(DisplayName = "port: closing the menu tells the input's handle, which then drops edits")]
    [Trait("Requirement", "MNU-EVT-16")]
    public void Closing_the_menu_updates_the_handle()
    {
        IRenderedComponent<MenuFilterInputHost> cut = Render<MenuFilterInputHost>(p => p.Add(x => x.KeepMounted, true));

        cut.Render(p => p.Add(x => x.Open, false));

        Assert.Contains("\"open\":false", LastUpdate(), StringComparison.Ordinal);
    }

    private MenuFilterTwin Twin() => ((DotNetObjectReference<MenuFilterTwin>)module.Invocations["attach"][^1].Arguments[2]!).Value;

    private string LastUpdate() => (string)handle.Invocations["update"][^1].Arguments[0]!;
}
