namespace Probe;

public union Pet(string, Uri);
public union Num(int, string);

public static class UnionProbe
{
    // nullable union with a null arm
    public static string Nullable(Pet? p) => p switch
    {
        null => "none",
        string s => "s:" + s,
        Uri u => "u:" + u,
    };

    // lifted conversion from int? to a union with an int case
    public static Num Lifted(int? n) => n is int i ? i : "null";

    // generic method over a union-typed parameter
    public static string Generic<T>(T value) where T : struct => value.ToString()!;
}
