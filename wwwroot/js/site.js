// Payment Due Date Validation Utilities
const PaymentValidation = {
    Terms: {
        Immediate: 0,
        Net10: 10,
        Net30: 30,
        Net45: 45,
        Net60: 60
    },

    validatePaymentDueDate: function(saleDate, dueDate, terms) {
        const today = new Date();
        today.setHours(0, 0, 0, 0);
        
        const saleDateObj = new Date(saleDate);
        const dueDateObj = new Date(dueDate);
        
        // For immediate terms, due date must equal sale date
        if (terms == this.Terms.Immediate) {
            if (dueDateObj.getTime() !== saleDateObj.getTime()) {
                return { isValid: false, message: "Payment due date must be the same as sale date for Immediate terms." };
            }
        } else {
            // For non-immediate terms, due date cannot be in the past
            if (dueDateObj < today) {
                return { isValid: false, message: "Payment due date cannot be in the past." };
            }
        }
        
        // Due date cannot be before sale date
        if (dueDateObj < saleDateObj) {
            return { isValid: false, message: "Payment due date cannot be before the sale date." };
        }
        
        return { isValid: true, message: "" };
    },

    calculateDueDate: function(saleDate, terms) {
        const saleDateObj = new Date(saleDate);
        const dueDate = new Date(saleDateObj);
        dueDate.setDate(dueDate.getDate() + parseInt(terms));
        return dueDate.toISOString().split('T')[0];
    },

    showValidationError: function(input, errorSpan, message) {
        if (errorSpan) {
            errorSpan.textContent = message;
            errorSpan.className = 'text-danger field-validation-error';
        }
        input.classList.add('input-validation-error');
    },

    clearValidationError: function(input, errorSpan) {
        if (errorSpan) {
            errorSpan.textContent = '';
            errorSpan.className = 'text-danger field-validation-valid';
        }
        input.classList.remove('input-validation-error');
    },

    setupPaymentValidation: function(saleDateSelector, termsSelector, dueDateSelector) {
        const saleDateInput = document.querySelector(saleDateSelector);
        const termsSelect = document.querySelector(termsSelector);
        const dueDateInput = document.querySelector(dueDateSelector);
        const errorSpan = document.querySelector(`span[data-valmsg-for="${dueDateInput.name}"]`);

        const validateAndUpdate = () => {
            if (!saleDateInput.value || !termsSelect.value) return;

            // Calculate due date
            const calculatedDueDate = this.calculateDueDate(saleDateInput.value, termsSelect.value);
            dueDateInput.value = calculatedDueDate;

            // Validate
            const validation = this.validatePaymentDueDate(
                saleDateInput.value, 
                calculatedDueDate, 
                parseInt(termsSelect.value)
            );

            if (validation.isValid) {
                this.clearValidationError(dueDateInput, errorSpan);
            } else {
                this.showValidationError(dueDateInput, errorSpan, validation.message);
            }
        };

        // Attach event listeners
        saleDateInput?.addEventListener('change', validateAndUpdate);
        termsSelect?.addEventListener('change', validateAndUpdate);
        dueDateInput?.addEventListener('blur', () => {
            if (saleDateInput.value && dueDateInput.value) {
                const validation = this.validatePaymentDueDate(
                    saleDateInput.value, 
                    dueDateInput.value, 
                    parseInt(termsSelect.value)
                );

                if (validation.isValid) {
                    this.clearValidationError(dueDateInput, errorSpan);
                } else {
                    this.showValidationError(dueDateInput, errorSpan, validation.message);
                }
            }
        });

        // Initial validation
        validateAndUpdate();
    }
};

// ===== CLICKABLE TABLE ROWS UTILITY =====
/*
 * Clickable Table Rows System
 * 
 * This system makes index view table rows clickable to navigate to detail pages
 * while preserving action button functionality.
 * 
 * IMPLEMENTATION:
 * 1. Add 'clickable-row' class to <tr> elements
 * 2. Add 'data-href' attribute with the destination URL
 * 3. Add 'onclick="event.stopPropagation();"' to action columns to prevent row clicks
 * 4. Global CSS handles styling (see site.css)
 * 
 * EXAMPLE HTML:
 * <tr class="clickable-row" data-href="/Items/Details/123" style="cursor: pointer;">
 *   <td>Item Name</td>
 *   <td onclick="event.stopPropagation();">
 *     <div class="btn-group">
 *       <a href="/Items/Edit/123" class="btn btn-outline-warning">Edit</a>
 *     </div>
 *   </td>
 * </tr>
 * 
 * FEATURES:
 * - Smooth hover animations
 * - Loading indicators for navigation
 * - Prevents text selection
 * - Works with existing action buttons
 * - Accessible keyboard navigation
 * 
 * USED IN:
 * - Sales Index (/Sales)
 * - Customers Index (/Customers)
 * - Items Index (/Items) 
 * - Vendors Index (/Vendors)
 */
const ClickableTableRows = {
    // Initialize clickable rows for a table
    init: function(tableSelector = '.table') {
        const tables = document.querySelectorAll(tableSelector);
        
        tables.forEach(table => {
            const clickableRows = table.querySelectorAll('.clickable-row');
            
            clickableRows.forEach(row => {
                this.setupRow(row);
            });
        });
    },

    // Setup individual row
    setupRow: function(row) {
        // Add click handler
        row.addEventListener('click', function(e) {
            // Don't navigate if clicking on action buttons, links, or form controls
            if (e.target.closest('.btn-group') || 
                e.target.closest('a') || 
                e.target.closest('button') ||
                e.target.closest('input') ||
                e.target.closest('select')) {
                return;
            }

            const href = this.getAttribute('data-href');
            if (href) {
                // Show loading indicator if available
                if (window.LoadingIndicator && window.LoadingIndicator.isInitialized) {
                    window.LoadingIndicator.showForOperation('loading-details');
                }
                
                // Navigate to details page
                window.location.href = href;
            }
        });

        // Add keyboard navigation
        row.setAttribute('tabindex', '0');
        row.addEventListener('keydown', function(e) {
            if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                this.click();
            }
        });

        // Add aria label for accessibility
        const href = row.getAttribute('data-href');
        if (href) {
            row.setAttribute('aria-label', 'Click to view details');
        }
    },

    // Add clickable behavior to dynamically created rows
    makeRowClickable: function(row, detailsUrl) {
        if (!row) return;
        
        row.classList.add('clickable-row');
        row.setAttribute('data-href', detailsUrl);
        row.style.cursor = 'pointer';
        
        this.setupRow(row);
    }
};

// Auto-initialize clickable rows when DOM is ready
document.addEventListener('DOMContentLoaded', function() {
    ClickableTableRows.init();
});

// Make utility globally available
window.ClickableTableRows = ClickableTableRows;

// ===== GLOBAL LOADING INDICATOR SYSTEM =====
// Only declare if it doesn't already exist (avoid conflicts with embedded version)
if (typeof window.LoadingIndicator === 'undefined') {
    console.log('Creating LoadingIndicator from site.js...');
    
    const LoadingIndicator = {
        isInitialized: false,
        isShowing: false,
        timeoutId: null,
        minDisplayTime: 300, // Minimum time to show spinner (prevents flashing)
        showDelay: 150, // Delay before showing spinner (for very fast requests)

        init: function() {
            if (this.isInitialized) {
                console.log('LoadingIndicator already initialized');
                return;
            }

            console.log('Initializing LoadingIndicator from site.js...');

            // Create the loading overlay HTML
            const loadingHTML = `
                <div id="globalLoadingOverlay" style="display: none;">
                    <div class="loading-backdrop"></div>
                    <div class="loading-content">
                        <div class="loading-spinner">
                            <div class="spinner-border text-primary" role="status">
                                <span class="visually-hidden">Loading...</span>
                            </div>
                        </div>
                        <div class="loading-text">Loading...</div>
                        <div class="loading-subtext">Please wait while the page loads</div>
                    </div>
                </div>
            `;

            // Add to document body
            document.body.insertAdjacentHTML('beforeend', loadingHTML);

            // Add CSS styles
            this.addStyles();

            // Setup navigation event listeners
            this.setupNavigationListeners();

            // Setup AJAX interceptors
            this.setupAjaxInterceptors();

            this.isInitialized = true;
            console.log('LoadingIndicator initialized successfully from site.js');
        },

        addStyles: function() {
            // Check if styles already exist
            if (document.getElementById('loading-indicator-styles')) {
                console.log('Loading indicator styles already exist');
                return;
            }

            const styles = `
                <style id="loading-indicator-styles">
                    #globalLoadingOverlay {
                        position: fixed;
                        top: 0;
                        left: 0;
                        width: 100%;
                        height: 100%;
                        z-index: 9999;
                        pointer-events: none;
                        opacity: 0;
                        transition: opacity 0.2s ease-in-out;
                    }
                    
                    #globalLoadingOverlay.show {
                        opacity: 1;
                    }

                    .loading-backdrop {
                        position: absolute;
                        top: 0;
                        left: 0;
                        width: 100%;
                        height: 100%;
                        background: rgba(255, 255, 255, 0.85);
                        backdrop-filter: blur(2px);
                    }

                    .loading-content {
                        position: absolute;
                        top: 50%;
                        left: 50%;
                        transform: translate(-50%, -50%);
                        text-align: center;
                        background: white;
                        padding: 2rem;
                        border-radius: 12px;
                        box-shadow: 0 8px 32px rgba(0, 0, 0, 0.15);
                        border: 1px solid rgba(255, 255, 255, 0.2);
                        min-width: 250px;
                    }

                    .loading-spinner {
                        margin-bottom: 1rem;
                    }

                    .loading-spinner .spinner-border {
                        width: 3rem;
                        height: 3rem;
                        border-width: 0.3em;
                    }

                    .loading-text {
                        font-size: 1.1rem;
                        font-weight: 600;
                        color: #333;
                        margin-bottom: 0.5rem;
                    }

                    .loading-subtext {
                        font-size: 0.9rem;
                        color: #6c757d;
                    }

                    /* Busy cursor for the entire page when loading */
                    body.loading {
                        cursor: wait !important;
                    }

                    body.loading * {
                        cursor: wait !important;
                    }

                    /* Loading state for buttons */
                    .btn.loading {
                        cursor: wait !important;
                        opacity: 0.8;
                        pointer-events: none;
                    }

                    .btn.loading .btn-text {
                        visibility: hidden;
                    }

                    .btn.loading::after {
                        content: "";
                        position: absolute;
                        width: 16px;
                        height: 16px;
                        top: 50%;
                        left: 50%;
                        margin-left: -8px;
                        margin-top: -8px;
                        border-radius: 50%;
                        border: 2px solid transparent;
                        border-top-color: currentColor;
                        animation: btnSpinner 1s ease infinite;
                    }

                    @keyframes btnSpinner {
                        0% { transform: rotate(0deg); }
                        100% { transform: rotate(360deg); }
                    }
                </style>
            `;
            
            document.head.insertAdjacentHTML('beforeend', styles);
            console.log('Loading indicator styles added from site.js');
        },

        show: function(message = 'Loading...', subtext = 'Please wait while the page loads') {
            console.log('LoadingIndicator.show() called from site.js:', message, subtext);
            
            if (!this.isInitialized) {
                console.error('LoadingIndicator not initialized! Call init() first.');
                return;
            }

            if (this.isShowing) {
                console.log('Loading indicator already showing');
                return;
            }

            clearTimeout(this.timeoutId);
            
            this.timeoutId = setTimeout(() => {
                const overlay = document.getElementById('globalLoadingOverlay');
                if (!overlay) {
                    console.error('Loading overlay not found in DOM!');
                    return;
                }

                const textElement = overlay.querySelector('.loading-text');
                const subtextElement = overlay.querySelector('.loading-subtext');
                
                if (textElement) textElement.textContent = message;
                if (subtextElement) subtextElement.textContent = subtext;
                
                overlay.style.display = 'block';
                document.body.classList.add('loading');
                
                // Trigger animation
                requestAnimationFrame(() => {
                    overlay.classList.add('show');
                });
                
                this.isShowing = true;
                console.log('Loading indicator shown from site.js');
            }, this.showDelay);
        },

        hide: function() {
            console.log('LoadingIndicator.hide() called from site.js');
            
            clearTimeout(this.timeoutId);
            
            if (!this.isShowing) {
                console.log('Loading indicator not showing');
                return;
            }

            const overlay = document.getElementById('globalLoadingOverlay');
            if (!overlay) {
                console.error('Loading overlay not found when trying to hide!');
                return;
            }
            
            // Ensure minimum display time
            setTimeout(() => {
                overlay.classList.remove('show');
                document.body.classList.remove('loading');
                
                setTimeout(() => {
                    overlay.style.display = 'none';
                    this.isShowing = false;
                    console.log('Loading indicator hidden from site.js');
                }, 200); // Wait for fade out animation
            }, this.minDisplayTime);
        },

        setupNavigationListeners: function() {
            console.log('Setting up navigation listeners from site.js...');
            
            // Show loading for all navigation links
            document.addEventListener('click', (e) => {
                const link = e.target.closest('a[href]');
                if (!link) return;

                const href = link.getAttribute('href');
                
                // Skip if it's a hash link, external link, or download link
                if (!href || 
                    href.startsWith('#') || 
                    href.startsWith('mailto:') || 
                    href.startsWith('tel:') ||
                    href.includes('download') ||
                    link.target === '_blank' ||
                    link.hasAttribute('download')) {
                    return;
                }

                // Skip if it's the same page
                if (href === window.location.pathname + window.location.search) {
                    return;
                }

                // Determine message based on URL
                let message = 'Loading...';
                let subtext = 'Please wait';
                
                if (href.includes('/Boms')) {
                    message = 'Loading BOMs...';
                    subtext = 'Fetching Bill of Materials data';
                } else if (href.includes('/Items')) {
                    message = 'Loading Items...';
                    subtext = 'Fetching inventory data';
                } else if (href.includes('/Vendors')) {
                    message = 'Loading Vendors...';
                    subtext = 'Fetching vendor information';
                } else if (href.includes('/Customers')) {
                    message = 'Loading Customers...';
                    subtext = 'Fetching customer data';
                } else if (href.includes('/Sales')) {
                    message = 'Loading Sales...';
                    subtext = 'Fetching sales information';
                } else if (href.includes('/Production')) {
                    message = 'Loading Production...';
                    subtext = 'Fetching production data';
                } else {
                    message = 'Navigating...';
                    subtext = 'Loading page content';
                }

                this.show(message, subtext);
            });

            // Show loading for form submissions
            document.addEventListener('submit', (e) => {
                // Don't show for forms with file uploads (they have their own indicators)
                const form = e.target;
                if (form.enctype === 'multipart/form-data') return;

                this.show('Processing...', 'Submitting form data');
            });

            // Hide loading when page starts to unload
            window.addEventListener('beforeunload', () => this.hide());

            // Hide loading when page is loaded (fallback)
            window.addEventListener('load', () => this.hide());
        },

        setupAjaxInterceptors: function() {
            console.log('Setting up AJAX interceptors from site.js...');
            
            // Intercept fetch requests
            const originalFetch = window.fetch;
            window.fetch = function(...args) {
                LoadingIndicator.show('Loading...', 'Fetching data');
                
                return originalFetch.apply(this, args)
                    .then(response => {
                        LoadingIndicator.hide();
                        return response;
                    })
                    .catch(error => {
                        LoadingIndicator.hide();
                        throw error;
                    });
            };

            // Intercept jQuery AJAX if available
            if (window.jQuery) {
                console.log('jQuery detected, setting up AJAX handlers');
                $(document).ajaxStart(function() {
                    LoadingIndicator.show('Loading...', 'Processing request');
                });
                
                $(document).ajaxComplete(function() {
                    LoadingIndicator.hide();
                });
            }
        },

        // Utility method to add loading state to specific button
        setButtonLoading: function(button, loading = true) {
            if (!button) return;

            if (loading) {
                button.classList.add('loading');
                button.disabled = true;
                if (!button.querySelector('.btn-text')) {
                    button.innerHTML = `<span class="btn-text">${button.innerHTML}</span>`;
                }
            } else {
                button.classList.remove('loading');
                button.disabled = false;
            }
        },

        // Custom show for specific operations
        showForOperation: function(operation) {
            const messages = {
                'loading-bom': { text: 'Loading BOM...', subtext: 'Fetching Bill of Materials data' },
                'loading-items': { text: 'Loading Items...', subtext: 'Fetching inventory data' },
                'loading-vendors': { text: 'Loading Vendors...', subtext: 'Fetching vendor information' },
                'loading-customers': { text: 'Loading Customers...', subtext: 'Fetching customer data' },
                'loading-sales': { text: 'Loading Sales...', subtext: 'Fetching sales information' },
                'loading-production': { text: 'Loading Production...', subtext: 'Fetching production data' },
                'checking-availability': { text: 'Checking Availability...', subtext: 'Analyzing inventory levels' },
                'generating-report': { text: 'Generating Report...', subtext: 'Processing data and calculations' },
                'uploading': { text: 'Uploading...', subtext: 'Transferring files to server' },
                'saving': { text: 'Saving...', subtext: 'Updating database records' }
            };

            const config = messages[operation] || { text: 'Loading...', subtext: 'Please wait' };
            this.show(config.text, config.subtext);
        },

        // Test function for debugging
        test: function() {
            console.log('Testing LoadingIndicator from site.js...');
            this.show('Test Loading...', 'This is a test from site.js');
            
            setTimeout(() => {
                this.hide();
            }, 3000);
        }
    };

    // Initialize the loading indicator when DOM is ready
    document.addEventListener('DOMContentLoaded', function() {
        LoadingIndicator.init();
    });

    // Make LoadingIndicator globally available
    window.LoadingIndicator = LoadingIndicator;
    
} else {
    console.log('LoadingIndicator already exists, using existing version');
}

// ===== SIMPLE FALLBACK LOADING INDICATOR =====
// Simple, lightweight alternative if the main system doesn't work
const SimpleLoadingIndicator = {
    overlay: null,
    
    init: function() {
        if (this.overlay) return; // Already initialized
        
        // Create simple overlay
        this.overlay = document.createElement('div');
        this.overlay.id = 'simpleLoadingOverlay';
        this.overlay.innerHTML = `
            <div style="
                position: fixed;
                top: 0;
                left: 0;
                width: 100%;
                height: 100%;
                background: rgba(255, 255, 255, 0.8);
                z-index: 9999;
                display: none;
                justify-content: center;
                align-items: center;
            ">
                <div style="
                    background: white;
                    padding: 2rem;
                    border-radius: 8px;
                    box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
                    text-align: center;
                ">
                    <div class="spinner-border text-primary mb-3" role="status">
                        <span class="visually-hidden">Loading...</span>
                    </div>
                    <div><strong>Loading...</strong></div>
                    <div class="text-muted">Please wait</div>
                </div>
            </div>
        `;
        
        document.body.appendChild(this.overlay);
        console.log('SimpleLoadingIndicator initialized');
    },
    
    show: function(message = 'Loading...') {
        this.init();
        if (this.overlay) {
            this.overlay.style.display = 'flex';
            document.body.style.cursor = 'wait';
            console.log('SimpleLoadingIndicator shown:', message);
        }
    },
    
    hide: function() {
        if (this.overlay) {
            this.overlay.style.display = 'none';
            document.body.style.cursor = '';
            console.log('SimpleLoadingIndicator hidden');
        }
    }
};

// Make SimpleLoadingIndicator globally available
window.SimpleLoadingIndicator = SimpleLoadingIndicator;