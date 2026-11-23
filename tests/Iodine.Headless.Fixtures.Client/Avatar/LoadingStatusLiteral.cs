using Iodine.Headless;

namespace Iodine.Headless.Fixtures.Client.AvatarFixtures;

/// <summary>Writes an <see cref="ImageLoadingStatus"/> as Base UI's literal, the form the specs compare.</summary>
internal static class LoadingStatusLiteral
{
    public static string Of(ImageLoadingStatus status) => status switch
    {
        ImageLoadingStatus.Loading => "loading",
        ImageLoadingStatus.Loaded => "loaded",
        ImageLoadingStatus.Error => "error",
        _ => "idle",
    };
}
