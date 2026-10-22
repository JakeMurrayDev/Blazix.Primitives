using System.Text.Json;
using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The form module's twin contract (ADR 06 §1, L2): one <see cref="FormTwin"/> per control and per label, the
/// <see cref="FormOptions"/> each sends on <c>attach</c> and <c>update</c> as bUnit's JS interop mock records them, an
/// <c>update</c> only when the options changed, <c>dispose</c> on removal, and each report driven into C# as one state
/// change (one render of the field's parts, FLD-EDGE-13).
/// </summary>
public sealed class FormTwinWiringTests : IDisposable
{
    private readonly FieldTestContext host = new();

    private BunitContext Context => host.Context;

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "attaches one form twin to a control after the first render with the control options")]
    [Trait("Verifies", "ADR06-3")]
    [Trait("Requirement", "FFI-ALG-12")]
    public void Attaches_one_form_twin_to_a_control_with_the_control_options()
    {
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p.AddChildContent<FieldControl>(c => c.Add(x => x.DefaultValue, "start")));

        FormOptions options = FieldTestContext.Parse(Assert.Single(host.AttachOptions()));
        Assert.Null(options.Label);
        FormControlOptions control = Assert.IsType<FormControlOptions>(options.Control);
        Assert.False(control.Controlled);
        Assert.Null(control.Value);
        Assert.Equal("start", control.DefaultValue);
        Assert.False(control.Disabled);
        Assert.False(control.ReportKeys);
        Assert.True(control.ReportEvents);
        Assert.Null(control.CustomValidity);
        Assert.Null(control.FormId);
        Assert.Equal("start", cut.Find("input").GetAttribute("value"));
    }

    [Fact(DisplayName = "attaches a controlled control with its value and no default value")]
    [Trait("Verifies", "ADR06-3")]
    [Trait("Requirement", "FLD-API-15")]
    public void Attaches_a_controlled_control_with_its_value()
    {
        Context.Render<FieldRoot>(p => p.AddChildContent<FieldControl>(c => c
            .Add(x => x.Value, "typed")
            .Add(x => x.DefaultValue, "ignored")
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string?>(this, _ => { }))));

        FormControlOptions control = FieldTestContext.Parse(Assert.Single(host.AttachOptions())).Control!;
        Assert.True(control.Controlled);
        Assert.Equal("typed", control.Value);
        Assert.Null(control.DefaultValue);
    }

    [Fact(DisplayName = "attaches one form twin to a label with the label options")]
    [Trait("Verifies", "ADR06-3")]
    [Trait("Requirement", "FLD-API-11")]
    public void Attaches_one_form_twin_to_a_label_with_the_label_options()
    {
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p
            .AddChildContent<FieldLabel>(l => l.AddChildContent("Label"))
            .AddChildContent<FieldControl>());

        FormLabelOptions label = Assert.Single(host.AttachOptions().Select(FieldTestContext.Parse), o => o.Label is not null).Label!;
        Assert.True(label.Native);
        Assert.False(label.Decides);
        Assert.Equal(cut.Find("input").GetAttribute("id"), label.ControlId);
    }

    [Fact(DisplayName = "sends an update only when the options changed")]
    [Trait("Verifies", "ADR06-3")]
    [Trait("Requirement", "ADR16-3")]
    public void Sends_an_update_only_when_the_options_changed()
    {
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p.AddChildContent<FieldControl>());
        int updates = host.UpdateOptions().Count;

        cut.Render();
        Assert.Equal(updates, host.UpdateOptions().Count);

        cut.Render(p => p.Add(x => x.Disabled, true));
        FormControlOptions control = Assert.Single(host.UpdateOptions().Skip(updates).Select(FieldTestContext.Parse)).Control!;
        Assert.True(control.Disabled);
    }

    [Fact(DisplayName = "reports keydown only while the consumer handles it")]
    [Trait("Verifies", "ADR06-3")]
    [Trait("Requirement", "FLD-KEY-04")]
    public void Reports_keydown_only_while_the_consumer_handles_it()
    {
        Context.Render<FieldRoot>(p => p.AddChildContent<FieldControl>(c => c
            .AddUnmatched("onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, _ => { }))));

        Assert.True(FieldTestContext.Parse(Assert.Single(host.AttachOptions())).Control!.ReportKeys);
    }

    [Fact(DisplayName = "disposes the handle when the part is removed")]
    [Trait("Verifies", "ADR06-3")]
    public void Disposes_the_handle_when_the_part_is_removed()
    {
        IRenderedComponent<FieldScenarios> cut = Context.Render<FieldScenarios>(p => p.Add(x => x.Scenario, FieldScenarios.FieldScenario.TwoControls).Add(x => x.SecondMounted, false));
        int before = host.FormHandle.Invocations["dispose"].Count;

        cut.Render(p => p.Add(x => x.Scenario, FieldScenarios.FieldScenario.TwoControls).Add(x => x.FirstMounted, false));

        Assert.Equal(before + 1, host.FormHandle.Invocations["dispose"].Count);
    }

    // Each part's own renders are counted through its render function: bUnit's RenderCount of the root also counts the
    // renders of the parts inside it.
    [Fact(DisplayName = "drives each control report into C# as one state change")]
    [Trait("Verifies", "ADR06-3")]
    [Trait("Requirement", "FLD-EDGE-13")]
    [Trait("Requirement", "FFI-EDGE-09")]
    public void Drives_each_control_report_into_CSharp_as_one_state_change()
    {
        int root = 0, label = 0, control = 0;
        RenderFragment<RenderProps<FieldRootState>> rootRender = props => builder =>
        {
            root++;
            builder.OpenElement(0, "div");
            builder.AddMultipleAttributes(1, props.Attributes);
            builder.AddContent(2, props.ChildContent);
            builder.CloseElement();
        };
        RenderFragment<RenderProps<FieldLabelState>> labelRender = props => builder =>
        {
            label++;
            builder.OpenElement(0, "label");
            builder.AddMultipleAttributes(1, props.Attributes);
            builder.CloseElement();
        };
        RenderFragment<RenderProps<FieldControlState>> controlRender = props => builder =>
        {
            control++;
            builder.OpenElement(0, "input");
            builder.AddMultipleAttributes(1, props.Attributes);
            builder.CloseElement();
        };
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p
            .AddUnmatched("data-testid", "root")
            .Add(x => x.RenderFunction, rootRender)
            .Add(x => x.ValidationMode, ValidationMode.OnChange)
            .Add(x => x.Validate, args => new ValueTask<FieldValidationResult?>((string?)args.Value == "bad" ? "error" : null))
            .AddChildContent<FieldLabel>(l => l.Add(x => x.RenderFunction, labelRender))
            .AddChildContent<FieldControl>(c => c.Add(x => x.RenderFunction, controlRender)));
        (int rootBefore, int labelBefore, int controlBefore) = (root, label, control);

        // One report changes dirty, filled and validity together.
        host.Module.Control.Input(cut, "bad");

        Assert.True(cut.Find("[data-testid=root]").HasAttribute("data-dirty"));
        Assert.True(cut.Find("[data-testid=root]").HasAttribute("data-filled"));
        Assert.True(cut.Find("[data-testid=root]").HasAttribute("data-invalid"));
        Assert.Equal(1, root - rootBefore);
        Assert.Equal(1, label - labelBefore);
        Assert.Equal(1, control - controlBefore);
    }

    [Fact(DisplayName = "installs the field's own custom validity through an update and withdraws it when valid")]
    [Trait("Verifies", "ADR06-3")]
    [Trait("Requirement", "FFI-FRM-09")]
    [Trait("Requirement", "FFI-FRM-10")]
    public void Installs_the_fields_own_custom_validity_through_an_update()
    {
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p
            .Add(x => x.ValidationMode, ValidationMode.OnChange)
            .Add(x => x.Validate, args => new ValueTask<FieldValidationResult?>((string?)args.Value == "bad" ? "custom error" : null))
            .AddChildContent<FieldControl>(c => c.AddUnmatched("type", "email")));

        host.Module.Control.Input(cut, "bad", Native.TypeMismatch);
        Assert.Equal("custom error", host.Module.Control.Options!.CustomValidity);

        host.Module.Control.Input(cut, "a@b.co", NativeValidity.Default);
        Assert.Null(host.Module.Control.Options!.CustomValidity);
    }
}
