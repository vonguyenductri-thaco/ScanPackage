using Android.Graphics;
using Xamarin.Google.MLKit.Vision.Text;
using Xamarin.Google.MLKit.Vision.Text.Latin;
using Xamarin.Google.MLKit.Vision.Common;
using Android.Gms.Tasks;
using Java.Lang;
using System.Text.RegularExpressions;
using ScanPackage.Platforms.Android;

namespace ScanPackage;

public class AndroidOcrService : IOcrService
{
    private readonly ITextRecognizer _textRecognizer;

    public AndroidOcrService()
    {
        // Use Latin script recognizer for better accuracy with English/Latin characters
        var options = new TextRecognizerOptions.Builder().Build();
        _textRecognizer = TextRecognition.GetClient(options);

        System.Diagnostics.Debug.WriteLine(">>> AndroidOcrService initialized with ML Kit Latin TextRecognizer");
    }
    public async Task<string?> ScanTextAsync(OcrMode mode)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($">>> ScanTextAsync started for mode: {mode}");

            // Open crop page to select scan region
            var tcs = new TaskCompletionSource<OcrCropResult>();
            var cropPage = new OcrCropPage(mode, tcs);
            var window = Application.Current?.Windows?.FirstOrDefault();
            var nav = window?.Page?.Navigation;
            if (nav == null)
            {
                System.Diagnostics.Debug.WriteLine(">>> ERROR: Navigation is null");
                return null;
            }

            await nav.PushAsync(cropPage);
            var cropResult = await tcs.Task;

            System.Diagnostics.Debug.WriteLine($">>> Crop result received. IsCanceled: {cropResult.IsCanceled}");

            if (cropResult.IsCanceled || cropResult.Photo == null)
            {
                System.Diagnostics.Debug.WriteLine(">>> Crop was canceled or no photo");
                return null;
            }

            // Load bitmap from photo
            System.Diagnostics.Debug.WriteLine(">>> Opening photo stream...");
            using var stream = await cropResult.Photo.OpenReadAsync();
            using var fullBitmap = await BitmapFactory.DecodeStreamAsync(stream);
            if (fullBitmap == null)
            {
                System.Diagnostics.Debug.WriteLine(">>> ERROR: Failed to decode bitmap");
                return null;
            }

            System.Diagnostics.Debug.WriteLine($">>> Full bitmap size: {fullBitmap.Width}x{fullBitmap.Height}");

            // Calculate crop region from relative coordinates
            var rel = cropResult.RelativeCrop;
            int cropW = System.Math.Max(1, (int)(fullBitmap.Width * rel.Width));
            int cropH = System.Math.Max(1, (int)(fullBitmap.Height * rel.Height));
            int left = System.Math.Max(0, (int)(fullBitmap.Width * rel.X));
            int top = System.Math.Max(0, (int)(fullBitmap.Height * rel.Y));
            cropW = System.Math.Min(cropW, fullBitmap.Width - left);
            cropH = System.Math.Min(cropH, fullBitmap.Height - top);

            System.Diagnostics.Debug.WriteLine($">>> Crop region: left={left}, top={top}, width={cropW}, height={cropH}");

            // Expand ROI with padding to avoid cutting text
            var roi = new Android.Graphics.Rect(left, top, left + cropW, top + cropH);
            roi = ImagePreprocessor.ExpandROI(roi, 20, fullBitmap.Width, fullBitmap.Height);
            System.Diagnostics.Debug.WriteLine($">>> Expanded ROI: {roi.Left},{roi.Top} -> {roi.Right},{roi.Bottom}");

            using var croppedBitmap = Bitmap.CreateBitmap(
                fullBitmap,
                roi.Left,
                roi.Top,
                roi.Width(),
                roi.Height()
            );
            System.Diagnostics.Debug.WriteLine($">>> Cropped bitmap: {croppedBitmap.Width}x{croppedBitmap.Height}");

            // Preprocess image for better OCR accuracy
            System.Diagnostics.Debug.WriteLine(">>> Preprocessing image...");
            using var preprocessed = ImagePreprocessor.PreprocessForOCR(croppedBitmap);

            // Try OCR with multiple attempts
            var result = await ScanWithRetry(preprocessed, mode);

            System.Diagnostics.Debug.WriteLine($">>> Final result: {(result == null ? "NULL" : $"'{result}'")}");
            return result;
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($">>> ScanTextAsync ERROR: {ex.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($">>> Message: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($">>> StackTrace: {ex.StackTrace}");
            return null;
        }
    }

    /// <summary>
    /// Scan with multiple retry attempts using different preprocessing
    /// </summary>
    private async Task<string?> ScanWithRetry(Bitmap bitmap, OcrMode mode)
    {
        // Attempt 1: Edge Enhanced (BEST for text) - try this first!
        System.Diagnostics.Debug.WriteLine(">>> Attempt 1: Edge Enhanced (best for text)");
        using var edgeEnhanced = ImagePreprocessor.PreprocessEdgeEnhanced(bitmap);
        var result = await PerformOCR(edgeEnhanced, mode);
        if (result != null) return result;

        // Attempt 2: Original image (NO preprocessing)
        System.Diagnostics.Debug.WriteLine(">>> Attempt 2: Original image (no preprocessing)");
        result = await PerformOCRRaw(bitmap, mode);
        if (result != null) return result;

        // Attempt 3: Standard preprocessing
        System.Diagnostics.Debug.WriteLine(">>> Attempt 3: Standard preprocessing");
        result = await PerformOCR(bitmap, mode);
        if (result != null) return result;

        // Attempt 4: High contrast variant
        System.Diagnostics.Debug.WriteLine(">>> Attempt 4: High contrast variant");
        using var highContrast = ImagePreprocessor.PreprocessHighContrast(bitmap);
        result = await PerformOCR(highContrast, mode);
        if (result != null) return result;

        // Attempt 5: Brightness adjusted
        System.Diagnostics.Debug.WriteLine(">>> Attempt 5: Brightness adjusted");
        using var bright = ImagePreprocessor.PreprocessBright(bitmap);
        result = await PerformOCR(bright, mode);
        if (result != null) return result;

        // Attempt 6: Rotate +3 degrees
        System.Diagnostics.Debug.WriteLine(">>> Attempt 6: Rotated +3 degrees");
        using var rotated1 = ImagePreprocessor.RotateImage(bitmap, 3);
        result = await PerformOCR(rotated1, mode);
        if (result != null) return result;

        // Attempt 7: Rotate -3 degrees
        System.Diagnostics.Debug.WriteLine(">>> Attempt 7: Rotated -3 degrees");
        using var rotated2 = ImagePreprocessor.RotateImage(bitmap, -3);
        result = await PerformOCR(rotated2, mode);
        if (result != null) return result;

        System.Diagnostics.Debug.WriteLine(">>> All 7 retry attempts failed");
        return null;
    }

    /// <summary>
    /// Perform OCR on raw bitmap (no preprocessing)
    /// </summary>
    private async Task<string?> PerformOCRRaw(Bitmap bitmap, OcrMode mode)
    {
        try
        {
            // Create InputImage from bitmap
            var inputImage = InputImage.FromBitmap(bitmap, 0);

            // Process with ML Kit
            var tcs = new TaskCompletionSource<Text>();
            _textRecognizer.Process(inputImage)
                .AddOnSuccessListener(new OnSuccessListener(text => tcs.TrySetResult((Text)text)))
                .AddOnFailureListener(new OnFailureListener(ex => tcs.TrySetException(new System.Exception(ex.Message))));

            var textResult = await tcs.Task;

            if (textResult == null || string.IsNullOrWhiteSpace(textResult.GetText()))
            {
                System.Diagnostics.Debug.WriteLine(">>> ML Kit returned no text (raw)");
                return null;
            }

            var recognizedText = textResult.GetText();
            System.Diagnostics.Debug.WriteLine($">>> ML Kit recognized (raw): '{recognizedText}'");

            // Log all text blocks for debugging
            var textBlocks = textResult.TextBlocks;
            System.Diagnostics.Debug.WriteLine($">>> ML Kit found {textBlocks.Count} text blocks");
            for (int i = 0; i < textBlocks.Count; i++)
            {
                System.Diagnostics.Debug.WriteLine($">>>   Block {i}: '{textBlocks[i].Text}'");
            }

            // Extract based on mode
            var extracted = mode == OcrMode.Container
                ? ExtractContainer(recognizedText)
                : ExtractSeal(recognizedText);

            if (extracted != null)
            {
                System.Diagnostics.Debug.WriteLine($">>> SUCCESS (raw): '{extracted}'");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($">>> FAILED to extract from (raw): '{recognizedText}'");
            }

            return extracted;
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($">>> PerformOCRRaw ERROR: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Perform OCR on bitmap using ML Kit
    /// </summary>
    private async Task<string?> PerformOCR(Bitmap bitmap, OcrMode mode)
    {
        try
        {
            // Create InputImage from bitmap
            var inputImage = InputImage.FromBitmap(bitmap, 0);

            // Process with ML Kit
            var tcs = new TaskCompletionSource<Text>();
            _textRecognizer.Process(inputImage)
                .AddOnSuccessListener(new OnSuccessListener(text => tcs.TrySetResult((Text)text)))
                .AddOnFailureListener(new OnFailureListener(ex => tcs.TrySetException(new System.Exception(ex.Message))));

            var textResult = await tcs.Task;

            if (textResult == null || string.IsNullOrWhiteSpace(textResult.GetText()))
            {
                System.Diagnostics.Debug.WriteLine(">>> ML Kit returned no text");
                return null;
            }

            var allText = textResult.GetText();
            System.Diagnostics.Debug.WriteLine($">>> ML Kit detected text: '{allText}'");

            // Log all text blocks for debugging
            var textBlocks = textResult.TextBlocks;
            System.Diagnostics.Debug.WriteLine($">>> ML Kit found {textBlocks.Count} text blocks");
            for (int i = 0; i < textBlocks.Count; i++)
            {
                System.Diagnostics.Debug.WriteLine($">>>   Block {i}: '{textBlocks[i].Text}'");
            }

            // Extract based on mode
            var extracted = mode switch
            {
                OcrMode.Container => ExtractContainer(allText),
                OcrMode.Seal => ExtractSeal(allText),
                _ => null
            };

            if (extracted != null)
            {
                System.Diagnostics.Debug.WriteLine($">>> Successfully extracted: '{extracted}'");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($">>> FAILED to extract from: '{allText}'");
            }

            return extracted;
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($">>> PerformOCR error: {ex.Message}");
            return null;
        }
    }



    /// <summary>
    /// Extract container number from text using improved regex patterns
    /// ISO 6346 format: 4 letters + 6 digits + 1 check digit
    /// </summary>
    private static string? ExtractContainer(string text)
    {
        System.Diagnostics.Debug.WriteLine($">>> ExtractContainer from: '{text}'");

        // Normalize text: remove extra whitespace
        var normalized = Regex.Replace(text, @"\s+", " ").Trim().ToUpperInvariant();
        System.Diagnostics.Debug.WriteLine($">>> Normalized: '{normalized}'");

        // Try original text first (before OCR correction)
        var noSpacesOriginal = Regex.Replace(normalized, @"\s+", "");

        // Pattern 1: Strict ISO 6346 format (4 LETTERS + 7 DIGITS) - original text
        var match = Regex.Match(noSpacesOriginal, @"([A-Z]{4})(\d{7})");
        if (match.Success)
        {
            var letters = match.Groups[1].Value;
            var digits = match.Groups[2].Value;
            var formatted = $"{letters} {digits.Substring(0, 6)} {digits[6]}";
            System.Diagnostics.Debug.WriteLine($">>> FOUND (Original Pattern 1): '{formatted}'");
            return formatted;
        }

        // Pattern 2: With spaces in original
        match = Regex.Match(normalized, @"([A-Z]{4})\s*(\d{6})\s*(\d)");
        if (match.Success)
        {
            var formatted = $"{match.Groups[1].Value} {match.Groups[2].Value} {match.Groups[3].Value}";
            System.Diagnostics.Debug.WriteLine($">>> FOUND (Original Pattern 2): '{formatted}'");
            return formatted;
        }

        // Now try with OCR error correction
        var corrected = normalized;

        // Fix common OCR errors in DIGIT positions only
        // Replace O, I, S, Z with digits when they appear after 4 letters
        corrected = Regex.Replace(corrected, @"([A-Z]{4})\s*([0-9OISZ]+)", m =>
        {
            var letters = m.Groups[1].Value;
            var digitsWithErrors = m.Groups[2].Value
                .Replace("O", "0")
                .Replace("I", "1")
                .Replace("S", "5")
                .Replace("Z", "2");
            return letters + digitsWithErrors;
        });

        var noSpaces = Regex.Replace(corrected, @"\s+", "");
        System.Diagnostics.Debug.WriteLine($">>> After correction: '{noSpaces}'");

        // Pattern 3: After correction (4 letters + 7 digits)
        match = Regex.Match(noSpaces, @"([A-Z]{4})(\d{7})");
        if (match.Success)
        {
            var letters = match.Groups[1].Value;
            var digits = match.Groups[2].Value;
            var formatted = $"{letters} {digits.Substring(0, 6)} {digits[6]}";
            System.Diagnostics.Debug.WriteLine($">>> FOUND (Corrected Pattern 3): '{formatted}'");
            return formatted;
        }

        // Pattern 4: Relaxed - 4 letters + 6-7 digits
        match = Regex.Match(noSpaces, @"([A-Z]{4})(\d{6,7})");
        if (match.Success)
        {
            var letters = match.Groups[1].Value;
            var digits = match.Groups[2].Value;

            if (digits.Length == 7)
            {
                var formatted = $"{letters} {digits.Substring(0, 6)} {digits[6]}";
                System.Diagnostics.Debug.WriteLine($">>> FOUND (Relaxed 7 digits): '{formatted}'");
                return formatted;
            }
            else if (digits.Length == 6)
            {
                var formatted = $"{letters} {digits}";
                System.Diagnostics.Debug.WriteLine($">>> FOUND (Relaxed 6 digits): '{formatted}'");
                return formatted;
            }
        }

        System.Diagnostics.Debug.WriteLine(">>> No container pattern matched");
        return null;
    }

    /// <summary>
    /// Extract seal number from text
    /// </summary>
    private static string? ExtractSeal(string text)
    {
        System.Diagnostics.Debug.WriteLine($">>> ExtractSeal from: '{text}'");

        // Normalize
        var normalized = Regex.Replace(text, @"\s+", " ").Trim().ToUpperInvariant();
        System.Diagnostics.Debug.WriteLine($">>> Normalized: '{normalized}'");

        // Try original text first (before correction)
        var noSpacesOriginal = Regex.Replace(normalized, @"\s+", "");

        // Pattern 1: Mixed alphanumeric (original text) - 6-15 chars
        // Example: YN646E4AO (9 chars)
        var matches = Regex.Matches(noSpacesOriginal, @"([A-Z0-9]{6,15})");
        foreach (Match match in matches)
        {
            var candidate = match.Groups[1].Value;

            // Skip if it's a container number pattern (4 letters + 7 digits)
            if (Regex.IsMatch(candidate, @"^[A-Z]{4}\d{7}$"))
            {
                System.Diagnostics.Debug.WriteLine($">>> Skipping container pattern: '{candidate}'");
                continue;
            }

            // Must have both letters and numbers
            bool hasLetter = Regex.IsMatch(candidate, @"[A-Z]");
            bool hasNumber = Regex.IsMatch(candidate, @"\d");

            if (hasLetter && hasNumber)
            {
                System.Diagnostics.Debug.WriteLine($">>> FOUND seal (original mixed): '{candidate}'");
                return candidate;
            }
        }

        // Now try with OCR error correction
        var corrected = normalized
            .Replace("O", "0")
            .Replace("I", "1")
            .Replace("S", "5")
            .Replace("Z", "2")
            .Replace("B", "8")  // B can be confused with 8
            .Replace("G", "6"); // G can be confused with 6

        System.Diagnostics.Debug.WriteLine($">>> After correction: '{corrected}'");

        // Remove spaces for pattern matching
        var noSpaces = Regex.Replace(corrected, @"\s+", "");

        // Pattern 2: Mixed alphanumeric (after correction) - 6-15 chars
        matches = Regex.Matches(noSpaces, @"([A-Z0-9]{6,15})");
        foreach (Match match in matches)
        {
            var candidate = match.Groups[1].Value;

            // Skip container patterns
            if (Regex.IsMatch(candidate, @"^[A-Z]{4}\d{7}$"))
            {
                System.Diagnostics.Debug.WriteLine($">>> Skipping container pattern: '{candidate}'");
                continue;
            }

            // Must have both letters and numbers
            bool hasLetter = Regex.IsMatch(candidate, @"[A-Z]");
            bool hasNumber = Regex.IsMatch(candidate, @"\d");

            if (hasLetter && hasNumber)
            {
                System.Diagnostics.Debug.WriteLine($">>> FOUND seal (corrected mixed): '{candidate}'");
                return candidate;
            }
        }

        // Pattern 3: All numeric seal (6-15 digits)
        var numericMatch = Regex.Match(noSpaces, @"(\d{6,15})");
        if (numericMatch.Success)
        {
            var result = numericMatch.Groups[1].Value;

            // Skip if it looks like part of container number (exactly 7 digits)
            if (result.Length != 7)
            {
                System.Diagnostics.Debug.WriteLine($">>> FOUND seal (numeric): '{result}'");
                return result;
            }
        }

        // Pattern 4: Fallback - any alphanumeric 6-15 chars
        var fallbackMatch = Regex.Match(noSpaces, @"([A-Z0-9]{6,15})");
        if (fallbackMatch.Success)
        {
            var result = fallbackMatch.Groups[1].Value;
            System.Diagnostics.Debug.WriteLine($">>> FOUND seal (fallback): '{result}'");
            return result;
        }

        System.Diagnostics.Debug.WriteLine(">>> No seal pattern matched");
        return null;
    }

    // Helper classes for ML Kit callbacks
    private class OnSuccessListener : Java.Lang.Object, IOnSuccessListener
    {
        private readonly Action<Java.Lang.Object> _onSuccess;

        public OnSuccessListener(Action<Java.Lang.Object> onSuccess)
        {
            _onSuccess = onSuccess;
        }

        public void OnSuccess(Java.Lang.Object? result)
        {
            if (result != null)
                _onSuccess(result);
        }
    }

    private class OnFailureListener : Java.Lang.Object, IOnFailureListener
    {
        private readonly Action<Java.Lang.Exception> _onFailure;

        public OnFailureListener(Action<Java.Lang.Exception> onFailure)
        {
            _onFailure = onFailure;
        }

        public void OnFailure(Java.Lang.Exception exception)
        {
            _onFailure(exception);
        }
    }
}



