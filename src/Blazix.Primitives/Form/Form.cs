using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives;

// Base UI's Form (form/Form.tsx); the contract is docs/components/form.md and the form scope of
// docs/architecture/field-and-form-infrastructure.md §2. FRM-STR-01, FFI-STR-04: the form scope is cascaded fixed as
// IFormScope around the part's whole output (ADR 16 rule 9, BLZX2001; the M2 contexts design point, settled by the
// orchestrator 2026-10-22), so portaled fields and a substituted root see it. FRM-STA-01: the state is empty.
// FRM-API-05: Base UI's actionsRef is ValidateAsync and ClearErrors, reached by @ref (ADR 08 Amendments 2026-10-22).
/// <summary>
/// Represents a form that validates its fields and collects their values: Base UI's <c>Form</c>, rendered as a
/// <c>form</c>. Every <see cref="FieldRoot"/> inside it, including one rendered elsewhere in the page through a portal,
/// takes part in it.
/// </summary>
/// <remarks>
/// <para>
/// On every submission the form validates each field. When one is invalid, the submission is blocked and the first invalid
/// control receives focus; otherwise <see cref="OnSubmit"/> runs, then <see cref="OnFormSubmit"/> with the fields' values.
/// </para>
/// <para>
/// <see cref="Errors"/> shows errors the form receives from elsewhere, typically a server, on the fields with those names.
/// A field's error goes away as soon as its value changes.
/// </para>
/// </remarks>
public sealed class Form : PartBase<EmptyState>
{
    private const string DefaultTag = "form";

    private readonly RenderFragment renderPart;
    private readonly FormScope scope = new();

    /// <summary>Initializes a new instance of the <see cref="Form"/> class.</summary>
    public Form()
    {
        renderPart = RenderPart;
    }

    // FRM-API-01, FFI-API-02.
    /// <summary>Gets or sets when the form's fields validate.</summary>
    /// <value>
    /// <see cref="ValidationMode.OnSubmit"/>, the default, to validate on submission and then on every change;
    /// <see cref="ValidationMode.OnBlur"/> to validate a field when it loses focus; <see cref="ValidationMode.OnChange"/> to
    /// validate on every change. A field's own <see cref="FieldRoot.ValidationMode"/> takes precedence.
    /// </value>
    [Parameter]
    public ValidationMode ValidationMode { get; set; } = ValidationMode.OnSubmit;

    // FRM-API-02, FRM-EDGE-03.
    /// <summary>Gets or sets the errors the form shows on its fields, by field name.</summary>
    /// <value>
    /// The errors, typically returned by a server after a submission; or <see langword="null"/>, the default, for none.
    /// Give a new instance to replace them: the form keeps the instance it has until the parameter is a different instance.
    /// </value>
    [Parameter]
    public FormErrors? Errors { get; set; }

    // FRM-API-04, FRM-EVT-02.
    /// <summary>Gets or sets the callback invoked when a submission passed validation, before <see cref="OnFormSubmit"/>.</summary>
    /// <value>The callback, which never runs for a blocked submission.</value>
    [Parameter]
    public EventCallback<FormSubmitEventArgs> OnSubmit { get; set; }

    // FRM-API-03, FRM-EVT-03/04.
    /// <summary>Gets or sets the callback invoked with the fields' values when a submission passed validation.</summary>
    /// <value>
    /// The callback. When it is set, the browser does not submit the form: the values are the callback's to send.
    /// </value>
    [Parameter]
    public EventCallback<FormSubmitEventArgs> OnFormSubmit { get; set; }

    // FRM-API-06, FRM-DOM-01.
    /// <summary>Gets or sets a value indicating whether the browser's own constraint validation is turned off.</summary>
    /// <value>
    /// <see langword="true"/>, the default, to render <c>novalidate</c> so that only the form's fields validate;
    /// <see langword="false"/> to let the browser also validate, and show its messages, before the form submits.
    /// </value>
    [Parameter]
    public bool NoValidate { get; set; } = true;

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        scope.SetValidationMode(ValidationMode);
        scope.SetErrors(Errors);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<IFormScope>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<IFormScope>.Value), scope);
        builder.AddComponentParameter(2, nameof(CascadingValue<IFormScope>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<IFormScope>.ChildContent), renderPart);
        builder.CloseComponent();
    }

    // FRM-API-05, FRM-FRM-11/12, FRM-EDGE-01/02, FFI-API-06, FFI-FRM-20.
    /// <summary>Validates the form's fields now, without submitting.</summary>
    /// <param name="fieldName">
    /// The name of the field to validate: the first registered field with that name is validated. <see langword="null"/>
    /// or empty, the default, validates every field.
    /// </param>
    /// <returns>A task that completes when the validations finished.</returns>
    /// <remarks>
    /// The fields show their verdicts as on submission. Nothing is submitted, focused or counted as a submission, and no
    /// callback runs. A name that no field has validates nothing.
    /// </remarks>
    public async Task ValidateAsync(string? fieldName = null)
    {
        FieldRegistry registry = scope.Registry;
        if (string.IsNullOrEmpty(fieldName))
        {
            string[] ids = [.. registry.Ids];
            foreach (string id in ids)
            {
                if (registry.TryGet(id, out FieldRegistryEntry entry))
                {
                    await entry.Validate().ConfigureAwait(true);
                }
            }

            return;
        }

        foreach (string id in registry.Ids)
        {
            if (registry.TryGet(id, out FieldRegistryEntry entry) && string.Equals(entry.Name, fieldName, StringComparison.Ordinal))
            {
                await entry.Validate().ConfigureAwait(true);
                return;
            }
        }
    }

    // FRM-EVT-06, FFI-EVT-03.
    /// <summary>Removes the form's error for a field.</summary>
    /// <param name="name">The field's name; <see langword="null"/>, empty or a name without an error removes nothing.</param>
    /// <remarks>The error stays removed until <see cref="Errors"/> is a new instance.</remarks>
    public void ClearErrors(string? name) => scope.ClearErrors(name);

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        if (NoValidate)
        {
            attributes.Set("novalidate", true);
        }
    }

    // ADR 04 Amendments 2026-10-18 item 1: the form module owns the submit event, so no second Blazor handler runs for it.
    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => !string.Equals(eventName, "onsubmit", StringComparison.Ordinal);

    private void RenderPart(RenderTreeBuilder builder) => base.BuildRenderTree(builder);
}
