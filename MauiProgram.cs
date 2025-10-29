// MauiProgram.cs
using CommunityToolkit.Maui;
using OfficeOpenXml;
using ScanPackage;
using ZXing.Net.Maui.Controls;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        // EPPlus 8+: dùng API mới - chọn 1 trong 3 kiểu dưới đây

        // 1) Non-commercial cá nhân:
        ExcelPackage.License.SetNonCommercialPersonal("Vo Nguyen Duc Tri");

        // hoặc 2) Non-commercial tổ chức:
        // ExcelPackage.License.SetNonCommercialOrganization("<Tên tổ chức>");

        // hoặc 3) Commercial (nếu có key):
        // ExcelPackage.License.SetCommercial("<LicenseKey>");

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseBarcodeReader()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        return builder.Build();
    }
}
