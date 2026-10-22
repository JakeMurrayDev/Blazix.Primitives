using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Scenario = Blazix.Primitives.Tests.Components.FieldErrorScenarios.ErrorScenario;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The <c>[L2]</c> titles of <c>FieldError.test.tsx</c> (PR 1c), and port titles for what Base UI leaves untested: the
/// message while the exit plays (FLD §16 item 8), <c>match="valid"</c> (item 3), an enclosing item's disabled state (item
/// 5), the C# half of the transition, and the messages of a cascaded <see cref="EditContext"/> (ADR 05 Amendments
/// 2026-10-22). The transitions module is the mock: an exit ends when a test reports it (<see cref="FieldTestContext.CompleteExits"/>),
/// as Base UI's jsdom tests run with animations disabled.
/// </summary>
public sealed class FieldErrorTests : IDisposable
{
    private readonly FieldTestContext host = new();

    private BunitContext Context => host.Context;

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "should set aria-describedby on the control automatically")]
    [Trait("Requirement", "FLD-API-31")]
    [Trait("Requirement", "FLD-ARIA-02")]
    [Trait("Requirement", "FLD-ARIA-05")]
    public void Should_set_aria_describedby_on_the_control_automatically()
    {
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p
            .Add(x => x.Invalid, true)
            .AddChildContent<FieldControl>()
            .AddChildContent<FieldError>(e => e.Add(x => x.Match, true).AddChildContent("Message")));

        string? errorId = cut.Find("div > div").GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(errorId));
        Assert.Equal(errorId, cut.Find("input").GetAttribute("aria-describedby"));
    }

    [Fact(DisplayName = "should show error messages by default")]
    [Trait("Requirement", "FLD-API-30")]
    [Trait("Requirement", "FLD-API-32")]
    [Trait("Requirement", "FLD-DOM-14")]
    [Trait("Requirement", "FLD-FRM-05")]
    [Trait("Requirement", "FLD-ANI-01")]
    public void Should_show_error_messages_by_default()
    {
        IRenderedComponent<FieldErrorScenarios> cut = Render(Scenario.Default);
        Assert.Empty(cut.FindAll("[data-testid=error]"));

        FormModuleStandIn.Attached control = host.Module.Control;
        control.Focused(cut);
        control.Input(cut, "a", Native.Valid);
        control.Input(cut, string.Empty, Native.ValueMissing);
        control.Blur(cut);
        Assert.Empty(cut.FindAll("[data-testid=error]"));

        host.Module.FormElement.SubmitAll(cut);
        Assert.Equal("Message", cut.Find("[data-testid=error]").TextContent);
    }

    [Fact(DisplayName = "should only render when `match` matches constraint validation")]
    [Trait("Requirement", "FLD-API-30")]
    [Trait("Requirement", "FLD-FRM-13")]
    [Trait("Requirement", "FLD-ANI-01")]
    public void Should_only_render_when_match_matches_constraint_validation()
    {
        IRenderedComponent<FieldErrorScenarios> cut = Render(Scenario.ValueMissing);
        FormModuleStandIn.Attached control = host.Module.Control;
        control.Native = Native.ValueMissing;
        Assert.Empty(Messages(cut));

        host.Module.FormElement.SubmitAll(cut);
        Assert.Equal(["Message"], Messages(cut));

        control.Focused(cut);
        control.Input(cut, "a", new NativeValidity { TooShort = true, ValidationMessage = "Please lengthen this text." });
        host.CompleteExits(cut);
        Assert.Empty(Messages(cut));

        control.Input(cut, string.Empty, Native.ValueMissing);
        Assert.Equal(["Message"], Messages(cut));
    }

    [Fact(DisplayName = "should show custom errors")]
    [Trait("Requirement", "FLD-API-30")]
    [Trait("Requirement", "FLD-FRM-07")]
    [Trait("Requirement", "FLD-EDGE-07")]
    public void Should_show_custom_errors()
    {
        IRenderedComponent<FieldErrorScenarios> cut = Render(Scenario.CustomError, p => p.Add(x => x.Validate, _ => new ValueTask<FieldValidationResult?>("error")));
        FormModuleStandIn.Attached control = host.Module.Control;

        control.Focused(cut);
        control.Input(cut, "a");
        control.Blur(cut);
        Assert.Empty(Messages(cut));

        host.Module.FormElement.SubmitAll(cut);
        Assert.Equal(["Message"], Messages(cut));
    }

    [Fact(DisplayName = "uses `match={false}` as the default slot for Form errors")]
    [Trait("Requirement", "FLD-API-30")]
    [Trait("Requirement", "FLD-DOM-15")]
    public void Uses_match_false_as_the_default_slot_for_Form_errors()
    {
        IRenderedComponent<FieldErrorScenarios> cut = Render(Scenario.DefaultSlot, p => p
            .Add(x => x.Errors, new FormErrors { ["username"] = "Username is reserved" })
            .Add(x => x.Match, false));

        Assert.Equal(["Username is reserved"], Messages(cut));
        Assert.Equal("Username is reserved", cut.Find("[data-testid=default-error]").TextContent);
    }

    [Fact(DisplayName = "uses an omitted `match` as the default slot for Form errors")]
    [Trait("Requirement", "FLD-API-30")]
    [Trait("Requirement", "FLD-DOM-15")]
    public void Uses_an_omitted_match_as_the_default_slot_for_Form_errors()
    {
        IRenderedComponent<FieldErrorScenarios> cut = Render(Scenario.DefaultSlot, p => p.Add(x => x.Errors, new FormErrors { ["username"] = "Username is reserved" }));

        Assert.Equal(["Username is reserved"], Messages(cut));
        Assert.Equal("Username is reserved", cut.Find("[data-testid=default-error]").TextContent);
    }

    [Fact(DisplayName = "uses the Field.Control name fallback for Form errors")]
    [Trait("Requirement", "FLD-API-01")]
    [Trait("Requirement", "FLD-API-18")]
    [Trait("Requirement", "FLD-DOM-15")]
    [Trait("Requirement", "FLD-ARIA-03")]
    [Trait("Requirement", "FLD-FRM-09")]
    public void Uses_the_Field_Control_name_fallback_for_Form_errors()
    {
        IRenderedComponent<FieldErrorScenarios> cut = Render(Scenario.NameFallback, p => p
            .Add(x => x.RootName, null)
            .Add(x => x.Errors, new FormErrors { ["email"] = "Email is already taken" }));

        Assert.Equal("true", cut.Find("input").GetAttribute("aria-invalid"));
        Assert.Equal("Email is already taken", cut.Find("[data-testid=default-error]").TextContent);

        host.Module.Control.Input(cut, "next@example.com");
        host.CompleteExits(cut);

        Assert.False(cut.Find("input").HasAttribute("aria-invalid"));
        Assert.Empty(cut.FindAll("[data-testid=default-error]"));
    }

    // FormErrors is a dictionary: no inherited property can be found under a field's name.
    [Fact(DisplayName = "ignores inherited Form error properties")]
    [Trait("Requirement", "FLD-STA-09")]
    [Trait("Requirement", "FLD-FRM-09")]
    public void Ignores_inherited_Form_error_properties()
    {
        IRenderedComponent<FieldErrorScenarios> cut = Render(Scenario.FormErrors, p => p.Add(x => x.RootName, "constructor").Add(x => x.Errors, new FormErrors()));

        Assert.False(cut.Find("input").HasAttribute("aria-invalid"));
        Assert.Empty(cut.FindAll("[data-testid=default-error]"));
    }

    [Fact(DisplayName = "renders Form error arrays as a list")]
    [Trait("Requirement", "FLD-DOM-16")]
    public void Renders_Form_error_arrays_as_a_list()
    {
        IRenderedComponent<FieldErrorScenarios> cut = Render(Scenario.FormErrors, p => p.Add(x => x.Errors, new FormErrors { ["username"] = new[] { "Username is reserved", "Username is too short" } }));

        AngleSharp.Dom.IElement list = cut.Find("[data-testid=default-error] > ul");
        Assert.Equal(["Username is reserved", "Username is too short"], list.QuerySelectorAll("li").Select(item => item.TextContent));
    }

    [Fact(DisplayName = "renders single-item Form error arrays as text")]
    [Trait("Requirement", "FLD-DOM-16")]
    public void Renders_single_item_Form_error_arrays_as_text()
    {
        IRenderedComponent<FieldErrorScenarios> cut = Render(Scenario.FormErrors, p => p.Add(x => x.Errors, new FormErrors { ["username"] = new[] { "Username is reserved" } }));

        Assert.Empty(cut.FindAll("[data-testid=default-error] ul"));
        Assert.Equal("Username is reserved", cut.Find("[data-testid=default-error]").TextContent);
    }

    [Fact(DisplayName = "renders client validation error arrays as a list")]
    [Trait("Requirement", "FLD-DOM-16")]
    [Trait("Requirement", "FFI-FRM-07")]
    public void Renders_client_validation_error_arrays_as_a_list()
    {
        IRenderedComponent<FieldErrorScenarios> cut = Render(Scenario.ClientErrors, p => p.Add(x => x.Validate, _ => new ValueTask<FieldValidationResult?>(new[] { "First error", "Second error" })));

        host.Module.FormElement.SubmitAll(cut);

        AngleSharp.Dom.IElement list = cut.Find("[data-testid=default-error] > ul");
        Assert.Equal(["First error", "Second error"], list.QuerySelectorAll("li").Select(item => item.TextContent));
    }

    [Fact(DisplayName = "does not register an empty error id")]
    [Trait("Requirement", "FLD-API-31")]
    [Trait("Requirement", "FLD-EDGE-03")]
    public void Does_not_register_an_empty_error_id()
    {
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p
            .Add(x => x.Invalid, true)
            .AddChildContent<FieldControl>(c => c.AddUnmatched("aria-describedby", "external-description"))
            .AddChildContent<FieldError>(e => e.AddUnmatched("id", string.Empty).AddChildContent("Message")));

        Assert.Equal("external-description", cut.Find("input").GetAttribute("aria-describedby"));
    }

    // FLD-EDGE-03: a rendered error renders the empty id verbatim and still publishes nothing.
    [Fact(DisplayName = "renders an empty id verbatim without publishing it")]
    [Trait("Requirement", "FLD-API-31")]
    [Trait("Requirement", "FLD-EDGE-03")]
    public void Renders_an_empty_id_verbatim_without_publishing_it()
    {
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p
            .AddChildContent<FieldControl>(c => c.AddUnmatched("aria-describedby", "external-description"))
            .AddChildContent<FieldError>(e => e.Add(x => x.Match, true).AddUnmatched("id", string.Empty).AddChildContent("Message")));

        Assert.Equal(string.Empty, cut.Find("div > div").GetAttribute("id"));
        Assert.Equal("external-description", cut.Find("input").GetAttribute("aria-describedby"));
    }

    [Fact(DisplayName = "ignores empty Form error arrays")]
    [Trait("Requirement", "FLD-API-30")]
    [Trait("Requirement", "FLD-FRM-09")]
    public void Ignores_empty_Form_error_arrays()
    {
        IRenderedComponent<FieldErrorScenarios> cut = Render(Scenario.FormErrors, p => p.Add(x => x.Errors, new FormErrors { ["username"] = Array.Empty<string>() }));

        Assert.Empty(cut.FindAll("[data-testid=default-error]"));
        Assert.False(cut.Find("input").HasAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "uses `match={false}` as the default slot for client validation errors")]
    [Trait("Requirement", "FLD-API-30")]
    public void Uses_match_false_as_the_default_slot_for_client_validation_errors()
    {
        IRenderedComponent<FieldErrorScenarios> cut = Render(Scenario.DefaultSlotClient);
        host.Module.Control.Native = Native.ValueMissing;
        Assert.Empty(cut.FindAll("[data-testid=default-error]"));

        host.Module.FormElement.SubmitAll(cut);

        Assert.Single(cut.FindAll("[data-testid=default-error]"));
    }

    [Fact(DisplayName = "uses the client validation path for specific matches when Form errors are present")]
    [Trait("Requirement", "FLD-API-30")]
    [Trait("Requirement", "FLD-DOM-15")]
    [Trait("Requirement", "FLD-EDGE-07")]
    public void Uses_the_client_validation_path_for_specific_matches_when_Form_errors_are_present()
    {
        IRenderedComponent<FieldErrorScenarios> cut = Render(Scenario.SpecificWithFormErrors, p => p
            .Add(x => x.Errors, new FormErrors { ["username"] = "Username is reserved" })
            .Add(x => x.Validate, _ => new ValueTask<FieldValidationResult?>("Client validation error")));

        host.Module.FormElement.SubmitAll(cut);

        Assert.Equal("Client validation error", cut.Find("[data-testid=custom-error]").TextContent);
        Assert.Equal("Username is reserved", cut.Find("[data-testid=default-error]").TextContent);
    }

    // FLD-ANI-05: rendered in the part's first render, so it mounts without data-starting-style.
    [Fact(DisplayName = "always renders the error message when `match` is true")]
    [Trait("Requirement", "FLD-API-30")]
    [Trait("Requirement", "FLD-ANI-05")]
    public void Always_renders_the_error_message_when_match_is_true()
    {
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p
            .AddChildContent<FieldControl>(c => c.AddUnmatched("required", true))
            .AddChildContent<FieldError>(e => e.Add(x => x.Match, true).AddChildContent("Message")));

        AngleSharp.Dom.IElement error = cut.Find("div > div");
        Assert.Equal("Message", error.TextContent);
        Assert.False(error.HasAttribute("data-starting-style"));
        TransitionsOptions attached = Assert.Single(host.TransitionsAttachOptions());
        Assert.True(attached.Open);
        Assert.True(attached.Mounted);
        Assert.Null(attached.Status);
    }

    [Fact(DisplayName = "is not rendered while the field is disabled, unless match is true")]
    [Trait("Requirement", "FLD-API-30")]
    [Trait("Requirement", "FLD-DOM-04")]
    public void Is_not_rendered_while_the_field_is_disabled_unless_match_is_true()
    {
        IRenderedComponent<FieldErrorScenarios> cut = Render(Scenario.Toggled, p => p.Add(x => x.Invalid, true).Add(x => x.Disabled, true));
        Assert.Empty(cut.FindAll("[data-testid=error]"));

        cut.Render(p => p.Add(x => x.Match, true));

        AngleSharp.Dom.IElement error = cut.Find("[data-testid=error]");
        Assert.True(error.HasAttribute("data-disabled"));
        Assert.True(error.HasAttribute("data-invalid"));
    }

    // FLD §16 item 5, followed: the error reads the field's disabled state, never the item's.
    [Fact(DisplayName = "ignores the disabled state of an enclosing Field.Item")]
    [Trait("Requirement", "FLD-EDGE-04")]
    public void Ignores_the_disabled_state_of_an_enclosing_Field_Item()
    {
        IRenderedComponent<FieldErrorScenarios> cut = Render(Scenario.Item, p => p.Add(x => x.Errors, new FormErrors { ["username"] = "Username is reserved" }));

        AngleSharp.Dom.IElement error = cut.Find("[data-testid=error]");
        Assert.False(error.HasAttribute("data-disabled"));
        Assert.Equal(error.GetAttribute("id"), cut.Find("[data-testid=control]").GetAttribute("aria-describedby"));
    }

    // FLD §16 item 3, followed: `valid` is one of the eleven names, so the message renders while the field is valid.
    [Fact(DisplayName = "renders while the field is valid when match is ValidityFlag.Valid")]
    [Trait("Requirement", "FLD-EDGE-07")]
    [Trait("Requirement", "FLD-API-30")]
    public void Renders_while_the_field_is_valid_when_match_is_Valid()
    {
        IRenderedComponent<FieldErrorScenarios> cut = Render(Scenario.Toggled, p => p
            .Add(x => x.Match, ValidityFlag.Valid)
            .Add(x => x.ValidationMode, ValidationMode.OnChange)
            .Add(x => x.ChildContent, (RenderFragment)(b => b.AddContent(0, "Looks good"))));
        Assert.Empty(cut.FindAll("[data-testid=error]"));

        host.Module.Control.Input(cut, "a", Native.Valid);
        Assert.Equal("Looks good", cut.Find("[data-testid=error]").TextContent);

        host.Module.Control.Input(cut, string.Empty, Native.ValueMissing);
        host.CompleteExits(cut);
        Assert.Empty(cut.FindAll("[data-testid=error]"));
    }

    // FLD-ANI-02's C# half (the handover, ADR 04 Amendments 2026-10-18): the element C# creates carries
    // data-starting-style, and the module takes the status over from attach.
    [Fact(DisplayName = "renders data-starting-style in the render that mounts it and hands the status to the module")]
    [Trait("Requirement", "FLD-ANI-02")]
    [Trait("Requirement", "FLD-DOM-18")]
    [Trait("Requirement", "FLD-STA-06")]
    public void Renders_data_starting_style_in_the_render_that_mounts_it()
    {
        IRenderedComponent<FieldErrorScenarios> cut = Render(Scenario.Toggled, p => p.Add(x => x.Match, false));
        Assert.Empty(host.TransitionsAttachOptions());

        cut.Render(p => p.Add(x => x.Match, true));

        AngleSharp.Dom.IElement error = cut.Find("[data-testid=error]");
        Assert.True(error.HasAttribute("data-starting-style"));
        Assert.Equal("error-starting", error.GetAttribute("class"));
        TransitionsOptions attached = Assert.Single(host.TransitionsAttachOptions());
        Assert.True(attached.Open);
        Assert.True(attached.Mounted);
        Assert.Equal(TransitionStatus.Starting, attached.Status);
        Assert.False(attached.EnableIdleState);
        Assert.False(attached.DeferEndingState);
        Assert.False(attached.KeepMounted);

        host.ReportTransitionStatus(cut, null);
        Assert.Equal("error", cut.Find("[data-testid=error]").GetAttribute("class"));
    }

    // FLD-ANI-03 (design point E1 of PR 1c): no attribute C# renders carries the error's rendered state, so the close
    // reaches the module through update; the element stays until the module reports the exit complete.
    [Fact(DisplayName = "sends the close through update and keeps the element until the module completes the exit")]
    [Trait("Requirement", "FLD-ANI-03")]
    [Trait("Requirement", "FLD-DOM-14")]
    [Trait("Requirement", "FLD-STA-06")]
    public void Sends_the_close_through_update_and_keeps_the_element_until_the_exit_completes()
    {
        IRenderedComponent<FieldErrorScenarios> cut = Render(Scenario.Toggled, p => p.Add(x => x.Match, true).Add(x => x.ChildContent, (RenderFragment)(b => b.AddContent(0, "Message"))));

        cut.Render(p => p.Add(x => x.Match, false));

        Assert.False(host.TransitionsUpdateOptions()[^1].Open);
        AngleSharp.Dom.IElement error = cut.Find("[data-testid=error]");
        Assert.Equal("error-ending", error.GetAttribute("class"));
        Assert.Equal(TransitionStatus.Ending, cut.Instance.ErrorStates[^1].TransitionStatus);

        host.CompleteExits(cut);
        Assert.Empty(cut.FindAll("[data-testid=error]"));
    }

    // FLD-DOM-17, FLD §16 item 8: the automatic content during the exit is the last message shown while rendered.
    [Fact(DisplayName = "keeps showing the last message while the exit plays")]
    [Trait("Requirement", "FLD-DOM-17")]
    [Trait("Requirement", "FLD-ANI-01")]
    public void Keeps_showing_the_last_message_while_the_exit_plays()
    {
        IRenderedComponent<FieldErrorScenarios> cut = Render(Scenario.Toggled, p => p
            .Add(x => x.ValidationMode, ValidationMode.OnChange)
            .Add(x => x.Validate, arguments => new ValueTask<FieldValidationResult?>((string?)arguments.Value is { Length: < 3 } ? "Too short" : null)));

        host.Module.Control.Input(cut, "ab");
        Assert.Equal("Too short", cut.Find("[data-testid=error]").TextContent);

        host.Module.Control.Input(cut, "abc");
        AngleSharp.Dom.IElement exiting = cut.Find("[data-testid=error]");
        Assert.Equal("Too short", exiting.TextContent);
        Assert.Equal("error-ending", exiting.GetAttribute("class"));

        host.CompleteExits(cut);
        Assert.Empty(cut.FindAll("[data-testid=error]"));
    }

    // FLD-ANI-04: rendered again during the exit, the element stays (no 'starting', no new element), the module is told
    // the part is open, and the content is the live message again.
    [Fact(DisplayName = "stays mounted without re-entering the starting phase when it is rendered again during its exit")]
    [Trait("Requirement", "FLD-ANI-04")]
    public void Stays_mounted_when_it_is_rendered_again_during_its_exit()
    {
        IRenderedComponent<FieldErrorScenarios> cut = Render(Scenario.Toggled, p => p
            .Add(x => x.ValidationMode, ValidationMode.OnChange)
            .Add(x => x.Validate, arguments => new ValueTask<FieldValidationResult?>((string?)arguments.Value switch { "a" => "First", "ab" => "Second", _ => null })));
        host.Module.Control.Input(cut, "a");
        host.ReportTransitionStatus(cut, null);
        host.Module.Control.Input(cut, "abc");
        Assert.Equal("First", cut.Find("[data-testid=error]").TextContent);

        host.Module.Control.Input(cut, "ab");

        Assert.Equal("Second", cut.Find("[data-testid=error]").TextContent);
        Assert.Equal(TransitionStatus.Ending, cut.Instance.ErrorStates[^1].TransitionStatus);
        Assert.Single(host.TransitionsAttachOptions());
        Assert.True(host.TransitionsUpdateOptions()[^1].Open);

        // A completion that crossed the reopen (Server) does not remove the open error.
        host.CompleteExits(cut);
        Assert.Single(cut.FindAll("[data-testid=error]"));
    }

    // FLD-API-31: the id is published while the message is rendered, not while it is mounted.
    [Fact(DisplayName = "withdraws its id from aria-describedby in the render that hides it, before the exit ends")]
    [Trait("Requirement", "FLD-API-31")]
    [Trait("Requirement", "FLD-ARIA-05")]
    public void Withdraws_its_id_in_the_render_that_hides_it()
    {
        IRenderedComponent<FieldErrorScenarios> cut = Render(Scenario.Toggled, p => p.Add(x => x.Match, true));
        string? id = cut.Find("[data-testid=error]").GetAttribute("id");
        Assert.Equal(id, cut.Find("[data-testid=control]").GetAttribute("aria-describedby"));

        cut.Render(p => p.Add(x => x.Match, false));

        Assert.Single(cut.FindAll("[data-testid=error]"));
        Assert.False(cut.Find("[data-testid=control]").HasAttribute("aria-describedby"));
    }

    // FLD-ANI-03 (TRN-ANI-03): with no element to watch (a substitute callback), nothing can play the exit.
    [Fact(DisplayName = "unmounts at once when a substitute callback leaves no element to watch")]
    [Trait("Requirement", "FLD-ANI-03")]
    public void Unmounts_at_once_without_an_element_to_watch()
    {
        RenderFragment<RenderProps<FieldErrorState>> callback = props => builder =>
        {
            builder.OpenElement(0, "div");
            builder.AddMultipleAttributes(1, props.Attributes);
            builder.AddContent(2, props.ChildContent);
            builder.CloseElement();
        };
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p
            .AddChildContent<FieldControl>()
            .AddChildContent<FieldError>(e => e.Add(x => x.Match, true).Add(x => x.RenderFunction, callback).AddChildContent("Message")));
        Assert.Single(cut.FindAll("div > div"));

        cut.FindComponent<FieldError>().Render(p => p.Add(x => x.Match, false));

        Assert.Empty(cut.FindAll("div > div"));
        Assert.Empty(host.TransitionsAttachOptions());
    }

    // ADR 05 Amendments 2026-10-22 (the form controls inside EditForm): the default slot shows the messages a cascaded
    // EditContext holds for the control's identifier.
    [Fact(DisplayName = "shows a cascaded EditContext's messages in the default slot")]
    [Trait("Verifies", "ADR05-5")]
    [Trait("Requirement", "FFI-FRM-25")]
    public void Shows_a_cascaded_EditContexts_messages_in_the_default_slot()
    {
        FieldErrorScenarios.Account model = new();
        EditContext editContext = new(model);
        ValidationMessageStore store = new(editContext);
        IRenderedComponent<FieldErrorScenarios> cut = Render(Scenario.EditContext, p => p.Add(x => x.Model, model).Add(x => x.EditContext, editContext));
        Assert.Empty(cut.FindAll("[data-testid=error]"));

        cut.InvokeAsync(() =>
        {
            store.Add(new FieldIdentifier(model, nameof(FieldErrorScenarios.Account.Email)), "The Email field is required.");
            editContext.NotifyValidationStateChanged();
        });
        Assert.Equal("The Email field is required.", cut.Find("[data-testid=error]").TextContent);

        cut.InvokeAsync(() =>
        {
            store.Clear();
            editContext.NotifyValidationStateChanged();
        });
        host.CompleteExits(cut);
        Assert.Empty(cut.FindAll("[data-testid=error]"));
    }

    [Fact(DisplayName = "throws outside a Field.Root")]
    [Trait("Requirement", "FLD-STR-02")]
    [Trait("Requirement", "FLD-EDGE-01")]
    public void Throws_outside_a_Field_Root()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Context.Render<FieldError>());

        Assert.Contains("FieldRoot", error.Message, StringComparison.Ordinal);
    }

    private IRenderedComponent<FieldErrorScenarios> Render(Scenario scenario, Action<ComponentParameterCollectionBuilder<FieldErrorScenarios>>? parameters = null) =>
        Context.Render<FieldErrorScenarios>(p =>
        {
            p.Add(x => x.Scenario, scenario);
            parameters?.Invoke(p);
        });

    private static string[] Messages(IRenderedComponent<FieldErrorScenarios> cut) =>
        [.. cut.FindComponents<FieldError>().SelectMany(error => error.Nodes.OfType<AngleSharp.Dom.IElement>()).Select(element => element.TextContent)];
}
