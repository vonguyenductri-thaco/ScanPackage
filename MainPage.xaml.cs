using Microsoft.Maui.ApplicationModel.DataTransfer;
using System.Collections.ObjectModel;
using OfficeOpenXml;

namespace ScanPackage;

public partial class MainPage : ContentPage
{
    public ObservableCollection<FileItem> Files { get; set; } = new();

    public MainPage()
    {
        InitializeComponent();
        BindingContext = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadFiles(); // quay lại trang là refresh danh sách
    }

    private void LoadFiles()
    {
        Files.Clear();
        var folder = FileSystem.AppDataDirectory;
        var files = Directory.GetFiles(folder, "*.xlsx");

        foreach (var file in files.OrderByDescending(File.GetCreationTime))
        {
            var fileInfo = new FileInfo(file);
            Files.Add(new FileItem
            {
                FileName = Path.GetFileName(file),
                FullPath = file,
                FileDate = fileInfo.CreationTime.ToString("dd/MM/yyyy HH:mm")
            });
        }
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is FileItem item)
        {
            try
            {
                // Load file Excel và mở DataEntryPage để chỉnh sửa
                var editPage = await LoadExcelFileAsync(item.FullPath);
                if (editPage != null)
                {
                    await Navigation.PushAsync(editPage);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi", $"Không thể mở file để chỉnh sửa:\n{ex.Message}", "OK");
            }
        }
    }

    private async void OnShareClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is FileItem item)
        {
            try
            {
                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "Chia sẻ file Excel",
                    File = new ShareFile(item.FullPath)
                });
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi", $"Không thể chia sẻ file:\n{ex.Message}", "OK");
            }
        }
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is FileItem item)
        {
            var confirm = await DisplayAlert(
                "Xác nhận xóa",
                $"Bạn có chắc muốn xóa file:\n{item.FileName}?",
                "Xóa",
                "Hủy");

            if (confirm)
            {
                try
                {
                    File.Delete(item.FullPath);
                    LoadFiles();
                    await DisplayAlert("Thành công", "Đã xóa file thành công", "OK");
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Lỗi", $"Không thể xóa file:\n{ex.Message}", "OK");
                }
            }
        }
    }

    private async Task<DataEntryPage?> LoadExcelFileAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                await DisplayAlert("Lỗi", "File không tồn tại", "OK");
                return null;
            }

            using var package = new ExcelPackage(new FileInfo(filePath));
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();
            
            if (worksheet == null)
            {
                await DisplayAlert("Lỗi", "File Excel không hợp lệ", "OK");
                return null;
            }

            // Đếm số hàng và cột có dữ liệu (bỏ qua header)
            int totalRows = worksheet.Dimension?.End.Row ?? 0;
            int totalCols = worksheet.Dimension?.End.Column ?? 0;

            if (totalRows <= 1 || totalCols <= 1)
            {
                await DisplayAlert("Lỗi", "File Excel không có dữ liệu", "OK");
                return null;
            }

            // Số hàng và cột dữ liệu (bỏ qua hàng header và cột header)
            int rows = totalRows - 1; // Bỏ hàng đầu (header cột)
            int cols = totalCols - 1; // Bỏ cột đầu (header hàng)

            // Đọc dữ liệu từ Excel vào mảng (bỏ qua header)
            // Hàng 1 là header cột, cột 1 là header hàng
            string[,] data = new string[rows, cols];
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    // Bỏ qua hàng 1 (header) và cột 1 (header), bắt đầu từ hàng 2, cột 2
                    data[r, c] = worksheet.Cells[r + 2, c + 2].Value?.ToString() ?? "";
                }
            }

            // Tạo DataEntryPage với kích thước từ file
            var dataPage = new DataEntryPage(rows, cols, filePath);

            // Đợi một chút để grid được build xong, sau đó load dữ liệu
            await Task.Delay(200);
            dataPage.LoadData(data);

            return dataPage;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", $"Không thể đọc file Excel:\n{ex.Message}", "OK");
            return null;
        }
    }

    private async void OnCreateClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new SetupPage());
    }
}

public class FileItem
{
    public string FileName { get; set; } = "";
    public string FullPath { get; set; } = "";
    public string FileDate { get; set; } = "";
}
