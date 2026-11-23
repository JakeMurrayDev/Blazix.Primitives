namespace Blazix.Primitives.Tests;

// FFI §2 (form scope), FFI-EVT-03/04, FFI-FOC-03, FRM-STA-02..04, FRM-EVT-06: the scope a Form cascades, with no renderer.
/// <summary>The form scope: the error map and its clearing, the submission counter and the submitted latch.</summary>
public sealed class FormScopeTests
{
    [Fact(DisplayName = "removes errors upon change")]
    [Trait("Requirement", "FFI-EVT-03")]
    [Trait("Requirement", "FRM-EVT-06")]
    public void Removes_errors_upon_change()
    {
        FormErrors source = new() { ["email"] = "Email is taken", ["name"] = "Name is taken" };
        FormScope scope = new();
        scope.SetErrors(source);

        scope.ClearErrors("email");

        Assert.False(scope.TryGetErrors("email", out _));
        Assert.True(scope.TryGetErrors("name", out IReadOnlyList<string> name));
        Assert.Equal(["Name is taken"], name);
        Assert.True(source.ContainsKey("email"));
    }

    [Fact(DisplayName = "removes errors for every field that changes within a single commit")]
    [Trait("Requirement", "FFI-EVT-03")]
    [Trait("Requirement", "FRM-EVT-06")]
    public void Removes_errors_for_every_field_that_changes_within_a_single_commit()
    {
        FormScope scope = new();
        scope.SetErrors(new FormErrors { ["a"] = "A", ["b"] = "B", ["c"] = "C" });

        scope.ClearErrors("a");
        scope.ClearErrors("b");

        Assert.False(scope.TryGetErrors("a", out _));
        Assert.False(scope.TryGetErrors("b", out _));
        Assert.True(scope.TryGetErrors("c", out _));
    }

    [Fact(DisplayName = "clears nothing for an empty name or a name with no error")]
    [Trait("Requirement", "FRM-EVT-06")]
    [Trait("Requirement", "FFI-EVT-03")]
    public void Clears_nothing_for_an_empty_name_or_an_absent_key()
    {
        FormScope scope = new();
        scope.SetErrors(new FormErrors { ["a"] = "A" });
        int notifications = 0;
        scope.AddListener(() => notifications++);

        scope.ClearErrors(null);
        scope.ClearErrors(string.Empty);
        scope.ClearErrors("absent");

        Assert.Equal(0, notifications);
        Assert.True(scope.TryGetErrors("a", out _));
    }

    [Fact(DisplayName = "keeps cleared keys cleared until the errors identity changes")]
    [Trait("Requirement", "FFI-EVT-04")]
    [Trait("Requirement", "FRM-STA-02")]
    public void Keeps_cleared_keys_cleared_until_the_errors_identity_changes()
    {
        FormErrors first = new() { ["a"] = "A" };
        FormScope scope = new();
        scope.SetErrors(first);
        scope.ClearErrors("a");

        Assert.False(scope.SetErrors(first));
        Assert.False(scope.TryGetErrors("a", out _));

        Assert.True(scope.SetErrors(new FormErrors { ["a"] = "A again" }));
        Assert.True(scope.TryGetErrors("a", out IReadOnlyList<string> messages));
        Assert.Equal(["A again"], messages);
    }

    [Fact(DisplayName = "treats null errors as an empty map")]
    [Trait("Requirement", "FRM-EDGE-03")]
    [Trait("Requirement", "FRM-API-02")]
    public void Treats_null_errors_as_an_empty_map()
    {
        FormScope scope = new();

        scope.SetErrors(null);

        Assert.False(scope.TryGetErrors("a", out IReadOnlyList<string> messages));
        Assert.Empty(messages);
    }

    [Fact(DisplayName = "counts every submission from zero and never resets the count")]
    [Trait("Requirement", "FRM-STA-03")]
    public void Counts_every_submission_from_zero()
    {
        FormScope scope = new();
        Assert.Equal(0, scope.SubmitCount);

        scope.BeginSubmit();
        scope.BeginSubmit();
        scope.SetErrors(new FormErrors());

        Assert.Equal(2, scope.SubmitCount);
    }

    [Fact(DisplayName = "consumes the submitted latch on the next change of the error map only")]
    [Trait("Requirement", "FRM-STA-04")]
    [Trait("Requirement", "FFI-FOC-03")]
    public void Consumes_the_submitted_latch_on_the_next_error_map_change_only()
    {
        FormScope scope = new();
        scope.SetErrors(new FormErrors { ["a"] = "A" });
        Assert.False(scope.SetErrors(new FormErrors { ["b"] = "B" }) && scope.ConsumeSubmitted());

        scope.SetSubmitted();
        Assert.True(scope.SetErrors(new FormErrors { ["c"] = "C" }) && scope.ConsumeSubmitted());
        Assert.False(scope.SetErrors(new FormErrors { ["d"] = "D" }) && scope.ConsumeSubmitted());

        // A clearErrors call that removes a key is a change of the map too.
        scope.SetSubmitted();
        scope.ClearErrors("d");
        Assert.True(scope.ConsumeSubmitted());
        Assert.False(scope.ConsumeSubmitted());
    }

    [Fact(DisplayName = "a change that leaves the map unchanged does not consume the latch")]
    [Trait("Requirement", "FFI-FOC-03")]
    public void A_change_that_leaves_the_map_unchanged_does_not_consume_the_latch()
    {
        FormErrors errors = new() { ["a"] = "A" };
        FormScope scope = new();
        scope.SetErrors(errors);
        scope.SetSubmitted();

        scope.ClearErrors("absent");
        scope.SetErrors(errors);

        Assert.True(scope.ConsumeSubmitted());
    }

    [Fact(DisplayName = "notifies fields only when the error map or the mode changes")]
    [Trait("Requirement", "FRM-API-01")]
    [Trait("Requirement", "FRM-STA-02")]
    public void Notifies_fields_only_when_the_error_map_or_the_mode_changes()
    {
        FormErrors errors = new() { ["a"] = "A" };
        FormScope scope = new();
        int notifications = 0;
        scope.AddListener(() => notifications++);

        scope.SetErrors(errors);
        scope.SetErrors(errors);
        scope.SetValidationMode(ValidationMode.OnSubmit);
        scope.BeginSubmit();
        scope.SetSubmitted();
        Assert.Equal(1, notifications);

        scope.SetValidationMode(ValidationMode.OnChange);
        Assert.Equal(ValidationMode.OnChange, scope.ValidationMode);
        scope.ClearErrors("a");
        Assert.Equal(3, notifications);
    }

    [Fact(DisplayName = "accepts registry entries, unlike the default scope")]
    [Trait("Requirement", "FRM-STR-01")]
    [Trait("Requirement", "FRM-STA-05")]
    public void Accepts_registry_entries()
    {
        FormScope scope = new();
        FieldRegistryEntry entry = new("email", () => "a@b.co", FieldValidityData.Initial, () => Task.CompletedTask, "email-input");

        scope.Registry.Set("email-input", entry);

        Assert.Equal(["email-input"], scope.Registry.Ids);
        Assert.Equal("a@b.co", scope.Registry.BuildValues()["email"]);
    }
}
