using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace ScanPackage;

public partial class ExportSuccessPopup : ContentPage
{
    private readonly string _filePath;
    private readonly TaskCompletionSource<PopupAction> _tcs;

    public ExportSuccessPopup(string filePath, bool isEditing, TaskCompletionSource<PopupAction> tcs)
    {
        InitializeComponent();
        _filePath = filePath;
        _tcs = tcs;

        // Cập nhật message dựa trên isEditing
        MessageLabel.Text = isEditing ? "Đã cập nhật file thành công!" : "Đã lưu file thành công!";
    }

    private async void OnShareClicked(object sender, EventArgs e)
    {
        _tcs.TrySetResult(PopupAction.Share);
        await Navigation.PopModalAsync(false);
    }

    private async void OnHomeClicked(object sender, EventArgs e)
    {
        _tcs.TrySetResult(PopupAction.Home);
        await Navigation.PopModalAsync(false);
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        _tcs.TrySetResult(PopupAction.Cancel);
        await Navigation.PopModalAsync(false);
    }

    // Ngăn back button
    protected override bool OnBackButtonPressed()
    {
        _tcs.TrySetResult(PopupAction.Cancel);
        Navigation.PopModalAsync(false);
        return true;
    }
}

public enum PopupAction
{
    Share,
    Home,
    Cancel
}

