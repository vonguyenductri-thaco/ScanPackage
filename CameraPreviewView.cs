using Microsoft.Maui.Handlers;

namespace ScanPackage
{
    public class CameraPreviewView : View
    {
        public static readonly BindableProperty IsActiveProperty = BindableProperty.Create(
            nameof(IsActive),
            typeof(bool),
            typeof(CameraPreviewView),
            true);

        public bool IsActive
        {
            get => (bool)GetValue(IsActiveProperty);
            set => SetValue(IsActiveProperty, value);
        }
    }
}

#if ANDROID
namespace ScanPackage.Platforms.Android
{
    using System;
    using AndroidX.Camera.Core;
    using AndroidX.Camera.Lifecycle;
    using AndroidX.Camera.View;
    using AndroidX.Core.Content;
    using Java.Util.Concurrent;
    
    internal sealed class CameraRunnable : Java.Lang.Object, Java.Lang.IRunnable
    {
        private readonly Action _action;
        public CameraRunnable(Action action) { _action = action; }
        public void Run() => _action();
    }

    public class CameraPreviewViewHandler : ViewHandler<CameraPreviewView, PreviewView>
    {
        public static IPropertyMapper<CameraPreviewView, CameraPreviewViewHandler> PropertyMapper = 
            new PropertyMapper<CameraPreviewView, CameraPreviewViewHandler>(ViewHandler.ViewMapper);

        public CameraPreviewViewHandler() : base(PropertyMapper)
        {
        }

        private ProcessCameraProvider? _cameraProvider;
        private Preview? _preview;
        private IExecutorService? _cameraExecutor;

        protected override PreviewView CreatePlatformView()
        {
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            if (activity == null)
                throw new InvalidOperationException("Activity is null");

            var previewView = new PreviewView(activity);
            previewView.SetImplementationMode(PreviewView.ImplementationMode.Performance);
            previewView.SetScaleType(PreviewView.ScaleType.FillCenter);
            return previewView;
        }

        protected override void ConnectHandler(PreviewView platformView)
        {
            base.ConnectHandler(platformView);
            StartCamera();
        }

        protected override void DisconnectHandler(PreviewView platformView)
        {
            StopCamera();
            base.DisconnectHandler(platformView);
        }

        private void StartCamera()
        {
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            if (activity == null || PlatformView == null) return;

            // Check permission
            if (ContextCompat.CheckSelfPermission(activity, global::Android.Manifest.Permission.Camera) 
                != global::Android.Content.PM.Permission.Granted)
            {
                System.Diagnostics.Debug.WriteLine("Camera permission not granted");
                return;
            }

            _cameraExecutor = Executors.NewSingleThreadExecutor();
            var providerFuture = ProcessCameraProvider.GetInstance(activity);
            
            providerFuture.AddListener(new CameraRunnable(() =>
            {
                try
                {
                    _cameraProvider = providerFuture.Get() as ProcessCameraProvider;
                    if (_cameraProvider != null && PlatformView != null)
                    {
                        BindCamera(activity);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error starting camera: {ex}");
                }
            }), ContextCompat.GetMainExecutor(activity));
        }

        private void BindCamera(global::Android.App.Activity activity)
        {
            if (_cameraProvider == null || PlatformView == null) return;

            try
            {
                _cameraProvider.UnbindAll();

                _preview = new Preview.Builder().Build();
                var executor = ContextCompat.GetMainExecutor(activity);
                
                if (executor != null && PlatformView.SurfaceProvider != null)
                {
                    _preview.SetSurfaceProvider(executor, PlatformView.SurfaceProvider);
                }

                var cameraSelector = CameraSelector.DefaultBackCamera;
                
                if (activity is AndroidX.Lifecycle.ILifecycleOwner lifecycleOwner)
                {
                    _cameraProvider.BindToLifecycle(lifecycleOwner, cameraSelector, _preview);
                    System.Diagnostics.Debug.WriteLine("Camera preview started successfully");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error binding camera: {ex}");
            }
        }

        private void StopCamera()
        {
            try
            {
                _cameraProvider?.UnbindAll();
                _cameraExecutor?.Shutdown();
                _preview = null;
                _cameraProvider = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping camera: {ex}");
            }
        }
    }
}
#endif
