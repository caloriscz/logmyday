/**
 * Todo list browser notification helpers.
 * Called from TodoListsPanel.razor via IJSRuntime.
 */

let _scheduledTimers = [];

/**
 * Request browser notification permission from the user.
 * Returns the permission state: 'granted', 'denied', or 'default'.
 */
window.todoNotifications = {
    requestPermission: async function () {
        if (!('Notification' in window)) {
            return 'unsupported';
        }
        if (Notification.permission === 'granted') {
            return 'granted';
        }
        const result = await Notification.requestPermission();
        return result;
    },

    /**
     * Schedule browser notifications for todo items with a NotifyAt time.
     * Clears any previously scheduled timers first.
     *
     * @param {Array<{id: number, title: string, notifyAtMs: number}>} items
     *   notifyAtMs: milliseconds from midnight today (local time) when the notification should fire.
     *   Pass -1 to skip an item.
     */
    scheduleNotifications: function (items) {
        // Clear previously scheduled timers
        _scheduledTimers.forEach(t => clearTimeout(t));
        _scheduledTimers = [];

        if (Notification.permission !== 'granted' || !items || items.length === 0) {
            return;
        }

        const now = new Date();
        const midnight = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 0, 0, 0, 0);

        items.forEach(function (item) {
            if (item.notifyAtMs < 0) {
                return;
            }

            const fireAt = new Date(midnight.getTime() + item.notifyAtMs);
            const delay = fireAt.getTime() - now.getTime();

            if (delay <= 0) {
                // Time has already passed today — skip
                return;
            }

            const timer = setTimeout(function () {
                try {
                    new Notification('To-Do: ' + item.title, {
                        body: 'Reminder from LogMyDay',
                        icon: '/images/logo.png'
                    });
                } catch (e) {
                    // Notification constructor can throw in some environments — silently ignore
                }
            }, delay);

            _scheduledTimers.push(timer);
        });
    }
};
