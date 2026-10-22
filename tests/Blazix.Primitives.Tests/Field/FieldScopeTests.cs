namespace Blazix.Primitives.Tests;

// The pure parts of the field scopes at L1: FFI-ALG-09 (described-by composition), FFI-ALG-06 (control-id selection),
// FFI-ALG-03's empty-value rule, FFI-API-01 (result normalization) and FFI-API-07 (the values object).
/// <summary>The labelable scope, the values object, the validation result and the change path's empty-value rule.</summary>
public sealed class FieldScopeTests
{
    [Fact(DisplayName = "should preserve user aria-describedby values on the control")]
    [Trait("Requirement", "FFI-ALG-09")]
    [Trait("Requirement", "FLD-ARIA-02")]
    public void Should_preserve_user_aria_describedby_values_on_the_control()
    {
        LabelableScope scope = new("base-ui-1", null, () => { });
        scope.AddMessageId("description");

        Assert.Equal("external-description description", scope.DescribedBy("external-description"));
    }

    [Fact(DisplayName = "composes the parent scope's ids before the scope's own, without duplicates")]
    [Trait("Requirement", "FFI-ALG-09")]
    [Trait("Requirement", "FFI-STR-03")]
    public void Composes_the_parent_scopes_ids_before_the_scopes_own_without_duplicates()
    {
        LabelableScope root = new("base-ui-1", null, () => { });
        LabelableScope item = new("base-ui-2", root, () => { });
        root.AddMessageId("root-description");
        item.AddMessageId("item-description");

        Assert.Equal("a root-description item-description", item.DescribedBy("a root-description"));
        Assert.Null(new LabelableScope("base-ui-3", null, () => { }).DescribedBy(null));
    }

    [Fact(DisplayName = "does not register an empty description id")]
    [Trait("Requirement", "FFI-ARIA-02")]
    [Trait("Requirement", "FLD-EDGE-03")]
    public void Does_not_register_an_empty_description_id()
    {
        int changes = 0;
        LabelableScope scope = new("base-ui-1", null, () => changes++);

        scope.AddMessageId(string.Empty);

        Assert.Empty(scope.MessageIds);
        Assert.Equal(0, changes);
        Assert.Equal("external-description", scope.DescribedBy("external-description"));
    }

    [Fact(DisplayName = "keeps the current control id while it is still registered")]
    [Trait("Requirement", "FFI-ALG-06")]
    public void Keeps_the_current_control_id_while_it_is_still_registered()
    {
        LabelableScope scope = new("base-ui-1", null, () => { });
        object first = new();
        object second = new();

        scope.RegisterControlId(first, "a");
        scope.RegisterControlId(second, "b");
        Assert.Equal("a", scope.ControlId);

        scope.UnregisterControlId(second);
        Assert.Equal("a", scope.ControlId);

        scope.RegisterControlId(second, "b");
        scope.UnregisterControlId(first);
        Assert.Equal("b", scope.ControlId);
    }

    [Fact(DisplayName = "keeps its selection when no claim remains, and resets to its default id on request")]
    [Trait("Requirement", "FFI-ALG-06")]
    [Trait("Requirement", "FFI-FRM-35")]
    public void Keeps_its_selection_when_no_claim_remains()
    {
        LabelableScope scope = new("base-ui-1", null, () => { });
        object control = new();

        scope.RegisterControlId(control, "email");
        scope.UnregisterControlId(control);
        Assert.Equal("email", scope.ControlId);

        scope.ResetControlId();
        Assert.Equal("base-ui-1", scope.ControlId);
    }

    [Fact(DisplayName = "selects a null claim, which suppresses the label's for")]
    [Trait("Requirement", "FFI-ALG-06")]
    [Trait("Requirement", "FFI-ARIA-04")]
    public void Selects_a_null_claim()
    {
        LabelableScope scope = new("base-ui-1", null, () => { });

        scope.RegisterControlId(new object(), null);

        Assert.Null(scope.ControlId);
    }

    [Fact(DisplayName = "shows a required error when a prefilled value is cleared")]
    [Trait("Requirement", "FFI-ALG-03")]
    [Trait("Requirement", "FFI-FRM-02")]
    public void Shows_a_required_error_when_a_prefilled_value_is_cleared()
    {
        FieldHarness harness = new(ValidationMode.OnChange, debounce: 100);
        harness.Mount(value: "value");

        harness.Change(string.Empty, Native.ValueMissing);

        // An empty value commits at once, whatever the debounce.
        Assert.True(harness.AriaInvalid);
        Assert.True(harness.Field.ValidityData.State.ValueMissing);
    }

    [Fact(DisplayName = "normalizes a result: empty messages are dropped and none means valid")]
    [Trait("Requirement", "FFI-API-01")]
    public void Normalizes_a_result()
    {
        FieldValidationResult? none = (string?)null;
        FieldValidationResult? empty = string.Empty;
        FieldValidationResult? list = new[] { "a", string.Empty, "b" };
        FieldValidationResult sequence = new(new List<string?> { null, "c" });

        Assert.Null(none);
        Assert.True(empty!.IsValid);
        Assert.Equal(["a", "b"], list!.Errors);
        Assert.Equal(["c"], sequence.Errors);
    }

    [Fact(DisplayName = "builds the values of named fields in registration order, a repeated name keeping its first position")]
    [Trait("Requirement", "FFI-API-07")]
    [Trait("Requirement", "FFI-FRM-19")]
    public void Builds_the_values_of_named_fields_in_registration_order()
    {
        FieldRegistry registry = new();
        registry.Set("a", Entry("first", "1"));
        registry.Set("b", Entry(null, "unnamed"));
        registry.Set("c", Entry("second", true));
        registry.Set("d", Entry("first", "4"));

        FormValues values = registry.BuildValues();

        Assert.Equal(["first", "second"], values.Keys);
        Assert.Equal("4", values["first"]);
        Assert.True(values.TryGetValue("second", out bool second) && second);
        Assert.False(values.TryGetValue("second", out string? _));
        Assert.False(values.ContainsKey("b"));
    }

    [Fact(DisplayName = "replacing an entry keeps its position; removing and adding moves it to the end")]
    [Trait("Requirement", "FFI-FRM-17")]
    public void Replacing_an_entry_keeps_its_position()
    {
        FieldRegistry registry = new();
        registry.Set("a", Entry("a", "1"));
        registry.Set("b", Entry("b", "2"));

        registry.Set("a", Entry("a", "3"));
        Assert.Equal(["a", "b"], registry.Ids);

        registry.Remove("a");
        registry.Set("a", Entry("a", "4"));
        Assert.Equal(["b", "a"], registry.Ids);
        Assert.Equal(0, FieldRegistry.None.Count);
        FieldRegistry.None.Set("x", Entry("x", "1"));
        Assert.Equal(0, FieldRegistry.None.Count);
    }

    private static FieldRegistryEntry Entry(string? name, object value) => new(name, () => value, FieldValidityData.Initial, () => Task.CompletedTask, null);
}
