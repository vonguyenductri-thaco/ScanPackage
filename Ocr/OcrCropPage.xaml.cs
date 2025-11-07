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

    public OcrCropPage(OcrMode mode, TaskCompletionSource<OcrCropResult> tcs)
    {
        InitializeComponent();
        _mode = mode;
        _tcs = tcs;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await CaptureAsync();
    }

    private async Task CaptureAsync()
    {
        _photo = await MediaPicker.CapturePhotoAsync(new MediaPickerOptions
        {
            Title = _mode == OcrMode.Container ? "Chụp số Container" : "Chụp số Seal"
        });
        if (_photo == null)
        {
            _tcs.TrySetResult(OcrCropResult.Canceled());
            await Navigation.PopAsync();
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
        }
        catch
        {
            _imageOriginalSize = Size.Zero;
        }
#else
        _imageOriginalSize = Size.Zero;
#endif

        PhotoView.SizeChanged += OnImageSizeChanged;
    }

    private void OnImageSizeChanged(object? sender, EventArgs e)
    {
        if (PhotoView.Width > 0 && PhotoView.Height > 0)
        {
            _imageDisplaySize = new Size(PhotoView.Width, PhotoView.Height);
        }
    }

    private async void OnRetakeClicked(object sender, EventArgs e)
    {
        PhotoView.SizeChanged -= OnImageSizeChanged;
        await CaptureAsync();
    }

    private async void OnConfirmClicked(object sender, EventArgs e)
    {
        if (_photo == null)
        {
            _tcs.TrySetResult(OcrCropResult.Canceled());
            await Navigation.PopAsync();
            return;
        }

        // Tính toán vùng crop dựa trên:
        // 1. Vị trí của CropFrame (cố định ở giữa màn hình)
        // 2. Scroll position của ScrollView
        // 3. Kích thước hiển thị của Image
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

        _tcs.TrySetResult(OcrCropResult.FromRelative(relRect, _photo));
        await Navigation.PopAsync();
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




