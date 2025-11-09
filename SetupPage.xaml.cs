namespace ScanPackage;

public partial class SetupPage : ContentPage
{
    public SetupPage()
    {
        InitializeComponent();   // Kết nối tới XAML
        NavigationPage.SetHasBackButton(this, false);
    }

    // Sự kiện click nút "Tạo bảng"
    private async void OnCreateClicked(object sender, EventArgs e)
    {
        if (!int.TryParse(RowsEntry.Text, out int rows) || rows <= 0)
            rows = 10;
        if (!int.TryParse(ColsEntry.Text, out int cols) || cols <= 0)
            cols = 15;

        // Hộp thoại xác nhận 2 nút
        bool confirm = await DisplayAlert(
            "Xác nhận tạo bảng",
            $"Tạo bảng với {rows} hàng và {cols} cột?",
            "Xác nhận", "Hủy bỏ");

        if (confirm)
        {
            // Chuyển đến trang nhập dữ liệu
            await Navigation.PushAsync(new DataEntryPage(rows, cols));
        }
    }

    private async void OnBackButtonClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
