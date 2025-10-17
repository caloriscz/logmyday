// Theme Management for LogMyDay Mobile
// Handles light/dark/system theme switching with Tailwind CSS

(function() {
    'use strict';

    console.log('Theme Manager: Initializing...');

    // Get theme from preferences or default to system
    function getStoredTheme() {
        // This will be set by C# via setTheme, but we can also check localStorage as fallback
        return localStorage.getItem('theme') || 'system';
    }

    // Apply theme to document - force immediate update
    function applyTheme(theme) {
        console.log('Applying theme:', theme);
        
        const root = document.documentElement;
        const body = document.body;
        
        if (theme === 'system') {
            // Follow system preference
            const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
            console.log('System prefers dark:', prefersDark);
            if (prefersDark) {
                root.classList.add('dark');
                body.classList.add('dark');
            } else {
                root.classList.remove('dark');
                body.classList.remove('dark');
            }
        } else if (theme === 'dark') {
            console.log('Setting dark mode');
            root.classList.add('dark');
            body.classList.add('dark');
        } else {
            console.log('Setting light mode');
            root.classList.remove('dark');
            body.classList.remove('dark');
        }

        // Also store in localStorage as backup
        localStorage.setItem('theme', theme);
        
        console.log('Dark class on html:', root.classList.contains('dark'));
        console.log('Dark class on body:', body.classList.contains('dark'));
    }

    // Initialize theme on page load
    function initTheme() {
        const theme = getStoredTheme();
        applyTheme(theme);

        // Listen for system theme changes if using system theme
        const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
        mediaQuery.addEventListener('change', (e) => {
            const currentTheme = getStoredTheme();
            if (currentTheme === 'system') {
                applyTheme('system');
            }
        });
    }

    // Exposed function for C# to call
    window.setTheme = function(theme) {
        console.log('setTheme called with:', theme);
        applyTheme(theme);
    };

    // Exposed function to get current theme
    window.getTheme = function() {
        return getStoredTheme();
    };

    // Initialize immediately
    initTheme();

    console.log('Theme Manager: Initialized');
})();
