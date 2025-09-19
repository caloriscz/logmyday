namespace LogMyDay.App.Mobile.Models;

/// <summary>
/// Simple error model for API error responses in mobile client
/// </summary>
public class ApiError
{
    public string? Title { get; set; }
    public string? Detail { get; set; }
}