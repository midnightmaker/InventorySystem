// wwwroot/js/enhanced-sales-creation.js
// Enhanced sales creation JavaScript with static 10 rows

let suggestedPrices = {}; // Store suggested prices for line items
let customerSearchTimeout;
let selectedCustomer = null;

// Determine if this is a quotation form based on the hidden IsQuotation input
function isQuotationMode() {
  const isQuotationInput = document.querySelector('input[name="IsQuotation"]');
  return isQuotationInput && isQuotationInput.value.toLowerCase() === 'true';
}

console.log('Enhanced sales creation JavaScript loaded');

document.addEventListener('DOMContentLoaded', function () {
  console.log('DOMContentLoaded fired - starting initialization');

  try {
    setupCustomerSearch();
    setupEventListeners();
    initializeLineItemSelects();
    populateAllProductDropdowns();
    calculateTotals();
    validateForm();

    // Detect if page was re-rendered after a validation error and reset the button
    var hasValidationErrors = document.querySelectorAll('.text-danger:not(:empty), .validation-summary-errors').length > 0
      || document.querySelectorAll('.field-validation-error').length > 0;
    if (hasValidationErrors) {
      console.log('Validation errors detected on page load, resetting submit button');
      var btn = document.getElementById('createSaleBtn');
      if (btn) {
        btn.disabled = false;
        validateForm(); // Re-run to set correct enabled/disabled state and text
      }
    }

    console.log('Enhanced sales creation initialization completed successfully');
  } catch (error) {
    console.error('Error during initialization:', error);
  }
});

function setupCustomerSearch() {
  const customerSearchInput = document.getElementById('customerSearch');
  if (!customerSearchInput) { console.error('Customer search input not found!'); return; }
  customerSearchInput.addEventListener('input', handleCustomerSearchInput);

  // Close results when clicking outside
  document.addEventListener('click', function (e) {
    if (!e.target.closest('#customerSearch') && !e.target.closest('#customerSearchResults')) {
      hideCustomerSearchResults();
    }
  });

  // Wire up clear button
  const clearBtn = document.getElementById('clearCustomerSelection');
  if (clearBtn) clearBtn.addEventListener('click', clearCustomerSelection);
}

function setupEventListeners() {
  const termsSelect = document.querySelector('select[name="Terms"]');
  if (termsSelect) termsSelect.addEventListener('change', updateDueDate);

  const saleDateInput = document.querySelector('input[name="SaleDate"]');
  if (saleDateInput) saleDateInput.addEventListener('change', updateDueDate);

  const discountTypeSelect = document.querySelector('select[name="DiscountType"]');
  if (discountTypeSelect) {
    discountTypeSelect.addEventListener('change', toggleDiscountInputs);
    toggleDiscountInputs();
  }
}

function handleCustomerSearchInput(event) {
  const query = event.target.value;
  if (customerSearchTimeout) clearTimeout(customerSearchTimeout);
  if (query.length < 2) { hideCustomerSearchResults(); return; }
  showCustomerLoadingSpinner();
  customerSearchTimeout = setTimeout(() => performCustomerSearch(query), 300);
}

function performCustomerSearch(query) {
  // Uses /Sales/SearchCustomers which accepts 'query' parameter
  const url = `/Sales/SearchCustomers?query=${encodeURIComponent(query)}&page=1&pageSize=10`;
  fetch(url)
    .then(response => { if (!response.ok) throw new Error(`HTTP ${response.status}`); return response.json(); })
    .then(data => {
      hideCustomerLoadingSpinner();
      if (data.success && data.customers && data.customers.length > 0) {
        displayCustomerResults(data.customers, data.hasMore);
        window.customerSearchResultsData = data.customers;
      } else {
        displayCustomerNoResults('No customers found');
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
    const displayText = customer.displayText || customer.name;
    const outstandingBalance = customer.outstandingBalance || 0;
    const creditLimit = customer.creditLimit || 0;
    let creditBadge = '';
    if (creditLimit > 0 && outstandingBalance > creditLimit)
      creditBadge = '<span class="badge bg-danger ms-2">Over Credit</span>';
    else if (creditLimit > 0)
      creditBadge = '<span class="badge bg-success ms-2">Credit OK</span>';

    html += `
      <a href="#" class="dropdown-item customer-result" data-customer-index="${index}">
        <div class="d-flex justify-content-between align-items-start">
          <div class="flex-grow-1">
            <div class="fw-bold">${escapeHtml(displayText)}</div>
            <small class="text-muted">${customer.email ? escapeHtml(customer.email) : ''}${customer.phone ? ' • ' + escapeHtml(customer.phone) : ''}</small>
          </div>
          <div class="text-end">
            <small class="badge bg-${outstandingBalance > 0 ? 'warning' : 'success'}">Balance: $${outstandingBalance.toFixed(2)}</small>
            ${creditBadge}
          </div>
        </div>
      </a>`;
  });
  if (hasMore) html += '<div class="dropdown-header text-center text-muted"><small><em>Type more to refine...</em></small></div>';

  document.getElementById('customerSearchResults').innerHTML = html;
  showCustomerSearchResults();
  bindCustomerResultClicks();
}

function displayCustomerNoResults(message) {
  document.getElementById('customerSearchResults').innerHTML =
    `<div class="dropdown-header text-center text-muted py-3"><i class="fas fa-search"></i> ${escapeHtml(message)}</div>`;
  showCustomerSearchResults();
}

function bindCustomerResultClicks() {
  document.querySelectorAll('.customer-result').forEach(el => {
    el.addEventListener('click', function (e) {
      e.preventDefault(); e.stopPropagation();
      const idx = parseInt(this.getAttribute('data-customer-index'));
      if (window.customerSearchResultsData && window.customerSearchResultsData[idx])
        selectCustomer(window.customerSearchResultsData[idx]);
      hideCustomerSearchResults();
    });
  });
}

function selectCustomer(customer) {
  selectedCustomer = customer;
  const customerIdInput = document.querySelector('input[name="CustomerId"]');
  if (customerIdInput) customerIdInput.value = customer.id;

  const searchInput = document.getElementById('customerSearch');
  if (searchInput) searchInput.value = customer.displayText || customer.name;

  const info = document.getElementById('selectedCustomerInfo');
  const display = document.getElementById('selectedCustomerDisplay');
  const clearBtn = document.getElementById('clearCustomerSelection');
  if (info) info.textContent = customer.displayText || customer.name;
  if (display) display.style.display = 'block';
  if (clearBtn) clearBtn.style.display = 'block';

  loadCustomerInfo(customer.id);

  const validationSpan = document.querySelector('span[data-valmsg-for="CustomerId"]');
  if (validationSpan) validationSpan.textContent = '';
  validateForm();
}

function clearCustomerSelection() {
  selectedCustomer = null;
  const customerIdInput = document.querySelector('input[name="CustomerId"]');
  if (customerIdInput) customerIdInput.value = '';
  const searchInput = document.getElementById('customerSearch');
  if (searchInput) searchInput.value = '';
  const display = document.getElementById('selectedCustomerDisplay');
  const clearBtn = document.getElementById('clearCustomerSelection');
  if (display) display.style.display = 'none';
  if (clearBtn) clearBtn.style.display = 'none';
  clearCustomerInfo();
  validateForm();
}

function showCustomerLoadingSpinner() {
  document.getElementById('customerSearchIcon')?.classList.add('d-none');
  document.getElementById('customerSearchSpinner')?.classList.remove('d-none');
}
function hideCustomerLoadingSpinner() {
  document.getElementById('customerSearchIcon')?.classList.remove('d-none');
  document.getElementById('customerSearchSpinner')?.classList.add('d-none');
}
function hideCustomerSearchResults() {
  const r = document.getElementById('customerSearchResults');
  if (r) { r.classList.remove('show'); r.style.display = 'none'; }
}
function showCustomerSearchResults() {
  const r = document.getElementById('customerSearchResults');
  if (r) { r.classList.add('show'); r.style.display = 'block'; }
}

function escapeHtml(text) {
  const div = document.createElement('div');
  div.textContent = text;
  return div.innerHTML;
}

// ─── Product Dropdowns ────────────────────────────────────────────────────────

function initializeLineItemSelects() {
  // On page load the default ProductType is "Item", so disable the hidden
  // FinishedGood and ServiceType selects so they don't post empty strings.
  for (let i = 0; i < 10; i++) {
    const fgSelect = document.querySelector(`select[name="LineItems[${i}].FinishedGoodId"]`);
    const stSelect = document.querySelector(`select[name="LineItems[${i}].ServiceTypeId"]`);
    if (fgSelect) fgSelect.disabled = true;
    if (stSelect) stSelect.disabled = true;
  }
}

function populateAllProductDropdowns() {
  console.log('Populating all product dropdowns');

  // Items
  fetch('/Sales/GetItemsForSale')
    .then(r => r.json())
    .then(data => {
      if (data.success && data.items) {
        document.querySelectorAll('.item-select').forEach(select => {
          select.innerHTML = '<option value="">-- Select Item --</option>';
          data.items.forEach(item => {
            const opt = document.createElement('option');
            opt.value = item.id;
            opt.textContent = `${item.partNumber} - ${item.description} (Stock: ${item.currentStock})`;
            select.appendChild(opt);
          });
        });
      }
    })
    .catch(err => console.error('Error loading items:', err));

  // Finished Goods
  fetch('/Sales/GetFinishedGoodsForSale')
    .then(r => r.json())
    .then(data => {
      if (data.success && data.finishedGoods) {
        document.querySelectorAll('.finished-good-select').forEach(select => {
          select.innerHTML = '<option value="">-- Select Finished Good --</option>';
          data.finishedGoods.forEach(fg => {
            const opt = document.createElement('option');
            opt.value = fg.id;
            opt.textContent = `${fg.partNumber} - ${fg.description} (Stock: ${fg.currentStock})`;
            select.appendChild(opt);
          });
        });
      }
    })
    .catch(err => console.error('Error loading finished goods:', err));

  // Service Types
  fetch('/Sales/GetServiceTypesForSale')
    .then(r => r.json())
    .then(data => {
      if (data.success && data.serviceTypes) {
        document.querySelectorAll('.service-type-select').forEach(select => {
          select.innerHTML = '<option value="">-- Select Service --</option>';
          data.serviceTypes.forEach(st => {
            const opt = document.createElement('option');
            opt.value = st.id;
            opt.textContent = `${st.serviceCode ? st.serviceCode + ' - ' : ''}${st.serviceName} ($${st.standardPrice.toFixed(2)})`;
            select.appendChild(opt);
          });
        });
      }
    })
    .catch(err => console.error('Error loading service types:', err));
}

function toggleProductSelect(index) {
  const productType = document.querySelector(`select[name="LineItems[${index}].ProductType"]`).value;
  const itemSelect = document.querySelector(`select[name="LineItems[${index}].ItemId"]`);
  const fgSelect = document.querySelector(`select[name="LineItems[${index}].FinishedGoodId"]`);
  const stSelect = document.querySelector(`select[name="LineItems[${index}].ServiceTypeId"]`);

  // Hide and disable all, then enable only the active one.
  // Disabled elements are NOT submitted with the form, preventing
  // empty-string model-binding errors on int? properties.
  itemSelect.classList.add('d-none'); itemSelect.value = ''; itemSelect.disabled = true;
  fgSelect.classList.add('d-none'); fgSelect.value = ''; fgSelect.disabled = true;
  if (stSelect) { stSelect.classList.add('d-none'); stSelect.value = ''; stSelect.disabled = true; }

  if (productType === 'Item') { itemSelect.classList.remove('d-none'); itemSelect.disabled = false; }
  else if (productType === 'FinishedGood') { fgSelect.classList.remove('d-none'); fgSelect.disabled = false; }
  else if (productType === 'ServiceType' && stSelect) { stSelect.classList.remove('d-none'); stSelect.disabled = false; }

  clearProductInfo(index);
}

function loadProductInfo(index) {
  const productType = document.querySelector(`select[name="LineItems[${index}].ProductType"]`).value;
  let productId = '';

  if (productType === 'Item') {
    productId = document.querySelector(`select[name="LineItems[${index}].ItemId"]`)?.value || '';
  } else if (productType === 'FinishedGood') {
    productId = document.querySelector(`select[name="LineItems[${index}].FinishedGoodId"]`)?.value || '';
  } else if (productType === 'ServiceType') {
    productId = document.querySelector(`select[name="LineItems[${index}].ServiceTypeId"]`)?.value || '';
  }

  if (!productId) { clearProductInfo(index); return; }

  const productInfoEl = document.querySelector(`#lineItem_${index} .product-info`);
  if (productInfoEl) productInfoEl.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Loading...';

  fetch(`/Sales/GetProductInfoForLineItem?productType=${productType}&productId=${productId}`)
    .then(r => r.json())
    .then(data => {
      if (data.success) {
        updateProductInfo(index, data.productInfo);
        autoFillPrice(index, data.productInfo);
      } else {
        showProductError(index, data.message || 'Error loading product information');
      }
    })
    .catch(err => { console.error('Error loading product info:', err); showProductError(index, 'Error loading product information'); });
}

function updateProductInfo(index, productInfo) {
  const el = document.querySelector(`#lineItem_${index} .product-info`);
  if (!el) return;

  let infoText = `${productInfo.partNumber || ''} - ${productInfo.description || productInfo.serviceName || ''}`;
  if (productInfo.tracksInventory) {
    const cls = productInfo.currentStock > 0 ? 'text-success' : 'text-warning';
    infoText += ` | <span class="${cls}">Stock: ${productInfo.currentStock}</span>`;
  } else {
    infoText += ' | <span class="text-info">Service (no inventory)</span>';
  }
  el.innerHTML = infoText;

  const row = document.getElementById(`lineItem_${index}`);
  if (row) {
    row.setAttribute('data-tracks-inventory', productInfo.tracksInventory);
    row.setAttribute('data-current-stock', productInfo.currentStock || 0);
    row.setAttribute('data-part-number', productInfo.partNumber || '');
  }
}

function autoFillPrice(index, productInfo) {
  const priceInput = document.querySelector(`input[name="LineItems[${index}].UnitPrice"]`);
  const suggestedBtn = document.querySelector(`#lineItem_${index} .suggested-price-btn`);
  if (!priceInput) return;

  priceInput.value = (productInfo.suggestedPrice || 0).toFixed(2);
  suggestedPrices[index] = productInfo.suggestedPrice || 0;

  if (suggestedBtn) {
    suggestedBtn.classList.remove('d-none');
    suggestedBtn.setAttribute('data-price', productInfo.suggestedPrice);
    suggestedBtn.className = productInfo.hasSalePrice
      ? 'btn btn-success btn-sm suggested-price-btn'
      : 'btn btn-outline-info btn-sm suggested-price-btn';
    suggestedBtn.title = productInfo.hasSalePrice ? 'Use set price' : 'Use calculated price';
  }

  calculateLineTotal(index);
}

function useSuggestedPrice(index) {
  const priceInput = document.querySelector(`input[name="LineItems[${index}].UnitPrice"]`);
  if (priceInput && suggestedPrices[index] !== undefined) {
    priceInput.value = suggestedPrices[index].toFixed(2);
    calculateLineTotal(index);
    priceInput.style.backgroundColor = '#d4edda';
    setTimeout(() => { priceInput.style.backgroundColor = ''; }, 1000);
  }
}

function clearProductInfo(index) {
  const el = document.querySelector(`#lineItem_${index} .product-info`);
  if (el) el.innerHTML = '';
  const priceInput = document.querySelector(`input[name="LineItems[${index}].UnitPrice"]`);
  if (priceInput) priceInput.value = '';
  const btn = document.querySelector(`#lineItem_${index} .suggested-price-btn`);
  if (btn) btn.classList.add('d-none');
  delete suggestedPrices[index];
  calculateLineTotal(index);
}

function showProductError(index, message) {
  const el = document.querySelector(`#lineItem_${index} .product-info`);
  if (el) el.innerHTML = `<span class="text-danger"><i class="fas fa-exclamation-triangle"></i> ${message}</span>`;
}

// ─── Calculations ─────────────────────────────────────────────────────────────

function calculateLineTotal(index) {
  try {
    const qty = parseFloat(document.querySelector(`input[name="LineItems[${index}].Quantity"]`)?.value) || 0;
    const price = parseFloat(document.querySelector(`input[name="LineItems[${index}].UnitPrice"]`)?.value) || 0;
    const total = qty * price;
    const display = document.querySelector(`#lineItem_${index} .line-total-display`);
    if (display) display.textContent = '$' + total.toFixed(2);
    validateLineItemStock(index, qty);
    calculateTotals();
  } catch (err) { console.error(`Error calculating line total for ${index}:`, err); }
}

function validateLineItemStock(index, quantity) {
  const row = document.getElementById(`lineItem_${index}`);
  if (!row) return;
  const tracksInventory = row.getAttribute('data-tracks-inventory') === 'true';
  const currentStock = parseInt(row.getAttribute('data-current-stock')) || 0;
  const partNumber = row.getAttribute('data-part-number') || '';
  const qtyInput = document.querySelector(`input[name="LineItems[${index}].Quantity"]`);
  if (!qtyInput) return;
  qtyInput.classList.remove('is-invalid', 'is-valid');
  if (tracksInventory && quantity > currentStock) {
    qtyInput.classList.add('is-invalid');
    qtyInput.title = `Insufficient stock for ${partNumber}. Available: ${currentStock}`;
  } else if (tracksInventory && quantity <= currentStock && quantity > 0) {
    qtyInput.classList.add('is-valid');
    qtyInput.title = '';
  }
}

function calculateTotals() {
  let subtotal = 0, totalQuantity = 0, itemCount = 0;

  document.querySelectorAll('.line-item-row').forEach(row => {
    const amount = parseFloat(row.querySelector('.line-total-display')?.textContent.replace(/[$,]/g, '')) || 0;
    const qty = parseFloat(row.querySelector('.quantity-input')?.value) || 0;
    if (amount > 0) { subtotal += amount; itemCount++; }
    if (qty > 0) totalQuantity += qty;
  });

  const shipping = parseFloat(document.querySelector('input[name="ShippingCost"]')?.value) || 0;
  const tax = parseFloat(document.querySelector('input[name="TaxAmount"]')?.value) || 0;

  const discountType = document.querySelector('select[name="DiscountType"]')?.value;
  const discountAmtInput = document.querySelector('input[name="DiscountAmount"]');
  const discountPctInput = document.querySelector('input[name="DiscountPercentage"]');
  let discount = 0;

  if (discountType === 'Percentage' && discountPctInput) {
    discount = subtotal * (parseFloat(discountPctInput.value) || 0) / 100;
    if (discountAmtInput) discountAmtInput.value = discount.toFixed(2);
  } else if (discountAmtInput) {
    discount = parseFloat(discountAmtInput.value) || 0;
  }

  const total = subtotal + shipping + tax - discount;
  updateDisplayElement('subtotalDisplay', subtotal);
  updateDisplayElement('discountDisplay', discount, true);
  updateDisplayElement('totalDisplay', total);
  updateDisplayElement('itemCountDisplay', itemCount, false, false);
  updateDisplayElement('totalQtyDisplay', totalQuantity, false, false);

  const discountRow = document.querySelector('.discount-row');
  if (discountRow) discountRow.style.display = discount > 0 ? 'table-row' : 'none';

  validateForm();
}

function updateDisplayElement(id, value, isNegative = false, isCurrency = true) {
  const el = document.getElementById(id);
  if (el) {
    el.textContent = isCurrency
      ? (isNegative ? '-' : '') + '$' + Math.abs(value).toFixed(2)
      : value.toString();
  }
}

function toggleDiscountInputs() {
  const type = document.querySelector('select[name="DiscountType"]')?.value;
  const amtDiv = document.getElementById('discountAmountDiv');
  const pctDiv = document.getElementById('discountPercentageDiv');
  const amtInput = document.querySelector('input[name="DiscountAmount"]');
  const pctInput = document.querySelector('input[name="DiscountPercentage"]');

  if (type === 'Percentage') {
    if (amtDiv) amtDiv.style.display = 'none';
    if (pctDiv) pctDiv.style.display = 'block';
    if (amtInput) amtInput.value = '0';
  } else {
    if (amtDiv) amtDiv.style.display = 'block';
    if (pctDiv) pctDiv.style.display = 'none';
    if (pctInput) pctInput.value = '0';
  }
  calculateTotals();
}

function validateForm() {
  const hasLineItems = Array.from(document.querySelectorAll('.line-item-row')).some(row => {
    const amount = parseFloat(row.querySelector('.line-total-display')?.textContent.replace(/[$,]/g, '')) || 0;
    return amount > 0;
  });
  const customerIdVal = document.querySelector('input[name="CustomerId"]')?.value;
  const hasCustomer = customerIdVal && customerIdVal !== '';
  const hasStockErrors = document.querySelectorAll('input.is-invalid').length > 0;

  const btn = document.getElementById('createSaleBtn');
  if (btn) {
    const isValid = hasLineItems && hasCustomer && !hasStockErrors;
    btn.disabled = !isValid;
    const isQuotation = isQuotationMode();
    if (!hasCustomer) btn.innerHTML = '<i class="fas fa-exclamation-triangle"></i> Select Customer';
    else if (!hasLineItems) btn.innerHTML = '<i class="fas fa-exclamation-triangle"></i> Add Items';
    else if (hasStockErrors) btn.innerHTML = '<i class="fas fa-exclamation-triangle"></i> Check Stock';
    else if (isQuotation) btn.innerHTML = '<i class="fas fa-file-alt"></i> Create Quotation';
    else btn.innerHTML = '<i class="fas fa-save"></i> Create Complete Sale';
  }
}

// ─── Customer Info ────────────────────────────────────────────────────────────

function loadCustomerInfo(customerId) {
  if (!customerId) { clearCustomerInfo(); return; }
  fetch(`/Customers/GetCustomerInfo/${customerId}`)
    .then(r => r.json())
    .then(data => { if (data.success) populateCustomerInfo(data.customer); })
    .catch(err => console.error('Error loading customer info:', err));
}

function populateCustomerInfo(customer) {
  const addr = document.querySelector('textarea[name="ShippingAddress"]');
  if (addr && customer.fullShippingAddress) addr.value = customer.fullShippingAddress;
  const terms = document.querySelector('select[name="Terms"]');
  if (terms && customer.paymentTerms !== undefined) { terms.value = customer.paymentTerms; updateDueDate(); }
}

function clearCustomerInfo() {
  const addr = document.querySelector('textarea[name="ShippingAddress"]');
  if (addr) addr.value = '';
}

function updateDueDate() {
  const saleDate = document.querySelector('input[name="SaleDate"]')?.value;
  const termsValue = parseInt(document.querySelector('select[name="Terms"]')?.value);
  const dueInput = document.querySelector('input[name="PaymentDueDate"]');
  if (!saleDate || !dueInput || isNaN(termsValue)) return;

  const date = new Date(saleDate);
  if (isNaN(date.getTime())) return;

  // PaymentTerms enum: Immediate=0, Net10=10, Net15=15, Net30=30, Net45=45, Net60=60, PrePayment=998, COD=999
  // The enum value IS the number of days, except PrePayment (998) and COD (999) which mean immediate/0 days.
  const days = (termsValue === 998 || termsValue === 999) ? 0 : termsValue;
  date.setDate(date.getDate() + days);
  dueInput.value = date.toISOString().split('T')[0];
}

// ─── Form Submission ──────────────────────────────────────────────────────────

document.addEventListener('DOMContentLoaded', function () {
  const form = document.getElementById('enhancedSaleForm');
  if (form) {
    form.addEventListener('submit', function (e) {
      // Check for stock availability errors first
      if (document.querySelectorAll('input.is-invalid').length > 0) {
        e.preventDefault();
        alert('Please resolve stock availability issues before submitting.');
        return false;
      }

      // Check jQuery unobtrusive validation if available
      var $form = $(form);
      if ($form.valid && !$form.valid()) {
        // jQuery validation will handle showing errors and preventing submit
        // Do NOT show spinner or disable button
        return; // let jQuery validation's handler call preventDefault
      }

      // Validation passed — show spinner and disable to prevent double-submit
      const btn = document.getElementById('createSaleBtn');
      if (btn) {
        btn.disabled = true;
        const isQuotation = isQuotationMode();
        btn.innerHTML = isQuotation
          ? '<i class="fas fa-spinner fa-spin"></i> Creating Quotation...'
          : '<i class="fas fa-spinner fa-spin"></i> Creating Sale...';
      }

      // Safety net: if the page doesn't navigate within 15 seconds
      // (e.g. server returned a validation error page), reset the button.
      setTimeout(function () {
        if (btn) {
          btn.disabled = false;
          validateForm(); // restore correct button text & state
        }
      }, 15000);

      return true;
    });
  }
});