using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// M6 track 8b slice 3 (issue 279): the C# half of the Provider's visual state, the Popup's measurement and the nested-drawer
/// report. The `drawer` module's Viewport handle is told its Provider's token, its Root's frontmost height and its enclosing
/// drawers' popups; the Popup attaches a handle that measures it and reports the height to its Root (DRW-STA-10); an open
/// nested drawer reports its frontmost height and presence to its parent (STA-11, STA-12); a nested drawer's swiping reaches
/// its parent (PTR-17); an Indent attaches a handle under a Provider (DOM-32). The module's half is js/drawer.provider.test.ts.
/// </summary>
public sealed class DrawerStackGestureTests : IDisposable
{
    private readonly DialogTestContext test = new();
    private readonly BunitJSModuleInterop drawerModule;

    public DrawerStackGestureTests()
    {
        drawerModule = test.Context.JSInterop.SetupModule("./_content/Blazix.Primitives/js/drawer.js");
        BunitJSModuleInterop handle = drawerModule.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: a viewport sends its provider's token, its root's frontmost height and its enclosing drawers' popups")]
    [Trait("Requirement", "DRW-PTR-16")]
    [Trait("Requirement", "DRW-API-14")]
    public async Task A_viewport_sends_its_provider_frontmost_height_and_ancestors()
    {
        IRenderedComponent<DrawerStackHarness> cut = test.Context.Render<DrawerStackHarness>();
        cut.WaitForAssertion(() => Assert.Equal(2, Attaches("viewport").Count));

        string parent = Options(Attaches("viewport")[0]);
        string child = Options(Attaches("viewport")[1]);
        string token = Assert.Single(Attaches("indent")) is { } indent ? Value(Options(indent), "provider") : string.Empty;
        Assert.NotEqual(string.Empty, token);
        Assert.Equal(token, Value(parent, "provider"));
        Assert.Equal(token, Value(child, "provider"));
        Assert.Contains("\"hasParent\":false", parent, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ancestorPopups\"", parent, StringComparison.Ordinal);
        Assert.Contains("\"hasParent\":true", child, StringComparison.Ordinal);
        // The harness has no Backdrop: the popup is reference 0 and the parent's popup reference 1.
        cut.WaitForAssertion(() => Assert.True(AnySent("viewport", "\"popup\":0") && AnySent("viewport", "\"hasParent\":true,\"ancestorPopups\":[1]")));

        DrawerRootContext drawer = cut.Instance.Parent!.DrawerContext;
        await cut.InvokeAsync(() => drawer.ReportPopupHeight(120));
        cut.WaitForAssertion(() => Assert.True(AnySent("viewport", "\"frontmostHeight\":120")));
    }

    [Fact(DisplayName = "port: a nested drawer's swiping report marks its parent's popup as swiping a nested drawer")]
    [Trait("Requirement", "DRW-PTR-17")]
    [Trait("Requirement", "DRW-STA-12")]
    [Trait("Requirement", "DRW-DOM-04")]
    public async Task A_nested_swiping_report_reaches_the_parent()
    {
        IRenderedComponent<DrawerStackHarness> cut = test.Context.Render<DrawerStackHarness>();
        cut.WaitForAssertion(() => Assert.Equal(2, Attaches("viewport").Count));
        DrawerTwin child = Twin(Attaches("viewport")[1]);

        await cut.InvokeAsync(() => child.OnNestedSwipingChange("""{"swiping":true}"""));
        cut.WaitForAssertion(() => Assert.True(cut.Find("[data-testid=parent-popup]").HasAttribute(DrawerPopupDataAttributes.NestedDrawerSwiping)));

        await cut.InvokeAsync(() => child.OnNestedSwipingChange("""{"swiping":false}"""));
        cut.WaitForAssertion(() => Assert.False(cut.Find("[data-testid=parent-popup]").HasAttribute(DrawerPopupDataAttributes.NestedDrawerSwiping)));
    }

    [Fact(DisplayName = "port: the popup attaches the drawer module and its height report sets its root's frontmost height")]
    [Trait("Requirement", "DRW-STA-10")]
    [Trait("Requirement", "DRW-STA-11")]
    [Trait("Requirement", "DRW-DOM-10")]
    public async Task The_popup_reports_its_height_to_its_root()
    {
        IRenderedComponent<DrawerStackHarness> cut = test.Context.Render<DrawerStackHarness>(p => p.Add(x => x.ChildOpen, false));
        cut.WaitForAssertion(() => Assert.Single(Attaches("popup")));
        JSRuntimeInvocation attach = Attaches("popup")[0];
        string options = Options(attach);
        Assert.Contains("\"open\":true", options, StringComparison.Ordinal);
        Assert.Contains("\"mounted\":true", options, StringComparison.Ordinal);
        Assert.Contains("\"hasNestedDrawer\":false", options, StringComparison.Ordinal);
        Assert.Equal(cut.Instance.Parent!.DrawerContext.PopupElement?.Id, ElementId(attach));

        await cut.InvokeAsync(() => Twin(attach).OnPopupHeightChange("""{"height":240}"""));

        DrawerRootContext drawer = cut.Instance.Parent!.DrawerContext;
        Assert.Equal(240, drawer.PopupHeight);
        Assert.Equal(240, drawer.FrontmostHeight);
        cut.WaitForAssertion(() => Assert.Contains("--drawer-frontmost-height: 240px", cut.Find("[data-testid=parent-popup]").GetAttribute("style"), StringComparison.Ordinal));
        cut.WaitForAssertion(() => Assert.True(AnySent("popup", "\"frontmostHeight\":240")));
    }

    [Fact(DisplayName = "port: an open nested drawer reports its frontmost height and presence to its parent, and clears them when it closes")]
    [Trait("Requirement", "DRW-STA-11")]
    [Trait("Requirement", "DRW-STA-12")]
    public async Task A_nested_drawer_reports_to_its_parent()
    {
        IRenderedComponent<DrawerStackHarness> cut = test.Context.Render<DrawerStackHarness>();
        cut.WaitForAssertion(() => Assert.Equal(2, Attaches("popup").Count));
        DrawerRootContext parent = cut.Instance.Parent!.DrawerContext;
        await cut.InvokeAsync(() => Twin(Attaches("popup")[0]).OnPopupHeightChange("""{"height":300}"""));
        cut.WaitForAssertion(() => Assert.True(parent.HasNestedDrawer));

        await cut.InvokeAsync(() => Twin(Attaches("popup")[1]).OnPopupHeightChange("""{"height":180}"""));
        cut.WaitForAssertion(() => Assert.Equal(180, parent.FrontmostHeight));
        cut.WaitForAssertion(() => Assert.True(AnySent("popup", "\"hasNestedDrawer\":true")));

        // Closed, the nested drawer is still present while it animates out (DRW-STA-12), and gone once it has.
        cut.Render(p => p.Add(x => x.ChildOpen, false));
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(300, parent.FrontmostHeight);
            Assert.True(parent.HasNestedDrawer);
        });
        await test.CompletePopupTransitionAsync(cut, open: false);
        cut.WaitForAssertion(() => Assert.False(parent.HasNestedDrawer));
    }

    [Fact(DisplayName = "port: an indent attaches the drawer module under a provider, and not without one")]
    [Trait("Requirement", "DRW-DOM-32")]
    public void An_indent_attaches_under_a_provider_only()
    {
        IRenderedComponent<DrawerStackHarness> cut = test.Context.Render<DrawerStackHarness>();
        cut.WaitForAssertion(() => Assert.Single(Attaches("indent")));
        Assert.NotNull(ElementId(Attaches("indent")[0]));

        IRenderedComponent<DrawerStackHarness> bare = test.Context.Render<DrawerStackHarness>(p => p.Add(x => x.WithProvider, false));
        bare.WaitForAssertion(() => Assert.Equal(4, Attaches("viewport").Count));
        Assert.Single(Attaches("indent"));
        Assert.Contains("\"provider\"", Options(Attaches("viewport")[0]), StringComparison.Ordinal);
        Assert.DoesNotContain("\"provider\"", Options(Attaches("viewport")[2]), StringComparison.Ordinal);
    }

    private List<JSRuntimeInvocation> Attaches(string role) =>
        drawerModule.Invocations["attach"].Where(attach => Options(attach).Contains($"\"role\":\"{role}\"", StringComparison.Ordinal)).ToList();

    private static string? ElementId(JSRuntimeInvocation attach) =>
        attach.Arguments[0] is ElementReference reference ? reference.Id : attach.Arguments[0] as string;

    private static string Options(JSRuntimeInvocation attach) => attach.Arguments[1] as string ?? string.Empty;

    private static DrawerTwin Twin(JSRuntimeInvocation attach) => ((DotNetObjectReference<DrawerTwin>)attach.Arguments[2]!).Value;

    // Every handle the module attaches shares one bUnit module, so a handle's later options are found by role and content.
    private bool AnySent(string role, string fragment) =>
        drawerModule.Invocations["attach"].Select(Options)
            .Concat(test.Context.JSInterop.Invocations["update"].Select(update => update.Arguments[0] as string ?? string.Empty))
            .Any(json => json.Contains($"\"role\":\"{role}\"", StringComparison.Ordinal) && json.Contains(fragment, StringComparison.Ordinal));

    private static string Value(string json, string name)
    {
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty(name, out System.Text.Json.JsonElement value) ? value.ToString() : string.Empty;
    }
}
