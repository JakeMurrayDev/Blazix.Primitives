using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Blazix.Primitives;

// FLD-STA-01, FFI-STA-01; field/root/FieldRoot.tsx:112-122,239-264 (Base UI's order; projection order is declaration order).
// The verdict's mapping is FieldValidityMapping (FLD-DOM-03): data-valid for true, data-invalid for false, nothing for null.
/// <summary>Represents the state of a field root, which every part of the field shares and exposes as data attributes.</summary>
/// <param name="Disabled">A value indicating whether the field is disabled, by its own parameter or an enclosing fieldset; projected as <c>data-disabled</c>.</param>
/// <param name="Touched">A value indicating whether the control has lost focus, or Enter was pressed in it, at least once; projected as <c>data-touched</c>.</param>
/// <param name="Dirty">A value indicating whether the value differs from the initial value; projected as <c>data-dirty</c>.</param>
/// <param name="Valid">
/// <see langword="true"/> when the field is valid, projected as <c>data-valid</c>; <see langword="false"/> when it is
/// invalid, projected as <c>data-invalid</c>; <see langword="null"/> before validation, while an asynchronous validator
/// runs, or while the field is disabled and not invalid for another reason, projected as neither.
/// </param>
/// <param name="Filled">A value indicating whether the control has a value; projected as <c>data-filled</c>.</param>
/// <param name="Focused">A value indicating whether the control has focus; projected as <c>data-focused</c>.</param>
[PartState]
public sealed partial record FieldRootState(bool Disabled, bool Touched, bool Dirty, bool? Valid, bool Filled, bool Focused);

// FLD-DOM-02/03; field/root/FieldRootDataAttributes.ts.
/// <summary>Provides the names of the data attributes a field root renders.</summary>
public static class FieldRootDataAttributes
{
    /// <summary>Represents the attribute present while the field is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";

    /// <summary>Represents the attribute present while the field is valid: <c>data-valid</c>.</summary>
    public const string Valid = "data-valid";

    /// <summary>Represents the attribute present while the field is invalid: <c>data-invalid</c>.</summary>
    public const string Invalid = "data-invalid";

    /// <summary>Represents the attribute present once the field has been touched: <c>data-touched</c>.</summary>
    public const string Touched = "data-touched";

    /// <summary>Represents the attribute present while the field's value differs from its initial value: <c>data-dirty</c>.</summary>
    public const string Dirty = "data-dirty";

    /// <summary>Represents the attribute present while the field has a value: <c>data-filled</c>.</summary>
    public const string Filled = "data-filled";

    /// <summary>Represents the attribute present while the field's control has focus: <c>data-focused</c>.</summary>
    public const string Focused = "data-focused";
}

// Base UI's Field.Root (field/root/FieldRoot.tsx); the contracts are docs/components/field.md and
// docs/architecture/field-and-form-infrastructure.md. FLD-STR-01, FFI-STR-01/03: it owns the field scope and the root
// labelable scope, cascaded fixed around its element (ADR 16 rule 9; the contexts design point, pending the user).
// FLD-API-01/02: Name and Disabled never reach the element. FLD-API-07: Dirty and Touched are bool? (null: the field owns
// the flag). FLD-API-06: ValidationDebounceTime is Duration? (ADR 08 Amendments 2026-10-21). FLD-API-08: Base UI's
// actionsRef is ValidateAsync(), reached by @ref (ADR 08 Amendments 2026-10-22).
/// <summary>
/// Represents a field: Base UI's <c>Field.Root</c>, rendered as a <c>div</c>. It groups a control with its label,
/// description and error message, tracks whether the control is touched, dirty, filled, focused and valid, and validates
/// the control's value.
/// </summary>
/// <remarks>
/// <para>
/// The control is a <see cref="FieldControl"/>, an <see cref="Input"/> or another form control placed inside the field.
/// Every part of the field renders the field's state as <c>data-disabled</c>, <c>data-touched</c>, <c>data-dirty</c>,
/// <c>data-filled</c>, <c>data-focused</c>, and <c>data-valid</c> or <c>data-invalid</c>.
/// </para>
/// <para>
/// The field validates with the control's native constraints (<c>required</c>, <c>pattern</c>, <c>type</c> and so on)
/// and with <see cref="Validate"/>, at the moment <see cref="ValidationMode"/> names.
/// </para>
/// </remarks>
public sealed class FieldRoot : PartBase<FieldRootState>
{
    private const string DefaultTag = "div";

    private static readonly StateAttributesMapping<FieldRootState> s_mapping = FieldValidityMapping.Create<FieldRootState>();

    private readonly RenderFragment renderPart;
    private readonly Action contextChanged;
    private readonly Action fieldsetChanged;
    private FieldRootContext? context;
    private FieldsetRootContext? fieldset;
    private FieldRootState? state;
    private FieldStateSnapshot renderedSnapshot;

    /// <summary>Initializes a new instance of the <see cref="FieldRoot"/> class.</summary>
    public FieldRoot()
    {
        renderPart = RenderPart;
        contextChanged = OnContextChanged;
        fieldsetChanged = OnFieldsetChanged;
    }

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [Inject]
    private IServiceProvider Services { get; set; } = null!;

    [CascadingParameter]
    private FieldsetRootContext? FieldsetContext { get; set; }

    [CascadingParameter]
    private IFormScope? FormScope { get; set; }

    // FLD-API-01
    /// <summary>Gets or sets the name that identifies the field in a form.</summary>
    /// <value>
    /// The name the form's values and errors use, which the control renders as its <c>name</c> attribute; or
    /// <see langword="null"/>, the default, to use the control's own name. It takes precedence over the control's name.
    /// </value>
    [Parameter]
    public string? Name { get; set; }

    // FLD-API-02
    /// <summary>Gets or sets a value indicating whether the field ignores user interaction.</summary>
    /// <value>
    /// <see langword="true"/> to disable every part of the field and its control; otherwise, <see langword="false"/>, the
    /// default. An enclosing disabled fieldset disables the field whatever this value is.
    /// </value>
    [Parameter]
    public bool Disabled { get; set; }

    // FLD-API-03
    /// <summary>Gets or sets a value indicating whether the field is invalid, whatever its own validation says.</summary>
    /// <value>
    /// <see langword="true"/> to mark the field invalid, as a form library might; <see langword="false"/>, the default,
    /// leaves the verdict to the field's validation. An invalid field stays invalid while disabled and blocks a form's
    /// submission.
    /// </value>
    [Parameter]
    public bool Invalid { get; set; }

    // FLD-API-07
    /// <summary>Gets or sets a value indicating whether the field's value differs from its initial value.</summary>
    /// <value>
    /// <see langword="true"/> or <see langword="false"/> to control the dirty state from outside, which user input then
    /// does not change; <see langword="null"/>, the default, to let the field track it.
    /// </value>
    [Parameter]
    public bool? Dirty { get; set; }

    // FLD-API-07
    /// <summary>Gets or sets a value indicating whether the field has been touched.</summary>
    /// <value>
    /// <see langword="true"/> or <see langword="false"/> to control the touched state from outside, which a blur then does
    /// not change; <see langword="null"/>, the default, to let the field track it.
    /// </value>
    [Parameter]
    public bool? Touched { get; set; }

    // FLD-API-04, FFI-API-01, FFI-FRM-11..14
    /// <summary>Gets or sets the custom validator.</summary>
    /// <value>
    /// A function that receives the value and the form's values and returns the error messages, or <see langword="null"/>
    /// when the value is valid; or <see langword="null"/>, the default, for no custom validation. A task that completes
    /// asynchronously makes the field's validity unknown while it runs, and does not keep a form from submitting.
    /// </value>
    /// <remarks>
    /// The validator runs after the native constraints pass, or on every change while the field validates on change. Its
    /// messages are installed as the control's custom validity, so the browser's own validation reports them too. When it
    /// throws, the field keeps its previous verdict.
    /// </remarks>
    [Parameter]
    public Func<FieldValidationArguments, ValueTask<FieldValidationResult?>>? Validate { get; set; }

    // FLD-API-05, FFI-API-02
    /// <summary>Gets or sets when the field validates.</summary>
    /// <value>
    /// The mode; or <see langword="null"/>, the default, to use the enclosing form's mode, and
    /// <see cref="ValidationMode.OnSubmit"/> outside a form.
    /// </value>
    [Parameter]
    public ValidationMode? ValidationMode { get; set; }

    // FLD-API-06, FFI-API-03
    /// <summary>Gets or sets how long a change waits before it validates.</summary>
    /// <value>
    /// The delay, as a number of milliseconds or a <see cref="TimeSpan"/>, restarted by every change; or
    /// <see langword="null"/>, the default, for no delay. It applies only while the field validates on change, and not
    /// when the value becomes empty.
    /// </value>
    [Parameter]
    public Duration? ValidationDebounceTime { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override FieldRootState State
    {
        get
        {
            FieldStateSnapshot snapshot = context!.State;
            if (state is null || snapshot != renderedSnapshot)
            {
                renderedSnapshot = snapshot;
                state = new FieldRootState(snapshot.Disabled, snapshot.Touched, snapshot.Dirty, snapshot.Valid, snapshot.Filled, snapshot.Focused);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<FieldRootState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        TimeProvider timeProvider = Services.GetService<TimeProvider>() ?? TimeProvider.System;
        context = new FieldRootContext(IdGenerator.Next(), timeProvider, InvokeAsync, DispatchExceptionAsync);
        context.AddListener(contextChanged);
        fieldset = FieldsetContext;
        fieldset?.AddListener(fieldsetChanged);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        using FieldRootContext.BatchScope batch = context!.Batch();
        context.SetForm(FormScope);
        context.SetFieldsetDisabled(fieldset?.Disabled ?? false);
        context.SetParameters(Name, Disabled, Invalid, Dirty, Touched, Validate, ValidationMode, ValidationDebounceTime?.ToMilliseconds() ?? 0);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<FieldRootContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<FieldRootContext>.Value), context);
        builder.AddComponentParameter(2, nameof(CascadingValue<FieldRootContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<FieldRootContext>.ChildContent), renderPart);
        builder.CloseComponent();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            fieldset?.RemoveListener(fieldsetChanged);
            if (context is not null)
            {
                context.RemoveListener(contextChanged);
                context.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    // FLD-API-08, FFI-API-06, FFI-FRM-21.
    /// <summary>Validates the field now, whatever its validation mode.</summary>
    /// <returns>
    /// A task that completes when the validation finished, including an asynchronous validator. It faults with the
    /// validator's exception; the field then keeps its previous verdict.
    /// </returns>
    /// <remarks>
    /// The field's value counts as changed for this validation, so a required value that is missing makes the field
    /// invalid. Without a control, the value the field last validated is validated again. This is Base UI's
    /// <c>actionsRef.current.validate()</c>; reach it through <c>@ref</c>.
    /// </remarks>
    public Task ValidateAsync() => context!.ValidateAsync();

    private void RenderPart(RenderTreeBuilder builder) => base.BuildRenderTree(builder);

    // The root renders again only when its own state changed.
    private void OnContextChanged()
    {
        if (state is not null && context!.State == renderedSnapshot)
        {
            return;
        }

        StateHasChanged();
    }

    private void OnFieldsetChanged() => context!.SetFieldsetDisabled(fieldset!.Disabled);
}
