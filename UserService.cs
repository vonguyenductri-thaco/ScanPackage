using Plugin.CloudFirestore;
using System.Collections.Generic;
using System.Linq;

namespace ScanPackage;

public class UserService
{
    private static UserService? _instance;
    public static UserService Instance => _instance ??= new UserService();

    private const string COLLECTION_NAME = "users";
    private readonly IFirestore _firestore;
    private List<UserData> _users = new();
    private bool _isLoaded = false;

    private UserService()
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

    /// <summary>
    /// Load users from Firebase
    /// </summary>
    public async Task LoadUsersAsync()
    {
        if (_isLoaded) return;

        try
        {
            var snapshot = await _firestore
                .Collection(COLLECTION_NAME)
                .OrderBy("name")
                .GetAsync();

            _users = new List<UserData>();

            foreach (var document in snapshot.Documents)
            {
                try
                {
                    // Manual parsing để handle lowercase field names
                    var rawData = document.Data;
                    var user = new UserData();

                    if (rawData.TryGetValue("msnv", out var msnvValue))
                        user.Msnv = msnvValue?.ToString() ?? "";
                    if (rawData.TryGetValue("name", out var nameValue))
                        user.Name = nameValue?.ToString() ?? "";
                    if (rawData.TryGetValue("position", out var positionValue))
                        user.Position = positionValue?.ToString() ?? "";

                    if (!string.IsNullOrEmpty(user.Msnv) &&
                        !string.IsNullOrEmpty(user.Name) &&
                        !string.IsNullOrEmpty(user.Position))
                    {
                        _users.Add(user);
                    }
                }
                catch (Exception ex)
                {
                    // Skip invalid documents
                }
            }

            _isLoaded = true;
        }
        catch (Exception ex)
        {
            throw new Exception($"Không thể tải danh sách người lập từ Firebase: {ex.Message}");
        }
    }

    /// <summary>
    /// Get all users
    /// </summary>
    public List<UserData> GetAllUsers()
    {
        return _users.ToList();
    }

    /// <summary>
    /// Get user display names (Name - MSNV)
    /// </summary>
    public List<string> GetUserDisplayNames()
    {
        return _users.Select(u => $"{u.Name} - {u.Msnv}").OrderBy(x => x).ToList();
    }

    /// <summary>
    /// Get user by display name
    /// </summary>
    public UserData? GetUserByDisplayName(string displayName)
    {
        return _users.FirstOrDefault(u => $"{u.Name} - {u.Msnv}" == displayName);
    }

    /// <summary>
    /// Check if users are loaded
    /// </summary>
    public bool IsLoaded => _isLoaded;
}