# ScanPackage - Ứng dụng Quét Mã Vạch .NET MAUI

Ứng dụng mobile Android được phát triển bằng .NET MAUI để quét mã vạch và quản lý gói hàng.

## 📋 Yêu Cầu Hệ Thống

### Phần Cứng Tối Thiểu
- **RAM**: 8GB (khuyến nghị 16GB)
- **Ổ cứng**: 10GB trống
- **CPU**: Đa nhân 64-bit

### Hệ Điều Hành
- **Windows 10/11** (phiên bản này chỉ hỗ trợ Windows)
- Visual Studio 2022 (khuyến nghị)

### Công Cụ Cần Thiết

#### 1. .NET 8 SDK
```bash
# Kiểm tra phiên bản .NET hiện tại
dotnet --version

# Nếu chưa cài hoặc < 8.0, tải từ:
# https://dotnet.microsoft.com/en-us/download/dotnet/8.0
```

#### 2. .NET MAUI Workload
```bash
# Cài đặt MAUI workload cho .NET 8
dotnet workload install maui

# Hoặc nếu cần cài riêng cho Android:
dotnet workload install maui-android
```

#### 3. Visual Studio 2022 (Khuyến nghị)
- **Community Edition**: https://visualstudio.microsoft.com/downloads/
- Cài các workload:
  - ✅ **Mobile development with .NET**
  - ✅ **Desktop development with .NET**
  - ✅ **.NET desktop development tools**

#### 4. Android SDK & Emulator
Visual Studio sẽ tự động cài khi bạn cài workload Mobile development với .NET.

Hoặc cài thủ công:
- Android Studio: https://developer.android.com/studio
- Android SDK Platform 34 (Target API 34)
- Android Emulator với Android 6.0+ (API level 23+)

### 5. Kết Nối Thiết Bị Android Thật

Để chạy app trên điện thoại thật:

**Bước 1: Bật Developer Options**
1. Vào Settings → About Phone
2. Tìm "Build Number" hoặc "Phiên bản build"
3. Tap 7 lần vào Build Number
4. Mở khóa với mã PIN/mật khẩu

**Bước 2: Bật USB Debugging**
1. Vào Settings → System → Developer Options
2. Bật "USB debugging"
3. Bật "Stay awake" (tuỳ chọn)

**Bước 3: Kết nối qua USB**
1. Dùng cáp USB kết nối điện thoại với máy tính
2. Trên điện thoại chọn "Allow USB debugging"
3. Check "Always allow from this computer" để không nhắc lại

**Bước 4: Kiểm tra kết nối**
- Trong Visual Studio: dropdown thiết bị hiển thị tên model
- Hoặc command line: `adb devices` hiển thị device connected

**Lưu ý:**
- Một số điện thoại (Samsung, Xiaomi) cần bật thêm "Install via USB" hoặc "USB Installation"
- Với điện thoại Android mới: cần chấp nhận popup "Allow USB debugging?" trên màn hình

## 🚀 Cách Chạy Dự Án

### Phương Pháp 1: Visual Studio với Điện Thoại Thật

1. **Mở dự án**
   ```
   Mở file ScanPackage.sln bằng Visual Studio 2022
   ```

2. **Kết nối điện thoại**
   - Cắm USB điện thoại vào máy tính
   - Trên điện thoại: chọn "Allow USB debugging"
   - Kiểm tra dropdown thiết bị ở thanh toolbar hiển thị model điện thoại

3. **Khôi phục packages (nếu cần)**
   ```
   Build → Restore NuGet Packages
   ```

4. **Chọn thiết bị**
   - Dropdown thiết bị hiển thị tên model (ví dụ: "Pixel 7" hoặc "SM-G991B")
   - Chọn device của bạn

5. **Build và chạy**
   ```
   Nhấn F5 hoặc click nút Start (Play màu xanh)
   ```

6. **Lần đầu chạy:**
   - App sẽ cài đặt tự động trên điện thoại
   - Có thể mất 2-3 phút lần đầu
   - Sau đó app tự động mở trên điện thoại

### Phương Pháp 2: Command Line

1. **Khôi phục dependencies**
   ```bash
   dotnet restore
   ```

2. **Kiểm tra thiết bị Android có sẵn**
   ```bash
   adb devices
   ```

3. **Build dự án**
   ```bash
   dotnet build
   ```

4. **Chạy trên thiết bị/emulator**
   ```bash
   dotnet build -t:Run -f net8.0-android
   ```

## 🔧 Cấu Hình Dự Án

### Framework & Phiên Bản
- **.NET 8.0**
- **Android Target**: API 34
- **Min API**: 23 (Android 6.0)
- **MinSdk**: 23 (yêu cầu của ML Kit nếu dùng sau)

### NuGet Packages Chính
- `CommunityToolkit.Maui` 8.0.0
- `ZXing.Net.Maui` 0.4.0
- `EPPlus` 8.2.1

## 📱 Chức Năng Ứng Dụng

1. **BarcodeScanPage**: Quét mã vạch bằng camera
2. **DataEntryPage**: Nhập thủ công dữ liệu
3. **SetupPage**: Cấu hình ứng dụng

## ⚠️ Xử Lý Lỗi Thường Gặp

### 1. Lỗi thiếu .NET MAUI workload
```bash
# Cài lại MAUI workload
dotnet workload restore
dotnet workload install maui
```

### 2. Lỗi không tìm thấy thiết bị Android
```bash
# Kiểm tra ADB
adb devices

# Khởi động lại ADB server
adb kill-server
adb start-server
```

### 3. Lỗi build về AndroidX
```bash
# Xóa cache và build lại
dotnet clean
dotnet restore
dotnet build
```

### 4. Lỗi về NuGet packages
```bash
# Xóa cache NuGet
dotnet nuget locals all --clear

# Restore lại
dotnet restore
```

### 5. Lỗi "Out of Memory" hoặc "java.exe exited with code 1" ⚠️

Đây là lỗi phổ biến khi build Android với máy có RAM thấp.

**Triệu chứng:**
```
Failed to reserve memory for new overflow mark stack
Failed to allocate initial concurrent mark overflow mark stack
javac.exe exited with code 1
```

**Giải pháp TỐT NHẤT: Dùng Visual Studio**
- Visual Studio quản lý memory tốt hơn
- Build trong IDE ít gặp lỗi hơn
- VS tự động handle Java heap settings

**Nếu PHẢI dùng command line:**

**Bước 1: Tăng paging file Windows**
1. Settings → System → About → Advanced system settings
2. Performance → Settings → Advanced
3. Virtual memory → Change
4. Bỏ check "Automatically manage"
5. Custom size: Initial = 4096 MB, Maximum = 8192 MB
6. Restart máy

**Bước 2: Đóng ứng dụng khác**
- Build Android yêu cầu ~8GB RAM
- Đóng trình duyệt, IDE khác, ứng dụng nặng

**Bước 3: Restart terminal và thử lại**
```bash
dotnet clean
dotnet build
```

### 6. Lỗi "Unable to get provider androidx.startup.InitializationProvider"

**Nguyên nhân:** Thiếu dependency AndroidX Startup.

**Giải pháp:** Đã thêm trong csproj:
```xml
<PackageReference Include="Xamarin.AndroidX.Startup.StartupRuntime" Version="1.1.1.7" />
```

Nếu vẫn lỗi:
1. Clean và rebuild
2. Xóa bin/obj folders
3. Restore packages lại

### 7. Lỗi "The package was not properly signed (NO_CERTIFICATES)"

**Nguyên nhân:** Vấn đề với debug signing keystore.

**Giải pháp đã áp dụng:** Bật fast deployment trong csproj:
```xml
<EmbedAssembliesIntoApk>false</EmbedAssembliesIntoApk>
```

**Nếu vẫn lỗi, thử:**
1. Clean project: `dotnet clean`
2. Xóa folder: `.vs`, `bin`, `obj`
3. Rebuild lại từ đầu
4. Nếu đang dùng VS, close VS rồi mở lại

## 🗂️ Cấu Trúc Thư Mục

```
ScanPackage/
├── Platforms/Android/     # Platform-specific Android code
├── Resources/             # Images, fonts, splash
├── MainPage.xaml          # Trang chủ
├── BarcodeScanPage.xaml   # Trang quét mã vạch
├── DataEntryPage.xaml     # Trang nhập liệu
├── SetupPage.xaml         # Trang cấu hình
└── ScanPackage.csproj     # Cấu hình dự án
```

## 📝 Ghi Chú

- Dự án hiện chỉ hỗ trợ **Android**, chưa hỗ trợ iOS/Windows
- ML Kit đã được comment trong csproj, có thể uncomment khi cần
- AOT và Linker đã tắt để tránh lỗi runtime
- Fast deployment tắt để tránh warning trong build

## 🆘 Hỗ Trợ

Nếu gặp vấn đề, kiểm tra:
1. Console logs trong Visual Studio Output
2. Android logcat: `adb logcat`
3. .NET logs: Tools → Options → Debugging → Output Window

## 📄 License

Copyright © 2024

