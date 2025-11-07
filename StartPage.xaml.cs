using System;
using Microsoft.Maui.Controls;

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
        await System.Threading.Tasks.Task.Delay(1200);
        await Navigation.PushAsync(new MainPage());
    }
}








