// wwwroot/js/confirmation-dialog.js
// Global confirmation dialog system for beautiful user interactions

class ConfirmationDialog {
    constructor() {
        this.isInitialized = false;
        this.currentResolve = null;
        this.init();
    }

    init() {
        if (this.isInitialized) return;
        
        // Create the modal HTML
        this.createModal();
        this.isInitialized = true;
    }

    createModal() {
        const modalHtml = `
            <div class="modal fade" id="confirmationModal" tabindex="-1" aria-labelledby="confirmationModalLabel" aria-hidden="true" data-bs-backdrop="static">
                <div class="modal-dialog modal-dialog-centered">
                    <div class="modal-content shadow-lg border-0">
                        <div class="modal-header border-0 pb-0">
                            <h5 class="modal-title d-flex align-items-center" id="confirmationModalLabel">
                                <span id="confirmationIcon" class="me-2"></span>
                                <span id="confirmationTitle">Confirm Action</span>
                            </h5>
                        </div>
                        <div class="modal-body pt-2">
                            <div id="confirmationMessage" class="fs-6"></div>
                            <div id="confirmationDetails" class="text-muted small mt-2" style="display: none;"></div>
                        </div>
                        <div class="modal-footer border-0 pt-1">
                            <button type="button" class="btn btn-outline-secondary" id="confirmationCancel">
                                <i class="fas fa-times me-1"></i> Cancel
                            </button>
                            <button type="button" class="btn btn-danger" id="confirmationConfirm">
                                <i class="fas fa-check me-1"></i> Confirm
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        // Remove existing modal if it exists
        const existingModal = document.getElementById('confirmationModal');
        if (existingModal) {
            existingModal.remove();
        }

        // Add modal to page
        document.body.insertAdjacentHTML('beforeend', modalHtml);

        // Set up event listeners
        this.setupEventListeners();
    }

    setupEventListeners() {
        const modal = document.getElementById('confirmationModal');
        const cancelBtn = document.getElementById('confirmationCancel');
        const confirmBtn = document.getElementById('confirmationConfirm');

        // Handle cancel
        cancelBtn.addEventListener('click', () => {
            this.resolve(false);
        });

        // Handle confirmation
        confirmBtn.addEventListener('click', () => {
            this.resolve(true);
        });

        // Handle modal close events
        modal.addEventListener('hidden.bs.modal', () => {
            if (this.currentResolve) {
                this.resolve(false);
            }
        });

        // Handle escape key
        modal.addEventListener('keydown', (e) => {
            if (e.key === 'Escape') {
                this.resolve(false);
            }
        });
    }

    async confirm(options = {}) {
        const {
            title = 'Confirm Action',
            message = 'Are you sure you want to continue?',
            details = null,
            confirmText = 'Confirm',
            cancelText = 'Cancel',
            confirmClass = 'btn-danger',
            icon = 'fas fa-question-circle text-warning',
            destructive = false
        } = options;

        return new Promise((resolve) => {
            this.currentResolve = resolve;

            // Update modal content
            document.getElementById('confirmationTitle').textContent = title;
            document.getElementById('confirmationMessage').innerHTML = message;
            document.getElementById('confirmationIcon').innerHTML = `<i class="${icon}"></i>`;
            
            // Handle details
            const detailsElement = document.getElementById('confirmationDetails');
            if (details) {
                detailsElement.innerHTML = details;
                detailsElement.style.display = 'block';
            } else {
                detailsElement.style.display = 'none';
            }

            // Update button text and styling
            const confirmBtn = document.getElementById('confirmationConfirm');
            const cancelBtn = document.getElementById('confirmationCancel');
            
            confirmBtn.innerHTML = `<i class="fas fa-check me-1"></i> ${confirmText}`;
            confirmBtn.className = `btn ${confirmClass}`;
            
            cancelBtn.innerHTML = `<i class="fas fa-times me-1"></i> ${cancelText}`;

            // Add destructive styling if needed
            if (destructive) {
                confirmBtn.className = 'btn btn-danger';
                confirmBtn.innerHTML = `<i class="fas fa-trash me-1"></i> ${confirmText}`;
            }

            // Show modal
            const modal = new bootstrap.Modal(document.getElementById('confirmationModal'), {
                backdrop: 'static',
                keyboard: false
            });
            modal.show();
        });
    }

    resolve(result) {
        if (this.currentResolve) {
            const modal = bootstrap.Modal.getInstance(document.getElementById('confirmationModal'));
            if (modal) {
                modal.hide();
            }
            
            this.currentResolve(result);
            this.currentResolve = null;
        }
    }

    // Convenience methods for common confirmation types
    async confirmDelete(itemName = 'item', details = null) {
        return this.confirm({
            title: 'Delete Confirmation',
            message: `Are you sure you want to delete this ${itemName}?`,
            details: details || 'This action cannot be undone.',
            confirmText: 'Delete',
            cancelText: 'Keep',
            confirmClass: 'btn-danger',
            icon: 'fas fa-trash text-danger',
            destructive: true
        });
    }

    async confirmRemove(itemName = 'item', details = null) {
        return this.confirm({
            title: 'Remove Confirmation',
            message: `Are you sure you want to remove this ${itemName}?`,
            details: details,
            confirmText: 'Remove',
            cancelText: 'Keep',
            confirmClass: 'btn-warning',
            icon: 'fas fa-exclamation-triangle text-warning'
        });
    }

    async confirmAction(action = 'action', details = null) {
        return this.confirm({
            title: 'Confirm Action',
            message: `Are you sure you want to ${action}?`,
            details: details,
            confirmText: 'Proceed',
            cancelText: 'Cancel',
            confirmClass: 'btn-primary',
            icon: 'fas fa-question-circle text-info'
        });
    }

    async confirmNavigation(message = 'You have unsaved changes. Are you sure you want to leave?') {
        return this.confirm({
            title: 'Unsaved Changes',
            message: message,
            details: 'Any unsaved changes will be lost.',
            confirmText: 'Leave',
            cancelText: 'Stay',
            confirmClass: 'btn-warning',
            icon: 'fas fa-exclamation-triangle text-warning'
        });
    }
}

// Global instance
window.confirmDialog = new ConfirmationDialog();

// Convenience global functions
window.confirmDelete = (itemName, details) => window.confirmDialog.confirmDelete(itemName, details);
window.confirmRemove = (itemName, details) => window.confirmDialog.confirmRemove(itemName, details);
window.confirmAction = (action, details) => window.confirmDialog.confirmAction(action, details);
window.confirmNavigation = (message) => window.confirmDialog.confirmNavigation(message);

// Enhanced form submission helper
window.confirmFormSubmission = async (form, options = {}) => {
    const confirmed = await window.confirmDialog.confirm(options);
    if (confirmed && form) {
        // Show loading state if needed
        if (options.showLoading !== false) {
            const submitBtn = form.querySelector('button[type="submit"]');
            if (submitBtn) {
                const originalText = submitBtn.innerHTML;
                submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i> Processing...';
                submitBtn.disabled = true;
                
                // Restore button after a delay if form doesn't submit
                setTimeout(() => {
                    if (submitBtn.disabled) {
                        submitBtn.innerHTML = originalText;
                        submitBtn.disabled = false;
                    }
                }, 5000);
            }
        }
        form.submit();
    }
    return confirmed;
};

// Auto-replace standard confirm dialogs
document.addEventListener('DOMContentLoaded', function() {
    // Replace onclick confirm handlers
    document.querySelectorAll('[onclick*="confirm("]').forEach(element => {
        const onclick = element.getAttribute('onclick');
        if (onclick && onclick.includes('confirm(')) {
            element.removeAttribute('onclick');
            element.addEventListener('click', async function(e) {
                e.preventDefault();
                
                // Extract message from confirm() call
                const match = onclick.match(/confirm\(['"`]([^'"`]+)['"`]\)/);
                const message = match ? match[1] : 'Are you sure?';
                
                const confirmed = await window.confirmDialog.confirm({
                    message: message,
                    icon: 'fas fa-question-circle text-warning'
                });
                
                if (confirmed) {
                    // Execute the rest of the onclick handler
                    const restOfHandler = onclick.replace(/return\s+confirm\([^)]+\)\s*[;&]?\s*/, '');
                    if (restOfHandler.trim()) {
                        eval(restOfHandler);
                    }
                }
            });
        }
    });
});