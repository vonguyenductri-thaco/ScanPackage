using Plugin.CloudFirestore;
using System.Collections.ObjectModel;

namespace ScanPackage;

public class CloudProductService
{
    private static CloudProductService? _instance;
    public static CloudProductService Instance => _instance ??= new CloudProductService();

    private const string COLLECTION_NAME = "products";
    private readonly IFirestore _firestore;

    private CloudProductService()
    {
        try
        {
            _firestore = CrossCloudFirestore.Current.Instance;
            
            if (_firestore == null)
            {
                throw new Exception("CrossCloudFirestore.Current.Instance is null - Firebase not initialized");
            }
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    // ==================== CLOUD OPERATIONS ====================

    /// <summary>
    /// Lấy tất cả dữ liệu sản phẩm từ Firestore
    /// </summary>
    public async Task<List<ProductData>> GetAllProductsAsync()
    {
        try
        {
            var snapshot = await _firestore
                .Collection(COLLECTION_NAME)
                .OrderBy("customer")
                .OrderBy("product")
                .OrderBy("model")
                .GetAsync();

            var products = new List<ProductData>();

            foreach (var document in snapshot.Documents)
            {
                try
                {
                    var rawData = document.Data;
                    var data = new ProductData();
                    
                    if (rawData.TryGetValue("customer", out var customerValue))
                        data.Customer = customerValue?.ToString() ?? "";
                    if (rawData.TryGetValue("product", out var productValue))
                        data.Product = productValue?.ToString() ?? "";
                    if (rawData.TryGetValue("model", out var modelValue))
                        data.Model = modelValue?.ToString() ?? "";
                    
                    if (data != null && 
                        !string.IsNullOrEmpty(data.Customer) && 
                        !string.IsNullOrEmpty(data.Product) && 
                        !string.IsNullOrEmpty(data.Model))
                    {
                        products.Add(data);
                    }
                }
                catch (Exception ex)
                {
                    // Skip invalid documents
                }
            }

            return products;
        }
        catch (Exception ex)
        {
            throw new Exception($"Không thể tải dữ liệu từ cloud: {ex.Message}");
        }
    }

    // ==================== REMOVED METHODS ====================
    // Các method sau đã được loại bỏ vì Admin sẽ import trực tiếp trên Firebase Console:
    // - ImportExcelToCloudAsync() 
    // - ParseExcelFileAsync()
    // - UploadProductsAsync()
    
    // App chỉ cần đọc dữ liệu từ Firestore, không cần upload

    /// <summary>
    /// Kiểm tra kết nối internet và Firestore
    /// </summary>
    public async Task<bool> IsCloudAvailableAsync()
    {
        try
        {
            var testSnapshot = await _firestore
                .Collection(COLLECTION_NAME)
                .GetAsync();
            
            return true;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    /// <summary>
    /// Lắng nghe thay đổi real-time từ Firestore
    /// </summary>
    public IDisposable ListenForChanges(Action<List<ProductData>> onDataChanged)
    {
        return _firestore
            .Collection(COLLECTION_NAME)
            .OrderBy("customer")
            .OrderBy("product")
            .OrderBy("model")
            .AddSnapshotListener((snapshot, error) =>
            {
                if (error != null)
                {
                    return;
                }

                if (snapshot != null)
                {
                    var products = new List<ProductData>();
                    
                    foreach (var document in snapshot.Documents)
                    {
                        try
                        {
                            // Manual parsing như GetAllProductsAsync
                            var rawData = document.Data;
                            var data = new ProductData();
                            
                            if (rawData.TryGetValue("customer", out var customerValue))
                                data.Customer = customerValue?.ToString() ?? "";
                            if (rawData.TryGetValue("product", out var productValue))
                                data.Product = productValue?.ToString() ?? "";
                            if (rawData.TryGetValue("model", out var modelValue))
                                data.Model = modelValue?.ToString() ?? "";
                            
                            if (data != null && 
                                !string.IsNullOrEmpty(data.Customer) && 
                                !string.IsNullOrEmpty(data.Product) && 
                                !string.IsNullOrEmpty(data.Model))
                            {
                                products.Add(data);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Skip invalid documents
                        }
                    }

                    onDataChanged?.Invoke(products);
                }
            });
    }

}
