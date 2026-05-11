let athleteToDQ = null;
let activeRow = null;

/**
 * Toggles between the Pending Queue and the Disqualified Archive
 * Also handles sub-filtering (Speed/Manual) within the Pending Queue
 */
function filterQueue(type) {
    const pendingSection = document.getElementById('pendingSection');
    const dqSection = document.getElementById('disqualifiedSection');
    const tabs = document.querySelectorAll('#modTabs .nav-link');
    const rows = document.querySelectorAll('#moderationTable tbody tr');

    // 1. Reset Tabs UI
    tabs.forEach(tab => {
        tab.classList.remove('active', 'bg-dark', 'text-white');
        tab.classList.add('bg-white', 'text-dark');
    });

    // 2. Highlight Active Tab
    event.currentTarget.classList.add('active', 'bg-dark', 'text-white');
    event.currentTarget.classList.remove('bg-white', 'text-dark');

    // 3. Toggle Sections
    if (type === 'disqualified_list') {
        pendingSection.classList.add('d-none');
        dqSection.classList.remove('d-none');
    } else {
        pendingSection.classList.remove('d-none');
        dqSection.classList.add('d-none');

        // 4. Sub-filter the Pending Rows (All / Speed / Manual)
        rows.forEach(row => {
            if (type === 'all' || row.getAttribute('data-type') === type) {
                row.style.display = '';
            } else {
                row.style.display = 'none';
            }
        });
    }
}

/**
 * Helper to jump back to queue from Archive header
 */
function toggleArchive(show) {
    // We simulate a click on the "All Flags" tab to trigger filterQueue('all')
    const allTab = document.querySelector('#modTabs .nav-link:first-child');
    if (show) {
        // Specifically find the archive tab and click it
        document.querySelector('[onclick="toggleArchive(true)"]').click();
    } else {
        allTab.click();
    }
}

/**
 * Populate and show the View Log Modal
 */
function viewActivityLog(name, reason, notes) {
    document.getElementById('logAthleteName').innerText = name;
    document.getElementById('logFlagReason').innerText = reason;
    document.getElementById('logAdminNotes').innerText = notes;

    const modal = new bootstrap.Modal(document.getElementById('logDetailsModal'));
    modal.show();
}

/**
 * Handle Disqualify button click (Opens confirmation modal)
 */
function openDQModal(btn, name) {
    athleteToDQ = name;
    activeRow = btn.closest('tr');

    document.getElementById('dqTargetName').innerText = name;
    const modal = new bootstrap.Modal(document.getElementById('dqModal'));
    modal.show();
}

/**
 * Execution logic for DQ (called from modal "Confirm" button)
 */
function confirmDQ() {
    const reason = document.getElementById('dqReasonSelect').value;

    // UI Feedback
    showToast(`${athleteToDQ} has been disqualified for ${reason}.`, "danger");

    // Animate and remove from pending
    removeRow(activeRow);

    // Close Modal
    bootstrap.Modal.getInstance(document.getElementById('dqModal')).hide();
}

/**
 * Handle Dismiss/Reinstate actions
 */
function handleModeration(btn, name, action) {
    const msg = action === 'approved'
        ? `Flag cleared for ${name}.`
        : `Athlete ${name} has been reinstated.`;

    showToast(msg, "success");
    removeRow(btn.closest('tr'));
}

/**
 * Reusable Row Removal Animation
 */
function removeRow(row) {
    row.style.transition = '0.4s';
    row.style.opacity = '0';
    row.style.transform = 'translateX(20px)';
    setTimeout(() => {
        row.remove();
        updatePendingCount();
    }, 400);
}

/**
 * Global Toast Handler
 */
function showToast(message, type) {
    const toastEl = document.getElementById('moderationToast');
    const toastMsg = document.getElementById('modToastMsg');
    const toastIcon = document.getElementById('modToastIcon');

    toastMsg.innerText = message;

    if (type === "success") {
        toastIcon.className = "bi bi-check-circle-fill text-success fs-5";
    } else {
        toastIcon.className = "bi bi-exclamation-triangle-fill text-danger fs-5";
    }

    const toast = new bootstrap.Toast(toastEl);
    toast.show();
}

/**
 * Keeps the "X Pending Reviews" badge updated
 */
function updatePendingCount() {
    const count = document.querySelectorAll('#moderationTable tbody tr').length;
    const badge = document.getElementById('pendingCount');
    if (badge) {
        badge.innerText = `${count} Pending Review${count !== 1 ? 's' : ''}`;
        if (count === 0) badge.classList.replace('bg-danger', 'bg-success');
    }
}

function reinstateAthlete(btn, name) {
    // 1. Show Toast Feedback
    showToast(`${name} has been reinstated to the leaderboard.`, "success");

    // 2. Visual Feedback (Loading state)
    btn.innerHTML = `<span class="spinner-border spinner-border-sm me-1"></span> Processing...`;
    btn.classList.add('disabled');

    // 3. Animate and remove the row
    const row = btn.closest('tr');
    setTimeout(() => {
        row.style.transition = '0.4s';
        row.style.opacity = '0';
        row.style.transform = 'scale(0.95)';

        setTimeout(() => {
            row.remove();
            // Optional: If no rows left, show a "No records" message
            checkEmptyArchive();
        }, 400);
    }, 600);
}

function checkEmptyArchive() {
    const tbody = document.getElementById('dqTableBody');
    if (tbody && tbody.children.length === 0) {
        tbody.innerHTML = `<tr><td colspan="5" class="py-5 text-muted small">No disqualified athletes in archive.</td></tr>`;
    }
}