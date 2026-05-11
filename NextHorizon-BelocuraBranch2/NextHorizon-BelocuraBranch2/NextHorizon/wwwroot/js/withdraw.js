// withdraw.js
document.addEventListener("DOMContentLoaded", function () {
    // Initialize elements
    const maxBtn = document.querySelector(".max-btn");
    const amountInput = document.getElementById("Amount");
    const balanceDisplay = document.querySelector(".balance-display");
    const submitBtn = document.getElementById("submitBtn");
    const payoutSelect = document.getElementById("PayoutAccountId");
    const withdrawForm = document.getElementById("withdrawForm");
    const accountDetails = document.getElementById("accountDetails");

    // Set max amount functionality
    if (maxBtn && amountInput && balanceDisplay) {
        maxBtn.addEventListener("click", function () {
            // Get balance value
            let balanceText = balanceDisplay.textContent;
            // Remove peso sign and commas
            let balance = balanceText.replace("₱", "").replace(/,/g, "").trim();
            // Put value into input
            amountInput.value = balance;
            // Trigger validation
            validateAmount();
        });
    }

    // Quick amount buttons
    window.setAmount = function(amount) {
        if (amountInput) {
            amountInput.value = amount;
            validateAmount();
        }
    };

    window.setMaxAmount = function() {
        if (amountInput && balanceDisplay) {
            let balanceText = balanceDisplay.textContent;
            let balance = balanceText.replace("₱", "").replace(/,/g, "").trim();
            amountInput.value = balance;
            validateAmount();
        }
    };

    // Validate amount function
    window.validateAmount = function() {
        if (!amountInput || !submitBtn || !payoutSelect) return;
        
        const amount = parseFloat(amountInput.value) || 0;
        const maxAmount = parseFloat(amountInput.getAttribute('max')) || 0;
        const payoutAccount = payoutSelect.value;
        
        if (amount > maxAmount) {
            submitBtn.disabled = true;
            submitBtn.title = 'Amount cannot exceed available balance';
        } else if (amount < 100) {
            submitBtn.disabled = true;
            submitBtn.title = 'Minimum withdrawal amount is ₱100';
        } else if (!payoutAccount) {
            submitBtn.disabled = true;
            submitBtn.title = 'Please select a payout account';
        } else {
            submitBtn.disabled = false;
            submitBtn.title = '';
        }
    };

    // Show account details
    window.showAccountDetails = function() {
        if (!payoutSelect || !accountDetails) return;
        
        const selectedOption = payoutSelect.options[payoutSelect.selectedIndex];
        
        if (selectedOption && selectedOption.value) {
            const accountText = selectedOption.text;
            const isDefault = accountText.includes('(Default)');
            const cleanText = accountText.replace('(Default)', '').trim();
            
            accountDetails.innerHTML = `
                <i class="fas fa-check-circle"></i>
                <strong>Selected Account:</strong> ${cleanText}
                ${isDefault ? '<span class="default-badge">DEFAULT</span>' : ''}
            `;
            accountDetails.style.display = 'flex';
        } else {
            accountDetails.style.display = 'none';
        }
        
        validateAmount();
    };

    // Form submission handler
    if (withdrawForm) {
        withdrawForm.addEventListener('submit', function(e) {
            const amount = parseFloat(amountInput?.value || 0);
            const maxAmount = parseFloat(amountInput?.getAttribute('max') || 0);
            const payoutAccount = payoutSelect?.value;
            
            if (!payoutAccount) {
                e.preventDefault();
                alert('Please select a payout account');
                return;
            }
            
            if (amount > maxAmount) {
                e.preventDefault();
                alert('Amount cannot exceed available balance');
                return;
            }
            
            if (amount < 100) {
                e.preventDefault();
                alert('Minimum withdrawal amount is ₱100');
                return;
            }
            
            // Show loading state
            if (submitBtn) {
                submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Processing...';
                submitBtn.disabled = true;
            }
        });
    }

    // Input validation on change
    if (amountInput) {
        amountInput.addEventListener('input', validateAmount);
    }

    if (payoutSelect) {
        payoutSelect.addEventListener('change', showAccountDetails);
    }

    // Initial validation and display
    validateAmount();
    if (payoutSelect?.value) {
        showAccountDetails();
    }
});