
let currentOrderId = 0;

function viewOrderDetails(orderId, orderNo, status, payment, seller, receiver, phone, address, total, color, size, qty) {
    if (arguments.length === 11) {
        qty = size;
        size = color;
        color = total;
        total = address;
        address = phone;
        phone = receiver;
        receiver = seller;
        seller = payment;
        payment = status;
        status = orderNo;
        orderNo = orderId;
        orderId = 0;
    }

    currentOrderId = parseInt(orderId, 10) || 0;
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
async function saveOrderChanges() {
    const newName = document.getElementById('inputReceiver').value;
    const newPhone = document.getElementById('inputPhone').value;
    const newAddr = document.getElementById('inputAddress').value;
    const newPay = document.getElementById('inputPayment').value;
    const newColor = document.getElementById('inputColor').value;
    const newSize = document.getElementById('inputSize').value;
    const newQty = document.getElementById('inputQty').value;

    if (!currentOrderId || !newName || !newPhone || !newAddr) {
        showToast("Please fill in required fields", "error");
        return;
    }

    try {
        const response = await fetch('/AccountProfile/UpdatePurchaseDetails', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                orderId: currentOrderId,
                receiverName: newName,
                phoneNumber: newPhone,
                shippingAddress: newAddr,
                paymentMethod: newPay,
                color: newColor,
                size: newSize,
                quantity: parseInt(newQty, 10) || 1
            })
        });

        const payload = await readJsonSafe(response);
        if (!response.ok || payload.success === false) {
            throw new Error(payload.message || 'Could not save order details.');
        }

        document.getElementById('displayReceiver').innerText = newName;
        document.getElementById('displayPhone').innerText = newPhone;
        document.getElementById('displayAddress').innerText = newAddr;
        document.getElementById('displayPayment').innerText = "Payment: " + newPay;
        document.getElementById('displayColor').innerText = newColor;
        document.getElementById('displaySize').innerText = newSize;
        document.getElementById('displayQty').innerText = newQty;

        toggleOrderEdit();
        showToast("Order details saved.");
    } catch (error) {
        showToast(error.message || "Could not save order details.", "error");
    }
}

async function updatePurchaseStatus(orderId, action) {
    const parsedOrderId = parseInt(orderId, 10) || 0;
    if (!parsedOrderId) {
        showToast("Invalid order.", "error");
        return;
    }

    try {
        const response = await fetch('/AccountProfile/UpdatePurchaseStatus', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ orderId: parsedOrderId, action: action })
        });

        const payload = await readJsonSafe(response);
        if (!response.ok || payload.success === false) {
            throw new Error(payload.message || 'Could not update order.');
        }

        showToast(payload.message || "Order updated.");
        window.location.reload();
    } catch (error) {
        showToast(error.message || "Could not update order.", "error");
    }
}

async function confirmReceive(orderId) {
    const parsedOrderId = parseInt(orderId, 10) || 0;
    if (!parsedOrderId) {
        showToast("Invalid order.", "error");
        return;
    }

    try {
        const response = await fetch('/AccountProfile/ConfirmReceive', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Accept': 'application/json',
                'X-Requested-With': 'XMLHttpRequest'
            },
            body: JSON.stringify({ orderId: parsedOrderId })
        });

        const contentType = response.headers.get('content-type') || '';
        if (!contentType.toLowerCase().includes('application/json')) {
            throw new Error(response.redirected ? 'Please log in again.' : 'The server did not return a valid response.');
        }

        const payload = await response.json();
        if (!response.ok || payload.success === false) {
            throw new Error(payload.message || 'Could not confirm this order.');
        }

        showToast(payload.message || "Order received.");
        window.location.href = '/AccountProfile/MyPurchases?status=To%20Review';
    } catch (error) {
        showToast(error.message || "Could not confirm this order.", "error");
    }
}

function openReturnRequest(orderId, orderNumber, productName) {
    const parsedOrderId = parseInt(orderId, 10) || 0;
    if (!parsedOrderId) {
        showToast("Invalid order.", "error");
        return;
    }

    const overlay = document.getElementById('returnRequestOverlay');
    const form = document.getElementById('returnRequestForm');
    const preview = document.getElementById('returnProofPreview');
    const subtitle = document.getElementById('returnOrderSubtitle');

    document.getElementById('returnOrderId').value = parsedOrderId;
    if (form) form.reset();
    if (preview) preview.innerHTML = '';
    if (subtitle) {
        subtitle.innerText = `${orderNumber || 'Order'} - ${productName || 'Return item'}`;
    }

    overlay.classList.add('active');
    document.body.style.overflow = 'hidden';
}

function closeReturnRequest() {
    const overlay = document.getElementById('returnRequestOverlay');
    const preview = document.getElementById('returnProofPreview');
    const form = document.getElementById('returnRequestForm');

    if (overlay) overlay.classList.remove('active');
    if (preview) preview.innerHTML = '';
    if (form) form.reset();
    document.body.style.overflow = 'auto';
}

function previewReturnProofImages(input) {
    const preview = document.getElementById('returnProofPreview');
    if (!preview) return;

    preview.innerHTML = '';
    Array.from(input.files || []).slice(0, 12).forEach(file => {
        if (!file.type.startsWith('image/')) return;

        const image = document.createElement('img');
        image.alt = file.name || 'Return proof image';
        preview.appendChild(image);

        const reader = new FileReader();
        reader.onload = event => {
            image.src = event.target.result;
        };
        reader.readAsDataURL(file);
    });
}

async function submitReturnRequest() {
    const orderId = parseInt(document.getElementById('returnOrderId')?.value || '0', 10);
    const reason = document.getElementById('returnReason')?.value || '';
    const fileInput = document.getElementById('returnProofImages');
    const submitBtn = document.getElementById('submitReturnRequestBtn');

    if (!orderId) {
        showToast("Invalid order.", "error");
        return;
    }

    if (!reason.trim()) {
        showToast("Please select a return reason.", "error");
        return;
    }

    if (!fileInput || !fileInput.files || fileInput.files.length === 0) {
        showToast("Please upload at least one proof image.", "error");
        return;
    }

    const formData = new FormData();
    formData.append('OrderId', orderId);
    formData.append('ReturnReason', reason);
    Array.from(fileInput.files).forEach(file => {
        formData.append('ReturnProofImages', file);
    });

    const originalText = submitBtn ? submitBtn.innerText : '';
    if (submitBtn) {
        submitBtn.disabled = true;
        submitBtn.innerText = 'SUBMITTING...';
    }

    try {
        const response = await fetch('/AccountProfile/RequestReturn', {
            method: 'POST',
            headers: { 'Accept': 'application/json' },
            body: formData
        });

        const payload = await readJsonSafe(response);
        if (!response.ok || payload.success === false) {
            throw new Error(payload.message || 'Could not submit return request.');
        }

        showToast(payload.message || "Return requested.");
        window.location.href = '/AccountProfile/MyPurchases?status=Returns';
    } catch (error) {
        showToast(error.message || "Could not submit return request.", "error");
    } finally {
        if (submitBtn) {
            submitBtn.disabled = false;
            submitBtn.innerText = originalText || 'SUBMIT RETURN';
        }
    }
}

document.addEventListener('DOMContentLoaded', () => {
    const returnProofInput = document.getElementById('returnProofImages');
    if (returnProofInput) {
        returnProofInput.addEventListener('change', function () {
            previewReturnProofImages(this);
        });
    }
});

async function readJsonSafe(response) {
    try {
        return await response.json();
    } catch {
        return {};
    }
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
