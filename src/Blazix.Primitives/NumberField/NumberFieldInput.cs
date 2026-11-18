using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// Base UI's NumberField.Input (number-field/input/NumberFieldInput.tsx:114-130); the contract is docs/components/number-field.md
// §3.6 and §5.3. A text input (NUM-DOM-08, NUM-ARIA-01): the labelable control id (NUM-DOM-09), the display text, the
// inputmode of NUM-STA-19, autocomplete, autocorrect and spellcheck off, the root's required, disabled and read-only states,
// `aria-roledescription` (a consumer value wins at the merge, NUM-API-22), never `name` (NUM-DOM-18).
// - The `number-field` module (6a, design point 2 (b), settled by the orchestrator 2026-11-18) owns the input's `value` from
//   attach (C# → JS, design point 7 (a)): C# renders the first text, then sends the text with the echoed revision. Focus and
//   blur reach the root as the module's reports (ADR 04 Amendments 2026-10-18 item 1). The consumer's `onfocus` and `onblur`
//   are declined and run by the part: on WebAssembly inside the `input-focus` / `input-blur` decision, where
//   PreventPartHandler() stops the report; on Server from the part's own handler, a round trip after the module reported
//   (ADR 01's C#-origin veto row).
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
    private readonly EventCallback onFocus;
    private readonly EventCallback onBlur;
    private readonly Action<EventSnapshot> focused;
    private readonly Action<NumberFieldBlurReport> blurred;
    private NumberFieldRootContext? context;
    private NumberFieldTwin? twin;
    private TwinHandle<NumberFieldOptions>? handle;
    private NumberFieldOptions? sentOptions;
    private string? renderedValue;
    private AttributeValue consumerFocus;
    private AttributeValue consumerBlur;
    private bool focusDecided;
    private bool blurDecided;
    private NumberFieldInputState? state;
    private int stateVersion = -1;

    /// <summary>Initializes a new instance of the <see cref="NumberFieldInput"/> class.</summary>
    public NumberFieldInput()
    {
        stateChanged = StateHasChanged;
        onFocus = CreateHandler<FocusEventArgs>("onfocus", e => RunConsumerAsync(e, consumerFocus, ref focusDecided));
        onBlur = CreateHandler<FocusEventArgs>("onblur", e => RunConsumerAsync(e, consumerBlur, ref blurDecided));
        focused = snapshot => _ = InvokeAsync(() => context!.Root.HandleInputFocus());
        blurred = report => _ = InvokeAsync(() => context!.Root.HandleInputBlurAsync(report.Text, report.Revision, report.Event));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

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
    protected override void OnParametersSet()
    {
        consumerFocus = default;
        consumerBlur = default;
        if (AdditionalAttributes is null)
        {
            return;
        }

        foreach (KeyValuePair<string, object> pair in AdditionalAttributes)
        {
            bool focus = string.Equals(pair.Key, "onfocus", StringComparison.OrdinalIgnoreCase);
            if (!focus && !string.Equals(pair.Key, "onblur", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            AttributeValue value = AttributeValue.FromObject(pair.Value);
            if (!MergeProps.IsHandler(pair.Key, value))
            {
                continue;
            }

            if (focus)
            {
                consumerFocus = value;
            }
            else
            {
                consumerBlur = value;
            }
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!Enabled || Element is not { } element)
        {
            return;
        }

        NumberFieldRoot root = context!.Root;
        string text = root.InputValue;
        int revision = root.EchoedRevision;
        if (handle is null)
        {
            twin ??= CreateTwin();
            sentOptions = new NumberFieldOptions { Value = text, Revision = revision };
            handle = await twin.AttachAsync(element, sentOptions);
            return;
        }

        // Compared before constructing, so an unchanged render allocates no options record.
        if (sentOptions is { } sent && string.Equals(sent.Value, text, StringComparison.Ordinal) && sent.Revision == revision)
        {
            return;
        }

        sentOptions = new NumberFieldOptions { Value = text, Revision = revision };
        await handle.UpdateAsync(sentOptions);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            context?.RemoveListener(stateChanged);
            context?.Root.SetInputMounted(false);
            if (twin is not null)
            {
                twin.InputFocused -= focused;
                twin.InputBlurred -= blurred;
                twin.FocusDecider = null;
                twin.BlurDecider = null;
                _ = twin.DisposeAsync().AsTask();
                twin = null;
                handle = null;
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName)
    {
        return !string.Equals(eventName, "onfocus", StringComparison.OrdinalIgnoreCase) && !string.Equals(eventName, "onblur", StringComparison.OrdinalIgnoreCase);
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
        // The handover at attach: the first text is rendered, then the module writes it.
        if (handle is null)
        {
            renderedValue = rootState.InputValue;
        }

        attributes.Set("value", renderedValue);
        if (consumerFocus.IsHandler)
        {
            attributes.Set("onfocus", onFocus);
        }

        if (consumerBlur.IsHandler)
        {
            attributes.Set("onblur", onBlur);
        }
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

    private NumberFieldTwin CreateTwin()
    {
        NumberFieldTwin created = new(JSRuntime)
        {
            FocusDecider = snapshot => Decide(snapshot, consumerFocus, ref focusDecided),
            BlurDecider = snapshot => Decide(snapshot, consumerBlur, ref blurDecided),
        };
        created.InputFocused += focused;
        created.InputBlurred += blurred;
        return created;
    }

    // The decision, on WebAssembly only: the consumer's handler runs inside the dispatch, so its PreventPartHandler() keeps
    // the report back; the marker keeps Blazor's own dispatch of the same event from running it twice.
    private DecisionPolicy Decide(EventSnapshot snapshot, AttributeValue consumer, ref bool decided)
    {
        if (!consumer.IsHandler)
        {
            return DecisionPolicy.Run;
        }

        FocusEventArgs args = new() { Type = snapshot.Type };
        decided = true;
        try
        {
            Task task = consumer.InvokeAsync(args);
            if (task.IsFaulted)
            {
                _ = DispatchExceptionAsync(task.Exception!.GetBaseException());
            }
        }
        catch (Exception exception)
        {
            _ = DispatchExceptionAsync(exception);
        }

        return args.PartHandlerPrevented() ? DecisionPolicy.Stop : DecisionPolicy.Run;
    }

    private Task RunConsumerAsync(FocusEventArgs e, AttributeValue consumer, ref bool decided)
    {
        bool pending = decided;
        decided = false;
        return !consumer.IsHandler || pending ? Task.CompletedTask : consumer.InvokeAsync(e);
    }
}
