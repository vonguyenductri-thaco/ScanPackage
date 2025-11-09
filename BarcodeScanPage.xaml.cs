using BarcodeScanning;

namespace ScanPackage;

public partial class BarcodeScanPage : ContentPage
{
    private readonly Action<string> _onResult;
    private bool _isProcessing = false;
    private bool _isFlashOn = false;
    private double _currentScale = 1.0;
    private double _startScale = 1.0;

    public BarcodeScanPage(Action<string> onResult)
    {
        InitializeComponent();
        _onResult = onResult;

        // Configure barcode formats
        cameraView.BarcodeSymbologies = BarcodeFormats.All;
    }

    private async void CameraView_BarcodesDetected(object sender, OnDetectionFinishedEventArg e)
    {
        var result = e.BarcodeResults?.FirstOrDefault()?.DisplayValue;
        if (string.IsNullOrEmpty(result)) return;

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            if (_isProcessing) return;

            _isProcessing = true;

            cameraView.OnDetectionFinished -= CameraView_BarcodesDetected;

            try
            {
                if (Navigation.NavigationStack.Count > 1)
                {
                    await Navigation.PopAsync();
                }

                _onResult?.Invoke(result);
            }
            catch
            {
                try
                {
                    if (Navigation.NavigationStack.Count > 1)
                    {
                        await Navigation.PopAsync();
                    }
                    _onResult?.Invoke(result);
                }
                catch { }
            }
        });
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        if (Navigation.NavigationStack.Count > 1)
        {
            await Navigation.PopAsync();
        }
    }

    private void OnZoomChanged(object sender, ValueChangedEventArgs e)
    {
        try
        {
            _currentScale = e.NewValue;
            ApplyZoom(_currentScale);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Zoom error: {ex.Message}");
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
        cameraView.AnchorX = 0.5;
        cameraView.AnchorY = 0.5;
        cameraView.Scale = zoomFactor;
    }

    private void OnFlashClicked(object sender, EventArgs e)
    {
        try
        {
            _isFlashOn = !_isFlashOn;
            cameraView.TorchOn = _isFlashOn;

            if (_isFlashOn)
            {
                FlashButton.BackgroundColor = Color.FromArgb("#007AFF");
            }
            else
            {
                FlashButton.BackgroundColor = Color.FromArgb("#40FFFFFF");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Flash error: {ex.Message}");
        }
    }
}
