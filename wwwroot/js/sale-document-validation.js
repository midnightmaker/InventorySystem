/**
 * Simple Sale Document Validation - Fixed error handling
 */
document.addEventListener('DOMContentLoaded', function() {
    console.log('=== VALIDATION SCRIPT LOADED ===');
    
    // Find the process sale button
    const button = document.querySelector('button[data-bs-target="#processSaleModal"]');
    console.log('Process button found:', button);
    
    if (button) {
        // IMPORTANT: Remove the data-bs-target attribute so Bootstrap doesn't automatically open the modal
        button.removeAttribute('data-bs-target');
        
        // Remove any existing event listeners first
        const newButton = button.cloneNode(true);
        button.parentNode.replaceChild(newButton, button);
        
        // Add our validation event listener
        newButton.addEventListener('click', function(event) {
            console.log('=== BUTTON CLICKED ===');
            
            // Always prevent any default behavior
            event.preventDefault();
            event.stopPropagation();
            
            // Get the sale ID with proper error handling
            try {
                const saleId = getSaleId(event.target);
                console.log('Sale ID found:', saleId);
                
                if (!saleId) {
                    console.log('No sale ID - opening modal directly');
                    openProcessModal();
                    return;
                }
                
                console.log('Validating sale before opening modal...');
                validateAndProceed(saleId);
            } catch (error) {
                console.error('Error getting sale ID:', error);
                showErrorToast('Error getting sale information. Please refresh the page.');
            }
        });
        
        console.log('Validation event listener attached');
    } else {
        console.log('WARNING: Process button not found!');
    }
});

function getSaleId(buttonElement) {
    console.log('=== GETTING SALE ID ===');
    console.log('Button element:', buttonElement);
    
    // Check if button element exists
    if (!buttonElement) {
        console.log('Button element is null/undefined');
        return null;
    }
    
    let saleId = null;
    
    // Method 1: Try button dataset
    try {
        if (buttonElement.dataset) {
            saleId = buttonElement.dataset.saleId;
            console.log('Method 1 - button.dataset.saleId:', saleId);
            if (saleId) return saleId;
        }
    } catch (e) {
        console.log('Method 1 failed:', e.message);
    }
    
    // Method 2: Try getAttribute
    try {
        saleId = buttonElement.getAttribute('data-sale-id');
        console.log('Method 2 - getAttribute("data-sale-id"):', saleId);
        if (saleId) return saleId;
    } catch (e) {
        console.log('Method 2 failed:', e.message);
    }
    
    // Method 3: Try hidden input
    try {
        const saleIdInput = document.querySelector('input[name="SaleId"]');
        saleId = saleIdInput ? saleIdInput.value : null;
        console.log('Method 3 - input[name="SaleId"]:', saleId);
        if (saleId) return saleId;
    } catch (e) {
        console.log('Method 3 failed:', e.message);
    }
    
    // Method 4: Try URL parameters
    try {
        const urlParams = new URLSearchParams(window.location.search);
        saleId = urlParams.get('id');
        console.log('Method 4 - URL parameter "id":', saleId);
        if (saleId) return saleId;
    } catch (e) {
        console.log('Method 4 failed:', e.message);
    }
    
    // Method 5: Try URL path
    try {
        const pathMatch = window.location.pathname.match(/\/Sales\/Details\/(\d+)/);
        saleId = pathMatch ? pathMatch[1] : null;
        console.log('Method 5 - URL path match:', saleId);
        if (saleId) return saleId;
    } catch (e) {
        console.log('Method 5 failed:', e.message);
    }
    
    console.log('Final Sale ID:', saleId);
    return saleId;
}

function validateAndProceed(saleId) {
    console.log('=== VALIDATION REQUEST ===');
    console.log('Sending Sale ID:', saleId);
    
    // Validate sale ID
    if (!saleId || isNaN(parseInt(saleId))) {
        showErrorToast('Invalid sale ID. Please refresh the page.');
        return;
    }
    
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    
    // Create form data instead of JSON
    const formData = new FormData();
    formData.append('saleId', saleId);
    if (token) {
        formData.append('__RequestVerificationToken', token);
    }
    
    fetch('/Sales/ValidateSaleProcessing', {
        method: 'POST',
        body: formData
    })
    .then(response => {
        console.log('Response status:', response.status);
        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
        return response.json();
    })
    .then(result => {
        console.log('=== VALIDATION RESULT ===');
        console.log(result);
        
        if (!result.success) {
            console.log('Validation service error:', result.message);
            showErrorToast('Validation Error: ' + (result.message || 'Unknown error'));
            return;
        }
        
        if (result.canProcess) {
            console.log('✅ Sale can be processed - opening Process & Ship modal');
            openProcessModal();
        } else {
            console.log('❌ Sale cannot be processed - showing validation errors');
            showValidationErrorModal(result);
        }
    })
    .catch(error => {
        console.error('=== VALIDATION ERROR ===');
        console.error(error);
        showErrorToast('Error validating sale: ' + error.message);
    });
}

function openProcessModal() {
    console.log('=== OPENING PROCESS & SHIP MODAL ===');
    const modalElement = document.getElementById('processSaleModal');
    
    if (modalElement) {
        const modal = new bootstrap.Modal(modalElement);
        modal.show();
        console.log('✅ Process & Ship modal opened');
    } else {
        console.error('❌ Process & Ship modal element not found!');
        showErrorToast('Could not open shipping form. Please refresh the page.');
    }
}

function showValidationErrorModal(validationResult) {
    console.log('=== SHOWING VALIDATION ERROR MODAL ===');
    
    let title = 'Cannot Process Sale';
    let content = '';
    let iconClass = 'fas fa-exclamation-triangle text-warning';
    
    if (validationResult.hasDocumentIssues && validationResult.hasInventoryIssues) {
        title = 'Multiple Issues Prevent Processing';
        iconClass = 'fas fa-exclamation-triangle text-danger';
        content = `
            <div class="alert alert-danger">
                <strong>This sale has both document and inventory issues that must be resolved.</strong>
            </div>
            ${buildDocumentIssuesSection(validationResult)}
            <hr>
            ${buildInventoryIssuesSection(validationResult)}
        `;
    } else if (validationResult.hasDocumentIssues) {
        title = 'Missing Service Documents';
        iconClass = 'fas fa-file-alt text-warning';
        content = buildDocumentIssuesSection(validationResult);
    } else if (validationResult.hasInventoryIssues) {
        title = 'Inventory Issues';
        iconClass = 'fas fa-boxes text-danger';
        content = buildInventoryIssuesSection(validationResult);
    } else {
        content = `
            <div class="alert alert-warning">
                <strong>This sale cannot be processed at this time.</strong>
            </div>
            <p>Please review the sale details and try again.</p>
            ${validationResult.errors?.length ? `
                <ul class="list-unstyled">
                    ${validationResult.errors.map(error => `
                        <li><i class="fas fa-exclamation-circle text-warning"></i> ${error}</li>
                    `).join('')}
                </ul>
            ` : ''}
        `;
    }
    
    const modalHtml = `
        <div class="modal fade" id="validationErrorModal" tabindex="-1" aria-hidden="true">
            <div class="modal-dialog modal-lg">
                <div class="modal-content border-warning">
                    <div class="modal-header bg-light">
                        <h5 class="modal-title">
                            <i class="${iconClass}"></i> ${title}
                        </h5>
                        <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                    </div>
                    <div class="modal-body">
                        ${content}
                    </div>
                    <div class="modal-footer">
                        <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">
                            <i class="fas fa-times"></i> Close
                        </button>
                        ${buildActionButtons(validationResult)}
                    </div>
                </div>
            </div>
        </div>
    `;
    
    // Remove any existing validation modal
    const existingModal = document.getElementById('validationErrorModal');
    if (existingModal) {
        existingModal.remove();
    }
    
    // Add and show the validation error modal
    document.body.insertAdjacentHTML('beforeend', modalHtml);
    const modal = new bootstrap.Modal(document.getElementById('validationErrorModal'));
    modal.show();
    
    // Clean up when hidden
    document.getElementById('validationErrorModal').addEventListener('hidden.bs.modal', function() {
        this.remove();
    });
}

function buildDocumentIssuesSection(validationResult) {
    if (!validationResult.missingServiceDocuments?.length) return '';
    
    // Separate missing service orders from missing documents
    const missingServiceOrders = validationResult.missingServiceDocuments.filter(doc => 
        !doc.serviceOrderId && doc.missingDocuments?.includes("Service Order must be created first")
    );
    
    const missingDocuments = validationResult.missingServiceDocuments.filter(doc => 
        doc.serviceOrderId && !doc.missingDocuments?.includes("Service Order must be created first")
    );
    
    let content = `
        <h6><i class="fas fa-file-alt text-warning"></i> Service Validation Issues</h6>
        <div class="alert alert-warning">
            <strong>Service requirements must be met before this sale can be processed.</strong>
        </div>
    `;
    
    // Show missing service orders first (higher priority)
    if (missingServiceOrders.length > 0) {
        content += `
            <h6 class="mt-3"><i class="fas fa-plus-circle text-danger"></i> Missing Service Orders</h6>
            <p class="text-danger">The following services need service orders created first:</p>
            <div class="list-group list-group-flush mb-3">
                ${missingServiceOrders.map(doc => `
                    <div class="list-group-item border-start border-danger border-3">
                        <div class="d-flex justify-content-between align-items-start">
                            <div>
                                <h6 class="mb-1 text-danger">${doc.serviceTypeName || 'Unknown Service'}</h6>
                                <small class="text-muted">Equipment: ${doc.equipmentIdentifier || 'N/A'}</small><br>
                                <span class="text-danger">
                                    <i class="fas fa-exclamation-triangle"></i> 
                                    Service order must be created before documents can be uploaded
                                </span>
                            </div>
                            <span class="badge bg-danger rounded-pill">Service Order Required</span>
                        </div>
                    </div>
                `).join('')}
            </div>
        `;
    }
    
    // Show missing documents (service orders exist but documents missing)
    if (missingDocuments.length > 0) {
        content += `
            <h6 class="mt-3"><i class="fas fa-upload text-warning"></i> Missing Documents</h6>
            <p class="text-warning">The following services have orders but are missing required documents:</p>
            <div class="list-group list-group-flush mb-3">
                ${missingDocuments.map(doc => `
                    <div class="list-group-item border-start border-warning border-3">
                        <div class="d-flex justify-content-between align-items-start">
                            <div>
                                <h6 class="mb-1">${doc.serviceTypeName || 'Unknown Service'}</h6>
                                <small class="text-muted">Equipment: ${doc.equipmentIdentifier || 'N/A'}</small><br>
                                <small class="text-muted">Service Order: ${doc.serviceOrderNumber}</small><br>
                                <span class="text-warning">Missing: ${(doc.missingDocuments || []).join(', ') || 'Required documents'}</span>
                            </div>
                            <span class="badge bg-warning rounded-pill">Documents Required</span>
                        </div>
                    </div>
                `).join('')}
            </div>
        `;
    }
    
    // Add instructions
    content += `
        <div class="mt-3">
            <h6>To proceed:</h6>
            <ol>
                ${missingServiceOrders.length > 0 ? '<li class="text-danger"><strong>Create service orders for the services listed above</strong></li>' : ''}
                ${missingDocuments.length > 0 ? '<li class="text-warning">Upload the required documents to existing service orders</li>' : ''}
                <li>Return to this sale and try processing again</li>
            </ol>
        </div>
    `;
    
    return content;
}

function buildInventoryIssuesSection(validationResult) {
    const inventoryErrors = validationResult.errors?.filter(error => 
        error.includes('stock') || error.includes('inventory')
    ) || [];
    
    if (!inventoryErrors.length) return '';
    
    return `
        <h6><i class="fas fa-boxes text-danger"></i> Inventory Issues</h6>
        <div class="alert alert-danger">
            <strong>Inventory shortage detected.</strong>
        </div>
        <ul class="list-unstyled">
            ${inventoryErrors.map(error => `
                <li class="mb-2">
                    <i class="fas fa-exclamation-circle text-danger"></i> ${error}
                </li>
            `).join('')}
        </ul>
    `;
}

function buildActionButtons(validationResult) {
    let actions = '';
    
    // Add buttons for missing service documents
    if (validationResult.hasDocumentIssues && validationResult.missingServiceDocuments?.length) {
        // Get current sale ID for service order creation
        const currentSaleId = getCurrentSaleIdFromPage();
        
        validationResult.missingServiceDocuments.forEach(doc => {
            if (!doc.serviceOrderId) {
                // Service order needs to be created first
                actions += `
                    <a href="/Services/Create?saleId=${currentSaleId}&serviceTypeId=${doc.serviceTypeId || ''}&serialNumber=${encodeURIComponent(doc.serialNumber || '')}&modelNumber=${encodeURIComponent(doc.modelNumber || '')}" 
                       class="btn btn-danger me-2 mb-2">
                        <i class="fas fa-plus"></i> Create Service Order
                        <br><small>${doc.serviceTypeName || 'Service'} - ${doc.equipmentIdentifier || 'Equipment'}</small>
                    </a>
                `;
            } else {
                // Service order exists - link to upload documents
                actions += `
                    <a href="/Services/Details/${doc.serviceOrderId}#documents" 
                       class="btn btn-warning me-2 mb-2">
                        <i class="fas fa-upload"></i> Upload Documents
                        <br><small>${doc.serviceTypeName || 'Service'} - ${doc.equipmentIdentifier || 'Equipment'}</small>
                    </a>
                `;
            }
        });
    }
    
    // Add inventory management button if needed
    if (validationResult.hasInventoryIssues) {
        actions += `
            <a href="/Inventory" class="btn btn-info me-2 mb-2">
                <i class="fas fa-boxes"></i> Manage Inventory
            </a>
        `;
    }
    
    return actions;
}

// Helper function to get sale ID from various sources
function getCurrentSaleIdFromPage() {
    // Try multiple methods to get sale ID from the current page
    let saleId = null;
    
    // Method 1: Hidden input
    const saleIdInput = document.querySelector('input[name="SaleId"]');
    if (saleIdInput?.value) {
        saleId = saleIdInput.value;
    }
    
    // Method 2: URL path
    if (!saleId) {
        const pathMatch = window.location.pathname.match(/\/Sales\/Details\/(\d+)/);
        saleId = pathMatch ? pathMatch[1] : null;
    }
    
    // Method 3: URL parameters  
    if (!saleId) {
        const urlParams = new URLSearchParams(window.location.search);
        saleId = urlParams.get('id');
    }
    
    console.log('Current sale ID from page:', saleId);
    return saleId;
}

function showErrorToast(message) {
    const toastHtml = `
        <div class="toast align-items-center text-white bg-danger border-0" role="alert" aria-live="assertive" aria-atomic="true">
            <div class="d-flex">
                <div class="toast-body">
                    <i class="fas fa-exclamation-circle"></i> ${message}
                </div>
                <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>
            </div>
        </div>
    `;
    
    let toastContainer = document.querySelector('.toast-container');
    if (!toastContainer) {
        toastContainer = document.createElement('div');
        toastContainer.className = 'toast-container position-fixed bottom-0 end-0 p-3';
        document.body.appendChild(toastContainer);
    }
    
    toastContainer.insertAdjacentHTML('beforeend', toastHtml);
    const toast = new bootstrap.Toast(toastContainer.lastElementChild);
    toast.show();
}

