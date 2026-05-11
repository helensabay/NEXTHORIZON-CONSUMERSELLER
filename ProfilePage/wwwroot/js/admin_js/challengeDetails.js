document.querySelector('input[placeholder="Search participant..."]')?.addEventListener('keyup', function (e) {
    const term = e.target.value.toLowerCase();
    const rows = document.querySelectorAll("tbody tr");

    rows.forEach(row => {
        const text = row.innerText.toLowerCase();
        row.style.display = text.includes(term) ? "" : "none";
    });
});

function exportParticipants() {
    const table = document.querySelector(".table");
    const rows = table.querySelectorAll("tr");
    let csvContent = "";

    // Iterate through rows
    rows.forEach((row, index) => {
        const cells = row.querySelectorAll("th, td");
        let rowData = [];

        cells.forEach((cell, cellIndex) => {
            // Skip the "Actions" column (usually the last one)
            if (cellIndex === cells.length - 1) return;

            // Clean up the text (remove extra spaces, newlines, and commas)
            let data = cell.innerText.replace(/(\r\n|\n|\r)/gm, " ").trim();
            data = data.replace(/"/g, '""'); // Escape double quotes

            // If data contains a comma, wrap it in quotes
            if (data.includes(",")) {
                data = `"${data}"`;
            }
            rowData.push(data);
        });

        csvContent += rowData.join(",") + "\n";
    });

    // Create a Blob and trigger download
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");

    // Set filename with current date
    const date = new Date().toISOString().split('T')[0];
    const challengeName = document.querySelector("h3").innerText.replace(/\s+/g, '_');

    link.setAttribute("href", url);
    link.setAttribute("download", `${challengeName}_Participants_${date}.csv`);
    link.style.visibility = 'hidden';

    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
}

/**
 * Helper to view specific activity log (Placeholder for routing)
 */
function viewActivityLog(athleteName) {
    console.log("Opening logs for: " + athleteName);
    // You could open a modal or route to /Admin/AthleteLogs?name=...
}

/**
* Unified Toast System
*/
function handleAction(message, type = 'success') {
    // Hide any open modals
    const openModals = ['editChallengeModal', 'activityDetailsModal'];
    openModals.forEach(id => {
        const modalEl = document.getElementById(id);
        const modalInstance = bootstrap.Modal.getInstance(modalEl);
        if (modalInstance) modalInstance.hide();
    });

    // Trigger Toast (Reusing the toast element from the main page)
    const toastEl = document.getElementById('challengeToast');
    const toastMsg = document.getElementById('toastMsg');
    const toastIcon = document.getElementById('toastIcon');

    if (toastEl && toastMsg) {
        toastMsg.innerText = message;

        // Adjust icon and color based on type
        if (type === 'success') {
            toastIcon.className = "bi bi-check-circle-fill text-success fs-5";
        } else if (type === 'warning') {
            toastIcon.className = "bi bi-exclamation-triangle-fill text-warning fs-5";
        }

        const toast = new bootstrap.Toast(toastEl);
        toast.show();
    }
}

function openEditModal() {
    var myModal = new bootstrap.Modal(document.getElementById('editChallengeModal'));
    myModal.show();
}
function viewActivityLog(athleteName) {
    document.getElementById('detailAthleteName').innerText = athleteName;
    new bootstrap.Modal(document.getElementById('activityDetailsModal')).show();
}

/**
* Handles the image preview for the Edit Modal
*/
function previewEditImage(input) {
    const preview = document.getElementById('editImagePreview');
    if (input.files && input.files[0]) {
        const reader = new FileReader();
        reader.onload = function (e) {
            preview.src = e.target.result;
        }
        reader.readAsDataURL(input.files[0]);
    }
}

/**
 * Open Modal Logic
 */
function openEditModal() {
    const modalEl = document.getElementById('editChallengeModal');
    const myModal = new bootstrap.Modal(modalEl);
    myModal.show();
}