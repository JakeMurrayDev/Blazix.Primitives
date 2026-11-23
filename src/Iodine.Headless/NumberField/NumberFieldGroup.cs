using Iodine.Headless.Rendering;
using Microsoft.AspNetCore.Components;

namespace Iodine.Headless;

// Base UI's NumberField.Group (number-field/group/NumberFieldGroup.tsx); the contract is docs/components/number-field.md.
// Structural only (NUM-STR-04): `role="group"` (NUM-ARIA-09), the root's state attributes, no handler and no id of its own.
/// <summary>
/// Represents the element that groups a number field's input with its stepper buttons: Base UI's <c>NumberField.Group</c>,
/// rendered as a <c>div</c> with <c>role="group"</c>. It must be placed within a <see cref="NumberFieldRoot"/>.
/// </summary>
public sealed class NumberFieldGroup : PartBase<NumberFieldGroupState>
{
    private const string DefaultTag = "div";

    private static readonly StateAttributesMapping<NumberFieldGroupState> s_mapping = FieldValidityMapping.Create<NumberFieldGroupState>();

    private readonly Action stateChanged;
    private NumberFieldRootContext? context;
    private NumberFieldGroupState? state;
    private int stateVersion = -1;

    /// <summary>Initializes a new instance of the <see cref="NumberFieldGroup"/> class.</summary>
    public NumberFieldGroup()
    {
        stateChanged = StateHasChanged;
    }

    [CascadingParameter]
    private NumberFieldRootContext? RootContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override NumberFieldGroupState State
    {
        get
        {
            // NUM-STA-02: the root's state, rebuilt only when the root made a new one.
            if (state is null || stateVersion != context!.StateVersion)
            {
                NumberFieldRootState root = context!.State;
                stateVersion = context.StateVersion;
                state = new NumberFieldGroupState(root.Value, root.InputValue, root.Required, root.Disabled, root.ReadOnly, root.Scrubbing, root.Touched, root.Dirty, root.Valid, root.Filled, root.Focused);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<NumberFieldGroupState>? StateAttributesMapping => s_mapping;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The group is not placed within a <see cref="NumberFieldRoot"/>.</exception>
    protected override void OnInitialized()
    {
        context = NumberFieldRootContext.Require(RootContext);
        context.AddListener(stateChanged);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            context?.RemoveListener(stateChanged);
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("role", "group");
    }
}
