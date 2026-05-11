
function viewOrderDetails(orderNo, status, payment, seller, receiver, phone, address, total, color, size, qty) {
    // 1. Set ID and Status Header
    document.getElementById('modalOrderNumber').innerText = orderNo;
    document.getElementById('modalStatusText').innerText = status.toUpperCase();

    // 2. Dynamic Banner Logic
    const subtext = document.getElementById('modalStatusSubtext');
    const icon = document.getElementById('modalStatusIcon');

    if (status.toUpperCase() === "TO PAY") {
        subtext.innerText = "Awaiting payment confirmation..";
        icon.className = "bi bi-wallet2"; // Changes icon to wallet
    } else {
        subtext.innerText = "Awaiting updates...";
        icon.className = "bi bi-box-seam"; // Default box icon
    }

    // 3. Populate Display Section
    document.getElementById('displayReceiver').innerText = receiver;
    document.getElementById('displayPhone').innerText = phone;
    document.getElementById('displayAddress').innerText = address;
    document.getElementById('displayPayment').innerText = "Payment: " + payment;
    document.getElementById('modalTotalAmount').innerText = total;
    document.getElementById('displayColor').innerText = color || "-";
    document.getElementById('displaySize').innerText = size || "-";
    document.getElementById('displayQty').innerText = qty;

    // 4. Pre-fill Input Form
    document.getElementById('inputReceiver').value = receiver;
    document.getElementById('inputPhone').value = phone;
    document.getElementById('inputAddress').value = address;
    document.getElementById('inputPayment').value = payment;
    document.getElementById('inputColor').value = color || "Black";
    document.getElementById('inputSize').value = size || "Medium";
    document.getElementById('inputQty').value = qty;

    // 5. Status Check for Editing Permissions
    const editBtn = document.getElementById('editOrderBtn');
    const lockNote = document.getElementById('editLockNote');

    if (status.toUpperCase() === "TO PAY") {
        editBtn.classList.remove('d-none');
        lockNote.classList.add('d-none');
    } else {
        editBtn.classList.add('d-none');
        lockNote.classList.remove('d-none');
    }

    // 6. Reset UI to Default Display View
    document.getElementById('orderDetailsDisplay').classList.remove('d-none');
    document.getElementById('productDetailsDisplay').classList.remove('d-none');
    document.getElementById('orderEditForm').classList.add('d-none');
    editBtn.innerText = "Edit";

    // 7. Open Overlay
    document.getElementById('customOrderOverlay').classList.add('active');
    document.body.style.overflow = 'hidden'; // Prevent background scrolling
}

/**
 * Toggles between the Display view and the Edit form
 */
function toggleOrderEdit() {
    const displayDetails = document.getElementById('orderDetailsDisplay');
    const displayProduct = document.getElementById('productDetailsDisplay');
    const form = document.getElementById('orderEditForm');
    const btn = document.getElementById('editOrderBtn');

    if (form.classList.contains('d-none')) {
        displayDetails.classList.add('d-none');
        displayProduct.classList.add('d-none');
        form.classList.remove('d-none');
        btn.innerText = "Cancel";
    } else {
        displayDetails.classList.remove('d-none');
        displayProduct.classList.remove('d-none');
        form.classList.add('d-none');
        btn.innerText = "Edit";
    }
}

/**
 * Validates and Saves local changes to the UI
 */
function saveOrderChanges() {
    const newName = document.getElementById('inputReceiver').value;
    const newPhone = document.getElementById('inputPhone').value;
    const newAddr = document.getElementById('inputAddress').value;
    const newPay = document.getElementById('inputPayment').value;
    const newColor = document.getElementById('inputColor').value;
    const newSize = document.getElementById('inputSize').value;
    const newQty = document.getElementById('inputQty').value;

    if (!newName || !newPhone || !newAddr) {
        showToast("Please fill in required fields", "error");
        return;
    }

    // Update Display UI
    document.getElementById('displayReceiver').innerText = newName;
    document.getElementById('displayPhone').innerText = newPhone;
    document.getElementById('displayAddress').innerText = newAddr;
    document.getElementById('displayPayment').innerText = "Payment: " + newPay;
    document.getElementById('displayColor').innerText = newColor;
    document.getElementById('displaySize').innerText = newSize;
    document.getElementById('displayQty').innerText = newQty;

    toggleOrderEdit();
    showToast("Order details updated locally!");
}

/**
 * Copies the Order ID to the user's clipboard
 */
function copyOrderId() {
    const orderId = document.getElementById('modalOrderNumber').innerText;
    navigator.clipboard.writeText(orderId).then(() => {
        showToast("Order ID copied to clipboard!");
    }).catch(err => {
        console.error('Failed to copy: ', err);
    });
}

/**
 * Closes the Order Details Overlay
 */
function closeOrderDetails() {
    document.getElementById('customOrderOverlay').classList.remove('active');
    document.body.style.overflow = 'auto'; // Restore scrolling
}

/**
 * Displays a monochrome Toast notification
 * @param {string} message 
 * @param {string} type - "success" or "error"
 */
function showToast(message, type = "success") {
    const container = document.getElementById('toastContainer');
    const toast = document.createElement('div');
    toast.className = `mono-toast ${type}`;

    const icon = type === "success" ? "bi-check-circle-fill" : "bi-exclamation-circle-fill";

    toast.innerHTML = `<i class="bi ${icon}"></i><span>${message}</span>`;
    container.appendChild(toast);

    // Fade out and remove
    setTimeout(() => {
        toast.classList.add('hide');
        setTimeout(() => toast.remove(), 400);
    }, 3000);
}