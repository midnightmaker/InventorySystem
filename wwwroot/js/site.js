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