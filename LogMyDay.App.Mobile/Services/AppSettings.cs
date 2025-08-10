namespace LogMyDay.App.Mobile.Services;

public class AppSettings
{
    public string WebUrl { get; set; } = string.Empty;
    public string DefaultPage { get; set; } = "/";
    
    public string FullUrl => WebUrl + DefaultPage;
}
