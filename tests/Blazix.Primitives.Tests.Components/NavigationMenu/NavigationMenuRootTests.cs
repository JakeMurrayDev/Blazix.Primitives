using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: <c>NavigationMenuRoot.test.tsx</c>'s, <c>NavigationMenuTrigger.test.tsx</c>'s, <c>NavigationMenuLink.test.tsx</c>'s and
/// the part suites' titles bUnit observes. A press, a hover or a dismissal is the <c>interactions</c> module's report on
/// Server, an activation the <c>navigation-menu</c> module's, the popup's exit the <c>transitions</c> module's; the tests
/// play them through the twins.
/// </summary>
public sealed class NavigationMenuRootTests : IDisposable
{
    private readonly NavigationMenuTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "does not apply aria-orientation to the top-level list or root element")]
    [Trait("Requirement", "NAV-DOM-02")]
    [Trait("Requirement", "NAV-DOM-04")]
    [Trait("Requirement", "NAV-ARIA-07")]
    public void Does_not_apply_aria_orientation_to_the_top_level_list_or_root_element()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render(p => p.Add(x => x.Orientation, Blazix.Primitives.Orientation.Vertical));

        IElement root = cut.Find("[data-testid=\"root\"]");
        IElement list = cut.Find("[data-testid=\"list\"]");
        Assert.Equal("NAV", root.TagName);
        Assert.Equal("UL", list.TagName);
        Assert.False(root.HasAttribute("aria-orientation"));
        Assert.False(root.HasAttribute("role"));
        Assert.False(list.HasAttribute("aria-orientation"));
        Assert.False(list.HasAttribute("role"));
    }

    [Fact(DisplayName = "does not apply aria-orientation to nested lists or root elements")]
    [Trait("Requirement", "NAV-STR-01")]
    [Trait("Requirement", "NAV-STA-04")]
    [Trait("Requirement", "NAV-ARIA-07")]
    public async Task Does_not_apply_aria_orientation_to_nested_lists_or_root_elements()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render(p => p.Add(x => x.Nested, true));

        await test.PressAsync(cut, "trigger-item-1", open: true);

        IElement nested = cut.Find("[data-testid=\"nested-root\"]");
        Assert.Equal("DIV", nested.TagName);
        Assert.True(nested.HasAttribute("data-nested"));
        Assert.False(nested.HasAttribute("aria-orientation"));
        Assert.False(cut.Find("[data-testid=\"nested-list\"]").HasAttribute("aria-orientation"));
        Assert.False(cut.Find("[data-testid=\"root\"]").HasAttribute("data-nested"));
    }

    [Fact(DisplayName = "should be controlled by value prop")]
    [Trait("Requirement", "NAV-API-01")]
    [Trait("Requirement", "NAV-STA-01")]
    public async Task Should_be_controlled_by_value_prop()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render(p => p.Add(x => x.Controlled, true).Add(x => x.Value, "item-1"));

        Assert.Equal("true", Expanded(cut, "trigger-item-1"));
        Assert.Equal("false", Expanded(cut, "trigger-item-2"));

        await cut.Instance.SetValueAsync("item-2");
        Assert.Equal("false", Expanded(cut, "trigger-item-1"));
        Assert.Equal("true", Expanded(cut, "trigger-item-2"));

        await cut.Instance.SetValueAsync(null);
        Assert.Equal("false", Expanded(cut, "trigger-item-2"));
        Assert.Empty(cut.Instance.Changes);
    }

    [Fact(DisplayName = "should respect defaultValue")]
    [Trait("Requirement", "NAV-API-02")]
    [Trait("Requirement", "NAV-STA-01")]
    public void Should_respect_defaultValue()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render(p => p.Add(x => x.DefaultValue, "item-2"));

        Assert.Equal("false", Expanded(cut, "trigger-item-1"));
        Assert.Equal("true", Expanded(cut, "trigger-item-2"));
        Assert.NotNull(cut.Find("[data-testid=\"content-item-2\"]"));
    }

    [Fact(DisplayName = "opens on click with mouse input")]
    [Trait("Requirement", "NAV-API-03")]
    [Trait("Requirement", "NAV-DOM-11")]
    public async Task Opens_on_click_with_mouse_input()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render();
        Assert.Equal("false", Expanded(cut, "trigger-item-1"));

        await test.PressAsync(cut, "trigger-item-1", open: true);

        Assert.Equal("true", Expanded(cut, "trigger-item-1"));
        Assert.NotNull(cut.Find("[data-testid=\"content-item-1\"]"));
        (string? value, OpenChangeEventDetails details) = Assert.Single(cut.Instance.Changes);
        Assert.Equal("item-1", value);
        Assert.Equal(EventReason.TriggerPress, details.Reason);
    }

    [Fact(DisplayName = "should call onValueChange when value changes")]
    [Trait("Requirement", "NAV-EVT-01")]
    [Trait("Requirement", "NAV-EVT-06")]
    [Trait("Requirement", "NAV-STA-02")]
    public async Task Should_call_onValueChange_when_value_changes()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render();

        // Each click is two requests in Base UI and here: the click role's press and the module's activation.
        await test.PressAsync(cut, "trigger-item-1", open: true);
        await NavigationMenuTestContext.ActivateAsync(cut, "trigger-item-1", "trigger-press", eventJson: NavigationMenuTestContext.Click);
        await test.PressAsync(cut, "trigger-item-2", open: true);
        await NavigationMenuTestContext.ActivateAsync(cut, "trigger-item-2", "trigger-press", eventJson: NavigationMenuTestContext.Click);

        Assert.Equal(["item-1", "item-2"], cut.Instance.Changes.Select(change => change.Value));
        Assert.Equal("true", Expanded(cut, "trigger-item-2"));
    }

    [Fact(DisplayName = "does not open when onValueChange cancels the interaction")]
    [Trait("Requirement", "NAV-EVT-04")]
    public async Task Does_not_open_when_onValueChange_cancels_the_interaction()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render(p => p.Add(x => x.Cancel, true));

        await test.PressAsync(cut, "trigger-item-1", open: true);

        Assert.Single(cut.Instance.Changes);
        Assert.Equal("false", Expanded(cut, "trigger-item-1"));
        Assert.Empty(cut.FindAll("[data-testid=\"content-item-1\"]"));
    }

    [Fact(DisplayName = "treats a falsy item value (\"\") as a valid open value")]
    [Trait("Requirement", "NAV-API-11")]
    [Trait("Requirement", "NAV-STA-03")]
    public async Task Treats_a_falsy_item_value_as_a_valid_open_value()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render(p => p.Add(x => x.Values, new[] { string.Empty, "item-2" }));

        await test.PressAsync(cut, "trigger-", open: true);

        Assert.Equal("true", Expanded(cut, "trigger-"));
        Assert.Equal(string.Empty, Assert.Single(cut.Instance.Changes).Value);
    }

    [Fact(DisplayName = "treats a falsy item value (0) as a valid open value under int?")]
    [Trait("Requirement", "NAV-STA-03")]
    public void Treats_a_falsy_item_value_0_as_a_valid_open_value_under_a_nullable_int()
    {
        IRenderedComponent<NavigationMenuRoot<int?>> cut = test.Context.Render<NavigationMenuRoot<int?>>(p => p
            .Add(x => x.DefaultValue, 0)
            .Add(x => x.ChildContent, (RenderFragment)(builder =>
            {
                builder.OpenComponent<NavigationMenuList>(0);
                builder.AddComponentParameter(1, nameof(NavigationMenuList.ChildContent), (RenderFragment)(list =>
                {
                    list.OpenComponent<NavigationMenuItem<int?>>(0);
                    list.AddComponentParameter(1, nameof(NavigationMenuItem<int?>.Value), (int?)0);
                    list.AddComponentParameter(2, nameof(NavigationMenuItem<int?>.ChildContent), (RenderFragment)(item =>
                    {
                        item.OpenComponent<NavigationMenuTrigger>(0);
                        item.AddAttribute(1, "data-testid", "trigger-0");
                        item.CloseComponent();
                    }));
                    list.CloseComponent();
                }));
                builder.CloseComponent();
            })));

        Assert.Equal("true", cut.Find("[data-testid=\"trigger-0\"]").GetAttribute("aria-expanded"));
    }

    [Fact(DisplayName = "a non-nullable value type has no closed state, an error at the first parameter set")]
    [Trait("Requirement", "NAV-STA-02")]
    public void A_non_nullable_value_type_has_no_closed_state()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => test.Context.Render<NavigationMenuRoot<int>>());

        Assert.StartsWith("Blazix: `NavigationMenuRoot` needs a `TValue` that can be `null`", error.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "handles focus and positioner height (items without value take distinct generated identities)")]
    [Trait("Requirement", "NAV-STA-10")]
    [Trait("Requirement", "NAV-API-11")]
    public async Task Items_without_a_value_take_distinct_generated_identities()
    {
        IRenderedComponent<NavigationMenuRoot<string>> cut = test.Context.Render<NavigationMenuRoot<string>>(p => p.Add(x => x.ChildContent, (RenderFragment)(builder =>
        {
            builder.OpenComponent<NavigationMenuList>(0);
            builder.AddComponentParameter(1, nameof(NavigationMenuList.ChildContent), (RenderFragment)(list =>
            {
                for (int i = 1; i <= 2; i++)
                {
                    list.OpenComponent<NavigationMenuItem<string>>(0);
                    list.SetKey(i);
                    int index = i;
                    list.AddComponentParameter(1, nameof(NavigationMenuItem<string>.ChildContent), (RenderFragment)(item =>
                    {
                        item.OpenComponent<NavigationMenuTrigger>(0);
                        item.AddAttribute(1, "data-testid", "trigger-" + index);
                        item.CloseComponent();
                    }));
                    list.CloseComponent();
                }
            }));
            builder.CloseComponent();
        })));
        string triggerId = cut.Find("[data-testid=\"trigger-1\"]").Id!;
        string json = $$"""{"open":true,"reason":"trigger-press","triggerId":"{{triggerId}}","interactionType":"mouse","event":{{NavigationMenuTestContext.Click}}}""";
        InteractionsTwin twin = ((Microsoft.JSInterop.DotNetObjectReference<InteractionsTwin>)test.Interactions.Invocations["attach"]
            .Last(attach => attach.Arguments[1] is string options && options.Contains(triggerId, StringComparison.Ordinal)).Arguments[2]!).Value;

        await cut.InvokeAsync(() => twin.OnPress(json));

        Assert.Equal("true", cut.Find("[data-testid=\"trigger-1\"]").GetAttribute("aria-expanded"));
        Assert.Equal("false", cut.Find("[data-testid=\"trigger-2\"]").GetAttribute("aria-expanded"));
    }

    [Fact(DisplayName = "an item without a value under a TValue other than string is an error")]
    [Trait("Requirement", "NAV-API-11")]
    public void An_item_without_a_value_under_a_TValue_other_than_string_is_an_error()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => test.Context.Render<NavigationMenuRoot<int?>>(p => p.Add(x => x.ChildContent, (RenderFragment)(builder =>
        {
            builder.OpenComponent<NavigationMenuItem<int?>>(0);
            builder.CloseComponent();
        }))));

        Assert.StartsWith("Blazix: A `NavigationMenuItem` without a `Value` is identified by a generated id", error.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "adds popup-open data attributes to the active icon")]
    [Trait("Requirement", "NAV-STA-11")]
    [Trait("Requirement", "NAV-DOM-07")]
    [Trait("Requirement", "NAV-DOM-13")]
    public async Task Adds_popup_open_data_attributes_to_the_active_icon()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render();

        await test.PressAsync(cut, "trigger-item-1", open: true);

        IElement trigger = cut.Find("[data-testid=\"trigger-item-1\"]");
        IElement icon = cut.Find("[data-testid=\"icon-item-1\"]");
        Assert.True(trigger.HasAttribute("data-popup-open"));
        Assert.True(trigger.HasAttribute("data-pressed"));
        Assert.True(icon.HasAttribute("data-popup-open"));
        Assert.False(icon.HasAttribute("data-pressed"));
        Assert.Equal("true", icon.GetAttribute("aria-hidden"));
        Assert.Equal("▼", icon.TextContent);
        Assert.False(cut.Find("[data-testid=\"icon-item-2\"]").HasAttribute("data-popup-open"));
        Assert.False(cut.Find("[data-testid=\"trigger-item-2\"]").HasAttribute("data-popup-open"));
    }

    [Fact(DisplayName = "applies the data-disabled style hook only when disabled")]
    [Trait("Requirement", "NAV-DOM-08")]
    [Trait("Requirement", "NAV-DOM-06")]
    public void Applies_the_data_disabled_style_hook_only_when_disabled()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render(p => p.Add(x => x.DisabledValue, "item-1"));

        IElement disabled = cut.Find("[data-testid=\"trigger-item-1\"]");
        Assert.True(disabled.HasAttribute("data-disabled"));
        Assert.Equal("true", disabled.GetAttribute("aria-disabled"));
        Assert.False(disabled.HasAttribute("disabled"));
        Assert.Equal("0", disabled.GetAttribute("tabindex"));
        Assert.False(cut.Find("[data-testid=\"trigger-item-2\"]").HasAttribute("data-disabled"));
        Assert.Equal("0", cut.Find("[data-testid=\"trigger-item-2\"]").GetAttribute("tabindex"));
    }

    [Fact(DisplayName = "does not close menu when clicking a different trigger with mouse")]
    [Trait("Requirement", "NAV-DOM-09")]
    [Trait("Requirement", "NAV-ARIA-01")]
    public async Task Does_not_close_menu_when_clicking_a_different_trigger_with_mouse()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render();

        await test.PressAsync(cut, "trigger-item-1", open: true);
        await test.PressAsync(cut, "trigger-item-2", open: true);

        Assert.Equal("false", Expanded(cut, "trigger-item-1"));
        Assert.Equal("true", Expanded(cut, "trigger-item-2"));
        Assert.True(cut.Find("[data-testid=\"trigger-item-1\"]").HasAttribute("data-base-ui-navigation-menu-trigger"));
        Assert.Contains("\"outsidePressExemptSelector\":\"[data-base-ui-navigation-menu-trigger]\"", test.LastSessionOptions(), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "returns focus to the trigger when no viewport focus guard is rendered (the guards and the owner after the active trigger)")]
    [Trait("Requirement", "NAV-DOM-10")]
    [Trait("Requirement", "NAV-STR-04")]
    [Trait("Requirement", "NAV-ARIA-06")]
    public async Task Renders_the_guards_and_the_owner_after_the_active_trigger()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render();

        await test.PressAsync(cut, "trigger-item-1", open: true);

        IElement trigger = cut.Find("[data-testid=\"trigger-item-1\"]");
        IElement first = trigger.NextElementSibling!;
        IElement owner = first.NextElementSibling!;
        IElement second = owner.NextElementSibling!;
        Assert.True(first.HasAttribute("data-base-ui-focus-guard"));
        Assert.Equal("0", first.GetAttribute("tabindex"));
        Assert.Equal("true", first.GetAttribute("aria-hidden"));
        Assert.Equal(cut.Find("[data-testid=\"viewport\"]").Id, owner.GetAttribute("aria-owns"));
        Assert.True(second.HasAttribute("data-base-ui-focus-guard"));
        Assert.Equal(cut.Find("[data-testid=\"popup\"]").Id, trigger.GetAttribute("aria-controls"));
        IElement inactive = cut.Find("[data-testid=\"trigger-item-2\"]");
        Assert.False(inactive.HasAttribute("aria-controls"));
        Assert.False(inactive.NextElementSibling?.HasAttribute("data-base-ui-focus-guard") ?? false);
    }

    [Fact(DisplayName = "preserves popup size when controlled value closes externally (the content's open state, no callback)")]
    [Trait("Requirement", "NAV-DOM-14")]
    [Trait("Requirement", "NAV-EVT-11")]
    public async Task Preserves_popup_size_when_controlled_value_closes_externally()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render(p => p.Add(x => x.Controlled, true).Add(x => x.Value, "item-1"));
        IElement content = cut.Find("[data-testid=\"content-item-1\"]");
        Assert.True(content.HasAttribute("data-open"));

        await cut.Instance.SetValueAsync(null);

        Assert.True(cut.Find("[data-testid=\"content-item-1\"]").HasAttribute("data-closed"));
        Assert.Empty(cut.Instance.Changes);
    }

    [Fact(DisplayName = "keeps content mounted inside the popup when closed if the portal is kept mounted")]
    [Trait("Requirement", "NAV-API-15")]
    [Trait("Requirement", "NAV-DOM-16")]
    public async Task Keeps_content_mounted_inside_the_popup_when_closed_if_the_portal_is_kept_mounted()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render(p => p.Add(x => x.KeepMountedPortal, true).Add(x => x.KeepMountedContent, true));

        await test.PressAsync(cut, "trigger-item-1", open: true);
        await test.SessionRequestAsync(cut, open: false, "escape-key");
        await NavigationMenuTestContext.CompleteExitAsync(cut);

        Assert.NotNull(cut.Find("[data-testid=\"portal\"]"));
        Assert.True(cut.Find("[data-testid=\"content-item-1\"]").HasAttribute("hidden"));
    }

    [Theory(DisplayName = "reports %s activation direction when switching in either direction")]
    [InlineData("right")]
    [InlineData("left")]
    [Trait("Requirement", "NAV-DOM-18")]
    [Trait("Requirement", "NAV-STA-06")]
    public async Task Reports_activation_direction_when_switching_in_either_direction(string direction)
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render();
        await test.PressAsync(cut, "trigger-item-1", open: true);

        await NavigationMenuTestContext.ActivateAsync(cut, "trigger-item-2", "trigger-hover", direction);

        Assert.Equal(direction, cut.Find("[data-testid=\"content-item-2\"]").GetAttribute("data-activation-direction"));
        Assert.Equal("item-2", cut.Instance.Changes[^1].Value);
        Assert.Equal(EventReason.TriggerHover, cut.Instance.Changes[^1].Details.Reason);
    }

    [Fact(DisplayName = "opens with list-navigation when the open key activates a trigger")]
    [Trait("Requirement", "NAV-EVT-02")]
    [Trait("Requirement", "NAV-KEY-01")]
    public async Task Opens_with_list_navigation_when_the_open_key_activates_a_trigger()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render();

        await NavigationMenuTestContext.ActivateAsync(cut, "trigger-item-1", "list-navigation", eventJson: NavigationMenuTestContext.Key("ArrowDown"));

        (string? value, OpenChangeEventDetails details) = Assert.Single(cut.Instance.Changes);
        Assert.Equal("item-1", value);
        Assert.Equal(EventReason.ListNavigation, details.Reason);
        Assert.Equal("true", Expanded(cut, "trigger-item-1"));
    }

    [Fact(DisplayName = "clears activation direction when controlled value closes externally after switching triggers")]
    [Trait("Requirement", "NAV-DOM-18")]
    public async Task Clears_activation_direction_when_controlled_value_closes_externally_after_switching_triggers()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render(p => p.Add(x => x.Controlled, true).Add(x => x.Value, "item-1"));
        await NavigationMenuTestContext.ActivateAsync(cut, "trigger-item-2", "trigger-hover", "right");
        Assert.Equal("right", cut.Find("[data-testid=\"content-item-2\"]").GetAttribute("data-activation-direction"));

        await cut.Instance.SetValueAsync(null);

        Assert.False(cut.Find("[data-testid=\"content-item-2\"]").HasAttribute("data-activation-direction"));
    }

    [Fact(DisplayName = "when `true`, renders with aria-current=\"page\"")]
    [Trait("Requirement", "NAV-API-13")]
    [Trait("Requirement", "NAV-ARIA-04")]
    [Trait("Requirement", "NAV-DOM-20")]
    public async Task When_true_renders_with_aria_current_page()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render(p => p.Add(x => x.ActiveLink, "item-1"));
        await test.PressAsync(cut, "trigger-item-1", open: true);

        IElement link = cut.Find("[data-testid=\"link-item-1\"]");
        Assert.Equal("page", link.GetAttribute("aria-current"));
        Assert.True(link.HasAttribute("data-active"));
    }

    [Fact(DisplayName = "when `false`, does not render with aria-current=\"page\"")]
    [Trait("Requirement", "NAV-API-13")]
    [Trait("Requirement", "NAV-ARIA-04")]
    public async Task When_false_does_not_render_with_aria_current_page()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render();
        await test.PressAsync(cut, "trigger-item-1", open: true);

        IElement link = cut.Find("[data-testid=\"link-item-1\"]");
        Assert.Equal("A", link.TagName);
        Assert.False(link.HasAttribute("aria-current"));
        Assert.False(link.HasAttribute("data-active"));
    }

    [Fact(DisplayName = "closes the menu when clicking a link when true")]
    [Trait("Requirement", "NAV-API-14")]
    [Trait("Requirement", "NAV-DIS-09")]
    public async Task Closes_the_menu_when_clicking_a_link_when_true()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render(p => p.Add(x => x.CloseOnClick, true));
        await test.PressAsync(cut, "trigger-item-1", open: true);

        await cut.InvokeAsync(() => cut.Find("[data-testid=\"link-item-1\"]").Click());

        (string? value, OpenChangeEventDetails details) = cut.Instance.Changes[^1];
        Assert.Null(value);
        Assert.Equal(EventReason.LinkPress, details.Reason);
        Assert.Equal("false", Expanded(cut, "trigger-item-1"));
    }

    [Fact(DisplayName = "does not close the menu when clicking a link when false")]
    [Trait("Requirement", "NAV-API-14")]
    public async Task Does_not_close_the_menu_when_clicking_a_link_when_false()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render();
        await test.PressAsync(cut, "trigger-item-1", open: true);

        await cut.InvokeAsync(() => cut.Find("[data-testid=\"link-item-1\"]").Click());

        Assert.Single(cut.Instance.Changes);
        Assert.Equal("true", Expanded(cut, "trigger-item-1"));
    }

    [Fact(DisplayName = "calls onValueChange on the parent root when nested closeOnClick link is clicked")]
    [Trait("Requirement", "NAV-EVT-10")]
    [Trait("Requirement", "NAV-DIS-09")]
    public async Task Calls_onValueChange_on_the_parent_root_when_nested_closeOnClick_link_is_clicked()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render(p => p.Add(x => x.Nested, true).Add(x => x.CloseOnClick, true).Add(x => x.NestedDefaultValue, "nested-1"));
        await test.PressAsync(cut, "trigger-item-1", open: true);

        await cut.InvokeAsync(() => cut.Find("[data-testid=\"nested-link-1\"]").Click());

        (string? nestedValue, OpenChangeEventDetails nestedDetails) = Assert.Single(cut.Instance.NestedChanges);
        Assert.Null(nestedValue);
        Assert.Equal(EventReason.LinkPress, nestedDetails.Reason);
        (string? parentValue, OpenChangeEventDetails parentDetails) = cut.Instance.Changes[^1];
        Assert.Null(parentValue);
        Assert.Same(nestedDetails, parentDetails);
    }

    [Fact(DisplayName = "closes through the `close` action so `onValueChange` can opt out")]
    [Trait("Requirement", "NAV-EVT-01")]
    [Trait("Requirement", "NAV-EVT-08")]
    [Trait("Requirement", "EVD-EVT-22")]
    public async Task Closes_through_the_close_action_so_onValueChange_can_opt_out()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render(p => p.Add(x => x.PreventUnmount, true));
        await test.PressAsync(cut, "trigger-item-1", open: true);

        await cut.Instance.Root!.CloseAsync();

        (string? value, OpenChangeEventDetails details) = cut.Instance.Changes[^1];
        Assert.Null(value);
        Assert.Equal(EventReason.ImperativeAction, details.Reason);
        Assert.True(cut.Instance.Root.Store.Lifecycle.PreventUnmountingOnClose);
    }

    [Fact(DisplayName = "keeps the popup mounted until the unmount action completes closing")]
    [Trait("Requirement", "NAV-EVT-05")]
    [Trait("Requirement", "NAV-EVT-11")]
    public async Task Keeps_the_popup_mounted_until_the_unmount_action_completes_closing()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render(p => p.Add(x => x.PreventUnmount, true));
        await test.PressAsync(cut, "trigger-item-1", open: true);
        await test.SessionRequestAsync(cut, open: false, "escape-key");

        await NavigationMenuTestContext.CompleteExitAsync(cut);
        Assert.NotNull(cut.Find("[data-testid=\"popup\"]"));
        Assert.Empty(cut.Instance.Completions);

        await cut.InvokeAsync(() => cut.Instance.Root!.Unmount());
        Assert.Empty(cut.FindAll("[data-testid=\"popup\"]"));
        Assert.Equal([false], cut.Instance.Completions);
    }

    [Fact(DisplayName = "port: the Arrow joins the Root's navigation-menu handle, which suppresses its transitions while the popup starts")]
    [Trait("Requirement", "NAV-ANI-15")]
    public void The_Arrow_joins_the_Roots_navigation_menu_handle()
    {
        test.Render(p => p.Add(x => x.WithArrow, true).Add(x => x.DefaultValue, "item-1"));

        Assert.Single(test.NavigationMenu.Invocations["attach"], invocation => invocation.Arguments[1] is string options && options.Contains("\"part\":\"arrow\"", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "port: keeps a controlled close's opt-out for the value parameter's close")]
    [Trait("Requirement", "NAV-STA-05")]
    [Trait("Requirement", "NAV-EVT-05")]
    public async Task Keeps_a_controlled_closes_opt_out_for_the_value_parameters_close()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render(p => p
            .Add(x => x.Controlled, true).Add(x => x.Value, "item-1").Add(x => x.FollowControlled, false).Add(x => x.PreventUnmount, true));

        // The close action runs onValueChange, which opts out; the page keeps its value, so the menu stays open.
        await cut.Instance.Root!.CloseAsync();
        Assert.Equal("true", Expanded(cut, "trigger-item-1"));

        await cut.Instance.SetValueAsync(null);
        await NavigationMenuTestContext.CompleteExitAsync(cut);
        Assert.NotNull(cut.Find("[data-testid=\"popup\"]"));
        Assert.Empty(cut.Instance.Completions);

        await cut.InvokeAsync(() => cut.Instance.Root!.Unmount());
        Assert.Empty(cut.FindAll("[data-testid=\"popup\"]"));
        Assert.Equal([false], cut.Instance.Completions);
    }

    [Fact(DisplayName = "keeps the opt-out when the `close` action is called again while closed")]
    [Trait("Requirement", "NAV-EVT-05")]
    [Trait("Requirement", "NAV-EVT-08")]
    public async Task Keeps_the_opt_out_when_the_close_action_is_called_again_while_closed()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render(p => p.Add(x => x.PreventUnmount, true));
        await test.PressAsync(cut, "trigger-item-1", open: true);
        await cut.Instance.Root!.CloseAsync();
        int calls = cut.Instance.Changes.Count;

        await cut.Instance.Root.CloseAsync();
        await NavigationMenuTestContext.CompleteExitAsync(cut);

        Assert.Equal(calls, cut.Instance.Changes.Count);
        Assert.NotNull(cut.Find("[data-testid=\"popup\"]"));
    }

    [Fact(DisplayName = "ignores `unmount` while the popup is open")]
    [Trait("Requirement", "NAV-EVT-09")]
    public async Task Ignores_unmount_while_the_popup_is_open()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render();
        await test.PressAsync(cut, "trigger-item-1", open: true);

        await cut.InvokeAsync(() => cut.Instance.Root!.Unmount());

        Assert.NotNull(cut.Find("[data-testid=\"popup\"]"));
        Assert.Empty(cut.Instance.Completions);
    }

    [Fact(DisplayName = "still unmounts on a later close after `unmount` was called while open")]
    [Trait("Requirement", "NAV-EVT-09")]
    public async Task Still_unmounts_on_a_later_close_after_unmount_was_called_while_open()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render();
        await test.PressAsync(cut, "trigger-item-1", open: true);
        await cut.InvokeAsync(() => cut.Instance.Root!.Unmount());

        await test.SessionRequestAsync(cut, open: false, "escape-key");
        await NavigationMenuTestContext.CompleteExitAsync(cut);

        Assert.Empty(cut.FindAll("[data-testid=\"popup\"]"));
        Assert.Equal([false], cut.Instance.Completions);
    }

    [Fact(DisplayName = "unmounts immediately when called while the popup is closing")]
    [Trait("Requirement", "NAV-EVT-09")]
    [Trait("Requirement", "TRN-ANI-22")]
    public async Task Unmounts_immediately_when_called_while_the_popup_is_closing()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render();
        await test.PressAsync(cut, "trigger-item-1", open: true);
        await test.SessionRequestAsync(cut, open: false, "escape-key");
        Assert.NotNull(cut.Find("[data-testid=\"popup\"]"));

        await cut.InvokeAsync(() => cut.Instance.Root!.Unmount());
        Assert.Empty(cut.FindAll("[data-testid=\"popup\"]"));

        // The exit's own completion afterwards fires no second completion.
        await NavigationMenuTestContext.CompleteExitAsync(cut);
        Assert.Equal([false], cut.Instance.Completions);
    }

    [Fact(DisplayName = "automatically unmounts with an actions ref and completes closing once")]
    [Trait("Requirement", "NAV-API-04")]
    [Trait("Requirement", "NAV-API-05")]
    [Trait("Requirement", "NAV-ANI-01")]
    public async Task Automatically_unmounts_and_completes_closing_once()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render();
        await test.PressAsync(cut, "trigger-item-1", open: true);
        Assert.NotNull(cut.Find("[data-testid=\"popup\"]"));

        await test.SessionRequestAsync(cut, open: false, "escape-key");
        await NavigationMenuTestContext.CompleteExitAsync(cut);

        Assert.Empty(cut.FindAll("[data-testid=\"popup\"]"));
        Assert.Equal([false], cut.Instance.Completions);
    }

    [Fact(DisplayName = "handles inline nested menu without positioner/popup correctly")]
    [Trait("Requirement", "NAV-STA-08")]
    [Trait("Requirement", "NAV-DIS-08")]
    public async Task Handles_inline_nested_menu_without_positioner_or_popup_correctly()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render(p => p.Add(x => x.Nested, true).Add(x => x.NestedDefaultValue, "nested-1"));

        await test.PressAsync(cut, "trigger-item-1", open: true);

        Assert.Equal("true", cut.Find("[data-testid=\"nested-trigger-1\"]").GetAttribute("aria-expanded"));
        Assert.NotNull(cut.Find("[data-testid=\"nested-content-1\"]"));

        // The nested Root has no Positioner: its trigger renders no aria-controls (§16 Q3) and it has no dismissal (§16 Q4).
        Assert.False(cut.Find("[data-testid=\"nested-trigger-1\"]").HasAttribute("aria-controls"));
    }

    [Fact(DisplayName = "renders viewport content correctly for inline nested menu")]
    [Trait("Requirement", "NAV-DOM-34")]
    [Trait("Requirement", "NAV-STR-12")]
    public async Task Renders_viewport_content_correctly_for_inline_nested_menu()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render(p => p.Add(x => x.Nested, true).Add(x => x.NestedDefaultValue, "nested-1"));

        await test.PressAsync(cut, "trigger-item-1", open: true);

        IElement viewport = cut.Find("[data-testid=\"nested-viewport\"]");
        Assert.Equal(3, viewport.Children.Length);
        Assert.True(viewport.Children[0].HasAttribute("data-base-ui-focus-guard"));
        Assert.Equal("DIV", viewport.Children[1].TagName);
        Assert.True(viewport.Children[2].HasAttribute("data-base-ui-focus-guard"));
        IElement top = cut.Find("[data-testid=\"viewport\"]");
        Assert.True(top.PreviousElementSibling!.HasAttribute("data-base-ui-focus-guard"));
        Assert.True(top.NextElementSibling!.HasAttribute("data-base-ui-focus-guard"));
    }

    [Fact(DisplayName = "suppresses the positioner transition for the first frame while initially open")]
    [Trait("Requirement", "NAV-ANI-13")]
    [Trait("Requirement", "NAV-DOM-28")]
    public void Suppresses_the_positioner_transition_for_the_first_frame_while_initially_open()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render(p => p.Add(x => x.DefaultValue, "item-1"));

        cut.WaitForAssertion(() => Assert.False(cut.Find("[data-testid=\"positioner\"]").HasAttribute("data-instant")));
        Assert.DoesNotContain(test.Render().FindAll("[data-testid=\"positioner\"]"), positioner => positioner.HasAttribute("data-instant"));
    }

    [Fact(DisplayName = "updates popup sizing when the window is resized while the popup is open (data-instant follows the resize report)")]
    [Trait("Requirement", "NAV-DOM-28")]
    public async Task Data_instant_follows_the_resize_report()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render();
        await test.PressAsync(cut, "trigger-item-1", open: true);
        NavigationMenuPositioner positioner = cut.FindComponent<NavigationMenuPositioner>().Instance;

        await cut.InvokeAsync(() => positioner.ModuleTwin.OnResize("{}"));

        Assert.True(cut.Find("[data-testid=\"positioner\"]").HasAttribute("data-instant"));
        cut.WaitForAssertion(() => Assert.False(cut.Find("[data-testid=\"positioner\"]").HasAttribute("data-instant")), TimeSpan.FromSeconds(2));
    }

    // Open to track 2 (issue 163's list): PositionerInputs.Shift reaches the positioning options once track 2 wires it.
    [Fact(DisplayName = "uses the layout viewport", Skip = "Open to M5 track 2: the Positioner's shift input (NAV-POS-03)")]
    [Trait("Requirement", "NAV-POS-03")]
    public async Task Uses_the_layout_viewport()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render();

        await test.PressAsync(cut, "trigger-item-1", open: true);

        string positioning = string.Join('\n', test.Context.JSInterop.Invocations.Select(invocation => string.Join(' ', invocation.Arguments.OfType<string>())));
        Assert.Contains("\"rootBoundary\":\"layoutViewport\"", positioning, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "the session joins the enclosing menu's tree and enables the dismissal only while a positioner is registered or closed")]
    [Trait("Requirement", "NAV-STA-08")]
    [Trait("Requirement", "NAV-PTR-16")]
    public async Task The_session_enables_the_dismissal_only_while_a_positioner_is_registered_or_closed()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render();
        Assert.Contains("\"dismiss\":", test.LastSessionOptions(), StringComparison.Ordinal);

        await test.PressAsync(cut, "trigger-item-1", open: true);

        string session = test.LastSessionOptions();
        Assert.Contains("\"outsidePressEvent\":{\"mouse\":\"intentional\",\"touch\":\"intentional\"}", session, StringComparison.Ordinal);
        Assert.Contains("\"activeTriggerId\":\"" + cut.Find("[data-testid=\"trigger-item-1\"]").Id + "\"", session, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "the trigger's hover rests 0 ms while mounted with a positioner, and its click toggles only while active")]
    [Trait("Requirement", "NAV-PTR-02")]
    [Trait("Requirement", "NAV-PTR-05")]
    public async Task The_trigger_roles_follow_the_menu()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render(p => p.Add(x => x.Delay, new Duration(100)));
        string first = cut.Find("[data-testid=\"trigger-item-1\"]").Id!;
        Assert.Contains("\"restMs\":100", test.LastTriggerOptions(first), StringComparison.Ordinal);

        await test.PressAsync(cut, "trigger-item-1", open: true);

        string options = test.LastTriggerOptions(first);
        Assert.DoesNotContain("\"restMs\":", options, StringComparison.Ordinal);
        Assert.Contains("\"toggle\":true", options, StringComparison.Ordinal);
        Assert.Contains("\"toggle\":false", test.LastTriggerOptions(cut.Find("[data-testid=\"trigger-item-2\"]").Id!), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "the Root's module handle carries the mounted state and the close reason, and focus-out reports are answered")]
    [Trait("Requirement", "NAV-STA-15")]
    [Trait("Requirement", "NAV-FOC-10")]
    public async Task The_root_module_handle_carries_the_mounted_state_and_the_close_reason()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render();
        await test.PressAsync(cut, "trigger-item-1", open: true);
        Assert.Contains("\"mounted\":true", test.LastRootModuleOptions(), StringComparison.Ordinal);

        await NavigationMenuTestContext.FocusOutAsync(cut, "trigger-item-1");

        string options = test.LastRootModuleOptions();
        Assert.Contains("\"closeReason\":\"focus-out\"", options, StringComparison.Ordinal);
        Assert.Contains("\"answered\":1", options, StringComparison.Ordinal);
        Assert.Equal(EventReason.FocusOut, cut.Instance.Changes[^1].Details.Reason);
    }

    [Fact(DisplayName = "throws a descriptive error when rendered outside <NavigationMenu.Root>")]
    [Trait("Requirement", "NAV-EDGE-01")]
    [Trait("Requirement", "NAV-STR-02")]
    public void Throws_a_descriptive_error_when_rendered_outside_the_root()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => test.Context.Render<NavigationMenuList>());

        Assert.Equal("Blazix: The `NavigationMenuRoot` context is missing. Navigation Menu parts must be placed within `<NavigationMenuRoot>`.", error.Message);
    }

    [Fact(DisplayName = "throws a descriptive error when rendered outside <NavigationMenu.Item>")]
    [Trait("Requirement", "NAV-EDGE-02")]
    [Trait("Requirement", "NAV-STR-03")]
    public void Throws_a_descriptive_error_when_rendered_outside_the_item()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => test.Context.Render<NavigationMenuRoot<string>>(p => p.Add(x => x.ChildContent, (RenderFragment)(builder =>
        {
            builder.OpenComponent<NavigationMenuIcon>(0);
            builder.CloseComponent();
        }))));

        Assert.Equal("Blazix: `NavigationMenuItem` parts must be used within a `<NavigationMenuItem>`.", error.Message);
    }

    [Fact(DisplayName = "throws a descriptive error when rendered outside <NavigationMenu.Portal>")]
    [Trait("Requirement", "NAV-EDGE-03")]
    [Trait("Requirement", "NAV-STR-10")]
    public void Throws_a_descriptive_error_when_rendered_outside_the_portal()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => test.Context.Render<NavigationMenuRoot<string>>(p => p.Add(x => x.ChildContent, (RenderFragment)(builder =>
        {
            builder.OpenComponent<NavigationMenuPositioner>(0);
            builder.CloseComponent();
        }))));

        Assert.Equal("Blazix: `<NavigationMenuPortal>` is missing.", error.Message);
    }

    [Fact(DisplayName = "throws a descriptive error when rendered outside <NavigationMenu.Positioner>")]
    [Trait("Requirement", "NAV-EDGE-04")]
    [Trait("Requirement", "NAV-STR-13")]
    public void Throws_a_descriptive_error_when_rendered_outside_the_positioner()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => test.Context.Render<NavigationMenuRoot<string>>(p => p.Add(x => x.ChildContent, (RenderFragment)(builder =>
        {
            builder.OpenComponent<NavigationMenuArrow>(0);
            builder.CloseComponent();
        }))));

        Assert.Equal("Blazix: The `NavigationMenuPositioner` context is missing. `NavigationMenuPositioner` parts must be placed within `<NavigationMenuPositioner>`.", error.Message);
    }

    [Fact(DisplayName = "render callback receives href")]
    [Trait("Requirement", "NAV-API-18")]
    public async Task Render_callback_receives_href()
    {
        IReadOnlyDictionary<string, object>? received = null;
        IRenderedComponent<NavigationMenuRoot<string>> cut = test.Context.Render<NavigationMenuRoot<string>>(p => p.Add(x => x.ChildContent, (RenderFragment)(builder =>
        {
            builder.OpenComponent<NavigationMenuList>(0);
            builder.AddComponentParameter(1, nameof(NavigationMenuList.ChildContent), (RenderFragment)(list =>
            {
                list.OpenComponent<NavigationMenuLink>(0);
                list.AddAttribute(1, "href", "#target");
                list.AddComponentParameter(2, nameof(NavigationMenuLink.RenderFunction), (RenderFragment<RenderProps<NavigationMenuLinkState>>)(props => link =>
                {
                    received = props.Attributes;
                    link.OpenElement(0, "a");
                    link.AddMultipleAttributes(1, props.Attributes);
                    link.CloseElement();
                }));
                list.CloseComponent();
            }));
            builder.CloseComponent();
        })));
        await Task.Yield();

        Assert.Equal("#target", received!["href"]);
        Assert.Equal("#target", cut.Find("a").GetAttribute("href"));
    }

    [Fact(DisplayName = "the portal renders its node and the popup its id and tabindex")]
    [Trait("Requirement", "NAV-STR-08")]
    [Trait("Requirement", "NAV-DOM-22")]
    [Trait("Requirement", "NAV-DOM-30")]
    [Trait("Requirement", "NAV-DOM-23")]
    public async Task The_portal_renders_its_node_and_the_popup_its_id_and_tabindex()
    {
        IRenderedComponent<NavigationMenuHarness> cut = test.Render(p => p.Add(x => x.WithBackdrop, true));

        await test.PressAsync(cut, "trigger-item-1", open: true);

        IElement portal = cut.Find("[data-testid=\"portal\"]");
        Assert.True(portal.HasAttribute("data-base-ui-portal"));
        Assert.False(string.IsNullOrEmpty(portal.Id));
        IElement popup = cut.Find("[data-testid=\"popup\"]");
        Assert.Equal("NAV", popup.TagName);
        Assert.Equal("-1", popup.GetAttribute("tabindex"));
        Assert.True(popup.HasAttribute("data-open"));
        IElement backdrop = cut.Find("[data-testid=\"backdrop\"]");
        Assert.Equal("presentation", backdrop.GetAttribute("role"));
        Assert.True(backdrop.HasAttribute("data-open"));
        Assert.Contains("user-select: none", backdrop.GetAttribute("style"), StringComparison.Ordinal);
    }

    private static string? Expanded(IRenderedComponent<NavigationMenuHarness> cut, string testId) => cut.Find($"[data-testid=\"{testId}\"]").GetAttribute("aria-expanded");
}
