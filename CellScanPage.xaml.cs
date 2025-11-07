using System;
using System.Linq;
using Microsoft.Maui.Controls;
using ZXing.Net.Maui;

namespace ScanPackage;

public partial class CellScanPage : ContentPage
{
    private readonly System.Threading.Tasks.TaskCompletionSource<string?> _tcs;
    private bool _completed;

    public CellScanPage(System.Threading.Tasks.TaskCompletionSource<string?> tcs)
    {
        InitializeComponent();
        _tcs = tcs;

        // Cải thiện nhận dạng: chỉ định định dạng, bật TryHarder và AutoRotate
        BarcodeView.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormat.Code128 |
                      BarcodeFormat.Code39  |
                      BarcodeFormat.Code93  |
                      BarcodeFormat.Ean13   |
                      BarcodeFormat.Ean8    |
                      BarcodeFormat.Codabar |
                      BarcodeFormat.UpcA    |
                      BarcodeFormat.UpcE    |
                      BarcodeFormat.QrCode,
            AutoRotate = true,
            TryHarder = true,
            TryInverted = true,
            Multiple = false
        };
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

    private async void OnBarcodesDetected(object sender, BarcodeDetectionEventArgs e)
    {
        if (_completed) return;

        var value = e.Results?.FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(value)) return;

        _completed = true;
        _tcs.TrySetResult(value.Trim());
        await MainThread.InvokeOnMainThreadAsync(async () => await Navigation.PopModalAsync());
    }
}



