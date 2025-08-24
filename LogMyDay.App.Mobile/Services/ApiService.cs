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
            var tags = await _activityApi.GetTags();
            return tags.ToList();
        }
        catch (HttpRequestException httpEx)
        {
            var errorDetails = $"HTTP Error: {httpEx.Message}";
            if (httpEx.InnerException != null)
            {
                errorDetails += $"\nInner Exception: {httpEx.InnerException.Message}";
            }
            errorDetails += "\nEndpoint: /tags";
            
            LastError = errorDetails;
            
            System.Diagnostics.Debug.WriteLine($"HTTP Error fetching tags: {httpEx.Message}");
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
            errorDetails += "\nEndpoint: /tags";
            errorDetails += $"\nThis usually means the server is not responding or network issues";
            
            LastError = errorDetails;
            
            System.Diagnostics.Debug.WriteLine($"Timeout fetching tags: {tcEx.Message}");
            
            return new List<TagResponse>();
        }
        catch (Exception ex)
        {
            var errorDetails = $"General Error: {ex.Message}";
            errorDetails += $"\nException Type: {ex.GetType().Name}";
            errorDetails += "\nEndpoint: /tags";
            if (ex.InnerException != null)
            {
                errorDetails += $"\nInner Exception: {ex.InnerException.Message}";
            }
            errorDetails += $"\nStack Trace: {ex.StackTrace}";
            
            LastError = errorDetails;
            
            System.Diagnostics.Debug.WriteLine($"General Error fetching tags: {ex.Message}");
            
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

    public async Task<bool> CreateTagAsync(string tagName)
    {
        LastError = null;
        
        try
        {
            System.Diagnostics.Debug.WriteLine($"=== CREATING TAG ===");
            System.Diagnostics.Debug.WriteLine($"Tag Name: {tagName}");
            
            var tagRequest = new TagRequest
            {
                Tag = tagName,
                TypeId = 2, // Default to String type
                IsRequired = false,
                IsRepeatable = true,
                TimeGranularity = LogMyDay.Domain.Enums.TimeGranularity.Exact,
                IsRange = false
            };
            
            await _activityApi.CreateTag(tagRequest);
            
            System.Diagnostics.Debug.WriteLine($"✅ SUCCESS: Tag '{tagName}' created successfully");
            System.Diagnostics.Debug.WriteLine("=== END TAG CREATION ===");
            
            return true;
        }
        catch (HttpRequestException httpEx)
        {
            var errorDetails = $"HTTP Error creating tag: {httpEx.Message}";
            LastError = errorDetails;
            
            System.Diagnostics.Debug.WriteLine("=== TAG CREATION ERROR ===");
            System.Diagnostics.Debug.WriteLine($"❌ HTTP Error creating tag '{tagName}': {httpEx.Message}");
            System.Diagnostics.Debug.WriteLine("=== END ERROR ===");
            
            return false;
        }
        catch (Exception ex)
        {
            var errorDetails = $"Error creating tag: {ex.Message}";
            LastError = errorDetails;
            
            System.Diagnostics.Debug.WriteLine("=== TAG CREATION ERROR ===");
            System.Diagnostics.Debug.WriteLine($"❌ Error creating tag '{tagName}': {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Exception Type: {ex.GetType().Name}");
            System.Diagnostics.Debug.WriteLine("=== END ERROR ===");
            
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
            LastError = $"Error creating activity: {ex.Message}";
            
            return false;
        }
    }

    public async Task<List<ActivityResponse>> GetActivitiesAsync(
        DateTime? startDate = null, 
        DateTime? endDate = null, 
        int pageSize = 20, 
        int pageNumber = 1,
        string orderBy = "desc",
        int? tagId = null)
    {
        LastError = null;
        
        try
        {
            System.Diagnostics.Debug.WriteLine($"=== API CALL: GetActivities ===");
            System.Diagnostics.Debug.WriteLine($"Parameters: startDate={startDate}, endDate={endDate}, pageSize={pageSize}");
            
            // Log current credentials
            var username = Preferences.Get("Username", "");
            var password = Preferences.Get("Password", "");
            System.Diagnostics.Debug.WriteLine($"Current stored credentials: '{username}' / password length: {password.Length}");
            
            var result = await _activityApi.GetActivities(
                pageNumber: pageNumber,
                pageSize: pageSize,
                orderBy: orderBy,
                tagId: tagId,
                startDate: startDate,
                endDate: endDate);
            
            System.Diagnostics.Debug.WriteLine($"✅ SUCCESS: Fetched {result.Items.Count()} activities from API");
            System.Diagnostics.Debug.WriteLine("=== END API CALL ===");
            
            return result.Items.ToList();
        }
        catch (HttpRequestException httpEx)
        {
            var errorDetails = $"HTTP Error: {httpEx.Message}";
            LastError = errorDetails;
            
            System.Diagnostics.Debug.WriteLine($"=== HTTP ERROR ===");
            System.Diagnostics.Debug.WriteLine($"❌ HTTP Error fetching activities: {httpEx.Message}");
            System.Diagnostics.Debug.WriteLine($"Status Code: {httpEx.Data}");
            System.Diagnostics.Debug.WriteLine($"Inner Exception: {httpEx.InnerException?.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack Trace: {httpEx.StackTrace}");
            System.Diagnostics.Debug.WriteLine("=== END HTTP ERROR ===");
            
            return new List<ActivityResponse>();
        }
        catch (TaskCanceledException tcEx)
        {
            var errorDetails = $"Timeout Error: {tcEx.Message}";
            LastError = errorDetails;
            
            System.Diagnostics.Debug.WriteLine($"=== TIMEOUT ERROR ===");
            System.Diagnostics.Debug.WriteLine($"❌ Timeout fetching activities: {tcEx.Message}");
            System.Diagnostics.Debug.WriteLine($"Inner Exception: {tcEx.InnerException?.Message}");
            System.Diagnostics.Debug.WriteLine("=== END TIMEOUT ERROR ===");
            
            return new List<ActivityResponse>();
        }
        catch (Exception ex)
        {
            var errorDetails = $"General Error: {ex.Message}";
            LastError = errorDetails;
            
            System.Diagnostics.Debug.WriteLine($"=== GENERAL ERROR ===");
            System.Diagnostics.Debug.WriteLine($"❌ General Error fetching activities: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Exception Type: {ex.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"Inner Exception: {ex.InnerException?.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            System.Diagnostics.Debug.WriteLine("=== END GENERAL ERROR ===");
            
            return new List<ActivityResponse>();
        }
    }

    public async Task<bool> DeleteActivityAsync(int activityId)
    {
        try
        {
            await _activityApi.Delete(activityId);
            System.Diagnostics.Debug.WriteLine($"✅ SUCCESS: Deleted activity {activityId}");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Error deleting activity {activityId}: {ex.Message}");
            LastError = $"Error deleting activity: {ex.Message}";
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
