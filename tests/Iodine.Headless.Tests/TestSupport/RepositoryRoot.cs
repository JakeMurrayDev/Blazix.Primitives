namespace Iodine.Headless.Tests.TestSupport;

/// <summary>Finds the repository root from the test binaries, for tests that read <c>docs/</c> as their oracle.</summary>
internal static class RepositoryRoot
{
    public static string Find()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Iodine.Headless.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Iodine.Headless.slnx not found above " + AppContext.BaseDirectory);
    }
}
