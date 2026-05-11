document.addEventListener('DOMContentLoaded', () => {
    const searchInput = document.getElementById('sellerSearchInput');

    if (searchInput) {
        searchInput.addEventListener('keyup', function () {
            const filter = this.value.toLowerCase();
            // Target rows in the Active Sellers table
            const rows = document.querySelectorAll('#view-active tbody tr');

            rows.forEach(row => {
                // Get text content from the specific columns
                const shopName = row.cells[0].textContent.toLowerCase();
                const owner = row.cells[1].textContent.toLowerCase();
                const contact = row.cells[2].textContent.toLowerCase();
                const dateJoined = row.cells[3].textContent.toLowerCase();
                const products = row.cells[4].textContent.toLowerCase();
                const sales = row.cells[5].textContent.toLowerCase();

                // Check if the search term exists in ANY of these columns
                if (shopName.includes(filter) ||
                    owner.includes(filter) ||
                    contact.includes(filter) ||
                    dateJoined.includes(filter) ||
                    products.includes(filter) ||
                    sales.includes(filter)) {
                    row.style.display = ""; // Show
                } else {
                    row.style.display = "none"; // Hide
                }
            });
        });
    }
});

function switchSellerTab(viewName) {
    // Select all views and buttons
    const views = {
        active: document.getElementById('view-active'),
        performance: document.getElementById('view-performance'),
        archived: document.getElementById('view-archived')
    };
    const buttons = {
        active: document.getElementById('tab-active'),
        performance: document.getElementById('tab-performance'),
        archived: document.getElementById('tab-archived')
    };

    // Reset all
    Object.keys(views).forEach(key => {
        views[key].classList.add('d-none');
        buttons[key].classList.remove('bg-dark', 'text-white', 'fw-bold');
        buttons[key].classList.add('text-muted');
    });

    // Show selected
    views[viewName].classList.remove('d-none');
    buttons[viewName].classList.add('bg-dark', 'text-white', 'fw-bold');
    buttons[viewName].classList.remove('text-muted');
}


// --- TOAST HELPER ---
function triggerSellerToast(msg, isError = false) {
    const toastMessage = document.getElementById('sellerToastMessage');
    const toastIcon = document.getElementById('sellerToastIcon');

    toastMessage.innerText = msg;
    toastIcon.className = isError ? "bi bi-exclamation-circle-fill text-danger fs-5" : "bi bi-check-circle-fill text-success fs-5";

    const toastEl = document.getElementById('sellerToast');
    const toast = new bootstrap.Toast(toastEl, { delay: 3000 });
    toast.show();
}

// --- VIEW HANDLER ---
function openViewModal(shopName, owner, sales, products) {
    document.getElementById('viewShopName').innerText = shopName;
    document.getElementById('viewOwner').innerText = owner;
    document.getElementById('viewSales').innerText = sales;
    document.getElementById('viewProducts').innerText = products;
    new bootstrap.Modal(document.getElementById('viewSellerModal')).show();
}

// --- EDIT HANDLER ---
function openEditModal(shopName, email) {
    document.getElementById('editShopName').value = shopName;
    document.getElementById('editEmail').value = email;
    new bootstrap.Modal(document.getElementById('editSellerModal')).show();
}

// --- CONFIRMATION HANDLER (Archive/Restore/Update) ---
function openConfirmModal(type, shopName) {
    const title = document.getElementById('sellerConfirmTitle');
    const msg = document.getElementById('sellerConfirmMessage');
    const icon = document.getElementById('sellerConfirmIcon');
    const btn = document.getElementById('sellerConfirmBtn');

    if (type === 'delete') {
        title.innerText = "Archive Shop?";
        msg.innerText = `Are you sure you want to archive ${shopName}?`;
        icon.innerHTML = '<i class="bi bi-archive text-danger fs-1"></i>';
        btn.className = "btn btn-danger rounded-pill px-4";
        btn.onclick = () => confirmAction(`Shop ${shopName} archived.`);
    } else if (type === 'restore') {
        title.innerText = "Restore Shop?";
        msg.innerText = `Bring ${shopName} back to active list?`;
        icon.innerHTML = '<i class="bi bi-arrow-counterclockwise text-success fs-1"></i>';
        btn.className = "btn btn-success rounded-pill px-4";
        btn.onclick = () => confirmAction(`Shop ${shopName} restored successfully.`);
    }

    new bootstrap.Modal(document.getElementById('confirmSellerModal')).show();
}

function confirmAction(toastMsg) {
    // Hide all possible modals that could be open
    const confirmMod = bootstrap.Modal.getInstance(document.getElementById('confirmSellerModal'));
    const editMod = bootstrap.Modal.getInstance(document.getElementById('editSellerModal'));

    if (confirmMod) confirmMod.hide();
    if (editMod) editMod.hide();

    // Trigger Success Toast
    triggerSellerToast(toastMsg || "Action confirmed!");
}