class ServiceStatusModal {
    constructor() {
        this.modalId = 'serviceStatusModal';
        this.currentModal = null;
        this.onSuccessCallback = null;
    }

    async show(serviceOrderId, onSuccess = null) {
        this.onSuccessCallback = onSuccess;
        
        try {
            this.cleanup();

            const response = await fetch(`/Services/GetStatusUpdateModal?serviceOrderId=${serviceOrderId}`);
            if (!response.ok) {
                throw new Error(`Failed to load modal: ${response.statusText}`);
            }

            const html = await response.text();
            document.body.insertAdjacentHTML('beforeend', html);

            const modalElement = document.getElementById(this.modalId);
            this.currentModal = new bootstrap.Modal(modalElement, {
                backdrop: 'static',
                keyboard: true,
                focus: true
            });

            this.setupEventHandlers();
            this.currentModal.show();

        } catch (error) {
            console.error('Error showing status modal:', error);
            this.showToast('Error loading status update form', 'error');
        }
    }

    setupEventHandlers() {
        const modalElement = document.getElementById(this.modalId);
        const form = modalElement.querySelector('form');

        // Form submission handler
        form.addEventListener('submit', async (e) => {
            e.preventDefault();
            await this.handleSubmit(form);
        });

        // Status change handler
        const statusSelect = form.querySelector('select[name="NewStatus"]');
        if (statusSelect) {
            statusSelect.addEventListener('change', () => this.handleStatusChange());
        }

        modalElement.addEventListener('hidden.bs.modal', () => {
            this.cleanup();
        }, { once: true });

        modalElement.addEventListener('shown.bs.modal', () => {
            const firstInput = modalElement.querySelector('select, input, textarea');
            if (firstInput) firstInput.focus();
        });
    }

    async handleSubmit(form) {
        const submitBtn = form.querySelector('button[type="submit"]');
        const originalText = submitBtn ? submitBtn.innerHTML : '';
        
        try {
            this.hideError();

            // Show loading state
            if (submitBtn) {
                submitBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Updating...';
                submitBtn.disabled = true;
            }

            const formData = new FormData(form);
            const response = await fetch(form.action, {
                method: 'POST',
                body: formData
            });

            const result = await response.json();

            if (result.success) {
                // Only restore button on success after showing success message
                if (submitBtn) {
                    submitBtn.innerHTML = originalText;
                    submitBtn.disabled = false;
                }
                
                this.showSuccess(result.message);
                
                setTimeout(() => {
                    this.currentModal.hide();
                    if (this.onSuccessCallback) {
                        this.onSuccessCallback(result);
                    }
                }, 1500);
            } else {
                // IMMEDIATELY restore button state on validation error
                if (submitBtn) {
                    submitBtn.innerHTML = originalText;
                    submitBtn.disabled = false;
                }
                
                // Remove any loading overlays that might be blocking the view
                this.removeLoadingOverlays();
                
                // Then show error
                this.showError(result.message);
                this.ensureModalVisible();
                this.scrollErrorIntoView();
            }

        } catch (error) {
            console.error('Error in form submission:', error);
            
            // IMMEDIATELY restore button state on error
            if (submitBtn) {
                submitBtn.innerHTML = originalText;
                submitBtn.disabled = false;
            }
            
            // Remove any loading overlays that might be blocking the view
            this.removeLoadingOverlays();
            
            this.showError(`Error updating status: ${error.message}`);
            this.ensureModalVisible();
            this.scrollErrorIntoView();
        }
    }

    removeLoadingOverlays() {
        // Remove loading overlays from the entire document, not just the modal
        const loadingOverlays = document.querySelectorAll('.loading-overlay, .spinner-overlay, .processing-overlay, [class*="loading"], [class*="spinner"], [class*="processing"]');
        loadingOverlays.forEach(overlay => {
            // Only remove if it's actually showing a loading state
            if (overlay.textContent.includes('Processing') || 
                overlay.textContent.includes('Submitting') || 
                overlay.textContent.includes('Loading') ||
                overlay.querySelector('.spinner-border') ||
                overlay.querySelector('[class*="spinner"]')) {
                overlay.remove();
            }
        });
        
        // Also hide any elements that might be blocking with specific styles
        const blockingElements = document.querySelectorAll('[style*="position: fixed"], [style*="position: absolute"]');
        blockingElements.forEach(element => {
            if (element.style.zIndex && parseInt(element.style.zIndex) > 1050 && 
                (element.textContent.includes('Processing') || 
                 element.textContent.includes('Submitting'))) {
                element.style.display = 'none';
            }
        });
        
        // Remove blur effects
        this.removeBlurEffects();
    }

    removeBlurEffects() {
        // Remove blur from body
        document.body.classList.remove('modal-open', 'blur', 'loading', 'processing');
        document.body.style.filter = '';
        document.body.style.backdropFilter = '';
        
        // Remove blur from modal
        const modalElement = document.getElementById(this.modalId);
        if (modalElement) {
            modalElement.style.filter = '';
            modalElement.style.backdropFilter = '';
            modalElement.classList.remove('blur', 'loading', 'processing');
            
            // Remove blur from modal content
            const modalContent = modalElement.querySelector('.modal-content');
            if (modalContent) {
                modalContent.style.filter = '';
                modalContent.style.backdropFilter = '';
                modalContent.classList.remove('blur', 'loading', 'processing');
            }
            
            // Remove blur from modal dialog
            const modalDialog = modalElement.querySelector('.modal-dialog');
            if (modalDialog) {
                modalDialog.style.filter = '';
                modalDialog.style.backdropFilter = '';
                modalDialog.classList.remove('blur', 'loading', 'processing');
            }
        }
        
        // Remove any elements with blur styles
        const blurredElements = document.querySelectorAll('[style*="blur"], [style*="filter"], .blur, .blurred');
        blurredElements.forEach(element => {
            element.style.filter = '';
            element.style.backdropFilter = '';
            element.classList.remove('blur', 'blurred', 'loading', 'processing');
        });
    }

    // ? NEW: Ensure modal content is visible and not obscured
    ensureModalVisible() {
        const modalElement = document.getElementById(this.modalId);
        if (!modalElement) return;

        // Remove any lingering loading overlays or backdrops that might be obscuring content
        const loadingOverlays = modalElement.querySelectorAll('.loading-overlay, .spinner-overlay');
        loadingOverlays.forEach(overlay => overlay.remove());

        // Ensure modal content is visible
        const modalContent = modalElement.querySelector('.modal-content');
        if (modalContent) {
            modalContent.style.opacity = '1';
            modalContent.style.visibility = 'visible';
            modalContent.style.position = 'relative';
            modalContent.style.zIndex = '1050';
        }

        // Ensure modal backdrop doesn't obscure content
        const modalBackdrop = document.querySelector('.modal-backdrop');
        if (modalBackdrop) {
            modalBackdrop.style.zIndex = '1040';
        }

        // Force re-render to ensure visibility
        modalElement.style.display = 'block';
        modalElement.classList.add('show');
    }

    handleStatusChange() {
        const form = document.querySelector(`#${this.modalId} form`);
        const statusSelect = form.querySelector('select[name="NewStatus"]');
        const selectedStatus = statusSelect.value;

        const conditionalFields = form.querySelectorAll('.conditional-field');
        conditionalFields.forEach(field => field.style.display = 'none');

        switch (selectedStatus) {
            case 'Scheduled':
                this.showField('schedulingFields');
                break;
            case 'QualityCheck':
                this.showField('qualityCheckFields');
                break;
            case 'Completed':
                this.showField('completionFields');
                break;
            case 'OnHold':
            case 'Cancelled':
                this.showField('holdCancelFields');
                this.setReasonRequired(true);
                break;
            default:
                this.setReasonRequired(false);
                break;
        }
    }

    showField(fieldId) {
        const field = document.getElementById(fieldId);
        if (field) field.style.display = 'block';
    }

    setReasonRequired(required) {
        const reasonField = document.querySelector(`#${this.modalId} textarea[name="Reason"]`);
        const requiredIndicator = document.getElementById('reasonRequired');
        
        if (reasonField) reasonField.required = required;
        if (requiredIndicator) {
            requiredIndicator.style.display = required ? 'inline' : 'none';
        }
    }

    showError(message) {
        const modalBody = document.querySelector(`#${this.modalId} .modal-body`);
        if (!modalBody) return;
        
        // Remove existing alerts
        const existingAlerts = modalBody.querySelectorAll('.alert');
        existingAlerts.forEach(alert => alert.remove());

        // ? ENHANCED: High visibility error with proper z-index
        const errorHtml = `
            <div class="alert alert-danger alert-dismissible fade show" style="position: relative; z-index: 1055;">
                <i class="fas fa-exclamation-circle"></i>
                <strong>Error:</strong> ${message}
                <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
            </div>
        `;
        
        modalBody.insertAdjacentHTML('afterbegin', errorHtml);
        
        // ? ENHANCED: Ensure error is visible
        const errorAlert = modalBody.querySelector('.alert-danger');
        if (errorAlert) {
            errorAlert.style.opacity = '1';
            errorAlert.style.visibility = 'visible';
        }
    }

    showSuccess(message) {
        const modalBody = document.querySelector(`#${this.modalId} .modal-body`);
        
        const existingAlerts = modalBody.querySelectorAll('.alert');
        existingAlerts.forEach(alert => alert.remove());

        const successHtml = `
            <div class="alert alert-success alert-dismissible fade show">
                <i class="fas fa-check-circle"></i>
                <strong>Success:</strong> ${message}
                <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
            </div>
        `;
        modalBody.insertAdjacentHTML('afterbegin', successHtml);
    }

    hideError() {
        const alerts = document.querySelectorAll(`#${this.modalId} .alert`);
        alerts.forEach(alert => alert.remove());
    }

    scrollErrorIntoView() {
        const modalBody = document.querySelector(`#${this.modalId} .modal-body`);
        if (modalBody) {
            modalBody.scrollTop = 0;
            
            // Ensure modal body is scrollable
            modalBody.style.overflowY = 'auto';
            modalBody.style.maxHeight = modalBody.style.maxHeight || '70vh';
        }
    }

    cleanup() {
        // ? ENHANCED: Thorough cleanup including any overlays
        const existingModal = document.getElementById(this.modalId);
        if (existingModal) {
            // Remove any loading overlays
            const overlays = existingModal.querySelectorAll('.loading-overlay, .spinner-overlay');
            overlays.forEach(overlay => overlay.remove());
            
            const modalInstance = bootstrap.Modal.getInstance(existingModal);
            if (modalInstance) modalInstance.dispose();
            existingModal.remove();
        }
        
        // Clean up any orphaned backdrops
        const backdrops = document.querySelectorAll('.modal-backdrop');
        backdrops.forEach(backdrop => backdrop.remove());
        
        this.currentModal = null;
        this.onSuccessCallback = null;
    }

    showToast(message, type = 'info') {
        console.log(`${type.toUpperCase()}: ${message}`);
    }
}

window.ServiceStatusModal = new ServiceStatusModal();

window.updateServiceStatus = (serviceOrderId, onSuccess) => {
    window.ServiceStatusModal.show(serviceOrderId, onSuccess);
};