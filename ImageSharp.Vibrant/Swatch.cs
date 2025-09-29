namespace ImageSharp.Vibrant;

/// <summary>
/// Represents a color swatch extracted from an image.
/// </summary>
public sealed class Swatch {
    public Swatch(int r, int g, int b, int population) {
        R = r;
        G = g;
        B = b;
        Population = population;

        // Convert RGB to HSL (values in 0-1 range like node-vibrant)
        Hsl = RgbToHsl(r, g, b);
    }

    public int R { get; }
    public int G { get; }
    public int B { get; }
    public int Population { get; }

    /// <summary>
    /// HSL values in 0-1 range: [h, s, l]
    /// </summary>
    public (float h, float s, float l) Hsl { get; }

    public string Hex => $"#{R:X2}{G:X2}{B:X2}";

    private static (float h, float s, float l) RgbToHsl(int r, int g, int b) {
        var rf = r / 255f;
        var gf = g / 255f;
        var bf = b / 255f;

        var max = Math.Max(rf, Math.Max(gf, bf));
        var min = Math.Min(rf, Math.Min(gf, bf));
        var delta = max - min;

        var l = (max + min) / 2f;

        if (delta == 0)
            return (0, 0, l);

        var s = l > 0.5f ? delta / (2f - max - min) : delta / (max + min);

        float h;
        if (max == rf)
            h = ((gf - bf) / delta + (gf < bf ? 6 : 0)) / 6f;
        else if (max == gf)
            h = ((bf - rf) / delta + 2) / 6f;
        else
            h = ((rf - gf) / delta + 4) / 6f;

        return (h, s, l);
    }

    public static (int r, int g, int b) HslToRgb(float h, float s, float l) {
        float r, g, b;

        if (s == 0) {
            r = g = b = l;
        }
        else {
            float HueToRgb(float p, float q, float t) {
                if (t < 0) t += 1;
                if (t > 1) t -= 1;
                if (t < 1f / 6f) return p + (q - p) * 6f * t;
                if (t < 1f / 2f) return q;
                if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
                return p;
            }

            var q = l < 0.5f ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;
            r = HueToRgb(p, q, h + 1f / 3f);
            g = HueToRgb(p, q, h);
            b = HueToRgb(p, q, h - 1f / 3f);
        }

        return ((int)(r * 255), (int)(g * 255), (int)(b * 255));
    }
}