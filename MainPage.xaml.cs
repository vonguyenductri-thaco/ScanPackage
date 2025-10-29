using Microsoft.Maui.ApplicationModel.DataTransfer;
using System.Collections.ObjectModel;

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
            Files.Add(new FileItem
            {
                FileName = Path.GetFileName(file),
                FullPath = file
            });
        }
    }

    private async void OnFileSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is FileItem item)
        {
            var action = await DisplayActionSheet(
                item.FileName,
                "Hủy", null,
                "Mở chia sẻ", "Xóa");

            if (action == "Mở chia sẻ")
            {
                await Share.Default.RequestAsync(new ShareFileRequest
                {
                    Title = "Chia sẻ file Excel",
                    File = new ShareFile(item.FullPath)
                });
            }
            else if (action == "Xóa")
            {
                File.Delete(item.FullPath);
                LoadFiles();
            }

            ((CollectionView)sender).SelectedItem = null;
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
}
