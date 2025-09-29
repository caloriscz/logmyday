using Microsoft.AspNetCore.Components;
using LogMyDay.App.Services;

namespace LogMyDay.App.Components.Base;

/// <summary>
/// Base component for pages that automatically manages page titles with site name appending
/// </summary>
public abstract class BasePageComponent : ComponentBase
{
    [Inject]
    protected IPageTitleService PageTitleService { get; set; } = default!;

    /// <summary>
    /// Gets or sets the page title (without site name). The service will automatically append " - LogMyDay"
    /// </summary>
    public string PageTitle
    {
        get => _pageTitle;
        set
        {
            _pageTitle = value;
            PageTitleService.SetTitle(value);
        }
    }
    
    private string _pageTitle = string.Empty;

    /// <summary>
    /// Sets the page title with automatic site name appending
    /// </summary>
    /// <param name="title">The page title without site name</param>
    protected void SetPageTitle(string title)
    {
        PageTitle = title;
    }

    /// <summary>
    /// Sets a raw page title without automatic site name appending (for special cases)
    /// </summary>
    /// <param name="title">The complete page title</param>
    protected void SetRawPageTitle(string title)
    {
        PageTitleService.SetRawTitle(title);
    }
}