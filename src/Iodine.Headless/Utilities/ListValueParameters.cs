using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Utilities;

// ADR 08 Amendments 2026-11-03 (1) and the probe's finding of 2026-11-04 (spikes/list-value-shape/): a value-typed list root
// (SelectRoot, ComboboxRoot, AutocompleteRoot) takes a single-mode and a multiple-mode triple, `Multiple` choosing the live
// one, and a supplied parameter of the other triple is an always-on error (ADR 02 Amendments 2026-10-20). The parameter view
// names what the consumer supplied before any parameter is assigned, so the root calls this first in SetParametersAsync.
/// <summary>Checks that a list root's supplied value parameters belong to the triple its <c>Multiple</c> chooses.</summary>
internal static class ListValueParameters
{
    private const string Multiple = "Multiple";

    /// <summary>Throws when the parameters supply a value parameter of the triple <c>Multiple</c> does not choose.</summary>
    /// <param name="parameters">The parameters the root receives, before it assigns them.</param>
    /// <param name="rootName">The root's part name, such as <c>SelectRoot</c>, for the message.</param>
    /// <exception cref="InvalidOperationException">
    /// <c>Multiple</c> is <see langword="true"/> and a single-mode parameter is supplied, or <c>Multiple</c> is not
    /// <see langword="true"/> and a multiple-mode parameter is supplied.
    /// </exception>
    public static void Check(in ParameterView parameters, string rootName)
    {
        bool multiple = false;
        string? single = null;
        string? many = null;
        foreach (ParameterValue parameter in parameters)
        {
            switch (parameter.Name)
            {
                case Multiple:
                    multiple = parameter.Value is true;
                    break;
                case "Value" or "ValueChanged" or "DefaultValue" or "OnValueChange" or "ValueExpression":
                    single ??= parameter.Name;
                    break;
                case "Values" or "ValuesChanged" or "DefaultValues" or "OnValuesChange" or "ValuesExpression":
                    many ??= parameter.Name;
                    break;
            }
        }

        if (multiple && single is not null)
        {
            throw new InvalidOperationException($"Blazix: `{rootName}` received `{single}` while `Multiple` is true. In multiple mode, use `Values`, `ValuesChanged` (or `@bind-Values`), `DefaultValues` and `OnValuesChange`.");
        }

        if (!multiple && many is not null)
        {
            throw new InvalidOperationException($"Blazix: `{rootName}` received `{many}` while `Multiple` is false. Set `Multiple=\"true\"`, or use `Value`, `ValueChanged` (or `@bind-Value`), `DefaultValue` and `OnValueChange` for one value.");
        }
    }

    // ADR 08 Amendments 2026-11-18 (the shared exclusive-triple check; settled by the orchestrator 2026-11-18): a root whose
    // single and list parameters are exclusive and that has no Multiple flag, SliderRoot's shape.
    /// <summary>Throws when the parameters supply a parameter of each triple.</summary>
    /// <param name="parameters">The parameters the root receives, before it assigns them.</param>
    /// <param name="rootName">The root's part name, such as <c>SliderRoot</c>, for the message.</param>
    /// <exception cref="InvalidOperationException">A single-value parameter and a list parameter are both supplied.</exception>
    public static void CheckExclusive(in ParameterView parameters, string rootName)
    {
        string? single = null;
        string? many = null;
        foreach (ParameterValue parameter in parameters)
        {
            switch (parameter.Name)
            {
                case "Value" or "ValueChanged" or "DefaultValue" or "OnValueChange" or "ValueExpression" or "OnValueCommitted":
                    single ??= parameter.Name;
                    break;
                case "Values" or "ValuesChanged" or "DefaultValues" or "OnValuesChange" or "ValuesExpression" or "OnValuesCommitted":
                    many ??= parameter.Name;
                    break;
            }
        }

        if (single is not null && many is not null)
        {
            throw new InvalidOperationException($"Blazix: `{rootName}` received `{single}` and `{many}`. Use `Value`, `ValueChanged` (or `@bind-Value`), `DefaultValue` and `OnValueChange` for one value, or `Values`, `ValuesChanged` (or `@bind-Values`), `DefaultValues` and `OnValuesChange` for a list.");
        }
    }
}
