namespace Blazix.Primitives.Interop;

// ADR 04 §1 (dom row) and Amendments 2026-10-18 (Avatar convergent write) and 2026-10-20 (owners); CHK-DOM-13,
// UTL-A11Y-01..04, AVA-ALG-01/02, AVA-STA-09/12
/// <summary>
/// The options of the <c>dom</c> module, sent on <c>attach</c> and on every <c>update</c>. Each property turns one
/// primitive on for the attached element; a handle may run several.
/// </summary>
/// <remarks>The module owns no style property, so a part leaves <see cref="TwinOptions.Style"/> unset.</remarks>
internal sealed record DomOptions : TwinOptions
{
    /// <summary>Gets the value of the <c>indeterminate</c> property of the attached checkbox input.</summary>
    /// <value>
    /// The value the module sets on <c>attach</c>, on every <c>update</c> and again inside every native click, which
    /// clears it; <see langword="null"/>, the default, leaves the property alone.
    /// </value>
    public bool? Indeterminate { get; init; }

    /// <summary>Gets a value indicating whether the attached element is a live region whose initial text must be announced.</summary>
    /// <value>
    /// <see langword="true"/> marks the region's text after the next paint, each time the value turns on;
    /// <see langword="false"/>, the default, removes a marker still in place.
    /// </value>
    public bool LiveRegion { get; init; }

    /// <summary>Gets the image whose loading status the module reports.</summary>
    /// <value>The image's source properties, or <see langword="null"/>, the default, for no image.</value>
    public ImageLoadOptions? Image { get; init; }
}

// AVA-API-02/03, AVA-ALG-01/02
/// <summary>The image the <c>dom</c> module loads or watches, with the properties the <c>&lt;img&gt;</c> element receives.</summary>
internal sealed record ImageLoadOptions
{
    /// <summary>Gets the image URL.</summary>
    /// <value>The <c>src</c> value, or <see langword="null"/>.</value>
    public string? Src { get; init; }

    /// <summary>Gets the responsive image candidates.</summary>
    /// <value>The <c>srcset</c> value, or <see langword="null"/>.</value>
    public string? SrcSet { get; init; }

    /// <summary>Gets the responsive image sizes.</summary>
    /// <value>The <c>sizes</c> value, or <see langword="null"/>.</value>
    public string? Sizes { get; init; }

    /// <summary>Gets the CORS mode of the request.</summary>
    /// <value>The <c>crossorigin</c> value, or <see langword="null"/> for no CORS request.</value>
    public string? CrossOrigin { get; init; }

    /// <summary>Gets the referrer policy of the request.</summary>
    /// <value>The <c>referrerpolicy</c> value, or <see langword="null"/>.</value>
    public string? ReferrerPolicy { get; init; }

    /// <summary>Gets a value indicating whether the status is derived from the attached <c>&lt;img&gt;</c> instead of a detached probe.</summary>
    /// <value><see langword="true"/> when the image element is always rendered; the default is <see langword="false"/>.</value>
    public bool KeepMounted { get; init; }

    /// <summary>Gets the id of the element the module hides the moment the image loads, ahead of the render that removes it.</summary>
    /// <value>The fallback element's id, or <see langword="null"/>.</value>
    public string? HideWhenLoaded { get; init; }
}

// AVA-EVT-02 (one report per status change)
/// <summary>The payload of the image-load report.</summary>
/// <param name="Status"><c>loading</c>, <c>loaded</c> or <c>error</c>.</param>
internal sealed record ImageStatusChange(string Status);
