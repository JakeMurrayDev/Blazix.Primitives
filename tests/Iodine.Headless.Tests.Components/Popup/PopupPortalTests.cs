using Microsoft.JSInterop;
using AngleSharp.Dom;
using Blazix.Primitives.Utilities;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The portal's markup at L2 over the test-only portal (FloatingPortal.test.tsx and FloatingFocusManager.test.tsx titles
/// verbatim; FLT-API-13, FLT-DOM-11..14, FLT-ARIA-04, PSH-STR-11/12): the node C# renders in place, inside its
/// <c>display: contents</c> wrapper, with the outside guards and the <c>aria-owns</c> owner beside the wrapper while a
/// non-modal, open focus manager has published to it. The module that moves the node is mocked here; its moves are L3
/// (<c>portal.test.ts</c>) and L4. A test publisher stands in for the focus manager's publication (track 4's).
/// </summary>
public sealed class PopupPortalTests : IDisposable
{
    private const string ModulePath = "./_content/Blazix.Primitives/js/portal.js";

    private readonly PopupTestContext test = new();
    private readonly BunitJSModuleInterop module;
    private readonly BunitJSModuleInterop handle;

    public PopupPortalTests()
    {
        module = test.Context.JSInterop.SetupModule(ModulePath);
        handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "FloatingPortalLite forwards HTML props to the portal element")]
    [Trait("Requirement", "PSH-STR-12")]
    public void FloatingPortalLite_forwards_HTML_props_to_the_portal_element()
    {
        IRenderedComponent<PopupTestHost> cut = test.Render((builder, _) =>
        {
            builder.OpenComponent<TestPopupPortal>(0);
            builder.AddComponentParameter(1, nameof(TestPopupPortal.Lite), true);
            builder.AddAttribute(2, "data-testid", "lite-portal");
            builder.AddComponentParameter(3, nameof(TestPopupPortal.ChildContent), (RenderFragment)(content => content.AddMarkupContent(0, "<div></div>")));
            builder.CloseComponent();
        });

        IElement portal = cut.Find("[data-testid=\"lite-portal\"]");
        Assert.True(portal.HasAttribute("data-base-ui-portal"));
    }

    [Fact(DisplayName = "uses the rendered portal ID for the aria-owns relationship")]
    [Trait("Requirement", "FLT-DOM-12")]
    [Trait("Requirement", "FLT-ARIA-04")]
    [Trait("Requirement", "FLT-FOC-18")]
    public void Uses_the_rendered_portal_ID_for_the_aria_owns_relationship()
    {
        IRenderedComponent<PopupTestHost> cut = test.Render((builder, _) =>
        {
            builder.OpenComponent<TestPopupPortal>(0);
            builder.AddAttribute(1, "id", "custom-portal");
            builder.AddComponentParameter(2, nameof(TestPopupPortal.ChildContent), Publisher(modal: false, open: true));
            builder.CloseComponent();
        });

        cut.WaitForAssertion(() => Assert.Equal("custom-portal", cut.Find("[aria-owns]").GetAttribute("aria-owns")));
    }

    [Fact(DisplayName = "renders the aria-owns owner without changing regular reference semantics")]
    [Trait("Requirement", "FLT-ARIA-04")]
    public async Task Renders_the_aria_owns_owner_without_changing_regular_reference_semantics()
    {
        IRenderedComponent<PopupTestHost> cut = RenderOpenable(ownerRole: null);

        await cut.Find("[data-testid=\"reference\"]").ClickAsync(new());

        cut.WaitForAssertion(() =>
        {
            IElement portalNode = cut.Find("[data-testid=\"floating\"]").Closest("[data-base-ui-portal]")!;
            string portalNodeId = portalNode.Id ?? string.Empty;
            IElement owner = cut.Find("span[aria-owns]");
            Assert.NotEqual(string.Empty, portalNodeId);
            Assert.False(owner.HasAttribute("role"));
            Assert.Equal(portalNodeId, owner.GetAttribute("aria-owns"));
            Assert.False(cut.Find("[data-testid=\"reference\"]").HasAttribute("aria-owns"));
        });
    }

    [Fact(DisplayName = "supports setting the aria-owns owner role explicitly")]
    [Trait("Requirement", "FLT-ARIA-04")]
    public async Task Supports_setting_the_aria_owns_owner_role_explicitly()
    {
        IRenderedComponent<PopupTestHost> cut = RenderOpenable(ownerRole: "group");

        await cut.Find("[data-testid=\"reference\"]").ClickAsync(new());

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(cut.Find("[data-testid=\"floating\"]").Closest("[data-base-ui-portal]"));
            Assert.Equal("group", cut.Find("span[aria-owns]").GetAttribute("role"));
        });
    }

    [Fact(DisplayName = "port: the portal node renders hidden as the only child of a display: contents wrapper, with the consumer's attributes and a generated id")]
    [Trait("Requirement", "FLT-DOM-11")]
    [Trait("Requirement", "PSH-STR-11")]
    public void The_portal_node_renders_hidden_as_the_only_child_of_a_display_contents_wrapper()
    {
        IRenderedComponent<PopupTestHost> cut = test.Render((builder, _) =>
        {
            builder.OpenComponent<TestPopupPortal>(0);
            builder.AddAttribute(1, "class", "portal");
            builder.AddAttribute(2, "data-testid", "portal");
            builder.AddComponentParameter(3, nameof(TestPopupPortal.ChildContent), (RenderFragment)(content => content.AddMarkupContent(0, "<p data-testid=\"content\"></p>")));
            builder.CloseComponent();
        });

        IElement node = cut.Find("[data-testid=\"portal\"]");
        IElement wrapper = node.ParentElement!;
        Assert.Equal("DIV", wrapper.TagName);
        Assert.Equal("display: contents", wrapper.GetAttribute("style"));
        Assert.Single(wrapper.Children);
        Assert.Equal("display: none", node.GetAttribute("style"));
        Assert.Equal("portal", node.GetAttribute("class"));
        Assert.StartsWith("base-ui-", node.Id, StringComparison.Ordinal);
        Assert.Equal(string.Empty, node.GetAttribute("data-base-ui-portal"));
        Assert.Equal("portal", cut.Find("[data-testid=\"content\"]").ParentElement!.GetAttribute("data-testid"));
        Assert.Empty(cut.FindAll("[data-base-ui-focus-guard], [aria-owns]"));
    }

    [Fact(DisplayName = "port: the outside guards and the owner render beside the wrapper only while a non-modal, open publication exists, and never in the lite portal")]
    [Trait("Requirement", "FLT-DOM-13")]
    [Trait("Requirement", "FLT-DOM-14")]
    [Trait("Requirement", "FLT-ARIA-04")]
    [Trait("Requirement", "FLT-ARIA-05")]
    public async Task The_outside_guards_and_the_owner_render_beside_the_wrapper_only_while_a_non_modal_open_publication_exists()
    {
        IRenderedComponent<PopupTestHost> cut = test.Render(
            (builder, host) =>
            {
                builder.OpenComponent<TestPopupPortal>(0);
                builder.AddComponentParameter(1, nameof(TestPopupPortal.Lite), host.Get<bool>("lite"));
                builder.AddAttribute(2, "data-testid", "portal");
                builder.AddComponentParameter(3, nameof(TestPopupPortal.ChildContent), host.Get<bool>("published") ? Publisher(host.Get<bool>("modal"), host.Get<bool>("open")) : null);
                builder.CloseComponent();
            },
            new() { ["published"] = true, ["modal"] = false, ["open"] = true });

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("[data-base-ui-focus-guard]").Count));
        IElement wrapper = cut.Find("[data-testid=\"portal\"]").ParentElement!;
        IElement before = wrapper.PreviousElementSibling!.PreviousElementSibling!;
        IElement owner = wrapper.PreviousElementSibling!;
        IElement after = wrapper.NextElementSibling!;
        foreach (IElement guard in new[] { before, after })
        {
            Assert.Equal("SPAN", guard.TagName);
            Assert.Equal("0", guard.GetAttribute("tabindex"));
            Assert.Equal(string.Empty, guard.GetAttribute("data-base-ui-focus-guard"));
            Assert.Equal("outside", guard.GetAttribute("data-type"));
            Assert.Equal("true", guard.GetAttribute("aria-hidden"));
            Assert.Equal(VisuallyHidden.Style, guard.GetAttribute("style"));
        }

        Assert.Equal(cut.Find("[data-testid=\"portal\"]").Id, owner.GetAttribute("aria-owns"));
        Assert.Equal(VisuallyHidden.OwnerStyle, owner.GetAttribute("style"));

        await cut.Instance.SetAsync("modal", true);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-base-ui-focus-guard], [aria-owns]")));
        await cut.Instance.SetAsync("modal", false);
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("[data-base-ui-focus-guard]").Count));
        await cut.Instance.SetAsync("open", false);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-base-ui-focus-guard], [aria-owns]")));
        await cut.Instance.SetAsync("open", true);
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("[data-base-ui-focus-guard]").Count));
        await cut.Instance.SetAsync("published", false);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-base-ui-focus-guard], [aria-owns]")));
        await cut.Instance.SetAsync("published", true);
        await cut.Instance.SetAsync("lite", true);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-base-ui-focus-guard], [aria-owns]")));
    }

    [Fact(DisplayName = "port: the node's style is frozen from its first render, and the consumer's later style reaches the module")]
    [Trait("Requirement", "PSH-STR-12")]
    public async Task The_node_style_is_frozen_from_its_first_render_and_the_consumer_later_style_reaches_the_module()
    {
        IRenderedComponent<PopupTestHost> cut = test.Render(
            (builder, host) =>
            {
                builder.OpenComponent<TestPopupPortal>(0);
                builder.AddAttribute(1, "style", host.Get<string>("style"));
                builder.AddAttribute(2, "data-testid", "portal");
                builder.CloseComponent();
            },
            new() { ["style"] = "color: red;" });

        Assert.Equal("color: red; display: none", cut.Find("[data-testid=\"portal\"]").GetAttribute("style"));
        cut.WaitForAssertion(() => Assert.Single(module.Invocations["attach"]));
        Assert.Equal("""{"lite":false,"style":"color: red;"}""", module.Invocations["attach"][0].Arguments[1]);

        await cut.Instance.SetAsync("style", "color: blue");

        Assert.Equal("color: red; display: none", cut.Find("[data-testid=\"portal\"]").GetAttribute("style"));
        cut.WaitForAssertion(() => Assert.Equal("""{"lite":false,"style":"color: blue"}""", Assert.Single(handle.Invocations["update"]).Arguments[0]));
    }

    [Fact(DisplayName = "port: a pending container renders no node, and the container supplied later renders it and attaches")]
    [Trait("Requirement", "FLT-API-13")]
    [Trait("Requirement", "FLT-DOM-11")]
    public async Task A_pending_container_renders_no_node_and_the_container_supplied_later_renders_it_and_attaches()
    {
        IRenderedComponent<PopupTestHost> cut = test.Render(
            (builder, host) =>
            {
                builder.OpenComponent<TestPopupPortal>(0);
                builder.AddComponentParameter(1, nameof(TestPopupPortal.Container), host.Get<PortalContainerValue?>("container"));
                builder.AddAttribute(2, "data-testid", "portal");
                builder.AddComponentParameter(3, nameof(TestPopupPortal.ChildContent), (RenderFragment)(content => content.AddMarkupContent(0, "<p data-testid=\"content\"></p>")));
                builder.CloseComponent();
            },
            new() { ["container"] = (PortalContainerValue?)PortalContainerValue.Pending });

        Assert.Empty(cut.FindAll("[data-testid=\"portal\"], [data-testid=\"content\"]"));
        Assert.Empty(module.Invocations["attach"]);

        await cut.Instance.SetAsync("container", (PortalContainerValue?)"#root");

        Assert.NotNull(cut.Find("[data-testid=\"portal\"] > [data-testid=\"content\"]"));
        cut.WaitForAssertion(() => Assert.Equal("""{"containerSelector":"#root","lite":false}""", Assert.Single(module.Invocations["attach"]).Arguments[1]));
    }

    [Fact(DisplayName = "port: a part inside the portal awaits the move, which completes once the module attached, and a nested portal resolves to the enclosing node")]
    [Trait("Requirement", "FLT-API-13")]
    public void A_part_inside_the_portal_awaits_the_move_and_a_nested_portal_resolves_to_the_enclosing_node()
    {
        IRenderedComponent<PopupTestHost> cut = test.Render((builder, _) =>
        {
            builder.OpenComponent<TestPopupPortal>(0);
            builder.AddAttribute(1, "data-testid", "outer");
            builder.AddComponentParameter(2, nameof(TestPopupPortal.ChildContent), (RenderFragment)(content =>
            {
                content.OpenComponent<TestPopupPortal>(0);
                content.AddAttribute(1, "data-testid", "inner");
                content.CloseComponent();
            }));
            builder.CloseComponent();
        });

        IReadOnlyList<IRenderedComponent<TestPopupPortal>> portals = cut.FindComponents<TestPopupPortal>();
        cut.WaitForAssertion(() => Assert.Equal(2, module.Invocations["attach"].Count));
        Assert.True(portals[0].Instance.PortalContext.Moved.IsCompletedSuccessfully);
        Assert.True(portals[1].Instance.PortalContext.Moved.IsCompletedSuccessfully);
        JSRuntimeInvocation inner = module.Invocations["attach"][1];
        Assert.Equal("""{"containerRef":0,"lite":false}""", inner.Arguments[1]);
        Assert.Equal(portals[0].Instance.Element, Assert.Single(Assert.IsType<object[]>(inner.Arguments[3])));
    }

    private static RenderFragment Publisher(bool modal, bool open) => builder =>
    {
        builder.OpenComponent<TestPortalFocusPublisher>(0);
        builder.AddComponentParameter(1, nameof(TestPortalFocusPublisher.Modal), modal);
        builder.AddComponentParameter(2, nameof(TestPortalFocusPublisher.Open), open);
        builder.AddComponentParameter(3, nameof(TestPortalFocusPublisher.ChildContent), (RenderFragment)(content =>
            content.AddMarkupContent(0, "<div data-testid=\"floating\"><span tabindex=\"0\" data-testid=\"inside\"></span></div>")));
        builder.CloseComponent();
    };

    // The reference button opens the popup, whose focus manager (the test publisher) then publishes to the portal.
    private IRenderedComponent<PopupTestHost> RenderOpenable(string? ownerRole) => test.Render((builder, host) =>
    {
        builder.OpenElement(0, "button");
        builder.AddAttribute(1, "data-testid", "reference");
        builder.AddAttribute(2, "onclick", EventCallback.Factory.Create(host, () => host.SetAsync("open", true)));
        builder.CloseElement();
        builder.OpenComponent<TestPopupPortal>(3);
        builder.AddComponentParameter(4, nameof(TestPopupPortal.PortalOwnerRole), ownerRole);
        builder.AddComponentParameter(5, nameof(TestPopupPortal.ChildContent), host.Get<bool>("open") ? Publisher(modal: false, open: true) : null);
        builder.CloseComponent();
    });

    // Issue 134 (design point 13.11 of M4 track 6, settled by the orchestrator 2026-10-28): a popup closed while the portal
    // module loads is gone when `attach` reaches the browser; the module's JSException would end the Server circuit from
    // OnAfterRenderAsync (the CollapsiblePanelBehavior and FocusManagerBehavior precedent).
    [Fact(DisplayName = "port: an attach that fails because the portal node left the document before it arrived is dropped, and a later render attaches again")]
    [Trait("Requirement", "PSH-STR-11")]
    public async Task An_attach_that_fails_because_the_portal_node_left_is_dropped_and_a_later_render_attaches_again()
    {
        GoneElementRuntime runtime = new();
        await using PopupPortalBehavior behavior = new(runtime, () => { });

        await behavior.OnAfterRenderAsync(new ElementReference("gone"), null);
        await behavior.OnAfterRenderAsync(new ElementReference("gone"), null);

        Assert.Equal(2, runtime.Attaches);
        Assert.False(behavior.Context.Moved.IsCompleted);
    }

    /// <summary>A runtime whose module's <c>attach</c> fails as the module does for an element no longer in the document.</summary>
    /// <remarks>BL0016 asks callers of JS interop to guard the call; this is the runtime itself, which only answers.</remarks>
#pragma warning disable BL0016
    private sealed class GoneElementRuntime : IJSRuntime, IJSObjectReference
    {
        public int Attaches { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => Answer<TValue>(identifier);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => Answer<TValue>(identifier);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private ValueTask<TValue> Answer<TValue>(string identifier)
        {
            if (identifier == "import")
            {
                return ValueTask.FromResult((TValue)(object)this);
            }

            Attaches++;
            return ValueTask.FromException<TValue>(new JSException("Blazix: no element for the reference"));
        }
    }
#pragma warning restore BL0016
}
