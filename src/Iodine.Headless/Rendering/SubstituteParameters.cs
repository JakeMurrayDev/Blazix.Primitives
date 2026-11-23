using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Rendering;

// ADR 02 decision 1, design point 14.8 of M3 track 4 (settled by the orchestrator 2026-10-27): a substitute whose root is a
// component receives the part's merged attributes as parameters, and its own parameters keep the objects Razor passed. An
// attribute whose name the component declares as a parameter is one of those, even when it is shaped like a handler
// (`OnPressedChange`, an EventCallback<T>), so it is never merged into the handler chain and re-emitted untyped. Each
// component type's names are read once.
/// <summary>Provides the parameter names a component type declares, for the frame rewrite of a component root.</summary>
internal static class SubstituteParameters
{
    private static readonly ConcurrentDictionary<Type, HashSet<string>> s_declared = new();

    /// <summary>Determines whether a component type declares a parameter of a given name.</summary>
    /// <param name="componentType">The type of the substitute's root component.</param>
    /// <param name="name">The attribute name; compared ignoring case, as Blazor binds parameters.</param>
    /// <returns><see langword="true"/> when a public property of the type carries <see cref="ParameterAttribute"/> under that name.</returns>
    public static bool Declares([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type componentType, string name)
    {
        if (!s_declared.TryGetValue(componentType, out HashSet<string>? names))
        {
            names = s_declared.GetOrAdd(componentType, Read(componentType));
        }

        return names.Contains(name);
    }

    private static HashSet<string> Read([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type componentType)
    {
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        foreach (PropertyInfo property in componentType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.IsDefined(typeof(ParameterAttribute), inherit: true))
            {
                names.Add(property.Name);
            }
        }

        return names;
    }
}
