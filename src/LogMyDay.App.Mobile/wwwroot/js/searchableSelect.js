// SearchableSelect outside-click handling.
// Global (not an ES module) so it is callable via the native WebView interop, and it calls back
// into .NET through the native bridge (window.LmdNative) rather than IJSRuntime (HARD RULE #1).
window.LogMyDaySearchableSelect = (function () {
    const registry = new Map();

    function ensureHandler(elementId) {
        return function handler(event) {
            const element = document.getElementById(elementId);
            if (!element) {
                return;
            }

            if (!element.contains(event.target)) {
                if (window.LmdNative && window.LmdNative.onOutsideClick) {
                    window.LmdNative.onOutsideClick(elementId);
                }
            }
        };
    }

    return {
        registerOutsideClick(elementId) {
            if (!elementId) {
                return;
            }

            this.unregisterOutsideClick(elementId);

            const handler = ensureHandler(elementId);
            document.addEventListener("mousedown", handler, true);
            registry.set(elementId, handler);
        },

        unregisterOutsideClick(elementId) {
            const handler = registry.get(elementId);
            if (!handler) {
                return;
            }

            document.removeEventListener("mousedown", handler, true);
            registry.delete(elementId);
        }
    };
})();
