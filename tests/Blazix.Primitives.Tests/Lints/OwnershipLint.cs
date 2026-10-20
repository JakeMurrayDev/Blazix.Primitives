using System.Reflection;

namespace Blazix.Primitives.Tests.Lints;

/// <summary>Skeleton for the red run.</summary>
internal static class OwnershipLint
{
    public static OwnershipReport Check(
        IReadOnlyDictionary<string, DocumentParse> documents,
        IReadOnlyList<RegistryEntry> registry,
        Func<string, bool> hasFolder,
        IReadOnlyCollection<string> mirroredFields) => new([], [], [], 0);

    public static IReadOnlyCollection<string> MirroredFields(Assembly assembly) => [];
}
