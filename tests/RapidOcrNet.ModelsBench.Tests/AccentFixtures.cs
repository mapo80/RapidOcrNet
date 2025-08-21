using SkiaSharp;

namespace RapidOcrNet.ModelsBench.Tests;

public static class AccentFixtures
{
    public static readonly string[] Words =
    {
        "Università", "perché", "città", "ragù", "più", "Così", "È", "Lì", "Già"
    };

    public static IEnumerable<object[]> Data()
    {
        foreach (var (bmp, text) in Generate())
        {
            yield return new object[] { bmp, text };
        }
    }

    public static IEnumerable<(SKBitmap Bitmap, string Text)> Generate()
    {
        foreach (var word in Words)
        {
            yield return (Render(word, 512, 128, 0), word);
        }
    }

    private static SKBitmap Render(string text, int width, int height, float angle)
    {
        var bmp = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        canvas.Translate(width / 2, height / 2);
        canvas.RotateDegrees(angle);
        canvas.Translate(-width / 2, -height / 2);
        using var paint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = height * 0.5f,
            IsAntialias = true,
            Typeface = SKFontManager.Default.MatchFamily("DejaVu Sans") ?? SKTypeface.Default
        };
        var bounds = new SKRect();
        paint.MeasureText(text, ref bounds);
        var x = (width - bounds.Width) / 2 - bounds.Left;
        var y = (height - bounds.Height) / 2 - bounds.Top;
        canvas.DrawText(text, x, y, paint);
        return bmp;
    }

    public static void SaveAssets(string dir)
    {
        Directory.CreateDirectory(dir);
        foreach (var word in Words.Take(5))
        {
            using var bmp = Render(word, 512, 128, 0);
            using var fs = File.OpenWrite(Path.Combine(dir, $"{word}.png"));
            bmp.Encode(fs, SKEncodedImageFormat.Png, 100);
        }
    }
}
