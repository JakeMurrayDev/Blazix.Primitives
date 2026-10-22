using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Scenario = Blazix.Primitives.Tests.Components.FieldErrorScenarios.ErrorScenario;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The <c>[L2]</c> titles of <c>FieldValidity.test.tsx</c> (PR 1c), and port titles for what Base UI leaves untested: the
/// argument's identity (FLD-STA-08) and the transition status of the invalid state (FLD-ANI-06, FLD §16 item 1). The
/// form module's native state is the stand-in's, so "defers badInput during required change revalidation", which Base
/// UI runs only in a browser, runs here with the payload the browser would send.
/// </summary>
public sealed class FieldValidityTests : IDisposable
{
    private static readonly NativeValidity s_badInputAndMissing = new() { BadInput = true, ValueMissing = true, ValidationMessage = "Please enter a number." };

    private readonly FieldTestContext host = new();

    private BunitContext Context => host.Context;

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    public static TheoryData<ValidationMode> BlurAndSubmit => new() { ValidationMode.OnBlur, ValidationMode.OnSubmit };

    [Theory(DisplayName = "surfaces valueMissing immediately after a stale custom error in $validationMode mode")]
    [MemberData(nameof(BlurAndSubmit))]
    [Trait("Requirement", "FLD-FRM-13")]
    [Trait("Requirement", "FLD-STA-07")]
    [Trait("Requirement", "FFI-FRM-05")]
    public void Surfaces_valueMissing_immediately_after_a_stale_custom_error(ValidationMode mode)
    {
        int calls = 0;
        IRenderedComponent<FieldErrorScenarios> cut = Render(p => p
            .Add(x => x.ValidationMode, mode)
            .Add(x => x.Validate, _ =>
            {
                calls++;
                return new ValueTask<FieldValidationResult?>("custom error");
            }));
        FormModuleStandIn.Attached control = host.Module.Control;
        void EstablishInvalidState()
        {
            if (mode == ValidationMode.OnBlur)
            {
                control.Blur(cut);
            }
            else
            {
                host.Module.FormElement.SubmitAll(cut);
            }
        }

        control.Focused(cut);
        control.Input(cut, "invalid", Native.Valid);
        EstablishInvalidState();

        FieldValidityState last = cut.Instance.ValidityCalls[^1];
        Assert.Equal("invalid", last.Value);
        Assert.True(last.Validity.CustomError);
        Assert.False(last.Validity.ValueMissing);
        Assert.Equal(1, calls);

        control.Focused(cut);
        control.Input(cut, string.Empty, Native.ValueMissing);

        last = cut.Instance.ValidityCalls[^1];
        Assert.Equal(string.Empty, last.Value);
        Assert.Equal(mode == ValidationMode.OnSubmit, last.Validity.CustomError);
        Assert.True(last.Validity.ValueMissing);
        Assert.Equal("Required", cut.Find("div > div").TextContent);
        Assert.Equal(mode == ValidationMode.OnBlur ? 1 : 2, calls);

        EstablishInvalidState();

        last = cut.Instance.ValidityCalls[^1];
        Assert.Equal(string.Empty, last.Value);
        Assert.Equal(mode == ValidationMode.OnSubmit, last.Validity.CustomError);
        Assert.True(last.Validity.ValueMissing);
        Assert.Equal("Required", cut.Find("div > div").TextContent);
    }

    [Fact(DisplayName = "defers badInput during required change revalidation")]
    [Trait("Requirement", "FLD-FRM-13")]
    [Trait("Requirement", "FFI-FRM-05")]
    public void Defers_badInput_during_required_change_revalidation()
    {
        IRenderedComponent<FieldErrorScenarios> cut = Render(p => p
            .Add(x => x.ValidationMode, ValidationMode.OnBlur)
            .Add(x => x.Validate, _ => new ValueTask<FieldValidationResult?>("custom error")));
        FormModuleStandIn.Attached control = host.Module.Control;

        control.Focused(cut);
        control.Input(cut, "1", Native.Valid);
        control.Blur(cut);
        Assert.Equal("1", cut.Instance.ValidityCalls[^1].Value);
        Assert.True(cut.Instance.ValidityCalls[^1].Validity.CustomError);

        control.Focused(cut);
        control.Input(cut, string.Empty, s_badInputAndMissing);

        FieldValidityState last = cut.Instance.ValidityCalls[^1];
        Assert.Equal("1", last.Value);
        Assert.False(last.Validity.ValueMissing);
        Assert.False(last.Validity.BadInput);
        Assert.Empty(cut.FindAll("div > div"));
    }

    [Fact(DisplayName = "should pass validity data")]
    [Trait("Requirement", "FLD-API-35")]
    [Trait("Requirement", "FLD-STA-01")]
    [Trait("Requirement", "FLD-STA-07")]
    [Trait("Requirement", "FLD-FRM-03")]
    public void Should_pass_validity_data_in_onSubmit_mode()
    {
        IRenderedComponent<FieldErrorScenarios> cut = Render();
        FormModuleStandIn.Attached control = host.Module.Control;
        control.Native = Native.ValueMissing;
        Assert.Null(cut.Instance.ValidityCalls[^1].Validity.Valid);

        host.Module.FormElement.SubmitAll(cut);

        FieldValidityState last = cut.Instance.ValidityCalls[^1];
        Assert.False(last.Validity.Valid);
        Assert.True(last.Validity.ValueMissing);
        Assert.Equal(TransitionStatus.Starting, last.TransitionStatus);

        control.Focused(cut);
        control.Input(cut, "test", Native.Valid);

        last = cut.Instance.ValidityCalls[^1];
        Assert.Equal("test", last.Value);
        Assert.True(last.Validity.Valid);
        Assert.False(last.Validity.ValueMissing);
    }

    [Fact(DisplayName = "should pass validity data")]
    [Trait("Requirement", "FLD-API-35")]
    [Trait("Requirement", "FLD-STA-07")]
    [Trait("Requirement", "FLD-FRM-03")]
    public void Should_pass_validity_data_in_onBlur_mode()
    {
        IRenderedComponent<FieldErrorScenarios> cut = Render(p => p.Add(x => x.ValidationMode, ValidationMode.OnBlur));
        FormModuleStandIn.Attached control = host.Module.Control;
        Assert.Null(cut.Instance.ValidityCalls[^1].Validity.Valid);

        control.Focused(cut);
        control.Input(cut, "test", Native.Valid);
        control.Blur(cut);

        FieldValidityState last = cut.Instance.ValidityCalls[^1];
        Assert.Equal("test", last.Value);
        Assert.True(last.Validity.Valid);
        Assert.False(last.Validity.ValueMissing);
    }

    [Fact(DisplayName = "should correctly pass errors when validate function returns a string")]
    [Trait("Requirement", "FLD-STA-07")]
    [Trait("Requirement", "FFI-FRM-07")]
    public void Should_correctly_pass_errors_when_validate_function_returns_a_string()
    {
        IRenderedComponent<FieldErrorScenarios> cut = Render(p => p
            .Add(x => x.ValidationMode, ValidationMode.OnBlur)
            .Add(x => x.Validate, _ => new ValueTask<FieldValidationResult?>("error")));

        host.Module.Control.Focused(cut);
        host.Module.Control.Blur(cut);

        FieldValidityState last = cut.Instance.ValidityCalls[^1];
        Assert.Equal("error", last.Error);
        Assert.Equal(["error"], last.Errors);
    }

    [Fact(DisplayName = "should correctly pass errors when validate function returns an array of strings")]
    [Trait("Requirement", "FLD-STA-07")]
    [Trait("Requirement", "FFI-API-01")]
    public void Should_correctly_pass_errors_when_validate_function_returns_an_array_of_strings()
    {
        IRenderedComponent<FieldErrorScenarios> cut = Render(p => p
            .Add(x => x.ValidationMode, ValidationMode.OnBlur)
            .Add(x => x.Validate, _ => new ValueTask<FieldValidationResult?>(new[] { "1", "2" })));

        host.Module.Control.Focused(cut);
        host.Module.Control.Blur(cut);

        FieldValidityState last = cut.Instance.ValidityCalls[^1];
        Assert.Equal("1", last.Error);
        Assert.Equal(["1", "2"], last.Errors);
    }

    // FLD-STA-07: the validity folds the field's external invalid state (the invalid parameter, a form error).
    [Fact(DisplayName = "folds the invalid parameter into validity.valid")]
    [Trait("Requirement", "FLD-STA-07")]
    [Trait("Requirement", "FFI-ALG-14")]
    public void Folds_the_invalid_parameter_into_validity_valid()
    {
        IRenderedComponent<FieldErrorScenarios> cut = Render(p => p.Add(x => x.Invalid, true));

        FieldValidityState state = cut.Instance.ValidityCalls[^1];
        Assert.False(state.Validity.Valid);
        Assert.Equal(string.Empty, state.Error);
        Assert.Empty(state.Errors);
        Assert.Null(state.TransitionStatus);
    }

    // FLD-STA-08, FFI-EDGE-12: unrelated changes keep the argument; every publish replaces it, even with equal values.
    [Fact(DisplayName = "keeps the argument across unrelated field changes and replaces it on every publish")]
    [Trait("Requirement", "FLD-STA-08")]
    [Trait("Requirement", "FFI-EDGE-12")]
    public void Keeps_the_argument_across_unrelated_field_changes_and_replaces_it_on_every_publish()
    {
        IRenderedComponent<FieldErrorScenarios> cut = Render(p => p.Add(x => x.ValidationMode, ValidationMode.OnBlur));
        FormModuleStandIn.Attached control = host.Module.Control;
        FieldValidityState initial = cut.Instance.ValidityCalls[^1];

        control.Focused(cut);
        control.Input(cut, "a", Native.Valid);
        Assert.Same(initial, cut.Instance.ValidityCalls[^1]);

        control.Blur(cut);
        FieldValidityState first = cut.Instance.ValidityCalls[^1];
        Assert.NotSame(initial, first);
        Assert.True(first.Validity.Valid);

        control.Focused(cut);
        control.Blur(cut);
        Assert.NotSame(first, cut.Instance.ValidityCalls[^1]);
        Assert.Equal(first.Validity, cut.Instance.ValidityCalls[^1].Validity);
    }

    // FLD-ANI-06, FLD §16 item 1 (followed, design point E2 of PR 1c): no element and no frame in C#, so the next frame is
    // one frame's time on the TimeProvider; the 'ending' status is latched until the field is invalid again.
    [Fact(DisplayName = "follows the invalid state through starting, unset and a latched ending")]
    [Trait("Requirement", "FLD-ANI-06")]
    public void Follows_the_invalid_state_through_starting_unset_and_a_latched_ending()
    {
        IRenderedComponent<FieldErrorScenarios> cut = Render(p => p
            .Add(x => x.ValidationMode, ValidationMode.OnChange)
            .Add(x => x.Validate, arguments => new ValueTask<FieldValidationResult?>((string?)arguments.Value == "bad" ? "error" : null)));
        FormModuleStandIn.Attached control = host.Module.Control;
        Assert.Null(cut.Instance.ValidityCalls[^1].TransitionStatus);

        control.Input(cut, "bad", Native.Valid);
        Assert.Equal(TransitionStatus.Starting, cut.Instance.ValidityCalls[^1].TransitionStatus);

        host.Clock.Advance(TimeSpan.FromMilliseconds(16));
        Assert.Null(cut.Instance.ValidityCalls[^1].TransitionStatus);

        control.Input(cut, "good", Native.Valid);
        Assert.Equal(TransitionStatus.Ending, cut.Instance.ValidityCalls[^1].TransitionStatus);

        host.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(TransitionStatus.Ending, cut.Instance.ValidityCalls[^1].TransitionStatus);

        control.Input(cut, "bad", Native.Valid);
        Assert.Equal(TransitionStatus.Ending, cut.Instance.ValidityCalls[^1].TransitionStatus);

        host.Clock.Advance(TimeSpan.FromMilliseconds(16));
        Assert.Null(cut.Instance.ValidityCalls[^1].TransitionStatus);
    }

    // FLD-ANI-06: a field invalid on the part's first render starts with the status unset.
    [Fact(DisplayName = "starts with the status unset when the field is invalid on its first render")]
    [Trait("Requirement", "FLD-ANI-06")]
    public void Starts_with_the_status_unset_when_the_field_is_invalid_on_its_first_render()
    {
        IRenderedComponent<FieldErrorScenarios> cut = Render(p => p.Add(x => x.Invalid, true));

        Assert.Null(cut.Instance.ValidityCalls[^1].TransitionStatus);
        host.Clock.Advance(TimeSpan.FromMilliseconds(16));
        Assert.Null(cut.Instance.ValidityCalls[^1].TransitionStatus);
    }

    [Fact(DisplayName = "renders only what its function returns")]
    [Trait("Requirement", "FLD-STR-05")]
    [Trait("Requirement", "FLD-API-35")]
    public void Renders_only_what_its_function_returns()
    {
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p
            .AddChildContent<FieldValidity>(v => v.Add(x => x.ChildContent, state => builder => builder.AddMarkupContent(0, $"<span>{state.Validity.Valid?.ToString() ?? "unknown"}</span>"))));

        Assert.Equal("<span>unknown</span>", cut.Find("div").InnerHtml);
    }

    [Fact(DisplayName = "throws outside a Field.Root")]
    [Trait("Requirement", "FLD-STR-02")]
    [Trait("Requirement", "FLD-EDGE-01")]
    public void Throws_outside_a_Field_Root()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Context.Render<FieldValidity>(v => v.Add(x => x.ChildContent, state => builder => { })));

        Assert.Contains("FieldRoot", error.Message, StringComparison.Ordinal);
    }

    private IRenderedComponent<FieldErrorScenarios> Render(Action<ComponentParameterCollectionBuilder<FieldErrorScenarios>>? parameters = null) =>
        Context.Render<FieldErrorScenarios>(p =>
        {
            p.Add(x => x.Scenario, Scenario.Validity);
            parameters?.Invoke(p);
        });
}
