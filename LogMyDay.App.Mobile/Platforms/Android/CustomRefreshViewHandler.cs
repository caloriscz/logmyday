#if ANDROID
using Android.Content;
using Android.Views;
using AndroidX.SwipeRefreshLayout.Widget;
using Microsoft.Maui.Handlers;

namespace LogMyDay.App.Mobile.Platforms.Android;

internal class CustomRefreshViewHandler : RefreshViewHandler
{
    protected override SwipeRefreshLayout CreatePlatformView()
    {
        return new GuardedSwipeRefreshLayout(Context, this);
    }

    private sealed class GuardedSwipeRefreshLayout : SwipeRefreshLayout
    {
        private readonly CustomRefreshViewHandler _handler;

        public GuardedSwipeRefreshLayout(Context context, CustomRefreshViewHandler handler)
            : base(context)
        {
            _handler = handler;
        }

        private double ScrollTolerance => (_handler.VirtualView as Controls.CustomRefreshView)?.ScrollTolerance ?? 4d;

        public override bool CanChildScrollUp()
        {
            for (int i = 0; i < ChildCount; i++)
            {
                if (CanViewScrollUp(GetChildAt(i)))
                {
                    return true;
                }
            }

            // No child can scroll further up -> allow pull-to-refresh gesture
            return false;
        }

        private bool CanViewScrollUp(View? view)
        {
            if (view is null)
            {
                return false;
            }

            if (view.CanScrollVertically(-1))
            {
                return true;
            }

            if (view is Android.Webkit.WebView webView)
            {
                // ScrollY is in raw pixels; compare with tolerance (converted to pixels)
                var density = Context?.Resources?.DisplayMetrics?.Density ?? 1f;
                var tolerancePx = (int)(ScrollTolerance * density);
                return webView.ScrollY > tolerancePx;
            }

            if (view is ViewGroup group)
            {
                for (int i = 0; i < group.ChildCount; i++)
                {
                    if (CanViewScrollUp(group.GetChildAt(i)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
#endif
