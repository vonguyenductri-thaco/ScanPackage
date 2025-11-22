using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Maui.Storage;
using OfficeOpenXml;

namespace ScanPackage;

public class ProductData
{
    public string Customer { get; set; } = "";
    public string Product { get; set; } = "";
    public string Model { get; set; } = "";
}

public class ProductDataService
{
    private static ProductDataService? _instance;
    private List<ProductData> _data = new();
    private bool _isLoaded = false;

    public static ProductDataService Instance => _instance ??= new ProductDataService();

    private ProductDataService()
    {
    }

    /// <summary>
    /// Load dữ liệu từ file Excel trong Resources/Raw
    /// File phải có tên: product_data.xlsx
    /// </summary>
    public async Task LoadDataAsync()
    {
        if (_isLoaded) return;

        try
        {
            System.Diagnostics.Debug.WriteLine("=== LOADING PRODUCT DATA ===");

            // Load file từ Resources/Raw
            System.Diagnostics.Debug.WriteLine("Opening product_data.xlsx from Resources/Raw...");
            using var stream = await FileSystem.OpenAppPackageFileAsync("product_data.xlsx");

            System.Diagnostics.Debug.WriteLine("Creating memory stream...");
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);

            System.Diagnostics.Debug.WriteLine($"Memory stream size: {memoryStream.Length} bytes");

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            System.Diagnostics.Debug.WriteLine("Creating ExcelPackage...");
            using var package = new ExcelPackage(memoryStream);

            if (package.Workbook.Worksheets.Count == 0)
            {
                throw new Exception("Excel file has no worksheets");
            }

            var worksheet = package.Workbook.Worksheets[0];
            System.Diagnostics.Debug.WriteLine($"Worksheet name: {worksheet.Name}");
            System.Diagnostics.Debug.WriteLine($"Dimensions: {worksheet.Dimension?.Address ?? "null"}");

            if (worksheet.Dimension == null)
            {
                throw new Exception("Worksheet has no data");
            }

            _data.Clear();

            string? lastCustomer = null;
            string? lastProduct = null;

            int rowCount = 0;
            int validCount = 0;

            // Đọc từ row 2 (row 1 là header)
            for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
            {
                rowCount++;

                var customerCell = worksheet.Cells[row, 2].Value;
                var productCell = worksheet.Cells[row, 3].Value;
                var modelCell = worksheet.Cells[row, 4].Value;

                var customer = customerCell?.ToString()?.Trim();
                var product = productCell?.ToString()?.Trim();
                var model = modelCell?.ToString()?.Trim();

                // Nếu customer rỗng, dùng giá trị trước đó
                if (string.IsNullOrEmpty(customer))
                    customer = lastCustomer;
                else
                    lastCustomer = customer;

                // Nếu product rỗng, dùng giá trị trước đó
                if (string.IsNullOrEmpty(product))
                    product = lastProduct;
                else
                    lastProduct = product;

                // Chỉ thêm nếu có đủ thông tin
                if (!string.IsNullOrEmpty(customer) &&
                    !string.IsNullOrEmpty(product) &&
                    !string.IsNullOrEmpty(model))
                {
                    _data.Add(new ProductData
                    {
                        Customer = customer,
                        Product = product,
                        Model = model
                    });
                    validCount++;

                    System.Diagnostics.Debug.WriteLine($"Row {row}: {customer} -> {product} -> {model}");
                }
            }

            _isLoaded = true;
            System.Diagnostics.Debug.WriteLine($"=== LOAD COMPLETE ===");
            System.Diagnostics.Debug.WriteLine($"Total rows processed: {rowCount}");
            System.Diagnostics.Debug.WriteLine($"Valid records loaded: {validCount}");

            // Log summary
            var customers = _data.Select(x => x.Customer).Distinct().ToList();
            System.Diagnostics.Debug.WriteLine($"Unique customers: {customers.Count}");
            foreach (var c in customers)
            {
                var products = _data.Where(x => x.Customer == c).Select(x => x.Product).Distinct().Count();
                var models = _data.Where(x => x.Customer == c).Select(x => x.Model).Distinct().Count();
                System.Diagnostics.Debug.WriteLine($"  - {c}: {products} products, {models} models");
            }
        }
        catch (FileNotFoundException ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadDataAsync FileNotFoundException: {ex.Message}");
            System.Diagnostics.Debug.WriteLine("File product_data.xlsx không tìm thấy trong Resources/Raw/");
            System.Diagnostics.Debug.WriteLine("Hãy đảm bảo:");
            System.Diagnostics.Debug.WriteLine("1. File có tên chính xác: product_data.xlsx");
            System.Diagnostics.Debug.WriteLine("2. File đặt trong thư mục Resources/Raw/");
            System.Diagnostics.Debug.WriteLine("3. Build Action = MauiAsset trong .csproj");
            throw new Exception("Không tìm thấy file product_data.xlsx. Kiểm tra file có trong Resources/Raw/ và đã config trong .csproj chưa.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadDataAsync error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            throw new Exception($"Lỗi load dữ liệu: {ex.Message}");
        }
    }

    /// <summary>
    /// Lấy danh sách tất cả khách hàng (unique)
    /// </summary>
    public List<string> GetCustomers()
    {
        return _data
            .Select(x => x.Customer)
            .Distinct()
            .OrderBy(x => x)
            .ToList();
    }

    /// <summary>
    /// Lấy danh sách sản phẩm theo khách hàng
    /// </summary>
    public List<string> GetProducts(string customer)
    {
        if (string.IsNullOrEmpty(customer))
            return new List<string>();

        return _data
            .Where(x => x.Customer == customer)
            .Select(x => x.Product)
            .Distinct()
            .OrderBy(x => x)
            .ToList();
    }

    /// <summary>
    /// Lấy danh sách model theo khách hàng và sản phẩm
    /// </summary>
    public List<string> GetModels(string customer, string product)
    {
        if (string.IsNullOrEmpty(customer) || string.IsNullOrEmpty(product))
            return new List<string>();

        return _data
            .Where(x => x.Customer == customer && x.Product == product)
            .Select(x => x.Model)
            .Distinct()
            .OrderBy(x => x)
            .ToList();
    }

    /// <summary>
    /// Kiểm tra xem customer có tồn tại không
    /// </summary>
    public bool IsValidCustomer(string customer)
    {
        return !string.IsNullOrEmpty(customer) &&
               _data.Any(x => x.Customer == customer);
    }

    /// <summary>
    /// Kiểm tra xem product có tồn tại cho customer này không
    /// </summary>
    public bool IsValidProduct(string customer, string product)
    {
        return !string.IsNullOrEmpty(customer) &&
               !string.IsNullOrEmpty(product) &&
               _data.Any(x => x.Customer == customer && x.Product == product);
    }

    /// <summary>
    /// Kiểm tra xem model có tồn tại cho customer và product này không
    /// </summary>
    public bool IsValidModel(string customer, string product, string model)
    {
        return !string.IsNullOrEmpty(customer) &&
               !string.IsNullOrEmpty(product) &&
               !string.IsNullOrEmpty(model) &&
               _data.Any(x => x.Customer == customer &&
                             x.Product == product &&
                             x.Model == model);
    }

    /// <summary>
    /// Clear cache và force reload
    /// </summary>
    public void ClearCache()
    {
        _data.Clear();
        _isLoaded = false;
    }
}