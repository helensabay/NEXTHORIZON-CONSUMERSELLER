/**
 * Opens the Modal and Populates Data
 * Extended to handle Payment, Color, Size, and Qty display
 */
function viewOrderDetails(orderNo, status, payment, seller, receiver, phone, address, total, color, size, qty) {
    // 1. Populate Text Display Data (Delivery Section)
    document.getElementById('modalOrderNumber').innerText = orderNo;
    document.getElementById('modalStatusText').innerText = status.toUpperCase();
    document.getElementById('displayReceiver').innerText = receiver;
    document.getElementById('displayPhone').innerText = phone;
    document.getElementById('displayAddress').innerText = address;
    document.getElementById('displayPayment').innerText = "Payment: " + payment;
    document.getElementById('modalTotalAmount').innerText = total;

    // 2. Populate Product Display Data (Product Details Section)
    document.getElementById('displayColor').innerText = color || "N/A";
    document.getElementById('displaySize').innerText = size || "N/A";
    document.getElementById('displayQty').innerText = qty || "1";

    // 3. Pre-fill Edit Inputs (Form Section)
    document.getElementById('inputReceiver').value = receiver;
    document.getElementById('inputPhone').value = phone;
    document.getElementById('inputAddress').value = address;
    document.getElementById('inputPayment').value = payment;
    document.getElementById('inputColor').value = color || "";
    document.getElementById('inputSize').value = size || "";
    document.getElementById('inputQty').value = qty;

    // 4. Status Restriction: Only allow editing for "TO PAY"
    const editBtn = document.getElementById('editOrderBtn');
    const lockNote = document.getElementById('editLockNote');

    if (status.toUpperCase() === "TO PAY") {
        editBtn.classList.remove('d-none');
        lockNote.classList.add('d-none');
    } else {
        editBtn.classList.add('d-none');
        lockNote.classList.remove('d-none');
    }

    // 5. Reset UI State: Always show display mode first
    document.getElementById('orderDetailsDisplay').classList.remove('d-none');
    document.getElementById('productDetailsDisplay').classList.remove('d-none'); // Show product info
    document.getElementById('orderEditForm').classList.add('d-none');
    editBtn.innerText = "Edit";

    // 6. Show Overlay
    document.getElementById('customOrderOverlay').classList.add('active');
    document.body.style.overflow = 'hidden';
}

/**
 * Toggles between Info Display and Edit Inputs
 */
function toggleOrderEdit() {
    const displayDetails = document.getElementById('orderDetailsDisplay');
    const displayProduct = document.getElementById('productDetailsDisplay');
    const form = document.getElementById('orderEditForm');
    const btn = document.getElementById('editOrderBtn');

    if (form.classList.contains('d-none')) {
        displayDetails.classList.add('d-none');
        displayProduct.classList.add('d-none'); // Hide info when editing
        form.classList.remove('d-none');
        btn.innerText = "Cancel";
    } else {
        displayDetails.classList.remove('d-none');
        displayProduct.classList.remove('d-none'); // Show info when cancelled
        form.classList.add('d-none');
        btn.innerText = "Edit";
    }
}

/**
 * Saves Changes and Updates Display Labels
 */
function saveOrderChanges() {
    // Get values from inputs/selects
    const newName = document.getElementById('inputReceiver').value;
    const newPhone = document.getElementById('inputPhone').value;
    const newAddr = document.getElementById('inputAddress').value;
    const newPay = document.getElementById('inputPayment').value;
    const newColor = document.getElementById('inputColor').value;
    const newSize = document.getElementById('inputSize').value;
    const newQty = document.getElementById('inputQty').value;

    // Basic Validation
    if (!newName || !newPhone || !newAddr || !newQty) {
        showToast("Please fill in all required fields", "error");
        return;
    }

    // Update the display text in the modal (Delivery)
    document.getElementById('displayReceiver').innerText = newName;
    document.getElementById('displayPhone').innerText = newPhone;
    document.getElementById('displayAddress').innerText = newAddr;
    document.getElementById('displayPayment').innerText = "Payment: " + newPay;

    // Update the display text in the modal (Product Details)
    document.getElementById('displayColor').innerText = newColor || "N/A";
    document.getElementById('displaySize').innerText = newSize || "N/A";
    document.getElementById('displayQty').innerText = newQty;

    // Switch back to display mode
    toggleOrderEdit();
    showToast("Order details updated successfully!");
}

/**
 * Closes the Modal
 */
function closeOrderDetails() {
    const overlay = document.getElementById('customOrderOverlay');
    if (overlay) {
        overlay.classList.remove('active');
        document.body.style.overflow = 'auto';
    }
}

/**
 * Toast Notification System
 */
function showToast(message, type = "success") {
    const container = document.getElementById('toastContainer');
    if (!container) return;

    const toast = document.createElement('div');
    toast.className = `mono-toast ${type}`;
    const icon = type === "success" ? "bi-check-circle-fill" : "bi-exclamation-circle-fill";
    toast.innerHTML = `<i class="bi ${icon}"></i><span>${message}</span>`;

    container.appendChild(toast);

    setTimeout(() => {
        toast.classList.add('hide');
        setTimeout(() => {
            if (toast.parentNode === container) {
                container.removeChild(toast);
            }
        }, 400);
    }, 3000);
}

(function () {
    function setupPurchaseFilters() {
        const statusNav = document.querySelector('[data-status-tabs]');
        if (!statusNav) {
            return;
        }

        const tabs = Array.from(statusNav.querySelectorAll('.status-item[data-status]'));
        const orderCards = Array.from(document.querySelectorAll('.order-card[data-order-status]'));
        const emptyPlaceholder = document.getElementById('ordersFilterEmpty');
        const filterMessage = emptyPlaceholder?.querySelector('.filter-empty-message');

        if (!tabs.length || !orderCards.length) {
            if (emptyPlaceholder) {
                emptyPlaceholder.classList.add('d-none');
            }
            return;
        }

        const normalize = value => (value ?? '').trim().toLowerCase();

        const updateEmptyMessage = (status, visibleCount) => {
            if (!emptyPlaceholder) {
                return;
            }

            if (visibleCount === 0) {
                emptyPlaceholder.classList.remove('d-none');
                if (filterMessage) {
                    const normalized = normalize(status);
                    const label = normalized === 'all' || !status ? 'Orders' : `Orders with status "${status}"`;
                    filterMessage.innerText = `${label} will appear here.`;
                }
            } else {
                emptyPlaceholder.classList.add('d-none');
            }
        };

        const updateTabStates = selectedStatus => {
            tabs.forEach(tab => {
                tab.classList.toggle('active', normalize(tab.dataset.status) === normalize(selectedStatus));
            });
        };

        const filterOrders = selectedStatus => {
            const target = selectedStatus ?? '';
            const normalizedTarget = normalize(target);
            let visibleCount = 0;

            orderCards.forEach(card => {
                const cardStatus = normalize(card.dataset.orderStatus);
                const shouldShow = normalizedTarget === 'all' || cardStatus === normalizedTarget;
                card.classList.toggle('d-none', !shouldShow);
                if (shouldShow) {
                    visibleCount++;
                }
            });

            updateEmptyMessage(target, visibleCount);
            updateTabStates(selectedStatus);
        };

        tabs.forEach(tab => {
            tab.addEventListener('click', event => {
                event.preventDefault();
                filterOrders(tab.dataset.status ?? tab.textContent ?? 'All');
            });
        });

        const initialStatus = statusNav.dataset.defaultStatus ?? 'To Pay';
        filterOrders(initialStatus);
    }

    document.addEventListener('DOMContentLoaded', setupPurchaseFilters);
})();
