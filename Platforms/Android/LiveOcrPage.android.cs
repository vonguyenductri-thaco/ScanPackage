using System;
using System.Linq;
using System.Threading.Tasks;
using Android.Content;
using Android.Graphics;
using Android.Util;
using Android.Views;
using AndroidX.Camera.Core;
using AndroidX.Camera.Lifecycle;
using AndroidX.Camera.View;
using AndroidX.Concurrent.Futures;
using AndroidX.Core.Content;
using Java.Util.Concurrent;
using Microsoft.Maui.Platform;
using Android.Gms.Tasks;
using Android.Runtime;
using Java.Interop;
using Xamarin.Google.MLKit.Vision.Text;
using Xamarin.Google.MLKit.Vision.Common;
using Xamarin.Google.MLKit.Vision.Text.Latin;
using GmsTask = Android.Gms.Tasks.Task;

namespace ScanPackage;

public partial class LiveOcrPage
{
    private PreviewView? _previewView;
    private IExecutorService? _cameraExecutor;
    private ProcessCameraProvider? _cameraProvider;
    private ImageAnalysis? _imageAnalysis;
    private dynamic? _textRecognizer;
    private bool _completed;
    private bool _acceptNextResult;
    private System.Threading.CancellationTokenSource? _scanTimeoutCts;

    partial void OnAppearingPlatform()
    {
        // Camera preview được xử lý bởi CameraPreviewView handler
        // Chỉ cần khởi động ImageAnalysis để OCR
        try
        {
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            if (activity == null)
            {
                System.Diagnostics.Debug.WriteLine("LiveOCR: Activity is null");
                return;
            }

            // Check camera permission
            if (ContextCompat.CheckSelfPermission(activity, Android.Manifest.Permission.Camera) != Android.Content.PM.Permission.Granted)
            {
                Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("Lỗi", "Ứng dụng cần quyền truy cập camera để quét văn bản.", "Đóng");
                    await Navigation.PopModalAsync();
                });
                return;
            }

            // Khởi tạo ML Kit Text Recognizer (unbundled)
            try
            {
                var options = new TextRecognizerOptions.Builder().Build();
                _textRecognizer = Xamarin.Google.MLKit.Vision.Text.TextRecognition.GetClient(options);
                System.Diagnostics.Debug.WriteLine("ML Kit Text Recognizer initialized");
                                }
                                catch (Exception ex)
                                {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize TextRecognizer: {ex}");
            }
            
            if (_textRecognizer == null)
            {
                System.Diagnostics.Debug.WriteLine("WARNING: TextRecognizer is null - OCR will not work");
            }
            
            // Khởi động ImageAnalysis để OCR (không cần preview vì CameraPreviewView đã xử lý)
            System.Diagnostics.Debug.WriteLine("Starting ImageAnalysis for OCR...");
            StartImageAnalysis(activity);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LiveOCR OnAppearing Error: {ex}");
            Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(async () =>
            {
                await DisplayAlert("Lỗi", $"Lỗi khởi tạo camera: {ex.Message}", "Đóng");
                await Navigation.PopModalAsync();
            });
        }
    }

    private void StartImageAnalysis(Android.App.Activity activity)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("StartImageAnalysis: Initializing ImageAnalysis for OCR...");

            _cameraExecutor = Executors.NewSingleThreadExecutor();
            var providerFuture = ProcessCameraProvider.GetInstance(activity);
            providerFuture.AddListener(new RunnableAction(() =>
            {
                try
                {
                    _cameraProvider = providerFuture.Get() as ProcessCameraProvider;
                    if (_cameraProvider != null)
                    {
                        BindImageAnalysis(activity);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("StartImageAnalysis: Failed to get ProcessCameraProvider");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"StartImageAnalysis Error: {ex}");
                }
            }), ContextCompat.GetMainExecutor(activity));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"StartImageAnalysis Exception: {ex}");
        }
    }

    private void BindImageAnalysis(Android.App.Activity activity)
    {
        if (_cameraProvider == null)
        {
            System.Diagnostics.Debug.WriteLine("BindImageAnalysis: _cameraProvider is null");
            return;
        }

        try
        {
            System.Diagnostics.Debug.WriteLine("BindImageAnalysis: Starting ImageAnalysis binding...");
            
            // Không unbind vì CameraPreviewView đã bind preview
            // Chỉ thêm ImageAnalysis vào cùng lifecycle

            // Chỉ dùng ImageAnalysis để capture frames cho OCR
            _imageAnalysis = new ImageAnalysis.Builder()
                .SetBackpressureStrategy(ImageAnalysis.StrategyKeepOnlyLatest)
                .Build();

            if (_textRecognizer != null && _cameraExecutor != null)
            {
                _imageAnalysis.SetAnalyzer(_cameraExecutor, new FrameAnalyzer(this));
                System.Diagnostics.Debug.WriteLine("FrameAnalyzer set for ImageAnalysis");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Warning: TextRecognizer or cameraExecutor is null");
            }

            var cameraSelector = CameraSelector.DefaultBackCamera;
            
            // Bind ImageAnalysis vào cùng lifecycle với preview (đã được bind bởi CameraPreviewView)
            if (activity is AndroidX.Lifecycle.ILifecycleOwner lifecycleOwner)
            {
                _cameraProvider.BindToLifecycle(
                    lifecycleOwner, 
                    cameraSelector, 
                    _imageAnalysis);
            }
            
            System.Diagnostics.Debug.WriteLine("ImageAnalysis bound to lifecycle successfully");
            
            // Cập nhật UI để hiển thị trạng thái đang quét
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                await System.Threading.Tasks.Task.Delay(300);
                Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        if (ResultLabel != null)
                        {
                            ResultLabel.Text = "Đang quét...";
                            ResultLabel.TextColor = Microsoft.Maui.Graphics.Colors.Gray;
                        }
                    }
                    catch (Exception uiEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error updating UI: {uiEx}");
                    }
                });
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BindImageAnalysis Error: {ex}");
            System.Diagnostics.Debug.WriteLine($"Exception details: {ex.GetType().Name}, Message: {ex.Message}");
        }
    }

    partial void OnDisappearingPlatform()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("OnDisappearingPlatform: Cleaning up camera...");
            _completed = true;
            _scanTimeoutCts?.Cancel();
            _scanTimeoutCts?.Dispose();
            _scanTimeoutCts = null;
            _cameraProvider?.UnbindAll();
            _cameraExecutor?.Shutdown();
            _textRecognizer?.Dispose();
            _previewView = null;
            _cameraProvider = null;
            _imageAnalysis = null;
            _textRecognizer = null;
            System.Diagnostics.Debug.WriteLine("Camera cleanup completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LiveOCR Cleanup Error: {ex}");
        }
    }

    partial void CaptureOncePlatform()
    {
        try
        {
            // Hủy timeout cũ nếu có
            _scanTimeoutCts?.Cancel();
            _scanTimeoutCts?.Dispose();
            _scanTimeoutCts = new System.Threading.CancellationTokenSource();

            _acceptNextResult = true;
            Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
            {
                if (ResultLabel != null)
                {
                    ResultLabel.Text = "Đang quét...";
                    ResultLabel.TextColor = Microsoft.Maui.Graphics.Colors.Gray;
                    CheckmarkIcon.IsVisible = false;
                    ConfirmButton.IsEnabled = false;
                }
            });

            // Set timeout 10 giây - nếu không quét được thì thông báo
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    await System.Threading.Tasks.Task.Delay(10000, _scanTimeoutCts.Token);
                    // Nếu sau 10 giây vẫn chưa có kết quả
                    if (_acceptNextResult && !_completed)
                    {
                        _acceptNextResult = false;
                        Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
                        {
                            if (ResultLabel != null)
                            {
                                ResultLabel.Text = "Không tìm thấy. Thử lại?";
                                ResultLabel.TextColor = Microsoft.Maui.Graphics.Colors.Orange;
                            }
                        });
                    }
                }
                catch (System.Threading.Tasks.TaskCanceledException)
                {
                    // Timeout bị hủy vì đã có kết quả - OK
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CaptureOncePlatform error: {ex}");
        }
    }

    // FrameAnalyzer để phân tích frame từ camera và nhận dạng text
    private sealed class FrameAnalyzer : Java.Lang.Object, ImageAnalysis.IAnalyzer
    {
        private readonly LiveOcrPage _page;
        private readonly Android.Util.Size _defaultResolution;
        
        public FrameAnalyzer(LiveOcrPage page) 
        { 
            _page = page;
            _defaultResolution = new Android.Util.Size(1280, 720);
        }

        // Implement property từ interface IAnalyzer
        // Property này được yêu cầu bởi CameraX 1.4+ để set default resolution
        Android.Util.Size ImageAnalysis.IAnalyzer.DefaultTargetResolution => _defaultResolution;

        public void Analyze(IImageProxy image)
        {
            if (_page._completed || _page._textRecognizer == null)
            {
                image.Close();
                return;
            }

            // Chỉ xử lý khi đang chờ kết quả (sau khi nhấn chụp)
            if (!_page._acceptNextResult)
            {
                image.Close();
                return;
            }

            try
            {
                var mediaImage = image.Image;
                if (mediaImage == null)
                {
                    image.Close();
                    return;
                }

                var inputImage = Xamarin.Google.MLKit.Vision.Common.InputImage.FromMediaImage(mediaImage, image.ImageInfo.RotationDegrees);

                // Process image với ML Kit Text Recognition
                var task = _page._textRecognizer.Process(inputImage);
                task.AddOnSuccessListener(new TextRecognitionSuccessListener(_page, image));
                task.AddOnFailureListener(new TextRecognitionFailureListener(image));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FrameAnalyzer Error: {ex}");
                image.Close();
            }
        }
    }

    private sealed class TextRecognitionSuccessListener : Java.Lang.Object, Android.Gms.Tasks.IOnSuccessListener
    {
        private readonly LiveOcrPage _page;
        private readonly IImageProxy _image;

        public TextRecognitionSuccessListener(LiveOcrPage page, IImageProxy image)
        {
            _page = page;
            _image = image;
        }

        public void OnSuccess(Java.Lang.Object result)
        {
            try
            {
                if (_page._completed)
                {
                    _image.Close();
                    return;
                }

                var textResult = Android.Runtime.Extensions.JavaCast<Xamarin.Google.MLKit.Vision.Text.Text>(result);
                if (textResult == null)
                {
                    _image.Close();
                    return;
                }

                // Tìm text phù hợp với pattern; ưu tiên trong vùng quét, sau đó fallback quét toàn bộ
                string? candidate = null;
                var scanArea = _page.ScanArea;
                var imageHeight = _image.Height;

                bool hasValidScanArea = scanArea.Width > 0 && scanArea.Height > 0;

                // Pass 1: ưu tiên trong scan area (nếu hợp lệ)
                if (hasValidScanArea && imageHeight > 0)
                {
                    foreach (var block in textResult.TextBlocks)
                {
                    foreach (var line in block.Lines)
                        {
                            var text = (line.Text ?? string.Empty).Trim().ToUpperInvariant();
                            if (string.IsNullOrWhiteSpace(text)) continue;

                            var bb = line.BoundingBox;
                            if (bb != null)
                            {
                                var lineCenterY = bb.CenterY() / (float)imageHeight;
                                var scanTop = scanArea.Y;
                                var scanBottom = scanArea.Y + scanArea.Height;
                                if (lineCenterY < scanTop - 0.08f || lineCenterY > scanBottom + 0.08f)
                                    continue;
                            }

                            if (_page._mode == OcrMode.Container)
                            {
                                var m = System.Text.RegularExpressions.Regex.Match(text, @"[A-Z]{3,4}[\s-]?\d{6,8}");
                                if (m.Success)
                                {
                                    candidate = m.Value.Replace(" ", string.Empty).Replace("-", string.Empty);
                                    break;
                                }
                            }
                            else
                            {
                                var m = System.Text.RegularExpressions.Regex.Match(text, @"[A-Z0-9]{5,14}");
                                if (m.Success)
                                {
                                    candidate = m.Value;
                                    break;
                                }
                            }
                        }
                        if (candidate != null) break;
                    }
                }

                // Pass 2: nếu chưa có, thử toàn bộ khung hình (không lọc theo vùng)
                if (candidate == null)
                {
                    foreach (var block in textResult.TextBlocks)
                    {
                        foreach (var line in block.Lines)
                        {
                        var text = (line.Text ?? string.Empty).Trim().ToUpperInvariant();
                        if (string.IsNullOrWhiteSpace(text)) continue;

                        if (_page._mode == OcrMode.Container)
                        {
                                var m = System.Text.RegularExpressions.Regex.Match(text, @"[A-Z]{3,4}[\s-]?\d{6,8}");
                                if (m.Success)
                                {
                                    candidate = m.Value.Replace(" ", string.Empty).Replace("-", string.Empty);
                                    break;
                                }
                        }
                        else
                        {
                                var m = System.Text.RegularExpressions.Regex.Match(text, @"[A-Z0-9]{5,14}");
                                if (m.Success)
                                {
                                    candidate = m.Value;
                                    break;
                                }
                            }
                        }
                        if (candidate != null) break;
                    }
                }

                if (!string.IsNullOrWhiteSpace(candidate) && _page._acceptNextResult)
                {
                    _page._acceptNextResult = false;
                    // Hủy timeout vì đã có kết quả
                    _page._scanTimeoutCts?.Cancel();
                    Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _page.UpdateResult(candidate!);
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TextRecognitionSuccessListener Error: {ex}");
            }
            finally
            {
                _image.Close();
            }
        }
    }

    private sealed class TextRecognitionFailureListener : Java.Lang.Object, Android.Gms.Tasks.IOnFailureListener
    {
        private readonly IImageProxy _image;

        public TextRecognitionFailureListener(IImageProxy image)
        {
            _image = image;
        }

        public void OnFailure(Java.Lang.Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"Text Recognition failed: {e}");
            _image.Close();
        }
    }
}

internal sealed class RunnableAction : Java.Lang.Object, Java.Lang.IRunnable
{
    private readonly Action _action;
    public RunnableAction(Action action) { _action = action; }
    public void Run() => _action();
}



