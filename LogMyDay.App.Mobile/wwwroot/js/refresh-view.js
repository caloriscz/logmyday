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
            isTracking: false
        };
        
        // Store instance reference
        window.refreshViewInstances.set(element, instance);
        
        // Touch event handlers
        const handleTouchStart = function(e) {
            if (e.touches.length !== 1) return;
            
            const touch = e.touches[0];
            instance.touchStartY = touch.clientY;
            instance.isTracking = true;
            
            try {
                dotNetRef.invokeMethodAsync('OnTouchStart', touch.clientY);
            } catch (ex) {
                console.error('RefreshView: Error in touchStart handler', ex);
            }
        };
        
        const handleTouchMove = function(e) {
            if (!instance.isTracking || e.touches.length !== 1) return;
            
            const touch = e.touches[0];
            const deltaY = touch.clientY - instance.touchStartY;
            
            // Find the scrollable container (could be the element itself or a parent)
            let scrollElement = element;
            while (scrollElement && scrollElement.scrollTop === 0 && scrollElement.parentElement) {
                if (scrollElement.scrollHeight > scrollElement.clientHeight) {
                    break;
                }
                scrollElement = scrollElement.parentElement;
            }
            
            const scrollTop = scrollElement ? scrollElement.scrollTop : 0;
            
            // Only handle if we're at the top of the scroll and pulling down
            if (scrollTop === 0 && deltaY > 0) {
                // Prevent default scroll behavior when pulling to refresh
                if (deltaY > 10) {
                    e.preventDefault();
                    e.stopPropagation();
                }
                
                try {
                    dotNetRef.invokeMethodAsync('OnTouchMove', touch.clientY, scrollTop);
                } catch (ex) {
                    console.error('RefreshView: Error in touchMove handler', ex);
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
