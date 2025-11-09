using Android.Graphics;
using Android.Media;
using AndroidRect = Android.Graphics.Rect;
using AndroidPaint = Android.Graphics.Paint;
using AndroidColor = Android.Graphics.Color;

namespace ScanPackage.Platforms.Android;

/// <summary>
/// Image preprocessing utilities to improve OCR accuracy
/// </summary>
public static class ImagePreprocessor
{
    /// <summary>
    /// Preprocess image for optimal OCR results (Standard)
    /// </summary>
    public static Bitmap PreprocessForOCR(Bitmap original)
    {
        System.Diagnostics.Debug.WriteLine(">>> ImagePreprocessor: Starting preprocessing (Standard)");

        // 1. Resize to optimal resolution
        var resized = ResizeForOCR(original);
        System.Diagnostics.Debug.WriteLine($">>> Resized: {resized.Width}x{resized.Height}");

        // 2. Convert to grayscale
        var grayscale = ToGrayscale(resized);
        System.Diagnostics.Debug.WriteLine(">>> Converted to grayscale");

        // 3. Increase contrast
        var contrasted = IncreaseContrast(grayscale, 1.8f);
        System.Diagnostics.Debug.WriteLine(">>> Increased contrast (1.8x)");

        // 4. Sharpen
        var sharpened = SharpenImage(contrasted);
        System.Diagnostics.Debug.WriteLine(">>> Sharpened image");

        return sharpened;
    }

    /// <summary>
    /// Preprocess with high contrast (for low contrast images)
    /// </summary>
    public static Bitmap PreprocessHighContrast(Bitmap original)
    {
        System.Diagnostics.Debug.WriteLine(">>> ImagePreprocessor: High Contrast variant");
        var resized = ResizeForOCR(original);
        var grayscale = ToGrayscale(resized);
        var contrasted = IncreaseContrast(grayscale, 2.5f);
        var sharpened = SharpenImage(contrasted);
        return sharpened;
    }

    /// <summary>
    /// Preprocess with brightness adjustment
    /// </summary>
    public static Bitmap PreprocessBright(Bitmap original)
    {
        System.Diagnostics.Debug.WriteLine(">>> ImagePreprocessor: Brightness variant");
        var resized = ResizeForOCR(original);
        var grayscale = ToGrayscale(resized);
        var brightened = AdjustBrightness(grayscale, 30);
        var contrasted = IncreaseContrast(brightened, 1.8f);
        var sharpened = SharpenImage(contrasted);
        return sharpened;
    }

    /// <summary>
    /// Preprocess with edge enhancement (best for text recognition)
    /// </summary>
    public static Bitmap PreprocessEdgeEnhanced(Bitmap original)
    {
        System.Diagnostics.Debug.WriteLine(">>> ImagePreprocessor: Edge Enhanced variant");
        var resized = ResizeForOCR(original);
        var grayscale = ToGrayscale(resized);
        var contrasted = IncreaseContrast(grayscale, 2.2f);
        var sharpened = SharpenImage(contrasted);
        var edgeEnhanced = SharpenImage(sharpened); // Double sharpen for better edges
        return edgeEnhanced;
    }


    
    /// <summary>
    /// Resize image to optimal resolution for OCR (max 3000x3000 - keep high resolution)
    /// </summary>
    private static Bitmap ResizeForOCR(Bitmap original)
    {
        const int MAX_SIZE = 3000;

        // Only resize if image is too large
        if (original.Width <= MAX_SIZE && original.Height <= MAX_SIZE)
        {
            System.Diagnostics.Debug.WriteLine($">>> Image size OK, no resize needed: {original.Width}x{original.Height}");
            return original;
        }

        float ratio = Math.Min(
            (float)MAX_SIZE / original.Width,
            (float)MAX_SIZE / original.Height
        );

        int newWidth = (int)(original.Width * ratio);
        int newHeight = (int)(original.Height * ratio);

        System.Diagnostics.Debug.WriteLine($">>> Resizing from {original.Width}x{original.Height} to {newWidth}x{newHeight}");
        return Bitmap.CreateScaledBitmap(original, newWidth, newHeight, true);
    }
    
    /// <summary>
    /// Convert image to grayscale
    /// </summary>
    private static Bitmap ToGrayscale(Bitmap original)
    {
        var grayscale = Bitmap.CreateBitmap(original.Width, original.Height, Bitmap.Config.Argb8888!);
        var canvas = new Canvas(grayscale);
        var paint = new AndroidPaint();
        
        var colorMatrix = new ColorMatrix();
        colorMatrix.SetSaturation(0); // Remove all color
        
        var filter = new ColorMatrixColorFilter(colorMatrix);
        paint.SetColorFilter(filter);
        
        canvas.DrawBitmap(original, 0, 0, paint);
        
        return grayscale;
    }
    
    /// <summary>
    /// Increase image contrast
    /// </summary>
    private static Bitmap IncreaseContrast(Bitmap original, float contrast)
    {
        var contrasted = Bitmap.CreateBitmap(original.Width, original.Height, Bitmap.Config.Argb8888!);
        var canvas = new Canvas(contrasted);
        var paint = new AndroidPaint();
        
        var colorMatrix = new ColorMatrix(new float[]
        {
            contrast, 0, 0, 0, 0,
            0, contrast, 0, 0, 0,
            0, 0, contrast, 0, 0,
            0, 0, 0, 1, 0
        });
        
        var filter = new ColorMatrixColorFilter(colorMatrix);
        paint.SetColorFilter(filter);
        
        canvas.DrawBitmap(original, 0, 0, paint);
        
        return contrasted;
    }
    
    /// <summary>
    /// Sharpen image using convolution matrix
    /// </summary>
    private static Bitmap SharpenImage(Bitmap original)
    {
        var sharpened = Bitmap.CreateBitmap(original.Width, original.Height, Bitmap.Config.Argb8888!);
        var canvas = new Canvas(sharpened);
        var paint = new AndroidPaint();
        
        // Sharpening kernel
        var sharpenMatrix = new ColorMatrix(new float[]
        {
            0, -1, 0, 0, 0,
            -1, 5, -1, 0, 0,
            0, -1, 0, 0, 0,
            0, 0, 0, 1, 0
        });
        
        var filter = new ColorMatrixColorFilter(sharpenMatrix);
        paint.SetColorFilter(filter);
        
        canvas.DrawBitmap(original, 0, 0, paint);
        
        return sharpened;
    }
    
    /// <summary>
    /// Expand ROI with padding to avoid cutting text
    /// </summary>
    public static AndroidRect ExpandROI(AndroidRect roi, int padding, int maxWidth, int maxHeight)
    {
        var expanded = new AndroidRect(roi);
        expanded.Inset(-padding, -padding);

        // Ensure within bounds
        if (expanded.Left < 0) expanded.Left = 0;
        if (expanded.Top < 0) expanded.Top = 0;
        if (expanded.Right > maxWidth) expanded.Right = maxWidth;
        if (expanded.Bottom > maxHeight) expanded.Bottom = maxHeight;

        return expanded;
    }
    
    /// <summary>
    /// Rotate image by specified degrees
    /// </summary>
    public static Bitmap RotateImage(Bitmap original, float degrees)
    {
        var matrix = new Matrix();
        matrix.PostRotate(degrees);
        
        return Bitmap.CreateBitmap(original, 0, 0, original.Width, original.Height, matrix, true)!;
    }
    
    /// <summary>
    /// Auto-rotate image based on EXIF orientation
    /// </summary>
    public static Bitmap AutoRotate(Bitmap bitmap, string imagePath)
    {
        try
        {
            var exif = new ExifInterface(imagePath);
            var orientation = exif.GetAttributeInt(
                ExifInterface.TagOrientation,
                (int)Orientation.Normal
            );

            var degrees = orientation switch
            {
                (int)Orientation.Rotate90 => 90,
                (int)Orientation.Rotate180 => 180,
                (int)Orientation.Rotate270 => 270,
                _ => 0
            };

            if (degrees != 0)
            {
                System.Diagnostics.Debug.WriteLine($">>> Auto-rotating image by {degrees} degrees");
                return RotateImage(bitmap, degrees);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($">>> Error reading EXIF: {ex.Message}");
        }

        return bitmap;
    }

    /// <summary>
    /// Adjust brightness
    /// </summary>
    private static Bitmap AdjustBrightness(Bitmap original, int brightness)
    {
        var adjusted = Bitmap.CreateBitmap(original.Width, original.Height, Bitmap.Config.Argb8888!);
        var canvas = new Canvas(adjusted);
        var paint = new AndroidPaint();

        var colorMatrix = new ColorMatrix(new float[]
        {
            1, 0, 0, 0, brightness,
            0, 1, 0, 0, brightness,
            0, 0, 1, 0, brightness,
            0, 0, 0, 1, 0
        });

        var filter = new ColorMatrixColorFilter(colorMatrix);
        paint.SetColorFilter(filter);

        canvas.DrawBitmap(original, 0, 0, paint);

        return adjusted;
    }




}

