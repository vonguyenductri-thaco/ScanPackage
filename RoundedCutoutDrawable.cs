using Microsoft.Maui.Graphics;

namespace ScanPackage;

public class RoundedCutoutDrawable : IDrawable
{
    public float CutoutWidth { get; set; } = 282;
    public float CutoutHeight { get; set; } = 160;
    public float CornerRadius { get; set; } = 20;
    public Color OverlayColor { get; set; } = Color.FromRgba(0, 0, 0, 0.50f); // 50% opacity

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        // Calculate cutout position (center of screen)
        float cutoutX = (dirtyRect.Width - CutoutWidth) / 2;
        float cutoutY = (dirtyRect.Height - CutoutHeight) / 2;

        // Create a path with outer rectangle and inner rounded rectangle
        var path = new PathF();

        // Outer rectangle (full screen) - clockwise
        path.MoveTo(0, 0);
        path.LineTo(dirtyRect.Width, 0);
        path.LineTo(dirtyRect.Width, dirtyRect.Height);
        path.LineTo(0, dirtyRect.Height);
        path.Close();

        // Inner rounded rectangle (cutout) - counter-clockwise to create hole
        var cutoutRect = new RectF(cutoutX, cutoutY, CutoutWidth, CutoutHeight);
        path.AppendRoundedRectangle(cutoutRect, CornerRadius, CornerRadius, CornerRadius, CornerRadius);

        // Fill with EvenOdd winding rule to create hole
        canvas.FillColor = OverlayColor;
        canvas.FillPath(path, WindingMode.EvenOdd);
    }
}