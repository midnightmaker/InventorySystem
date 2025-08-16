// Document Management Module
class DocumentManager {
    constructor(serviceOrderId) {
        this.serviceOrderId = serviceOrderId;
        this.init();
    }

    init() {
        this.loadDocumentsList();
        this.addEventListeners();
    }

    addEventListeners() {
        // ESC key to close modal
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape') {
                const modal = bootstrap.Modal.getInstance(document.getElementById('documentViewerModal'));
                if (modal) {
                    modal.hide();
                }
            }
        });

        // Context menu support
        this.addDocumentContextMenu();
    }

    toggleDocumentUpload() {
        const uploadSection = document.getElementById('documentUploadSection');
        const isVisible = uploadSection.style.display !== 'none';

        if (isVisible) {
            uploadSection.style.display = 'none';
            document.getElementById('documentUploadForm').reset();
        } else {
            uploadSection.style.display = 'block';
            document.getElementById('documentFile').focus();
        }
    }

    async uploadDocument() {
        const form = document.getElementById('documentUploadForm');
        const fileInput = document.getElementById('documentFile');
        const progressDiv = document.getElementById('uploadProgress');

        if (!fileInput.files || fileInput.files.length === 0) {
            alert('Please select a file to upload.');
            return;
        }

        const file = fileInput.files[0];

        // Validate file size (50MB limit)
        if (file.size > 50 * 1024 * 1024) {
            alert('File size cannot exceed 50MB.');
            return;
        }

        // Show progress
        progressDiv.style.display = 'block';

        const formData = new FormData();
        formData.append('serviceOrderId', this.serviceOrderId);
        formData.append('file', file);
        formData.append('documentType', document.getElementById('documentType').value);
        formData.append('documentName', document.getElementById('documentName').value);
        formData.append('description', document.getElementById('documentDescription').value);

        // Get the anti-forgery token
        const token = document.querySelector('input[name="__RequestVerificationToken"]');
        if (token) {
            formData.append('__RequestVerificationToken', token.value);
        }

        try {
            const response = await fetch('/Services/UploadDocument', {
                method: 'POST',
                body: formData
            });

            progressDiv.style.display = 'none';

            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            const contentType = response.headers.get('content-type');
            if (!contentType || !contentType.includes('application/json')) {
                throw new Error('Server did not return JSON response');
            }

            const data = await response.json();

            if (data.success) {
                form.reset();
                this.toggleDocumentUpload();
                this.loadDocumentsList();
                this.showNotification('Document uploaded successfully!', 'success');
            } else {
                alert('Error uploading document: ' + data.message);
            }
        } catch (error) {
            progressDiv.style.display = 'none';
            console.error('Upload error:', error);

            if (error.message.includes('HTTP error')) {
                alert('Server error occurred. Please check the file size and type.');
            } else if (error.message.includes('JSON')) {
                alert('Server response error. Please try again.');
            } else {
                alert('Error uploading document: ' + error.message);
            }
        }
    }

    async loadDocumentsList() {
        try {
            const response = await fetch(`/Services/GetDocuments?serviceOrderId=${this.serviceOrderId}`);
            const data = await response.json();
            const container = document.getElementById('documentsContainer');

            if (data.success) {
                if (data.documents && data.documents.length > 0) {
                    let html = '<div id="documentsList">';
                    data.documents.forEach(doc => {
                        html += this.createDocumentHtml(doc);
                    });
                    html += '</div>';
                    container.innerHTML = html;
                } else {
                    container.innerHTML = this.getNoDocumentsHtml();
                }
            } else {
                container.innerHTML = '<div class="alert alert-warning">Error loading documents</div>';
            }
        } catch (error) {
            console.error('Error:', error);
            document.getElementById('documentsContainer').innerHTML = '<div class="alert alert-danger">Error loading documents</div>';
        }
    }

    createDocumentHtml(doc) {
        return `
            <div class="d-flex justify-content-between align-items-center border-bottom py-2" id="document-${doc.id}">
                <div class="d-flex align-items-center">
                    <i class="${this.getFileIcon(doc.fileName)} fa-2x me-3"></i>
                    <div>
                        <h6 class="mb-1">
                            <a href="javascript:void(0)"
                               onclick="documentManager.viewDocument(${doc.id})"
                               class="text-decoration-none text-primary document-name-link"
                               title="Click to preview document">
                                ${doc.documentName}
                            </a>
                        </h6>
                        <small class="text-muted">
                            <strong>Type:</strong> ${doc.documentType}<br>
                            <strong>File:</strong> ${doc.originalFileName} (${this.formatFileSize(doc.fileSize)})<br>
                            <strong>Uploaded:</strong> ${this.formatDate(doc.uploadedDate)}${doc.uploadedBy ? ' by ' + doc.uploadedBy : ''}
                        </small>
                        ${doc.description ? `<br><small class="text-muted"><strong>Description:</strong> ${doc.description}</small>` : ''}
                    </div>
                </div>
                <div class="btn-group btn-group-sm">
                    <button class="btn btn-outline-primary" onclick="documentManager.downloadDocument(${doc.id})" title="Download">
                        <i class="fas fa-download"></i>
                    </button>
                    <button class="btn btn-outline-info" onclick="documentManager.viewDocument(${doc.id})" title="View">
                        <i class="fas fa-eye"></i>
                    </button>
                    <button class="btn btn-outline-danger" onclick="documentManager.deleteDocument(${doc.id}, '${doc.documentName}')" title="Delete">
                        <i class="fas fa-trash"></i>
                    </button>
                </div>
            </div>
        `;
    }

    getNoDocumentsHtml() {
        return `
            <div id="noDocumentsMessage" class="text-center py-4">
                <i class="fas fa-file-alt fa-3x text-muted mb-3"></i>
                <h6 class="text-muted">No Documents Uploaded</h6>
                <p class="text-muted">Upload photos, specifications, drawings, certificates, or other relevant documents for this service order.</p>
                <button class="btn btn-outline-primary" onclick="documentManager.toggleDocumentUpload()">
                    <i class="fas fa-upload"></i> Upload First Document
                </button>
            </div>
        `;
    }

    downloadDocument(documentId) {
        window.open(`/Services/DownloadDocument/${documentId}`, '_blank');
    }

    async viewDocument(documentId) {
        try {
            const response = await fetch(`/Services/GetDocuments?serviceOrderId=${this.serviceOrderId}`);
            const data = await response.json();
            
            if (data.success) {
                const document = data.documents.find(d => d.id === documentId);
                if (document) {
                    this.showDocumentInModal(documentId, document);
                }
            }
        } catch (error) {
            console.error('Error getting document info:', error);
            window.open(`/Services/ViewDocument/${documentId}`, '_blank');
        }
    }

    showDocumentInModal(documentId, documentInfo) {
        const modal = document.getElementById('documentViewerModal');
        const modalTitle = document.getElementById('documentViewerModalLabel');
        const modalContent = document.getElementById('documentViewerContent');
        const downloadBtn = document.getElementById('downloadFromViewer');

        modalTitle.innerHTML = `<i class="fas fa-eye"></i> ${documentInfo.documentName}`;
        downloadBtn.onclick = () => this.downloadDocument(documentId);

        const extension = documentInfo.originalFileName.split('.').pop().toLowerCase();

        if (['pdf'].includes(extension)) {
            modalContent.innerHTML = `
                <iframe src="/Services/ViewDocument/${documentId}"
                        width="100%" height="800px" style="border: none;">
                    <p>Your browser doesn't support PDF viewing.
                       <a href="/Services/DownloadDocument/${documentId}" target="_blank">Download the PDF</a>
                    </p>
                </iframe>
            `;
        } else if (['jpg', 'jpeg', 'png', 'gif', 'bmp', 'tiff'].includes(extension)) {
            modalContent.innerHTML = `
                <div class="text-center p-3">
                    <img src="/Services/ViewDocument/${documentId}"
                         class="img-fluid" style="max-height: 800px;"
                         alt="${documentInfo.documentName}">
                </div>
            `;
        } else if (['txt', 'html', 'css', 'js', 'json', 'xml'].includes(extension)) {
            fetch(`/Services/ViewDocument/${documentId}`)
                .then(response => response.text())
                .then(text => {
                    modalContent.innerHTML = `
                        <div class="p-3">
                            <pre class="bg-light p-3 rounded" style="max-height: 800px; overflow-y: auto;"><code>${this.escapeHtml(text)}</code></pre>
                        </div>
                    `;
                })
                .catch(error => {
                    modalContent.innerHTML = this.getUnsupportedDocumentHtml(documentId, documentInfo);
                });
        } else {
            modalContent.innerHTML = this.getUnsupportedDocumentHtml(documentId, documentInfo);
        }

        const bootstrapModal = new bootstrap.Modal(modal);
        bootstrapModal.show();
    }

    getUnsupportedDocumentHtml(documentId, documentInfo) {
        return `
            <div class="text-center p-4">
                <i class="fas fa-file fa-3x text-muted mb-3"></i>
                <h5>${documentInfo.documentName}</h5>
                <p class="text-muted">This document type cannot be previewed in the browser.</p>
                <p><strong>Type:</strong> ${documentInfo.documentType}</p>
                <p><strong>Size:</strong> ${this.formatFileSize(documentInfo.fileSize)}</p>
                <button class="btn btn-primary" onclick="documentManager.downloadDocument(${documentId})">
                    <i class="fas fa-download"></i> Download Document
                </button>
            </div>
        `;
    }

    async deleteDocument(documentId, documentName) {
        if (!confirm(`Are you sure you want to delete "${documentName}"? This action cannot be undone.`)) {
            return;
        }

        try {
            const token = document.querySelector('input[name="__RequestVerificationToken"]');
            
            const response = await fetch('/Services/DeleteDocument', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    ...(token && { 'RequestVerificationToken': token.value })
                },
                body: JSON.stringify({ documentId: documentId })
            });

            const data = await response.json();

            if (data.success) {
                const documentElement = document.getElementById('document-' + documentId);
                if (documentElement) {
                    documentElement.remove();
                }

                const documentsList = document.getElementById('documentsList');
                if (documentsList && documentsList.children.length === 0) {
                    this.loadDocumentsList();
                }

                this.showNotification('Document deleted successfully!', 'success');
            } else {
                alert('Error deleting document: ' + data.message);
            }
        } catch (error) {
            console.error('Error:', error);
            alert('Error deleting document. Please try again.');
        }
    }

    addDocumentContextMenu() {
        document.addEventListener('contextmenu', (e) => {
            if (e.target.closest('.document-name-link')) {
                e.preventDefault();
                const link = e.target.closest('.document-name-link');
                const documentId = link.onclick.toString().match(/viewDocument\((\d+)\)/)[1];

                const contextMenu = document.createElement('div');
                contextMenu.className = 'dropdown-menu show';
                contextMenu.style.position = 'fixed';
                contextMenu.style.left = e.clientX + 'px';
                contextMenu.style.top = e.clientY + 'px';
                contextMenu.style.zIndex = '9999';

                contextMenu.innerHTML = `
                    <a class="dropdown-item" href="javascript:void(0)" onclick="documentManager.viewDocument(${documentId}); this.parentNode.remove();">
                        <i class="fas fa-eye"></i> Preview
                    </a>
                    <a class="dropdown-item" href="javascript:void(0)" onclick="documentManager.downloadDocument(${documentId}); this.parentNode.remove();">
                        <i class="fas fa-download"></i> Download
                    </a>
                    <div class="dropdown-divider"></div>
                    <a class="dropdown-item text-danger" href="javascript:void(0)" onclick="documentManager.deleteDocument(${documentId}, '${link.textContent}'); this.parentNode.remove();">
                        <i class="fas fa-trash"></i> Delete
                    </a>
                `;

                document.body.appendChild(contextMenu);

                setTimeout(() => {
                    document.addEventListener('click', function() {
                        if (contextMenu.parentNode) {
                            contextMenu.remove();
                        }
                    }, { once: true });
                }, 100);
            }
        });
    }

    // Helper methods
    getFileIcon(fileName) {
        const extension = fileName.split('.').pop().toLowerCase();
        switch (extension) {
            case 'pdf': return 'fas fa-file-pdf text-danger';
            case 'jpg': case 'jpeg': case 'png': case 'gif': case 'bmp': case 'tiff': return 'fas fa-file-image text-info';
            case 'doc': case 'docx': return 'fas fa-file-word text-primary';
            case 'xls': case 'xlsx': return 'fas fa-file-excel text-success';
            case 'ppt': case 'pptx': return 'fas fa-file-powerpoint text-warning';
            case 'dwg': case 'dxf': return 'fas fa-drafting-compass text-info';
            case 'zip': case 'rar': case '7z': return 'fas fa-file-archive text-secondary';
            case 'txt': case 'rtf': return 'fas fa-file-alt text-muted';
            default: return 'fas fa-file text-muted';
        }
    }

    formatFileSize(bytes) {
        if (bytes === 0) return '0 Bytes';
        const k = 1024;
        const sizes = ['Bytes', 'KB', 'MB', 'GB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
    }

    formatDate(dateString) {
        return new Date(dateString).toLocaleDateString() + ' ' + new Date(dateString).toLocaleTimeString();
    }

    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    showNotification(message, type = 'info') {
        const toastHtml = `
            <div class="toast align-items-center text-white bg-${type} border-0" role="alert" aria-live="assertive" aria-atomic="true">
                <div class="d-flex">
                    <div class="toast-body">
                        <i class="fas fa-${type === 'success' ? 'check-circle' : 'info-circle'}"></i> ${message}
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
}

// Global functions for backward compatibility
let documentManager;

function toggleDocumentUpload() {
    documentManager.toggleDocumentUpload();
}

function uploadDocument() {
    documentManager.uploadDocument();
}