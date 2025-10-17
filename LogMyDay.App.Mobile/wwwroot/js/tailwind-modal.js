// Tailwind Modal Handler - LogMyDay Mobile
// Replaces Bootstrap modal functionality with pure Tailwind CSS + JavaScript

(function() {
    'use strict';
    
    console.log('Tailwind Modal Handler: Initializing...');

    // Modal state management
    const openModals = new Set();

    // Show modal function
    window.showModal = function(modalId) {
        const modal = document.getElementById(modalId);
        if (!modal) {
            console.error(`Modal not found: ${modalId}`);
            return;
        }

        console.log(`Showing modal: ${modalId}`);
        
        // Add modal to open set
        openModals.add(modalId);
        
        // Show modal with fade-in animation
        modal.classList.remove('hidden');
        
        // Trigger reflow for animation
        modal.offsetHeight;
        
        // Fade in overlay
        modal.classList.remove('opacity-0');
        modal.classList.add('opacity-100');
        
        if (content) {
            content.classList.add('opacity-100', 'translate-y-0');
            content.classList.remove('opacity-0', 'translate-y-4');
        }
        
        // Prevent body scroll
        document.body.style.overflow = 'hidden';
        
        // Dispatch custom event
        modal.dispatchEvent(new CustomEvent('modal-shown', { detail: { modalId } }));
    };

    // Hide modal function
    window.hideModal = function(modalId) {
        const modal = document.getElementById(modalId);
        if (!modal) {
            console.error(`Modal not found: ${modalId}`);
            return;
        }

        console.log(`Hiding modal: ${modalId}`);
        
        // Remove from open set
        openModals.delete(modalId);
        
        // Start fade-out animation
        modal.classList.remove('opacity-100');
        modal.classList.add('opacity-0');
        
        // Wait for animation to complete, then hide
        setTimeout(() => {
            modal.classList.add('hidden');
            
            // Restore body scroll if no modals are open
            if (openModals.size === 0) {
                document.body.style.overflow = '';
            }
            
            // Dispatch custom event
            modal.dispatchEvent(new CustomEvent('modal-hidden', { detail: { modalId } }));
        }, 300); // Match transition duration
    };

    // Toggle modal function
    window.toggleModal = function(modalId) {
        const modal = document.getElementById(modalId);
        if (!modal) {
            console.error(`Modal not found: ${modalId}`);
            return;
        }

        if (modal.classList.contains('hidden')) {
            showModal(modalId);
        } else {
            hideModal(modalId);
        }
    };

    // Check if any modals are open (for Android back button)
    window.hasOpenModals = function() {
        return openModals.size > 0;
    };

    // Close all open modals
    window.closeAllModals = function() {
        console.log(`Closing all modals. Count: ${openModals.size}`);
        const modalIds = Array.from(openModals);
        modalIds.forEach(modalId => hideModal(modalId));
        return modalIds.length > 0;
    };

    // Track if listeners are already attached
    let listenersAttached = false;

    // Initialize modal triggers on document
    function initializeModalTriggers() {
        if (listenersAttached) {
            console.log('Tailwind Modal Handler: Listeners already attached, skipping');
            return;
        }
        
        // Handle data-modal-toggle attributes
        document.addEventListener('click', function(e) {
            console.log('Click detected on:', e.target);
            const trigger = e.target.closest('[data-modal-toggle]');
            if (trigger) {
                console.log('Modal toggle trigger found:', trigger);
                e.preventDefault();
                const modalId = trigger.getAttribute('data-modal-toggle');
                toggleModal(modalId);
            }
        }, true); // Use capture phase

        // Handle data-modal-show attributes
        document.addEventListener('click', function(e) {
            const trigger = e.target.closest('[data-modal-show]');
            if (trigger) {
                console.log('Modal show trigger found:', trigger, 'modalId:', trigger.getAttribute('data-modal-show'));
                e.preventDefault();
                e.stopPropagation();
                const modalId = trigger.getAttribute('data-modal-show');
                showModal(modalId);
            }
        }, true); // Use capture phase

        // Handle data-modal-hide attributes (close buttons)
        document.addEventListener('click', function(e) {
            const trigger = e.target.closest('[data-modal-hide]');
            if (trigger) {
                console.log('Modal hide trigger found:', trigger);
                e.preventDefault();
                e.stopPropagation();
                const modalId = trigger.getAttribute('data-modal-hide');
                hideModal(modalId);
            }
        }, true); // Use capture phase

        // Handle backdrop clicks (close on backdrop click)
        document.addEventListener('click', function(e) {
            if (e.target.hasAttribute('data-modal-backdrop')) {
                const modal = e.target.closest('[data-modal]');
                if (modal) {
                    const modalId = modal.id;
                    const backdrop = modal.getAttribute('data-modal-backdrop');
                    // Only close if backdrop is not "static"
                    if (backdrop !== 'static') {
                        hideModal(modalId);
                    }
                }
            }
        });

        // Handle ESC key to close modals
        document.addEventListener('keydown', function(e) {
            if (e.key === 'Escape' && openModals.size > 0) {
                // Close the most recently opened modal
                const modalIds = Array.from(openModals);
                const lastModalId = modalIds[modalIds.length - 1];
                hideModal(lastModalId);
            }
        });

        listenersAttached = true;
        console.log('Tailwind Modal Handler: Event listeners attached with capture phase');
    }

    // Initialize when DOM is ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initializeModalTriggers);
    } else {
        initializeModalTriggers();
    }

    // Android back button support
    window.addEventListener('popstate', function(event) {
        if (hasOpenModals()) {
            event.preventDefault();
            closeAllModals();
            // Push state back so we don't navigate away
            history.pushState(null, '', window.location.pathname);
        }
    });

    console.log('Tailwind Modal Handler: Initialized');
    console.log('Available functions: showModal(), hideModal(), toggleModal(), hasOpenModals(), closeAllModals()');
})();
