using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

// Family tests use the project's root namespace (tests/Iodine.Headless.Tests.Components/CONTEXT.md).
namespace Iodine.Headless.Tests.Components;

/// <summary>
/// L2: the <c>Menubar</c> part (MBR §3.1, §4, §5.1; M5 track 8, issue 195): its element, attributes and state, and the
/// flag its menus set (MBR-STA-02..04, DOM-03).
/// </summary>
public sealed class MenubarTests : BunitContext
{
    public MenubarTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddIodineHeadless();
    }

    [Fact(DisplayName = "throws when the menubar context is missing")]
    [Trait("Requirement", "MBR-EDGE-01")]
    public void Throws_when_the_menubar_context_is_missing()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Render<MenubarContextReader>());

        Assert.Equal("Iodine: The `Menubar` context is missing. Menubar parts must be placed within `<Menubar>`.", error.Message);
    }

    [Fact(DisplayName = "port: Menubar renders a div with role menubar, its id, aria-orientation and its state attributes")]
    [Trait("Requirement", "MBR-STR-01")]
    [Trait("Requirement", "MBR-ARIA-01")]
    [Trait("Requirement", "MBR-ARIA-02")]
    [Trait("Requirement", "MBR-DOM-01")]
    [Trait("Requirement", "MBR-DOM-02")]
    [Trait("Requirement", "MBR-DOM-03")]
    [Trait("Requirement", "MBR-DOM-04")]
    public void Renders_a_div_with_role_menubar_its_id_and_its_attributes()
    {
        IRenderedComponent<MenubarHost> cut = Render<MenubarHost>();

        IElement bar = cut.Find("[role=\"menubar\"]");
        Assert.Equal("DIV", bar.TagName);
        Assert.Equal("bar", bar.Id);
        Assert.Equal("horizontal", bar.GetAttribute("aria-orientation"));
        Assert.Equal("horizontal", bar.GetAttribute("data-orientation"));
        Assert.Equal("", bar.GetAttribute("data-modal"));
        Assert.False(bar.HasAttribute("data-has-submenu-open"));
    }

    [Fact(DisplayName = "port: a vertical, non-modal Menubar renders vertical orientation and no data-modal")]
    [Trait("Requirement", "MBR-API-01")]
    [Trait("Requirement", "MBR-API-03")]
    [Trait("Requirement", "MBR-ARIA-02")]
    [Trait("Requirement", "MBR-DOM-02")]
    public void A_vertical_non_modal_menubar_renders_its_attributes()
    {
        IRenderedComponent<MenubarHost> cut = Render<MenubarHost>(p => p.Add(x => x.Orientation, Iodine.Headless.Orientation.Vertical).Add(x => x.Modal, false));

        IElement bar = cut.Find("[role=\"menubar\"]");
        Assert.Equal("vertical", bar.GetAttribute("aria-orientation"));
        Assert.Equal("vertical", bar.GetAttribute("data-orientation"));
        Assert.False(bar.HasAttribute("data-modal"));
    }

    [Fact(DisplayName = "port: a consumer role and aria-orientation replace the part's, and a Menubar without an id renders a generated one")]
    [Trait("Requirement", "MBR-API-05")]
    [Trait("Requirement", "MBR-API-06")]
    public void Consumer_attributes_win_and_an_id_is_generated()
    {
        IRenderedComponent<Menubar> cut = Render<Menubar>(p => p.AddUnmatched("role", "toolbar").AddUnmatched("aria-orientation", "vertical"));

        IElement bar = cut.Find("div");
        Assert.Equal("toolbar", bar.GetAttribute("role"));
        Assert.Equal("vertical", bar.GetAttribute("aria-orientation"));
        Assert.False(string.IsNullOrEmpty(bar.Id));
    }

    [Fact(DisplayName = "port: the state is MenubarState with the orientation, modal and whether a menu is open")]
    [Trait("Requirement", "MBR-STA-01")]
    public void The_state_is_the_orientation_modal_and_the_flag()
    {
        MenubarState? seen = null;
        Render<Menubar>(p => p.Add(x => x.Modal, false).Add(x => x.ClassValue, state =>
        {
            seen = state;
            return null;
        }));

        Assert.Equal(new MenubarState(Iodine.Headless.Orientation.Horizontal, false, false), seen);
    }

    // MBR-STR-03 (Menubar.tsx:79, FloatingTree): the bar draws an elementless session and cascades its token, so each bar
    // menu's session names it as its parent and the bar's menus are one forest (Deliverable 4 of the brief).
    [Fact(DisplayName = "port: the bar draws an elementless session whose token is its menus' parent token")]
    [Trait("Requirement", "MBR-STR-03")]
    public void The_bar_draws_an_elementless_session_its_menus_name_as_parent()
    {
        Render<MenubarHost>();

        AssertTheMenuNamesTheBarsSession();
    }

    // MenuTrigger.tsx:95-108: the trigger holding a detached Root's popup forwards the bar's floating parent node too.
    [Fact(DisplayName = "port: a detached menu held by a bar trigger names the bar's session as its parent")]
    [Trait("Requirement", "MBR-STR-04")]
    public void A_detached_menu_held_by_a_bar_trigger_names_the_bars_session()
    {
        IRenderedComponent<MenubarDetachedHost> cut = Render<MenubarDetachedHost>();

        Assert.Equal("bar", cut.Find("[data-testid=\"popup\"]").GetAttribute("data-rootownerid"));
        cut.WaitForAssertion(AssertTheMenuNamesTheBarsSession, TimeSpan.FromSeconds(2));
    }

    // The menu's session (the one with a popup id) names as parent a session the bar drew (no popup id).
    private void AssertTheMenuNamesTheBarsSession()
    {
        List<JsonElement> sessions = [.. JSInterop.Invocations
            .SelectMany(invocation => invocation.Arguments.OfType<string>())
            .Where(argument => argument.StartsWith('{') && argument.Contains("\"session\":", StringComparison.Ordinal))
            .Select(argument => JsonDocument.Parse(argument).RootElement)];
        string? parentToken = sessions
            .Where(options => options.GetProperty("session").TryGetProperty("floatingId", out JsonElement id) && id.ValueKind == JsonValueKind.String)
            .Select(options => options.GetProperty("session").TryGetProperty("parentToken", out JsonElement token) ? token.GetString() : null)
            .LastOrDefault();

        Assert.NotNull(parentToken);
        Assert.Contains(sessions, options => options.TryGetProperty("sessionToken", out JsonElement token) && token.GetString() == parentToken
            && !(options.GetProperty("session").TryGetProperty("floatingId", out JsonElement id) && id.ValueKind == JsonValueKind.String));
    }

    [Fact(DisplayName = "port: a bar menu's open sets data-has-submenu-open, and its close by another reason clears it")]
    [Trait("Requirement", "MBR-STA-02")]
    [Trait("Requirement", "MBR-STA-03")]
    [Trait("Requirement", "MBR-DOM-03")]
    public async Task A_bar_menus_open_sets_the_flag_and_its_close_clears_it()
    {
        IRenderedComponent<MenubarHost> cut = Render<MenubarHost>(p => p.Add(x => x.DefaultOpen, true));
        cut.WaitForAssertion(() => Assert.True(cut.Find("[role=\"menubar\"]").HasAttribute("data-has-submenu-open")), TimeSpan.FromSeconds(2));

        await cut.InvokeAsync(() => cut.FindComponent<MenuRoot<NoPayload>>().Instance.CloseAsync());

        cut.WaitForAssertion(() => Assert.False(cut.Find("[role=\"menubar\"]").HasAttribute("data-has-submenu-open")), TimeSpan.FromSeconds(2));
    }

    // The brief's render counts: clearing the flag renders the bar once and nothing of a closed menu's content. The other
    // menu's trigger renders once: the bar's own render reaches it through the bar's child content, and the flag itself
    // only sends the trigger's hover and focus options (issue 239, chosen by the user 2026-11-28).
    [Fact(DisplayName = "port: a flag flip renders the bar once, the other trigger once and no closed menu's content")]
    [Trait("Requirement", "MBR-STA-02")]
    [Trait("Requirement", "MBR-STA-03")]
    public async Task A_flag_flip_renders_the_bar_once_and_no_closed_menus_content()
    {
        IRenderedComponent<MenubarRenderHost> cut = Render<MenubarRenderHost>();
        cut.WaitForAssertion(() => Assert.True(cut.Find("[role=\"menubar\"]").HasAttribute("data-has-submenu-open")), TimeSpan.FromSeconds(2));
        Dictionary<string, int> before = new(cut.Instance.Renders);

        await cut.InvokeAsync(() => cut.Instance.File!.CloseAsync());
        cut.WaitForAssertion(() => Assert.False(cut.Find("[role=\"menubar\"]").HasAttribute("data-has-submenu-open")), TimeSpan.FromSeconds(2));

        Dictionary<string, int> renders = cut.Instance.Renders;
        Assert.Equal(before["bar"] + 1, renders["bar"]);
        Assert.Equal(before["edit"] + 1, renders["edit"]);
        Assert.Equal(0, renders.GetValueOrDefault("edit-item"));
    }

    [Fact(DisplayName = "port: a bar menu's open and its close each render the other menu's trigger once")]
    [Trait("Requirement", "MBR-STA-02")]
    [Trait("Requirement", "MBR-STA-03")]
    public async Task A_bar_menus_open_and_close_each_render_the_other_trigger_once()
    {
        IRenderedComponent<MenubarRenderHost> cut = Render<MenubarRenderHost>();
        cut.WaitForAssertion(() => Assert.True(cut.Find("[role=\"menubar\"]").HasAttribute("data-has-submenu-open")), TimeSpan.FromSeconds(2));
        await cut.InvokeAsync(() => cut.Instance.File!.CloseAsync());
        cut.WaitForAssertion(() => Assert.False(cut.Find("[role=\"menubar\"]").HasAttribute("data-has-submenu-open")), TimeSpan.FromSeconds(2));
        int closed = cut.Instance.Renders["edit"];

        await cut.InvokeAsync(() => cut.Instance.File!.Store.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.ImperativeAction)));
        cut.WaitForAssertion(() => Assert.True(cut.Find("[role=\"menubar\"]").HasAttribute("data-has-submenu-open")), TimeSpan.FromSeconds(2));
        int opened = cut.Instance.Renders["edit"];
        await cut.InvokeAsync(() => cut.Instance.File!.CloseAsync());
        cut.WaitForAssertion(() => Assert.False(cut.Find("[role=\"menubar\"]").HasAttribute("data-has-submenu-open")), TimeSpan.FromSeconds(2));

        Assert.Equal(closed + 1, opened);
        Assert.Equal(opened + 1, cut.Instance.Renders["edit"]);
    }

    // Issue 239's wrapped shape: a consumer component without parameters stops the bar's render above Edit's menu, so the
    // flag reaches Edit's trigger only through the bar's subscription, which sends its options without a render.
    [Fact(DisplayName = "port: a flag flip sends a trigger the bar's render does not reach its new options and renders it zero times")]
    [Trait("Requirement", "MBR-STA-02")]
    [Trait("Requirement", "MNU-API-19")]
    public async Task A_flag_flip_sends_an_unreached_trigger_its_options_without_a_render()
    {
        BunitJSModuleInterop interactions = JSInterop.SetupModule("./_content/Iodine.Headless/js/interactions.js");
        BunitJSModuleInterop handle = interactions.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        IRenderedComponent<MenubarWrappedHost> cut = Render<MenubarWrappedHost>();
        cut.WaitForAssertion(() => Assert.True(cut.Find("[role=\"menubar\"]").HasAttribute("data-has-submenu-open")), TimeSpan.FromSeconds(2));
        cut.WaitForAssertion(() => Assert.True(EditOptions(interactions, handle).TryGetProperty("hover", out _)), TimeSpan.FromSeconds(2));
        int before = cut.Instance.EditRenders;

        await cut.InvokeAsync(() => cut.Instance.File!.CloseAsync());
        cut.WaitForAssertion(() => Assert.False(cut.Find("[role=\"menubar\"]").HasAttribute("data-has-submenu-open")), TimeSpan.FromSeconds(2));

        cut.WaitForAssertion(() => Assert.False(EditOptions(interactions, handle).TryGetProperty("hover", out _)), TimeSpan.FromSeconds(2));
        Assert.False(EditOptions(interactions, handle).TryGetProperty("focus", out _));
        Assert.Equal(before, cut.Instance.EditRenders);
    }

    // Issue 239's follow-up (main red at 9446001b, menubar.spec.ts's Shift+Tab title on Server): a bar menu's commit flips
    // the flag inside a listener of the menu's store, and the bar's render runs at once and re-renders the menu's popup
    // through its child content. The popup's focus manager then read the commit before the session had sent it, so on a
    // collapsed reopen the focus manager ran its initial focus. The session sends a commit before the popup's focus manager
    // sends what it read from it, as Base UI commits them in one batch.
    [Fact(DisplayName = "port: a bar menu's close reaches its session before its popup's focus manager")]
    [Trait("Requirement", "MBR-STA-03")]
    [Trait("Requirement", "MBR-KEY-12")]
    public async Task A_bar_menus_close_reaches_its_session_before_its_focus_manager()
    {
        List<string> sent = [];
        const string prefix = "./_content/Iodine.Headless/js/";
        BunitJSModuleInterop interactions = JSInterop.SetupModule(prefix + "interactions.js");
        BunitJSModuleInterop interactionsHandle = interactions.SetupModule("attach", _ => true);
        interactionsHandle.SetupVoid("update", invocation => Record(sent, "session", invocation)).SetVoidResult();
        interactionsHandle.SetupVoid("dispose", _ => true).SetVoidResult();
        BunitJSModuleInterop focusManager = JSInterop.SetupModule(prefix + "focus-manager.js");
        BunitJSModuleInterop focusManagerHandle = focusManager.SetupModule("attach", _ => true);
        focusManagerHandle.SetupVoid("update", invocation => Record(sent, "focus", invocation)).SetVoidResult();
        focusManagerHandle.SetupVoid("dispose", _ => true).SetVoidResult();
        IRenderedComponent<MenubarRenderHost> cut = Render<MenubarRenderHost>();
        cut.WaitForAssertion(() => Assert.True(cut.Find("[role=\"menubar\"]").HasAttribute("data-has-submenu-open")), TimeSpan.FromSeconds(2));
        cut.WaitForAssertion(() => Assert.NotEmpty(focusManager.Invocations["attach"]), TimeSpan.FromSeconds(2));
        sent.Clear();

        await cut.InvokeAsync(() => cut.Instance.File!.CloseAsync());
        cut.WaitForAssertion(() => Assert.False(cut.Find("[role=\"menubar\"]").HasAttribute("data-has-submenu-open")), TimeSpan.FromSeconds(2));

        int revision = sent.Select(entry => int.Parse(entry.Split(':')[1], System.Globalization.CultureInfo.InvariantCulture)).Max();
        int session = sent.IndexOf($"session:{revision}");
        int focus = sent.IndexOf($"focus:{revision}");
        Assert.True(session >= 0, string.Join(" ", sent));
        Assert.True(focus >= 0, string.Join(" ", sent));
        Assert.True(session < focus, string.Join(" ", sent));
    }

    // Records an update that carries a relay, by its handle and the relay's revision; every update is accepted.
    private static bool Record(List<string> sent, string handle, JSRuntimeInvocation invocation)
    {
        JsonElement root = JsonDocument.Parse((string)invocation.Arguments[0]!).RootElement;
        if (root.TryGetProperty("relay", out JsonElement relay) && relay.TryGetProperty("revision", out JsonElement revision)
            && (handle == "focus" || root.TryGetProperty("session", out _)))
        {
            sent.Add($"{handle}:{revision.GetInt32()}");
        }

        return true;
    }

    // The last trigger options Edit's interactions handle was sent, by its attach or an update.
    private static JsonElement EditOptions(BunitJSModuleInterop interactions, BunitJSModuleInterop handle)
    {
        JsonElement last = default;
        IEnumerable<string> sent = interactions.Invocations["attach"].Select(attach => (string)attach.Arguments[1]!)
            .Concat(handle.Invocations["update"].Select(update => (string)update.Arguments[0]!));
        foreach (string json in sent)
        {
            JsonElement root = JsonDocument.Parse(json).RootElement;
            if (root.TryGetProperty("trigger", out JsonElement trigger) && trigger.TryGetProperty("triggerId", out JsonElement id) && id.GetString() == "edit")
            {
                last = trigger;
            }
        }

        Assert.Equal(JsonValueKind.Object, last.ValueKind);
        return last;
    }
}
