using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// Base UI's NumberField.Increment (number-field/increment/NumberFieldIncrement.tsx over root/useNumberFieldStepperButton.ts); the
// contract is docs/components/number-field.md §3.7. One NumberFieldStepper composed (ADR 16 rule 10).
/// <summary>
/// Represents a stepper button that increases a number field's value: Base UI's <c>NumberField.Increment</c>, rendered as a
/// <c>button</c> labeled <c>Increase</c>. It must be placed within a <see cref="NumberFieldRoot"/>.
/// </summary>
/// <remarks>
/// The button is out of the tab order, since the input's arrow keys do the same, and stays focusable while disabled. It is
/// disabled by its own <see cref="Disabled"/>, by the field, and while the value is at the field's maximum.
/// </remarks>
public sealed class NumberFieldIncrement : PartBase<NumberFieldIncrementState>
{
    private const string DefaultTag = "button";

    private static readonly StateAttributesMapping<NumberFieldIncrementState> s_mapping = FieldValidityMapping.Create<NumberFieldIncrementState>();

    private readonly Action rootChanged;
    private readonly EventCallback onKeyDown;
    private readonly EventCallback onKeyUp;
    private NumberFieldStepper? stepper;
    private NumberFieldRootContext? context;
    private NumberFieldIncrementState? state;

    /// <summary>Initializes a new instance of the <see cref="NumberFieldIncrement"/> class.</summary>
    public NumberFieldIncrement()
    {
        rootChanged = OnRootChanged;
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => stepper!.Behavior.HandleKeyDownAsync(e));
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", e => stepper!.Behavior.HandleKeyUpAsync(e));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<NumberFieldIncrement> Logger { get; set; } = null!;

    [Inject]
    private ButtonDevelopmentLog DevelopmentLog { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private NumberFieldRootContext? RootContext { get; set; }

    // NUM-API-23
    /// <summary>Gets or sets a value indicating whether the button is disabled on its own.</summary>
    /// <value><see langword="true"/> to disable the button whatever the field's state; the default is <see langword="false"/>.</value>
    [Parameter]
    public bool Disabled { get; set; }

    // NUM-API-23, UBT-API-02
    /// <summary>Gets or sets a value indicating whether the rendered element is a native <c>button</c>.</summary>
    /// <value>
    /// <see langword="true"/>, the default, for the <c>button</c>; <see langword="false"/> when
    /// <see cref="PartBase{TState}.Render"/> supplies another element, which then gets <c>role="button"</c>.
    /// </value>
    [Parameter]
    public bool NativeButton { get; set; } = true;

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override NumberFieldIncrementState State => state!;

    // UBT: under a substitute callback the mechanism attaches by id (the Button precedent).
    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    /// <inheritdoc />
    protected override StateAttributesMapping<NumberFieldIncrementState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The button is not placed within a <see cref="NumberFieldRoot"/>.</exception>
    protected override void OnInitialized()
    {
        stepper = new NumberFieldStepper(true, JSRuntime, Logger, DevelopmentLog, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown, onKeyUp);
        context = stepper.Initialize(RootContext);
        context.AddListener(rootChanged);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => Update();

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => stepper!.AfterRenderAsync(Enabled, Element, Id);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            context?.RemoveListener(rootChanged);
            stepper?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => stepper!.AddAttributes(attributes, Id);

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => stepper!.Behavior.ForwardsConsumerHandler(eventName);

    private void OnRootChanged()
    {
        Update();
        StateHasChanged();
    }

    // NUM-STA-02: the root's state with `Disabled` replaced by the button's effective disabled, a new record only on a change.
    private void Update()
    {
        stepper!.Update(Disabled, NativeButton, AdditionalAttributes);
        NumberFieldRootState root = context!.State;
        bool disabled = stepper.EffectiveDisabled;
        if (state is { } current
            && Store.Equality.StrictEquals(current.Value, root.Value)
            && current.Value.HasValue == root.Value.HasValue
            && ReferenceEquals(current.InputValue, root.InputValue)
            && current.Required == root.Required
            && current.Disabled == disabled
            && current.ReadOnly == root.ReadOnly
            && current.Scrubbing == root.Scrubbing
            && current.Touched == root.Touched
            && current.Dirty == root.Dirty
            && current.Valid == root.Valid
            && current.Filled == root.Filled
            && current.Focused == root.Focused)
        {
            return;
        }

        state = new NumberFieldIncrementState(root.Value, root.InputValue, root.Required, disabled, root.ReadOnly, root.Scrubbing, root.Touched, root.Dirty, root.Valid, root.Filled, root.Focused);
    }
}
