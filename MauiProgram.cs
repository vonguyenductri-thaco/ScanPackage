// MauiProgram.cs
using CommunityToolkit.Maui;
using OfficeOpenXml;
using ScanPackage;
using BarcodeScanning;
using Plugin.CloudFirestore;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // EPPlus license context for older versions
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseBarcodeScanning()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if ANDROID
        // Register Android OCR service
        builder.Services.AddSingleton<IOcrService, AndroidOcrService>();
        
        // Initialize Firebase
        try
        {
            System.Diagnostics.Debug.WriteLine("=== INITIALIZING FIREBASE ===");
            // Firebase auto-initializes when accessing CrossCloudFirestore.Current.Instance
            var firestore = Plugin.CloudFirestore.CrossCloudFirestore.Current.Instance;
            System.Diagnostics.Debug.WriteLine($"Firebase initialized successfully: {firestore != null}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Firebase initialization error: {ex.Message}");
        }
#endif

        // Register CloudProductService
        builder.Services.AddSingleton<CloudProductService>();

        return builder.Build();
    }
}
