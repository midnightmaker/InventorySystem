class ServiceStatusUpdater {
    constructor() {
        this.currentServiceOrder = null;
    }

    async updateStatus(serviceOrderId, newStatus, additionalData = {}) {
        try {
            // Build form data
            const formData = new FormData();
            formData.append('ServiceOrderId', serviceOrderId);
            formData.append('NewStatus', newStatus);
            formData.append('__RequestVerificationToken', document.querySelector('input[name="__RequestVerificationToken"]').value);

            // Add additional data if provided
            Object.keys(additionalData).forEach(key => {
                formData.append(key, additionalData[key]);
            });

            const response = await fetch('/Services/UpdateStatus', {
                method: 'POST',
                body: formData
            });

            const result = await response.json();

            if (result.success) {
                this.showToast(result.message, 'success');
                // Reload the page or update the UI
                setTimeout(() => location.reload(), 1500);
            } else {
                this.showToast(result.message, 'error');
                // Reset dropdown
                const dropdown = document.querySelector(`[data-order-id="${serviceOrderId}"]`);
                if (dropdown) dropdown.value = dropdown.dataset.currentStatus;
            }

            return result;
        } catch (error) {
            this.showToast('Error updating status: ' + error.message, 'error');
            return { success: false, message: error.message };
        }
    }

    async handleStatusChange(dropdown) {
        const serviceOrderId = parseInt(dropdown.dataset.orderId);
        const newStatus = dropdown.value;
        const currentStatus = dropdown.dataset.currentStatus;

        if (!newStatus || newStatus === currentStatus) {
            dropdown.value = currentStatus;
            return;
        }

        // Get service order details from data attributes
        const requiresDocuments = dropdown.dataset.requiresDocuments === 'true';
        const documentsComplete = dropdown.dataset.documentsComplete === 'true';
        const missingDocuments = dropdown.dataset.missingDocuments;

        // Check if completing and documents are required
        if (newStatus === 'Completed' && requiresDocuments && !documentsComplete) {
            alert(`Cannot complete service order. Missing required documents: ${missingDocuments}`);
            dropdown.value = currentStatus;
            return;
        }

        // Handle statuses that need additional information
        let additionalData = {};

        switch (newStatus) {
            case 'Scheduled':
                const scheduledDate = prompt('Enter scheduled date and time (MM/DD/YYYY HH:MM):');
                if (!scheduledDate) {
                    dropdown.value = currentStatus;
                    return;
                }
                const technician = prompt('Assign technician (optional):');
                additionalData.ScheduledDateTime = scheduledDate;
                if (technician) additionalData.AssignedTechnician = technician;
                break;

            case 'OnHold':
            case 'Cancelled':
                const reason = prompt(`Please provide a reason for ${newStatus}:`);
                if (!reason) {
                    dropdown.value = currentStatus;
                    return;
                }
                additionalData.Reason = reason;
                break;

            case 'QualityCheck':
                if (confirm('Mark for quality check?')) {
                    additionalData.QcNotes = prompt('QC notes (optional):') || '';
                } else {
                    dropdown.value = currentStatus;
                    return;
                }
                break;
        }

        // Confirm the status change
        if (confirm(`Change status from ${currentStatus} to ${newStatus}?`)) {
            await this.updateStatus(serviceOrderId, newStatus, additionalData);
        } else {
            dropdown.value = currentStatus;
        }
    }

    showToast(message, type = 'info') {
        // Simple toast implementation
        const toast = document.createElement('div');
        toast.className = `alert alert-${type === 'error' ? 'danger' : type} alert-dismissible fade show position-fixed top-0 end-0 m-3`;
        toast.style.zIndex = '9999';
        toast.innerHTML = `
            ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        `;
        document.body.appendChild(toast);

        setTimeout(() => {
            toast.remove();
        }, 5000);
    }
}

// Initialize
const statusUpdater = new ServiceStatusUpdater();

// Attach to all status dropdowns
document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('.status-update-dropdown').forEach(dropdown => {
        dropdown.addEventListener('change', (e) => {
            statusUpdater.handleStatusChange(e.target);
        });
    });
});