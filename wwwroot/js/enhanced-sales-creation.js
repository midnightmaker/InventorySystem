// wwwroot/js/enhanced-sales-creation.js
// Enhanced sales creation JavaScript with static 10 rows

let suggestedPrices = {}; // Store suggested prices for line items
let customerSearchTimeout;
let selectedCustomer = null;

// Simple test to verify JavaScript is loading
console.log('Enhanced sales creation JavaScript loaded');

// Initialize form when page loads
document.addEventListener('DOMContentLoaded', function () {
  console.log('DOMContentLoaded fired - starting initialization');

  try {
    // Setup customer search
    setupCustomerSearch();

    // Setup other event listeners
    setupEventListeners();

    // Populate all product dropdowns
    populateAllProductDropdowns();

    // Initialize calculations
    calculateTotals();
    validateForm();

    console.log('Enhanced sales creation initialization completed successfully');
  } catch (error) {
    console.error('Error during initialization:', error);
  }
});

function setupCustomerSearch() {
  console.log('Setting up customer search...');

  const customerSearchInput = document.getElementById('customerSearch');

  if (!customerSearchInput) {
    console.error('Customer search input not found!');
    return;
  }

  customerSearchInput.addEventListener('input', function (e) {
    handleCustomerSearchInput(e);
  });

  console.log('Customer search event listeners attached successfully');
}

function setupEventListeners() {
  // Payment terms change
  const termsSelect = document.querySelector('select[name="Terms"]');
  if (termsSelect) {
    termsSelect.addEventListener('change', updateDueDate);
  }

  // Sale date change
  const saleDateInput = document.querySelector('input[name="SaleDate"]');
  if (saleDateInput) {
    saleDateInput.addEventListener('change', updateDueDate);
  }

  // Discount type change
  const discountTypeSelect = document.querySelector('select[name="DiscountType"]');
  if (discountTypeSelect) {
    discountTypeSelect.addEventListener('change', toggleDiscountInputs);
    toggleDiscountInputs(); // Initialize on load
  }
}

function handleCustomerSearchInput(event) {
  const query = event.target.value;

  if (customerSearchTimeout) {
    clearTimeout(customerSearchTimeout);
  }

  if (query.length < 2) {
    hideCustomerSearchResults();
    return;
  }

  showCustomerLoadingSpinner();

  customerSearchTimeout = setTimeout(function () {
    performCustomerSearch(query);
  }, 300);
}

function performCustomerSearch(query) {
  const url = `/Sales/SearchCustomers?query=${encodeURIComponent(query)}&page=1&pageSize=10`;

  fetch(url)
    .then(response => response.json())
    .then(data => {
      hideCustomerLoadingSpinner();

      if (data.success && data.customers && data.customers.length > 0) {
        displayCustomerResults(data.customers, data.hasMore);
        window.customerSearchResultsData = data.customers;
      } else {
        displayCustomerNoResults(data.message || 'No customers found');
      }
    })
    .catch(error => {
      console.error('Customer search error:', error);
      hideCustomerLoadingSpinner();
      displayCustomerNoResults('Error searching customers');
    });
}

function displayCustomerResults(customers, hasMore = false) {
  let html = '';
  customers.forEach((customer, index) => {
    html += `
            <a href="#" class="dropdown-item customer-result" data-customer-index="${index}">
                <div class="d-flex justify-content-between align-items-start">
                    <div class="flex-grow-1">
                        <div class="fw-bold">${escapeHtml(customer.displayText)}</div>
                        <small class="text-muted">
                            ${customer.email ? escapeHtml(customer.email) : ''}
                            ${customer.phone ? ' • ' + escapeHtml(customer.phone) : ''}
                        </small>
                    </div>
                    <div class="text-end">
                        <small class="badge bg-${customer.outstandingBalance > 0 ? 'warning' : 'success'}">
                            Balance: $${customer.outstandingBalance.toFixed(2)}
                        </small>
                    </div>
                </div>
            </a>
        `;
  });

  if (hasMore) {
    html += '<div class="dropdown-header text-center text-muted"><small><em>Type more to refine search...</em></small></div>';
  }

  const customerSearchResults = document.getElementById('customerSearchResults');
  customerSearchResults.innerHTML = html;
  showCustomerSearchResults();

  bindCustomerResultClicks();
}

function displayCustomerNoResults(message) {
  const html = `
        <div class="dropdown-header text-center text-muted py-3">
            <i class="fas fa-search"></i> ${escapeHtml(message)}
        </div>
    `;

  const customerSearchResults = document.getElementById('customerSearchResults');
  customerSearchResults.innerHTML = html;
  showCustomerSearchResults();
}

function bindCustomerResultClicks() {
  document.querySelectorAll('.customer-result').forEach(element => {
    element.addEventListener('click', function (e) {
      e.preventDefault();
      e.stopPropagation();

      const customerIndex = parseInt(this.getAttribute('data-customer-index'));

      if (window.customerSearchResultsData && window.customerSearchResultsData[customerIndex]) {
        const customerData = window.customerSearchResultsData[customerIndex];
        selectCustomer(customerData);
        hideCustomerSearchResults();
      }
    });
  });
}

function selectCustomer(customer) {
  selectedCustomer = customer;

  // Set the main CustomerId field
  const customerIdInput = document.querySelector('input[name="CustomerId"]');
  if (customerIdInput) {
    customerIdInput.value = customer.id;
  }

  // Update the search input
  const customerSearchInput = document.getElementById('customerSearch');
  if (customerSearchInput) {
    customerSearchInput.value = customer.displayText;
  }

  // Show selected customer display
  const selectedCustomerInfo = document.getElementById('selectedCustomerInfo');
  const selectedCustomerDisplay = document.getElementById('selectedCustomerDisplay');
  const clearButton = document.getElementById('clearCustomerSelection');

  if (selectedCustomerInfo) {
    selectedCustomerInfo.textContent = customer.displayText;
  }
  if (selectedCustomerDisplay) {
    selectedCustomerDisplay.style.display = 'block';
  }
  if (clearButton) {
    clearButton.style.display = 'block';
  }

  // Load customer info
  loadCustomerInfo(customer.id);

  // Clear validation error
  const validationSpan = document.querySelector('span[data-valmsg-for="CustomerId"]');
  if (validationSpan) {
    validationSpan.textContent = '';
  }

  validateForm();
}

function clearCustomerSelection() {
  selectedCustomer = null;

  const customerIdInput = document.querySelector('input[name="CustomerId"]');
  if (customerIdInput) {
    customerIdInput.value = '';
  }

  const customerSearchInput = document.getElementById('customerSearch');
  if (customerSearchInput) {
    customerSearchInput.value = '';
  }

  const selectedCustomerDisplay = document.getElementById('selectedCustomerDisplay');
  const clearButton = document.getElementById('clearCustomerSelection');

  if (selectedCustomerDisplay) {
    selectedCustomerDisplay.style.display = 'none';
  }
  if (clearButton) {
    clearButton.style.display = 'none';
  }

  clearCustomerInfo();
  validateForm();
}

function showCustomerLoadingSpinner() {
  const icon = document.getElementById('customerSearchIcon');
  const spinner = document.getElementById('customerSearchSpinner');

  if (icon) icon.classList.add('d-none');
  if (spinner) spinner.classList.remove('d-none');
}

function hideCustomerLoadingSpinner() {
  const icon = document.getElementById('customerSearchIcon');
  const spinner = document.getElementById('customerSearchSpinner');

  if (icon) icon.classList.remove('d-none');
  if (spinner) spinner.classList.add('d-none');
}

function hideCustomerSearchResults() {
  const results = document.getElementById('customerSearchResults');
  if (results) {
    results.classList.remove('show');
    results.style.display = 'none';
  }
}

function showCustomerSearchResults() {
  const results = document.getElementById('customerSearchResults');
  if (results) {
    results.classList.add('show');
    results.style.display = 'block';
  }
}

function escapeHtml(text) {
  const div = document.createElement('div');
  div.textContent = text;
  return div.innerHTML;
}

// Populate all product dropdowns on page load
function populateAllProductDropdowns() {
  console.log('Populating all product dropdowns');

  // Populate Items dropdowns
  fetch('/Sales/GetItemsForSale')
    .then(response => response.json())
    .then(data => {
      if (data.success && data.items) {
        const itemSelects = document.querySelectorAll('.item-select');
        itemSelects.forEach(select => {
          select.innerHTML = '<option value="">-- Select Item --</option>';
          data.items.forEach(item => {
            const option = document.createElement('option');
            option.value = item.id;
            option.textContent = `${item.partNumber} - ${item.description} (Stock: ${item.currentStock})`;
            select.appendChild(option);
          });
        });
      }
    })
    .catch(error => {
      console.error('Error loading items:', error);
    });

  // Populate Finished Goods dropdowns
  fetch('/Sales/GetFinishedGoodsForSale')
    .then(response => response.json())
    .then(data => {
      if (data.success && data.finishedGoods) {
        const finishedGoodSelects = document.querySelectorAll('.finished-good-select');
        finishedGoodSelects.forEach(select => {
          select.innerHTML = '<option value="">-- Select Finished Good --</option>';
          data.finishedGoods.forEach(fg => {
            const option = document.createElement('option');
            option.value = fg.id;
            option.textContent = `${fg.partNumber} - ${fg.description} (Stock: ${fg.currentStock})`;
            select.appendChild(option);
          });
        });
      }
    })
    .catch(error => {
      console.error('Error loading finished goods:', error);
    });
}

function toggleProductSelect(index) {
  const productType = document.querySelector(`select[name="LineItems[${index}].ProductType"]`).value;
  const itemSelect = document.querySelector(`select[name="LineItems[${index}].ItemId"]`);
  const finishedGoodSelect = document.querySelector(`select[name="LineItems[${index}].FinishedGoodId"]`);

  if (productType === 'Item') {
    itemSelect.classList.remove('d-none');
    finishedGoodSelect.classList.add('d-none');
    finishedGoodSelect.value = '';
  } else {
    itemSelect.classList.add('d-none');
    finishedGoodSelect.classList.remove('d-none');
    itemSelect.value = '';
  }

  clearProductInfo(index);
}

function loadProductInfo(index) {
  const productType = document.querySelector(`select[name="LineItems[${index}].ProductType"]`).value;
  const productId = productType === 'Item'
    ? document.querySelector(`select[name="LineItems[${index}].ItemId"]`).value
    : document.querySelector(`select[name="LineItems[${index}].FinishedGoodId"]`).value;

  if (!productId) {
    clearProductInfo(index);
    return;
  }

  // Show loading state
  const productInfoElement = document.querySelector(`#lineItem_${index} .product-info`);
  if (productInfoElement) {
    productInfoElement.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Loading...';
  }

  // AJAX call to get product info and suggested pricing
  fetch(`/Sales/GetProductInfoForLineItem?productType=${productType}&productId=${productId}`)
    .then(response => response.json())
    .then(data => {
      if (data.success) {
        updateProductInfo(index, data.productInfo);
        autoFillPrice(index, data.productInfo);
      } else {
        showProductError(index, data.message || 'Error loading product information');
      }
    })
    .catch(error => {
      console.error('Error loading product info:', error);
      showProductError(index, 'Error loading product information');
    });
}

function updateProductInfo(index, productInfo) {
  const productInfoElement = document.querySelector(`#lineItem_${index} .product-info`);
  if (!productInfoElement) return;

  let infoText = `${productInfo.partNumber} - ${productInfo.description}`;

  if (productInfo.tracksInventory) {
    const stockClass = productInfo.currentStock > 0 ? 'text-success' : 'text-warning';
    infoText += ` | <span class="${stockClass}">Stock: ${productInfo.currentStock}</span>`;
  } else {
    infoText += ' | <span class="text-info">Service Item</span>';
  }

  productInfoElement.innerHTML = infoText;

  // Store product info for validation
  const row = document.getElementById(`lineItem_${index}`);
  if (row) {
    row.setAttribute('data-tracks-inventory', productInfo.tracksInventory);
    row.setAttribute('data-current-stock', productInfo.currentStock);
    row.setAttribute('data-part-number', productInfo.partNumber);
  }
}

function autoFillPrice(index, productInfo) {
  const priceInput = document.querySelector(`input[name="LineItems[${index}].UnitPrice"]`);
  const suggestedBtn = document.querySelector(`#lineItem_${index} .suggested-price-btn`);

  if (!priceInput) return;

  // Auto-fill price
  priceInput.value = productInfo.suggestedPrice.toFixed(2);

  // Store suggested price
  suggestedPrices[index] = productInfo.suggestedPrice;

  // Show/configure suggested price button
  if (suggestedBtn) {
    suggestedBtn.classList.remove('d-none');
    suggestedBtn.setAttribute('data-price', productInfo.suggestedPrice);

    if (productInfo.hasSalePrice) {
      suggestedBtn.className = 'btn btn-success btn-sm suggested-price-btn';
      suggestedBtn.title = 'Use set price';
    } else {
      suggestedBtn.className = 'btn btn-outline-info btn-sm suggested-price-btn';
      suggestedBtn.title = 'Use calculated price';
    }
  }

  calculateLineTotal(index);
}

function useSuggestedPrice(index) {
  const priceInput = document.querySelector(`input[name="LineItems[${index}].UnitPrice"]`);
  const suggestedPrice = suggestedPrices[index];

  if (priceInput && suggestedPrice !== undefined) {
    priceInput.value = suggestedPrice.toFixed(2);
    calculateLineTotal(index);

    // Visual feedback
    priceInput.style.backgroundColor = '#d4edda';
    setTimeout(() => {
      priceInput.style.backgroundColor = '';
    }, 1000);
  }
}

function clearProductInfo(index) {
  const productInfoElement = document.querySelector(`#lineItem_${index} .product-info`);
  if (productInfoElement) {
    productInfoElement.innerHTML = '';
  }

  const priceInput = document.querySelector(`input[name="LineItems[${index}].UnitPrice"]`);
  if (priceInput) {
    priceInput.value = '';
  }

  const suggestedBtn = document.querySelector(`#lineItem_${index} .suggested-price-btn`);
  if (suggestedBtn) {
    suggestedBtn.classList.add('d-none');
  }

  delete suggestedPrices[index];
  calculateLineTotal(index);
}

function showProductError(index, message) {
  const productInfoElement = document.querySelector(`#lineItem_${index} .product-info`);
  if (productInfoElement) {
    productInfoElement.innerHTML = `<span class="text-danger"><i class="fas fa-exclamation-triangle"></i> ${message}</span>`;
  }
}

function calculateLineTotal(index) {
  try {
    const quantityInput = document.querySelector(`input[name="LineItems[${index}].Quantity"]`);
    const priceInput = document.querySelector(`input[name="LineItems[${index}].UnitPrice"]`);
    const lineTotalElement = document.querySelector(`#lineItem_${index} .line-total-display`);

    if (!quantityInput || !priceInput || !lineTotalElement) {
      return;
    }

    const quantity = parseFloat(quantityInput.value) || 0;
    const unitPrice = parseFloat(priceInput.value) || 0;
    const lineTotal = quantity * unitPrice;

    // Update the line total display
    lineTotalElement.textContent = '$' + lineTotal.toFixed(2);

    // Validate stock if needed
    validateLineItemStock(index, quantity);

    // Update overall totals
    calculateTotals();
  } catch (error) {
    console.error(`Error calculating line total for index ${index}:`, error);
  }
}

function validateLineItemStock(index, quantity) {
  const row = document.getElementById(`lineItem_${index}`);
  if (!row) return;

  const tracksInventory = row.getAttribute('data-tracks-inventory') === 'true';
  const currentStock = parseInt(row.getAttribute('data-current-stock')) || 0;
  const partNumber = row.getAttribute('data-part-number') || '';

  const quantityInput = document.querySelector(`input[name="LineItems[${index}].Quantity"]`);
  if (!quantityInput) return;

  // Clear previous validation styling
  quantityInput.classList.remove('is-invalid', 'is-valid');

  if (tracksInventory && quantity > currentStock) {
    quantityInput.classList.add('is-invalid');
    quantityInput.title = `Insufficient stock for ${partNumber}. Available: ${currentStock}`;
  } else if (tracksInventory && quantity <= currentStock && quantity > 0) {
    quantityInput.classList.add('is-valid');
    quantityInput.title = '';
  }
}

function calculateTotals() {
  let subtotal = 0;
  let totalQuantity = 0;
  let itemCount = 0;

  // Count only rows with selected products and quantities > 0
  document.querySelectorAll('.line-item-row').forEach(row => {
    const lineTotalElement = row.querySelector('.line-total-display');
    const quantityInput = row.querySelector('.quantity-input');

    if (lineTotalElement) {
      const amount = parseFloat(lineTotalElement.textContent.replace(/[$,]/g, '')) || 0;
      if (amount > 0) {
        subtotal += amount;
        itemCount++;
      }
    }

    if (quantityInput) {
      const qty = parseFloat(quantityInput.value) || 0;
      if (qty > 0) {
        totalQuantity += qty;
      }
    }
  });

  // Get shipping and tax
  const shippingInput = document.querySelector('input[name="ShippingCost"]');
  const taxInput = document.querySelector('input[name="TaxAmount"]');

  const shipping = shippingInput ? parseFloat(shippingInput.value) || 0 : 0;
  const tax = taxInput ? parseFloat(taxInput.value) || 0 : 0;

  // Calculate discount
  const discountTypeSelect = document.querySelector('select[name="DiscountType"]');
  const discountAmountInput = document.querySelector('input[name="DiscountAmount"]');
  const discountPercentageInput = document.querySelector('input[name="DiscountPercentage"]');

  let discount = 0;
  if (discountTypeSelect && discountTypeSelect.value === 'Percentage' && discountPercentageInput) {
    const percentage = parseFloat(discountPercentageInput.value) || 0;
    discount = subtotal * (percentage / 100);

    if (discountAmountInput) {
      discountAmountInput.value = discount.toFixed(2);
    }
  } else if (discountAmountInput) {
    discount = parseFloat(discountAmountInput.value) || 0;

    if (discountPercentageInput && discountTypeSelect && discountTypeSelect.value === 'Amount') {
      discountPercentageInput.value = '';
    }
  }

  const total = subtotal + shipping + tax - discount;

  // Update displays
  updateDisplayElement('subtotalDisplay', subtotal);
  updateDisplayElement('discountDisplay', discount, true);
  updateDisplayElement('totalDisplay', total);
  updateDisplayElement('itemCountDisplay', itemCount, false, false);
  updateDisplayElement('totalQtyDisplay', totalQuantity, false, false);

  // Show/hide discount row
  const discountRow = document.querySelector('.discount-row');
  if (discountRow) {
    discountRow.style.display = discount > 0 ? 'table-row' : 'none';
  }

  validateForm();
}

function updateDisplayElement(elementId, value, isNegative = false, isCurrency = true) {
  const element = document.getElementById(elementId);
  if (element) {
    if (isCurrency) {
      element.textContent = (isNegative ? '-' : '') + '$' + Math.abs(value).toFixed(2);
    } else {
      element.textContent = value.toString();
    }
  }
}

function toggleDiscountInputs() {
  const discountType = document.querySelector('select[name="DiscountType"]').value;
  const amountDiv = document.getElementById('discountAmountDiv');
  const percentageDiv = document.getElementById('discountPercentageDiv');
  const amountInput = document.querySelector('input[name="DiscountAmount"]');
  const percentageInput = document.querySelector('input[name="DiscountPercentage"]');

  if (discountType === 'Percentage') {
    amountDiv.style.display = 'none';
    percentageDiv.style.display = 'block';
    if (amountInput) amountInput.value = '';
  } else {
    amountDiv.style.display = 'block';
    percentageDiv.style.display = 'none';
    if (percentageInput) percentageInput.value = '';
  }

  calculateTotals();
}

function validateForm() {
  const hasLineItems = Array.from(document.querySelectorAll('.line-item-row')).some(row => {
    const lineTotalElement = row.querySelector('.line-total-display');
    const amount = parseFloat(lineTotalElement.textContent.replace(/[$,]/g, '')) || 0;
    return amount > 0;
  });

  const customerIdInput = document.querySelector('input[name="CustomerId"]');
  const hasCustomer = customerIdInput && customerIdInput.value !== '';

  // Check for stock validation errors
  const hasStockErrors = document.querySelectorAll('input.is-invalid').length > 0;

  const submitBtn = document.getElementById('createSaleBtn');

  if (submitBtn) {
    const isValid = hasLineItems && hasCustomer && !hasStockErrors;
    submitBtn.disabled = !isValid;

    if (!hasCustomer) {
      submitBtn.innerHTML = '<i class="fas fa-exclamation-triangle"></i> Select Customer';
    } else if (!hasLineItems) {
      submitBtn.innerHTML = '<i class="fas fa-exclamation-triangle"></i> Add Items';
    } else if (hasStockErrors) {
      submitBtn.innerHTML = '<i class="fas fa-exclamation-triangle"></i> Check Stock';
    } else {
      submitBtn.innerHTML = '<i class="fas fa-save"></i> Create Complete Sale';
    }
  }
}

function loadCustomerInfo(customerId) {
  if (!customerId) {
    clearCustomerInfo();
    return;
  }

  fetch(`/Customers/GetCustomerInfo/${customerId}`)
    .then(response => response.json())
    .then(data => {
      if (data.success) {
        populateCustomerInfo(data.customer);
      }
    })
    .catch(error => {
      console.error('Error loading customer info:', error);
    });
}

function populateCustomerInfo(customer) {
  const shippingAddressTextarea = document.querySelector('textarea[name="ShippingAddress"]');
  if (shippingAddressTextarea && customer.fullShippingAddress) {
    shippingAddressTextarea.value = customer.fullShippingAddress;
  }

  const termsSelect = document.querySelector('select[name="Terms"]');
  if (termsSelect && customer.paymentTerms !== undefined) {
    termsSelect.value = customer.paymentTerms;
    updateDueDate();
  }
}

function clearCustomerInfo() {
  const shippingAddressTextarea = document.querySelector('textarea[name="ShippingAddress"]');
  if (shippingAddressTextarea) {
    shippingAddressTextarea.value = '';
  }
}

function updateDueDate() {
  const saleDateInput = document.querySelector('input[name="SaleDate"]');
  const termsSelect = document.querySelector('select[name="Terms"]');
  const dueDateInput = document.querySelector('input[name="PaymentDueDate"]');

  if (!saleDateInput || !termsSelect || !dueDateInput) return;

  const saleDate = new Date(saleDateInput.value);
  const terms = parseInt(termsSelect.value);

  if (isNaN(saleDate.getTime())) return;

  let dueDate = new Date(saleDate);

  switch (terms) {
    case 0: dueDate = new Date(saleDate); break;
    case 1: dueDate.setDate(saleDate.getDate() + 10); break;
    case 2: dueDate.setDate(saleDate.getDate() + 15); break;
    case 3: dueDate.setDate(saleDate.getDate() + 30); break;
    case 4: dueDate.setDate(saleDate.getDate() + 60); break;
    case 5: dueDate.setDate(saleDate.getDate() + 90); break;
    default: dueDate.setDate(saleDate.getDate() + 30);
  }

  const formattedDate = dueDate.toISOString().split('T')[0];
  dueDateInput.value = formattedDate;
}

// Form submission handling
document.addEventListener('DOMContentLoaded', function () {
  const form = document.getElementById('enhancedSaleForm');
  if (form) {
    form.addEventListener('submit', function (e) {
      const hasStockErrors = document.querySelectorAll('input.is-invalid').length > 0;

      if (hasStockErrors) {
        e.preventDefault();
        alert('Please resolve stock availability issues before submitting.');
        return false;
      }

      const submitBtn = document.getElementById('createSaleBtn');
      if (submitBtn) {
        submitBtn.disabled = true;
        submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Creating Sale...';
      }

      return true;
    });
  }
});