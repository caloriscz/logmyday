// RefreshView JavaScript handlers - Strict gating for MAUI Blazor pull-to-refresh
(function () {
    'use strict';

    // Ensure window object exists
    if (typeof window === 'undefined') {
        console.warn('RefreshView: Window object not available');
        return;
    }

    // Initialize global storage
    window.refreshViewInstances = window.refreshViewInstances || new Map();

    // --- Tunables (adjust to taste) ---
    const SCROLL_TOP_TOLERANCE = 2;                 // px considered "at top"
    const TOP_START_REGION_FRACTION = 0.20;         // top 20% of viewport
    const MIN_SWIPE_VELOCITY_PX_PER_MS = 0.45;      // ~450 px/s (fast-ish swipe)
    const MIN_PULL_DISTANCE_PX = 60;                // total downward distance required
    const CANCEL_ON_DIRECTION_CHANGE = true;        // cancel if user starts moving up
    const PASSIVE_MOVE_PREVENT_THRESHOLD = 10;      // start preventing default after this many px pulled

    // Helpers
    const getFallbackScrollElement = function () {
        return document.scrollingElement || document.documentElement || document.body || null;
    };

    const readScrollTop = function (target) {
        if (!target) return 0;

        if (target === document || target === window) {
            return window.pageYOffset || 0;
        }

        if (target === document.body || target === document.documentElement) {
            return window.pageYOffset || target.scrollTop || 0;
        }

        return target.scrollTop || 0;
    };

    const isNearTop = function (value) {
        return value <= SCROLL_TOP_TOLERANCE;
    };

    const findScrollTarget = function (element) {
        if (!element) return getFallbackScrollElement();

        const attributeSelector = '[data-refresh-scrollable]';

        const attributeAncestor = element.closest(attributeSelector);
        if (attributeAncestor) return attributeAncestor;

        const mobileAncestor = element.closest('.mobile-content');
        if (mobileAncestor) return mobileAncestor;

        const attributeDescendant = element.querySelector(attributeSelector);
        if (attributeDescendant) return attributeDescendant;

        const refreshContent = element.querySelector('.refresh-content');
        if (refreshContent) {
            const nestedAttribute = refreshContent.querySelector(attributeSelector);
            if (nestedAttribute) return nestedAttribute;

            const nestedMobile = refreshContent.querySelector('.mobile-content');
            if (nestedMobile) return nestedMobile;

            return refreshContent;
        }

        const mobileDescendant = element.querySelector('.mobile-content');
        if (mobileDescendant) return mobileDescendant;

        return getFallbackScrollElement();
    };

    const resolveScrollTarget = function (instance) {
        if (!instance) return null;
        const target = findScrollTarget(instance.element);
        instance.scrollTarget = target || instance.scrollTarget || getFallbackScrollElement();
        return instance.scrollTarget;
    };

    // --- Public API: initialize ---
    window.initializeRefreshView = function (element, dotNetRef) {
        console.log('RefreshView: Initializing touch handlers', { element, dotNetRef });

        if (!element || !dotNetRef) {
            console.error('RefreshView: Invalid parameters', { element, dotNetRef });
            return;
        }

        // Already initialized?
        if (window.refreshViewInstances.has(element)) {
            console.log('RefreshView: Already initialized for this element');
            return;
        }

        const instance = {
            element,
            dotNetRef,
            // Gesture state
            startY: 0,
            startTime: 0,
            lastY: 0,
            lastTime: 0,
            maxVelocity: 0,
            totalDeltaY: 0,
            startedAtTop: false,
            startedInTopRegion: false,
            isTracking: false,  // raw touch tracking
            isArmed: false,     // passed all gates (top + region)
            isActivePull: false,// currently pulling down at top
            scrollTarget: null
        };

        window.refreshViewInstances.set(element, instance);

        // --- Handlers ---
        const handleTouchStart = function (e) {
            if (e.touches.length !== 1) return;

            const t = e.touches[0];
            const scrollTarget = resolveScrollTarget(instance);
            const scrollTop = readScrollTop(scrollTarget);

            // Gate 1: must be at the very top
            const atTop = isNearTop(scrollTop);

            // Gate 2: touch must start in top N% of viewport
            const topRegionLimit = window.innerHeight * TOP_START_REGION_FRACTION;
            const inTopRegion = t.clientY <= topRegionLimit;

            instance.startedAtTop = atTop;
            instance.startedInTopRegion = inTopRegion;

            if (atTop && inTopRegion) {
                instance.startY = t.clientY;
                instance.lastY = t.clientY;
                instance.totalDeltaY = 0;
                instance.startTime = performance.now();
                instance.lastTime = instance.startTime;
                instance.maxVelocity = 0;
                instance.isTracking = true;
                instance.isArmed = true;
                instance.isActivePull = false;

                // Inform .NET only once we’re armed
                try {
                    instance.dotNetRef.invokeMethodAsync('OnTouchStart', t.clientY);
                } catch (ex) {
                    console.error('RefreshView: Error in OnTouchStart', ex);
                }
            } else {
                // Not eligible; ignore this gesture entirely
                instance.isTracking = false;
                instance.isArmed = false;
                instance.isActivePull = false;
            }
        };

        const handleTouchMove = function (e) {
            if (!instance.isTracking || !instance.isArmed) return;
            if (e.touches.length !== 1) return;

            const t = e.touches[0];
            const now = performance.now();

            const scrollTarget = resolveScrollTarget(instance);
            const scrollTop = readScrollTop(scrollTarget);
            const currentlyAtTop = isNearTop(scrollTop);

            const dy = t.clientY - instance.lastY;              // incremental movement
            const totalDy = t.clientY - instance.startY;        // total pull
            const dt = Math.max(1, now - instance.lastTime);    // ms
            const v = dy / dt;                                   // px per ms (positive = down)

            // Track velocity and totals only while still at the very top
            if (!currentlyAtTop) {
                // User scrolled away -> disarm
                disarm('scrolled away from top');
                return;
            }

            // Cancel if direction goes up and we require consistent down pull
            if (CANCEL_ON_DIRECTION_CHANGE && dy < 0) {
                disarm('direction changed upward');
                return;
            }

            // We’re at top and moving
            instance.totalDeltaY = totalDy;
            instance.maxVelocity = Math.max(instance.maxVelocity, v);
            instance.lastY = t.clientY;
            instance.lastTime = now;

            // Activate “pull” mode (visual updates) only while pulling down at top
            if (totalDy > 0) {
                instance.isActivePull = true;
                // Prevent native scrolling once it’s clearly a pull
                if (totalDy > PASSIVE_MOVE_PREVENT_THRESHOLD) {
                    e.preventDefault();
                    e.stopPropagation();
                }

                try {
                    // Forward to .NET so it can animate indicator (we gate it here)
                    instance.dotNetRef.invokeMethodAsync('OnTouchMove', t.clientY, scrollTop);
                } catch (ex) {
                    console.error('RefreshView: Error in OnTouchMove', ex);
                }
            } else {
                // Not a downward pull
                if (CANCEL_ON_DIRECTION_CHANGE) {
                    disarm('not a downward pull');
                }
            }
        };

        const handleTouchEnd = function () {
            if (!instance.isTracking) return;

            const elapsed = performance.now() - instance.startTime;

            // Decide whether to request refresh:
            // Must satisfy: started at top, started in top region,
            // currently armed, pulled down at least MIN_PULL_DISTANCE_PX,
            // and peak velocity exceeded MIN_SWIPE_VELOCITY_PX_PER_MS.
            const shouldTrigger =
                instance.isArmed &&
                instance.startedAtTop &&
                instance.startedInTopRegion &&
                instance.isActivePull &&
                (instance.totalDeltaY >= MIN_PULL_DISTANCE_PX) &&
                (instance.maxVelocity >= MIN_SWIPE_VELOCITY_PX_PER_MS);

            // Reset tracking before calling .NET to avoid racey moves
            const dotNet = instance.dotNetRef;
            instance.isTracking = false;
            instance.isArmed = false;
            instance.isActivePull = false;

            try {
                // Always notify end so .NET can reset UI
                dotNet.invokeMethodAsync('OnTouchEnd');

                // Optionally, you can add a dedicated method on .NET side like:
                // dotNet.invokeMethodAsync('OnSwipeRefreshRequested');
                // For backward-compat, we rely on OnTouchEnd + your distance/velocity gates here.
                if (!shouldTrigger) {
                    // If your .NET logic triggers refresh purely on distance,
                    // consider sending a "cancel" signal or just let the
                    // missing distance/velocity imply cancellation.
                    // Nothing extra to call here.
                }
            } catch (ex) {
                console.error('RefreshView: Error in OnTouchEnd', ex);
            }
        };

        function disarm(reason) {
            // Stop forwarding move events and ensure .NET resets UI
            if (instance.isArmed || instance.isTracking) {
                // console.debug('RefreshView: Disarmed -', reason);
                instance.isArmed = false;
                instance.isTracking = false;
                instance.isActivePull = false;

                try {
                    instance.dotNetRef.invokeMethodAsync('OnTouchEnd');
                } catch (ex) {
                    console.error('RefreshView: Error during disarm OnTouchEnd', ex);
                }
            }
        }

        // Add event listeners (note: move must be passive:false to allow preventDefault)
        element.addEventListener('touchstart', handleTouchStart, { passive: true });
        element.addEventListener('touchmove', handleTouchMove, { passive: false });
        element.addEventListener('touchend', handleTouchEnd, { passive: true });
        element.addEventListener('touchcancel', handleTouchEnd, { passive: true });

        instance.handlers = {
            touchstart: handleTouchStart,
            touchmove: handleTouchMove,
            touchend: handleTouchEnd,
            touchcancel: handleTouchEnd
        };

        console.log('RefreshView: Touch handlers initialized with strict gating');
    };

    // --- Utility APIs (unchanged, but safe-guarded) ---
    window.getRefreshViewScrollTop = function () {
        try {
            if (window.refreshViewInstances && window.refreshViewInstances.size > 0) {
                for (const instance of window.refreshViewInstances.values()) {
                    const target = resolveScrollTarget(instance);
                    const scrollTop = Math.max(0, readScrollTop(target));
                    return scrollTop;
                }
            }
            const fallback = getFallbackScrollElement();
            return Math.max(0, readScrollTop(fallback));
        } catch (ex) {
            console.error('RefreshView: Error determining scroll position', ex);
            return 0;
        }
    };

    window.updateRefreshIndicator = function (indicator, translateY, opacity) {
        if (!indicator) return;
        try {
            indicator.style.transform = 'translateY(' + (translateY - 80) + 'px)';
            indicator.style.opacity = String(Math.min(opacity, 1));
        } catch (ex) {
            console.error('RefreshView: Error updating indicator', ex);
        }
    };

    window.updateRefreshContent = function (content, translateY) {
        if (!content) return;
        try {
            content.style.transform = 'translateY(' + translateY + 'px)';
        } catch (ex) {
            console.error('RefreshView: Error updating content', ex);
        }
    };

    window.showRefreshIndicator = function (indicator, content) {
        if (!indicator || !content) return;
        try {
            indicator.classList.add('refreshing');
            content.style.transform = 'translateY(30px)';
        } catch (ex) {
            console.error('RefreshView: Error showing indicator', ex);
        }
    };

    window.resetRefreshIndicator = function (indicator, content) {
        if (!indicator || !content) return;
        try {
            indicator.style.transform = 'translateY(0)';
            indicator.style.opacity = '0';
            indicator.classList.remove('refreshing');
            content.style.transform = 'translateY(0)';
        } catch (ex) {
            console.error('RefreshView: Error resetting indicator', ex);
        }
    };

    window.cleanupRefreshView = function (element) {
        const instance = window.refreshViewInstances.get(element);
        if (!instance) return;

        try {
            if (instance.handlers) {
                element.removeEventListener('touchstart', instance.handlers.touchstart);
                element.removeEventListener('touchmove', instance.handlers.touchmove);
                element.removeEventListener('touchend', instance.handlers.touchend);
                element.removeEventListener('touchcancel', instance.handlers.touchcancel);
            }

            if (instance.dotNetRef) {
                instance.dotNetRef.dispose();
            }

            instance.scrollTarget = null;
            window.refreshViewInstances.delete(element);
            console.log('RefreshView: Cleaned up successfully');
        } catch (ex) {
            console.error('RefreshView: Error during cleanup', ex);
        }
    };

    // Fallback simulator (kept for testing)
    window.simulateRefresh = function (dotNetRef) {
        if (dotNetRef) {
            try {
                dotNetRef.invokeMethodAsync('OnTouchStart', 0);
                dotNetRef.invokeMethodAsync('OnTouchMove', 100, 0);
                dotNetRef.invokeMethodAsync('OnTouchEnd');
            } catch (ex) {
                console.error('RefreshView: Error simulating refresh', ex);
            }
        }
    };

    console.log('RefreshView: JavaScript module loaded successfully');
})();