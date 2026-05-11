// Modal System - USING CSS CLASSES
const Modal = {
    // Show confirmation modal
    confirm: function(options) {
        console.log('Modal.confirm called with:', options);
        return new Promise((resolve) => {
            const {
                title = 'Confirm Action',
                message = 'Are you sure you want to proceed?',
                detail = '',
                confirmText = 'Yes',
                cancelText = 'No',
                type = 'confirm'
            } = options;

            this.show({
                title,
                message,
                detail,
                confirmText,
                cancelText,
                type,
                onConfirm: () => {
                    console.log('Modal confirmed');
                    this.hide();
                    resolve(true);
                },
                onCancel: () => {
                    console.log('Modal cancelled');
                    this.hide();
                    resolve(false);
                }
            });
        });
    },

    // Show success modal
    success: function(message, title = 'Success', detail = '') {
        this.show({
            title,
            message,
            detail,
            type: 'success',
            showCancel: false,
            confirmText: 'OK'
        });
    },

    // Show error modal
    error: function(message, title = 'Error', detail = '') {
        this.show({
            title,
            message,
            detail,
            type: 'error',
            showCancel: false,
            confirmText: 'OK'
        });
    },

    // Show warning modal
    warning: function(message, title = 'Warning', detail = '') {
        this.show({
            title,
            message,
            detail,
            type: 'warning',
            showCancel: false,
            confirmText: 'OK'
        });
    },

    // Show info modal
    info: function(message, title = 'Information', detail = '') {
        this.show({
            title,
            message,
            detail,
            type: 'info',
            showCancel: false,
            confirmText: 'OK'
        });
    },

    // Show modal
    show: function(options) {
        // First, hide any existing modal
        this.hide();
        
        const {
            title = 'Notification',
            message = '',
            detail = '',
            type = 'info',
            confirmText = 'OK',
            cancelText = 'Cancel',
            showCancel = true,
            onConfirm,
            onCancel
        } = options;

        // Create modal elements
        const overlay = document.createElement('div');
        overlay.className = 'modal-overlay';
        
        const modal = document.createElement('div');
        modal.className = 'modal-container';

        // Set icon based on type
        let icon = 'fa-info-circle';
        if (type === 'success') icon = 'fa-check-circle';
        if (type === 'error') icon = 'fa-exclamation-circle';
        if (type === 'warning') icon = 'fa-exclamation-triangle';
        if (type === 'confirm') icon = 'fa-question-circle';

        // Build modal HTML
        modal.innerHTML = `
            <div class="modal-header">
                <h3>${title}</h3>
                <button class="modal-close">
                    <i class="fas fa-times"></i>
                </button>
            </div>
            <div class="modal-body">
                <div class="modal-icon ${type}">
                    <i class="fas ${icon}"></i>
                </div>
                <div class="modal-message">${message}</div>
                ${detail ? `<div class="modal-detail">${detail}</div>` : ''}
            </div>
            <div class="modal-footer">
                ${showCancel ? `<button class="modal-btn secondary" id="modalCancelBtn">${cancelText}</button>` : ''}
                <button class="modal-btn ${type === 'warning' ? 'danger' : 'primary'}" id="modalConfirmBtn">${confirmText}</button>
            </div>
        `;

        overlay.appendChild(modal);
        document.body.appendChild(overlay);

        // Store references
        this.currentOverlay = overlay;
        this.currentModal = modal;

        // Show modal with animation
        setTimeout(() => {
            overlay.classList.add('show');
        }, 10);

        // Handle close button
        const closeBtn = modal.querySelector('.modal-close');
        if (closeBtn) {
            closeBtn.addEventListener('click', (e) => {
                e.preventDefault();
                e.stopPropagation();
                console.log('Close button clicked');
                this.hide();
                if (onCancel) onCancel();
            });
        }

        // Handle cancel button
        if (showCancel) {
            const cancelBtn = modal.querySelector('#modalCancelBtn');
            if (cancelBtn) {
                cancelBtn.addEventListener('click', (e) => {
                    e.preventDefault();
                    e.stopPropagation();
                    console.log('Cancel button clicked');
                    this.hide();
                    if (onCancel) onCancel();
                });
            }
        }

        // Handle confirm button
        const confirmBtn = modal.querySelector('#modalConfirmBtn');
        if (confirmBtn) {
            confirmBtn.addEventListener('click', (e) => {
                e.preventDefault();
                e.stopPropagation();
                
                console.log('Confirm button clicked');
                
                // Hide modal immediately
                this.hide();
                
                // Call confirm callback
                if (onConfirm) {
                    console.log('Calling onConfirm');
                    onConfirm();
                }
            });
        }

        // Handle overlay click
        overlay.addEventListener('click', (e) => {
            if (e.target === overlay) {
                console.log('Overlay clicked');
                this.hide();
                if (onCancel) onCancel();
            }
        });

        // Handle Escape key
        const handleEscape = (e) => {
            if (e.key === 'Escape') {
                console.log('Escape key pressed');
                document.removeEventListener('keydown', handleEscape);
                this.hide();
                if (onCancel) onCancel();
            }
        };
        document.addEventListener('keydown', handleEscape);
        this.escapeHandler = handleEscape;
    },

    // Hide modal
    hide: function() {
        console.log('Modal.hide called');
        
        // Remove overlay with animation
        if (this.currentOverlay) {
            this.currentOverlay.classList.remove('show');
            
            setTimeout(() => {
                if (this.currentOverlay && this.currentOverlay.parentNode) {
                    this.currentOverlay.parentNode.removeChild(this.currentOverlay);
                    console.log('Removed current overlay');
                }
            }, 300);
        }
        
        // Also find and remove any other overlays
        const overlays = document.querySelectorAll('.modal-overlay');
        overlays.forEach(overlay => {
            if (overlay !== this.currentOverlay && overlay.parentNode) {
                overlay.classList.remove('show');
                setTimeout(() => {
                    if (overlay.parentNode) {
                        overlay.parentNode.removeChild(overlay);
                    }
                }, 300);
            }
        });
        
        // Remove escape key handler
        if (this.escapeHandler) {
            document.removeEventListener('keydown', this.escapeHandler);
            this.escapeHandler = null;
        }
        
        // Clear references
        this.currentOverlay = null;
        this.currentModal = null;
    }
};

// Toast notification system
const Toast = {
    show: function(message, type = 'success', duration = 3000) {
        // Create container if not exists
        let container = document.querySelector('.toast-container');
        if (!container) {
            container = document.createElement('div');
            container.className = 'toast-container';
            document.body.appendChild(container);
        }

        // Set icon based on type
        let icon = 'fa-check-circle';
        let title = type.charAt(0).toUpperCase() + type.slice(1);
        
        if (type === 'error') icon = 'fa-exclamation-circle';
        if (type === 'warning') icon = 'fa-exclamation-triangle';
        if (type === 'info') icon = 'fa-info-circle';

        // Create toast
        const toast = document.createElement('div');
        toast.className = `toast ${type}`;

        toast.innerHTML = `
            <div class="toast-icon">
                <i class="fas ${icon}"></i>
            </div>
            <div class="toast-content">
                <div class="toast-title">${title}</div>
                <div class="toast-message">${message}</div>
            </div>
            <button class="toast-close">
                <i class="fas fa-times"></i>
            </button>
        `;

        container.appendChild(toast);

        // Show toast with animation
        setTimeout(() => {
            toast.classList.add('show');
        }, 10);

        // Close button
        const closeBtn = toast.querySelector('.toast-close');
        closeBtn.addEventListener('click', () => {
            toast.classList.remove('show');
            setTimeout(() => {
                if (toast.parentNode) {
                    toast.parentNode.removeChild(toast);
                    if (container.children.length === 0) {
                        container.parentNode.removeChild(container);
                    }
                }
            }, 300);
        });

        // Auto remove
        setTimeout(() => {
            if (toast.parentNode) {
                toast.classList.remove('show');
                setTimeout(() => {
                    if (toast.parentNode) {
                        toast.parentNode.removeChild(toast);
                        if (container.children.length === 0) {
                            container.parentNode.removeChild(container);
                        }
                    }
                }, 300);
            }
        }, duration);
    },

    success: function(message, duration = 3000) {
        this.show(message, 'success', duration);
    },

    error: function(message, duration = 3000) {
        this.show(message, 'error', duration);
    },

    warning: function(message, duration = 3000) {
        this.show(message, 'warning', duration);
    },

    info: function(message, duration = 3000) {
        this.show(message, 'info', duration);
    }
};

window.Modal = Modal;
window.Toast = Toast;

console.log('Modal and Toast systems loaded successfully - Using CSS classes');