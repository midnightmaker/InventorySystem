document.addEventListener('DOMContentLoaded', function() {
    // Attach to all status dropdowns
    document.querySelectorAll('.status-updater').forEach(select => {
        select.addEventListener('change', function() {
            const orderId = this.dataset.orderId;
            const currentStatus = this.dataset.currentStatus;
            const orderNumber = this.dataset.orderNumber || `Order #${orderId}`;

            if (this.value) {
                statusUpdateModal.show(orderId, currentStatus, orderNumber);
            }

            // Reset dropdown
            this.value = '';
        });
    });
});

class StatusUpdateModal {
    constructor() {
        this.modalId = 'statusUpdateModal';
        this.currentServiceOrderId = null;
        this.technicians = ['John Smith', 'Jane Doe', 'Mike Johnson', 'Sarah Wilson'];
    }

    async show(serviceOrderId, currentStatus, serviceOrderNumber) {
        this.currentServiceOrderId = serviceOrderId;
        
        // Create the modal HTML
        this.createModal(currentStatus, serviceOrderNumber);
        
        // Show the modal
        const modal = new bootstrap.Modal(document.getElementById(this.modalId));
        modal.show();
        
        // Setup event handlers
        this.setupEventHandlers();
    }

    createModal(currentStatus, serviceOrderNumber) {
        // Remove existing modal if any
        const existingModal = document.getElementById(this.modalId);
        if (existingModal) {
            existingModal.remove();
        }

        const modalHtml = `
            <div class="modal fade" id="${this.modalId}" tabindex="-1" aria-labelledby="${this.modalId}Label">
                <div class="modal-dialog modal-lg">
                    <div class="modal-content">
                        <div class="modal-header bg-primary text-white">
                            <h5 class="modal-title" id="${this.modalId}Label">
                                <i class="fas fa-flag"></i> Update Service Status
                                ${serviceOrderNumber ? `<span class="ms-2 badge bg-light text-dark">${serviceOrderNumber}</span>` : ''}
                            </h5>
                            <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal"></button>
                        </div>
                        <div class="modal-body">
                            <div class="alert alert-info">
                                <i class="fas fa-info-circle"></i>
                                <strong>Current Status:</strong> 
                                <span class="badge bg-primary ms-2">${currentStatus}</span>
                            </div>

                            <form id="statusUpdateForm">
                                <div class="row">
                                    <div class="col-md-6 mb-3">
                                        <label class="form-label">
                                            <i class="fas fa-flag"></i> New Status <span class="text-danger">*</span>
                                        </label>
                                        <select id="newStatus" class="form-select" required>
                                            <option value="">Select new status...</option>
                                            <option value="InProgress">
                                                <i class="fas fa-play"></i> In Progress
                                            </option>
                                            <option value="Scheduled">
                                                <i class="fas fa-calendar"></i> Scheduled
                                            </option>
                                            <option value="QualityCheck">
                                                <i class="fas fa-search"></i> Quality Check
                                            </option>
                                            <option value="Completed">
                                                <i class="fas fa-check"></i> Completed
                                            </option>
                                            <option value="OnHold">
                                                <i class="fas fa-pause"></i> On Hold
                                            </option>
                                            <option value="Cancelled">
                                                <i class="fas fa-times"></i> Cancelled
                                            </option>
                                        </select>
                                    </div>
                                    <div class="col-md-6 mb-3">
                                        <label class="form-label">
                                            <i class="fas fa-exclamation-triangle"></i> Priority
                                        </label>
                                        <select id="priority" class="form-select">
                                            <option value="">Keep current priority</option>
                                            <option value="Low">Low</option>
                                            <option value="Normal">Normal</option>
                                            <option value="High">High</option>
                                            <option value="Urgent">Urgent</option>
                                            <option value="Emergency">Emergency</option>
                                        </select>
                                    </div>
                                </div>

                                <!-- Conditional Fields Container -->
                                <div id="conditionalFields"></div>

                                <div class="mb-3">
                                    <label class="form-label">
                                        <i class="fas fa-comment"></i> Notes (Optional)
                                    </label>
                                    <textarea id="statusNotes" class="form-control" rows="3" 
                                              placeholder="Add any notes about this status change..."></textarea>
                                </div>
                            </form>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">
                                <i class="fas fa-times"></i> Cancel
                            </button>
                            <button type="button" class="btn btn-primary" id="updateStatusBtn">
                                <i class="fas fa-save"></i> Update Status
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        document.body.insertAdjacentHTML('beforeend', modalHtml);
    }

    setupEventHandlers() {
        const newStatusSelect = document.getElementById('newStatus');
        const updateBtn = document.getElementById('updateStatusBtn');

        // Handle status change to show/hide conditional fields
        newStatusSelect.addEventListener('change', () => {
            this.handleStatusChange(newStatusSelect.value);
        });

        // Handle form submission
        updateBtn.addEventListener('click', () => {
            this.handleSubmit();
        });

        // Close modal cleanup
        document.getElementById(this.modalId).addEventListener('hidden.bs.modal', () => {
            document.getElementById(this.modalId).remove();
        });
    }

    handleStatusChange(newStatus) {
        const conditionalFields = document.getElementById('conditionalFields');
        
        switch (newStatus) {
            case 'Scheduled':
                conditionalFields.innerHTML = `
                    <div class="card border-info mb-3">
                        <div class="card-header bg-info text-white">
                            <i class="fas fa-calendar-alt"></i> Scheduling Information
                        </div>
                        <div class="card-body">
                            <div class="row">
                                <div class="col-md-6 mb-3">
                                    <label class="form-label">Scheduled Date & Time <span class="text-danger">*</span></label>
                                    <input type="datetime-local" id="scheduledDateTime" class="form-control" required>
                                </div>
                                <div class="col-md-6 mb-3">
                                    <label class="form-label">Assign Technician</label>
                                    <select id="assignedTechnician" class="form-select">
                                        <option value="">Select technician...</option>
                                        ${this.technicians.map(tech => `<option value="${tech}">${tech}</option>`).join('')}
                                    </select>
                                </div>
                            </div>
                            <div class="mb-3">
                                <label class="form-label">Work Location</label>
                                <input type="text" id="workLocation" class="form-control" placeholder="e.g., Lab 1, Customer Site, etc.">
                            </div>
                        </div>
                    </div>
                `;
                break;

            case 'OnHold':
            case 'Cancelled':
                conditionalFields.innerHTML = `
                    <div class="card border-warning mb-3">
                        <div class="card-header bg-warning text-dark">
                            <i class="fas fa-exclamation-triangle"></i> ${newStatus === 'OnHold' ? 'Hold' : 'Cancellation'} Reason Required
                        </div>
                        <div class="card-body">
                            <div class="mb-3">
                                <label class="form-label">Reason <span class="text-danger">*</span></label>
                                <textarea id="statusReason" class="form-control" rows="3" required
                                          placeholder="Please provide a detailed reason for ${newStatus.toLowerCase()}..."></textarea>
                            </div>
                            ${newStatus === 'OnHold' ? `
                                <div class="mb-3">
                                    <label class="form-label">Expected Resume Date</label>
                                    <input type="date" id="expectedResumeDate" class="form-control">
                                </div>
                            ` : ''}
                        </div>
                    </div>
                `;
                break;

            case 'QualityCheck':
                conditionalFields.innerHTML = `
                    <div class="card border-success mb-3">
                        <div class="card-header bg-success text-white">
                            <i class="fas fa-clipboard-check"></i> Quality Control Information
                        </div>
                        <div class="card-body">
                            <div class="row">
                                <div class="col-md-6 mb-3">
                                    <label class="form-label">QC Technician</label>
                                    <select id="qcTechnician" class="form-select">
                                        <option value="">Select QC technician...</option>
                                        ${this.technicians.map(tech => `<option value="${tech}">${tech}</option>`).join('')}
                                    </select>
                                </div>
                                <div class="col-md-6 mb-3">
                                    <label class="form-label">QC Date</label>
                                    <input type="date" id="qcDate" class="form-control" value="${new Date().toISOString().split('T')[0]}">
                                </div>
                            </div>
                            <div class="mb-3">
                                <label class="form-label">QC Notes</label>
                                <textarea id="qcNotes" class="form-control" rows="2" 
                                          placeholder="Quality control notes and observations..."></textarea>
                            </div>
                        </div>
                    </div>
                `;
                break;

            case 'Completed':
                conditionalFields.innerHTML = `
                    <div class="card border-success mb-3">
                        <div class="card-header bg-success text-white">
                            <i class="fas fa-check-circle"></i> Completion Information
                        </div>
                        <div class="card-body">
                            <div class="alert alert-info">
                                <i class="fas fa-info-circle"></i>
                                <strong>Note:</strong> Marking as completed will validate that all required documents and quality checks are in place.
                            </div>
                            <div class="row">
                                <div class="col-md-6 mb-3">
                                    <label class="form-label">Completion Date</label>
                                    <input type="date" id="completionDate" class="form-control" value="${new Date().toISOString().split('T')[0]}">
                                </div>
                                <div class="col-md-6 mb-3">
                                    <label class="form-label">Completed By</label>
                                    <select id="completedBy" class="form-select">
                                        <option value="">Select technician...</option>
                                        ${this.technicians.map(tech => `<option value="${tech}">${tech}</option>`).join('')}
                                    </select>
                                </div>
                            </div>
                            <div class="mb-3">
                                <label class="form-label">Work Summary</label>
                                <textarea id="workSummary" class="form-control" rows="3" 
                                          placeholder="Brief summary of work completed..."></textarea>
                            </div>
                        </div>
                    </div>
                `;
                break;

            default:
                conditionalFields.innerHTML = '';
                break;
        }
    }

    async handleSubmit() {
        const form = document.getElementById('statusUpdateForm');
        const newStatus = document.getElementById('newStatus').value;
        const updateBtn = document.getElementById('updateStatusBtn');

        if (!newStatus) {
            this.showAlert('Please select a new status.', 'warning');
            return;
        }

        // Validate required fields based on status
        if (!this.validateRequiredFields(newStatus)) {
            return;
        }

        // Show loading state
        updateBtn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Updating...';
        updateBtn.disabled = true;

        try {
            const formData = this.buildFormData(newStatus);
            
            const response = await fetch('/Services/UpdateStatus', {
                method: 'POST',
                body: formData
            });

            const result = await response.json();

            if (result.success) {
                this.showAlert(result.message, 'success');
                setTimeout(() => {
                    const modal = bootstrap.Modal.getInstance(document.getElementById(this.modalId));
                    modal.hide();
                    location.reload();
                }, 1500);
            } else {
                this.showAlert(result.message, 'danger');
                updateBtn.innerHTML = '<i class="fas fa-save"></i> Update Status';
                updateBtn.disabled = false;
            }
        } catch (error) {
            this.showAlert('Error updating status: ' + error.message, 'danger');
            updateBtn.innerHTML = '<i class="fas fa-save"></i> Update Status';
            updateBtn.disabled = false;
        }
    }

    validateRequiredFields(newStatus) {
        const requiredFields = [];

        switch (newStatus) {
            case 'Scheduled':
                if (!document.getElementById('scheduledDateTime').value) {
                    requiredFields.push('Scheduled Date & Time');
                }
                break;
            case 'OnHold':
            case 'Cancelled':
                if (!document.getElementById('statusReason').value.trim()) {
                    requiredFields.push('Reason');
                }
                break;
        }

        if (requiredFields.length > 0) {
            this.showAlert(`Please fill in the following required fields: ${requiredFields.join(', ')}`, 'warning');
            return false;
        }

        return true;
    }

    buildFormData(newStatus) {
        const formData = new FormData();
        formData.append('ServiceOrderId', this.currentServiceOrderId);
        formData.append('NewStatus', newStatus);

        // Add priority if changed
        const priority = document.getElementById('priority').value;
        if (priority) {
            formData.append('Priority', priority);
        }

        // Add notes
        const notes = document.getElementById('statusNotes').value;
        if (notes) {
            formData.append('Reason', notes);
        }

        // Add status-specific fields
        switch (newStatus) {
            case 'Scheduled':
                const scheduledDateTime = document.getElementById('scheduledDateTime').value;
                const assignedTechnician = document.getElementById('assignedTechnician').value;
                const workLocation = document.getElementById('workLocation').value;
                
                if (scheduledDateTime) formData.append('ScheduledDateTime', scheduledDateTime);
                if (assignedTechnician) formData.append('AssignedTechnician', assignedTechnician);
                if (workLocation) formData.append('WorkLocation', workLocation);
                break;

            case 'OnHold':
            case 'Cancelled':
                const reason = document.getElementById('statusReason').value;
                formData.append('Reason', reason);
                
                if (newStatus === 'OnHold') {
                    const resumeDate = document.getElementById('expectedResumeDate').value;
                    if (resumeDate) formData.append('ExpectedResumeDate', resumeDate);
                }
                break;

            case 'QualityCheck':
                const qcTechnician = document.getElementById('qcTechnician').value;
                const qcDate = document.getElementById('qcDate').value;
                const qcNotes = document.getElementById('qcNotes').value;
                
                if (qcTechnician) formData.append('QcTechnician', qcTechnician);
                if (qcDate) formData.append('QcDate', qcDate);
                if (qcNotes) formData.append('QcNotes', qcNotes);
                break;

            case 'Completed':
                const completionDate = document.getElementById('completionDate').value;
                const completedBy = document.getElementById('completedBy').value;
                const workSummary = document.getElementById('workSummary').value;
                
                if (completionDate) formData.append('CompletionDate', completionDate);
                if (completedBy) formData.append('CompletedBy', completedBy);
                if (workSummary) formData.append('WorkSummary', workSummary);
                break;
        }

        // Add anti-forgery token
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        if (token) formData.append('__RequestVerificationToken', token);

        return formData;
    }

    showAlert(message, type) {
        // Remove existing alerts
        const existingAlerts = document.querySelectorAll(`#${this.modalId} .modal-body .alert`);
        existingAlerts.forEach(alert => {
            if (!alert.classList.contains('alert-info')) { // Don't remove the status info alert
                alert.remove();
            }
        });

        const alertHtml = `
            <div class="alert alert-${type} alert-dismissible fade show">
                <i class="fas fa-${type === 'success' ? 'check-circle' : type === 'warning' ? 'exclamation-triangle' : 'exclamation-circle'}"></i>
                ${message}
                <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
            </div>
        `;

        const modalBody = document.querySelector(`#${this.modalId} .modal-body`);
        modalBody.insertAdjacentHTML('afterbegin', alertHtml);
    }
}

// Create global instance
const statusUpdateModal = new StatusUpdateModal();

// Updated functions for better UX
document.addEventListener('DOMContentLoaded', function() {
    // Attach to all status dropdowns in tables
    document.querySelectorAll('.status-updater').forEach(select => {
        select.addEventListener('change', function() {
            const orderId = this.dataset.orderId;
            const currentStatus = this.dataset.currentStatus;
            const orderNumber = this.dataset.orderNumber || `Order #${orderId}`;

            if (this.value) {
                statusUpdateModal.show(orderId, currentStatus, orderNumber);
            }

            // Reset dropdown
            this.value = '';
        });
    });
});

// Legacy function with modern modal
window.updateServiceStatus = function(serviceOrderId, callback) {
    statusUpdateModal.show(serviceOrderId, 'Current Status', `Service Order #${serviceOrderId}`);
};

// Quick update functions for action buttons
window.quickUpdateStatus = async function(serviceOrderId, status, serviceOrderNumber) {
    statusUpdateModal.show(serviceOrderId, 'Current Status', serviceOrderNumber || `Service Order #${serviceOrderId}`);
    
    // Pre-select the status
    setTimeout(() => {
        const statusSelect = document.getElementById('newStatus');
        if (statusSelect) {
            statusSelect.value = status;
            statusSelect.dispatchEvent(new Event('change'));
        }
    }, 100);
};

// Beautiful toast notifications
function showToast(message, type) {
    const toastContainer = document.querySelector('.toast-container') || createToastContainer();
    
    const toastHtml = `
        <div class="toast align-items-center text-white bg-${type === 'error' ? 'danger' : 'success'} border-0" role="alert">
            <div class="d-flex">
                <div class="toast-body">
                    <i class="fas fa-${type === 'error' ? 'exclamation-circle' : 'check-circle'} me-2"></i>
                    ${message}
                </div>
                <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>
            </div>
        </div>
    `;
    
    toastContainer.insertAdjacentHTML('beforeend', toastHtml);
    const toastElement = toastContainer.lastElementChild;
    const toast = new bootstrap.Toast(toastElement);
    toast.show();
    
    toastElement.addEventListener('hidden.bs.toast', () => toastElement.remove());
}

function createToastContainer() {
    const container = document.createElement('div');
    container.className = 'toast-container position-fixed top-0 end-0 p-3';
    container.style.zIndex = '9999';
    document.body.appendChild(container);
    return container;
}