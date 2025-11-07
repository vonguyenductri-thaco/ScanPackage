namespace ScanPackage;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Bắt đầu bằng trang StartPage (logo), sau đó điều hướng sang MainPage
        return new Window(new NavigationPage(new StartPage()));
    }
}
