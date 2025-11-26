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

    private void OnPhoto1Tapped(object? sender, EventArgs e) => ShowPhotoActions(1);
    private void OnPhoto2Tapped(object? sender, EventArgs e) => ShowPhotoActions(2);
    private void OnPhoto3Tapped(object? sender, EventArgs e) => ShowPhotoActions(3);
    private void OnPhoto4Tapped(object? sender, EventArgs e) => ShowPhotoActions(4);

    private async void ShowPhotoActions(int photoIndex)
    {
        string currentPath = photoIndex switch
        {
            1 => _photo1Path,
            2 => _photo2Path,
            3 => _photo3Path,
            4 => _photo4Path,
            _ => null
        };

        var options = currentPath == null
            ? new[] { "Ch?p ?nh" }
            : new[] { "Ch?p l?i", "Xóa ?nh" };

        var action = await DisplayActionSheet("?nh ch?p", "H?y", null, options);
        if (action == "Ch?p ?nh" || action == "Ch?p l?i")
        {
            await CapturePhotoAsync(photoIndex);
        }
        else if (action == "Xóa ?nh")
        {
            await DeletePhotoAsync(photoIndex);
        }
    }

    private async Task CapturePhotoAsync(int photoIndex)
    {
        try
        {
            if (MediaPicker.Default.IsCaptureSupported)
            {
                var photo = await MediaPicker.Default.CapturePhotoAsync();
                if (photo != null)
                {
                    var folder = FileSystem.AppDataDirectory;
                    var fileName = $"photo_{photoIndex}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                    var targetPath = IOPath.Combine(folder, fileName);

                    using var sourceStream = await photo.OpenReadAsync();
                    using var targetStream = File.Create(targetPath);
                    await sourceStream.CopyToAsync(targetStream);

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
                }
            }
            else
            {
                await DisplayAlert("L?i", "Camera không du?c h? tr? trên thi?t b? này.", "OK");
            }
        }
        catch
        {
            await DisplayAlert("L?i", "Không th? ch?p ?nh.", "OK");
        }
    }

    private async Task DeletePhotoAsync(int photoIndex)
    {
        bool confirm = await DisplayAlert("Xóa ?nh", "B?n có ch?c mu?n xóa ?nh này?", "Xóa", "H?y");
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
            await DisplayAlert("L?i", "Không th? xóa ?nh.", "OK");
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
                            var displayName = $"{user.Name} - {user.Position}";
                            CreatorLabel.Text = displayName;
                            CreatorLabel.TextColor = Color.FromArgb("#212121");

                        }
                        else
                        {

                            // N?u không tìm th?y user theo MSNV, th? tìm theo tên
                            if (!string.IsNullOrEmpty(creatorName))
                            {
                                var userByName = UserService.Instance.GetAllUsers().FirstOrDefault(u =>
                                    u.Name.Equals(creatorName, StringComparison.OrdinalIgnoreCase));
                                if (userByName != null)
                                {
                                    _selectedUser = userByName;
                                    var displayName = $"{userByName.Name} - {userByName.Position}";
                                    CreatorLabel.Text = displayName;
                                    CreatorLabel.TextColor = Color.FromArgb("#212121");

                                }
                                else
                                {
                                    // N?u v?n không tìm th?y, t?o user t?m v?i tên g?c
                                    _selectedUser = new UserData { Name = creatorName, Position = "", Msnv = "" };
                                    CreatorLabel.Text = creatorName; // Ch? hi?n th? tên
                                    CreatorLabel.TextColor = Color.FromArgb("#212121");

                                }
                            }
                        }
                    }
                    else if (!string.IsNullOrEmpty(creatorName))
                    {
                        // N?u không có MSNV nhung có tên, t?o user t?m và hi?n th? tên
                        _selectedUser = new UserData { Name = creatorName, Position = "", Msnv = "" };
                        CreatorLabel.Text = creatorName; // Ch? hi?n th? tên
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
            var checkSheet = package.Workbook.Worksheets["Phi?u ki?m tra"];
            
            if (checkSheet?.Drawings == null)
            {

                return;
            }

            // T?o thu m?c temp d? luu ?nh
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
                    catch (Exception ex)
                    {

                    }
                }
            }
            

        }
        catch (Exception ex)
        {

        }
    }

    private void FillSeriesToCheckSheet(ExcelWorksheet checkSheet)
    {
        try
        {



            int totalDataCells = _rows * _cols;
            int seriesIndex = 0;
            int totalSeriesWithData = 0;
            int totalFilled = 0;
            int totalSkipped = 0;
            int totalOverflow = 0;

            // Ð?m t?ng s? series có d? li?u
            for (int i = 0; i < totalDataCells; i++)
            {
                if (!string.IsNullOrWhiteSpace(GetSeriesValueByIndex(i)))
                    totalSeriesWithData++;
            }


            // 1) Ð? vào c?t H: H13 ? H75 (gi? nguyên v? trí theo index)
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


                    totalFilled++;
                }
                else
                {
                    // B? QUA Ô NÀY - KHÔNG GHI GÌ C?

                    totalSkipped++;
                }

                seriesIndex++; // QUAN TR?NG: V?n tang index dù ô tr?ng
            }

            // 2) Ð? ti?p vào c?t L: L13 ? L75 (ti?p t?c index)
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


                    totalFilled++;
                }
                else
                {
                    // B? QUA Ô NÀY - KHÔNG GHI GÌ C?

                    totalSkipped++;
                }

                seriesIndex++; // QUAN TR?NG: V?n tang index dù ô tr?ng
            }

            // Ð?m s? b? th?a (n?u còn d? li?u sau khi h?t L75)
            if (seriesIndex < totalDataCells)
            {
                totalOverflow = totalDataCells - seriesIndex;
                for (int i = seriesIndex; i < totalDataCells; i++)
                {
                    if (!string.IsNullOrWhiteSpace(GetSeriesValueByIndex(i)))
                    {

                    }
                }
            }








        }
        catch
        {

        }
    }

    private string GetSeriesValueByIndex(int index)
    {
        try
        {
            // Column-major: di h?t các hàng c?a 1 c?t r?i sang c?t ti?p theo
            int col = index / _rows;   // c?t logic (0,1,2,...)
            int row = index % _rows;   // hàng (0.._rows-1)

            // UI hi?n th? c?t d?o: C?t 1 UI là c?t v?t lý cu?i cùng
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
                Text = $"C?t {colNum}",
                FontAttributes = FontAttributes.Bold,
                BackgroundColor = Color.FromArgb("#F5F5F5"),
                TextColor = Color.FromArgb("#212121"),
                HorizontalTextAlignment = MauiTextAlignment.Center,
                VerticalTextAlignment = MauiTextAlignment.Center,
                Margin = 1,
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
                Margin = 1,
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
                    Margin = 1,
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
                    Margin = new Thickness(0.5),
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
                Text = $"C?t {colNum}",
                FontAttributes = FontAttributes.Bold,
                BackgroundColor = Color.FromArgb("#F5F5F5"),
                TextColor = Color.FromArgb("#212121"),
                HorizontalTextAlignment = MauiTextAlignment.Center,
                VerticalTextAlignment = MauiTextAlignment.Center,
                Margin = 1,
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
                Margin = 1,
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
                await DisplayAlert("OCR", "OCR chua s?n sàng.", "Ðóng");
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
                    "Không quét du?c Container",
                    "Không th? nh?n di?n s? Container t? ?nh.\n\n" +
                    "Hãy th?:\n" +
                    "• Ch?p ?nh rõ nét hon\n" +
                    "• Ð?m b?o d? ánh sáng\n" +
                    "• Ch?p th?ng góc (không xiên)\n" +
                    "• Zoom vào vùng có s? Container\n" +
                    "• Ð?m b?o s? Container n?m trong khung xanh\n\n" +
                    "Format Container: 4 ch? cái + 7 s?\n" +
                    "Ví d?: KOCU 411486 2",
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
                await DisplayAlert("OCR", "OCR chua s?n sàng.", "Ðóng");
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
                    "Không quét du?c Seal",
                    "Không th? nh?n di?n s? Seal t? ?nh.\n\n" +
                    "Hãy th?:\n" +
                    "• Ch?p ?nh rõ nét hon\n" +
                    "• Ð?m b?o d? ánh sáng\n" +
                    "• Ch?p th?ng góc (không xiên)\n" +
                    "• Zoom vào vùng có s? Seal\n" +
                    "• Ð?m b?o s? Seal n?m trong khung xanh\n\n" +
                    "Format Seal: 6-15 ký t? (ch? + s?)\n" +
                    "Ví d?: YN646E4AO",
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
                await DisplayAlert("Thông báo", "Chua có d? li?u khách hàng.\n\nKi?m tra:\n1. K?t n?i internet\n2. D? li?u dã upload lên Firebase\n3. Firebase config dúng", "OK");
                return;
            }

            var tcs = new TaskCompletionSource<string?>();
            var pickerPage = new SearchablePickerPage("Ch?n khách hàng", customers, tcs);

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
            await DisplayAlert("L?i", "Không th? ch?n khách hàng.", "OK");
        }
    }

    private async void OnProductPickerTapped(object sender, EventArgs e)
    {
        if (_isProcessing) return;

        if (string.IsNullOrEmpty(_selectedCustomer))
        {
            await DisplayAlert("Thông báo", "Vui lòng ch?n khách hàng tru?c", "OK");
            return;
        }

        try
        {
            var products = ProductDataService.Instance.GetProducts(_selectedCustomer);

            if (products.Count == 0)
            {
                await DisplayAlert("Thông báo", $"Không có s?n ph?m cho khách hàng '{_selectedCustomer}'", "OK");
                return;
            }

            var tcs = new TaskCompletionSource<string?>();
            var pickerPage = new SearchablePickerPage("Ch?n s?n ph?m", products, tcs);

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
            await DisplayAlert("L?i", "Không th? ch?n s?n ph?m.", "OK");
        }
    }

    private async void OnModelPickerTapped(object sender, EventArgs e)
    {
        if (_isProcessing) return;

        if (string.IsNullOrEmpty(_selectedCustomer))
        {
            await DisplayAlert("Thông báo", "Vui lòng ch?n khách hàng tru?c", "OK");
            return;
        }

        if (string.IsNullOrEmpty(_selectedProduct))
        {
            await DisplayAlert("Thông báo", "Vui lòng ch?n s?n ph?m tru?c", "OK");
            return;
        }

        try
        {
            var models = ProductDataService.Instance.GetModels(_selectedCustomer, _selectedProduct);

            if (models.Count == 0)
            {
                await DisplayAlert("Thông báo", $"Không có model cho s?n ph?m '{_selectedProduct}' c?a khách hàng '{_selectedCustomer}'", "OK");
                return;
            }

            var tcs = new TaskCompletionSource<string?>();
            var pickerPage = new SearchablePickerPage("Ch?n model", models, tcs);

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
            await DisplayAlert("L?i", "Không th? ch?n model.", "OK");
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
                await DisplayAlert("Thông báo", "Chua có d? li?u ngu?i l?p.\n\nKi?m tra:\n1. K?t n?i internet\n2. D? li?u dã upload lên Firebase\n3. Firebase config dúng", "OK");
                return;
            }

            var tcs = new TaskCompletionSource<string?>();
            var pickerPage = new SearchablePickerPage("Ch?n ngu?i l?p", userDisplayNames, tcs);

            await Navigation.PushModalAsync(pickerPage);

            var selected = await tcs.Task;

            if (!string.IsNullOrEmpty(selected))
            {
                var newUser = UserService.Instance.GetUserByDisplayName(selected);
                if (newUser != null && (_selectedUser == null || _selectedUser.Msnv != newUser.Msnv))
                {
                    _selectedUser = newUser;
                    CreatorLabel.Text = selected;
                    CreatorLabel.TextColor = Color.FromArgb("#212121");
                }
            }
        }
        catch
        {
            await DisplayAlert("L?i", "Không th? ch?n ngu?i l?p.", "OK");
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

        try
        {
            if (string.IsNullOrEmpty(_selectedCustomer))
            {
                await DisplayAlert("Thi?u thông tin", "Vui lòng ch?n tên khách hàng", "OK");
                return;
            }

            if (string.IsNullOrEmpty(_selectedProduct))
            {
                await DisplayAlert("Thi?u thông tin", "Vui lòng ch?n tên s?n ph?m", "OK");
                return;
            }

            if (string.IsNullOrEmpty(_selectedModel))
            {
                await DisplayAlert("Thi?u thông tin", "Vui lòng ch?n model", "OK");
                return;
            }

            if (_selectedUser == null)
            {
                await DisplayAlert("Thi?u thông tin", "Vui lòng ch?n ngu?i l?p", "OK");
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
                    await DisplayAlert("Thông báo", "Không có thay d?i nào d? luu", "OK");
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
                var ws = pkg.Workbook.Worksheets.Add("B?ng d? li?u");

                for (int c = 0; c < _cols; c++)
                {
                    int colNum = _cols - c;
                    var cell = ws.Cells[1, c + 2];
                    cell.Value = $"C?t {colNum}";
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

                // Thêm Sheet 2 t? template - ch?y trên background thread
                await Task.Run(async () =>
                {
                    using (var templateStream = await FileSystem.Current.OpenAppPackageFileAsync("Template/BM_Phieu kiem tra dong container.xlsx"))
                    using (var templatePkg = new ExcelPackage(templateStream))
                    {
                        var templateSheet = templatePkg.Workbook.Worksheets["Sheet2"];
                        if (templateSheet != null)
                        {
                            var newSheet = pkg.Workbook.Worksheets.Add("Phi?u ki?m tra", templateSheet);
                            
                            // Ði?n thông tin vào các ô tuong ?ng
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
                            dateCell.Value = DateEntry.Date.ToString("dd/MM/yyyy"); // Ngày ki?m tra
                            dateCell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                            dateCell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                            
                            var sttCell = newSheet.Cells["D8"];
                            sttCell.Value = sttPart;            // S? th? t?
                            sttCell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                            sttCell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;

                            var contCell = newSheet.Cells["L5"];
                            contCell.Value = contPart;           // S? container
                            contCell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                            contCell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                            
                            var sealCell = newSheet.Cells["L7"];
                            sealCell.Value = sealPart;           // S? seal
                            sealCell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                            sealCell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                            
                            var userCell = newSheet.Cells["L9"];
                            userCell.Value = _selectedUser?.Name; // Nhân viên ki?m tra
                            userCell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                            userCell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;

                            // Ði?n s? series vào sheet 2 (Phi?u ki?m tra)
                            FillSeriesToCheckSheet(newSheet);

                            // Chèn ?nh th?c t? vào vùng A35:F75 (4 ?nh)
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
                        Title = "Chia s? file Excel",
                        File = new ShareFile(path)
                    });
                }
                catch
                {
                    await DisplayAlert("L?i", "Không th? chia s? file.", "OK");
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
            await DisplayAlert("L?i xu?t Excel", "L?i xu?t Excel.", "Ðóng");
        }
        finally
        {
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
                "Thoát mà không luu?",
                "B?n có thay d?i chua luu. B?n có ch?c mu?n thoát?",
                "Thoát",
                "H?y"
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
        }
        catch
        {
        }
    }

    private async Task InsertPhotosToExcel(ExcelWorksheet worksheet)
    {
        try
        {
            // Layout 2x2: 4 ?nh trong Excel
            // M?i ?nh width t? A d?n 80% c?t C, t? l? 9:16

            var photos = new[]
            {
                _photo1Path,
                _photo2Path, 
                _photo3Path,
                _photo4Path
            };

            var positions = new[]
            {
                new { StartCol = 1, EndCol = 3, StartRow = 35, EndRow = 54 }, // A35:C54 - ?nh 1 (hàng 1, c?t 1)
                new { StartCol = 4, EndCol = 6, StartRow = 35, EndRow = 54 }, // D35:F54 - ?nh 2 (hàng 1, c?t 2)
                new { StartCol = 1, EndCol = 3, StartRow = 55, EndRow = 75 }, // A55:C75 - ?nh 3 (hàng 2, c?t 1)
                new { StartCol = 4, EndCol = 6, StartRow = 55, EndRow = 75 }  // D55:F75 - ?nh 4 (hàng 2, c?t 2)
            };

            // Chèn t?t c? 4 ?nh
            for (int i = 0; i < photos.Length; i++)
            {
                if (!string.IsNullOrEmpty(photos[i]) && File.Exists(photos[i]))
                {
                    try
                    {
                        var pos = positions[i];
                        
                        // Ð?c ?nh t? file
                        using var imageStream = File.OpenRead(photos[i]!);
                        
                        // Thêm ?nh vào worksheet
                        var picture = worksheet.Drawings.AddPicture($"Photo{i + 1}", imageStream);
                        
                        // Ð?t v? trí ?nh theo position
                        picture.From.Column = pos.StartCol - 1; // 0-based index
                        picture.From.Row = pos.StartRow - 1;
                        
                        // Kích thu?c theo yêu c?u W:300px v?i t? l? 9:16
                        int maxWidth = 300;  // pixels theo yêu c?u
                        int maxHeight = (int)(maxWidth * 16.0 / 9.0); // 533 pixels (9:16 ratio chính xác)
                        
                        picture.SetSize(maxWidth, maxHeight);
                        
                        // Ð?t ch? d? không resize khi thay d?i cell
                        picture.EditAs = OfficeOpenXml.Drawing.eEditAs.Absolute;
                        

                    }
                    catch (Exception ex)
                    {

                    }
                }
            }
        }
        catch (Exception ex)
        {

        }
    }
}
