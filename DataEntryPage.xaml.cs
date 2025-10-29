using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Maui.Controls;
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
    private bool _autoContinueScan = true;

    public DataEntryPage(int rows, int cols)
    {
        InitializeComponent();

        _rows = rows;
        _cols = cols;
        _cellValues = new string[_rows, _cols];

        DateEntry.Text = DateTime.Now.ToString("dd/MM/yyyy");

        NavigationPage.SetHasBackButton(this, false);
        ToolbarItems.Add(new ToolbarItem
        {
            Text = "Quay lại",
            Command = new Command(async () => await ConfirmLeaveAsync())
        });

        BuildGrid();
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
                BackgroundColor = Colors.LightGray,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                Margin = 1,
                HeightRequest = 40,
                WidthRequest = 120
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
                BackgroundColor = Colors.LightGray,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                Margin = 1,
                HeightRequest = 40
            };
            DataGrid.Children.Add(lbl);
            Grid.SetRow(lbl, r);
            Grid.SetColumn(lbl, 0);
        }

        // Ô dữ liệu = Label trong Frame + Tap
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
                    FontSize = 14,
                    HeightRequest = 40,
                    WidthRequest = 120,
                    Margin = 1,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center,
                    TextColor = Colors.Black
                };

                var tap = new TapGestureRecognizer();
                tap.Tapped += async (_, __) => await StartScanForCell(cellLabel, rr, cc);
                cellLabel.GestureRecognizers.Add(tap);

                var frame = new Frame
                {
                    Content = cellLabel,
                    BorderColor = Colors.Silver,
                    Padding = 0,
                    Margin = new Thickness(0.5),
                    HasShadow = false,
                    CornerRadius = 0
                };
                frame.GestureRecognizers.Add(tap);

                _cellLabelMap[(rr, cc)] = cellLabel;

                DataGrid.Children.Add(frame);
                Grid.SetRow(frame, r);
                Grid.SetColumn(frame, c);
            }
        }
    }

    private async System.Threading.Tasks.Task StartScanForCell(Label cellLabel, int rr, int cc)
    {
        HighlightCell(cellLabel);

        await Navigation.PushAsync(new BarcodeScanPage(result =>
        {
            _cellValues[rr, cc] = result;
            cellLabel.Text = result;

            PlayBeep();

            cellLabel.BackgroundColor = Colors.White;
            _highlightLabel = null;

            if (_autoContinueScan)
                _ = NextCellAndScan(rr, cc); // fire & forget
        }));
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

        label.BackgroundColor = Color.FromArgb("#D1C4E9");
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
            var dateText = (DateEntry.Text ?? "").Trim();
            if (!DateTime.TryParseExact(dateText, "dd/MM/yyyy", null,
                System.Globalization.DateTimeStyles.None, out var dt))
                dt = DateTime.Now;

            var datePart = dt.ToString("yyyy.MM.dd");
            var sttPart = (SttEntry.Text ?? "").Trim();
            var contPart = (ContainerEntry.Text ?? "").Trim();
            var sealPart = (SealEntry.Text ?? "").Trim();

            var raw = $"{datePart}_{sttPart}_{contPart}_{sealPart}";
            var fileName = SanitizeFileName(raw) + ".xlsx";
            var path = IOPath.Combine(FileSystem.AppDataDirectory, fileName);

            // FIX: EPPlus 8.x - Set license khi tạo ExcelPackage
            using (var pkg = new OfficeOpenXml.ExcelPackage())
            {

                var ws = pkg.Workbook.Worksheets.Add("Sheet1");

                for (int r = 0; r < _rows; r++)
                    for (int c = 0; c < _cols; c++)
                        ws.Cells[r + 1, c + 1].Value = _cellValues[r, c] ?? "";

                pkg.SaveAs(new FileInfo(path));
            }

            await DisplayAlert("Xuất Excel", $"Đã lưu file:\n{path}", "OK");

            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "Chia sẻ file Excel",
                File = new ShareFile(path)
            });
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

    // ====================== Nhập thủ công Container / Seal (TẠM THỜI - thay cho OCR) ======================
    private async void OnContainerOcrClicked(object sender, EventArgs e)
    {
        string result = await DisplayPromptAsync(
            "Nhập Số Container",
            "Nhập thủ công (OCR sẽ được thêm sau):",
            initialValue: ContainerEntry.Text ?? "",
            maxLength: 50,
            keyboard: Keyboard.Text);

        if (!string.IsNullOrWhiteSpace(result))
            ContainerEntry.Text = result.ToUpperInvariant().Trim();
    }

    private async void OnSealOcrClicked(object sender, EventArgs e)
    {
        string result = await DisplayPromptAsync(
            "Nhập Số Seal",
            "Nhập thủ công (OCR sẽ được thêm sau):",
            initialValue: SealEntry.Text ?? "",
            maxLength: 50,
            keyboard: Keyboard.Text);

        if (!string.IsNullOrWhiteSpace(result))
            SealEntry.Text = result.ToUpperInvariant().Trim();
    }
}