namespace ScanPackage;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Bọc MainPage trong NavigationPage
        return new Window(new NavigationPage(new MainPage()));
    }
}
