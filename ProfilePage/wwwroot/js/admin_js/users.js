
document.addEventListener('DOMContentLoaded', () => {
    const searchInput = document.querySelector('.search-container input');

    if (searchInput) {
        searchInput.addEventListener('keyup', function () {
            const filter = this.value.toLowerCase();
            // Target the rows in the active view table
            const rows = document.querySelectorAll('#view-active tbody tr');

            rows.forEach(row => {
                // Mapping the data based on your table structure:
                // Column 0: Name
                // Column 1: Number
                // Column 2: Email
                // Column 3: Address
                const name = row.cells[0].textContent.toLowerCase();
                const number = row.cells[1].textContent.toLowerCase();
                const email = row.cells[2].textContent.toLowerCase();
                const address = row.cells[3].textContent.toLowerCase();

                // If any of these columns contain the search term, show the row
                if (name.includes(filter) ||
                    number.includes(filter) ||
                    email.includes(filter) ||
                    address.includes(filter)) {
                    row.style.display = "";
                } else {
                    row.style.display = "none";
                }
            });
        });
    }
});

// Tab Switching
function switchUserTab(viewName) {
    const activeView = document.getElementById('view-active');
    const archivedView = document.getElementById('view-archived');
    const activeTabBtn = document.getElementById('tab-active');
    const archivedTabBtn = document.getElementById('tab-archived');

    if (viewName === 'active') {
        activeView.classList.remove('d-none');
        archivedView.classList.add('d-none');
        activeTabBtn.classList.add('bg-dark', 'text-white', 'fw-bold');
        activeTabBtn.classList.remove('text-muted');
        archivedTabBtn.classList.add('text-muted');
        archivedTabBtn.classList.remove('bg-dark', 'text-white', 'fw-bold');
    } else {
        archivedView.classList.remove('d-none');
        activeView.classList.add('d-none');
        archivedTabBtn.classList.add('bg-dark', 'text-white', 'fw-bold');
        archivedTabBtn.classList.remove('text-muted');
        activeTabBtn.classList.add('text-muted');
        activeTabBtn.classList.remove('bg-dark', 'text-white', 'fw-bold');
    }
}

// Toast Helper
function triggerToast(msg, iconClass = "text-success", iconType = "bi-check-circle-fill") {
    document.getElementById('toastMessage').innerText = msg;
    const icon = document.getElementById('toastIcon');
    icon.className = `bi ${iconType} ${iconClass} fs-5`;

    const toastEl = document.getElementById('actionToast');
    const toast = new bootstrap.Toast(toastEl);
    toast.show();
}

// Modal Handlers
function openEditModal(name) {
    document.getElementById('editName').value = name;
    new bootstrap.Modal(document.getElementById('editModal')).show();
}

function saveChanges() {
    bootstrap.Modal.getInstance(document.getElementById('editModal')).hide();
    triggerToast("Changes saved successfully!");
}

function openConfirmModal(type, name) {
    const title = document.getElementById('confirmTitle');
    const msg = document.getElementById('confirmMessage');
    const icon = document.getElementById('confirmIcon');
    const btn = document.getElementById('confirmBtn');

    if (type === 'delete') {
        title.innerText = "Archive User?";
        msg.innerText = `Move ${name} to archives?`;
        icon.innerHTML = '<i class="bi bi-trash text-danger" style="font-size: 3rem;"></i>';
        btn.className = "btn btn-danger rounded-pill px-4";
        btn.onclick = () => {
            bootstrap.Modal.getInstance(document.getElementById('confirmModal')).hide();
            triggerToast(`${name} archived!`, "text-danger", "bi-archive-fill");
        };
    } else {
        title.innerText = "Restore User?";
        msg.innerText = `Restore ${name} to active list?`;
        icon.innerHTML = '<i class="bi bi-arrow-counterclockwise text-success" style="font-size: 3rem;"></i>';
        btn.className = "btn btn-success rounded-pill px-4";
        btn.onclick = () => {
            bootstrap.Modal.getInstance(document.getElementById('confirmModal')).hide();
            triggerToast(`${name} restored!`);
        };
    }
    new bootstrap.Modal(document.getElementById('confirmModal')).show();
}