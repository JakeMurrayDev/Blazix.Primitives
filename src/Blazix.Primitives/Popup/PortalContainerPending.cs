namespace Blazix.Primitives;

// ADR 04 Amendments 2026-10-27, decision 5; ADR 08 Amendments 2026-10-27: Base UI's `container={null}`, "render nothing
// until a container is supplied" (FLT-API-13), the named case of PortalContainerValue.
/// <summary>Represents the pending case of <see cref="PortalContainerValue"/>: the portal renders nothing until a container is supplied.</summary>
/// <remarks>Write it as <see cref="PortalContainerValue.Pending"/>.</remarks>
public readonly struct PortalContainerPending;
