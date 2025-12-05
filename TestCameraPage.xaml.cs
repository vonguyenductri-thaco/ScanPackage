using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Media;
using System;
using System.Threading.Tasks;
using System.Text;
using IOPath = System.IO.Path;

#if ANDROID
using Android.OS;
#endif

namespace ScanPackage;

public partial class TestCameraPage : ContentPage
{
    private StringBuilder _debugLog = new StringBuilder();

    public TestCameraPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(100);
            LoadDeviceInfo();
            await CheckPermissionsStatus();
        });
    }

    private void LoadDeviceInfo()
    {
        try
        {
#if ANDROID
            var manufacturer = Build.Manufacturer ?? "Unknown";
            var model = Build.Model ?? "Unknown";
            var androidVersion = Build.VERSION.Release ?? "Unknown";
            var apiLevel = (int)Build.VERSION.SdkInt;

            DeviceInfoLabel.Text = $"• Manufacturer: {manufacturer}\n" +
                                 $"• Model: {model}\n" +
                                 $"• Android: {androidVersion} (API {apiLevel})\n" +
                                 $"• Is Samsung A30: {(model.ToLower().Contains("a30") || model.ToLower().Contains("sm-a305") ? "✅ YES" : "❌ NO")}\n" +
                                 $"• MediaPicker Support: {(MediaPicker.Default.IsCaptureSupported ? "✅ YES" : "❌ NO")}";

            LogDebug($"Device: {manufacturer} {model}, Android {androidVersion} (API {apiLevel})");
            LogDebug($"MediaPicker.IsCaptureSupported: {MediaPicker.Default.IsCaptureSupported}");
#else
            DeviceInfoLabel.Text = "• Platform: Non-Android\n• Camera test chỉ hoạt động trên Android";
            LogDebug("Platform: Non-Android");
#endif
        }
        catch (Exception ex)
        {
            DeviceInfoLabel.Text = $"❌ Lỗi: {ex.Message}";
            LogDebug($"LoadDeviceInfo error: {ex.Message}");
        }
    }

    private async Task CheckPermissionsStatus()
    {
        try
        {
            LogDebug("Checking permissions...");

            // Camera permission
            var cameraStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();
            LogDebug($"Camera permission: {cameraStatus}");

            // Storage permission (Android 10 và thấp hơn)
            var storageStatus = PermissionStatus.Granted;
#if ANDROID
            if (Build.VERSION.SdkInt <= BuildVersionCodes.Q) // Android 10 và thấp hơn
            {
                storageStatus = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
                LogDebug($"Storage write permission: {storageStatus}");
            }
            else
            {
                LogDebug("Storage permission not needed (Android 11+)");
            }
#endif

            // Update UI
            var cameraIcon = cameraStatus == PermissionStatus.Granted ? "✅" : "❌";
            var storageIcon = storageStatus == PermissionStatus.Granted ? "✅" : "❌";

            PermissionStatusLabel.Text = $"• Camera: {cameraIcon} {cameraStatus}\n" +
                                       $"• Storage: {storageIcon} {storageStatus}\n" +
                                       $"• Ready to capture: {(cameraStatus == PermissionStatus.Granted && storageStatus == PermissionStatus.Granted ? "✅ YES" : "❌ NO")}";
        }
        catch (Exception ex)
        {
            PermissionStatusLabel.Text = $"❌ Lỗi kiểm tra permissions: {ex.Message}";
            LogDebug($"CheckPermissionsStatus error: {ex.Message}");
        }
    }

    private async void OnCheckPermissionsClicked(object sender, EventArgs e)
    {
        LogDebug("Manual permission check requested");
        await CheckPermissionsStatus();
    }

    private async void OnCaptureTestPhotoClicked(object sender, EventArgs e)
    {
        LogDebug("=== STARTING CAMERA TEST ===");
        PhotoStatusLabel.Text = "Đang chuẩn bị camera...";

        try
        {
            // Step 1: Check permissions
            LogDebug("Step 1: Checking permissions");
            var cameraPermission = await CheckAndRequestCameraPermission();
            if (!cameraPermission)
            {
                PhotoStatusLabel.Text = "❌ Không có quyền camera";
                LogDebug("Camera permission denied");
                await DisplayAlert("Lỗi", "Cần quyền truy cập camera để chụp ảnh.", "OK");
                return;
            }

            // Step 2: Check MediaPicker support
            LogDebug("Step 2: Checking MediaPicker support");
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                PhotoStatusLabel.Text = "❌ Camera không được hỗ trợ";
                LogDebug("MediaPicker.IsCaptureSupported = false");
                await DisplayAlert("Lỗi", "Camera không được hỗ trợ trên thiết bị này.", "OK");
                return;
            }

            // Step 3: Configure MediaPicker
            LogDebug("Step 3: Configuring MediaPicker");
            PhotoStatusLabel.Text = "📷 Đang mở camera...";
            
            var options = new MediaPickerOptions
            {
                Title = "Test Camera - Samsung A30"
            };

            // Step 4: Capture photo
            LogDebug("Step 4: Calling CapturePhotoAsync");
            var photo = await MediaPicker.Default.CapturePhotoAsync(options);
            
            if (photo == null)
            {
                PhotoStatusLabel.Text = "⚠️ Người dùng hủy hoặc lỗi camera";
                LogDebug("CapturePhotoAsync returned null");
                return;
            }

            LogDebug($"Photo captured: {photo.FileName}, ContentType: {photo.ContentType}");

            // Step 5: Save photo
            LogDebug("Step 5: Saving photo");
            PhotoStatusLabel.Text = "💾 Đang lưu ảnh...";

            var folder = FileSystem.AppDataDirectory;
            var fileName = $"test_camera_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
            var targetPath = IOPath.Combine(folder, fileName);

            LogDebug($"Saving to: {targetPath}");

            using (var sourceStream = await photo.OpenReadAsync())
            using (var targetStream = File.Create(targetPath))
            {
                await sourceStream.CopyToAsync(targetStream);
            }

            // Step 6: Verify file
            if (!File.Exists(targetPath))
            {
                throw new Exception("File không được tạo thành công");
            }

            var fileInfo = new FileInfo(targetPath);
            LogDebug($"File saved successfully: {fileInfo.Length} bytes");

            // Step 7: Display photo
            LogDebug("Step 7: Displaying photo");
            TestPhotoImage.Source = ImageSource.FromFile(targetPath);
            TestPhotoImage.IsVisible = true;
            TestPhotoPlaceholder.IsVisible = false;

            PhotoStatusLabel.Text = $"✅ Thành công! File: {fileInfo.Length} bytes";
            LogDebug("=== CAMERA TEST COMPLETED SUCCESSFULLY ===");
        }
        catch (PermissionException ex)
        {
            PhotoStatusLabel.Text = "❌ Lỗi quyền truy cập";
            LogDebug($"PermissionException: {ex.Message}");
            await DisplayAlert("Lỗi quyền truy cập", "Cần cấp quyền camera và storage để chụp ảnh.", "OK");
        }
        catch (FeatureNotSupportedException ex)
        {
            PhotoStatusLabel.Text = "❌ Tính năng không hỗ trợ";
            LogDebug($"FeatureNotSupportedException: {ex.Message}");
            await DisplayAlert("Lỗi", "Tính năng camera không được hỗ trợ trên thiết bị này.", "OK");
        }
        catch (Exception ex)
        {
            PhotoStatusLabel.Text = $"❌ Lỗi: {ex.Message}";
            LogDebug($"Exception: {ex.Message}");
            LogDebug($"StackTrace: {ex.StackTrace}");
            await DisplayAlert("Lỗi", $"Không thể chụp ảnh:\n{ex.Message}", "OK");
        }
    }

    private async Task<bool> CheckAndRequestCameraPermission()
    {
        try
        {
            LogDebug("Checking camera permission...");
            
            // Camera permission
            var cameraStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();
            LogDebug($"Current camera permission: {cameraStatus}");
            
            if (cameraStatus != PermissionStatus.Granted)
            {
                LogDebug("Requesting camera permission...");
                cameraStatus = await Permissions.RequestAsync<Permissions.Camera>();
                LogDebug($"Camera permission after request: {cameraStatus}");
            }

            // Storage permission for Android 10 and below
#if ANDROID
            if (Build.VERSION.SdkInt <= BuildVersionCodes.Q) // Android 10 và thấp hơn
            {
                LogDebug("Checking storage permission (Android 10-)...");
                var storageStatus = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
                LogDebug($"Current storage permission: {storageStatus}");
                
                if (storageStatus != PermissionStatus.Granted)
                {
                    LogDebug("Requesting storage permission...");
                    storageStatus = await Permissions.RequestAsync<Permissions.StorageWrite>();
                    LogDebug($"Storage permission after request: {storageStatus}");
                    
                    if (storageStatus != PermissionStatus.Granted)
                    {
                        LogDebug("Storage permission denied");
                        return false;
                    }
                }
            }
#endif

            var result = cameraStatus == PermissionStatus.Granted;
            LogDebug($"Final permission result: {result}");
            return result;
        }
        catch (Exception ex)
        {
            LogDebug($"CheckAndRequestCameraPermission error: {ex.Message}");
            return false;
        }
    }

    private void OnClearLogClicked(object sender, EventArgs e)
    {
        _debugLog.Clear();
        DebugLogLabel.Text = "Debug log cleared...";
    }

    private void LogDebug(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var logEntry = $"[{timestamp}] {message}";
        
        _debugLog.AppendLine(logEntry);
        
        MainThread.BeginInvokeOnMainThread(() =>
        {
            DebugLogLabel.Text = _debugLog.ToString();
        });
        
        System.Diagnostics.Debug.WriteLine($"[TestCamera] {logEntry}");
    }
}