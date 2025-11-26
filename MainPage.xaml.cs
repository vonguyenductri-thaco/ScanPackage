using Microsoft.Maui.ApplicationModel.DataTransfer;
using System.Collections.ObjectModel;
using OfficeOpenXml;

#if ANDROID
using Android.Views;
using Microsoft.Maui.Platform;
#endif

namespace ScanPackage;

public partial class MainPage : ContentPage
{
    private ObservableCollection<FileItem> _files = new();
    
    // Loading overlay helpers
    private void ShowLoading(string message = "Đang tải file...")
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadingMessage.Text = message;
            LoadingOverlay.IsVisible = true;
        });
    }
    
    private void HideLoading()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadingOverlay.IsVisible = false;
        });
    }

    public ObservableCollection<FileItem> Files => _files;

    public MainPage()
    {
        InitializeComponent();
        BindingContext = this;

        // Ngăn không cho lùi về splash screen
        NavigationPage.SetHasBackButton(this, false);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadFiles();

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(100);
            ApplySafeAreaInsets();
        });
    }

    protected override bool OnBackButtonPressed()
    {
#if ANDROID
        var activity = Platform.CurrentActivity as AndroidX.AppCompat.App.AppCompatActivity;
        activity?.MoveTaskToBack(true);
#else
        // Trên các platform khác
        Application.Current?.Quit();
#endif
        return true;
    }

    private void ApplySafeAreaInsets()
    {
        try
        {
#if ANDROID
            var safeInsets = GetAndroidSafeAreaInsets();

            if (safeInsets.Top > 0)
            {
                HeaderGrid.Padding = new Thickness(10, safeInsets.Top, 10, 0);
                HeaderGrid.HeightRequest = safeInsets.Top + 44; // 22 (font) + 16 (margin) + 6 (buffer)
            }
            if (safeInsets.Bottom > 0)
            {
                FooterGrid.Padding = new Thickness(10, 20, 10, safeInsets.Bottom + 10);
                FooterGrid.HeightRequest = safeInsets.Bottom + 90;
            }
#endif
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Safe area error: {ex.Message}");
        }
    }

#if ANDROID
    private Thickness GetAndroidSafeAreaInsets()
    {
        try
        {
            var activity = Platform.CurrentActivity;
            if (activity?.Window?.DecorView?.RootWindowInsets == null)
                return new Thickness(0);

            var insets = activity.Window.DecorView.RootWindowInsets;

            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.R)
            {
                var windowInsets = insets.GetInsetsIgnoringVisibility(
                    Android.Views.WindowInsets.Type.SystemBars());

                var density = activity.Resources?.DisplayMetrics?.Density ?? 1;

                return new Thickness(
                    windowInsets.Left / density,
                    windowInsets.Top / density,
                    windowInsets.Right / density,
                    windowInsets.Bottom / density
                );
            }
            else
            {
                var density = activity.Resources?.DisplayMetrics?.Density ?? 1;

                return new Thickness(
                    insets.SystemWindowInsetLeft / density,
                    insets.SystemWindowInsetTop / density,
                    insets.SystemWindowInsetRight / density,
                    insets.SystemWindowInsetBottom / density
                );
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GetAndroidSafeAreaInsets error: {ex.Message}");
            return new Thickness(0);
        }
    }
#endif

    private void LoadFiles()
    {
        Files.Clear();
        var folder = FileSystem.AppDataDirectory;
        var files = Directory.GetFiles(folder, "*.xlsx");

        var fileList = files.OrderByDescending(File.GetCreationTime).ToList();

        for (int i = 0; i < fileList.Count; i++)
        {
            var file = fileList[i];
            var fileInfo = new FileInfo(file);
            Files.Add(new FileItem
            {
                FileName = Path.GetFileName(file),
                FullPath = file,
                FileDate = fileInfo.CreationTime.ToString("dd/MM/yyyy HH:mm"),
                IsNotLastItem = (i < fileList.Count - 1) // Item cuối = false
            });
        }
    }

    // ==================== ICON BUTTON EVENT HANDLERS ====================

    private void OnEditIconTapped(object sender, EventArgs e)
    {
        if (sender is Border border && border.BindingContext is FileItem item)
        {
            OnEditClickedAsync(item);
        }
    }

    private void OnShareIconTapped(object sender, EventArgs e)
    {
        if (sender is Border border && border.BindingContext is FileItem item)
        {
            OnShareClickedAsync(item);
        }
    }

    private void OnDeleteIconTapped(object sender, EventArgs e)
    {
        if (sender is Border border && border.BindingContext is FileItem item)
        {
            OnDeleteClickedAsync(item);
        }
    }

    // ==================== CORE ACTION METHODS ====================

    private async void OnEditClickedAsync(FileItem item)
    {
        ShowLoading("Đang tải file...");
        try
        {
            // Load file Excel và mở DataEntryPage để chỉnh sửa
            var editPage = await LoadExcelFileAsync(item.FullPath);
            if (editPage != null)
            {
                HideLoading();
                await Navigation.PushAsync(editPage);
            }
            else
            {
                HideLoading();
            }
        }
        catch (Exception ex)
        {
            HideLoading();
            await DisplayAlert("Lỗi", $"Không thể mở file để chỉnh sửa:\n{ex.Message}", "OK");
        }
    }

    private async void OnShareClickedAsync(FileItem item)
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

    private async void OnDeleteClickedAsync(FileItem item)
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

    // ==================== PARSE METADATA FROM FILENAME ====================

    private (DateTime? date, string stt, string container, string seal) ParseMetadataFromFilename(string fileName)
    {
        try
        {
            // Format: yyyy.MM.dd_STT_Container_Seal.xlsx
            // Example: 2025.01.15_123_ABCD1234567_YN646E4AO.xlsx

            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            var parts = nameWithoutExt.Split('_');

            if (parts.Length >= 4)
            {
                // Parse date
                DateTime? date = null;
                if (DateTime.TryParseExact(parts[0], "yyyy.MM.dd", null,
                    System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
                {
                    date = parsedDate;
                }

                string stt = parts[1];
                string container = parts[2];
                string seal = parts[3];

                return (date, stt, container, seal);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ParseMetadataFromFilename error: {ex.Message}");
        }

        return (null, "", "", "");
    }

    // ==================== LOAD EXCEL FILE ====================

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
        
        // Tìm sheet "Dữ liệu" và "Thông tin"
        var dataSheet = package.Workbook.Worksheets["Dữ liệu"] ?? package.Workbook.Worksheets.FirstOrDefault();
        var metaSheet = package.Workbook.Worksheets["Thông tin"];
        var checkSheet = package.Workbook.Worksheets["Phiếu kiểm tra"]; // Thêm sheet Phiếu kiểm tra

        if (dataSheet == null)
        {
            await DisplayAlert("Lỗi", "File Excel không hợp lệ", "OK");
            return null;
        }

            // Đếm số hàng và cột có dữ liệu (bỏ qua header)
        int totalRows = dataSheet.Dimension?.End.Row ?? 0;
        int totalCols = dataSheet.Dimension?.End.Column ?? 0;

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
                data[r, c] = dataSheet.Cells[r + 2, c + 2].Value?.ToString() ?? "";
                }
            }

            // Tạo DataEntryPage với kích thước từ file
            var dataPage = new DataEntryPage(rows, cols, filePath);

            // Parse metadata từ filename
            var fileName = Path.GetFileName(filePath);
            var (date, stt, container, seal) = ParseMetadataFromFilename(fileName);
            // Đợi một chút để grid được build xong, sau đó load dữ liệu và metadata
            await Task.Delay(200);
            dataPage.LoadData(data);

            // Load metadata from sheets
        if (metaSheet != null)
        {
            await LoadProductMetadataFromSheet(dataPage, metaSheet);
        }
        else if (checkSheet != null)
        {
            await LoadProductMetadataFromCheckSheet(dataPage, checkSheet);
        }
        else
        {
            // Extract from filename as fallback
            var fileNameParts = Path.GetFileNameWithoutExtension(filePath).Split('_');
            if (fileNameParts.Length >= 7)
            {
                var customer = fileNameParts[4];
                var product = fileNameParts[5];
                var model = fileNameParts[6];
                var creator = fileNameParts.Length > 7 ? fileNameParts[7] : "";
                
                await dataPage.LoadProductMetadata(customer, product, model, "", creator);
            }
        }
        
        // Load metadata vào fields
        if (date.HasValue)
        {
            dataPage.LoadMetadata(date.Value, stt, container, seal);
        }

        // Load photos from Excel file
        await dataPage.LoadPhotosFromExcel(filePath);

            return dataPage;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", $"Không thể đọc file Excel:\n{ex.Message}", "OK");
            return null;
        }
    }

    private async Task LoadProductMetadataFromSheet(DataEntryPage dataPage, ExcelWorksheet metaSheet)
    {
        try
        {
            string customer = metaSheet.Cells["B5"].Value?.ToString() ?? "";
            string product = metaSheet.Cells["B6"].Value?.ToString() ?? "";
            string model = metaSheet.Cells["B7"].Value?.ToString() ?? "";
            string creatorInfo = metaSheet.Cells["B8"].Value?.ToString() ?? "";

            string msnv = "";
            string creatorName = "";
            
            if (!string.IsNullOrEmpty(creatorInfo))
            {
                var match = System.Text.RegularExpressions.Regex.Match(creatorInfo, @"MSNV:\s*(\w+)\)");
                if (match.Success)
                {
                    msnv = match.Groups[1].Value;
                    var nameMatch = System.Text.RegularExpressions.Regex.Match(creatorInfo, @"^(.+?)\s*\(\s*MSNV:");
                    if (nameMatch.Success)
                    {
                        creatorName = nameMatch.Groups[1].Value.Trim();
                    }
                }
                else
                {
                    creatorName = creatorInfo.Trim();
                }
            }

            await dataPage.LoadProductMetadata(customer, product, model, msnv, creatorName);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadProductMetadataFromSheet error: {ex.Message}");
        }
    }

    private async Task LoadProductMetadataFromCheckSheet(DataEntryPage dataPage, ExcelWorksheet checkSheet)
    {
        try
        {
            string customer = checkSheet.Cells["D4"].Value?.ToString() ?? "";
            string product = checkSheet.Cells["D5"].Value?.ToString() ?? "";
            string model = checkSheet.Cells["D6"].Value?.ToString() ?? "";
            string creatorName = checkSheet.Cells["L9"].Value?.ToString() ?? "";

            await dataPage.LoadProductMetadata(customer, product, model, "", creatorName);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadProductMetadataFromCheckSheet error: {ex.Message}");
        }
    }

    // ==================== CREATE NEW FILE ====================

    private async void OnCreateClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new SetupPage());
    }
}

// ==================== FILE ITEM CLASS ====================

public class FileItem
{
    public string FileName { get; set; } = "";
    public string FullPath { get; set; } = "";
    public string FileDate { get; set; } = "";
    public bool IsNotLastItem { get; set; } = true; // Mặc định true, item cuối = false
}