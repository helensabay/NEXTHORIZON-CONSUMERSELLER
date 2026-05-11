/**
 * Tab Switching Logic
 */
function switchChallengeTab(viewName) {
    const leaderboardView = document.getElementById('view-leaderboard');
    const tasksView = document.getElementById('view-active-tasks');
    const btnLead = document.getElementById('tab-leaderboard');
    const btnTasks = document.getElementById('tab-active-tasks');

    if (viewName === 'leaderboard') {
        leaderboardView.classList.remove('d-none');
        tasksView.classList.add('d-none');
        btnLead.className = "btn btn-sm fw-bold bg-dark text-white rounded-0 px-3 py-2 active-tab me-1";
        btnTasks.className = "btn btn-sm text-muted rounded-0 px-3 py-2 me-1";
    } else {
        leaderboardView.classList.add('d-none');
        tasksView.classList.remove('d-none');
        btnTasks.className = "btn btn-sm fw-bold bg-dark text-white rounded-0 px-3 py-2 active-tab me-1";
        btnLead.className = "btn btn-sm text-muted rounded-0 px-3 py-2 me-1";
    }
}

/**
 * Filter and Search Logic
 */
function filterLeaderboard() {
    const filterEl = document.getElementById('categoryFilter');
    if (!filterEl) return; // No category filter present on this page — nothing to do
    const selectedCategory = filterEl.value;
    const rows = document.querySelectorAll('#leaderboardTable tbody tr');

    rows.forEach(row => {
        // Assuming Category is the 3rd column (index 2)
        const categoryBadge = row.querySelector('td:nth-child(3)').innerText.trim();

        // Match logic: Check if "All" is selected OR if the text matches the badge
        // Using includes to handle emojis (e.g., "Running" inside "🏃 Running")
        if (selectedCategory === "All" || categoryBadge.includes(selectedCategory)) {
            row.style.display = "";
        } else {
            row.style.display = "none";
        }
    });
}

// Search functionality listener
document.getElementById('leaderboardSearch')?.addEventListener('keyup', function (e) {
    const term = e.target.value.toLowerCase();
    const rows = document.querySelectorAll('#leaderboardTable tbody tr');

    rows.forEach(row => {
        const username = row.querySelector('td:nth-child(2)').innerText.toLowerCase();
        if (username.includes(term)) {
            row.style.display = "";
        } else {
            row.style.display = "none";
        }
    });
});

/**
 * Launch & Edit Challenge Modals
 */
function openCreateChallengeModal() {
    // Reset form
    document.getElementById('launchForm').reset();

    // Reset Image Preview specifically
    const preview = document.getElementById('imagePreview');
    const placeholder = document.getElementById('uploadPlaceholder');
    if (preview && placeholder) {
        preview.src = "#";
        preview.classList.add('d-none');
        placeholder.classList.remove('d-none');
    }

    new bootstrap.Modal(document.getElementById('launchChallengeModal')).show();
}

function openEditChallenge(challengeName) {
    console.log("Editing: " + challengeName);
    // In a real app, populate the form fields and image preview here based on ID
    new bootstrap.Modal(document.getElementById('launchChallengeModal')).show();
}

/**
 * Image Preview Logic
 */
function previewChallengeImage(input) {
    const preview = document.getElementById('imagePreview');
    const placeholder = document.getElementById('uploadPlaceholder');

    if (input.files && input.files[0]) {
        const reader = new FileReader();
        reader.onload = function (e) {
            preview.src = e.target.result;
            preview.classList.remove('d-none');
            placeholder.classList.add('d-none');
        }
        reader.readAsDataURL(input.files[0]);
    }
}

/**
 * User Profile View Logic
 */
function openUserView(username, rank, dist, activities, pts) {
    document.getElementById('userNameView').innerText = username;
    const initial = username.startsWith('@') ? username.charAt(1) : username.charAt(0);
    document.getElementById('userInitial').innerText = initial.toUpperCase();

    document.getElementById('userRankView').innerText = "Ranked " + rank;
    document.getElementById('userDistView').innerText = dist;
    document.getElementById('userActView').innerText = activities;
    document.getElementById('userPtsView').innerText = pts;

    new bootstrap.Modal(document.getElementById('userViewModal')).show();
}

/**
 * Override & Confirmation Logic
 */
function openOverrideModal(challengeName) {
    new bootstrap.Modal(document.getElementById('overrideModal')).show();
}

function confirmAction(message, colorClass = "text-success", icon = "bi-check-circle-fill") {
    const modalIds = ['launchChallengeModal', 'userViewModal', 'overrideModal'];

    modalIds.forEach(id => {
        const modalEl = document.getElementById(id);
        if (modalEl) {
            const modalInstance = bootstrap.Modal.getInstance(modalEl);
            if (modalInstance) modalInstance.hide();
        }
    });

    triggerToast(message, colorClass, icon);
}

function confirmOverride() {
    confirmAction("Challenge archived. Final results calculated.", "text-warning", "bi-shield-fill-check");
}

function saveChallengeAction() {
    confirmAction("Challenge live! Users will receive a notification.", "text-success", "bi-lightning-fill");
}

/**
 * Toast Notification System
 */
function triggerToast(msg, colorClass = "text-success", icon = "bi-check-circle-fill") {
    const toastMsgEl = document.getElementById('toastMsg');
    const toastIconEl = document.getElementById('toastIcon');
    const toastEl = document.getElementById('challengeToast');

    if (toastMsgEl && toastIconEl && toastEl) {
        toastMsgEl.innerText = msg;
        toastIconEl.className = `bi ${icon} ${colorClass} fs-5`;

        const toast = new bootstrap.Toast(toastEl, { delay: 3000 });
        toast.show();
    }
}
