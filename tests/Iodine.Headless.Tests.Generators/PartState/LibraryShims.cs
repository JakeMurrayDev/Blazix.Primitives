namespace Blazix.Primitives.Tests.Generators.PartState;

/// <summary>
/// The library types the generated code references, as source: the generator tests compile against the testing SDK's
/// reference assemblies and never against the library, so the contract the generated code targets is written out here
/// and must match <c>src/Blazix.Primitives/Rendering</c> member for member.
/// </summary>
internal static class LibraryShims
{
    public const string Source = """
        #nullable enable
        namespace Blazix.Primitives.Rendering
        {
            [global::System.AttributeUsage(global::System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
            public sealed class PartStateAttribute : global::System.Attribute
            {
            }

            [global::System.AttributeUsage(global::System.AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
            public sealed class MirroredFieldAttribute : global::System.Attribute
            {
            }

            [global::System.AttributeUsage(global::System.AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
            public sealed class NotProjectedAttribute : global::System.Attribute
            {
            }

            public interface IPartState<TSelf>
                where TSelf : IPartState<TSelf>
            {
                void ProjectStateAttributes(StateAttributesMapping<TSelf>? mapping, StateAttributeWriter writer);
            }

            public readonly struct StateAttributeWriter
            {
                public void Set(string name, string value)
                {
                }
            }

            public sealed class StateAttributesMapping<TState>
            {
                public bool TryProject<TValue>(string field, TValue value, StateAttributeWriter writer) => false;
            }
        }
        """;
}
