using OfficeOpenXml;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Maui.Storage;

namespace ScanPackage;

public class ProductData
{
    public string Customer { get; set; } = "";
    public string Product { get; set; } = "";
    public string Model { get; set; } = "";
}

public class UserData
{
    public string Msnv { get; set; } = "";
    public string Name { get; set; } = "";
    public string Position { get; set; } = "";
}

public class ProductDataService
{
    private static ProductDataService? _instance;
    private List<ProductData> _data = new();
    private bool _isLoaded = false;
    private IDisposable? _realtimeListener;

    public static ProductDataService Instance => _instance ??= new ProductDataService();
    
    public bool IsDataLoaded => _isLoaded;
    
    // Event để notify khi data thay đổi
    public event Action<List<ProductData>>? DataChanged;

    private ProductDataService()
    {
    }

    /// <summary>
    /// Cleanup resources
    /// </summary>
    ~ProductDataService()
    {
        StopRealtimeSync();
    }

    /// <summary>
    /// Load dữ liệu sản phẩm từ Firebase Cloud Firestore và bắt đầu real-time sync
    /// </summary>
    public async Task LoadDataAsync()
    {
        if (_isLoaded) return;

        try
        {
            // Chỉ load từ Firebase, không fallback về local
            if (await TryLoadFromCloudAsync())
            {
                // Bắt đầu real-time sync
                StartRealtimeSync();
                return;
            }

            // Nếu không load được từ Firebase thì báo lỗi
            throw new Exception("Không thể kết nối Firebase. Kiểm tra:\n1. Kết nối internet\n2. Firebase config\n3. Dữ liệu đã upload lên Firebase");
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    /// <summary>
    /// Bắt đầu lắng nghe real-time updates từ Firebase
    /// </summary>
    public void StartRealtimeSync()
    {
        try
        {
            // Dừng listener cũ nếu có
            _realtimeListener?.Dispose();
            
            var cloudService = CloudProductService.Instance;
            _realtimeListener = cloudService.ListenForChanges(OnDataChanged);
        }
        catch (Exception ex)
        {
            // Ignore sync errors
        }
    }

    /// <summary>
    /// Dừng real-time sync
    /// </summary>
    public void StopRealtimeSync()
    {
        try
        {
            _realtimeListener?.Dispose();
            _realtimeListener = null;
        }
        catch (Exception ex)
        {
            // Ignore stop errors
        }
    }

    /// <summary>
    /// Callback khi có data thay đổi từ Firebase
    /// </summary>
    private void OnDataChanged(List<ProductData> newData)
    {
        try
        {
            _data = newData;
            _isLoaded = true;
            
            // Notify subscribers
            DataChanged?.Invoke(newData);
        }
        catch (Exception ex)
        {
            // Ignore data change errors
        }
    }

    /// <summary>
    /// Thử load dữ liệu từ Cloud Firestore
    /// </summary>
    private async Task<bool> TryLoadFromCloudAsync()
    {
        try
        {
            var cloudService = CloudProductService.Instance;
            
            // Kiểm tra kết nối cloud
            if (!await cloudService.IsCloudAvailableAsync())
            {
                return false;
            }

            // Load dữ liệu từ cloud
            var cloudData = await cloudService.GetAllProductsAsync();
            
            if (cloudData.Count > 0)
            {
                _data = cloudData;
                _isLoaded = true;
                
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    // ==================== LOCAL EXCEL METHODS REMOVED ====================
    // Local Excel loading removed - app now uses Firebase only

    /// <summary>
    /// Reload dữ liệu từ Cloud (dành cho real-time updates)
    /// </summary>
    public async Task ReloadFromCloudAsync()
    {
        try
        {
            _isLoaded = false;
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ReloadFromCloudAsync error: {ex.Message}");
            throw;
        }
    }

    // ==================== IMPORT EXCEL METHODS REMOVED ====================
    // Excel import functionality removed - data managed via Firebase Admin Dashboard only



    // ==================== PUBLIC API METHODS ====================

    public List<string> GetCustomers()
    {
        return _data.Select(x => x.Customer).Distinct().OrderBy(x => x).ToList();
    }

    public List<string> GetProducts(string customer)
    {
        return _data.Where(x => x.Customer == customer)
                   .Select(x => x.Product)
                   .Distinct()
                   .OrderBy(x => x)
                   .ToList();
    }

    public List<string> GetModels(string customer, string product)
    {
        return _data.Where(x => x.Customer == customer && x.Product == product)
                   .Select(x => x.Model)
                   .Distinct()
                   .OrderBy(x => x)
                   .ToList();
    }

    public bool IsLoaded => _isLoaded;

    public int Count => _data.Count;

    /// <summary>
    /// Force reload dữ liệu từ cloud hoặc local
    /// </summary>
    public async Task ReloadDataAsync()
    {
        _isLoaded = false;
        await LoadDataAsync();
    }
}
