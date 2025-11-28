#if ANDROID
using Android.Content;
using AndroidX.SwipeRefreshLayout.Widget;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace LogMyDay.App.Mobile.Platforms.Android;

internal class CustomRefreshViewHandler : RefreshViewHandler
{
    protected override MauiSwipeRefreshLayout CreatePlatformView()
    {
    return new SimpleSwipeRefreshLayout(Context, this);
    }

    private sealed class SimpleSwipeRefreshLayout : MauiSwipeRefreshLayout
    {
        private readonly CustomRefreshViewHandler _handler;

   public SimpleSwipeRefreshLayout(Context context, CustomRefreshViewHandler handler)
     : base(context)
        {
       _handler = handler;
          
        // Disable native pull-to-refresh - we'll use JavaScript instead
            this.Enabled = false;

            System.Diagnostics.Debug.WriteLine("[SimpleSwipeRefreshLayout] Native refresh DISABLED - using JavaScript implementation");
        }

   private double ScrollTolerance => (_handler.VirtualView as Controls.CustomRefreshView)?.ScrollTolerance ?? 4d;

        public override bool CanChildScrollUp()
        {
// Always return false - let JavaScript handle everything
         return false;
      }
    }
}
#endif
