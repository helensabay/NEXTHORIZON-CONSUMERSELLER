document.addEventListener('DOMContentLoaded', function () {
    let currentEditingRowId = null;

    const wrapper = document.querySelector('[data-update-profile-url]');
    const updateUrl = wrapper ? wrapper.dataset.updateProfileUrl : '';
    const rowMappings = {
        'name-row': { id: 'universalModal', title: 'Full Name', field: 'fullName', required: true },
        'motto-row': { id: 'mottoModal', field: 'motto', required: true },
        'gender-row': { id: 'genderModal', field: 'gender', required: false },
        'birthday-row': { id: 'birthdayModal', field: 'birthday', required: false },
        'phone-row': { id: 'universalModal', title: 'Phone Number', field: 'phone', required: true },
        'email-row': { id: 'universalModal', title: 'Email Address', field: 'email', required: true },
        'password-row': { id: 'passwordModal', required: true }
    };

    Object.keys(rowMappings).forEach(rowId => {
        const element = document.getElementById(rowId);
        if (!element) return;

        element.addEventListener('click', function () {
            currentEditingRowId = rowId;
            const config = rowMappings[rowId];
            const valueSpan = element.querySelector('.info-value');
            const currentValue = valueSpan?.dataset.value || '';

            if (config.id === 'universalModal') {
                const titleElement = document.getElementById('modalTitle');
                const inputElement = document.getElementById('modalInput');
                titleElement.innerText = config.title.toUpperCase();
                inputElement.value = currentValue;
                inputElement.style.borderColor = '#eee';
                inputElement.placeholder = `Enter your ${config.title.toLowerCase()}...`;
            }

            if (config.id === 'mottoModal') {
                const mottoInput = document.getElementById('mottoInput');
                if (mottoInput) {
                    mottoInput.value = currentValue;
                    mottoInput.style.borderColor = '#eee';
                }
            }

            if (config.id === 'genderModal') {
                document.querySelectorAll('input[name="gender"]').forEach(input => {
                    input.checked = input.value === currentValue;
                });
            }

            if (config.id === 'birthdayModal') {
                const birthDateInput = document.getElementById('birthDateInput');
                if (birthDateInput) {
                    birthDateInput.value = currentValue;
                }
            }

            openModal(config.id);
        });
    });

    const universalSaveBtn = document.querySelector('#universalModal .save-btn');
    if (universalSaveBtn) {
        universalSaveBtn.onclick = async function () {
            const input = document.getElementById('modalInput');
            const newValue = input.value.trim();
            const config = rowMappings[currentEditingRowId];

            if (config.required && newValue === '') {
                showToast('ERROR: THIS FIELD IS REQUIRED', true);
                input.style.borderColor = '#000';
                return;
            }

            const saved = await saveProfileField(config.field, newValue);
            if (!saved) return;

            updateRowDisplay(currentEditingRowId, saved.value || newValue);
            showToast('DETAILS UPDATED');
            closeModal('universalModal');
        };
    }

    const mottoSaveBtn = document.querySelector('#mottoModal .save-btn');
    if (mottoSaveBtn) {
        mottoSaveBtn.onclick = async function () {
            const input = document.getElementById('mottoInput');
            const newValue = input.value.trim();

            if (newValue === '') {
                showToast('ERROR: USERNAME IS REQUIRED', true);
                input.style.borderColor = '#000';
                return;
            }

            const saved = await saveProfileField('motto', newValue);
            if (!saved) return;

            updateRowDisplay(currentEditingRowId, saved.value || newValue);
            showToast('USERNAME UPDATED');
            closeModal('mottoModal');
        };
    }

    const passwordSaveBtn = document.querySelector('#passwordModal .save-btn');
    if (passwordSaveBtn) {
        passwordSaveBtn.onclick = function () {
            showToast('PASSWORD UPDATE IS NOT CONNECTED YET', true);
        };
    }

    const genderSaveBtn = document.querySelector('#genderModal .save-btn');
    if (genderSaveBtn) {
        genderSaveBtn.onclick = async function () {
            const selected = document.querySelector('input[name="gender"]:checked');
            const newValue = selected ? selected.value : '';
            const saved = await saveProfileField('gender', newValue);
            if (!saved) return;

            updateRowDisplay('gender-row', saved.value || newValue);
            showToast(newValue ? 'GENDER UPDATED' : 'GENDER CLEARED');
            closeModal('genderModal');
        };
    }

    const birthdaySaveBtn = document.querySelector('#birthdayModal .save-btn');
    if (birthdaySaveBtn) {
        birthdaySaveBtn.onclick = async function () {
            const input = document.getElementById('birthDateInput');
            const newValue = input ? input.value : '';
            const saved = await saveProfileField('birthday', newValue);
            if (!saved) return;

            updateRowDisplay('birthday-row', saved.value || newValue);
            showToast(newValue ? 'BIRTHDAY UPDATED' : 'BIRTHDAY CLEARED');
            closeModal('birthdayModal');
        };
    }

    async function saveProfileField(field, value) {
        if (!updateUrl) {
            showToast('UPDATE URL MISSING', true);
            return null;
        }

        try {
            const response = await fetch(updateUrl, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ field, value })
            });

            const result = await response.json().catch(() => ({}));
            if (!response.ok || result.success === false) {
                showToast(result.message || 'UPDATE FAILED', true);
                return null;
            }

            return result;
        } catch {
            showToast('UPDATE FAILED', true);
            return null;
        }
    }

    function updateRowDisplay(rowId, value) {
        const rowElement = document.getElementById(rowId);
        if (!rowElement) return;

        const valueSpan = rowElement.querySelector('.info-value');
        if (!valueSpan) return;

        const displayValue = value || 'Set now';
        valueSpan.dataset.value = value || '';
        valueSpan.innerHTML = `${escapeHtml(displayValue)} <i class="bi bi-chevron-right"></i>`;
        valueSpan.classList.toggle('text-muted', !value);
    }

    function escapeHtml(value) {
        return String(value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#039;');
    }

    window.addEventListener('click', e => {
        if (e.target.classList.contains('modal-overlay')) {
            closeModal(e.target.id);
        }
    });
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
    let toast = document.querySelector('.toast-container');
    if (!toast) {
        toast = document.createElement('div');
        toast.className = 'toast-container';
        document.body.appendChild(toast);
    }

    toast.textContent = msg;
    toast.classList.toggle('error', isErr);
    setTimeout(() => toast.classList.add('show'), 10);
    setTimeout(() => toast.classList.remove('show'), 3000);
}
