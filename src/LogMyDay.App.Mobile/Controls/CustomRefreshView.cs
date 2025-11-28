namespace LogMyDay.App.Mobile.Controls;

/// <summary>
/// RefreshView that exposes a configurable scroll tolerance for platform handlers.
/// </summary>
public class CustomRefreshView : RefreshView
{
    /// <summary>
    /// Gets or sets the vertical scroll tolerance (in device-independent pixels) that
    /// the platform handler can use to decide whether the content is effectively at the top.
    /// </summary>
    public double ScrollTolerance { get; set; } = 4d;
}
