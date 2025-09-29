using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ImageSharp.Vibrant;

/// <summary>
/// Extracts vibrant color palettes from images using Modified Median Cut Quantization (MMCQ).
/// Based on node-vibrant implementation.
/// </summary>
public static class VibrantExtractor {
    private const int DefaultColorCount = 64;
    private const int DefaultQuality = 5;
    private const int MaxDimension = 256;
    private const int SigBits = 5;
    private const int RShift = 8 - SigBits;
    private const double FractByPopulations = 0.75;

    public static class GeneratorOptions {
        public static float TargetDarkLuma = 0.26f;
        public static float MaxDarkLuma = 0.45f;
        public static float MinLightLuma = 0.55f;
        public static float TargetLightLuma = 0.74f;
        public static float MinNormalLuma = 0.3f;
        public static float TargetNormalLuma = 0.5f;
        public static float MaxNormalLuma = 0.7f;
        public static float TargetMutesSaturation = 0.3f;
        public static float MaxMutesSaturation = 0.4f;
        public static float TargetVibrantSaturation = 1.0f;
        public static float MinVibrantSaturation = 0.35f;
        public static float WeightSaturation = 3f;
        public static float WeightLuma = 6.5f;
        public static float WeightPopulation = 0.5f;
    }

    /// <summary>
    /// Extracts a color palette from the specified image.
    /// </summary>
    public static Palette GetPalette(Image<Rgba32> image, int colorCount = DefaultColorCount,
        int quality = DefaultQuality) {
        var workingImage = image;
        var needsDispose = false;

        if (image.Width > MaxDimension || image.Height > MaxDimension) {
            var scale = Math.Min(MaxDimension / (float)image.Width, MaxDimension / (float)image.Height);
            var newWidth = (int)(image.Width * scale);
            var newHeight = (int)(image.Height * scale);
            workingImage = image.Clone(ctx => ctx.Resize(newWidth, newHeight));
            needsDispose = true;
        }

        try {
            var pixels = SamplePixels(workingImage, quality);
            var swatches = Quantize(pixels, colorCount);
            return GeneratePalette(swatches);
        }
        finally {
            if (needsDispose)
                workingImage.Dispose();
        }
    }

    private static List<int> SamplePixels(Image<Rgba32> image, int quality) {
        var pixels = new List<int>();

        image.ProcessPixelRows(accessor => {
            for (var y = 0; y < accessor.Height; y += quality) {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x += quality) {
                    var pixel = row[x];
                    if (pixel.A < 125) continue;

                    var r = pixel.R >> RShift;
                    var g = pixel.G >> RShift;
                    var b = pixel.B >> RShift;
                    var index = (r << (2 * SigBits)) | (g << SigBits) | b;
                    pixels.Add(index);
                }
            }
        });

        return pixels;
    }

    private static List<Swatch> Quantize(List<int> pixels, int maxColors) {
        if (pixels.Count == 0 || maxColors < 2 || maxColors > 256)
            return [];

        var histogram = BuildHistogram(pixels);
        var vbox = VBox.Build(histogram);

        var pq = new PriorityQueue<VBox, int>(Comparer<int>.Create((a, b) => b.CompareTo(a)));
        pq.Enqueue(vbox, vbox.Count);

        // First split by population
        SplitBoxes(pq, (int)(FractByPopulations * maxColors));

        // Re-order by count * volume
        var pq2 = new PriorityQueue<VBox, int>(Comparer<int>.Create((a, b) => b.CompareTo(a)));
        while (pq.TryDequeue(out var box, out _))
            pq2.Enqueue(box, box.Count * box.Volume);

        // Final split
        SplitBoxes(pq2, maxColors - pq2.Count);

        // Generate swatches
        var swatches = new List<Swatch>();
        while (pq2.TryDequeue(out var box, out _)) {
            var (r, g, b) = box.Avg();
            swatches.Add(new Swatch(r, g, b, box.Count));
        }

        return swatches;
    }

    private static void SplitBoxes(PriorityQueue<VBox, int> pq, int target) {
        var lastSize = pq.Count;

        while (pq.Count < target) {
            if (!pq.TryDequeue(out var vbox, out _))
                break;

            if (vbox.Count == 0)
                break;

            var (vbox1, vbox2) = vbox.Split();

            if (vbox1 == null)
                break;

            pq.Enqueue(vbox1, vbox1.Count);

            if (vbox2 != null && vbox2.Count > 0)
                pq.Enqueue(vbox2, vbox2.Count);

            if (pq.Count == lastSize)
                break;

            lastSize = pq.Count;
        }
    }

    private static int[] BuildHistogram(List<int> pixels) {
        var hist = new int[1 << (3 * SigBits)];
        foreach (var pixel in pixels)
            hist[pixel]++;
        return hist;
    }

    private static Palette GeneratePalette(List<Swatch> swatches) {
        if (swatches.Count == 0)
            return new Palette();

        var maxPopulation = swatches.Max(s => s.Population);
        var palette = new Palette();

        palette.Vibrant = FindColorVariation(palette, swatches, maxPopulation,
            GeneratorOptions.TargetNormalLuma, GeneratorOptions.MinNormalLuma, GeneratorOptions.MaxNormalLuma,
            GeneratorOptions.TargetVibrantSaturation, GeneratorOptions.MinVibrantSaturation, 1f);

        palette.LightVibrant = FindColorVariation(palette, swatches, maxPopulation,
            GeneratorOptions.TargetLightLuma, GeneratorOptions.MinLightLuma, 1f,
            GeneratorOptions.TargetVibrantSaturation, GeneratorOptions.MinVibrantSaturation, 1f);

        palette.DarkVibrant = FindColorVariation(palette, swatches, maxPopulation,
            GeneratorOptions.TargetDarkLuma, 0f, GeneratorOptions.MaxDarkLuma,
            GeneratorOptions.TargetVibrantSaturation, GeneratorOptions.MinVibrantSaturation, 1f);

        palette.Muted = FindColorVariation(palette, swatches, maxPopulation,
            GeneratorOptions.TargetNormalLuma, GeneratorOptions.MinNormalLuma, GeneratorOptions.MaxNormalLuma,
            GeneratorOptions.TargetMutesSaturation, 0f, GeneratorOptions.MaxMutesSaturation);

        palette.LightMuted = FindColorVariation(palette, swatches, maxPopulation,
            GeneratorOptions.TargetLightLuma, GeneratorOptions.MinLightLuma, 1f,
            GeneratorOptions.TargetMutesSaturation, 0f, GeneratorOptions.MaxMutesSaturation);

        palette.DarkMuted = FindColorVariation(palette, swatches, maxPopulation,
            GeneratorOptions.TargetDarkLuma, 0f, GeneratorOptions.MaxDarkLuma,
            GeneratorOptions.TargetMutesSaturation, 0f, GeneratorOptions.MaxMutesSaturation);

        GenerateEmptySwatches(palette);

        return palette;
    }

    private static Swatch? FindColorVariation(Palette palette, List<Swatch> swatches, int maxPopulation,
        float targetLuma, float minLuma, float maxLuma,
        float targetSaturation, float minSaturation, float maxSaturation) {
        Swatch? max = null;
        float maxValue = 0;

        foreach (var swatch in swatches) {
            var (_, s, l) = swatch.Hsl;

            if (s >= minSaturation && s <= maxSaturation &&
                l >= minLuma && l <= maxLuma &&
                !IsAlreadySelected(palette, swatch)) {
                var value = CreateComparisonValue(s, targetSaturation, l, targetLuma,
                    swatch.Population, maxPopulation);

                if (max == null || value > maxValue) {
                    max = swatch;
                    maxValue = value;
                }
            }
        }

        return max;
    }

    private static float CreateComparisonValue(float saturation, float targetSaturation,
        float luma, float targetLuma, int population, int maxPopulation) {
        float WeightedMean(float value1, float weight1, float value2, float weight2, float value3, float weight3) {
            var sum = value1 * weight1 + value2 * weight2 + value3 * weight3;
            var weightSum = weight1 + weight2 + weight3;
            return sum / weightSum;
        }

        float InvertDiff(float value, float targetValue) => 1 - Math.Abs(value - targetValue);

        return WeightedMean(
            InvertDiff(saturation, targetSaturation), GeneratorOptions.WeightSaturation,
            InvertDiff(luma, targetLuma), GeneratorOptions.WeightLuma,
            (float)population / maxPopulation, GeneratorOptions.WeightPopulation
        );
    }

    private static bool IsAlreadySelected(Palette palette, Swatch swatch) =>
        palette.Vibrant == swatch || palette.DarkVibrant == swatch || palette.LightVibrant == swatch ||
        palette.Muted == swatch || palette.DarkMuted == swatch || palette.LightMuted == swatch;

    private static void GenerateEmptySwatches(Palette palette) {
        if (palette.Vibrant == null && palette.DarkVibrant == null && palette.LightVibrant == null) {
            if (palette.DarkVibrant == null && palette.DarkMuted != null) {
                var (h, s, _) = palette.DarkMuted.Hsl;
                var (r, g, b) = Swatch.HslToRgb(h, s, GeneratorOptions.TargetDarkLuma);
                palette.DarkVibrant = new Swatch(r, g, b, 0);
            }

            if (palette.LightVibrant == null && palette.LightMuted != null) {
                var (h, s, _) = palette.LightMuted.Hsl;
                var (r, g, b) = Swatch.HslToRgb(h, s, GeneratorOptions.TargetLightLuma);
                palette.LightVibrant = new Swatch(r, g, b, 0);
            }
        }

        if (palette.Vibrant == null && palette.DarkVibrant != null) {
            var (h, s, _) = palette.DarkVibrant.Hsl;
            var (r, g, b) = Swatch.HslToRgb(h, s, GeneratorOptions.TargetNormalLuma);
            palette.Vibrant = new Swatch(r, g, b, 0);
        }
        else if (palette.Vibrant == null && palette.LightVibrant != null) {
            var (h, s, _) = palette.LightVibrant.Hsl;
            var (r, g, b) = Swatch.HslToRgb(h, s, GeneratorOptions.TargetNormalLuma);
            palette.Vibrant = new Swatch(r, g, b, 0);
        }

        if (palette.DarkVibrant == null && palette.Vibrant != null) {
            var (h, s, _) = palette.Vibrant.Hsl;
            var (r, g, b) = Swatch.HslToRgb(h, s, GeneratorOptions.TargetDarkLuma);
            palette.DarkVibrant = new Swatch(r, g, b, 0);
        }

        if (palette.LightVibrant == null && palette.Vibrant != null) {
            var (h, s, _) = palette.Vibrant.Hsl;
            var (r, g, b) = Swatch.HslToRgb(h, s, GeneratorOptions.TargetLightLuma);
            palette.LightVibrant = new Swatch(r, g, b, 0);
        }

        if (palette.Muted == null && palette.Vibrant != null) {
            var (h, _, l) = palette.Vibrant.Hsl;
            var (r, g, b) = Swatch.HslToRgb(h, GeneratorOptions.TargetMutesSaturation, l);
            palette.Muted = new Swatch(r, g, b, 0);
        }

        if (palette.DarkMuted == null && palette.DarkVibrant != null) {
            var (h, _, l) = palette.DarkVibrant.Hsl;
            var (r, g, b) = Swatch.HslToRgb(h, GeneratorOptions.TargetMutesSaturation, l);
            palette.DarkMuted = new Swatch(r, g, b, 0);
        }

        if (palette.LightMuted == null && palette.LightVibrant != null) {
            var (h, _, l) = palette.LightVibrant.Hsl;
            var (r, g, b) = Swatch.HslToRgb(h, GeneratorOptions.TargetMutesSaturation, l);
            palette.LightMuted = new Swatch(r, g, b, 0);
        }
    }

    private sealed class VBox {
        private readonly int[] _histogram;
        public int R1, R2, G1, G2, B1, B2;
        private int _volume = -1;
        private int _count = -1;
        private (int r, int g, int b)? _avg;

        private VBox(int r1, int r2, int g1, int g2, int b1, int b2, int[] histogram) {
            R1 = r1;
            R2 = r2;
            G1 = g1;
            G2 = g2;
            B1 = b1;
            B2 = b2;
            _histogram = histogram;
        }

        public static VBox Build(int[] histogram) {
            int rmin = 31, rmax = 0, gmin = 31, gmax = 0, bmin = 31, bmax = 0;

            for (var i = 0; i < histogram.Length; i++) {
                if (histogram[i] == 0) continue;

                var r = (i >> (2 * SigBits)) & ((1 << SigBits) - 1);
                var g = (i >> SigBits) & ((1 << SigBits) - 1);
                var b = i & ((1 << SigBits) - 1);

                if (r < rmin) rmin = r;
                if (r > rmax) rmax = r;
                if (g < gmin) gmin = g;
                if (g > gmax) gmax = g;
                if (b < bmin) bmin = b;
                if (b > bmax) bmax = b;
            }

            return new VBox(rmin, rmax, gmin, gmax, bmin, bmax, histogram);
        }

        public int Volume {
            get {
                if (_volume < 0)
                    _volume = (R2 - R1 + 1) * (G2 - G1 + 1) * (B2 - B1 + 1);
                return _volume;
            }
        }

        public int Count {
            get {
                if (_count < 0) {
                    var c = 0;
                    for (var r = R1; r <= R2; r++)
                    for (var g = G1; g <= G2; g++)
                    for (var b = B1; b <= B2; b++) {
                        var index = (r << (2 * SigBits)) | (g << SigBits) | b;
                        c += _histogram[index];
                    }

                    _count = c;
                }

                return _count;
            }
        }

        public (int r, int g, int b) Avg() {
            if (_avg != null)
                return _avg.Value;

            var ntot = 0;
            var mult = 1 << RShift;
            long rsum = 0, gsum = 0, bsum = 0;

            for (var r = R1; r <= R2; r++)
            for (var g = G1; g <= G2; g++)
            for (var b = B1; b <= B2; b++) {
                var index = (r << (2 * SigBits)) | (g << SigBits) | b;
                var h = _histogram[index];
                if (h == 0) continue;

                ntot += h;
                rsum += (long)(h * (r + 0.5) * mult);
                gsum += (long)(h * (g + 0.5) * mult);
                bsum += (long)(h * (b + 0.5) * mult);
            }

            if (ntot > 0)
                _avg = ((int)(rsum / ntot), (int)(gsum / ntot), (int)(bsum / ntot));
            else
                _avg = ((mult * (R1 + R2 + 1)) / 2, (mult * (G1 + G2 + 1)) / 2, (mult * (B1 + B2 + 1)) / 2);

            return _avg.Value;
        }

        public (VBox? box1, VBox? box2) Split() {
            var count = Count;
            if (count == 0) return (null, null);
            if (count == 1) return (new VBox(R1, R2, G1, G2, B1, B2, _histogram), null);

            var rw = R2 - R1 + 1;
            var gw = G2 - G1 + 1;
            var bw = B2 - B1 + 1;
            var maxw = Math.Max(rw, Math.Max(gw, bw));

            int[]? accSum = null;
            var total = 0;
            char maxd = 'r';

            int dimMin, dimMax;

            if (maxw == rw) {
                maxd = 'r';
                dimMin = R1;
                dimMax = R2;
            }
            else if (maxw == gw) {
                maxd = 'g';
                dimMin = G1;
                dimMax = G2;
            }
            else {
                maxd = 'b';
                dimMin = B1;
                dimMax = B2;
            }

            accSum = new int[dimMax + 1];

            for (var i = dimMin; i <= dimMax; i++) {
                var sum = 0;

                if (maxd == 'r') {
                    for (var g = G1; g <= G2; g++)
                    for (var b = B1; b <= B2; b++) {
                        var index = (i << (2 * SigBits)) | (g << SigBits) | b;
                        sum += _histogram[index];
                    }
                }
                else if (maxd == 'g') {
                    for (var r = R1; r <= R2; r++)
                    for (var b = B1; b <= B2; b++) {
                        var index = (r << (2 * SigBits)) | (i << SigBits) | b;
                        sum += _histogram[index];
                    }
                }
                else {
                    for (var r = R1; r <= R2; r++)
                    for (var g = G1; g <= G2; g++) {
                        var index = (r << (2 * SigBits)) | (g << SigBits) | i;
                        sum += _histogram[index];
                    }
                }

                total += sum;
                accSum[i] = total;
            }

            var splitPoint = -1;
            var reverseSum = new int[accSum.Length];
            for (var i = 0; i < accSum.Length; i++) {
                var d = accSum[i];
                if (splitPoint < 0 && d > total / 2)
                    splitPoint = i;
                reverseSum[i] = total - d;
            }

            return DoCut(maxd, splitPoint, accSum, reverseSum);
        }

        private (VBox?, VBox?) DoCut(char dim, int splitPoint, int[] accSum, int[] reverseSum) {
            int d1, d2;

            if (dim == 'r') {
                d1 = R1;
                d2 = R2;
            }
            else if (dim == 'g') {
                d1 = G1;
                d2 = G2;
            }
            else {
                d1 = B1;
                d2 = B2;
            }

            var vbox1 = new VBox(R1, R2, G1, G2, B1, B2, _histogram);
            var vbox2 = new VBox(R1, R2, G1, G2, B1, B2, _histogram);

            var left = splitPoint - d1;
            var right = d2 - splitPoint;

            if (left <= right) {
                d2 = Math.Min(d2 - 1, (int)(splitPoint + right / 2.0));
                d2 = Math.Max(0, d2);
            }
            else {
                d2 = Math.Max(d1, (int)(splitPoint - 1 - left / 2.0));
                // Use original d2 value as max limit
                if (dim == 'r')
                    d2 = Math.Min(R2, d2);
                else if (dim == 'g')
                    d2 = Math.Min(G2, d2);
                else
                    d2 = Math.Min(B2, d2);
            }

            while (d2 < accSum.Length && accSum[d2] == 0) d2++;

            if (d2 >= reverseSum.Length)
                d2 = reverseSum.Length - 1;

            var c2 = reverseSum[d2];
            while (c2 == 0 && d2 > 0 && d2 - 1 >= 0 && d2 - 1 < accSum.Length && accSum[d2 - 1] > 0)
                c2 = reverseSum[--d2];

            if (dim == 'r') {
                vbox1.R2 = d2;
                vbox2.R1 = d2 + 1;
            }
            else if (dim == 'g') {
                vbox1.G2 = d2;
                vbox2.G1 = d2 + 1;
            }
            else {
                vbox1.B2 = d2;
                vbox2.B1 = d2 + 1;
            }

            return (vbox1, vbox2);
        }
    }
}