// Import Tailwind CSS
import '../css/tailwind.css';

// Theme management
const THEME_KEY = 'lmd-theme';

function getTheme() {
  const stored = localStorage.getItem(THEME_KEY);
  if (stored) return stored;
  
  // Check system preference
  if (window.matchMedia('(prefers-color-scheme: dark)').matches) {
    return 'dark';
  }
  
  return 'light';
}

function setTheme(theme) {
  localStorage.setItem(THEME_KEY, theme);
  
  if (theme === 'dark') {
    document.documentElement.classList.add('dark');
  } else {
    document.documentElement.classList.remove('dark');
  }
}

function toggleTheme() {
  const current = getTheme();
  const next = current === 'dark' ? 'light' : 'dark';
  setTheme(next);
  
  // Dispatch custom event for Blazor components
  window.dispatchEvent(new CustomEvent('themeChanged', { detail: { theme: next } }));
  
  return next;
}

// Initialize theme immediately (prevents FOUC)
(function() {
  const theme = getTheme();
  setTheme(theme);
})();

// Export functions for Blazor interop
window.LogMyDayTheme = {
  get: getTheme,
  set: setTheme,
  toggle: toggleTheme
};

// Listen for system theme changes
window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', (e) => {
  // Only auto-switch if user hasn't manually set a preference
  if (!localStorage.getItem(THEME_KEY)) {
    setTheme(e.matches ? 'dark' : 'light');
  }
});

// Modal management
window.LogMyDayModal = {
  show: function(modalId) {
    const modal = document.getElementById(modalId);
    if (modal) {
      modal.classList.remove('hidden');
      document.body.style.overflow = 'hidden';
      
      // Focus first input
      setTimeout(() => {
        const firstInput = modal.querySelector('input, select, textarea, button');
        if (firstInput) {
          firstInput.focus();
        }
      }, 100);
    }
  },
  
  hide: function(modalId) {
    const modal = document.getElementById(modalId);
    if (modal) {
      modal.classList.add('hidden');
      document.body.style.overflow = '';
    }
  }
};

// Date picker helper for mobile detection
window.LogMyDayDatePicker = {
  isMobile: function() {
    return /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent);
  }
};

// Scroll utilities
window.LogMyDayScroll = {
  toTop: function() {
    window.scrollTo({ top: 0, behavior: 'smooth' });
  },
  
  toElement: function(elementId) {
    const element = document.getElementById(elementId);
    if (element) {
      element.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
  }
};

export {};
