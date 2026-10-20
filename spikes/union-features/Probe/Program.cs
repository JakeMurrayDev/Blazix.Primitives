using Probe;
Console.WriteLine(ClosedUse.Describe(new OpenDetails()));
Console.WriteLine(UnionProbe.Nullable(null));
Console.WriteLine(UnionProbe.Nullable("cat"));
Console.WriteLine(UnionProbe.Nullable(new Uri("https://x.test/")));
Console.WriteLine(UnionProbe.Lifted(null).Value);
Console.WriteLine(UnionProbe.Lifted(3).Value);
Pet? p = default(Pet); Console.WriteLine(p is null ? "null" : "has-value (default struct)");
