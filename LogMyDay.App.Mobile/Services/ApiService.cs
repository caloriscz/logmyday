using LogMyDay.Shared.DTOs;
using LogMyDay.Shared.Interfaces;
using Refit;
using System.Text;

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

    public async Task<List<TagResponse>> GetTagsAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("ApiService: Attempting to fetch tags...");
            
            var tags = await _activityApi.GetTags();
            
            System.Diagnostics.Debug.WriteLine($"ApiService: Successfully fetched {tags.Count()} tags");
            
            return tags.ToList();
        }
        catch (HttpRequestException httpEx)
        {
            System.Diagnostics.Debug.WriteLine($"ApiService HTTP Error fetching tags: {httpEx.Message}");
            System.Diagnostics.Debug.WriteLine($"HTTP Status: {httpEx.Data}");
            
            return new List<TagResponse>();
        }
        catch (TaskCanceledException tcEx)
        {
            System.Diagnostics.Debug.WriteLine($"ApiService Timeout fetching tags: {tcEx.Message}");
            
            return new List<TagResponse>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ApiService General Error fetching tags: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Exception Type: {ex.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
            
            return new List<TagResponse>();
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
