/* ------------------------------
   PAYOUT ACCOUNTS MANAGEMENT
--------------------------------*/
console.log('payoutAccounts.js loaded');

function bindRemoveButtons() {
    document.querySelectorAll(".remove-account").forEach(function(btn) {
        btn.onclick = async function(e) {
            e.preventDefault();
            
            const accountId = this.dataset.accountId;
            console.log('Remove clicked for account:', accountId);
            
            // Show confirmation modal - FIXED FORMAT
            const confirmed = await Modal.confirm({
                title: 'Remove Account',
                message: 'Are you sure you want to remove this account?',
                detail: 'This action cannot be undone.', // Moved to detail field
                confirmText: 'Yes, Remove',
                cancelText: 'No, Keep',
                type: 'warning'
            });
            
            if (confirmed) {
                // Show loading state
                const originalText = this.textContent;
                this.textContent = 'Removing...';
                this.style.opacity = '0.5';
                this.style.pointerEvents = 'none';
                
                try {
                    const response = await fetch('/Dashboard/RemovePayoutAccount', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                        },
                        body: JSON.stringify({ accountId: parseInt(accountId) })
                    });
                    
                    const result = await response.json();
                    
                    if (result.success) {
                        // Success - remove the row
                        const row = this.closest("tr");
                        if (row) {
                            row.remove();
                        }
                        
                        Toast.success('Account removed successfully');
                        
                        // Check if table is now empty
                        const tbody = document.querySelector('tbody');
                        if (tbody && tbody.children.length === 0) {
                            setTimeout(() => location.reload(), 1500);
                        }
                    } else {
                        Modal.error('Error removing account', 'Error', result.message);
                    }
                } catch (error) {
                    console.error('Error:', error);
                    Modal.error('Error removing account', 'Error', error.message);
                } finally {
                    // Restore button state
                    this.textContent = originalText;
                    this.style.opacity = '1';
                    this.style.pointerEvents = 'auto';
                }
            }
        };
    });
}

// Run when DOM is ready
document.addEventListener('DOMContentLoaded', function() {
    bindRemoveButtons();
});

// Also run after a short delay to catch dynamically loaded content
setTimeout(function() {
    bindRemoveButtons();
}, 500);