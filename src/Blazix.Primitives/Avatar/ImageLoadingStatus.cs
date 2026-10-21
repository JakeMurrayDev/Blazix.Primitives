namespace Blazix.Primitives;

// AVA §3.4; avatar/root/AvatarRoot.tsx:44. ADR 08 §3 (Enums row; Amendments 2026-10-21): the PascalCase of Base UI's
// literals, no sentinel member. Declared beside its first consumer, the Avatar family.
/// <summary>Specifies the loading status of an avatar's image.</summary>
public enum ImageLoadingStatus
{
    /// <summary>Specifies that no image has reported a status yet, or that the image was removed.</summary>
    Idle,

    /// <summary>Specifies that the image is loading.</summary>
    Loading,

    /// <summary>Specifies that the image has loaded and can be shown.</summary>
    Loaded,

    /// <summary>Specifies that the image failed to load or has no source.</summary>
    Error,
}
