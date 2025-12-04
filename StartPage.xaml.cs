using System;
using Microsoft.Maui.Controls;
using ScanPackage.Helpers;

namespace ScanPackage;

public partial class StartPage : ContentPage
{
    public StartPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Áp dụng Safe Area cho StartPage
        ApplySafeAreaForStartPage();
        
        await System.Threading.Tasks.Task.Delay(1200);
        await Navigation.PushAsync(new MainPage());
    }

    private void ApplySafeAreaForStartPage()
    {
        try
        {
            var safeInsets = SafeAreaHelper.GetSafeAreaInsets();
            
            // Áp dụng padding cho RootGrid để tránh notch
            if (safeInsets.Top > 0 || safeInsets.Bottom > 0)
            {
                RootGrid.Padding = new Thickness(
                    Math.Max(40, safeInsets.Left),
                    Math.Max(40, safeInsets.Top),
                    Math.Max(40, safeInsets.Right),
                    Math.Max(40, safeInsets.Bottom)
                );
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"StartPage Safe Area error: {ex.Message}");
        }
    }
}








