using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components.Forms;

namespace Probe.Parts;

// Formats what a part received, for the harness's results.txt.
public static class Show
{
    public static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    public static string List(IReadOnlyList<double>? values) => values is null ? "null" : "[" + string.Join(", ", values.Select(Number)) + "]";

    public static string Expression(LambdaExpression? expression) => expression is null ? "none" : expression.Body.ToString();

    // FieldIdentifier.Create is what EditContextParticipation and a validator use (ADR 05 Amendments 2026-11-18 item 2).
    public static string Field<T>(Expression<Func<T>>? expression)
    {
        if (expression is null)
        {
            return "none";
        }

        try
        {
            FieldIdentifier field = FieldIdentifier.Create(expression);
            return $"{field.Model.GetType().Name}.{field.FieldName}";
        }
        catch (Exception exception)
        {
            return $"threw {exception.GetType().Name}: {exception.Message}";
        }
    }
}
