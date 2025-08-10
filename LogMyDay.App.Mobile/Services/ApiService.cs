using LogMyDay.Shared.DTOs;
using LogMyDay.Shared.Interfaces;
using Refit;

namespace LogMyDay.App.Mobile.Services;

public class ApiService
{
    private readonly IActivityApi _activityApi;
    private readonly AppSettings _appSettings;

    public ApiService(IActivityApi activityApi, AppSettings appSettings)
    {
        _activityApi = activityApi;
        _appSettings = appSettings;
    }

    public string? LastError { get; private set; }

    public async Task<List<TagResponse>> GetTagsAsync()
    {
        LastError = null; // Reset error state
        
        try
        {
            // Log the API call details
            var baseUrl = "https://logmyday.tadata.cz/api"; // From configuration
            var fullUrl = $"{baseUrl}/tags";
            
            System.Diagnostics.Debug.WriteLine("=== API CALL DEBUG INFO ===");
            System.Diagnostics.Debug.WriteLine($"Base URL: {baseUrl}");
            System.Diagnostics.Debug.WriteLine($"Full URL: {fullUrl}");
            System.Diagnostics.Debug.WriteLine($"Credentials: admin/secret123");
            System.Diagnostics.Debug.WriteLine($"Attempting to fetch tags...");
            
            var tags = await _activityApi.GetTags();
            
            System.Diagnostics.Debug.WriteLine($"✅ SUCCESS: Fetched {tags.Count()} tags from API");
            System.Diagnostics.Debug.WriteLine("=== END API CALL ===");
            
            return tags.ToList();
        }
        catch (HttpRequestException httpEx)
        {
            var errorDetails = $"HTTP Error: {httpEx.Message}";
            if (httpEx.InnerException != null)
            {
                errorDetails += $"\nInner Exception: {httpEx.InnerException.Message}";
            }
            errorDetails += $"\nURL: https://logmyday.tadata.cz/api/tags";
            errorDetails += $"\nCredentials: admin/secret123";
            
            LastError = errorDetails;
            
            System.Diagnostics.Debug.WriteLine("=== HTTP ERROR ===");
            System.Diagnostics.Debug.WriteLine($"❌ HTTP Error fetching tags: {httpEx.Message}");
            System.Diagnostics.Debug.WriteLine($"URL: https://logmyday.tadata.cz/api/tags");
            System.Diagnostics.Debug.WriteLine($"Credentials: admin/secret123");
            System.Diagnostics.Debug.WriteLine($"HTTP Status: {httpEx.Data}");
            if (httpEx.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"Inner Exception: {httpEx.InnerException.Message}");
            }
            System.Diagnostics.Debug.WriteLine("=== END ERROR ===");
            
            return new List<TagResponse>();
        }
        catch (TaskCanceledException tcEx)
        {
            var errorDetails = $"Timeout Error: {tcEx.Message}";
            errorDetails += $"\nURL: https://logmyday.tadata.cz/api/tags";
            errorDetails += $"\nThis usually means the server is not responding or network issues";
            
            LastError = errorDetails;
            
            System.Diagnostics.Debug.WriteLine("=== TIMEOUT ERROR ===");
            System.Diagnostics.Debug.WriteLine($"❌ Timeout fetching tags: {tcEx.Message}");
            System.Diagnostics.Debug.WriteLine($"URL: https://logmyday.tadata.cz/api/tags");
            System.Diagnostics.Debug.WriteLine($"Credentials: admin/secret123");
            System.Diagnostics.Debug.WriteLine("This usually means the server is not responding or network issues");
            System.Diagnostics.Debug.WriteLine("=== END TIMEOUT ===");
            
            return new List<TagResponse>();
        }
        catch (Exception ex)
        {
            var errorDetails = $"General Error: {ex.Message}";
            errorDetails += $"\nException Type: {ex.GetType().Name}";
            errorDetails += $"\nURL: https://logmyday.tadata.cz/api/tags";
            if (ex.InnerException != null)
            {
                errorDetails += $"\nInner Exception: {ex.InnerException.Message}";
            }
            errorDetails += $"\nStack Trace: {ex.StackTrace}";
            
            LastError = errorDetails;
            
            System.Diagnostics.Debug.WriteLine("=== GENERAL ERROR ===");
            System.Diagnostics.Debug.WriteLine($"❌ General Error fetching tags: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"URL: https://logmyday.tadata.cz/api/tags");
            System.Diagnostics.Debug.WriteLine($"Credentials: admin/secret123");
            System.Diagnostics.Debug.WriteLine($"Exception Type: {ex.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            System.Diagnostics.Debug.WriteLine("=== END GENERAL ERROR ===");
            
            return new List<TagResponse>();
        }
    }

    public async Task<bool> TestApiConnectionAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("=== API CONNECTION TEST ===");
            System.Diagnostics.Debug.WriteLine($"Testing connection to: https://logmyday.tadata.cz/api/tags");
            System.Diagnostics.Debug.WriteLine($"Using credentials: admin/secret123");
            
            var tags = await _activityApi.GetTags();
            
            System.Diagnostics.Debug.WriteLine($"✅ CONNECTION SUCCESS: API is accessible and returned {tags.Count()} tags");
            System.Diagnostics.Debug.WriteLine("=== TEST COMPLETED ===");
            
            return true;
        }
        catch (HttpRequestException httpEx)
        {
            System.Diagnostics.Debug.WriteLine("=== CONNECTION TEST FAILED - HTTP ERROR ===");
            System.Diagnostics.Debug.WriteLine($"❌ HTTP Error: {httpEx.Message}");
            
            if (httpEx.Message.Contains("401") || httpEx.Message.Contains("Unauthorized"))
            {
                System.Diagnostics.Debug.WriteLine("🔑 AUTHENTICATION ISSUE: Check username/password");
            }
            else if (httpEx.Message.Contains("404") || httpEx.Message.Contains("Not Found"))
            {
                System.Diagnostics.Debug.WriteLine("🌐 ENDPOINT ISSUE: API endpoint may not exist");
            }
            else if (httpEx.Message.Contains("500"))
            {
                System.Diagnostics.Debug.WriteLine("🔥 SERVER ERROR: Internal server error");
            }
            
            System.Diagnostics.Debug.WriteLine("=== TEST FAILED ===");
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("=== CONNECTION TEST FAILED - GENERAL ERROR ===");
            System.Diagnostics.Debug.WriteLine($"❌ Error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Type: {ex.GetType().Name}");
            System.Diagnostics.Debug.WriteLine("=== TEST FAILED ===");
            return false;
        }
    }

    public async Task<bool> CreateActivityAsync(ActivityRequest request)
    {
        try
        {
            var response = await _activityApi.CreateCalendarItem(request);
            
            return response != null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error creating activity: {ex.Message}");
            
            return false;
        }
    }

    public async Task<DuplicateCheckResponse> CheckDuplicateAsync(int tagId, DateTime dateStarted)
    {
        try
        {
            var response = await _activityApi.CheckDuplicate(tagId, dateStarted);
            
            return response;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking duplicates: {ex.Message}");
            
            return new DuplicateCheckResponse { HasDuplicate = false };
        }
    }
}
