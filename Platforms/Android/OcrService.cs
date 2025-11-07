using Android.Graphics;
using Xamarin.Google.MLKit.Vision.Text;
using Xamarin.Google.MLKit.Vision.Text.Latin;
using Xamarin.Google.MLKit.Vision.Common;
using Android.Gms.Tasks;
using Java.Lang;

namespace ScanPackage;

public class AndroidOcrService : IOcrService
{
    public async Task<string?> ScanTextAsync(OcrMode mode)
    {
        try
        {
            // Mở trang chọn vùng quét ngang và nhận kết quả crop tương đối
            var tcs = new TaskCompletionSource<OcrCropResult>();
            var cropPage = new OcrCropPage(mode, tcs);
            var window = Application.Current?.Windows?.FirstOrDefault();
            var nav = window?.Page?.Navigation;
            if (nav == null) return null;
            await nav.PushAsync(cropPage);

            var cropResult = await tcs.Task;
            if (cropResult.IsCanceled || cropResult.Photo == null) return null;

            using var stream = await cropResult.Photo.OpenReadAsync();
            using var fullBitmap = await BitmapFactory.DecodeStreamAsync(stream);
            if (fullBitmap == null) return null;

            // Crop theo tỉ lệ tương đối từ overlay
            var rel = cropResult.RelativeCrop;
            int cropW = System.Math.Max(1, (int)(fullBitmap.Width * rel.Width));
            int cropH = System.Math.Max(1, (int)(fullBitmap.Height * rel.Height));
            int left = System.Math.Max(0, (int)(fullBitmap.Width * rel.X));
            int top = System.Math.Max(0, (int)(fullBitmap.Height * rel.Y));
            cropW = System.Math.Min(cropW, fullBitmap.Width - left);
            cropH = System.Math.Min(cropH, fullBitmap.Height - top);
            using var crop = Bitmap.CreateBitmap(fullBitmap, left, top, cropW, cropH);

            // Sử dụng ML Kit Text Recognition để OCR
            try
            {
                var options = new Xamarin.Google.MLKit.Vision.Text.Latin.TextRecognizerOptions.Builder().Build();
                var textRecognizer = Xamarin.Google.MLKit.Vision.Text.TextRecognition.GetClient(options);
                
                // Chuyển Bitmap sang InputImage
                var inputImage = Xamarin.Google.MLKit.Vision.Common.InputImage.FromBitmap(crop, 0);
                
                // Xử lý OCR
                var task = textRecognizer.Process(inputImage);
                var textResult = await System.Threading.Tasks.Task.Run(() =>
                {
                    var tcs = new System.Threading.Tasks.TaskCompletionSource<Xamarin.Google.MLKit.Vision.Text.Text?>();
                    task.AddOnSuccessListener(new TextRecognitionTaskListener(tcs));
                    task.AddOnFailureListener(new TextRecognitionFailureTaskListener(tcs));
                    return tcs.Task;
                });

                if (textResult == null) return null;

                // Lấy tất cả text từ TextBlocks
                var allText = string.Empty;
                foreach (var block in textResult.TextBlocks)
                {
                    foreach (var line in block.Lines)
                    {
                        if (!string.IsNullOrWhiteSpace(line.Text))
                        {
                            allText += line.Text + " ";
                        }
                    }
                }
                allText = allText.Trim();
                textRecognizer.Dispose();

                if (string.IsNullOrWhiteSpace(allText)) return null;

                return mode switch
                {
                    OcrMode.Container => ExtractContainer(allText),
                    OcrMode.Seal => ExtractSeal(allText),
                    _ => null
                };
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OCR Error: {ex}");
                return null;
            }
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> ScanLiveAsync(OcrMode mode)
    {
        try
        {
            var tcs = new TaskCompletionSource<string?>();
            var page = new LiveOcrPage(mode, tcs);
            var window = Application.Current?.Windows?.FirstOrDefault();
            var nav = window?.Page?.Navigation;
            if (nav == null) return null;
            await nav.PushModalAsync(page);

            // TODO: attach CameraX + ML Kit analyzer to page.CameraHost and resolve tcs when detected
            var result = await tcs.Task;
            return result;
        }
        catch
        {
            return null;
        }
    }

    

    private static string? ExtractContainer(string text)
    {
        // Common container formats like ABCD1234567 or KOCU 411486 2
        var patterns = new[]
        {
            @"\b[A-Z]{4}\s?-?\s?\d{6,7}\b",
            @"\b[A-Z]{3}[A-Z]?\d{6,7}\b"
        };
        foreach (var pat in patterns)
        {
            var m = System.Text.RegularExpressions.Regex.Match(text, pat);
            if (m.Success)
                return NormalizeContainer(m.Value);
        }
        return null;
    }

    private static string NormalizeContainer(string raw)
    {
        var cleaned = new string(raw.Where(char.IsLetterOrDigit).ToArray());
        return cleaned;
    }

    private static string? ExtractSeal(string text)
    {
        // Seal often alphanumeric length 6-12, e.g., VN64554AO
        var m = System.Text.RegularExpressions.Regex.Match(text, @"\b[A-Z0-9]{6,12}\b");
        return m.Success ? m.Value.ToUpperInvariant() : null;
    }
}

// Helper classes for ML Kit async processing
internal class TextRecognitionTaskListener : Java.Lang.Object, IOnSuccessListener
{
    private readonly System.Threading.Tasks.TaskCompletionSource<Xamarin.Google.MLKit.Vision.Text.Text?> _tcs;

    public TextRecognitionTaskListener(System.Threading.Tasks.TaskCompletionSource<Xamarin.Google.MLKit.Vision.Text.Text?> tcs)
    {
        _tcs = tcs;
    }

    public void OnSuccess(Java.Lang.Object? result)
    {
        var textResult = Android.Runtime.Extensions.JavaCast<Xamarin.Google.MLKit.Vision.Text.Text>(result);
        _tcs.TrySetResult(textResult);
    }
}

internal class TextRecognitionFailureTaskListener : Java.Lang.Object, IOnFailureListener
{
    private readonly System.Threading.Tasks.TaskCompletionSource<Xamarin.Google.MLKit.Vision.Text.Text?> _tcs;

    public TextRecognitionFailureTaskListener(System.Threading.Tasks.TaskCompletionSource<Xamarin.Google.MLKit.Vision.Text.Text?> tcs)
    {
        _tcs = tcs;
    }

    public void OnFailure(Java.Lang.Exception e)
    {
        System.Diagnostics.Debug.WriteLine($"Text Recognition failed: {e}");
        _tcs.TrySetResult(null);
    }
}



