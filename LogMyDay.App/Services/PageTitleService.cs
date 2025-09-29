using Microsoft.AspNetCore.Components;

namespace LogMyDay.App.Services;

/// <summary>
/// Service for managing page titles with automatic site name appending
/// </summary>
public interface IPageTitleService
{
    /// <summary>
    /// Set a page title that will automatically have " - LogMyDay" appended
    /// </summary>
    /// <param name="title">The base page title</param>
    void SetTitle(string title);

    /// <summary>
    /// Set a raw page title without automatic appending (for special cases)
    /// </summary>
    /// <param name="title">The complete page title</param>
    void SetRawTitle(string title);

    /// <summary>
    /// Get the current formatted title
    /// </summary>
    string CurrentTitle { get; }

    /// <summary>
    /// Event that fires when the title changes
    /// </summary>
    event EventHandler<string>? TitleChanged;
}

/// <summary>
/// Implementation of the page title service
/// </summary>
public class PageTitleService : IPageTitleService
{
    private const string SITE_NAME = "LogMyDay";
    private string _currentTitle = SITE_NAME;

    public string CurrentTitle => _currentTitle;
    
    public event EventHandler<string>? TitleChanged;

    public void SetTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            _currentTitle = SITE_NAME;
        }
        else
        {
            _currentTitle = $"{title} - {SITE_NAME}";
        }
        
        TitleChanged?.Invoke(this, _currentTitle);
    }

    public void SetRawTitle(string title)
    {
        _currentTitle = string.IsNullOrWhiteSpace(title) ? SITE_NAME : title;
        TitleChanged?.Invoke(this, _currentTitle);
    }
}