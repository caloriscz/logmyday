const registry = new Map();

function ensureHandler(elementId) {
    return function handler(event) {
        const element = document.getElementById(elementId);
        if (!element) {
            return;
        }

        if (!element.contains(event.target)) {
            const entry = registry.get(elementId);
            if (entry) {
                entry.dotNetRef.invokeMethodAsync("CloseDropdownAsync");
            }
        }
    };
}

export const searchableSelect = {
    ensureElementId(element) {
        if (!element) {
            return null;
        }

        if (!element.id) {
            const idFactory = typeof crypto !== "undefined" && typeof crypto.randomUUID === "function"
                ? crypto.randomUUID()
                : Math.random().toString(36).slice(2, 11);
            element.id = `searchable-select-menu-${idFactory}`;
        }

        return element.id;
    },

    registerOutsideClick(dotNetRef, elementId) {
        if (!elementId) {
            return;
        }

        searchableSelect.unregisterOutsideClick(elementId);

        const handler = ensureHandler(elementId);
        document.addEventListener("mousedown", handler, true);
        registry.set(elementId, { handler, dotNetRef });
    },

    unregisterOutsideClick(elementId) {
        const entry = registry.get(elementId);
        if (!entry) {
            return;
        }

        document.removeEventListener("mousedown", entry.handler, true);
        registry.delete(elementId);
    }
};
