namespace ScanPackage;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // Always use light theme regardless of system setting
        UserAppTheme = AppTheme.Light;
        RequestedThemeChanged += (_, __) => UserAppTheme = AppTheme.Light;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Bắt đầu bằng trang StartPage (logo), sau đó điều hướng sang MainPage
        var navPage = new NavigationPage(new StartPage())
        {
            BarBackgroundColor = Microsoft.Maui.Graphics.Colors.Transparent,
            BarTextColor = Microsoft.Maui.Graphics.Colors.Transparent,
            Title = ""
        };
        
        // Ẩn navigation bar hoàn toàn trên Android
        #if ANDROID
        Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.AppCompat.NavigationPage.SetBarHeight(navPage, 0);
        #endif
        
        return new Window(navPage);
    }
}
