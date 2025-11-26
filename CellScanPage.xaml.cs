using System;
using System.Linq;
using Microsoft.Maui.Controls;
using BarcodeScanning;

#if ANDROID
using Microsoft.Maui.Platform;
using Android.Views;
#endif

namespace ScanPackage;

public partial class CellScanPage : ContentPage
{
    private readonly System.Threading.Tasks.TaskCompletionSource<string?> _tcs;
    private bool _completed;
    private bool _isFlashOn = false;
    private double _currentScale = 1.0;
    private double _startScale = 1.0;

    public CellScanPage(System.Threading.Tasks.TaskCompletionSource<string?> tcs)
    {
        InitializeComponent();
        _tcs = tcs;

        // Configure barcode formats in code
        BarcodeView.BarcodeSymbologies = BarcodeFormats.All;

        // Setup custom overlay with rounded cutout
        OverlayGraphics.Drawable = new RoundedCutoutDrawable
        {
            CutoutWidth = 372,
            CutoutHeight = 220,
            CornerRadius = 20,
            OverlayColor = Color.FromRgba(0, 0, 0, 0.50f)
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Subscribe to grid size changes
        if (ZoomSlider.Parent is Grid grid)
        {
            grid.SizeChanged += OnGridSizeChanged;
        }

        // Apply safe area và initialize custom thumb position after layout is ready
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(100);
            ApplySafeAreaInsets();
            await Task.Delay(100);
            UpdateCustomThumbPosition();
        });
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // QUAN TRỌNG: Tắt camera trước khi trang đóng để giải phóng tài nguyên
        DisableCamera();

        // Unsubscribe from events
        if (ZoomSlider.Parent is Grid grid)
        {
            grid.SizeChanged -= OnGridSizeChanged;
        }
    }

    private void OnGridSizeChanged(object sender, EventArgs e)
    {
        UpdateCustomThumbPosition();
    }

    // ==================== CAMERA CONTROL ====================

    private void DisableCamera()
    {
        try
        {
            // Tắt camera để giải phóng tài nguyên native
            var cameraEnabledProperty = BarcodeView.GetType().GetProperty("CameraEnabled");
            if (cameraEnabledProperty != null && cameraEnabledProperty.CanWrite)
            {
                cameraEnabledProperty.SetValue(BarcodeView, false);
                System.Diagnostics.Debug.WriteLine("Camera disabled successfully");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DisableCamera error: {ex.Message}");
        }
    }

    // ==================== SAFE AREA HANDLING ====================

    private void ApplySafeAreaInsets()
    {
        try
        {
#if ANDROID
            var safeInsets = GetAndroidSafeAreaInsets();

            if (safeInsets.Top > 0)
            {
                HeaderGrid.Padding = new Thickness(10, safeInsets.Top, 10, 0);
                HeaderGrid.MinimumHeightRequest = safeInsets.Top + 44;
            }
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Safe area error: {ex.Message}");
        }
    }

#if ANDROID
    private Thickness GetAndroidSafeAreaInsets()
    {
        try
        {
            var activity = Platform.CurrentActivity;
            if (activity?.Window?.DecorView?.RootWindowInsets == null)
                return new Thickness(0);

            var insets = activity.Window.DecorView.RootWindowInsets;

            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.R)
            {
                var windowInsets = insets.GetInsetsIgnoringVisibility(
                    WindowInsets.Type.SystemBars());

                var density = activity.Resources?.DisplayMetrics?.Density ?? 1;

                return new Thickness(
                    windowInsets.Left / density,
                    windowInsets.Top / density,
                    windowInsets.Right / density,
                    windowInsets.Bottom / density
                );
            }
            else
            {
                var density = activity.Resources?.DisplayMetrics?.Density ?? 1;

                return new Thickness(
                    insets.SystemWindowInsetLeft / density,
                    insets.SystemWindowInsetTop / density,
                    insets.SystemWindowInsetRight / density,
                    insets.SystemWindowInsetBottom / density
                );
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetAndroidSafeAreaInsets error: {ex.Message}");
            return new Thickness(0);
        }
    }
#endif

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        if (!_completed)
        {
            _completed = true;
            _tcs.TrySetResult(null);
        }

        // Tắt camera trước khi đóng trang
        DisableCamera();
        
        await Task.Delay(50); // Chờ một chút để camera tắt hoàn toàn
        await Navigation.PopModalAsync();
    }

    private async void OnBarcodesDetected(object sender, OnDetectionFinishedEventArg e)
    {
        if (_completed) return;

        var value = e.BarcodeResults?.FirstOrDefault()?.DisplayValue;
        if (string.IsNullOrWhiteSpace(value)) return;

        _completed = true;
        _tcs.TrySetResult(value.Trim());

        // Tắt camera trước khi đóng trang
        DisableCamera();

        await Task.Delay(50); // Chờ một chút để camera tắt hoàn toàn
        await MainThread.InvokeOnMainThreadAsync(async () => await Navigation.PopModalAsync());
    }

    // ==================== ZOOM CONTROLS ====================

    private void OnZoomChanged(object sender, ValueChangedEventArgs e)
    {
        try
        {
            _currentScale = e.NewValue;
            ApplyZoom(_currentScale);
            UpdateCustomThumbPosition();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Zoom error: {ex.Message}");
        }
    }

    private void UpdateCustomThumbPosition()
    {
        try
        {
            if (CustomThumb == null || ZoomSlider.Parent is not Grid grid) return;

            if (grid.Width <= 0) return;

            var sliderValue = ZoomSlider.Value;
            var sliderMin = ZoomSlider.Minimum;
            var sliderMax = ZoomSlider.Maximum;
            var sliderRange = sliderMax - sliderMin;

            var normalizedValue = (sliderValue - sliderMin) / sliderRange;

            var thumbWidth = CustomThumb.Width > 0 ? CustomThumb.Width : 31;
            var trackWidth = grid.Width - thumbWidth;

            var thumbPosition = normalizedValue * trackWidth;

            CustomThumb.TranslationX = thumbPosition;

            System.Diagnostics.Debug.WriteLine($"Thumb Position: {thumbPosition:F2}, Slider Value: {sliderValue:F2}, Track Width: {trackWidth:F2}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateCustomThumbPosition error: {ex.Message}");
        }
    }

    private void OnPinchUpdated(object sender, PinchGestureUpdatedEventArgs e)
    {
        try
        {
            if (e.Status == GestureStatus.Started)
            {
                _startScale = _currentScale;
            }
            else if (e.Status == GestureStatus.Running)
            {
                var newScale = _startScale * e.Scale;
                newScale = Math.Max(1.0, Math.Min(newScale, 5.0));

                _currentScale = newScale;
                ApplyZoom(_currentScale);

                ZoomSlider.Value = _currentScale;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Pinch error: {ex.Message}");
        }
    }

    private void ApplyZoom(double zoomFactor)
    {
        try
        {
            var zoomProperty = BarcodeView.GetType().GetProperty("RequestZoomFactor");
            if (zoomProperty != null && zoomProperty.CanWrite)
            {
                zoomProperty.SetValue(BarcodeView, (float)zoomFactor);
                System.Diagnostics.Debug.WriteLine($"Applied RequestZoomFactor: {zoomFactor:F2}");
            }
            else
            {
                BarcodeView.AnchorX = 0.5;
                BarcodeView.AnchorY = 0.5;
                BarcodeView.Scale = zoomFactor;
                System.Diagnostics.Debug.WriteLine($"Applied Scale: {zoomFactor:F2}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ApplyZoom error: {ex.Message}");
        }
    }

    // ==================== FLASH CONTROL ====================

    private void OnFlashClicked(object sender, EventArgs e)
    {
        try
        {
            _isFlashOn = !_isFlashOn;

            var torchProperty = BarcodeView.GetType().GetProperty("TorchOn");
            if (torchProperty != null && torchProperty.CanWrite)
            {
                torchProperty.SetValue(BarcodeView, _isFlashOn);
            }

            if (FlashCircle != null)
            {
                FlashCircle.Fill = _isFlashOn ? Color.FromArgb("#F9C41C") : Colors.White;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Flash error: {ex.Message}");
        }
    }
}