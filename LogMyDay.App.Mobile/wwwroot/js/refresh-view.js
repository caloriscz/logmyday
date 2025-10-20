// RefreshView JavaScript handlers - Enhanced for MAUI Blazor
(function() {
    'use strict';
    
    // Ensure window object exists
    if (typeof window === 'undefined') {
        console.warn('RefreshView: Window object not available');
        return;
    }

    // Initialize global storage
    window.refreshViewInstances = window.refreshViewInstances || new Map();

    const SCROLL_TOP_TOLERANCE = 1;

    const getFallbackScrollElement = function() {
        return document.scrollingElement || document.documentElement || document.body || null;
    };

    const readScrollTop = function(target) {
        if (!target) {
            return 0;
        }

        if (target === document || target === window) {
            return window.pageYOffset || 0;
        }

        if (target === document.body || target === document.documentElement) {
            return window.pageYOffset || target.scrollTop || 0;
        }

        return target.scrollTop || 0;
    };

    const isNearTop = function(value) {
        return value <= SCROLL_TOP_TOLERANCE;
    };

    const findScrollTarget = function(element) {
        if (!element) {
            return getFallbackScrollElement();
        }

        const attributeSelector = '[data-refresh-scrollable]';

        const attributeAncestor = element.closest(attributeSelector);
        if (attributeAncestor) {
            return attributeAncestor;
        }

        const mobileAncestor = element.closest('.mobile-content');
        if (mobileAncestor) {
            return mobileAncestor;
        }

        const attributeDescendant = element.querySelector(attributeSelector);
        if (attributeDescendant) {
            return attributeDescendant;
        }

        const refreshContent = element.querySelector('.refresh-content');
        if (refreshContent) {
            const nestedAttribute = refreshContent.querySelector(attributeSelector);
            if (nestedAttribute) {
                return nestedAttribute;
            }

            const nestedMobile = refreshContent.querySelector('.mobile-content');
            if (nestedMobile) {
                return nestedMobile;
            }

            return refreshContent;
        }

        const mobileDescendant = element.querySelector('.mobile-content');
        if (mobileDescendant) {
            return mobileDescendant;
        }

        return getFallbackScrollElement();
    };

    const resolveScrollTarget = function(instance) {
        if (!instance) {
            return null;
        }

        const target = findScrollTarget(instance.element);
        instance.scrollTarget = target || instance.scrollTarget || getFallbackScrollElement();
        return instance.scrollTarget;
    };

    window.initializeRefreshView = function(element, dotNetRef) {
        console.log('RefreshView: Initializing touch handlers', { element, dotNetRef });
        
        if (!element || !dotNetRef) {
            console.error('RefreshView: Invalid parameters', { element, dotNetRef });
            return;
        }
        
        // Check if already initialized
        if (window.refreshViewInstances.has(element)) {
            console.log('RefreshView: Already initialized for this element');
            return;
        }
        
        const instance = {
            element: element,
            dotNetRef: dotNetRef,
            touchStartY: 0,
            touchStartScrollTop: 0,  // Track scroll position when touch started
            isTracking: false,
            scrollTarget: null
        };
        
        // Store instance reference
        window.refreshViewInstances.set(element, instance);
        
        // Touch event handlers
        const handleTouchStart = function(e) {
            if (e.touches.length !== 1) return;

            const touch = e.touches[0];

            // Find the actual scrollable content related to the refresh container
            const scrollTarget = resolveScrollTarget(instance);
            const scrollTop = readScrollTop(scrollTarget);

            // CRITICAL: Only start tracking if we're EXACTLY at the top (scrollTop === 0)
            // This prevents pull-to-refresh from activating when scrolling up from a scrolled position
            if (isNearTop(scrollTop)) {
                instance.touchStartY = touch.clientY;
                instance.touchStartScrollTop = scrollTop;
                instance.isTracking = true;

                try {
                    dotNetRef.invokeMethodAsync('OnTouchStart', touch.clientY);
                } catch (ex) {
                    console.error('RefreshView: Error in touchStart handler', ex);
                }
            } else {
                // Not at top, don't track pull-to-refresh
                instance.isTracking = false;
            }
        };

        const handleTouchMove = function(e) {
            if (!instance.isTracking || e.touches.length !== 1) return;

            const touch = e.touches[0];
            const deltaY = touch.clientY - instance.touchStartY;

            const scrollTarget = resolveScrollTarget(instance);
            const scrollTop = readScrollTop(scrollTarget);
            const startedAtTop = isNearTop(instance.touchStartScrollTop);
            const currentlyAtTop = isNearTop(scrollTop);

            // CRITICAL: Only activate pull-to-refresh when:
            // 1. Touch started at the very top (within tolerance)
            // 2. The scroll position is still within the top tolerance
            // 3. The gesture remains a downward pull (deltaY > 0 for preventDefault)
            // This prevents accidental refresh when scrolling up from below
            if (startedAtTop && currentlyAtTop) {
                // Prevent default scroll behavior when pulling to refresh
                // But only after significant pull to avoid interfering with normal touches
                if (deltaY > 10) {
                    e.preventDefault();
                    e.stopPropagation();
                }

                try {
                    dotNetRef.invokeMethodAsync('OnTouchMove', touch.clientY, scrollTop);
                } catch (ex) {
                    console.error('RefreshView: Error in touchMove handler', ex);
                }
            } else {
                // User has scrolled away from top or changed direction - notify and stop tracking
                instance.isTracking = false;

                try {
                    dotNetRef.invokeMethodAsync('OnTouchMove', touch.clientY, scrollTop);
                    dotNetRef.invokeMethodAsync('OnTouchEnd');
                } catch (ex) {
                    console.error('RefreshView: Error cancelling touch move', ex);
                }
            }
        };

        const handleTouchEnd = function(e) {
            if (!instance.isTracking) return;
            
            instance.isTracking = false;
            
            try {
                dotNetRef.invokeMethodAsync('OnTouchEnd');
            } catch (ex) {
                console.error('RefreshView: Error in touchEnd handler', ex);
            }
        };
        
        // Add event listeners with passive: false to allow preventDefault
        element.addEventListener('touchstart', handleTouchStart, { passive: true });
        element.addEventListener('touchmove', handleTouchMove, { passive: false });
        element.addEventListener('touchend', handleTouchEnd, { passive: true });
        element.addEventListener('touchcancel', handleTouchEnd, { passive: true });
        
        // Store handlers for cleanup
        instance.handlers = {
            touchstart: handleTouchStart,
            touchmove: handleTouchMove,
            touchend: handleTouchEnd,
            touchcancel: handleTouchEnd
        };
        
        console.log('RefreshView: Touch handlers initialized successfully');
    };

    window.getRefreshViewScrollTop = function() {
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

    window.updateRefreshIndicator = function(indicator, translateY, opacity) {
        if (!indicator) return;
        
        try {
            indicator.style.transform = 'translateY(' + (translateY - 80) + 'px)';
            indicator.style.opacity = Math.min(opacity, 1);
        } catch (ex) {
            console.error('RefreshView: Error updating indicator', ex);
        }
    };

    window.updateRefreshContent = function(content, translateY) {
        if (!content) return;
        
        try {
            content.style.transform = 'translateY(' + translateY + 'px)';
        } catch (ex) {
            console.error('RefreshView: Error updating content', ex);
        }
    };

    window.showRefreshIndicator = function(indicator, content) {
        if (!indicator || !content) return;
        
        try {
            indicator.classList.add('refreshing');
            content.style.transform = 'translateY(30px)';
        } catch (ex) {
            console.error('RefreshView: Error showing indicator', ex);
        }
    };

    window.resetRefreshIndicator = function(indicator, content) {
        if (!indicator || !content) return;
        
        try {
            // Reset to initial state
            indicator.style.transform = 'translateY(0)';
            indicator.style.opacity = '0';
            indicator.classList.remove('refreshing');
            content.style.transform = 'translateY(0)';
        } catch (ex) {
            console.error('RefreshView: Error resetting indicator', ex);
        }
    };

    window.preventDefault = function() {
        // This is called from touch move to prevent scrolling
        // The actual preventDefault is handled in the touchmove handler
    };

    window.cleanupRefreshView = function(element) {
        const instance = window.refreshViewInstances.get(element);
        if (!instance) return;
        
        try {
            // Remove event listeners
            if (instance.handlers) {
                element.removeEventListener('touchstart', instance.handlers.touchstart);
                element.removeEventListener('touchmove', instance.handlers.touchmove);
                element.removeEventListener('touchend', instance.handlers.touchend);
                element.removeEventListener('touchcancel', instance.handlers.touchcancel);
            }

            // Clean up references
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

    // Fallback for older browsers or when touch events aren't supported
    window.simulateRefresh = function(dotNetRef) {
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
