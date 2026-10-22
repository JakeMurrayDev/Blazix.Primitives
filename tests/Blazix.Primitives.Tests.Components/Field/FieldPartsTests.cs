using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Scenario = Blazix.Primitives.Tests.Components.FieldScenarios.FieldScenario;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The <c>[L2]</c> titles of <c>FieldLabel.test.tsx</c>, <c>FieldDescription.test.tsx</c> and <c>FieldItem.test.tsx</c>
/// that render only field parts (PR 1a), the label's development check (FLD-EDGE-02) driven as the form module reports it,
/// and the port titles for the consumer handlers the field parts run first (FLD-API-14, FLD-PTR-04, FLD-EVT-09, FLD-KEY-04).
/// </summary>
public sealed class FieldPartsTests : IDisposable
{
    private readonly FieldTestContext host = new();

    private BunitContext Context => host.Context;

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "should set htmlFor referencing the control automatically")]
    [Trait("Requirement", "FLD-API-11")]
    [Trait("Requirement", "FLD-DOM-06")]
    [Trait("Requirement", "FFI-ARIA-04")]
    public void Should_set_htmlFor_referencing_the_control_automatically()
    {
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p
            .AddChildContent<FieldControl>()
            .AddChildContent<FieldLabel>(l => l.AddUnmatched("data-testid", "label").AddChildContent("Label")));

        Assert.Equal(cut.Find("input").GetAttribute("id"), cut.Find("[data-testid=label]").GetAttribute("for"));
    }

    // The click-to-focus is the form module's inside the dispatch (L4, field.spec.ts); here: no `for`, and the module is
    // told the label is non-native and which control it focuses.
    [Fact(DisplayName = "when nativeLabel={false}, clicking focuses the associated control")]
    [Trait("Requirement", "FLD-API-11")]
    [Trait("Requirement", "FLD-DOM-07")]
    [Trait("Requirement", "FLD-PTR-03")]
    public void When_nativeLabel_false_clicking_focuses_the_associated_control()
    {
        RenderFragment div = builder =>
        {
            builder.OpenElement(0, "div");
            builder.CloseElement();
        };
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p
            .AddChildContent<FieldControl>(c => c.AddUnmatched("data-testid", "control"))
            .AddChildContent<FieldLabel>(l => l.Add(x => x.NativeLabel, false).Add(x => x.Render, div).AddUnmatched("data-testid", "label").AddChildContent("Label")));

        Assert.False(cut.Find("[data-testid=label]").HasAttribute("for"));
        FormLabelOptions label = host.AttachOptions().Select(FieldTestContext.Parse).Last(o => o.Label is not null).Label!;
        Assert.False(label.Native);
        Assert.Equal(cut.Find("[data-testid=control]").GetAttribute("id"), label.ControlId);
    }

    [Fact(DisplayName = "keeps the selected control id when another control unmounts")]
    [Trait("Requirement", "FLD-STR-03")]
    [Trait("Requirement", "FLD-DOM-06")]
    [Trait("Requirement", "FFI-ALG-06")]
    public void Keeps_the_selected_control_id_when_another_control_unmounts()
    {
        IRenderedComponent<FieldScenarios> cut = Context.Render<FieldScenarios>(p => p.Add(x => x.Scenario, Scenario.TwoControls).Add(x => x.FirstId, "a").Add(x => x.SecondId, "b"));
        Assert.Equal("a", cut.Find("[data-testid=label]").GetAttribute("for"));

        cut.Render(p => p.Add(x => x.Scenario, Scenario.TwoControls).Add(x => x.SecondMounted, false));

        Assert.Equal("a", cut.Find("[data-testid=label]").GetAttribute("for"));
    }

    [Fact(DisplayName = "falls over to the remaining control when the selected one unmounts")]
    [Trait("Requirement", "FLD-STR-03")]
    [Trait("Requirement", "FLD-DOM-06")]
    [Trait("Requirement", "FFI-ALG-06")]
    public void Falls_over_to_the_remaining_control_when_the_selected_one_unmounts()
    {
        IRenderedComponent<FieldScenarios> cut = Context.Render<FieldScenarios>(p => p.Add(x => x.Scenario, Scenario.TwoControls).Add(x => x.FirstId, "a").Add(x => x.SecondId, "b"));
        Assert.Equal("a", cut.Find("[data-testid=label]").GetAttribute("for"));

        cut.Render(p => p.Add(x => x.Scenario, Scenario.TwoControls).Add(x => x.FirstMounted, false));

        Assert.Equal("b", cut.Find("[data-testid=label]").GetAttribute("for"));
    }

    [Fact(DisplayName = "reflects the disabled state from Field.Item")]
    [Trait("Requirement", "FLD-STA-02")]
    [Trait("Requirement", "FLD-STA-04")]
    [Trait("Requirement", "FLD-DOM-04")]
    public void Reflects_the_disabled_state_from_Field_Item()
    {
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p
            .AddChildContent<FieldItem>(i => i.Add(x => x.Disabled, true)
                .AddChildContent<FieldLabel>(l => l.AddUnmatched("data-testid", "label").AddChildContent("Label"))
                .AddChildContent<FieldDescription>(d => d.AddUnmatched("data-testid", "description").AddChildContent("Message"))));

        Assert.True(cut.Find("[data-testid=label]").HasAttribute("data-disabled"));
        Assert.True(cut.Find("[data-testid=description]").HasAttribute("data-disabled"));
    }

    [Fact(DisplayName = "does not warn by default")]
    [Trait("Requirement", "FLD-EDGE-02")]
    [Trait("Requirement", "FFI-EDGE-02")]
    public void Does_not_warn_by_default()
    {
        CapturingLogger<FieldLabel> logger = new();
        Context.Services.AddSingleton<ILogger<FieldLabel>>(logger);

        Context.Render<FieldRoot>(p => p.AddChildContent<FieldControl>().AddChildContent<FieldLabel>(l => l.AddChildContent("Label")));

        Assert.True(host.AttachOptions().Select(FieldTestContext.Parse).Last(o => o.Label is not null).Label!.DevelopmentChecks);
        Assert.Empty(logger.Entries);
    }

    // A substitute that renders no element leaves the module nothing to check; the module reports only a rendered element.
    [Fact(DisplayName = "does not warn when the render function returns no element")]
    [Trait("Requirement", "FLD-EDGE-02")]
    public void Does_not_warn_when_the_render_function_returns_no_element()
    {
        CapturingLogger<FieldLabel> logger = new();
        Context.Services.AddSingleton<ILogger<FieldLabel>>(logger);

        Context.Render<FieldRoot>(p => p.AddChildContent<FieldLabel>(l => l.Add(x => x.RenderFunction, (RenderFragment<RenderProps<FieldLabelState>>)(_ => _ => { })).AddChildContent("Label")));

        Assert.Empty(logger.Entries);
    }

    // In a browser the module finds no element by the label's id and throws; the label then attaches nothing and checks
    // nothing, and neither its render nor the field fails.
    [Fact(DisplayName = "attaches nothing when the render function returns no element")]
    [Trait("Requirement", "FFI-EDGE-02")]
    [Trait("Requirement", "FLD-EDGE-02")]
    public void Attaches_nothing_when_the_render_function_returns_no_element()
    {
        CapturingLogger<FieldLabel> logger = new();
        Context.Services.AddSingleton<ILogger<FieldLabel>>(logger);
        NoElementRuntime runtime = new();
        Context.Services.AddSingleton<IJSRuntime>(runtime);

        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p
            .AddUnmatched("data-testid", "root")
            .AddChildContent<FieldLabel>(l => l.Add(x => x.RenderFunction, (RenderFragment<RenderProps<FieldLabelState>>)(_ => _ => { })).AddChildContent("Label"))
            .AddChildContent<FieldControl>(c => c.Add(x => x.RenderFunction, (RenderFragment<RenderProps<FieldControlState>>)(_ => _ => { }))));
        cut.Render();

        Assert.NotNull(cut.Find("[data-testid=root]"));
        Assert.Empty(logger.Entries);
        Assert.False(cut.FindComponent<FieldLabel>().Instance.TwinAttached.IsCompleted);
        Assert.True(runtime.Attempts >= 2);
    }

    [Fact(DisplayName = "errors if nativeLabel=true but ref is not a label")]
    [Trait("Requirement", "FLD-EDGE-02")]
    [Trait("Requirement", "FFI-EDGE-02")]
    public void Errors_if_nativeLabel_true_but_ref_is_not_a_label()
    {
        CapturingLogger<FieldLabel> logger = new();
        Context.Services.AddSingleton<ILogger<FieldLabel>>(logger);
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p.AddChildContent<FieldControl>().AddChildContent<FieldLabel>(l => l.Add(x => x.NativeLabel, true).AddChildContent("Label")));

        cut.InvokeAsync(() => host.Module.Labels[^1].Twin!.OnLabelMismatch("""{"expectedNative":true}"""));
        cut.InvokeAsync(() => host.Module.Labels[^1].Twin!.OnLabelMismatch("""{"expectedNative":true}"""));

        (LogLevel level, string message) = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, level);
        Assert.Contains("Blazix: `<FieldLabel>` expected a `<label>` element because the `NativeLabel` parameter is `true`.", message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "errors if nativeLabel=false but ref is a label")]
    [Trait("Requirement", "FLD-EDGE-02")]
    [Trait("Requirement", "FFI-EDGE-02")]
    public void Errors_if_nativeLabel_false_but_ref_is_a_label()
    {
        CapturingLogger<FieldLabel> logger = new();
        Context.Services.AddSingleton<ILogger<FieldLabel>>(logger);
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p.AddChildContent<FieldControl>().AddChildContent<FieldLabel>(l => l.Add(x => x.NativeLabel, false).AddChildContent("Label")));

        cut.InvokeAsync(() => host.Module.Labels[^1].Twin!.OnLabelMismatch("""{"expectedNative":false}"""));

        (LogLevel level, string message) = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, level);
        Assert.Contains("Blazix: `<FieldLabel>` expected a non-`<label>` element because the `NativeLabel` parameter is `false`.", message, StringComparison.Ordinal);
    }

    // FieldRoot.react17.test.tsx mocks the owner-stack API away; the port has none, so the title asserts the message alone.
    [Fact(DisplayName = "reports label mismatches without the owner-stack API")]
    [Trait("Requirement", "FLD-EDGE-02")]
    [Trait("Requirement", "FFI-EDGE-02")]
    public void Reports_label_mismatches_without_the_owner_stack_API()
    {
        CapturingLogger<FieldLabel> logger = new();
        Context.Services.AddSingleton<ILogger<FieldLabel>>(logger);
        RenderFragment div = builder =>
        {
            builder.OpenElement(0, "div");
            builder.CloseElement();
        };
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p.AddChildContent<FieldLabel>(l => l.Add(x => x.Render, div).AddChildContent("Label")));

        cut.InvokeAsync(() => host.Module.Labels[^1].Twin!.OnLabelMismatch("""{"expectedNative":true}"""));

        Assert.Contains("`<FieldLabel>` expected a `<label>` element", Assert.Single(logger.Entries).Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "reports non-native label mismatches without the owner-stack API")]
    [Trait("Requirement", "FLD-EDGE-02")]
    [Trait("Requirement", "FFI-EDGE-02")]
    public void Reports_non_native_label_mismatches_without_the_owner_stack_API()
    {
        CapturingLogger<FieldLabel> logger = new();
        Context.Services.AddSingleton<ILogger<FieldLabel>>(logger);
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p.AddChildContent<FieldLabel>(l => l.Add(x => x.NativeLabel, false).AddChildContent("Label")));

        cut.InvokeAsync(() => host.Module.Labels[^1].Twin!.OnLabelMismatch("""{"expectedNative":false}"""));

        Assert.Contains("`<FieldLabel>` expected a non-`<label>` element", Assert.Single(logger.Entries).Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "falls back to a generated id when an explicit control id is removed")]
    [Trait("Requirement", "FFI-FRM-36")]
    [Trait("Requirement", "FLD-API-20")]
    [Trait("Requirement", "FLD-DOM-06")]
    public void Falls_back_to_a_generated_id_when_an_explicit_control_id_is_removed()
    {
        IRenderedComponent<FieldScenarios> cut = Context.Render<FieldScenarios>(p => p
            .Add(x => x.Scenario, Scenario.TwoControls).Add(x => x.FirstId, "custom").Add(x => x.SecondMounted, false));
        Assert.Equal("custom", cut.Find("[data-testid=label]").GetAttribute("for"));

        cut.Render(p => p.Add(x => x.Scenario, Scenario.TwoControls).Add(x => x.FirstId, (string?)null));

        string? id = cut.Find("[data-testid=first]").GetAttribute("id");
        Assert.NotEqual("custom", id);
        Assert.False(string.IsNullOrEmpty(id));
        Assert.Equal(id, cut.Find("[data-testid=label]").GetAttribute("for"));
    }

    [Fact(DisplayName = "should set aria-describedby on the control automatically")]
    [Trait("Requirement", "FLD-API-24")]
    [Trait("Requirement", "FLD-DOM-13")]
    [Trait("Requirement", "FLD-ARIA-02")]
    [Trait("Requirement", "FLD-ARIA-04")]
    public void Should_set_aria_describedby_on_the_control_automatically()
    {
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p
            .AddChildContent<FieldControl>()
            .AddChildContent<FieldDescription>(d => d.AddChildContent("Message")));

        Assert.Equal(cut.Find("p").GetAttribute("id"), cut.Find("input").GetAttribute("aria-describedby"));
    }

    [Fact(DisplayName = "should preserve user aria-describedby values on the control")]
    [Trait("Requirement", "FLD-ARIA-02")]
    [Trait("Requirement", "FFI-ARIA-01")]
    public void Should_preserve_user_aria_describedby_values_on_the_control()
    {
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p
            .AddChildContent<FieldControl>(c => c.AddUnmatched("aria-describedby", "external-description"))
            .AddChildContent<FieldDescription>(d => d.AddChildContent("Message")));

        Assert.Equal($"external-description {cut.Find("p").GetAttribute("id")}", cut.Find("input").GetAttribute("aria-describedby"));
    }

    [Fact(DisplayName = "does not register an empty description id")]
    [Trait("Requirement", "FLD-API-24")]
    [Trait("Requirement", "FLD-ARIA-02")]
    [Trait("Requirement", "FLD-EDGE-03")]
    public void Does_not_register_an_empty_description_id()
    {
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p
            .AddChildContent<FieldControl>(c => c.AddUnmatched("aria-describedby", "external-description"))
            .AddChildContent<FieldDescription>(d => d.AddUnmatched("id", string.Empty).AddChildContent("Message")));

        Assert.Equal("external-description", cut.Find("input").GetAttribute("aria-describedby"));
        Assert.Equal(string.Empty, cut.Find("p").GetAttribute("id"));
    }

    [Fact(DisplayName = "reflects disabled state on the item")]
    [Trait("Requirement", "FLD-STR-06")]
    [Trait("Requirement", "FLD-API-27")]
    [Trait("Requirement", "FLD-API-28")]
    [Trait("Requirement", "FLD-STA-05")]
    public void Reflects_disabled_state_on_the_item()
    {
        List<FieldItemState> states = [];
        RenderFragment<RenderProps<FieldItemState>> render = props => builder =>
        {
            states.Add(props.State);
            builder.OpenElement(0, "div");
            builder.AddMultipleAttributes(1, props.Attributes);
            builder.CloseElement();
        };
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p
            .AddChildContent<FieldItem>(i => i.Add(x => x.Disabled, true).AddUnmatched("data-testid", "item").Add(x => x.RenderFunction, render)));

        Assert.True(cut.Find("[data-testid=item]").HasAttribute("data-disabled"));
        Assert.True(states[^1].Disabled);
        Assert.False(cut.Find("[data-testid=item]").HasAttribute("disabled"));
    }

    [Fact(DisplayName = "throws a descriptive error when a field part is rendered outside <Field.Root>")]
    [Trait("Requirement", "FLD-STR-02")]
    [Trait("Requirement", "FLD-EDGE-01")]
    [Trait("Requirement", "FFI-STR-02")]
    [Trait("Requirement", "FFI-EDGE-01")]
    public void Throws_a_descriptive_error_when_a_field_part_is_rendered_outside_Field_Root()
    {
        const string Message = "Blazix: The `FieldRoot` context is missing. Field parts must be placed within `<FieldRoot>`.";

        Assert.Equal(Message, Assert.Throws<InvalidOperationException>(() => Context.Render<FieldLabel>()).Message);
        Assert.Equal(Message, Assert.Throws<InvalidOperationException>(() => Context.Render<FieldDescription>()).Message);
        Assert.Equal(Message, Assert.Throws<InvalidOperationException>(() => Context.Render<FieldItem>()).Message);
        Assert.NotNull(Context.Render<FieldControl>().Find("input"));
    }

    [Fact(DisplayName = "runs the consumer's input, blur and keydown handlers before the control's own, which a veto skips")]
    [Trait("Requirement", "FLD-EVT-09")]
    [Trait("Requirement", "FLD-KEY-04")]
    [Trait("Requirement", "FLD-API-23")]
    [Trait("Requirement", "INP-API-10")]
    public void Runs_the_consumers_handlers_before_the_controls_own_which_a_veto_skips()
    {
        List<string> order = [];
        int validations = 0;
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p
            .AddUnmatched("data-testid", "root")
            .Add(x => x.ValidationMode, ValidationMode.OnChange)
            .Add(x => x.Validate, _ => { validations++; return default; })
            .AddChildContent<FieldControl>(c => c
                .AddUnmatched("oninput", EventCallback.Factory.Create<ChangeEventArgs>(this, e => { order.Add($"input {e.Value}"); e.PreventPartHandler(); }))
                .AddUnmatched("onblur", EventCallback.Factory.Create<FocusEventArgs>(this, e => order.Add("blur")))
                .AddUnmatched("onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, e => { order.Add($"keydown {e.Key}"); e.PreventPartHandler(); }))));

        Assert.False(cut.Find("input").HasAttribute("oninput"));
        host.Module.Control.Input(cut, "a");
        host.Module.Control.KeyDown(cut, "Enter");
        host.Module.Control.Blur(cut);

        Assert.Equal(["input a", "keydown Enter", "blur"], order);
        Assert.Equal(0, validations);
        Assert.False(cut.Find("[data-testid=root]").HasAttribute("data-dirty"));
        Assert.True(cut.Find("[data-testid=root]").HasAttribute("data-touched"));
        Assert.True(host.Module.Control.Options!.ReportKeys);
    }

    [Fact(DisplayName = "runs the consumer's label pointer handler inside the decision, and Blazor's dispatch of the same event does not run it again")]
    [Trait("Requirement", "FLD-PTR-04")]
    [Trait("Requirement", "FLD-API-14")]
    public void Runs_the_consumers_label_pointer_handler_inside_the_decision()
    {
        int clicks = 0;
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p
            .AddChildContent<FieldControl>()
            .AddChildContent<FieldLabel>(l => l.Add(x => x.NativeLabel, false)
                .AddUnmatched("onclick", EventCallback.Factory.Create<MouseEventArgs>(this, e => { clicks++; e.PreventPartHandler(); }))));
        FormTwin twin = host.Module.Labels[^1].Twin!;
        Assert.True(host.AttachOptions().Select(FieldTestContext.Parse).Last(o => o.Label is not null).Label!.Decides);

        string verdict = string.Empty;
        cut.InvokeAsync(() => verdict = twin.Decide(FormTwin.LabelPoint, """{"kind":"MouseEvent","type":"click","detail":1}"""));
        cut.Find("label").Click(new MouseEventArgs { Type = "click", Detail = 1 });

        Assert.Equal("stop", verdict);
        Assert.Equal(1, clicks);

        cut.Find("label").Click(new MouseEventArgs { Type = "click", Detail = 1 });
        Assert.Equal(2, clicks);
    }

    [Fact(DisplayName = "an input outside a field renders only data-disabled and registers nowhere")]
    [Trait("Requirement", "INP-FRM-07")]
    [Trait("Requirement", "INP-STA-01")]
    [Trait("Requirement", "INP-STR-03")]
    public void An_input_outside_a_field_renders_only_data_disabled()
    {
        List<string> changes = [];
        IRenderedComponent<Input> cut = Context.Render<Input>(p => p
            .Add(x => x.Disabled, true)
            .AddUnmatched("aria-describedby", "external")
            .Add(x => x.OnValueChange, args => changes.Add(args.Value)));
        IElement input = cut.Find("input");

        Assert.True(input.HasAttribute("data-disabled"));
        Assert.True(input.HasAttribute("disabled"));
        Assert.False(input.HasAttribute("aria-labelledby"));
        Assert.False(input.HasAttribute("aria-invalid"));
        Assert.Equal("external", input.GetAttribute("aria-describedby"));
        Assert.StartsWith("base-ui-", input.GetAttribute("id"), StringComparison.Ordinal);
        foreach (string attribute in new[] { "data-valid", "data-invalid", "data-touched", "data-dirty", "data-filled", "data-focused" })
        {
            Assert.False(input.HasAttribute(attribute));
        }

        host.Module.Control.Input(cut, "typed");
        Assert.Equal(["typed"], changes);
        Assert.False(cut.Find("input").HasAttribute("data-dirty"));
    }

    /// <summary>A runtime whose form module, like the browser's, finds no element for an id that nothing rendered.</summary>
    /// <remarks>BL0016 asks callers of JS interop to guard the call; this is the runtime itself, which only answers.</remarks>
#pragma warning disable BL0016
    private sealed class NoElementRuntime : IJSRuntime, IJSObjectReference
    {
        public int Attempts { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => Answer<TValue>(identifier, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => Answer<TValue>(identifier, args);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private ValueTask<TValue> Answer<TValue>(string identifier, object?[]? args)
        {
            if (identifier == "import")
            {
                return ValueTask.FromResult((TValue)(object)this);
            }

            Attempts++;
            return ValueTask.FromException<TValue>(new JSException($"Blazix: no element for id '{args?[0]}'"));
        }
    }
#pragma warning restore BL0016
}
