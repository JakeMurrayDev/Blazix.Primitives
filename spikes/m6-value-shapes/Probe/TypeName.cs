namespace Probe;

public static class TypeName
{
    // `int?` prints as Nullable`1[Int32]; a nullable reference annotation is not part of the runtime type.
    public static string Of<T>() => Nullable.GetUnderlyingType(typeof(T)) is { } underlying ? underlying.Name + "?" : typeof(T).Name;
}
