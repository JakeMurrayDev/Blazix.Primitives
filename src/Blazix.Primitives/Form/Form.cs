using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// Base UI's Form (form/Form.tsx); the contract is docs/components/form.md and the form scope of
// docs/architecture/field-and-form-infrastructure.md §2. FRM-STR-01, FFI-STR-04: the form scope is cascaded fixed as
// IFormScope around the part's whole output (ADR 16 rule 9, BLZX2001; the M2 contexts design point, settled by the
// orchestrator 2026-10-22), so portaled fields and a substituted root see it. FRM-STA-01: the state is empty.
// FRM-API-05: Base UI's actionsRef is ValidateAsync and ClearErrors, reached by @ref (ADR 08 Amendments 2026-10-22).
// FRM-EVT-01, FFI-ALG-05: the form module prevents every native submit and reports it with the state of every enrolled
// control; HandleSubmitAsync runs the six steps and answers (design points F1, F2 of M2 track 2, settled by the
// orchestrator 2026-10-23). FRM-DOM-02, FRM-DOM-03 (design point F6, settled by the orchestrator 2026-10-23; ADR 04 §3's
// one-way contract): the form draws a token it never renders, which its attach registers the element under and its
// controls carry; a substituted form without an element reference attaches by a generated id it renders (ADR 02 decision 1).
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
    private readonly Action scopeChanged;
    private readonly Func<FormSubmitRequest, Task<FormSubmitAnswer>> submitHandler;
    private readonly TaskCompletionSource attachedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private FormTwin? twin;
    private TwinHandle<FormOptions>? handle;
    private FormOptions? sentOptions;
    private string? substituteId;
    private string? lateId;
    private int seenErrorsVersion;
    private bool focusPending;
    private int focusRevision;
    private IReadOnlyList<string>? focusIds;

    /// <summary>Initializes a new instance of the <see cref="Form"/> class.</summary>
    public Form()
    {
        renderPart = RenderPart;
        scopeChanged = OnScopeChanged;
        submitHandler = HandleSubmitAsync;
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    private string? ConsumerId => AdditionalAttributes is not null && AdditionalAttributes.TryGetValue("id", out object? value) && value is string text ? text : null;

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

    // For the fixtures: completes once the form module is attached, so a browser test can wait for its listener.
    /// <summary>Gets a task that completes when the form's browser-side behavior is attached to its element.</summary>
    /// <value>A task that completes after the first interactive render attached the behavior.</value>
    internal Task TwinAttached => attachedSource.Task;

    // FRM-STA-03, for the tests: the number of submissions started.
    /// <summary>Gets the number of submissions the form has started.</summary>
    /// <value>The submission counter.</value>
    internal int SubmitCount => scope.SubmitCount;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        scope.FormToken = IdGenerator.Next();
        scope.AddListener(scopeChanged);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        scope.SetValidationMode(ValidationMode);
        scope.SetErrors(Errors);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        string? elementId = Element is null ? ConsumerId ?? substituteId ?? lateId : null;
        if (Element is null && elementId is null)
        {
            // A substitute whose root is a component leaves no element reference: render an id, attach next time.
            lateId = IdGenerator.Next();
            StateHasChanged();
            return;
        }

        if (focusPending)
        {
            // FRM-FOC-02, FFI-FOC-03, FFI-FRM-27: the registry already holds the fields' verdicts on the new errors.
            focusPending = false;
            focusRevision++;
            focusIds = InvalidControlIds(out _);
        }

        FormOptions options = new() { Submit = new FormSubmitOptions { Token = scope.FormToken, FocusRevision = focusRevision, Focus = focusIds } };
        if (handle is not null)
        {
            if (options != sentOptions)
            {
                sentOptions = options;
                await handle.UpdateAsync(options).ConfigureAwait(true);
            }

            return;
        }

        twin ??= new FormTwin(JSRuntime) { SubmitHandler = submitHandler };
        sentOptions = options;
        handle = Element is { } element ? await twin.AttachAsync(element, options).ConfigureAwait(true) : await twin.AttachAsync(elementId!, options).ConfigureAwait(true);
        attachedSource.TrySetResult();
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

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            scope.RemoveListener(scopeChanged);
            if (twin is not null)
            {
                twin.SubmitHandler = null;
                _ = twin.DisposeAsync().AsTask();
                twin = null;
                handle = null;
            }
        }

        base.Dispose(disposing);
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

        // A substitute callback leaves no element reference: the form's handle is an id it renders, drawn in the first
        // render (the consumer's id wins), as a part's substitute does under ADR 02 decision 1.
        if (RenderFunction is not null)
        {
            substituteId ??= IdGenerator.Next();
            attributes.Set("id", substituteId);
        }
        else if (lateId is not null)
        {
            attributes.Set("id", lateId);
        }
    }

    // ADR 04 Amendments 2026-10-18 item 1: the form module owns the submit event, so no second Blazor handler runs for it.
    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => !string.Equals(eventName, "onsubmit", StringComparison.Ordinal);

    private void RenderPart(RenderTreeBuilder builder) => base.BuildRenderTree(builder);

    // FRM-EVT-01, FFI-ALG-05: (1) count; (2) validate every entry in registration order, from the state the module read in
    // the submit task (FFI-FRM-04), each commit's synchronous part updating its entry, a pending validator never awaited
    // (FFI-FRM-14); (3) blocked: the invalid entries' controls for the module to focus (FRM-FOC-01), no callback; else (4)
    // the latch, (5) OnSubmit, (6) OnFormSubmit with the values; the native submission is re-issued only when neither
    // OnFormSubmit nor the consumer's veto prevents it (FRM-EVT-02/03).
    private async Task<FormSubmitAnswer> HandleSubmitAsync(FormSubmitRequest request)
    {
        scope.BeginSubmit();
        FieldRegistry registry = scope.Registry;
        foreach (FormControlState state in request.Controls)
        {
            if (registry.TryGet(state.Id, out FieldRegistryEntry entry) && entry.ApplyState is { } apply)
            {
                apply(state.Value, state.Validity);
            }
            else if (registry.TryGetInput(state.Id, out FieldValidationInput input))
            {
                input.Report(state.Validity);
            }
        }

        string[] ids = [.. registry.Ids];
        foreach (string id in ids)
        {
            if (registry.TryGet(id, out FieldRegistryEntry entry))
            {
                Observe(entry.Validate());
            }
        }

        IReadOnlyList<string> focus = InvalidControlIds(out bool blocked);
        if (blocked)
        {
            return new FormSubmitAnswer { Verdict = FormSubmitAnswer.BlockedVerdict, Focus = focus };
        }

        scope.SetSubmitted();
        GenericEventDetails details = new(EventReason.None, request.Event);
        FormSubmitEventArgs submitArgs = new(registry.BuildValues(), details);
        await OnSubmit.InvokeAsync(submitArgs).ConfigureAwait(true);
        if (OnFormSubmit.HasDelegate)
        {
            await OnFormSubmit.InvokeAsync(new FormSubmitEventArgs(registry.BuildValues(), details)).ConfigureAwait(true);
            return new FormSubmitAnswer { Verdict = FormSubmitAnswer.HandledVerdict };
        }

        return new FormSubmitAnswer { Verdict = submitArgs.NativeSubmitPrevented ? FormSubmitAnswer.HandledVerdict : FormSubmitAnswer.SubmitVerdict };
    }

    // FFI-FOC-01/02: every invalid entry blocks; those with a control are listed, in registration order. A group's control
    // is its representative member when the form blocks (pending the orchestrator, M2 track 6).
    private List<string> InvalidControlIds(out bool anyInvalid)
    {
        anyInvalid = false;
        List<string> ids = [];
        FieldRegistry registry = scope.Registry;
        foreach (string id in registry.Ids)
        {
            if (!registry.TryGet(id, out FieldRegistryEntry entry) || entry.ValidityData.State.Valid != false)
            {
                continue;
            }

            anyInvalid = true;
            if ((entry.ResolveControlId is { } resolve ? resolve() : entry.ControlId) is { } controlId)
            {
                ids.Add(controlId);
            }
        }

        return ids;
    }

    // ADR 16 rule 8: a validator's exception reaches the form's error boundary.
    private void Observe(Task task)
    {
        if (!task.IsCompletedSuccessfully)
        {
            _ = ObserveAsync(task);
        }
    }

    private async Task ObserveAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            await DispatchExceptionAsync(exception).ConfigureAwait(true);
        }
    }

    // FRM-FOC-02, FFI-FOC-03: any change of the error map consumes the submitted latch, whether a new Errors instance or a
    // field's clearErrors; the focus request goes to the module after the render.
    private void OnScopeChanged()
    {
        if (scope.ErrorsVersion == seenErrorsVersion)
        {
            return;
        }

        seenErrorsVersion = scope.ErrorsVersion;
        if (scope.ConsumeSubmitted())
        {
            focusPending = true;
            StateHasChanged();
        }
    }
}
