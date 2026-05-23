/**
 * Todo list browser notification helpers.
 * Called from TodoListsPanel.razor via IJSRuntime.
 *
 * Timers are namespaced by `scope` so multiple panel instances on the same
 * page (e.g., the homepage's Reminder + Basic panels) don't clobber each
 * other when each one reschedules its own subset of items.
 *
 * Each scope also carries a DotNetObjectReference to its owning panel so
 * the JS timer callback can invoke `LogNotificationFired` for diagnostics.
 */

const _timersByScope = {};
const _dotNetRefByScope = {};

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
     * Clears any previously scheduled timers within the given scope first;
     * timers from other scopes are left alone.
     *
     * @param {string} scope
     *   Per-panel scope key (typically a Guid). Falls back to 'default' if empty.
     * @param {object} dotNetRef
     *   DotNetObjectReference to the owning panel (used for fired-event logging).
     * @param {Array<{id: number, title: string, notifyAtMs: number}>} items
     */
    scheduleNotifications: function (scope, dotNetRef, items) {
        const key = scope || 'default';

        const existing = _timersByScope[key] || [];
        existing.forEach(t => clearTimeout(t));
        _timersByScope[key] = [];
        _dotNetRefByScope[key] = dotNetRef;

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
                return;
            }

            const timer = setTimeout(function () {
                const ref = _dotNetRefByScope[key];
                if (ref) {
                    try {
                        ref.invokeMethodAsync('LogNotificationFired', item.id, Date.now());
                    } catch (e) {
                        // best-effort diagnostic — never block the notification
                    }
                }
                try {
                    new Notification('To-Do: ' + item.title, {
                        body: 'Reminder from LogMyDay',
                        icon: '/images/logo.png'
                    });
                } catch (e) {
                    // Notification constructor can throw in some environments — silently ignore
                }
            }, delay);

            _timersByScope[key].push(timer);
        });
    }
};
