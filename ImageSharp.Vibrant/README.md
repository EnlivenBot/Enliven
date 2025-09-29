# ImageSharp.Vibrant

A simplified C# port of [node-vibrant](https://github.com/Vibrant-Colors/node-vibrant) for extracting prominent color
palettes from images using ImageSharp.

## Features

- Extract vibrant color palettes from images
- Uses Modified Median Cut Quantization (MMCQ) algorithm
- High-performance with ImageSharp
- Returns 6 swatch types: Vibrant, Muted, DarkVibrant, DarkMuted, LightVibrant, LightMuted

## Usage

```csharp
using ImageSharp.Vibrant;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

// Load an image
using var image = Image.Load<Rgba32>("path/to/image.jpg");

// Extract palette
var palette = VibrantExtractor.GetPalette(image);

// Access swatches
Console.WriteLine($"Vibrant: {palette.Vibrant?.Hex}");
Console.WriteLine($"Muted: {palette.Muted?.Hex}");
Console.WriteLine($"Dark Vibrant: {palette.DarkVibrant?.Hex}");
Console.WriteLine($"Dark Muted: {palette.DarkMuted?.Hex}");
Console.WriteLine($"Light Vibrant: {palette.LightVibrant?.Hex}");
Console.WriteLine($"Light Muted: {palette.LightMuted?.Hex}");

// Swatch properties
if (palette.Vibrant != null)
{
    var swatch = palette.Vibrant;
    Console.WriteLine($"RGB: ({swatch.R}, {swatch.G}, {swatch.B})");
    Console.WriteLine($"HSL: ({swatch.Hue:F1}°, {swatch.Saturation:F2}, {swatch.Lightness:F2})");
    Console.WriteLine($"Population: {swatch.Population}");
}
```

## Advanced Options

```csharp
// Custom color count and quality
var palette = VibrantExtractor.GetPalette(
    image,
    colorCount: 128,  // More colors = better accuracy but slower (default: 64)
    quality: 10       // Higher = faster but less accurate (default: 5)
);
```

## Performance

- Automatically resizes images larger than 256x256 for faster processing
- Samples pixels based on quality setting to reduce computation
- Efficient MMCQ implementation with priority queue for box splitting

## Algorithm

The implementation uses MMCQ (Modified Median Cut Quantization):

1. Sample pixels from the image (skipping transparent and extreme colors)
2. Create 3D color boxes in RGB space
3. Iteratively split boxes along the longest dimension
4. Calculate average color for each box
5. Select swatches based on HSL criteria for different palette types