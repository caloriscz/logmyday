/**
 * Account calls that the browser has to make itself, because the response changes its sign-in
 * cookie. A call from the page's circuit leaves the browser with the old cookie.
 */
window.lmdAccount = {
    async changePassword(currentPassword, newPassword) {
        try {
            const response = await fetch('/api/account/password/change', {
                method: 'POST',
                credentials: 'same-origin',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ currentPassword, newPassword })
            });

            return { status: response.status, message: response.ok ? '' : await response.text() };
        } catch {
            return { status: 0, message: '' };
        }
    }
};
