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

    // Product selection fields
    private string? _selectedCustomer;
    private string? _selectedProduct;
    private string? _selectedModel;

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

        // Load product data
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await ProductDataService.Instance.LoadDataAsync();
                System.Diagnostics.Debug.WriteLine("Product data loaded successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load product data: {ex.Message}");
                await DisplayAlert("Lỗi", "Không thể load dữ liệu sản phẩm", "OK");
            }
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
            CreatorEntry.Focused -= OnMetadataFieldFocused;
            CreatorEntry.Unfocused -= OnMetadataFieldUnfocused;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnDisappearing unsubscribe error: {ex.Message}");
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

            System.Diagnostics.Debug.WriteLine($"Calculated table height: {calculatedHeight}px for {_rows} rows");

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (TableContainerFrame != null)
                {
                    TableContainerFrame.HeightRequest = calculatedHeight;
                    TableContainerFrame.VerticalOptions = LayoutOptions.Start;

                    System.Diagnostics.Debug.WriteLine($"Set TableContainerFrame.HeightRequest = {calculatedHeight}");
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SetDynamicTableHeight error: {ex.Message}");
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

        CreatorEntry.Focused += OnMetadataFieldFocused;
        CreatorEntry.Unfocused += OnMetadataFieldUnfocused;
    }

    private void OnMetadataFieldFocused(object sender, FocusEventArgs e)
    {
        _isAnyFieldFocused = true;
        System.Diagnostics.Debug.WriteLine("Metadata field focused");
    }

    private void OnMetadataFieldUnfocused(object sender, FocusEventArgs e)
    {
        _isAnyFieldFocused = false;
        System.Diagnostics.Debug.WriteLine("Metadata field unfocused");
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadMetadata error: {ex.Message}");
            }
        });
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
                Text = $"Cột {colNum}",
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateDuplicateHighlighting error: {ex.Message}");
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"StartScanForCell error: {ex.Message}");
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ScanBarcodeAsync error: {ex.Message}");
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
            CreatorEntry.IsEnabled = enabled;
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
        catch { }
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

    // ==================== PRODUCT SELECTION HANDLERS ====================

    private async void OnCustomerPickerTapped(object sender, EventArgs e)
    {
        if (_isProcessing) return;

        try
        {
            var customers = ProductDataService.Instance.GetCustomers();

            if (customers.Count == 0)
            {
                await DisplayAlert("Thông báo", "Chưa có dữ liệu khách hàng", "OK");
                return;
            }

            var tcs = new TaskCompletionSource<string?>();
            var pickerPage = new SearchablePickerPage("Chọn khách hàng", customers, tcs);

            await Navigation.PushModalAsync(pickerPage);

            var selected = await tcs.Task;

            if (!string.IsNullOrEmpty(selected))
            {
                _selectedCustomer = selected;
                CustomerLabel.Text = selected;
                CustomerLabel.TextColor = Color.FromArgb("#212121");

                // Reset product và model khi đổi customer
                _selectedProduct = null;
                _selectedModel = null;
                ProductLabel.Text = "Chọn sản phẩm";
                ProductLabel.TextColor = Color.FromArgb("#9E9E9E");
                ModelLabel.Text = "Chọn model";
                ModelLabel.TextColor = Color.FromArgb("#9E9E9E");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnCustomerPickerTapped error: {ex.Message}");
            await DisplayAlert("Lỗi", $"Không thể chọn khách hàng: {ex.Message}", "OK");
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
                await DisplayAlert("Thông báo", "Không có sản phẩm cho khách hàng này", "OK");
                return;
            }

            var tcs = new TaskCompletionSource<string?>();
            var pickerPage = new SearchablePickerPage("Chọn sản phẩm", products, tcs);

            await Navigation.PushModalAsync(pickerPage);

            var selected = await tcs.Task;

            if (!string.IsNullOrEmpty(selected))
            {
                _selectedProduct = selected;
                ProductLabel.Text = selected;
                ProductLabel.TextColor = Color.FromArgb("#212121");

                // Reset model khi đổi product
                _selectedModel = null;
                ModelLabel.Text = "Chọn model";
                ModelLabel.TextColor = Color.FromArgb("#9E9E9E");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnProductPickerTapped error: {ex.Message}");
            await DisplayAlert("Lỗi", $"Không thể chọn sản phẩm: {ex.Message}", "OK");
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
                await DisplayAlert("Thông báo", "Không có model cho sản phẩm này", "OK");
                return;
            }

            var tcs = new TaskCompletionSource<string?>();
            var pickerPage = new SearchablePickerPage("Chọn model", models, tcs);

            await Navigation.PushModalAsync(pickerPage);

            var selected = await tcs.Task;

            if (!string.IsNullOrEmpty(selected))
            {
                _selectedModel = selected;
                ModelLabel.Text = selected;
                ModelLabel.TextColor = Color.FromArgb("#212121");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"OnModelPickerTapped error: {ex.Message}");
            await DisplayAlert("Lỗi", $"Không thể chọn model: {ex.Message}", "OK");
        }
    }

    // ==================== EXPORT EXCEL ====================

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
            // Validate product fields
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

            if (string.IsNullOrWhiteSpace(CreatorEntry.Text))
            {
                await DisplayAlert("Thiếu thông tin", "Vui lòng nhập tên người lập", "OK");
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
            var creatorPart = SanitizeFileName(CreatorEntry.Text?.Trim() ?? "");

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

                if (!metadataChanged && !cellsChanged)
                {
                    await DisplayAlert("Thông báo", "Không có thay đổi nào để lưu", "OK");
                    _navigationConfirmed = true;
                    await Navigation.PopToRootAsync();
                    return;
                }
                else if (metadataChanged)
                {
                    path = IOPath.Combine(FileSystem.AppDataDirectory, baseFileName + ".xlsx");
                }
                else
                {
                    path = GetUniqueFilePath(baseFileName);
                }
            }
            else
            {
                path = IOPath.Combine(FileSystem.AppDataDirectory, baseFileName + ".xlsx");
            }

            using (var pkg = new ExcelPackage())
            {
                // Add metadata sheet
                var metaWs = pkg.Workbook.Worksheets.Add("Thông tin");
                metaWs.Cells["A1"].Value = "Ngày";
                metaWs.Cells["B1"].Value = DateEntry.Date.ToString("dd/MM/yyyy");
                metaWs.Cells["A2"].Value = "Số thứ tự";
                metaWs.Cells["B2"].Value = sttPart;
                metaWs.Cells["A3"].Value = "Số container";
                metaWs.Cells["B3"].Value = contPart;
                metaWs.Cells["A4"].Value = "Số seal";
                metaWs.Cells["B4"].Value = sealPart;
                metaWs.Cells["A5"].Value = "Tên khách hàng";
                metaWs.Cells["B5"].Value = _selectedCustomer;
                metaWs.Cells["A6"].Value = "Tên sản phẩm";
                metaWs.Cells["B6"].Value = _selectedProduct;
                metaWs.Cells["A7"].Value = "Model";
                metaWs.Cells["B7"].Value = _selectedModel;
                metaWs.Cells["A8"].Value = "Người lập";
                metaWs.Cells["B8"].Value = CreatorEntry.Text?.Trim();

                metaWs.Cells["A:A"].Style.Font.Bold = true;
                metaWs.Cells["A:A"].AutoFitColumns();
                metaWs.Cells["B:B"].AutoFitColumns();

                // Add data sheet
                var ws = pkg.Workbook.Worksheets.Add("Dữ liệu");

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
                    int stt = _rows - r;
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

                var fileInfo = new System.IO.FileInfo(path);
                pkg.SaveAs(fileInfo);
            }

            if (isEditing)
            {
                UpdateOriginalCellValues();
            }

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
                catch (Exception shareEx)
                {
                    await DisplayAlert("Lỗi", $"Không thể chia sẻ file:\n{shareEx.Message}", "OK");
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
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi xuất Excel", ex.Message, "Đóng");
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
            System.Diagnostics.Debug.WriteLine("Field is focused - treated as having changes");
            return true;
        }

        // Kiểm tra product fields có giá trị không
        if (!string.IsNullOrEmpty(_selectedCustomer) ||
            !string.IsNullOrEmpty(_selectedProduct) ||
            !string.IsNullOrEmpty(_selectedModel) ||
            !string.IsNullOrEmpty(CreatorEntry.Text?.Trim()))
        {
            return true;
        }

        if (string.IsNullOrEmpty(_existingFilePath))
            return true;

        try
        {
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HasAnyChangesIncludingMetadata metadata check error: {ex.Message}");
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ParseMetadataFromFilename error: {ex.Message}");
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
}