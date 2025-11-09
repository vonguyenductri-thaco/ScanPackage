using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Graphics;
using System.Threading.Tasks;

namespace ScanPackage;

public partial class OcrCropPage : ContentPage
{
    private readonly OcrMode _mode;
    private readonly TaskCompletionSource<OcrCropResult> _tcs;
    private FileResult? _photo;
    private Size _imageDisplaySize;
    private Size _imageOriginalSize;
    private double _currentZoom = 1.0;

    public OcrCropPage(OcrMode mode, TaskCompletionSource<OcrCropResult> tcs)
    {
        InitializeComponent();
        _mode = mode;
        _tcs = tcs;

        // Set title based on mode
        TitleLabel.Text = mode == OcrMode.Container ? "Quét Container" : "Quét Seal";
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await CaptureAsync();
    }

    private async Task CaptureAsync()
    {
        try
        {
            _photo = await MediaPicker.CapturePhotoAsync(new MediaPickerOptions
            {
                Title = _mode == OcrMode.Container ? "Chụp số Container" : "Chụp số Seal"
            });

            if (_photo == null)
            {
                _tcs.TrySetResult(OcrCropResult.Canceled());
                // Đảm bảo không bị crash khi pop
                if (Navigation.NavigationStack.Count > 0)
                {
                    await Navigation.PopAsync();
                }
                return;
            }

            PhotoView.Source = ImageSource.FromFile(_photo.FullPath);

            // Lấy kích thước gốc của ảnh (Android-specific)
#if ANDROID
            try
            {
                using var stream = await _photo.OpenReadAsync();
                var options = new Android.Graphics.BitmapFactory.Options
                {
                    InJustDecodeBounds = true
                };
                await Android.Graphics.BitmapFactory.DecodeStreamAsync(stream, null, options);
                _imageOriginalSize = new Size(options.OutWidth, options.OutHeight);
                System.Diagnostics.Debug.WriteLine($"Image original size: {_imageOriginalSize.Width}x{_imageOriginalSize.Height}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting image size: {ex}");
                _imageOriginalSize = Size.Zero;
            }
#else
            _imageOriginalSize = Size.Zero;
#endif

            PhotoView.SizeChanged += OnImageSizeChanged;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CaptureAsync error: {ex}");
            await DisplayAlert("Lỗi", $"Không thể chụp ảnh: {ex.Message}", "OK");
            _tcs.TrySetResult(OcrCropResult.Canceled());
            if (Navigation.NavigationStack.Count > 0)
            {
                await Navigation.PopAsync();
            }
        }
    }

    private void OnImageSizeChanged(object? sender, EventArgs e)
    {
        if (PhotoView.Width > 0 && PhotoView.Height > 0)
        {
            _imageDisplaySize = new Size(PhotoView.Width, PhotoView.Height);
            UpdateImageSize();
        }
    }

    private void UpdateImageSize()
    {
        if (PhotoView != null && _imageDisplaySize.Width > 0 && _imageDisplaySize.Height > 0)
        {
            PhotoView.WidthRequest = _imageDisplaySize.Width * _currentZoom;
            PhotoView.HeightRequest = _imageDisplaySize.Height * _currentZoom;
        }
    }

    private void OnZoomInClicked(object sender, EventArgs e)
    {
        _currentZoom = Math.Min(_currentZoom + 0.25, 3.0);
        UpdateImageSize();
    }

    private void OnZoomOutClicked(object sender, EventArgs e)
    {
        _currentZoom = Math.Max(_currentZoom - 0.25, 1.0);
        UpdateImageSize();
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        _tcs.TrySetResult(OcrCropResult.Canceled());
        if (Navigation.NavigationStack.Count > 0)
        {
            await Navigation.PopAsync();
        }
    }

    private async void OnHelpClicked(object sender, EventArgs e)
    {
        await DisplayAlert(
            "Hướng dẫn",
            "• Kéo ảnh để đưa số vào khung xanh\n" +
            "• Dùng nút +/- để zoom\n" +
            "• Nhấn nút tròn trắng để quét vùng trong khung\n" +
            "• Hoặc nhấn 📄 để quét toàn bộ ảnh",
            "Đóng");
    }

    private async void OnRetakeClicked(object sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("=== RETAKE BUTTON CLICKED ===");
        PhotoView.SizeChanged -= OnImageSizeChanged;
        _currentZoom = 1.0;
        await CaptureAsync();
    }

    private async void OnScanRegionClicked(object sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("=== SCAN REGION BUTTON CLICKED ===");
        try
        {
            await ScanWithCropAsync();
            System.Diagnostics.Debug.WriteLine("ScanWithCropAsync completed successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnScanRegionClicked ERROR: {ex}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            await DisplayAlert("Lỗi", $"Lỗi quét vùng: {ex.Message}", "OK");
        }
    }

    private async void OnScanFullClicked(object sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("=== SCAN FULL BUTTON CLICKED ===");
        try
        {
            await ScanFullImageAsync();
            System.Diagnostics.Debug.WriteLine("ScanFullImageAsync completed successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnScanFullClicked ERROR: {ex}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            await DisplayAlert("Lỗi", $"Lỗi quét toàn bộ: {ex.Message}", "OK");
        }
    }

    private async Task ScanFullImageAsync()
    {
        try
        {
            if (_photo == null)
            {
                await DisplayAlert("Lỗi", "Không có ảnh để quét", "OK");
                return;
            }

            System.Diagnostics.Debug.WriteLine("Scanning full image...");

            // Quét toàn bộ ảnh (crop = toàn bộ ảnh)
            var fullRect = new Rect(0, 0, 1, 1);
            _tcs.TrySetResult(OcrCropResult.FromRelative(fullRect, _photo));

            if (Navigation.NavigationStack.Count > 0)
            {
                await Navigation.PopAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ScanFullImageAsync error: {ex}");
            await DisplayAlert("Lỗi", $"Lỗi khi quét: {ex.Message}", "OK");
        }
    }

    private async Task ScanWithCropAsync()
    {
        try
        {
            if (_photo == null)
            {
                await DisplayAlert("Lỗi", "Không có ảnh để quét", "OK");
                return;
            }

            System.Diagnostics.Debug.WriteLine("Scanning with crop region...");

            // Tính toán vùng crop dựa trên:
            // 1. Vị trí của CropFrame (cố định ở giữa màn hình)
            // 2. Scroll position của ScrollView
            // 3. Kích thước hiển thị của Image (có tính zoom)
            // 4. Kích thước gốc của ảnh

            await Task.Delay(100); // Đợi layout hoàn tất

        var frameW = CropFrame.Width;
        var frameH = CropFrame.Height;
        var scrollX = ImageScrollView.ScrollX;
        var scrollY = ImageScrollView.ScrollY;
        var scrollViewW = ImageScrollView.Width;
        var scrollViewH = ImageScrollView.Height;
        var imageW = PhotoView.Width;
        var imageH = PhotoView.Height;

        if (frameW <= 0 || frameH <= 0 || scrollViewW <= 0 || scrollViewH <= 0 ||
            imageW <= 0 || imageH <= 0 || _imageOriginalSize.Width <= 0 || _imageOriginalSize.Height <= 0)
        {
            // Fallback: tỉ lệ mặc định ở giữa
            _tcs.TrySetResult(OcrCropResult.FromRelative(new Rect(0.05, 0.375, 0.9, 0.25), _photo));
            await Navigation.PopAsync();
            return;
        }

        // Vị trí của CropFrame trong ScrollView (giữa màn hình)
        var frameCenterX = scrollViewW / 2.0;
        var frameCenterY = scrollViewH / 2.0;

        // Vị trí của khung trong không gian ScrollView (tính cả scroll offset)
        var frameLeftInScrollView = frameCenterX - frameW / 2.0;
        var frameTopInScrollView = frameCenterY - frameH / 2.0;

        // Vị trí của khung trong không gian Image (tính cả scroll offset)
        var frameLeftInImage = frameLeftInScrollView + scrollX;
        var frameTopInImage = frameTopInScrollView + scrollY;

        // Tính toán scale và offset cho AspectFit
        // AspectFit: ảnh được scale để fit vào Image control, có thể có letterboxing
        var imageAspect = _imageOriginalSize.Width / _imageOriginalSize.Height;
        var viewAspect = imageW / imageH;

        double actualImageW, actualImageH, offsetX, offsetY, scale;
        if (imageAspect > viewAspect)
        {
            // Ảnh rộng hơn -> letterboxing ở trên/dưới
            actualImageW = imageW;
            scale = _imageOriginalSize.Width / imageW;
            actualImageH = _imageOriginalSize.Height / scale;
            offsetX = 0;
            offsetY = (imageH - actualImageH) / 2.0;
        }
        else
        {
            // Ảnh cao hơn -> letterboxing ở trái/phải
            actualImageH = imageH;
            scale = _imageOriginalSize.Height / imageH;
            actualImageW = _imageOriginalSize.Width / scale;
            offsetX = (imageW - actualImageW) / 2.0;
            offsetY = 0;
        }

        // Chuyển đổi từ tọa độ trong Image control sang tọa độ ảnh gốc
        // Trừ đi offset của letterboxing
        var cropXInImage = frameLeftInImage - offsetX;
        var cropYInImage = frameTopInImage - offsetY;

        // Chuyển sang tọa độ ảnh gốc
        var cropX = cropXInImage * scale / _imageOriginalSize.Width;
        var cropY = cropYInImage * scale / _imageOriginalSize.Height;
        var cropW = frameW * scale / _imageOriginalSize.Width;
        var cropH = frameH * scale / _imageOriginalSize.Height;

            // Đảm bảo giá trị trong khoảng [0, 1]
            cropX = Math.Max(0, Math.Min(1, cropX));
            cropY = Math.Max(0, Math.Min(1, cropY));
            cropW = Math.Max(0, Math.Min(1 - cropX, cropW));
            cropH = Math.Max(0, Math.Min(1 - cropY, cropH));

            var relRect = new Rect(cropX, cropY, cropW, cropH);
            System.Diagnostics.Debug.WriteLine($"Crop region: {relRect}");

            _tcs.TrySetResult(OcrCropResult.FromRelative(relRect, _photo));

            if (Navigation.NavigationStack.Count > 0)
            {
                await Navigation.PopAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ScanWithCropAsync error: {ex}");
            await DisplayAlert("Lỗi", $"Lỗi khi quét: {ex.Message}", "OK");
        }
    }
}

public sealed class OcrCropResult
{
    public bool IsCanceled { get; }
    public Rect RelativeCrop { get; }
    public FileResult? Photo { get; }

    private OcrCropResult(bool canceled, Rect relativeCrop, FileResult? photo)
    {
        IsCanceled = canceled;
        RelativeCrop = relativeCrop;
        Photo = photo;
    }

    public static OcrCropResult Canceled() => new(true, Rect.Zero, null);

    public static OcrCropResult FromRelative(Rect relative, FileResult photo)
        => new(false, relative, photo);
}




