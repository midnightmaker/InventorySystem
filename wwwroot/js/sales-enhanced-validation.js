// wwwroot/js/sales-enhanced-validation.js

// Enhanced sales validation and UI interactions
class SalesEnhancedValidation {
    constructor() {
        this.initializeToastHandling();
        this.initializeAdjustmentValidation();
        this.initializeServiceOrderPrompts();
        this.initializePackAndShipValidation();
    }

    // Initialize toast notification handling
    initializeToastHandling() {
        // Check for toast messages in TempData
        const toastMessage = document.body.dataset.toastMessage;
        const toastType = document.body.dataset.toastType || 'info';

        if (toastMessage) {
            this.showToast(toastMessage, toastType);
        }
    }

    // Initialize Create Adjustment button validation
    initializeAdjustmentValidation() {
        const adjustmentButton = document.querySelector('a[href*="CreateCustomerAdjustment"]');
        
        if (adjustmentButton) {
            adjustmentButton.addEventListener('click', (e) => {
                e.preventDefault();
                this.validateAdjustmentCreation(adjustmentButton.href);
            });
        }
    }

    // Initialize service order creation prompts
    initializeServiceOrderPrompts() {
        // Check if service order prompt is needed
        const serviceOrderPrompt = document.body.dataset.serviceOrderPrompt;
        
        if (serviceOrderPrompt === 'true') {
            this.showServiceOrderCreationPrompt();
        }
    }

    // Initialize Pack and Ship button validation
    initializePackAndShipValidation() {
        const packShipButton = document.querySelector('button[data-bs-target="#processSaleModal"]');
        
        if (packShipButton) {
            packShipButton.addEventListener('click', (e) => {
                // Additional validation can be added here
                this.validatePackAndShip();
            });
        }
    }

    // Validate adjustment creation
    async validateAdjustmentCreation(adjustmentUrl) {
        try {
            const saleId = this.extractSaleIdFromUrl(adjustmentUrl);
            const response = await fetch(`/Sales/ValidateAdjustmentCreation?saleId=${saleId}`);
            const result = await response.json();

            if (result.success && result.canCreate) {
                // Allow navigation to adjustment creation
                window.location.href = adjustmentUrl;
            } else {
                // Show toast notification
                this.showToast(result.message, 'warning');
            }
        } catch (error) {
            console.error('Error validating adjustment creation:', error);
            this.showToast('Error validating adjustment creation. Please try again.', 'error');
        }
    }

    // Show beautiful service order creation prompt
    showServiceOrderCreationPrompt() {
        const missingServiceOrders = JSON.parse(document.body.dataset.missingServiceOrders || '[]');
        const serviceOrderUrl = document.body.dataset.serviceOrderCreationUrl;

        // Create beautiful modal
        const modalHtml = `
            <div class="modal fade" id="serviceOrderPromptModal" tabindex="-1" aria-hidden="true">
                <div class="modal-dialog modal-lg">
                    <div class="modal-content border-primary">
                        <div class="modal-header bg-primary text-white">
                            <h5 class="modal-title">
                                <i class="fas fa-cogs"></i> Service Orders Required
                            </h5>
                            <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal"></button>
                        </div>
                        <div class="modal-body">
                            <div class="alert alert-info">
                                <i class="fas fa-info-circle"></i>
                                <strong>Service orders must be created before packing and shipping.</strong>
                            </div>
                            <p>This sale contains <strong>${missingServiceOrders.length}</strong> service item(s) that require service orders:</p>
                            <ul class="list-group list-group-flush">
                                ${missingServiceOrders.map(so => `
                                    <li class="list-group-item d-flex justify-content-between align-items-center">
                                        <div>
                                            <strong>${so.ServiceTypeName}</strong>
                                            <br><small class="text-muted">${so.ItemName}</small>
                                        </div>
                                        <span class="badge bg-warning rounded-pill">Pending</span>
                                    </li>
                                `).join('')}
                            </ul>
                            <div class="mt-3">
                                <p class="mb-2"><strong>What happens when you create service orders:</strong></p>
                                <ul class="small text-muted">
                                    <li>Service requests will be tracked and managed</li>
                                    <li>Technicians can be assigned and scheduled</li>
                                    <li>Service completion can be monitored</li>
                                    <li>Sale can be packed and shipped once services are complete</li>
                                </ul>
                            </div>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">
                                <i class="fas fa-times"></i> Cancel
                            </button>
                            <a href="${serviceOrderUrl}" class="btn btn-primary">
                                <i class="fas fa-plus"></i> Create Service Orders
                            </a>
                        </div>
                    </div>
                </div>
            </div>
        `;

        // Add modal to page and show
        document.body.insertAdjacentHTML('beforeend', modalHtml);
        const modal = new bootstrap.Modal(document.getElementById('serviceOrderPromptModal'));
        modal.show();

        // Clean up modal when closed
        document.getElementById('serviceOrderPromptModal').addEventListener('hidden.bs.modal', function() {
            this.remove();
        });
    }

    // Validate pack and ship operation
    validatePackAndShip() {
        // Additional client-side validation can be added here
        console.log('Validating pack and ship operation...');
    }

    // Show toast notification
    showToast(message, type = 'info') {
        // Create toast container if it doesn't exist
        let toastContainer = document.getElementById('toast-container');
        if (!toastContainer) {
            toastContainer = document.createElement('div');
            toastContainer.id = 'toast-container';
            toastContainer.className = 'toast-container position-fixed top-0 end-0 p-3';
            toastContainer.style.zIndex = '9999';
            document.body.appendChild(toastContainer);
        }

        const toastId = 'toast-' + Date.now();
        const bgClass = this.getToastBgClass(type);
        const icon = this.getToastIcon(type);

        const toastHtml = `
            <div id="${toastId}" class="toast ${bgClass} text-white" role="alert" aria-live="assertive" aria-atomic="true" data-bs-delay="5000">
                <div class="toast-header ${bgClass} text-white border-0">
                    <i class="${icon} me-2"></i>
                    <strong class="me-auto">${this.getToastTitle(type)}</strong>
                    <button type="button" class="btn-close btn-close-white" data-bs-dismiss="toast" aria-label="Close"></button>
                </div>
                <div class="toast-body">
                    ${message}
                </div>
            </div>
        `;

        toastContainer.insertAdjacentHTML('beforeend', toastHtml);
        const toastElement = document.getElementById(toastId);
        const toast = new bootstrap.Toast(toastElement);
        toast.show();

        // Clean up after toast is hidden
        toastElement.addEventListener('hidden.bs.toast', function() {
            this.remove();
        });
    }

    // Helper methods for toast styling
    getToastBgClass(type) {
        switch (type) {
            case 'success': return 'bg-success';
            case 'warning': return 'bg-warning';
            case 'error': return 'bg-danger';
            default: return 'bg-info';
        }
    }

    getToastIcon(type) {
        switch (type) {
            case 'success': return 'fas fa-check-circle';
            case 'warning': return 'fas fa-exclamation-triangle';
            case 'error': return 'fas fa-times-circle';
            default: return 'fas fa-info-circle';
        }
    }

    getToastTitle(type) {
        switch (type) {
            case 'success': return 'Success';
            case 'warning': return 'Warning';
            case 'error': return 'Error';
            default: return 'Information';
        }
    }

    // Extract sale ID from URL
    extractSaleIdFromUrl(url) {
        const match = url.match(/saleId=(\d+)/);
        return match ? match[1] : null;
    }
}

// Initialize when DOM is loaded
document.addEventListener('DOMContentLoaded', function() {
    new SalesEnhancedValidation();
});