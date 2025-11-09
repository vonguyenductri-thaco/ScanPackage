using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Storage;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;

using OfficeOpenXml;

// Alias tránh nhầm lẫn với System.IO.Path
using IOPath = System.IO.Path;

#if ANDROID
using AApp = Android.App.Application;
using Android.Media;
#endif

namespace ScanPackage;

public partial class DataEntryPage : ContentPage
{
    private readonly int _rows;
    private readonly int _cols;

    private readonly string[,] _cellValues;
    private readonly Dictionary<(int r, int c), Label> _cellLabelMap = new();

    private Label? _highlightLabel;
    private string? _existingFilePath; // Đường dẫn file nếu đang chỉnh sửa

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

        BuildGrid();
    }

    // Method để set giá trị cell từ bên ngoài (khi load từ Excel)
    public void SetCellValue(int row, int col, string value)
    {
        if (row >= 0 && row < _rows && col >= 0 && col < _cols)
        {
            _cellValues[row, col] = value;
            // Cập nhật UI nếu grid đã được build
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_cellLabelMap.TryGetValue((row, col), out var label))
                {
                    label.Text = value;
                }
            });
        }
    }

    // Method để load toàn bộ dữ liệu từ mảng (sau khi grid đã build)
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
    }

    // ====================== Lưới dữ liệu ======================
    private void BuildGrid()
    {
        DataGrid.RowDefinitions.Clear();
        DataGrid.ColumnDefinitions.Clear();
        DataGrid.Children.Clear();
        _cellLabelMap.Clear();

        // Cột 0 (header dòng) auto; cột dữ liệu ~120dp (~13 ký tự)
        DataGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        for (int c = 1; c <= _cols; c++)
            DataGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(120)));

        for (int r = 0; r <= _rows; r++)
            DataGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        // Header cột: Cột N -> 1
        for (int c = 1; c <= _cols; c++)
        {
            int colNum = _cols - c + 1;
            var lbl = new Label
            {
                Text = $"Cột {colNum}",
                FontAttributes = FontAttributes.Bold,
                BackgroundColor = Color.FromArgb("#F5F5F5"),
                TextColor = Color.FromArgb("#212121"),
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                Margin = 1,
                HeightRequest = 40,
                WidthRequest = 120,
                FontSize = 13
            };
            DataGrid.Children.Add(lbl);
            Grid.SetRow(lbl, 0);
            Grid.SetColumn(lbl, c);
        }

        // Header hàng (STT giảm dần)
        for (int r = 1; r <= _rows; r++)
        {
            int stt = _rows - r + 1;
            var lbl = new Label
            {
                Text = stt.ToString(),
                FontAttributes = FontAttributes.Bold,
                BackgroundColor = Color.FromArgb("#F5F5F5"),
                TextColor = Color.FromArgb("#212121"),
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                Margin = 1,
                HeightRequest = 40,
                FontSize = 13
            };
            DataGrid.Children.Add(lbl);
            Grid.SetRow(lbl, r);
            Grid.SetColumn(lbl, 0);
        }

        // Ô dữ liệu = Label trong Border + Tap
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
                    FontSize = 13,
                    HeightRequest = 40,
                    WidthRequest = 120,
                    Margin = 1,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center,
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

                DataGrid.Children.Add(border);
                Grid.SetRow(border, r);
                Grid.SetColumn(border, c);
            }
        }
    }

    private async System.Threading.Tasks.Task StartScanForCell(Label cellLabel, int rr, int cc)
    {
        HighlightCell(cellLabel);

        // Cho phép chọn giữa nhập tay hoặc quét barcode
        var action = await DisplayActionSheet(
            "Nhập dữ liệu cho ô",
            "Hủy",
            null,
            "Nhập tay",
            "Quét barcode");

        string? entered = null;

        if (action == "Nhập tay")
        {
            entered = await DisplayPromptAsync("Nhập dữ liệu", "Nhập giá trị cho ô:", "OK", "Hủy", keyboard: Keyboard.Default);
        }
        else if (action == "Quét barcode")
        {
            entered = await ScanBarcodeAsync();
        }

        if (string.IsNullOrWhiteSpace(entered))
        {
            cellLabel.BackgroundColor = Colors.White;
            _highlightLabel = null;
            return;
        }

        _cellValues[rr, cc] = entered.Trim();
        cellLabel.Text = _cellValues[rr, cc];

        PlayBeep();

        cellLabel.BackgroundColor = Colors.White;
        _highlightLabel = null;
    }

    private async Task<string?> ScanBarcodeAsync()
    {
        var tcs = new System.Threading.Tasks.TaskCompletionSource<string?>();
        var scanPage = new CellScanPage(tcs);
        await Navigation.PushModalAsync(scanPage);
        return await tcs.Task;
    }

    private async void OnBackButtonClicked(object sender, EventArgs e)
    {
        await ConfirmLeaveAsync();
    }

    private async System.Threading.Tasks.Task NextCellAndScan(int rr, int cc)
    {
        int nextC = cc + 1;
        int nextR = rr;
        if (nextC >= _cols) { nextC = 0; nextR++; }
        if (nextR >= _rows) return;

        if (_cellLabelMap.TryGetValue((nextR, nextC), out var nextLabel))
            await StartScanForCell(nextLabel, nextR, nextC);
    }

    private void HighlightCell(Label label)
    {
        if (_highlightLabel != null)
            _highlightLabel.BackgroundColor = Colors.White;

        label.BackgroundColor = Color.FromArgb("#E3F2FD");
        _highlightLabel = label;
    }

    // ====================== Beep ======================
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

    // ====================== Xuất & Share Excel ======================
    private static string SanitizeFileName(string name)
    {
        foreach (var ch in IOPath.GetInvalidFileNameChars())
            name = name.Replace(ch, '_');
        return string.IsNullOrWhiteSpace(name) ? "export" : name;
    }

    private async void OnExportClicked(object sender, EventArgs e)
    {
        try
        {
            string path;
            bool isEditing = !string.IsNullOrEmpty(_existingFilePath);

            if (isEditing)
            {
                // Đang chỉnh sửa file cũ - lưu vào file đó
                path = _existingFilePath!;
            }
            else
            {
                // Tạo file mới
                var dt = DateEntry.Date;

                var datePart = dt.ToString("yyyy.MM.dd");
                var sttPart = (SttEntry.Text ?? "").Trim();
                var contPart = (ContainerEntry.Text ?? "").Trim();
                var sealPart = (SealEntry.Text ?? "").Trim();

                var raw = $"{datePart}_{sttPart}_{contPart}_{sealPart}";
                var fileName = SanitizeFileName(raw) + ".xlsx";
                path = IOPath.Combine(FileSystem.AppDataDirectory, fileName);
            }

            // FIX: EPPlus 8.x - Set license khi tạo ExcelPackage
            using (var pkg = new OfficeOpenXml.ExcelPackage())
            {
                var ws = pkg.Workbook.Worksheets.Add("Sheet1");

                // Thêm header cột (Cột N -> 1)
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

                // Thêm header hàng (STT giảm dần) và dữ liệu
                for (int r = 0; r < _rows; r++)
                {
                    int stt = _rows - r;
                    // Header hàng
                    var headerCell = ws.Cells[r + 2, 1];
                    headerCell.Value = stt.ToString();
                    headerCell.Style.Font.Bold = true;
                    headerCell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    headerCell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    headerCell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    headerCell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                    
                    // Dữ liệu
                    for (int c = 0; c < _cols; c++)
                    {
                        var dataCell = ws.Cells[r + 2, c + 2];
                        dataCell.Value = _cellValues[r, c] ?? "";
                        dataCell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                        dataCell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                    }
                }

                // AutoFit độ rộng cột để vừa với nội dung
                ws.Cells[ws.Dimension.Address].AutoFitColumns();

                pkg.SaveAs(new FileInfo(path));
            }

            var message = isEditing ? "Đã cập nhật file thành công!" : "Đã lưu file thành công!";
            await DisplayAlert("Thành công", message, "OK");

            // Quay về MainPage sau khi lưu
            await Navigation.PopToRootAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi xuất Excel", ex.Message, "Đóng");
        }
    }

    // ====================== Cảnh báo khi lùi ======================
    protected override bool OnBackButtonPressed()
    {
        MainThread.BeginInvokeOnMainThread(async () => await ConfirmLeaveAsync());
        return true;
    }

    private async System.Threading.Tasks.Task ConfirmLeaveAsync()
    {
        bool hasData = HasAnyDataFilled();
        if (!hasData) { await Navigation.PopAsync(); return; }

        bool ok = await DisplayAlert(
            "Xác nhận",
            "Bạn có chắc muốn rời khỏi trang nhập liệu? Dữ liệu chưa lưu sẽ bị mất.",
            "Rời đi", "Ở lại");

        if (ok) await Navigation.PopAsync();
    }

    private bool HasAnyDataFilled()
    {
        for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _cols; c++)
                if (!string.IsNullOrEmpty(_cellValues[r, c]))
                    return true;

        return
            !string.IsNullOrWhiteSpace(SttEntry.Text) ||
            !string.IsNullOrWhiteSpace(ContainerEntry.Text) ||
            !string.IsNullOrWhiteSpace(SealEntry.Text);
    }

    // ====================== Chụp ảnh và quét Container / Seal bằng OCR ======================
    private async void OnContainerOcrClicked(object sender, EventArgs e)
    {
        var services = Application.Current?.Handler?.MauiContext?.Services;
        var ocr = services?.GetService<IOcrService>();
        if (ocr == null)
        {
            await DisplayAlert("OCR", "OCR chưa sẵn sàng.", "Đóng");
            return;
        }

        // Chỉ dùng chế độ chụp ảnh rồi quét
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

    private async void OnSealOcrClicked(object sender, EventArgs e)
    {
        var services = Application.Current?.Handler?.MauiContext?.Services;
        var ocr = services?.GetService<IOcrService>();
        if (ocr == null)
        {
            await DisplayAlert("OCR", "OCR chưa sẵn sàng.", "Đóng");
            return;
        }

        // Chỉ dùng chế độ chụp ảnh rồi quét
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
}