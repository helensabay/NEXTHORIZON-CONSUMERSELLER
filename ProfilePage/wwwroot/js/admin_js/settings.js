/**
 * SETTINGS.JS - Admin Portal
 * Handles Global Branding, Security, Moderation, and Audit Logging
 */

let pendingAction = null;

document.addEventListener('DOMContentLoaded', () => {
    // 1. Setup the Confirm Button inside the modal
    // We use a clone technique to ensure no duplicate event listeners attach on reload
    const confirmBtn = document.getElementById('confirmBtnExecute');
    if (confirmBtn) {
        const newConfirmBtn = confirmBtn.cloneNode(true);
        confirmBtn.parentNode.replaceChild(newConfirmBtn, confirmBtn);

        newConfirmBtn.addEventListener('click', () => {
            if (typeof pendingAction === 'function') {
                pendingAction();
                pendingAction = null; // Reset to prevent double-execution
            }
        });
    }
});

/**
 * 1. UTILITIES
 */

// Centralized Gatekeeper for all Admin actions
function openConfirmModal(title, message, action) {
    const modalEl = document.getElementById('confirmActionModal');
    if (!modalEl) return;

    document.getElementById('confirmTitle').innerText = title;
    document.getElementById('confirmBody').innerText = message;

    pendingAction = action;

    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    modal.show();
}

function showActionToast(message) {
    const toastEl = document.getElementById('settingsToast');
    const toastMsg = document.getElementById('toastMsg');
    if (toastEl && toastMsg) {
        toastMsg.innerText = message;
        const toast = bootstrap.Toast.getOrCreateInstance(toastEl);
        toast.show();
    }
}

function hideModal(modalId = 'confirmActionModal') {
    const modalElement = document.getElementById(modalId);
    if (!modalElement) return;
    const modal = bootstrap.Modal.getInstance(modalElement);
    if (modal) modal.hide();
}

function addAuditEntry(user, action, target) {
    const tableBody = document.getElementById('auditLogBody');
    if (!tableBody) return;

    const now = new Date().toLocaleString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
    const newRow = `
        <tr>
            <td>${now}</td>
            <td><strong>${user}</strong></td>
            <td>${action}</td>
            <td>${target}</td>
            <td><span class="badge bg-success-subtle text-success rounded-pill">Success</span></td>
        </tr>
    `;
    tableBody.insertAdjacentHTML('afterbegin', newRow);
}

/**
 * 2. GENERAL & BRANDING
 */

function previewImage(input) {
    if (input.files && input.files[0]) {
        const reader = new FileReader();
        reader.onload = function (e) {
            document.getElementById('logoPreview').src = e.target.result;
            showActionToast("Logo preview updated locally.");
        };
        reader.readAsDataURL(input.files[0]);
    }
}

function saveGeneralSettings() {
    const platform = document.getElementById('platformName').value;
    openConfirmModal(
        "Save Platform Settings",
        "Update global branding and measurement units?",
        () => {
            hideModal();
            addAuditEntry("System Admin", "Updated Branding", platform);
            showActionToast("Platform settings saved.");
        }
    );
}

/**
 * 3. ACCOUNT SECURITY
 */

function saveProfileInfo() {
    const name = document.getElementById('adminFullName').value;
    const email = document.getElementById('adminEmail').value;

    if (!name || !email) {
        showActionToast("Error: Name and Email cannot be empty.");
        return;
    }

    openConfirmModal(
        "Update Profile",
        `Change account details to ${name}?`,
        () => {
            hideModal();
            addAuditEntry("System Admin", "Profile Update", name);
            showActionToast("Profile information updated.");
        }
    );
}

function requestPasswordChange() {
    const currentPass = document.getElementById('currentPass').value;
    const newPass = document.getElementById('newPass').value;
    const confirmPass = document.getElementById('confirmNewPass').value;

    if (!currentPass || newPass !== confirmPass || newPass.length < 6) {
        showActionToast("Error: Check password requirements.");
        return;
    }

    openConfirmModal(
        "Update Password",
        "This will change your login credentials immediately.",
        () => {
            document.getElementById('currentPass').value = "";
            document.getElementById('newPass').value = "";
            document.getElementById('confirmNewPass').value = "";
            hideModal();
            addAuditEntry("System Admin", "Security Update", "Password Changed");
            showActionToast("Password updated successfully.");
        }
    );
}

/**
 * 4. MODERATION RULES
 */

function toggleAutoFlag() {
    const checkbox = document.getElementById('autoDQ');
    const newState = checkbox.checked;

    checkbox.checked = !newState; // Revert visually

    openConfirmModal(
        "Moderation Change",
        `Confirm ${newState ? 'ENABLING' : 'DISABLING'} AI Fraud Detection?`,
        () => {
            checkbox.checked = newState;
            hideModal();
            addAuditEntry("System Admin", "Toggle AI Moderation", newState ? "ENABLED" : "DISABLED");
            showActionToast("Moderation rules updated.");
        }
    );
}

/**
 * 5. PERMISSIONS
 */

function openInviteModal() {
    const modalEl = document.getElementById('inviteModModal');
    document.getElementById('modModalLabel').innerText = "Add New Moderator";
    document.getElementById('modEmail').value = "";
    document.getElementById('modRole').value = "Moderator";
    bootstrap.Modal.getOrCreateInstance(modalEl).show();
}

function editModerator(name, email, role) {
    const modalEl = document.getElementById('inviteModModal');
    document.getElementById('modModalLabel').innerText = "Edit Permissions: " + name;
    document.getElementById('modEmail').value = email;
    document.getElementById('modRole').value = role;
    bootstrap.Modal.getOrCreateInstance(modalEl).show();
}

function processModerator() {
    const email = document.getElementById('modEmail').value;
    if (!email) {
        showActionToast("Error: Email is required.");
        return;
    }
    // Nested confirmation for processModerator
    openConfirmModal(
        "Confirm Permissions",
        `Apply these access settings for ${email}?`,
        () => {
            hideModal(); // Hide confirm
            hideModal('inviteModModal'); // Hide input modal
            addAuditEntry("System Admin", "Moderator Access Update", email);
            showActionToast("Permissions updated.");
        }
    );
}

function requestRemoveAccess(name, rowId) {
    openConfirmModal(
        "Revoke Access",
        `Remove ${name} from the administrative team?`,
        () => {
            const row = document.getElementById(rowId);
            if (row) row.remove();
            hideModal();
            addAuditEntry("System Admin", "Revoked Access", name);
            showActionToast(`Access revoked for ${name}.`);
        }
    );
}

/**
 * 6. DANGER ZONE & SESSIONS
 */

function handleMaintenanceToggle() {
    const toggle = document.getElementById('maintenanceToggle');
    const newState = toggle.checked;
    toggle.checked = !newState;

    openConfirmModal(
        "Maintenance Mode",
        `Confirm ${newState ? 'ENABLING' : 'DISABLING'} maintenance?`,
        () => {
            toggle.checked = newState;
            hideModal();
            addAuditEntry("System Admin", "Toggle Maintenance", newState ? "ON" : "OFF");
            showActionToast(`Maintenance mode is now ${newState ? 'Active' : 'Inactive'}.`);
        }
    );
}

function requestPurgeData() {
    openConfirmModal(
        "Purge All Data",
        "Irreversible: This will permanently delete all flagged records.",
        () => {
            hideModal();
            addAuditEntry("System Admin", "Data Purge", "All Flagged Records");
            showActionToast("Database purge initiated.");
        }
    );
}

function requestExpireAdminSessions() {
    openConfirmModal(
        "Expire Admin Sessions",
        "Force all other administrators to log in again?",
        () => {
            hideModal();
            addAuditEntry("System Admin", "Expire Sessions", "All Administrators");
            showActionToast("Admin sessions expired.");
        }
    );
}

function requestSignOutAllUsers() {
    openConfirmModal(
        "Global Sign Out",
        "EMERGENCY: Force disconnect every user on the platform?",
        () => {
            hideModal();
            addAuditEntry("System Admin", "Global Sign Out", "All Users");
            showActionToast("Global sign-out command sent.");
        }
    );
}