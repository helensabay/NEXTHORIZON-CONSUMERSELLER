document.addEventListener('DOMContentLoaded', function () {
    let currentEditingRowId = null;

    const rowMappings = {
        'name-row': { id: 'universalModal', title: 'Full Name', required: true },
        'motto-row': { id: 'mottoModal', required: false },
        'gender-row': { id: 'genderModal', required: false },
        'birthday-row': { id: 'birthdayModal', required: false },
        'phone-row': { id: 'universalModal', title: 'Phone Number', required: true },
        'email-row': { id: 'universalModal', title: 'Email Address', required: true },
        'password-row': { id: 'passwordModal', required: true }
    };

    // --- 1. Row Click Listeners ---
    Object.keys(rowMappings).forEach(rowId => {
        const element = document.getElementById(rowId);
        if (element) {
            element.addEventListener('click', function () {
                currentEditingRowId = rowId;
                const config = rowMappings[rowId];

                if (config.id === 'universalModal') {
                    const titleElement = document.getElementById('modalTitle');
                    const inputElement = document.getElementById('modalInput');
                    titleElement.innerText = config.title.toUpperCase();
                    inputElement.value = "";
                    inputElement.style.borderColor = "#eee";
                    inputElement.placeholder = `Enter your ${config.title.toLowerCase()}...`;
                }
                openModal(config.id);
            });
        }
    });

    // --- 2. Universal Modal Save ---
    const universalSaveBtn = document.querySelector('#universalModal .save-btn');
    if (universalSaveBtn) {
        universalSaveBtn.onclick = function () {
            const input = document.getElementById('modalInput');
            const newValue = input.value.trim();

            if (newValue === "") {
                showToast("ERROR: THIS FIELD IS REQUIRED", true);
                input.style.borderColor = "#000";
                return;
            }

            updateRowDisplay(currentEditingRowId, newValue);
            showToast("DETAILS UPDATED");
            closeModal('universalModal');
        };
    }

    // --- 3. Motto Modal Save ---
    const mottoSaveBtn = document.querySelector('#mottoModal .save-btn');
    if (mottoSaveBtn) {
        mottoSaveBtn.onclick = function () {
            const newValue = document.getElementById('mottoInput').value.trim();
            updateRowDisplay(currentEditingRowId, newValue || "Set now");
            showToast(newValue ? "MOTTO UPDATED" : "MOTTO CLEARED");
            closeModal('mottoModal');
        };
    }

    // --- 4. Gender Modal Save ---
    const genderSaveBtn = document.querySelector('#genderModal .save-btn');
    if (genderSaveBtn) {
        genderSaveBtn.onclick = function () {
            const selected = document.querySelector('input[name="gender"]:checked');
            const val = selected ? selected.value : "Set now";
            updateRowDisplay(currentEditingRowId, val);
            showToast("GENDER UPDATED");
            closeModal('genderModal');
        };
    }

    // --- 5. Birthday Modal Save ---
    const birthdaySaveBtn = document.querySelector('#birthdayModal .save-btn');
    if (birthdaySaveBtn) {
        birthdaySaveBtn.onclick = function () {
            const val = document.getElementById('birthDateInput').value;
            updateRowDisplay(currentEditingRowId, val || "Set now");
            showToast(val ? "BIRTHDAY UPDATED" : "BIRTHDAY CLEARED");
            closeModal('birthdayModal');
        };
    }

    // --- 6. Password Modal Save ---
    const passwordSaveBtn = document.querySelector('#passwordModal .save-btn');
    if (passwordSaveBtn) {
        passwordSaveBtn.onclick = function () {
            const inputs = document.querySelectorAll('#passwordModal .modal-input');
            let empty = false;
            inputs.forEach(i => { if (!i.value) { i.style.borderColor = "#000"; empty = true; } });

            if (empty) {
                showToast("ERROR: ALL FIELDS REQUIRED", true);
                return;
            }
            showToast("PASSWORD CHANGED");
            closeModal('passwordModal');
        };
    }

    // --- 7. Helper: Update Display ---
    function updateRowDisplay(rowId, value) {
        const rowElement = document.getElementById(rowId);
        if (!rowElement) return;
        const valueSpan = rowElement.querySelector('.info-value');

        // Added 'bi' and 'bi-chevron-right' classes to ensure visibility via CSS
        if (value === "Set now") {
            valueSpan.innerHTML = `Set now <i class="bi bi-chevron-right"></i>`;
            valueSpan.classList.add('text-muted');
        } else {
            valueSpan.innerHTML = `${value} <i class="bi bi-chevron-right"></i>`;
            valueSpan.classList.remove('text-muted');
        }
    }

    window.addEventListener('click', e => { if (e.target.classList.contains('modal-overlay')) closeModal(e.target.id); });
});

function openModal(id) {
    const modal = document.getElementById(id);
    if (modal) {
        modal.style.display = 'flex';
        document.body.style.overflow = 'hidden';
    }
}

function closeModal(id) {
    const modal = document.getElementById(id);
    if (modal) {
        modal.style.display = 'none';
        document.body.style.overflow = '';
    }
}

function showToast(msg, isErr = false) {
    let t = document.querySelector('.toast-container');
    if (!t) {
        t = document.createElement('div');
        t.className = 'toast-container';
        document.body.appendChild(t);
    }
    t.className = 'toast-container' + (isErr ? ' error' : '');
    t.textContent = msg;
    setTimeout(() => t.classList.add('show'), 10);
    setTimeout(() => t.classList.remove('show'), 3000);
}

function showToast(msg, isErr = false) {
    // 1. Check if a toast container already exists, otherwise create it
    let t = document.querySelector('.toast-container');
    if (!t) {
        t = document.createElement('div');
        t.className = 'toast-container';
        document.body.appendChild(t);
    }

    // 2. Set the message and error styling
    t.textContent = msg;
    if (isErr) {
        t.classList.add('error');
    } else {
        t.classList.remove('error');
    }

    // 3. Trigger the "show" animation
    // We use a small timeout to ensure the browser registers the class change
    setTimeout(() => {
        t.classList.add('show');
    }, 10);

    // 4. Hide it after 3 seconds
    setTimeout(() => {
        t.classList.remove('show');
    }, 3000);
}