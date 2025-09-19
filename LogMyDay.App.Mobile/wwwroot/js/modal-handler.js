// Modal Handler for Android Back Button - LogMyDay Mobile
(function() {
    'use strict';
    
    // Ensure window object exists
    if (typeof window === 'undefined') {
        console.warn('ModalHandler: Window object not available');
        return;
    }

    // Check if any Bootstrap modals are currently open
    window.hasOpenModals = function() {
        try {
            // Check for Bootstrap 5 modals with 'show' class
            const openModals = document.querySelectorAll('.modal.show');
            const hasModals = openModals.length > 0;
            
            console.log('ModalHandler: Checking for open modals', {
                found: hasModals,
                count: openModals.length,
                modalIds: Array.from(openModals).map(m => m.id)
            });
            
            return hasModals;
        } catch (error) {
            console.error('ModalHandler: Error checking for open modals', error);
            return false;
        }
    };

    // Close all open Bootstrap modals
    window.closeAllModals = function() {
        try {
            const openModals = document.querySelectorAll('.modal.show');
            let closedCount = 0;
            
            openModals.forEach(modal => {
                try {
                    // Use Bootstrap's Modal API to properly close the modal
                    const modalInstance = bootstrap.Modal.getInstance(modal);
                    if (modalInstance) {
                        modalInstance.hide();
                        closedCount++;
                        console.log('ModalHandler: Closed modal using Bootstrap instance', modal.id);
                    } else {
                        // Fallback: manually trigger the close button
                        const closeBtn = modal.querySelector('[data-bs-dismiss="modal"]');
                        if (closeBtn) {
                            closeBtn.click();
                            closedCount++;
                            console.log('ModalHandler: Closed modal using close button', modal.id);
                        } else {
                            // Last resort: manually remove show class and backdrop
                            modal.classList.remove('show');
                            modal.style.display = 'none';
                            modal.setAttribute('aria-hidden', 'true');
                            modal.removeAttribute('aria-modal');
                            
                            // Remove backdrop if it exists
                            const backdrop = document.querySelector('.modal-backdrop');
                            if (backdrop) {
                                backdrop.remove();
                            }
                            
                            // Remove modal-open class from body
                            document.body.classList.remove('modal-open');
                            document.body.style.overflow = '';
                            document.body.style.paddingRight = '';
                            
                            closedCount++;
                            console.log('ModalHandler: Manually closed modal', modal.id);
                        }
                    }
                } catch (modalError) {
                    console.error('ModalHandler: Error closing individual modal', modal.id, modalError);
                }
            });
            
            console.log('ModalHandler: Closed modals', { count: closedCount });
            return closedCount > 0;
        } catch (error) {
            console.error('ModalHandler: Error closing modals', error);
            return false;
        }
    };

    // Check if Bootstrap is available
    window.isBootstrapAvailable = function() {
        return typeof bootstrap !== 'undefined' && bootstrap.Modal;
    };

    // Enhanced back button handling for Android
    function setupAndroidBackButtonHandler() {
        try {
            // Listen for the browser's popstate event which gets triggered on back navigation
            window.addEventListener('popstate', function(event) {
                console.log('ModalHandler: Popstate event detected');
                
                // Check if we have open modals
                if (window.hasOpenModals()) {
                    console.log('ModalHandler: Modals detected during popstate, closing them');
                    
                    // Prevent the navigation by pushing current state back
                    history.pushState(null, null, window.location.href);
                    
                    // Close the modals
                    window.closeAllModals();
                    
                    // Prevent default browser back behavior
                    event.preventDefault();
                    event.stopPropagation();
                    return false;
                }
            });

            // Also listen for hashchange as a backup
            window.addEventListener('hashchange', function(event) {
                console.log('ModalHandler: Hashchange event detected');
                
                if (window.hasOpenModals()) {
                    console.log('ModalHandler: Modals detected during hashchange, closing them');
                    window.closeAllModals();
                    event.preventDefault();
                    return false;
                }
            });

            // Push initial state to enable popstate detection
            if (typeof history !== 'undefined' && history.pushState) {
                history.pushState(null, null, window.location.href);
            }

            console.log('ModalHandler: Android back button handler setup complete');
        } catch (error) {
            console.error('ModalHandler: Error setting up back button handler', error);
        }
    }

    // Enhanced modal event listeners
    function setupModalEventListeners() {
        try {
            // Listen for modal show events to manage history state
            document.addEventListener('shown.bs.modal', function(event) {
                console.log('ModalHandler: Modal shown', event.target.id);
                
                // Push a state when modal opens so back button can close it
                if (typeof history !== 'undefined' && history.pushState) {
                    history.pushState({ modalOpen: true, modalId: event.target.id }, '', window.location.href);
                }
            });

            // Listen for modal hide events
            document.addEventListener('hidden.bs.modal', function(event) {
                console.log('ModalHandler: Modal hidden', event.target.id);
                
                // When modal is properly closed, we can clean up history if needed
                // Note: We need to be careful not to interfere with normal navigation
            });

            console.log('ModalHandler: Modal event listeners setup complete');
        } catch (error) {
            console.error('ModalHandler: Error setting up modal event listeners', error);
        }
    }

    // Initialize when DOM is ready
    function initialize() {
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', function() {
                setupAndroidBackButtonHandler();
                setupModalEventListeners();
            });
        } else {
            setupAndroidBackButtonHandler();
            setupModalEventListeners();
        }
    }

    // Initialize the modal handler
    initialize();

    console.log('ModalHandler: JavaScript functions registered', {
        hasOpenModals: typeof window.hasOpenModals,
        closeAllModals: typeof window.closeAllModals,
        isBootstrapAvailable: typeof window.isBootstrapAvailable
    });
})();