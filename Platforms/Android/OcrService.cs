using Android.Graphics;
 

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
            var nav = Application.Current?.MainPage?.Navigation;
            if (nav == null) return null;
            await nav.PushAsync(cropPage);

            var cropResult = await tcs.Task;
            if (cropResult.IsCanceled || cropResult.Photo == null) return null;

            using var stream = await cropResult.Photo.OpenReadAsync();
            using var fullBitmap = await BitmapFactory.DecodeStreamAsync(stream);
            if (fullBitmap == null) return null;

            // Crop theo tỉ lệ tương đối từ overlay
            var rel = cropResult.RelativeCrop;
            int cropW = Math.Max(1, (int)(fullBitmap.Width * rel.Width));
            int cropH = Math.Max(1, (int)(fullBitmap.Height * rel.Height));
            int left = Math.Max(0, (int)(fullBitmap.Width * rel.X));
            int top = Math.Max(0, (int)(fullBitmap.Height * rel.Y));
            cropW = Math.Min(cropW, fullBitmap.Width - left);
            cropH = Math.Min(cropH, fullBitmap.Height - top);
            using var crop = Bitmap.CreateBitmap(fullBitmap, left, top, cropW, cropH);

            // TODO: Re-enable ML Kit on .NET 9 after package add
            var allText = string.Empty;
            if (string.IsNullOrWhiteSpace(allText)) return null;

            return mode switch
            {
                OcrMode.Container => ExtractContainer(allText),
                OcrMode.Seal => ExtractSeal(allText),
                _ => null
            };
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
            var nav = Application.Current?.MainPage?.Navigation;
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



