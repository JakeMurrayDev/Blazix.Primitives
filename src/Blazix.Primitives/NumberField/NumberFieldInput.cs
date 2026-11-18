using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// Base UI's NumberField.Input (number-field/input/NumberFieldInput.tsx:114-130); the contract is docs/components/number-field.md
// §3.6 and §5.3. A text input (NUM-DOM-08, NUM-ARIA-01): the labelable control id (NUM-DOM-09), the display text, the
// inputmode of NUM-STA-19, autocomplete, autocorrect and spellcheck off, the root's required, disabled and read-only states,
// `aria-roledescription` (a consumer value wins at the merge, NUM-API-22), never `name` (NUM-DOM-18).
/// <summary>
/// Represents the text input of a number field: Base UI's <c>NumberField.Input</c>, rendered as an <c>input type="text"</c>
/// that shows the formatted value. It must be placed within a <see cref="NumberFieldRoot"/>.
/// </summary>
public sealed class NumberFieldInput : PartBase<NumberFieldInputState>
{
    private const string DefaultTag = "input";
    private const string RoleDescription = "Number field";

    private static readonly StateAttributesMapping<NumberFieldInputState> s_mapping = FieldValidityMapping.Create<NumberFieldInputState>();

    private readonly Action stateChanged;
    private NumberFieldRootContext? context;
    private NumberFieldInputState? state;
    private int stateVersion = -1;

    /// <summary>Initializes a new instance of the <see cref="NumberFieldInput"/> class.</summary>
    public NumberFieldInput()
    {
        stateChanged = StateHasChanged;
    }

    [CascadingParameter]
    private NumberFieldRootContext? RootContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override NumberFieldInputState State
    {
        get
        {
            // NUM-STA-02: the root's state, rebuilt only when the root made a new one.
            if (state is null || stateVersion != context!.StateVersion)
            {
                NumberFieldRootState root = context!.State;
                stateVersion = context.StateVersion;
                state = new NumberFieldInputState(root.Value, root.InputValue, root.Required, root.Disabled, root.ReadOnly, root.Scrubbing, root.Touched, root.Dirty, root.Valid, root.Filled, root.Focused);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<NumberFieldInputState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The input is not placed within a <see cref="NumberFieldRoot"/>.</exception>
    protected override void OnInitialized()
    {
        context = NumberFieldRootContext.Require(RootContext);
        context.AddListener(stateChanged);
        context.Root.SetInputMounted(true);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            context?.RemoveListener(stateChanged);
            context?.Root.SetInputMounted(false);
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        NumberFieldRoot root = context!.Root;
        NumberFieldRootState rootState = context.State;
        attributes.Set("id", root.InputId);
        attributes.Set("required", rootState.Required);
        attributes.Set("disabled", rootState.Disabled);
        attributes.Set("readonly", rootState.ReadOnly);
        attributes.Set("inputmode", root.InputMode);
        attributes.Set("value", rootState.InputValue);
        attributes.Set("type", "text");
        attributes.Set("autocomplete", "off");
        attributes.Set("autocorrect", "off");
        attributes.Set("spellcheck", "false");
        attributes.Set("aria-roledescription", RoleDescription);

        // NUM-ARIA-03: the field label's id; a consumer aria-labelledby wins at the merge.
        if (root.Field.FieldLabelId is { } labelId)
        {
            attributes.Set("aria-labelledby", labelId);
        }
    }

    // ADR 02 Amendments 2026-10-21 (FinishAttributes); NUM-DOM-15: the field's validation props come last, so the composed
    // aria-describedby keeps the consumer's ids first and aria-invalid wins over a consumer value (NUM-ARIA-02/04).
    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes)
    {
        NumberFieldField field = context!.Root.Field;
        if (!field.InField)
        {
            return;
        }

        string? consumer = attributes.TryGetValue("aria-describedby", out AttributeValue current) && current.TryGetValue(out string text) ? text : null;
        if (field.DescribedBy(consumer) is { } describedBy)
        {
            attributes.Set("aria-describedby", describedBy);
        }
        else
        {
            attributes.Remove("aria-describedby");
        }

        if (field.AriaInvalid)
        {
            attributes.Set("aria-invalid", "true");
        }
        else
        {
            attributes.Remove("aria-invalid");
        }
    }
}
