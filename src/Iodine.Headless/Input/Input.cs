using System.Linq.Expressions;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// INP-STA-01, FLD-STA-03 (INP-STR-04); input/Input.tsx:35,42.
/// <summary>Represents the state of an input, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the input is disabled, by its own parameter or its field; projected as <c>data-disabled</c>.</param>
/// <param name="Touched">A value indicating whether the field has been touched; projected as <c>data-touched</c>. Outside a field, always <see langword="false"/>.</param>
/// <param name="Dirty">A value indicating whether the field's value differs from its initial value; projected as <c>data-dirty</c>. Outside a field, always <see langword="false"/>.</param>
/// <param name="Valid">The field's verdict: projected as <c>data-valid</c> for <see langword="true"/>, <c>data-invalid</c> for <see langword="false"/>, and neither for <see langword="null"/>. Outside a field, always <see langword="null"/>.</param>
/// <param name="Filled">A value indicating whether the input has a value; projected as <c>data-filled</c>. Outside a field, always <see langword="false"/>.</param>
/// <param name="Focused">A value indicating whether the input has focus; projected as <c>data-focused</c>. Outside a field, always <see langword="false"/>.</param>
[PartState]
public sealed partial record InputState(bool Disabled, bool Touched, bool Dirty, bool? Valid, bool Filled, bool Focused);

// FLD-DOM-04 (INP-STR-04); input/InputDataAttributes.ts.
/// <summary>Provides the names of the data attributes an input renders.</summary>
public static class InputDataAttributes
{
    /// <summary>Represents the attribute present while the input is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";

    /// <summary>Represents the attribute present while the input's field is valid: <c>data-valid</c>.</summary>
    public const string Valid = "data-valid";

    /// <summary>Represents the attribute present while the input's field is invalid: <c>data-invalid</c>.</summary>
    public const string Invalid = "data-invalid";

    /// <summary>Represents the attribute present once the input's field has been touched: <c>data-touched</c>.</summary>
    public const string Touched = "data-touched";

    /// <summary>Represents the attribute present while the input's value differs from its initial value: <c>data-dirty</c>.</summary>
    public const string Dirty = "data-dirty";

    /// <summary>Represents the attribute present while the input has a value: <c>data-filled</c>.</summary>
    public const string Filled = "data-filled";

    /// <summary>Represents the attribute present while the input has focus: <c>data-focused</c>.</summary>
    public const string Focused = "data-focused";
}

// Base UI's Input (input/Input.tsx, which renders Field.Control unchanged); the contract is docs/components/input.md, which
// adopts Field.Control's requirements (INP-STR-04). Two sealed parts over one FieldControlBehavior (ADR 08 Amendments
// 2026-10-22): neither renders nor derives from the other (ADR 16 rule 10). INP-STR-03, INP-FRM-07: outside a FieldRoot it is
// a plain native input with no registration and no validation; INP-EVT-02: the details are ChangeEventDetails with
// EventReason.None, as FieldControl's.
/// <summary>
/// Represents a native text input that works with fields: Base UI's <c>Input</c>, rendered as an <c>input</c>. Alone it is
/// an ordinary input; inside a <see cref="FieldRoot"/> it is the field's control.
/// </summary>
/// <remarks>
/// <para>
/// The value is the consumer's when <see cref="Value"/> is supplied at the first render (<c>@bind-Value</c> works), and
/// the element's own otherwise, starting from <see cref="DefaultValue"/>. Native attributes such as <c>type</c>,
/// <c>required</c> and <c>pattern</c> pass through; inside a field they drive its validation.
/// </para>
/// <para>
/// Inside an <c>EditForm</c>, or any component that cascades an <c>EditContext</c>, <c>@bind-Value</c> also notifies the
/// context of every change, and inside a field the context's validation messages for the value make the field invalid.
/// </para>
/// </remarks>
public sealed class Input : PartBase<InputState>
{
    private const string DefaultTag = "input";

    private static readonly StateAttributesMapping<InputState> s_mapping = FieldValidityMapping.Create<InputState>();

    private readonly EventCallback onFocus;
    private FieldControlBehavior? behavior;
    private InputState? state;
    private FieldStateSnapshot renderedSnapshot;

    /// <summary>Initializes a new instance of the <see cref="Input"/> class.</summary>
    public Input()
    {
        onFocus = CreateHandler<FocusEventArgs>("onfocus", e => behavior!.HandleFocus(e));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<Input> Logger { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [Inject]
    private DefaultFormScope Formless { get; set; } = null!;

    [CascadingParameter]
    private FieldRootContext? FieldContext { get; set; }

    [CascadingParameter]
    private FieldItemContext? ItemContext { get; set; }

    [CascadingParameter]
    private IFormScope? FormScope { get; set; }

    [CascadingParameter]
    private EditContext? CascadedEditContext { get; set; }

    // FLD-API-15
    /// <summary>Gets or sets the value when the consumer owns it.</summary>
    /// <value>
    /// The value the input shows. Supplying it at the first render makes the input controlled for its lifetime: an
    /// edit is reported through <see cref="ValueChanged"/> and <see cref="OnValueChange"/> and shown only once applied.
    /// </value>
    [Parameter]
    public string? Value { get; set; }

    // ADR 05 §2
    /// <summary>Gets or sets the callback that receives the edited value, for <c>@bind-Value</c>.</summary>
    /// <value>The callback, invoked after <see cref="OnValueChange"/> unless the change was canceled.</value>
    [Parameter]
    public EventCallback<string?> ValueChanged { get; set; }

    // FLD-API-16
    /// <summary>Gets or sets the initial value when the input owns its value.</summary>
    /// <value>
    /// The value the input starts with, or <see langword="null"/>. A later change is applied to the element only while
    /// the user has not edited it.
    /// </value>
    [Parameter]
    public string? DefaultValue { get; set; }

    // FLD-API-17, FLD-EVT-01..05
    /// <summary>Gets or sets the callback invoked on every edit, with the new value and the change's details.</summary>
    /// <value>
    /// The callback. It runs before a field reacts; canceling the details keeps the field from clearing the form's error
    /// for the field and from validating, and, when the consumer owns the value, from applying it through
    /// <see cref="ValueChanged"/>.
    /// </value>
    [Parameter]
    public EventCallback<ValueChangeEventArgs<string>> OnValueChange { get; set; }

    // ADR 05 Amendments 2026-10-22
    /// <summary>Gets or sets the expression of the bound value, which <c>@bind-Value</c> supplies.</summary>
    /// <value>
    /// The expression that identifies the value in a cascaded <c>EditContext</c>, or <see langword="null"/>. Without an
    /// <c>EditContext</c> it does nothing.
    /// </value>
    [Parameter]
    public Expression<Func<string?>>? ValueExpression { get; set; }

    // FLD-API-18
    /// <summary>Gets or sets the input's name.</summary>
    /// <value>
    /// The <c>name</c> attribute, and inside a field the field's name when its <see cref="FieldRoot.Name"/> is not set;
    /// or <see langword="null"/>, the default. A field's name takes precedence.
    /// </value>
    [Parameter]
    public string? Name { get; set; }

    // FLD-API-19
    /// <summary>Gets or sets a value indicating whether the input ignores user interaction.</summary>
    /// <value>
    /// <see langword="true"/> to disable the input, which then leaves a field's validation and the form's values;
    /// otherwise, <see langword="false"/>, the default. A disabled field disables the input whatever this value is.
    /// </value>
    [Parameter]
    public bool Disabled { get; set; }

    // FLD-API-21
    /// <summary>Gets or sets a value indicating whether the input takes focus when the page loads.</summary>
    /// <value><see langword="true"/> to render the <c>autofocus</c> attribute; otherwise, <see langword="false"/>, the default.</value>
    [Parameter]
    public bool AutoFocus { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override InputState State
    {
        get
        {
            FieldStateSnapshot snapshot = behavior!.Snapshot;
            if (state is null || snapshot != renderedSnapshot)
            {
                renderedSnapshot = snapshot;
                state = new InputState(snapshot.Disabled, snapshot.Touched, snapshot.Dirty, snapshot.Valid, snapshot.Filled, snapshot.Focused);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<InputState>? StateAttributesMapping => s_mapping;

    // For the fixtures: completes once the form module is attached, so a browser test can wait for its listeners.
    /// <summary>Gets a task that completes when the input's browser-side behavior is attached to its element.</summary>
    /// <value>A task that completes after the first interactive render attached the behavior.</value>
    internal Task TwinAttached => behavior!.TwinAttached;

    // ADR 05 Amendments 2026-10-18 item 1: the mode is fixed by the first parameter set, which arrives before OnInitialized.
    /// <inheritdoc />
    public override Task SetParametersAsync(ParameterView parameters)
    {
        behavior ??= new FieldControlBehavior(nameof(Input), IdGenerator.Next(), Formless, JSRuntime, Logger, StateHasChanged, InvokeAsync, DispatchExceptionAsync);
        behavior.SetParameters(parameters);
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();
        behavior!.Initialize(FieldContext, ItemContext);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        behavior!.SetParameters(
            new FieldControlParameters(Name, Disabled, AutoFocus, DefaultValue, ValueChanged, OnValueChange, ValueExpression, CascadedEditContext, AdditionalAttributes, Enabled),
            FormScope);
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.AfterRenderAsync(Element);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            behavior?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddAttributes(attributes, onFocus);

    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes) => behavior!.FinishAttributes(attributes);

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => behavior!.ForwardsConsumerHandler(eventName);
}
