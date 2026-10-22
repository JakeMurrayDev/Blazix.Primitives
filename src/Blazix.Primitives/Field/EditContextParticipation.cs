using System.Linq.Expressions;
using Microsoft.AspNetCore.Components.Forms;

namespace Blazix.Primitives;

// ADR 05 Amendments 2026-10-22 (chosen by the user; verification item 5, issue 76): a form control inside a cascaded
// EditContext, with Blazor's expression parameter (`ValueExpression`, `CheckedExpression`), builds its FieldIdentifier once
// per parameter set, calls NotifyFieldChanged on every committed change, defaults its name to the identifier's field name,
// and, inside a field, feeds the context's messages for the identifier into the field's external-error slot on
// OnValidationStateChanged. Without the context or the expression it is inert. Shared by FieldControl, Input and the
// controls of tracks 4-8, each with its own expression parameter.
/// <summary>The participation of one form control in a cascaded <see cref="EditContext"/>.</summary>
internal sealed class EditContextParticipation : IDisposable
{
    private static readonly string[] s_none = [];

    private readonly Action<IReadOnlyList<string>> messagesChanged;
    private readonly EventHandler<ValidationStateChangedEventArgs> validationStateChanged;
    private EditContext? editContext;
    private LambdaExpression? expression;
    private FieldIdentifier identifier;

    /// <summary>Initializes a new instance of the <see cref="EditContextParticipation"/> class.</summary>
    /// <param name="messagesChanged">Receives the context's messages for the control after they may have changed.</param>
    public EditContextParticipation(Action<IReadOnlyList<string>> messagesChanged)
    {
        this.messagesChanged = messagesChanged;
        validationStateChanged = OnValidationStateChanged;
    }

    /// <summary>Gets a value indicating whether the control has both a cascaded context and an expression.</summary>
    /// <value><see langword="true"/> while the control participates.</value>
    public bool IsActive => editContext is not null && expression is not null;

    // ADR 05 Amendments 2026-10-22 item 2, as InputBase defaults its name.
    /// <summary>Gets the name the control uses when it has none of its own.</summary>
    /// <value>The identifier's field name while the control participates; otherwise, <see langword="null"/>.</value>
    public string? FieldName => IsActive ? identifier.FieldName : null;

    /// <inheritdoc />
    /// <remarks>Stops listening to the context.</remarks>
    public void Dispose()
    {
        if (editContext is not null)
        {
            editContext.OnValidationStateChanged -= validationStateChanged;
            editContext = null;
        }
    }

    /// <summary>Applies the control's cascaded context and expression; called once per parameter set.</summary>
    /// <typeparam name="TField">The type of the bound value.</typeparam>
    /// <param name="context">The cascaded context, or <see langword="null"/>.</param>
    /// <param name="valueExpression">The expression of the bound value, or <see langword="null"/>.</param>
    public void Update<TField>(EditContext? context, Expression<Func<TField>>? valueExpression)
    {
        if (!ReferenceEquals(context, editContext))
        {
            if (editContext is not null)
            {
                editContext.OnValidationStateChanged -= validationStateChanged;
            }

            editContext = context;
            if (editContext is not null)
            {
                editContext.OnValidationStateChanged += validationStateChanged;
            }
        }

        if (!ReferenceEquals(valueExpression, expression))
        {
            expression = valueExpression;
            if (valueExpression is not null)
            {
                identifier = FieldIdentifier.Create(valueExpression);
            }
        }

        ReportMessages();
    }

    // ADR 05 Amendments 2026-10-22 item 1.
    /// <summary>Tells the context that the control's value changed.</summary>
    public void NotifyChanged()
    {
        if (IsActive)
        {
            editContext!.NotifyFieldChanged(identifier);
        }
    }

    private void OnValidationStateChanged(object? sender, ValidationStateChangedEventArgs e) => ReportMessages();

    private void ReportMessages()
    {
        if (!IsActive)
        {
            messagesChanged(s_none);
            return;
        }

        messagesChanged([.. editContext!.GetValidationMessages(identifier)]);
    }
}
