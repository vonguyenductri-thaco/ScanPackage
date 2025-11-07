using System;
using Microsoft.Maui.Controls;

namespace ScanPackage;

public partial class LiveOcrPage : ContentPage
{
    private readonly OcrMode _mode;
    private readonly System.Threading.Tasks.TaskCompletionSource<string?> _tcs;

    // Vùng quét để OCR (tọa độ tương đối)
    public Microsoft.Maui.Graphics.Rect ScanArea { get; private set; }

    public LiveOcrPage(OcrMode mode, System.Threading.Tasks.TaskCompletionSource<string?> tcs)
    {
        InitializeComponent();
        _mode = mode;
        _tcs = tcs;

        Title = mode == OcrMode.Container ? "Quét số Container" : "Quét số Seal";
        
        // Update label text based on mode
        ContainerLabel.Text = mode == OcrMode.Container ? "Số Container" : "Số Seal";
        
        // Tính toán vùng quét sau khi layout được render
        ScanFrame.SizeChanged += OnScanFrameSizeChanged;
    }

    private void OnScanFrameSizeChanged(object? sender, EventArgs e)
    {
        if (ScanFrame != null && CameraPreview != null)
        {
            // Tính toán vùng quét tương đối (0-1)
            var scanFrameBounds = ScanFrame.Bounds;
            var cameraBounds = CameraPreview.Bounds;
            
            if (cameraBounds.Width > 0 && cameraBounds.Height > 0)
            {
                ScanArea = new Microsoft.Maui.Graphics.Rect(
                    scanFrameBounds.X / cameraBounds.Width,
                    scanFrameBounds.Y / cameraBounds.Height,
                    scanFrameBounds.Width / cameraBounds.Width,
                    scanFrameBounds.Height / cameraBounds.Height
                );
            }
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Native camera/analysis will be attached per-platform
        OnAppearingPlatform();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Cleanup camera resources per-platform
        OnDisappearingPlatform();
    }

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        _tcs.TrySetResult(null);
        await Navigation.PopModalAsync();
    }

    private async void OnConfirmClicked(object sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(ResultLabel.Text))
        {
            _tcs.TrySetResult(ResultLabel.Text);
            await Navigation.PopModalAsync();
        }
    }

    public void UpdateResult(string text)
    {
        Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
        {
            ResultLabel.Text = text;
            CheckmarkIcon.IsVisible = true;
            ConfirmButton.IsEnabled = true;
        });
    }

    private void OnCaptureClicked(object sender, EventArgs e)
    {
        CaptureOncePlatform();
    }

    partial void OnAppearingPlatform();
    partial void OnDisappearingPlatform();
    partial void CaptureOncePlatform();
}


