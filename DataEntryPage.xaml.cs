using System;
using System.Collections.Generic;
using IOPath = System.IO.Path;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Storage;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using OfficeOpenXml;
using MauiTextAlignment = Microsoft.Maui.TextAlignment;
using Android.Service.Credentials;


#if ANDROID
using AApp = Android.App.Application;
using Android.Media;
using Microsoft.Maui.Platform;
#endif

namespace ScanPackage;

public partial class DataEntryPage : ContentPage
{
    private readonly int _rows;
    private readonly int _cols;

    // Loading overlay helpers
    private void ShowLoading(string message = "Đang xử lý...")
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                var loadingMessage = this.FindByName<Label>("LoadingMessage");
                var loadingOverlay = this.FindByName<ContentView>("LoadingOverlay");

                if (loadingMessage != null)
                    loadingMessage.Text = message;
                if (loadingOverlay != null)
                    loadingOverlay.IsVisible = true;
            }
            catch (Exception)
            {
                // Fallback if controls not found
            }
        });
    }

    private void HideLoading()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                var loadingOverlay = this.FindByName<ContentView>("LoadingOverlay");
                if (loadingOverlay != null)
                    loadingOverlay.IsVisible = false;
            }
            catch (Exception)
            {
                // Fallback if controls not found
            }
        });
    }

    private readonly string[,] _cellValues;
    private readonly Dictionary<(int r, int c), Label> _cellLabelMap = new();
    private readonly Dictionary<(int r, int c), Border> _cellBorderMap = new();

    private Label? _highlightLabel;
    private string? _existingFilePath;

    private string[,]? _originalCellValues;
    private bool _isProcessing = false;
    private bool _isAnyFieldFocused = false;
    private bool _navigationConfirmed = false;

    private string? _selectedCustomer;
    private string? _selectedProduct;
    private string? _selectedModel;
    private UserData? _selectedUser;

    private string? _photo1Path;
    private string? _photo2Path;
    private string? _photo3Path;
    private string? _photo4Path;

    private string? _originalCustomer;
    private string? _originalProduct;
    private string? _originalModel;
    private UserData? _originalUser;

    private static readonly Color DuplicateColor = Color.FromArgb("#FFF9C4");
    private static readonly Color NormalColor = Colors.White;
    private static readonly Color HighlightColor = Color.FromArgb("#E3F2FD");

    public DataEntryPage(int rows, int cols) : this(rows, cols, null)
    {
    }

    public DataEntryPage(int rows, int cols, string? existingFilePath)
    {
        InitializeComponent();

        _rows = rows;
        _cols = cols;
        _cellValues = new string[_rows, _cols];
        _existingFilePath = existingFilePath;

        DateEntry.Date = DateTime.Now;

        NavigationPage.SetHasBackButton(this, false);

        SubscribeToFocusEvents();
        BuildGrid();

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await LoadProductDataAsync();
            await LoadPickerData();
        });
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _isProcessing = false;
        _navigationConfirmed = false;
        SetUIEnabled(true);

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(100);
            ApplySafeAreaInsets();
            UpdateDuplicateHighlighting();
            SetDynamicTableHeight();
        });
    }

    protected override bool OnBackButtonPressed()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await HandleBackNavigation();
        });
        return true;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        try
        {
            SttEntry.Focused -= OnMetadataFieldFocused;
            SttEntry.Unfocused -= OnMetadataFieldUnfocused;
            ContainerEntry.Focused -= OnMetadataFieldFocused;
            ContainerEntry.Unfocused -= OnMetadataFieldUnfocused;
            SealEntry.Focused -= OnMetadataFieldFocused;
            SealEntry.Unfocused -= OnMetadataFieldUnfocused;
            DateEntry.Focused -= OnMetadataFieldFocused;
            DateEntry.Unfocused -= OnMetadataFieldUnfocused;
        }
        catch
        {
        }
    }

    private void SetDynamicTableHeight()
    {
        try
        {
            int headerHeight = 32;
            int rowHeight = 35;
            int totalRows = _rows;
            int spacing = totalRows + 1;
            int framePadding = 20;
            int labelHeight = 30;
            int scrollbarBuffer = 15;

            double calculatedHeight = headerHeight + (totalRows * rowHeight) + spacing + framePadding + labelHeight + scrollbarBuffer;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (TableContainerFrame != null)
                {
                    TableContainerFrame.HeightRequest = calculatedHeight;
                    TableContainerFrame.VerticalOptions = LayoutOptions.Start;
                }
            });
        }
        catch
        {
        }
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
                HeaderGrid.HeightRequest = safeInsets.Top + 44;
            }

            if (safeInsets.Bottom > 0)
            {
                FooterGrid.Padding = new Thickness(10, 20, 10, safeInsets.Bottom + 10);
                FooterGrid.HeightRequest = safeInsets.Bottom + 90;
            }
#endif
        }
        catch
        {
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
        catch
        {
            return new Thickness(0);
        }
    }
#endif

    private void SubscribeToFocusEvents()
    {
        SttEntry.Focused += OnMetadataFieldFocused;
        SttEntry.Unfocused += OnMetadataFieldUnfocused;

        ContainerEntry.Focused += OnMetadataFieldFocused;
        ContainerEntry.Unfocused += OnMetadataFieldUnfocused;

        SealEntry.Focused += OnMetadataFieldFocused;
        SealEntry.Unfocused += OnMetadataFieldUnfocused;

        DateEntry.Focused += OnMetadataFieldFocused;
        DateEntry.Unfocused += OnMetadataFieldUnfocused;
    }

    private void OnMetadataFieldFocused(object? sender, FocusEventArgs e)
    {
        _isAnyFieldFocused = true;
    }

    private void OnMetadataFieldUnfocused(object? sender, FocusEventArgs e)
    {
        _isAnyFieldFocused = false;
    }

    private async void OnPhoto1Tapped(object? sender, EventArgs e) => await CapturePhotoAsync(1);
    private async void OnPhoto2Tapped(object? sender, EventArgs e) => await CapturePhotoAsync(2);
    private async void OnPhoto3Tapped(object? sender, EventArgs e) => await CapturePhotoAsync(3);
    private async void OnPhoto4Tapped(object? sender, EventArgs e) => await CapturePhotoAsync(4);

    // Picker event handlers
    private void OnCustomerPickerChanged(object sender, EventArgs e)
    {
        if (sender is Picker picker && picker.SelectedItem is string selected)
        {
            _selectedCustomer = selected;

            // Reset và load lại Product picker
            _selectedProduct = null;
            _selectedModel = null;
            // ProductPicker.ItemsSource = ProductDataService.Instance.GetProducts(selected); // Removed - using Label instead
            // ProductPicker.SelectedItem = null;
            // ModelPicker.ItemsSource = null;
            // ModelPicker.SelectedItem = null;
        }
    }

    private void OnProductPickerChanged(object sender, EventArgs e)
    {
        if (sender is Picker picker && picker.SelectedItem is string selected)
        {
            _selectedProduct = selected;

            // Reset và load lại Model picker
            _selectedModel = null;
            if (!string.IsNullOrEmpty(_selectedCustomer))
            {
                // ModelPicker.ItemsSource = ProductDataService.Instance.GetModels(_selectedCustomer, selected); // Removed - using Label instead
            }
            // ModelPicker.SelectedItem = null; // Removed - using Label instead
        }
    }

    private void OnModelPickerChanged(object sender, EventArgs e)
    {
        if (sender is Picker picker && picker.SelectedItem is string selected)
        {
            _selectedModel = selected;
        }
    }

    private void OnCreatorPickerChanged(object sender, EventArgs e)
    {
        if (sender is Picker picker && picker.SelectedItem is UserData selected)
        {
            _selectedUser = selected;
        }
    }

    private async Task LoadPickerData()
    {
        try
        {
            // Load Customer data
            if (!ProductDataService.Instance.IsDataLoaded)
            {
                await ProductDataService.Instance.LoadDataAsync();
            }
            // CustomerPicker.ItemsSource = ProductDataService.Instance.GetCustomers(); // Removed - using Label instead

            // Load Creator data
            if (!UserService.Instance.IsLoaded)
            {
                await UserService.Instance.LoadUsersAsync();
            }
            // CreatorPicker.ItemsSource = UserService.Instance.GetAllUsers(); // Removed - using Label instead
            // CreatorPicker.ItemDisplayBinding = new Binding("Name");
        }
        catch (Exception)
        {
            await DisplayAlert("Lỗi", "Không thể tải dữ liệu", "OK");
        }
    }

    private async Task CapturePhotoAsync(int photoIndex)
    {
        try
        {
            // Kiểm tra quyền camera trước khi chụp
            var cameraStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (cameraStatus != PermissionStatus.Granted)
            {
                cameraStatus = await Permissions.RequestAsync<Permissions.Camera>();
                if (cameraStatus != PermissionStatus.Granted)
                {
                    await DisplayAlert("Quyền camera", "Ứng dụng cần quyền camera để chụp ảnh. Vui lòng cấp quyền trong Cài đặt.", "OK");
                    return;
                }
            }

            // Kiểm tra quyền storage cho Android 11+
            var storageStatus = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
            if (storageStatus != PermissionStatus.Granted)
            {
                storageStatus = await Permissions.RequestAsync<Permissions.StorageWrite>();
            }

            if (MediaPicker.Default.IsCaptureSupported)
            {
                var mediaPickerOptions = new MediaPickerOptions
                {
                    Title = $"Chụp ảnh {photoIndex}"
                };

                var photo = await MediaPicker.Default.CapturePhotoAsync(mediaPickerOptions);
                if (photo != null)
                {
                    // Sử dụng AppDataDirectory thay vì external storage để tránh vấn đề scoped storage
                    var folder = FileSystem.AppDataDirectory;
                    var fileName = $"photo_{photoIndex}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                    var targetPath = IOPath.Combine(folder, fileName);

                    // Đảm bảo thư mục tồn tại
                    Directory.CreateDirectory(folder);

                    using var sourceStream = await photo.OpenReadAsync();
                    using var targetStream = File.Create(targetPath);
                    await sourceStream.CopyToAsync(targetStream);

                    // Cập nhật UI
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        switch (photoIndex)
                        {
                            case 1:
                                _photo1Path = targetPath;
                                Photo1Image.IsVisible = true;
                                Photo1Placeholder.IsVisible = false;
                                Photo1Image.Source = ImageSource.FromFile(targetPath);
                                break;
                            case 2:
                                _photo2Path = targetPath;
                                Photo2Image.IsVisible = true;
                                Photo2Placeholder.IsVisible = false;
                                Photo2Image.Source = ImageSource.FromFile(targetPath);
                                break;
                            case 3:
                                _photo3Path = targetPath;
                                Photo3Image.IsVisible = true;
                                Photo3Placeholder.IsVisible = false;
                                Photo3Image.Source = ImageSource.FromFile(targetPath);
                                break;
                            case 4:
                                _photo4Path = targetPath;
                                Photo4Image.IsVisible = true;
                                Photo4Placeholder.IsVisible = false;
                                Photo4Image.Source = ImageSource.FromFile(targetPath);
                                break;
                        }
                    });

                    System.Diagnostics.Debug.WriteLine($"📸 Đã chụp ảnh {photoIndex}: {targetPath}");
                }
            }
            else
            {
                await DisplayAlert("Lỗi", "Camera không được hỗ trợ trên thiết bị này.", "OK");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Lỗi chụp ảnh {photoIndex}: {ex.Message}");
            await DisplayAlert("Lỗi", $"Không thể chụp ảnh: {ex.Message}", "OK");
        }
    }

    private async Task DeletePhotoAsync(int photoIndex)
    {
        bool confirm = await DisplayAlert("Xóa ảnh", "Bạn có chắc muốn xóa ảnh này?", "Xóa", "Hủy");
        if (!confirm) return;

        try
        {
            switch (photoIndex)
            {
                case 1:
                    if (!string.IsNullOrEmpty(_photo1Path) && File.Exists(_photo1Path))
                        File.Delete(_photo1Path);
                    _photo1Path = null;
                    Photo1Image.IsVisible = false;
                    Photo1Placeholder.IsVisible = true;
                    Photo1Image.Source = null;
                    break;
                case 2:
                    if (!string.IsNullOrEmpty(_photo2Path) && File.Exists(_photo2Path))
                        File.Delete(_photo2Path);
                    _photo2Path = null;
                    Photo2Image.IsVisible = false;
                    Photo2Placeholder.IsVisible = true;
                    Photo2Image.Source = null;
                    break;
                case 3:
                    if (!string.IsNullOrEmpty(_photo3Path) && File.Exists(_photo3Path))
                        File.Delete(_photo3Path);
                    _photo3Path = null;
                    Photo3Image.IsVisible = false;
                    Photo3Placeholder.IsVisible = true;
                    Photo3Image.Source = null;
                    break;
                case 4:
                    if (!string.IsNullOrEmpty(_photo4Path) && File.Exists(_photo4Path))
                        File.Delete(_photo4Path);
                    _photo4Path = null;
                    Photo4Image.IsVisible = false;
                    Photo4Placeholder.IsVisible = true;
                    Photo4Image.Source = null;
                    break;
            }
        }
        catch
        {
            await DisplayAlert("Lỗi", "Không thể xóa ảnh.", "OK");
        }
    }

    public void SetCellValue(int row, int col, string value)
    {
        if (row >= 0 && row < _rows && col >= 0 && col < _cols)
        {
            _cellValues[row, col] = value;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_cellLabelMap.TryGetValue((row, col), out var label))
                {
                    label.Text = value;
                }
            });
        }
    }

    public void LoadData(string[,] data)
    {
        if (data == null) return;

        int dataRows = data.GetLength(0);
        int dataCols = data.GetLength(1);

        for (int r = 0; r < Math.Min(dataRows, _rows); r++)
        {
            for (int c = 0; c < Math.Min(dataCols, _cols); c++)
            {
                SetCellValue(r, c, data[r, c] ?? "");
            }
        }

        _originalCellValues = new string[_rows, _cols];
        for (int r = 0; r < _rows; r++)
        {
            for (int c = 0; c < _cols; c++)
            {
                _originalCellValues[r, c] = _cellValues[r, c] ?? "";
            }
        }
    }

    public void LoadMetadata(DateTime date, string stt, string container, string seal)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                DateEntry.Date = date;
                SttEntry.Text = stt;
                ContainerEntry.Text = container;
                SealEntry.Text = seal;
            }
            catch
            {
            }
        });
    }

    public async Task LoadProductMetadata(string customer, string product, string model, string msnv, string creatorName = "")
    {
        try
        {






            if (!ProductDataService.Instance.IsDataLoaded)
            {
                await ProductDataService.Instance.LoadDataAsync();

            }
            if (!UserService.Instance.IsLoaded)
            {
                await UserService.Instance.LoadUsersAsync();

            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    if (!string.IsNullOrEmpty(customer))
                    {
                        _selectedCustomer = customer;
                        CustomerLabel.Text = customer;
                        CustomerLabel.TextColor = Color.FromArgb("#212121");

                    }

                    if (!string.IsNullOrEmpty(product))
                    {
                        _selectedProduct = product;
                        ProductLabel.Text = product;
                        ProductLabel.TextColor = Color.FromArgb("#212121");

                    }

                    if (!string.IsNullOrEmpty(model))
                    {
                        _selectedModel = model;
                        ModelLabel.Text = model;
                        ModelLabel.TextColor = Color.FromArgb("#212121");

                    }

                    if (!string.IsNullOrEmpty(msnv))
                    {
                        var user = UserService.Instance.GetAllUsers().FirstOrDefault(u => u.Msnv == msnv);
                        if (user != null)
                        {
                            _selectedUser = user;
                            var displayName = $"{user.Name} - {user.Msnv}";
                            CreatorLabel.Text = displayName;
                            CreatorLabel.TextColor = Color.FromArgb("#212121");

                        }
                        else
                        {

                            // Nếu không tìm thấy user theo MSNV, thì tìm theo tên
                            if (!string.IsNullOrEmpty(creatorName))
                            {
                                var userByName = UserService.Instance.GetAllUsers().FirstOrDefault(u =>
                                    u.Name.Equals(creatorName, StringComparison.OrdinalIgnoreCase));
                                if (userByName != null)
                                {
                                    _selectedUser = userByName;
                                    var displayName = $"{userByName.Name} - {userByName.Msnv}";
                                    CreatorLabel.Text = displayName;
                                    CreatorLabel.TextColor = Color.FromArgb("#212121");

                                }
                                else
                                {
                                    // Nếu vẫn không tìm thấy, tạo user tạm với tên gốc
                                    _selectedUser = new UserData { Name = creatorName, Position = "", Msnv = "" };
                                    CreatorLabel.Text = creatorName; // Chỉ hiển thị tên
                                    CreatorLabel.TextColor = Color.FromArgb("#212121");

                                }
                            }
                        }
                    }
                    else if (!string.IsNullOrEmpty(creatorName))
                    {
                        // Nếu không có MSNV nhưng có tên, tạo user tạm và hiển thị tên
                        _selectedUser = new UserData { Name = creatorName, Position = "", Msnv = "" };
                        CreatorLabel.Text = creatorName; // Chỉ hiển thị tên
                        CreatorLabel.TextColor = Color.FromArgb("#212121");

                    }


                }
                catch
                {

                }
            });
        }
        catch
        {

        }
    }

    public async Task LoadPhotosFromExcel(string excelFilePath)
    {
        try
        {



            if (!File.Exists(excelFilePath))
            {

                return;
            }

            using var package = new ExcelPackage(new FileInfo(excelFilePath));
            var checkSheet = package.Workbook.Worksheets["Phiếu kiểm tra"];

            if (checkSheet?.Drawings == null)
            {

                return;
            }

            // Tạo thư mục temp để lưu ảnh
            var tempFolder = IOPath.Combine(FileSystem.AppDataDirectory, "temp_photos");
            if (!Directory.Exists(tempFolder))
                Directory.CreateDirectory(tempFolder);

            int photoCount = 0;
            foreach (var drawing in checkSheet.Drawings)
            {
                if (drawing is OfficeOpenXml.Drawing.ExcelPicture picture && photoCount < 4)
                {
                    try
                    {
                        // Chỉ lấy ảnh trong vùng A35:F75 (vùng ảnh thực tế, bỏ qua logo)
                        var fromRow = picture.From.Row;
                        var fromCol = picture.From.Column;

                        // Kiểm tra ảnh có nằm trong vùng A35:F75 không
                        // Row 35 = index 34, Row 75 = index 74
                        // Column A = 0, Column F = 5
                        if (fromRow < 34 || fromRow > 74 || fromCol < 0 || fromCol > 5)
                        {
                            continue; // Bỏ qua ảnh ngoài vùng (như logo)
                        }

                        var imageBytes = picture.Image.ImageBytes;
                        if (imageBytes != null && imageBytes.Length > 0)
                        {
                            photoCount++;
                            var fileName = $"loaded_photo_{photoCount}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                            var filePath = IOPath.Combine(tempFolder, fileName);

                            await File.WriteAllBytesAsync(filePath, imageBytes);

                            await MainThread.InvokeOnMainThreadAsync(() =>
                            {
                                switch (photoCount)
                                {
                                    case 1:
                                        _photo1Path = filePath;
                                        Photo1Image.IsVisible = true;
                                        Photo1Placeholder.IsVisible = false;
                                        Photo1Image.Source = ImageSource.FromFile(filePath);
                                        break;
                                    case 2:
                                        _photo2Path = filePath;
                                        Photo2Image.IsVisible = true;
                                        Photo2Placeholder.IsVisible = false;
                                        Photo2Image.Source = ImageSource.FromFile(filePath);
                                        break;
                                    case 3:
                                        _photo3Path = filePath;
                                        Photo3Image.IsVisible = true;
                                        Photo3Placeholder.IsVisible = false;
                                        Photo3Image.Source = ImageSource.FromFile(filePath);
                                        break;
                                    case 4:
                                        _photo4Path = filePath;
                                        Photo4Image.IsVisible = true;
                                        Photo4Placeholder.IsVisible = false;
                                        Photo4Image.Source = ImageSource.FromFile(filePath);
                                        break;
                                }
                            });


                        }
                    }
                    catch (Exception)
                    {

                    }
                }
            }


        }
        catch (Exception)
        {

        }
    }

    private void FillSeriesToCheckSheet(ExcelWorksheet checkSheet)
    {
        try
        {
            int totalDataCells = _rows * _cols;
            int seriesIndex = 0;

            // 1) Đổ vào cột H: H13 đến H75 (bao gồm cả Page 1 và Page 2)
            for (int row = 13; row <= 75 && seriesIndex < totalDataCells; row++)
            {
                string seriesValue = GetSeriesValueByIndex(seriesIndex);

                if (!string.IsNullOrWhiteSpace(seriesValue))
                {
                    var cell = checkSheet.Cells[$"H{row}"];
                    cell.Value = seriesValue;
                    cell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    cell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;

                    var okCell = checkSheet.Cells[$"I{row}"];
                    okCell.Value = "OK";
                    okCell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    okCell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                }

                seriesIndex++;
            }

            // 2) Đổ tiếp vào cột L: L13 đến L75 (bao gồm cả Page 1 và Page 2)
            for (int row = 13; row <= 75 && seriesIndex < totalDataCells; row++)
            {
                string seriesValue = GetSeriesValueByIndex(seriesIndex);

                if (!string.IsNullOrWhiteSpace(seriesValue))
                {
                    var cell = checkSheet.Cells[$"L{row}"];
                    cell.Value = seriesValue;
                    cell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    cell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;

                    var okCell = checkSheet.Cells[$"M{row}"];
                    okCell.Value = "OK";
                    okCell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    okCell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                }

                seriesIndex++;
            }
        }
        catch (Exception)
        {
            // Xử lý lỗi im lặng
        }
    }

    private string GetSeriesValueByIndex(int index)
    {
        try
        {
            int col = index / _rows;
            int row = index % _rows;
            int actualCol = _cols - col - 1;

            if (row >= 0 && row < _rows && actualCol >= 0 && actualCol < _cols)
            {
                return _cellValues[row, actualCol] ?? string.Empty;
            }
        }
        catch
        {

        }
        return string.Empty;
    }

    private void BuildGrid()
    {
        DataGrid.RowDefinitions.Clear();
        DataGrid.ColumnDefinitions.Clear();
        DataGrid.Children.Clear();
        _cellLabelMap.Clear();
        _cellBorderMap.Clear();

        DataGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(32)));
        for (int c = 1; c <= _cols; c++)
            DataGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(120)));

        DataGrid.RowDefinitions.Add(new RowDefinition(new GridLength(32)));
        for (int r = 1; r <= _rows; r++)
            DataGrid.RowDefinitions.Add(new RowDefinition(new GridLength(35)));

        BuildFrozenColumnHeaders();
        BuildFrozenRowHeaders();

        for (int c = 1; c <= _cols; c++)
        {
            int colNum = _cols - c + 1;
            var lbl = new Label
            {
                Text = $"Cột {colNum}",
                FontAttributes = FontAttributes.Bold,
                BackgroundColor = Color.FromArgb("#F5F5F5"),
                TextColor = Color.FromArgb("#212121"),
                HorizontalTextAlignment = MauiTextAlignment.Center,
                VerticalTextAlignment = MauiTextAlignment.Center,
                Margin = new Thickness(0.3),
                HeightRequest = 32,
                WidthRequest = 120,
                FontSize = 11
            };

            DataGrid.Children.Add(lbl);
            Grid.SetRow(lbl, 0);
            Grid.SetColumn(lbl, c);
        }

        for (int r = 1; r <= _rows; r++)
        {
            int stt = r;
            var lbl = new Label
            {
                Text = stt.ToString(),
                FontAttributes = FontAttributes.Bold,
                BackgroundColor = Color.FromArgb("#F5F5F5"),
                TextColor = Color.FromArgb("#212121"),
                HorizontalTextAlignment = MauiTextAlignment.Center,
                VerticalTextAlignment = MauiTextAlignment.Center,
                Margin = new Thickness(0.3),
                HeightRequest = 35,
                WidthRequest = 32,
                FontSize = 11
            };

            DataGrid.Children.Add(lbl);
            Grid.SetRow(lbl, r);
            Grid.SetColumn(lbl, 0);
        }

        for (int r = 1; r <= _rows; r++)
        {
            for (int c = 1; c <= _cols; c++)
            {
                int rr = r - 1;
                int cc = c - 1;

                var cellLabel = new Label
                {
                    Text = "",
                    BackgroundColor = Colors.White,
                    FontSize = 12,
                    HeightRequest = 35,
                    WidthRequest = 120,
                    Margin = 0,
                    HorizontalTextAlignment = MauiTextAlignment.Center,
                    VerticalTextAlignment = MauiTextAlignment.Center,
                    TextColor = Color.FromArgb("#212121")
                };

                var tap = new TapGestureRecognizer();
                tap.Tapped += async (_, __) => await StartScanForCell(cellLabel, rr, cc);
                cellLabel.GestureRecognizers.Add(tap);

                var border = new Border
                {
                    Content = cellLabel,
                    Stroke = Color.FromArgb("#E0E0E0"),
                    StrokeThickness = 1,
                    Padding = 0,
                    Margin = new Thickness(0.3),
                    BackgroundColor = Colors.White
                };
                border.StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(2) };
                border.GestureRecognizers.Add(tap);

                _cellLabelMap[(rr, cc)] = cellLabel;
                _cellBorderMap[(rr, cc)] = border;

                DataGrid.Children.Add(border);
                Grid.SetRow(border, r);
                Grid.SetColumn(border, c);
            }
        }

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(50);
            SetDynamicTableHeight();
        });
    }

    private void BuildFrozenColumnHeaders()
    {
        FrozenColumnHeader.ColumnDefinitions.Clear();
        FrozenColumnHeader.Children.Clear();

        for (int c = 1; c <= _cols; c++)
        {
            FrozenColumnHeader.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(120)));

            int colNum = _cols - c + 1;
            var lbl = new Label
            {
                Text = $"Cột {colNum}",
                FontAttributes = FontAttributes.Bold,
                BackgroundColor = Color.FromArgb("#F5F5F5"),
                TextColor = Color.FromArgb("#212121"),
                HorizontalTextAlignment = MauiTextAlignment.Center,
                VerticalTextAlignment = MauiTextAlignment.Center,
                Margin = new Thickness(0.3),
                HeightRequest = 32,
                FontSize = 11
            };

            FrozenColumnHeader.Children.Add(lbl);
            Grid.SetColumn(lbl, c - 1);
        }
    }

    private void BuildFrozenRowHeaders()
    {
        FrozenRowHeader.RowDefinitions.Clear();
        FrozenRowHeader.Children.Clear();

        for (int r = 1; r <= _rows; r++)
        {
            FrozenRowHeader.RowDefinitions.Add(new RowDefinition(new GridLength(35)));

            int stt = r;
            var lbl = new Label
            {
                Text = stt.ToString(),
                FontAttributes = FontAttributes.Bold,
                BackgroundColor = Color.FromArgb("#F5F5F5"),
                TextColor = Color.FromArgb("#212121"),
                HorizontalTextAlignment = MauiTextAlignment.Center,
                VerticalTextAlignment = MauiTextAlignment.Center,
                Margin = new Thickness(0.3),
                WidthRequest = 32,
                FontSize = 11
            };

            FrozenRowHeader.Children.Add(lbl);
            Grid.SetRow(lbl, r - 1);
        }
    }

    private void OnScrollViewScrolled(object sender, ScrolledEventArgs e)
    {
        if (sender is ScrollView scrollView)
        {
            FrozenColumnHeader.TranslationX = -e.ScrollX;
            FrozenRowHeader.TranslationY = -e.ScrollY;
        }
    }

    private void UpdateDuplicateHighlighting()
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var valueFrequency = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int r = 0; r < _rows; r++)
                {
                    for (int c = 0; c < _cols; c++)
                    {
                        var value = _cellValues[r, c];
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            var key = value.Trim();
                            valueFrequency[key] = valueFrequency.GetValueOrDefault(key, 0) + 1;
                        }
                    }
                }

                for (int r = 0; r < _rows; r++)
                {
                    for (int c = 0; c < _cols; c++)
                    {
                        var value = _cellValues[r, c];
                        if (_cellLabelMap.TryGetValue((r, c), out var label))
                        {
                            if (label == _highlightLabel)
                                continue;

                            if (!string.IsNullOrWhiteSpace(value))
                            {
                                var key = value.Trim();
                                if (valueFrequency.GetValueOrDefault(key, 0) > 1)
                                {
                                    label.BackgroundColor = DuplicateColor;
                                }
                                else
                                {
                                    label.BackgroundColor = NormalColor;
                                }
                            }
                            else
                            {
                                label.BackgroundColor = NormalColor;
                            }
                        }
                    }
                }
            });
        }
        catch
        {
        }
    }

    private async Task StartScanForCell(Label cellLabel, int rr, int cc)
    {
        if (_isProcessing) return;

        _isProcessing = true;
        SetUIEnabled(false);

        try
        {
            HighlightCell(cellLabel);

            string? entered = await ScanBarcodeAsync();

            if (string.IsNullOrWhiteSpace(entered))
            {
                RestoreCellColor(cellLabel, rr, cc);
                _highlightLabel = null;
                return;
            }

            _cellValues[rr, cc] = entered.Trim();
            cellLabel.Text = _cellValues[rr, cc];

            PlayBeep();

            _highlightLabel = null;

            UpdateDuplicateHighlighting();
        }
        catch
        {
            RestoreCellColor(cellLabel, rr, cc);
            _highlightLabel = null;
        }
        finally
        {
            _isProcessing = false;
            SetUIEnabled(true);
        }
    }

    private async Task<string?> ScanBarcodeAsync()
    {
        try
        {
            var tcs = new TaskCompletionSource<string?>();
            var scanPage = new CellScanPage(tcs);

            await Navigation.PushModalAsync(scanPage, animated: true);

            var result = await tcs.Task;

            return result;
        }
        catch
        {
            return null;
        }
    }

    private void HighlightCell(Label label)
    {
        if (_highlightLabel != null)
        {
            RestorePreviousHighlightedCell();
        }

        label.BackgroundColor = HighlightColor;
        _highlightLabel = label;
    }

    private void RestorePreviousHighlightedCell()
    {
        if (_highlightLabel == null) return;

        foreach (var kvp in _cellLabelMap)
        {
            if (kvp.Value == _highlightLabel)
            {
                RestoreCellColor(_highlightLabel, kvp.Key.r, kvp.Key.c);
                break;
            }
        }
    }

    private void RestoreCellColor(Label label, int row, int col)
    {
        var value = _cellValues[row, col];

        if (string.IsNullOrWhiteSpace(value))
        {
            label.BackgroundColor = NormalColor;
            return;
        }

        var trimmedValue = value.Trim();
        int count = 0;
        for (int r = 0; r < _rows; r++)
        {
            for (int c = 0; c < _cols; c++)
            {
                var cellValue = _cellValues[r, c];
                if (!string.IsNullOrWhiteSpace(cellValue) &&
                    string.Equals(cellValue.Trim(), trimmedValue, StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                    if (count > 1) break;
                }
            }
            if (count > 1) break;
        }

        label.BackgroundColor = count > 1 ? DuplicateColor : NormalColor;
    }

    private void SetUIEnabled(bool enabled)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SttEntry.IsEnabled = enabled;
            ContainerEntry.IsEnabled = enabled;
            DateEntry.IsEnabled = enabled;
            SealEntry.IsEnabled = enabled;
            DataGrid.IsEnabled = enabled;
        });
    }

    private void PlayBeep()
    {
#if ANDROID
        try
        {
            var ctx = AApp.Context;
            var uri = RingtoneManager.GetDefaultUri(RingtoneType.Notification);
            var ring = RingtoneManager.GetRingtone(ctx, uri);
            ring?.Play();
        }
        catch
        {
        }
#endif
    }

    private async void OnContainerOcrClicked(object sender, EventArgs e)
    {
        if (_isProcessing) return;

        _isProcessing = true;
        SetUIEnabled(false);

        try
        {
            var services = Application.Current?.Handler?.MauiContext?.Services;
            var ocr = services?.GetService<IOcrService>();
            if (ocr == null)
            {
                await DisplayAlert("OCR", "OCR chưa sẵn sàng.", "Đóng");
                return;
            }

            string? result = await ocr.ScanTextAsync(OcrMode.Container);

            if (!string.IsNullOrWhiteSpace(result))
            {
                ContainerEntry.Text = result.ToUpperInvariant().Trim();
            }
            else
            {
                await DisplayAlert(
                    "Không quét được Container",
                    "Không thể nhận diện số Container từ ảnh.\n\n" +
                    "Hãy thử:\n" +
                    "• Chụp ảnh rõ nét hơn\n" +
                    "• Đảm bảo đủ ánh sáng\n" +
                    "• Chụp thẳng góc (không xiên)\n" +
                    "• Zoom vào vùng có số Container\n" +
                    "• Đảm bảo số Container nằm trong khung xanh\n\n" +
                    "Format Container: 4 chữ cái + 7 số\n" +
                    "Ví dụ: KOCU 411486 2",
                    "OK"
                );
            }
        }
        finally
        {
            _isProcessing = false;
            SetUIEnabled(true);
        }
    }

    private async void OnSealOcrClicked(object sender, EventArgs e)
    {
        if (_isProcessing) return;

        _isProcessing = true;
        SetUIEnabled(false);

        try
        {
            var services = Application.Current?.Handler?.MauiContext?.Services;
            var ocr = services?.GetService<IOcrService>();
            if (ocr == null)
            {
                await DisplayAlert("OCR", "OCR chưa sẵn sàng.", "Đóng");
                return;
            }

            string? result = await ocr.ScanTextAsync(OcrMode.Seal);

            if (!string.IsNullOrWhiteSpace(result))
            {
                SealEntry.Text = result.ToUpperInvariant().Trim();
            }
            else
            {
                await DisplayAlert(
                    "Không quét được Seal",
                    "Không thể nhận diện số Seal từ ảnh.\n\n" +
                    "Hãy thử:\n" +
                    "• Chụp ảnh rõ nét hơn\n" +
                    "• Đảm bảo đủ ánh sáng\n" +
                    "• Chụp thẳng góc (không xiên)\n" +
                    "• Zoom vào vùng có số Seal\n" +
                    "• Đảm bảo số Seal nằm trong khung xanh\n\n" +
                    "Format Seal: 6-15 ký tự (chữ + số)\n" +
                    "Ví dụ: YN646E4AO",
                    "OK"
                );
            }
        }
        finally
        {
            _isProcessing = false;
            SetUIEnabled(true);
        }
    }

    private async void OnCustomerPickerTapped(object sender, EventArgs e)
    {
        if (_isProcessing) return;

        try
        {
            if (!ProductDataService.Instance.IsDataLoaded)
            {
                await ProductDataService.Instance.LoadDataAsync();
            }

            var customers = ProductDataService.Instance.GetCustomers();

            if (customers.Count == 0)
            {
                await DisplayAlert("Thông báo", "Chưa có dữ liệu khách hàng.\n\nKiểm tra:\n1. Kết nối internet\n2. Dữ liệu đã upload lên Firebase\n3. Firebase config đúng", "OK");
                return;
            }

            var tcs = new TaskCompletionSource<string?>();
            var pickerPage = new SearchablePickerPage("Chọn khách hàng", customers, tcs);

            await Navigation.PushModalAsync(pickerPage);

            var selected = await tcs.Task;

            if (!string.IsNullOrEmpty(selected))
            {
                if (_selectedCustomer != selected)
                {
                    _selectedCustomer = selected;
                    CustomerLabel.Text = selected;
                    CustomerLabel.TextColor = Color.FromArgb("#212121");
                }
            }
        }
        catch
        {
            await DisplayAlert("Lỗi", "Không thể chọn khách hàng.", "OK");
        }
    }

    private async void OnProductPickerTapped(object sender, EventArgs e)
    {
        if (_isProcessing) return;

        if (string.IsNullOrEmpty(_selectedCustomer))
        {
            await DisplayAlert("Thông báo", "Vui lòng chọn khách hàng trước", "OK");
            return;
        }

        try
        {
            var products = ProductDataService.Instance.GetProducts(_selectedCustomer);

            if (products.Count == 0)
            {
                await DisplayAlert("Thông báo", $"Không có sản phẩm cho khách hàng '{_selectedCustomer}'", "OK");
                return;
            }

            var tcs = new TaskCompletionSource<string?>();
            var pickerPage = new SearchablePickerPage("Chọn sản phẩm", products, tcs);

            await Navigation.PushModalAsync(pickerPage);

            var selected = await tcs.Task;

            if (!string.IsNullOrEmpty(selected))
            {
                if (_selectedProduct != selected)
                {
                    _selectedProduct = selected;
                    ProductLabel.Text = selected;
                    ProductLabel.TextColor = Color.FromArgb("#212121");
                }
            }
        }
        catch
        {
            await DisplayAlert("Lỗi", "Không thể chọn sản phẩm.", "OK");
        }
    }

    private async void OnModelPickerTapped(object sender, EventArgs e)
    {
        if (_isProcessing) return;

        if (string.IsNullOrEmpty(_selectedCustomer))
        {
            await DisplayAlert("Thông báo", "Vui lòng chọn khách hàng trước", "OK");
            return;
        }

        if (string.IsNullOrEmpty(_selectedProduct))
        {
            await DisplayAlert("Thông báo", "Vui lòng chọn sản phẩm trước", "OK");
            return;
        }

        try
        {
            var models = ProductDataService.Instance.GetModels(_selectedCustomer, _selectedProduct);

            if (models.Count == 0)
            {
                await DisplayAlert("Thông báo", $"Không có model cho sản phẩm '{_selectedProduct}' của khách hàng '{_selectedCustomer}'", "OK");
                return;
            }

            var tcs = new TaskCompletionSource<string?>();
            var pickerPage = new SearchablePickerPage("Chọn model", models, tcs);

            await Navigation.PushModalAsync(pickerPage);

            var selected = await tcs.Task;

            if (!string.IsNullOrEmpty(selected))
            {
                if (_selectedModel != selected)
                {
                    _selectedModel = selected;
                    ModelLabel.Text = selected;
                    ModelLabel.TextColor = Color.FromArgb("#212121");
                }
            }
        }
        catch
        {
            await DisplayAlert("Lỗi", "Không thể chọn model.", "OK");
        }
    }

    private async void OnCreatorPickerTapped(object sender, EventArgs e)
    {
        if (_isProcessing) return;

        try
        {
            if (!UserService.Instance.IsLoaded)
            {
                await UserService.Instance.LoadUsersAsync();
            }

            var userDisplayNames = UserService.Instance.GetUserDisplayNames();

            if (userDisplayNames.Count == 0)
            {
                await DisplayAlert("Thông báo", "Chưa có dữ liệu người lập.\n\nKiểm tra:\n1. Kết nối internet\n2. Dữ liệu đã upload lên Firebase\n3. Firebase config đúng", "OK");
                return;
            }

            var tcs = new TaskCompletionSource<string?>();
            var pickerPage = new SearchablePickerPage("Chọn người lập", userDisplayNames, tcs);

            await Navigation.PushModalAsync(pickerPage);

            var selected = await tcs.Task;

            if (!string.IsNullOrEmpty(selected))
            {
                var newUser = UserService.Instance.GetUserByDisplayName(selected);
                if (newUser != null && (_selectedUser == null || _selectedUser.Msnv != newUser.Msnv))
                {
                    _selectedUser = newUser;
                    var displayName = $"{newUser.Name} - {newUser.Msnv}";
                    CreatorLabel.Text = displayName;
                    CreatorLabel.TextColor = Color.FromArgb("#212121");
                }
            }
        }
        catch
        {
            await DisplayAlert("Lỗi", "Không thể chọn người lập.", "OK");
        }
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var ch in IOPath.GetInvalidFileNameChars())
            name = name.Replace(ch, '_');
        return string.IsNullOrWhiteSpace(name) ? "export" : name;
    }

    private async void OnExportClicked(object sender, EventArgs e)
    {
        if (_isProcessing) return;

        _isProcessing = true;
        SetUIEnabled(false);
        ShowLoading("Đang xuất file Excel...");

        try
        {
            if (string.IsNullOrEmpty(_selectedCustomer))
            {
                await DisplayAlert("Thiếu thông tin", "Vui lòng chọn tên khách hàng", "OK");
                return;
            }

            if (string.IsNullOrEmpty(_selectedProduct))
            {
                await DisplayAlert("Thiếu thông tin", "Vui lòng chọn tên sản phẩm", "OK");
                return;
            }

            if (string.IsNullOrEmpty(_selectedModel))
            {
                await DisplayAlert("Thiếu thông tin", "Vui lòng chọn model", "OK");
                return;
            }

            if (_selectedUser == null)
            {
                await DisplayAlert("Thiếu thông tin", "Vui lòng chọn người lập", "OK");
                return;
            }

            bool isEditing = !string.IsNullOrEmpty(_existingFilePath);

            var dt = DateEntry.Date;
            var datePart = dt.ToString("yyyy.MM.dd");
            var sttPart = (SttEntry.Text ?? "").Trim();
            var contPart = (ContainerEntry.Text ?? "").Trim();
            var sealPart = (SealEntry.Text ?? "").Trim();
            var customerPart = SanitizeFileName(_selectedCustomer ?? "");
            var productPart = SanitizeFileName(_selectedProduct ?? "");
            var modelPart = SanitizeFileName(_selectedModel ?? "");
            var creatorPart = SanitizeFileName(_selectedUser?.Name ?? "");

            var raw = $"{datePart}_{sttPart}_{contPart}_{sealPart}_{customerPart}_{productPart}_{modelPart}_{creatorPart}";
            var baseFileName = SanitizeFileName(raw);

            string path;
            bool metadataChanged = false;

            if (isEditing)
            {
                var oldFileName = IOPath.GetFileNameWithoutExtension(_existingFilePath!);
                var (oldDate, oldStt, oldContainer, oldSeal) = ParseMetadataFromFilename(oldFileName);

                metadataChanged =
                    dt.ToString("yyyy.MM.dd") != oldDate?.ToString("yyyy.MM.dd") ||
                    sttPart != oldStt ||
                    contPart != oldContainer ||
                    sealPart != oldSeal;

                bool cellsChanged = HasAnyChanges();

                bool productChanged =
                    _selectedCustomer != _originalCustomer ||
                    _selectedProduct != _originalProduct ||
                    _selectedModel != _originalModel ||
                    (_selectedUser?.Msnv ?? "") != (_originalUser?.Msnv ?? "");

                if (!metadataChanged && !cellsChanged && !productChanged)
                {
                    await DisplayAlert("Thông báo", "Không có thay đổi nào để lưu", "OK");
                    _navigationConfirmed = true;
                    await Navigation.PopToRootAsync();
                    return;
                }

                if (metadataChanged || productChanged)
                {
                    path = IOPath.Combine(FileSystem.AppDataDirectory, baseFileName + ".xlsx");

                    if (File.Exists(_existingFilePath!))
                    {
                        File.Delete(_existingFilePath!);
                    }
                }
                else
                {
                    path = _existingFilePath!;
                }
            }
            else
            {
                path = IOPath.Combine(FileSystem.AppDataDirectory, baseFileName + ".xlsx");
            }

            using (var pkg = new ExcelPackage())
            {
                var ws = pkg.Workbook.Worksheets.Add("Bảng dữ liệu");

                for (int c = 0; c < _cols; c++)
                {
                    int colNum = _cols - c;
                    var cell = ws.Cells[1, c + 2];
                    cell.Value = $"Cột {colNum}";
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    cell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    cell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                }

                for (int r = 0; r < _rows; r++)
                {
                    int stt = r + 1;
                    var headerCell = ws.Cells[r + 2, 1];
                    headerCell.Value = stt.ToString();
                    headerCell.Style.Font.Bold = true;
                    headerCell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    headerCell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    headerCell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    headerCell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;

                    for (int c = 0; c < _cols; c++)
                    {
                        var dataCell = ws.Cells[r + 2, c + 2];
                        dataCell.Value = _cellValues[r, c] ?? "";
                        dataCell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                        dataCell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                    }
                }

                ws.Cells[ws.Dimension.Address].AutoFitColumns();

                // Thêm Sheet 2 từ template - chạy trên background thread
                await Task.Run(async () =>
                {
                    using (var templateStream = await FileSystem.Current.OpenAppPackageFileAsync("Template/BM_Phieu kiem tra dong container.xlsx"))
                    using (var templatePkg = new ExcelPackage(templateStream))
                    {
                        var templateSheet = templatePkg.Workbook.Worksheets["Sheet2"];
                        if (templateSheet != null)
                        {
                            var newSheet = pkg.Workbook.Worksheets.Add("Phiếu kiểm tra", templateSheet);

                            // Điền thông tin vào các ô tương ứng
                            var customerCell = newSheet.Cells["D4"];
                            customerCell.Value = _selectedCustomer;  // Tên khách hàng 
                            customerCell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                            customerCell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                            var productCell = newSheet.Cells["D5"];
                            productCell.Value = _selectedProduct;   // Tên Products
                            productCell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                            productCell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;

                            var modelCell = newSheet.Cells["D6"];
                            modelCell.Value = _selectedModel;     // Model
                            modelCell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                            modelCell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;

                            var dateCell = newSheet.Cells["D9"];
                            dateCell.Value = DateEntry.Date.ToString("dd/MM/yyyy"); // Ngày kiểm tra
                            dateCell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                            dateCell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;

                            var sttCell = newSheet.Cells["D8"];
                            sttCell.Value = sttPart;            // Số thứ tự
                            sttCell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                            sttCell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;

                            var contCell = newSheet.Cells["L5"];
                            contCell.Value = contPart;           // Số container
                            contCell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                            contCell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;

                            var sealCell = newSheet.Cells["L7"];
                            sealCell.Value = sealPart;           // Số seal
                            sealCell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                            sealCell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;

                            var userCell = newSheet.Cells["L9"];
                            userCell.Value = _selectedUser?.Name; // Nhân viên kiểm tra
                            userCell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                            userCell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;

                            // Điền số series vào sheet 2 (Phiếu kiểm tra)
                            FillSeriesToCheckSheet(newSheet);

                            // Chèn ảnh thực tế vào vùng A35:F75 (4 ảnh)
                            await InsertPhotosToExcel(newSheet);
                        }
                    }

                    ws.Cells[ws.Dimension.Address].AutoFitColumns();
                });
                var fileInfo = new FileInfo(path);
                await Task.Run(() => pkg.SaveAs(fileInfo));
            }

            UpdateOriginalCellValues();
            _originalCustomer = _selectedCustomer;
            _originalProduct = _selectedProduct;
            _originalModel = _selectedModel;
            _originalUser = _selectedUser;

            _existingFilePath = path;

            var tcs = new TaskCompletionSource<PopupAction>();
            var popup = new ExportSuccessPopup(path, isEditing, tcs);
            await Navigation.PushModalAsync(popup, false);

            var action = await tcs.Task;

            if (action == PopupAction.Share)
            {
                try
                {
                    await Share.Default.RequestAsync(new ShareFileRequest
                    {
                        Title = "Chia sẻ file Excel",
                        File = new ShareFile(path)
                    });
                }
                catch
                {
                    await DisplayAlert("Lỗi", "Không thể chia sẻ file.", "OK");
                }

                _navigationConfirmed = true;
                await Navigation.PopToRootAsync();
            }
            else if (action == PopupAction.Home)
            {
                _navigationConfirmed = true;
                await Navigation.PopToRootAsync();
            }
        }
        catch
        {
            await DisplayAlert("Lỗi xuất Excel", "Lỗi xuất Excel.", "Đóng");
        }
        finally
        {
            HideLoading();
            _isProcessing = false;
            SetUIEnabled(true);
        }
    }

    private string GetUniqueFilePath(string baseFileName)
    {
        var folder = FileSystem.AppDataDirectory;
        var path = IOPath.Combine(folder, baseFileName + ".xlsx");

        if (!File.Exists(path))
            return path;

        int counter = 1;
        while (true)
        {
            var newFileName = $"{baseFileName}_({counter}).xlsx";
            path = IOPath.Combine(folder, newFileName);

            if (!File.Exists(path))
                return path;

            counter++;

            if (counter > 999)
                return IOPath.Combine(folder, $"{baseFileName}_{DateTime.Now.Ticks}.xlsx");
        }
    }

    private bool HasAnyChanges()
    {
        if (_originalCellValues == null)
            return true;

        for (int r = 0; r < _rows; r++)
        {
            for (int c = 0; c < _cols; c++)
            {
                var current = _cellValues[r, c] ?? "";
                var original = _originalCellValues[r, c] ?? "";

                if (current != original)
                    return true;
            }
        }

        return false;
    }

    private bool HasAnyChangesIncludingMetadata()
    {
        if (_isAnyFieldFocused)
        {
            return true;
        }

        if (string.IsNullOrEmpty(_existingFilePath))
        {
            return !string.IsNullOrEmpty(_selectedCustomer) ||
                   !string.IsNullOrEmpty(_selectedProduct) ||
                   !string.IsNullOrEmpty(_selectedModel) ||
                   _selectedUser != null ||
                   HasAnyChanges();
        }

        try
        {
            bool productChanged =
                _selectedCustomer != _originalCustomer ||
                _selectedProduct != _originalProduct ||
                _selectedModel != _originalModel ||
                (_selectedUser?.Msnv ?? "") != (_originalUser?.Msnv ?? "");

            if (productChanged)
                return true;

            var oldFileName = IOPath.GetFileNameWithoutExtension(_existingFilePath);
            var (oldDate, oldStt, oldContainer, oldSeal) = ParseMetadataFromFilename(oldFileName);

            var currentDate = DateEntry.Date.ToString("yyyy.MM.dd");
            var currentStt = (SttEntry.Text ?? "").Trim();
            var currentContainer = (ContainerEntry.Text ?? "").Trim();
            var currentSeal = (SealEntry.Text ?? "").Trim();

            bool metadataChanged =
                currentDate != oldDate?.ToString("yyyy.MM.dd") ||
                currentStt != oldStt ||
                currentContainer != oldContainer ||
                currentSeal != oldSeal;

            if (metadataChanged)
                return true;
        }
        catch
        {
        }

        return HasAnyChanges();
    }

    private void UpdateOriginalCellValues()
    {
        _originalCellValues = new string[_rows, _cols];
        Array.Copy(_cellValues, _originalCellValues, _cellValues.Length);
    }

    private (DateTime? date, string stt, string container, string seal) ParseMetadataFromFilename(string fileNameWithoutExt)
    {
        try
        {
            var cleanName = System.Text.RegularExpressions.Regex.Replace(
                fileNameWithoutExt, @"_\(\d+\)$", "");

            var parts = cleanName.Split('_');

            if (parts.Length >= 4)
            {
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
        catch
        {
        }

        return (null, "", "", "");
    }

    private async Task HandleBackNavigation()
    {
        if (_navigationConfirmed)
        {
            await Navigation.PopAsync();
            return;
        }

        if (HasAnyChangesIncludingMetadata())
        {
            bool confirm = await DisplayAlert(
                "Thoát mà không lưu?",
                "Bạn có thay đổi chưa lưu. Bạn có chắc muốn thoát?",
                "Thoát",
                "Hủy"
            );

            if (!confirm)
                return;

            _navigationConfirmed = true;
        }
        else
        {
            _navigationConfirmed = true;
        }

        if (_navigationConfirmed)
        {
            await Navigation.PopAsync();
        }
    }

    private async void OnBackButtonClicked(object sender, EventArgs e)
    {
        await HandleBackNavigation();
    }

    private async Task LoadProductDataAsync()
    {
        try
        {
            await ProductDataService.Instance.LoadDataAsync();
            await UserService.Instance.LoadUsersAsync();
            
            // Kiểm tra quyền camera khi load app
            await CheckAndRequestPermissions();
        }
        catch
        {
        }
    }

    private async Task CheckAndRequestPermissions()
    {
        try
        {
            // Kiểm tra quyền camera
            var cameraStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (cameraStatus != PermissionStatus.Granted)
            {
                System.Diagnostics.Debug.WriteLine(" Yêu cầu quyền camera...");
            }

            // Kiểm tra quyền storage (cho Android 11+)
            var storageStatus = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
            if (storageStatus != PermissionStatus.Granted)
            {
                System.Diagnostics.Debug.WriteLine(" Yêu cầu quyền storage...");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($" Lỗi kiểm tra quyền: {ex.Message}");
        }
    }

    private async Task InsertPhotosToExcel(ExcelWorksheet worksheet)
    {
        try
        {
            // Layout 2x2: 4 ảnh trong Excel
            var photos = new[]
            {
                _photo1Path,
                _photo2Path,
                _photo3Path,
                _photo4Path
            };

            var positions = new[]
            {
                new { StartCol = 0, StartRow = 34 }, // A35 - ảnh 1 (0-based: col=0, row=34)
                new { StartCol = 3, StartRow = 34 }, // D35 - ảnh 2 
                new { StartCol = 0, StartRow = 54 }, // A55 - ảnh 3 (0-based: row=54)
                new { StartCol = 3, StartRow = 54 }  // D55 - ảnh 4
            };

            int photoCount = 0;

            // Chèn tất cả ảnh có sẵn
            for (int i = 0; i < photos.Length; i++)
            {
                if (!string.IsNullOrEmpty(photos[i]) && File.Exists(photos[i]))
                {
                    try
                    {
                        var pos = positions[i];

                        // Xử lý ảnh để sửa orientation trước khi chèn
                        byte[] correctedImageBytes = await CorrectImageOrientation(photos[i]!);

                        // Tạo ảnh từ byte array - KHÔNG dùng using để tránh dispose sớm
                        var imageStream = new MemoryStream(correctedImageBytes);

                        // Thêm ảnh vào worksheet với tên unique
                        var pictureName = $"Photo_{i + 1}_{DateTime.Now.Ticks}";
                        var picture = worksheet.Drawings.AddPicture(pictureName, imageStream);

                        // Đặt vị trí ảnh
                        picture.From.Column = pos.StartCol;
                        picture.From.Row = pos.StartRow;

                        // Kích thước cố định
                        int maxWidth = 320;  // Giảm một chút để tránh overlap
                        int maxHeight = (int)(maxWidth * 16.0 / 9.0);

                        picture.SetSize(maxWidth, maxHeight);

                        // Đặt chế độ không resize khi thay đổi cell
                        picture.EditAs = OfficeOpenXml.Drawing.eEditAs.Absolute;

                        photoCount++;
                    }
                    catch (Exception)
                    {
                        // Xử lý lỗi im lặng
                    }
                }
            }
        }
        catch (Exception)
        {
            // Xử lý lỗi im lặng
        }
    }

    private async Task<byte[]> CorrectImageOrientation(string imagePath)
    {
        try
        {
            // Đọc ảnh từ file
            byte[] originalBytes = await File.ReadAllBytesAsync(imagePath);

#if ANDROID
            // Sử dụng Android Bitmap để xử lý orientation
            using var bitmap = await Android.Graphics.BitmapFactory.DecodeByteArrayAsync(originalBytes, 0, originalBytes.Length);
            if (bitmap == null) return originalBytes;

            // Đọc EXIF data để lấy orientation
            var exif = new Android.Media.ExifInterface(imagePath);
            var orientation = exif.GetAttributeInt(Android.Media.ExifInterface.TagOrientation, 1);

            // Xác định góc xoay cần thiết
            int rotationAngle = 0;
            switch (orientation)
            {
                case 6: // ORIENTATION_ROTATE_90
                    rotationAngle = 90;
                    break;
                case 3: // ORIENTATION_ROTATE_180
                    rotationAngle = 180;
                    break;
                case 8: // ORIENTATION_ROTATE_270
                    rotationAngle = 270;
                    break;
                default:
                    // Không cần xoay (orientation = 1 là normal)
                    return originalBytes;
            }

            // Tạo matrix để xoay ảnh
            var matrix = new Android.Graphics.Matrix();
            matrix.PostRotate(rotationAngle);

            // Tạo bitmap mới đã được xoay
            using var rotatedBitmap = Android.Graphics.Bitmap.CreateBitmap(
                bitmap, 0, 0, bitmap.Width, bitmap.Height, matrix, true);

            // Chuyển bitmap thành byte array
            using var stream = new MemoryStream();
            await rotatedBitmap.CompressAsync(Android.Graphics.Bitmap.CompressFormat.Jpeg!, 90, stream);

            return stream.ToArray();
#else
            // Trên các platform khác, trả về ảnh gốc
            return originalBytes;
#endif
        }
        catch (Exception)
        {
            // Nếu có lỗi, trả về ảnh gốc
            return await File.ReadAllBytesAsync(imagePath);
        }
    }
}