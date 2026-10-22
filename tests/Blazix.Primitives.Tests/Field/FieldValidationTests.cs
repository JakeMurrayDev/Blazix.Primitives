using Blazix.Primitives.Interop;

namespace Blazix.Primitives.Tests;

// The validation service at L1 (FFI-ALG-01/03/14, FFI-FRM-01..16, FFI-API-01/03): FieldRoot.test.tsx's titles over the
// C# field scope, with the browser's native state as a stand-in payload (the form module's report) and FakeTimeProvider
// for the debounce. "submit" is FieldHarness.Submit: the form's counter and every registered field's validation, in order.
// The verdict assertions read what the parts render: Valid false is data-invalid and aria-invalid, null is neither.
/// <summary>The field validation service: commits, the change path, asynchronous validators and the debounce.</summary>
public sealed class FieldValidationTests
{
    private static Func<FieldValidationArguments, ValueTask<FieldValidationResult?>> Sync(Func<FieldValidationArguments, FieldValidationResult?> validate) =>
        args => ValueTask.FromResult(validate(args));

    public static TheoryData<string, Func<FieldValidationResult?>> ValidResults => new()
    {
        { "an empty array", () => Array.Empty<string>() },
        { "an undefined", () => null },
        { "an empty string", () => string.Empty },
        { "an array of empty strings", () => new[] { string.Empty, string.Empty } },
    };

    [Theory(DisplayName = "treats $label result as valid")]
    [MemberData(nameof(ValidResults))]
    [Trait("Requirement", "FFI-API-01")]
    [Trait("Requirement", "FLD-API-04")]
    public void Treats_label_result_as_valid(string label, Func<FieldValidationResult?> result)
    {
        Assert.NotNull(label);
        FieldHarness harness = new(ValidationMode.OnChange, Sync(_ => result()), inForm: true, name: "field");
        harness.Mount();

        harness.Change("abc");

        Assert.NotEqual(false, harness.Valid);
        Assert.False(harness.AriaInvalid);
        Assert.Null(harness.Input.OwnMessage);
        Assert.True(harness.Submit());
    }

    [Fact(DisplayName = "accepts synchronous and async validators with no return value")]
    [Trait("Requirement", "FFI-API-01")]
    [Trait("Requirement", "FLD-API-04")]
    public async Task Accepts_synchronous_and_async_validators_with_no_return_value()
    {
        FieldHarness sync = new(ValidationMode.OnChange, _ => ValueTask.FromResult<FieldValidationResult?>(null));
        FieldHarness async = new(ValidationMode.OnChange, async _ => { await Task.Yield(); return null; });
        sync.Mount();
        async.Mount();

        sync.Change("abc");
        await async.Field.ValidateAsync();

        Assert.Equal(true, sync.Valid);
        Assert.Equal(true, async.Valid);
    }

    [Fact(DisplayName = "when not in <Form> the function does not run by default")]
    [Trait("Requirement", "FFI-STR-05")]
    [Trait("Requirement", "FLD-FRM-03")]
    public void When_not_in_Form_the_function_does_not_run_by_default()
    {
        int calls = 0;
        FieldHarness harness = new(validate: Sync(_ => { calls++; return "error"; }));
        harness.Mount();

        harness.Change("abc");
        harness.Blur();

        Assert.Equal(0, calls);
        Assert.Null(harness.Valid);
    }

    [Fact(DisplayName = "runs after native validations")]
    [Trait("Requirement", "FFI-ALG-01")]
    [Trait("Requirement", "FFI-FRM-07")]
    public void Runs_after_native_validations()
    {
        FieldHarness harness = new(validate: Sync(args => (string?)args.Value == "ab" ? "custom error" : null), inForm: true);
        harness.Mount(native: Native.ValueMissing);

        Assert.False(harness.Submit());
        Assert.True(harness.Field.ValidityData.State.ValueMissing);
        Assert.False(harness.Field.ValidityData.State.CustomError);

        harness.Change("ab", Native.Valid);
        Assert.False(harness.Field.ValidityData.State.ValueMissing);
        Assert.True(harness.Field.ValidityData.State.CustomError);
        Assert.Equal("custom error", harness.Field.ValidityData.Error);

        harness.Change(string.Empty, Native.ValueMissing);
        Assert.True(harness.Field.ValidityData.State.ValueMissing);
    }

    [Fact(DisplayName = "should validate the field on submit")]
    [Trait("Requirement", "FFI-FRM-04")]
    [Trait("Requirement", "FLD-FRM-03")]
    public void Should_validate_the_field_on_submit()
    {
        FieldHarness harness = new(validate: Sync(_ => "error"), inForm: true);
        harness.Mount();

        Assert.Equal(string.Empty, harness.Field.ValidityData.Error);
        Assert.False(harness.Submit());
        Assert.Equal("error", harness.Field.ValidityData.Error);
    }

    [Fact(DisplayName = "revalidates on change")]
    [Trait("Requirement", "FFI-FRM-01")]
    [Trait("Requirement", "FLD-FRM-03")]
    public void Revalidates_on_change()
    {
        FieldHarness harness = new(inForm: true);
        harness.Mount(native: Native.ValueMissing);

        Assert.False(harness.Submit());
        Assert.False(harness.Valid);

        harness.Change("http://example", Native.Valid);
        Assert.Equal(true, harness.Valid);
    }

    [Fact(DisplayName = "validates the field on change")]
    [Trait("Requirement", "FFI-FRM-01")]
    [Trait("Requirement", "FLD-ARIA-03")]
    public void Validates_the_field_on_change()
    {
        FieldHarness harness = new(ValidationMode.OnChange, Sync(args => ((string)args.Value!).Length < 3 ? "error" : null));
        harness.Mount();

        harness.Change("t");

        Assert.False(harness.Valid);
        Assert.True(harness.AriaInvalid);
    }

    [Fact(DisplayName = "validates the field on blur")]
    [Trait("Requirement", "FFI-FRM-03")]
    [Trait("Requirement", "FLD-FOC-02")]
    public void Validates_the_field_on_blur()
    {
        FieldHarness harness = new(ValidationMode.OnBlur, Sync(args => ((string)args.Value!).Length < 3 ? "error" : null));
        harness.Mount();

        harness.Change("t");
        Assert.NotEqual(false, harness.Valid);

        harness.Blur();
        Assert.False(harness.Valid);
        Assert.True(harness.AriaInvalid);
    }

    [Fact(DisplayName = "should debounce validation")]
    [Trait("Requirement", "FFI-API-03")]
    [Trait("Requirement", "FFI-FRM-02")]
    [Trait("Requirement", "FLD-FRM-04")]
    public void Should_debounce_validation()
    {
        FieldHarness harness = new(ValidationMode.OnChange, Sync(args => ((string)args.Value!).Length < 3 ? "error" : null), debounce: 100);
        harness.Mount();

        harness.Change("t");
        Assert.False(harness.AriaInvalid);

        harness.Clock.Advance(TimeSpan.FromMilliseconds(99));
        harness.Change("te");
        harness.Clock.Advance(TimeSpan.FromMilliseconds(99));
        Assert.False(harness.AriaInvalid);

        harness.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.True(harness.AriaInvalid);
        Assert.Equal("error", harness.Field.ValidityData.Error);
    }

    [Fact(DisplayName = "drops a pending debounced validation when the control unmounts")]
    [Trait("Requirement", "FFI-FRM-15")]
    [Trait("Requirement", "FLD-FRM-04")]
    public void Drops_a_pending_debounced_validation_when_the_control_unmounts()
    {
        int calls = 0;
        FieldHarness harness = new(ValidationMode.OnChange, Sync(_ => { calls++; return "error"; }), debounce: 100);
        harness.Mount();

        harness.Change("abc");
        harness.Clock.Advance(TimeSpan.FromMilliseconds(99));
        harness.Unmount();
        harness.Clock.Advance(TimeSpan.FromMilliseconds(100));

        Assert.Equal(0, calls);
        Assert.NotEqual(false, harness.Valid);
    }

    [Fact(DisplayName = "keeps a pending debounce armed across the first registration")]
    [Trait("Requirement", "FFI-FRM-02")]
    [Trait("Requirement", "FLD-FRM-04")]
    public void Keeps_a_pending_debounce_armed_across_the_first_registration()
    {
        int calls = 0;
        FieldHarness harness = new(ValidationMode.OnChange, Sync(_ => { calls++; return "error"; }), debounce: 100);
        harness.Field.Validation.InputRef = harness.Input;

        harness.Change("abc");
        harness.Mount();
        harness.Clock.Advance(TimeSpan.FromMilliseconds(100));

        Assert.Equal(1, calls);
        Assert.False(harness.Valid);
    }

    public static TheoryData<ValidationMode> AllModes => new() { ValidationMode.OnSubmit, ValidationMode.OnChange, ValidationMode.OnBlur };

    public static TheoryData<ValidationMode> ChangeAndBlur => new() { ValidationMode.OnChange, ValidationMode.OnBlur };

    [Theory(DisplayName = "publishes neutral validity while a validator is in flight in %s mode")]
    [MemberData(nameof(AllModes))]
    [Trait("Requirement", "FFI-FRM-11")]
    [Trait("Requirement", "FLD-FRM-08")]
    public void Publishes_neutral_validity_while_a_validator_is_in_flight(ValidationMode mode)
    {
        TaskCompletionSource<FieldValidationResult?> pending = new();
        int calls = 0;
        FieldHarness harness = new(mode, _ => { calls++; return new ValueTask<FieldValidationResult?>(pending.Task); }, inForm: true, name: "username");
        harness.Mount();

        harness.Change("taken");
        if (mode == ValidationMode.OnBlur)
        {
            harness.Blur();
        }
        else if (mode == ValidationMode.OnSubmit)
        {
            Assert.True(harness.Submit());
        }

        Assert.Equal(1, calls);
        Assert.Null(harness.Valid);
        Assert.False(harness.AriaInvalid);

        pending.SetResult("Username is taken");

        Assert.False(harness.Valid);
        Assert.True(harness.AriaInvalid);
        Assert.Equal("Username is taken", harness.Field.ValidityData.Error);
    }

    [Theory(DisplayName = "retires a previously valid result to neutral while revalidating in %s mode")]
    [MemberData(nameof(ChangeAndBlur))]
    [Trait("Requirement", "FFI-FRM-11")]
    [Trait("Requirement", "FLD-FRM-08")]
    public void Retires_a_previously_valid_result_to_neutral_while_revalidating(ValidationMode mode)
    {
        List<TaskCompletionSource<FieldValidationResult?>> resolvers = [];
        FieldHarness harness = new(mode, _ =>
        {
            TaskCompletionSource<FieldValidationResult?> next = new();
            resolvers.Add(next);
            return new ValueTask<FieldValidationResult?>(next.Task);
        });
        harness.Mount();

        harness.Change("good");
        if (mode == ValidationMode.OnBlur)
        {
            harness.Blur();
        }

        resolvers[0].SetResult(null);
        Assert.Equal(true, harness.Valid);

        if (mode == ValidationMode.OnBlur)
        {
            harness.Blur();
        }
        else
        {
            harness.Change("taken");
        }

        Assert.Null(harness.Valid);

        resolvers[1].SetResult("Username is taken");
        Assert.False(harness.Valid);
        Assert.Equal("Username is taken", harness.Field.ValidityData.Error);
    }

    [Theory(DisplayName = "keeps a previously resolved error while revalidating in %s mode")]
    [MemberData(nameof(ChangeAndBlur))]
    [Trait("Requirement", "FFI-FRM-11")]
    [Trait("Requirement", "FFI-FRM-14")]
    [Trait("Requirement", "FLD-FRM-08")]
    public void Keeps_a_previously_resolved_error_while_revalidating(ValidationMode mode)
    {
        List<TaskCompletionSource<FieldValidationResult?>> resolvers = [];
        FieldHarness harness = new(mode, _ =>
        {
            TaskCompletionSource<FieldValidationResult?> next = new();
            resolvers.Add(next);
            return new ValueTask<FieldValidationResult?>(next.Task);
        }, inForm: true, name: "username");
        harness.Mount();

        harness.Change("taken");
        if (mode == ValidationMode.OnBlur)
        {
            harness.Blur();
        }

        resolvers[0].SetResult("Username is taken");
        Assert.False(harness.Valid);

        if (mode == ValidationMode.OnBlur)
        {
            harness.Blur();
        }
        else
        {
            harness.Change("taken2");
        }

        Assert.False(harness.Valid);
        Assert.Equal("Username is taken", harness.Field.ValidityData.Error);
        Assert.False(harness.Submit());

        resolvers[^1].SetResult(null);
        Assert.NotEqual(false, harness.Valid);
        Assert.Equal(string.Empty, harness.Field.ValidityData.Error);
    }

    [Fact(DisplayName = "retires a stale native error to neutral once the constraint passes again")]
    [Trait("Requirement", "FFI-FRM-11")]
    [Trait("Requirement", "FLD-FRM-08")]
    public void Retires_a_stale_native_error_to_neutral_once_the_constraint_passes_again()
    {
        List<TaskCompletionSource<FieldValidationResult?>> resolvers = [];
        FieldHarness harness = new(ValidationMode.OnChange, _ =>
        {
            TaskCompletionSource<FieldValidationResult?> next = new();
            resolvers.Add(next);
            return new ValueTask<FieldValidationResult?>(next.Task);
        }, inForm: true, name: "email");
        harness.Mount();

        harness.Change("nope", Native.TypeMismatch);
        resolvers[^1].SetResult(null);
        Assert.False(harness.Valid);

        harness.Change("name@example.com", Native.Valid);
        Assert.Null(harness.Valid);
        Assert.True(harness.Submit());

        resolvers[^1].SetResult(null);
        Assert.Equal(true, harness.Valid);
    }

    [Theory(DisplayName = "keeps a native constraint failure published while the validator is in flight in %s mode")]
    [MemberData(nameof(AllModes))]
    [Trait("Requirement", "FFI-FRM-07")]
    [Trait("Requirement", "FFI-FRM-11")]
    [Trait("Requirement", "FLD-FRM-07")]
    public void Keeps_a_native_constraint_failure_published_while_the_validator_is_in_flight(ValidationMode mode)
    {
        int calls = 0;
        FieldHarness harness = new(mode, _ => { calls++; return new ValueTask<FieldValidationResult?>(new TaskCompletionSource<FieldValidationResult?>().Task); }, inForm: true, name: "username");
        harness.Mount(native: Native.ValueMissing);

        Assert.False(harness.Submit());

        Assert.Equal(mode == ValidationMode.OnBlur ? 0 : 1, calls);
        Assert.False(harness.Valid);
        Assert.True(harness.AriaInvalid);
    }

    [Fact(DisplayName = "retires a resolved async error to neutral while revalidating in onSubmit mode")]
    [Trait("Requirement", "FFI-FRM-11")]
    [Trait("Requirement", "FFI-FRM-14")]
    public void Retires_a_resolved_async_error_to_neutral_while_revalidating_in_onSubmit_mode()
    {
        List<TaskCompletionSource<FieldValidationResult?>> resolvers = [];
        FieldHarness harness = new(validate: _ =>
        {
            TaskCompletionSource<FieldValidationResult?> next = new();
            resolvers.Add(next);
            return new ValueTask<FieldValidationResult?>(next.Task);
        }, inForm: true, name: "username");
        harness.Mount();

        Assert.True(harness.Submit());
        resolvers[0].SetResult("Username is taken");
        Assert.False(harness.Valid);

        Assert.True(harness.Submit());
        Assert.Null(harness.Valid);
        Assert.False(harness.AriaInvalid);

        resolvers[1].SetResult(null);
        Assert.Equal(true, harness.Valid);
    }

    [Fact(DisplayName = "supports async validation")]
    [Trait("Requirement", "FFI-FRM-16")]
    [Trait("Requirement", "FLD-FRM-08")]
    public async Task Supports_async_validation()
    {
        FieldHarness harness = new(ValidationMode.OnBlur, async _ => { await Task.Yield(); return "error"; });
        harness.Mount();

        harness.Blur();
        await harness.Field.ValidateAsync();

        Assert.Equal("error", harness.Field.ValidityData.Error);
    }

    [Fact(DisplayName = "ignores stale async validation results")]
    [Trait("Requirement", "FFI-FRM-12")]
    [Trait("Requirement", "FLD-FRM-08")]
    public void Ignores_stale_async_validation_results()
    {
        Dictionary<string, TaskCompletionSource<FieldValidationResult?>> resolvers = [];
        FieldHarness harness = new(ValidationMode.OnChange, args =>
        {
            TaskCompletionSource<FieldValidationResult?> next = new();
            resolvers[(string)args.Value!] = next;
            return new ValueTask<FieldValidationResult?>(next.Task);
        });
        harness.Mount();

        harness.Change("old");
        harness.Change("new");
        resolvers["new"].SetResult(null);
        Assert.False(harness.AriaInvalid);

        resolvers["old"].SetResult("old error");
        Assert.False(harness.AriaInvalid);
        Assert.NotEqual("old error", harness.Field.ValidityData.Error);
    }

    // FieldControl.test.tsx's title at L1 (FFI-FRM-12): the blur commits the blurred value, the control's re-commit after the
    // render (FLD-FRM-17) commits the normalised one, and the blurred value's late result is stale.
    [Fact(DisplayName = "keeps the final async validation when a controlled value is normalized on blur")]
    [Trait("Requirement", "FFI-FRM-12")]
    public void Keeps_the_final_async_validation_when_a_controlled_value_is_normalized_on_blur()
    {
        Dictionary<string, TaskCompletionSource<FieldValidationResult?>> resolvers = [];
        FieldHarness harness = new(ValidationMode.OnBlur, args =>
        {
            TaskCompletionSource<FieldValidationResult?> next = new();
            resolvers[(string)args.Value!] = next;
            return new ValueTask<FieldValidationResult?>(next.Task);
        });
        harness.Mount();

        harness.Change("foo ");
        harness.Blur();
        harness.Field.Observe(harness.Field.Validation.CommitAsync("foo"));
        Assert.Equal(["foo ", "foo"], resolvers.Keys);

        resolvers["foo"].SetResult("Invalid email");
        Assert.Equal("Invalid email", harness.Field.ValidityData.Error);

        resolvers["foo "].SetResult("Stale error");
        Assert.Equal("Invalid email", harness.Field.ValidityData.Error);
    }

    [Fact(DisplayName = "ignores async validation results superseded during debounce")]
    [Trait("Requirement", "FFI-FRM-12")]
    [Trait("Requirement", "FLD-FRM-04")]
    public void Ignores_async_validation_results_superseded_during_debounce()
    {
        Dictionary<string, TaskCompletionSource<FieldValidationResult?>> resolvers = [];
        string? last = null;
        FieldHarness harness = new(ValidationMode.OnChange, args =>
        {
            last = (string)args.Value!;
            TaskCompletionSource<FieldValidationResult?> next = new();
            resolvers[last] = next;
            return new ValueTask<FieldValidationResult?>(next.Task);
        }, debounce: 100);
        harness.Mount();

        harness.Change("old");
        harness.Clock.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal("old", last);

        harness.Change("new");
        resolvers["old"].SetResult("old error");
        Assert.False(harness.AriaInvalid);

        harness.Clock.Advance(TimeSpan.FromMilliseconds(100));
        resolvers["new"].SetResult(null);
        Assert.Equal("new", last);
        Assert.False(harness.AriaInvalid);
    }

    [Fact(DisplayName = "drops an in-flight async validation when the control unmounts")]
    [Trait("Requirement", "FFI-FRM-12")]
    [Trait("Requirement", "FLD-FRM-08")]
    public void Drops_an_in_flight_async_validation_when_the_control_unmounts()
    {
        TaskCompletionSource<FieldValidationResult?> pending = new();
        FieldHarness harness = new(ValidationMode.OnChange, _ => new ValueTask<FieldValidationResult?>(pending.Task));
        harness.Mount();

        harness.Change("abc");
        harness.Unmount();
        pending.SetResult("error");

        Assert.NotEqual(false, harness.Valid);
        Assert.Equal(string.Empty, harness.Field.ValidityData.Error);
    }

    [Fact(DisplayName = "keeps the published error when async validation rejects")]
    [Trait("Requirement", "FFI-FRM-13")]
    [Trait("Requirement", "FLD-FRM-08")]
    public async Task Keeps_the_published_error_when_async_validation_rejects()
    {
        int calls = 0;
        FieldHarness harness = new(ValidationMode.OnBlur, async _ =>
        {
            calls++;
            await Task.Yield();
            if (calls == 2)
            {
                throw new InvalidOperationException("network");
            }

            return "Username is taken";
        }, inForm: true, name: "username");
        harness.Mount();

        harness.Change("taken");
        await harness.Field.Validation.CommitAsync("taken");
        Assert.Equal("Username is taken", harness.Field.ValidityData.Error);

        InvalidOperationException rejection = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Field.Validation.CommitAsync("taken"));
        Assert.Equal("network", rejection.Message);
        Assert.Equal("Username is taken", harness.Field.ValidityData.Error);
        Assert.True(harness.AriaInvalid);
        Assert.False(harness.Submit());
    }

    [Fact(DisplayName = "routes a faulted validation nobody awaits to the field's error boundary")]
    [Trait("Requirement", "FFI-FRM-13")]
    public async Task Routes_a_faulted_validation_nobody_awaits_to_the_error_boundary()
    {
        FieldHarness harness = new(ValidationMode.OnChange, async _ => { await Task.Yield(); throw new InvalidOperationException("network"); });
        harness.Mount();

        harness.Change("abc");
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Exception exception = Assert.Single(harness.Exceptions);
        Assert.Equal("network", exception.Message);
    }

    [Fact(DisplayName = "revalidates on change for `valueMissing`")]
    [Trait("Requirement", "FFI-FRM-05")]
    [Trait("Requirement", "FLD-FRM-13")]
    public void Revalidates_on_change_for_valueMissing()
    {
        FieldHarness harness = new(ValidationMode.OnBlur);
        harness.Mount(native: Native.ValueMissing);

        harness.Change("t", Native.Valid);
        harness.Blur();
        Assert.False(harness.AriaInvalid);

        harness.Change(string.Empty, Native.ValueMissing);
        harness.Blur();
        Assert.True(harness.AriaInvalid);
    }

    [Fact(DisplayName = "handles both `required` and `typeMismatch`")]
    [Trait("Requirement", "FFI-FRM-05")]
    [Trait("Requirement", "FLD-FRM-13")]
    public void Handles_both_required_and_typeMismatch()
    {
        FieldHarness harness = new(ValidationMode.OnBlur);
        harness.Mount(native: Native.ValueMissing);

        harness.Blur();
        Assert.False(harness.AriaInvalid);

        harness.Change("tt", Native.TypeMismatch);
        harness.Blur();
        Assert.True(harness.AriaInvalid);

        harness.Change(string.Empty, Native.ValueMissing);
        harness.Blur();
        Assert.True(harness.AriaInvalid);

        harness.Change("email@email.com", Native.Valid);
        harness.Blur();
        Assert.False(harness.AriaInvalid);
    }

    [Fact(DisplayName = "revalidates on change when clearing a type mismatch leaves only `valueMissing`")]
    [Trait("Requirement", "FFI-FRM-05")]
    [Trait("Requirement", "FLD-FRM-13")]
    public void Revalidates_on_change_when_clearing_a_type_mismatch_leaves_only_valueMissing()
    {
        FieldHarness harness = new(ValidationMode.OnBlur);
        harness.Mount(native: Native.ValueMissing);

        harness.Change("invalid", Native.TypeMismatch);
        harness.Blur();
        Assert.True(harness.Field.ValidityData.State.TypeMismatch);
        Assert.False(harness.Field.ValidityData.State.ValueMissing);

        harness.Change(string.Empty, Native.ValueMissing);
        Assert.False(harness.Field.ValidityData.State.TypeMismatch);
        Assert.True(harness.Field.ValidityData.State.ValueMissing);
    }

    [Fact(DisplayName = "clears valueMissing on change but defers other native errors like typeMismatch until blur when both are active")]
    [Trait("Requirement", "FFI-FRM-05")]
    [Trait("Requirement", "FLD-FRM-13")]
    public void Clears_valueMissing_on_change_but_defers_other_native_errors_until_blur()
    {
        FieldHarness harness = new(ValidationMode.OnBlur);
        harness.Mount(native: Native.ValueMissing);

        harness.Blur();
        Assert.False(harness.AriaInvalid);

        harness.Change("a", Native.TypeMismatch);
        harness.Change(string.Empty, Native.ValueMissing);
        harness.Blur();
        Assert.True(harness.AriaInvalid);
        Assert.NotEqual(string.Empty, harness.Field.ValidityData.Error);

        harness.Change("t", Native.TypeMismatch);
        Assert.False(harness.AriaInvalid);
        Assert.Equal(string.Empty, harness.Field.ValidityData.Error);

        harness.Blur();
        Assert.True(harness.AriaInvalid);
        Assert.NotEqual(string.Empty, harness.Field.ValidityData.Error);

        harness.Change("test@example.com", Native.Valid);
        Assert.False(harness.AriaInvalid);

        harness.Blur();
        Assert.False(harness.AriaInvalid);
    }

    [Fact(DisplayName = "should not mark invalid if `valueMissing` is the only error and not yet dirtied")]
    [Trait("Requirement", "FFI-FRM-06")]
    [Trait("Requirement", "FLD-FRM-05")]
    public void Should_not_mark_invalid_if_valueMissing_is_the_only_error_and_not_yet_dirtied()
    {
        FieldHarness harness = new(ValidationMode.OnBlur);
        harness.Mount(native: Native.ValueMissing);

        harness.Blur();

        Assert.False(harness.AriaInvalid);
        Assert.NotEqual(false, harness.Valid);
    }

    [Fact(DisplayName = "should mark invalid if `valueMissing` is the only error and dirtied")]
    [Trait("Requirement", "FFI-FRM-06")]
    [Trait("Requirement", "FLD-FRM-05")]
    public void Should_mark_invalid_if_valueMissing_is_the_only_error_and_dirtied()
    {
        FieldHarness harness = new(ValidationMode.OnBlur);
        harness.Mount(native: Native.ValueMissing);

        harness.Change("a", Native.Valid);
        harness.Change(string.Empty, Native.ValueMissing);
        harness.Blur();

        Assert.True(harness.AriaInvalid);
    }

    [Fact(DisplayName = "uses the controlled dirty state for required validation")]
    [Trait("Requirement", "FFI-STA-06")]
    [Trait("Requirement", "FLD-API-07")]
    public void Uses_the_controlled_dirty_state_for_required_validation()
    {
        FieldHarness harness = new(ValidationMode.OnBlur, dirty: true);
        harness.Mount(native: Native.ValueMissing);

        harness.Blur();

        Assert.True(harness.AriaInvalid);
    }

    [Fact(DisplayName = "should mark field as invalid for other errors (e.g., typeMismatch) even if not dirty")]
    [Trait("Requirement", "FFI-FRM-06")]
    [Trait("Requirement", "FLD-FRM-05")]
    public void Should_mark_field_as_invalid_for_other_errors_even_if_not_dirty()
    {
        FieldHarness harness = new(ValidationMode.OnBlur);
        harness.Mount(native: Native.TypeMismatch, value: "not_an_email@");

        harness.Blur();

        Assert.True(harness.AriaInvalid);
    }

    [Fact(DisplayName = "installs the validator's messages as the input's own custom validity, joined by a line feed")]
    [Trait("Requirement", "FFI-FRM-07")]
    [Trait("Requirement", "FFI-FRM-09")]
    public void Installs_the_validators_messages_as_the_inputs_own_custom_validity()
    {
        FieldHarness harness = new(ValidationMode.OnChange, Sync(_ => new[] { "first", "second" }));
        harness.Mount();

        harness.Change("x");

        Assert.Equal("first\nsecond", harness.Input.OwnMessage);
        Assert.Equal(["first", "second"], harness.Field.ValidityData.Errors);
        Assert.Equal("first\nsecond", Assert.Single(harness.SentMessages));
    }

    [Fact(DisplayName = "does not write its own message to a barred control")]
    [Trait("Requirement", "FFI-FRM-08")]
    [Trait("Requirement", "FLD-FRM-06")]
    public void Does_not_write_its_own_message_to_a_barred_control()
    {
        FieldHarness harness = new(ValidationMode.OnChange, Sync(_ => "custom error"));
        harness.Mount(native: new NativeValidity { Valid = true, WillValidate = false });

        harness.Change("x", new NativeValidity { Valid = true, WillValidate = false });

        Assert.Null(harness.Input.OwnMessage);
        Assert.Equal("custom error", harness.Field.ValidityData.Error);
    }

    [Fact(DisplayName = "keeps a message set outside the field when validating on change")]
    [Trait("Requirement", "FFI-FRM-09")]
    [Trait("Requirement", "FLD-FRM-06")]
    public void Keeps_a_message_set_outside_the_field_when_validating_on_change()
    {
        FieldHarness harness = new(ValidationMode.OnChange);
        harness.Mount();

        harness.Change("abc", Native.ForeignCustom("external error"));

        Assert.False(harness.Valid);
        Assert.Equal("external error", harness.Field.ValidityData.Error);
        Assert.Null(harness.Input.OwnMessage);
    }

    [Fact(DisplayName = "withdraws its own message when the validator passes")]
    [Trait("Requirement", "FFI-FRM-09")]
    public void Withdraws_its_own_message_when_the_validator_passes()
    {
        FieldHarness harness = new(ValidationMode.OnChange, Sync(args => (string?)args.Value == "bad" ? "custom error" : null));
        harness.Mount();

        harness.Change("bad");
        harness.Change("good");

        Assert.Null(harness.Input.OwnMessage);
        Assert.Equal(["custom error", null], harness.SentMessages);
    }

    [Fact(DisplayName = "re-installs its own message after someone else replaced it")]
    [Trait("Requirement", "FFI-FRM-09")]
    public void Re_installs_its_own_message_after_someone_else_replaced_it()
    {
        FieldHarness harness = new(ValidationMode.OnChange, Sync(_ => "custom error"));
        harness.Mount();

        harness.Change("a");
        harness.Change("b", Native.ForeignCustom("external error"), ownMessageLost: true);

        Assert.Equal(["custom error", "custom error"], harness.SentMessages);
    }

    [Fact(DisplayName = "should pass validity data")]
    [Trait("Requirement", "FFI-STA-03")]
    [Trait("Requirement", "FFI-FRM-16")]
    [Trait("Requirement", "FFI-ALG-14")]
    public void Should_pass_validity_data()
    {
        FieldHarness harness = new(ValidationMode.OnBlur, Sync(_ => "error"));
        harness.Mount();

        Assert.Null(harness.Field.ValidityData.State.Valid);
        Assert.Null(harness.Field.ValidityData.Value);

        harness.Change("test");
        harness.Blur();

        FieldValidityData data = harness.Field.ValidityData;
        Assert.False(data.State.Valid);
        Assert.True(data.State.CustomError);
        Assert.Equal("error", data.Error);
        Assert.Equal(["error"], data.Errors);
        Assert.Equal("test", data.Value);
        Assert.Equal(string.Empty, data.InitialValue);
    }

    [Fact(DisplayName = "publishes a new validity object even when the values are equal")]
    [Trait("Requirement", "FFI-ALG-14")]
    [Trait("Requirement", "FLD-STA-08")]
    public void Publishes_a_new_validity_object_even_when_the_values_are_equal()
    {
        FieldHarness harness = new(ValidationMode.OnChange);
        harness.Mount();
        harness.Change("a");
        FieldValidityData first = harness.Field.ValidityData;

        harness.Change("a");

        Assert.NotSame(first, harness.Field.ValidityData);
    }

    [Fact(DisplayName = "updates the registry entry before the field's own data")]
    [Trait("Requirement", "FFI-FRM-16")]
    [Trait("Requirement", "FFI-FRM-27")]
    public void Updates_the_registry_entry_before_the_fields_own_data()
    {
        FieldHarness harness = new(validate: Sync(_ => null), inForm: true, invalid: true);
        harness.Mount();

        Assert.False(harness.Submit());
        Assert.True(harness.Form.Registry.TryGet("control", out FieldRegistryEntry entry));
        Assert.False(entry.ValidityData.State.Valid);
        Assert.Equal(true, harness.Field.ValidityData.State.Valid);
    }

    [Fact(DisplayName = "allocates within budget for a synchronous commit whose validator returns null")]
    [Trait("Requirement", "ADR16-7")]
    public void Allocates_within_budget_for_a_synchronous_commit()
    {
        Func<FieldValidationArguments, ValueTask<FieldValidationResult?>> validate = static _ => default;
        FieldHarness harness = new(ValidationMode.OnChange, validate);
        harness.Mount();
        harness.Field.Validation.CommitAsync("warm");

        long before = GC.GetAllocatedBytesForCurrentThread();
        harness.Field.Validation.CommitAsync("value");
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // The new validity data (one object, its flags inline) and the boxed-free FieldValidationArguments.
        Assert.True(allocated <= 160, $"A synchronous commit allocated {allocated} bytes.");
    }
}
