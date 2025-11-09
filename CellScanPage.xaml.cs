using System;
using System.Linq;
using Microsoft.Maui.Controls;
using BarcodeScanning;

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

        // Configure barcode formats
        BarcodeView.BarcodeSymbologies = BarcodeFormats.All;
    }

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        if (!_completed)
        {
            _completed = true;
            _tcs.TrySetResult(null);
        }
        await Navigation.PopModalAsync();
    }

    private async void OnBarcodesDetected(object sender, OnDetectionFinishedEventArg e)
    {
        if (_completed) return;

        var value = e.BarcodeResults?.FirstOrDefault()?.DisplayValue;
        if (string.IsNullOrWhiteSpace(value)) return;

        _completed = true;

        _tcs.TrySetResult(value.Trim());
        await MainThread.InvokeOnMainThreadAsync(async () => await Navigation.PopModalAsync());
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
        BarcodeView.AnchorX = 0.5;
        BarcodeView.AnchorY = 0.5;
        BarcodeView.Scale = zoomFactor;
    }

    private void OnFlashClicked(object sender, EventArgs e)
    {
        try
        {
            _isFlashOn = !_isFlashOn;
            BarcodeView.TorchOn = _isFlashOn;

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



