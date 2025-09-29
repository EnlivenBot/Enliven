namespace ImageSharp.Vibrant;

/// <summary>
/// Represents a color palette extracted from an image.
/// </summary>
public sealed class Palette {
    public Swatch? Vibrant { get; set; }
    public Swatch? Muted { get; set; }
    public Swatch? DarkVibrant { get; set; }
    public Swatch? DarkMuted { get; set; }
    public Swatch? LightVibrant { get; set; }
    public Swatch? LightMuted { get; set; }
}