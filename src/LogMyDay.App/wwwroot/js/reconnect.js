/**
 * Blazor circuit reconnect tuning (see wiki: docs/blazor-server-hosting.md).
 *
 * blazor.web.js is loaded with autostart="false"; this script starts it with fast
 * retries so a brief WebSocket drop (mobile browser backgrounded between edits,
 * proxy hiccup) resumes quickly. The custom #components-reconnect-modal toast in
 * App.razor stays invisible for a short grace period (CSS animation delay), so
 * sub-second resumes never flash a "loading" overlay.
 *
 * State classes blazor.web.js applies to the modal element:
 *   components-reconnect-show     reconnect in progress
 *   components-reconnect-hide     reconnected (or connected)
 *   components-reconnect-failed   retries exhausted — toast offers Reload
 *   components-reconnect-rejected server no longer has the circuit
 */
(function () {
    Blazor.start({
        circuit: {
            reconnectionOptions: {
                maxRetries: 60,
                retryIntervalMilliseconds: function (previousAttempts) {
                    return previousAttempts < 10 ? 500 : 3000;
                }
            }
        }
    });

    const modal = document.getElementById('components-reconnect-modal');
    if (!modal) {
        return;
    }

    // Rejected means the server dropped the circuit (restart, retention expired) —
    // only a fresh page load gets a new one, so do it without user interaction.
    new MutationObserver(function () {
        if (modal.classList.contains('components-reconnect-rejected')) {
            location.reload();
        }
    }).observe(modal, { attributes: true, attributeFilter: ['class'] });
})();
