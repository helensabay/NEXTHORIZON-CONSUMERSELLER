/**
 * Logistics Partner Module - Admin Logic
 * Handles Search, Filtering, Modals, and SweetAlert2 Actions
 */

document.addEventListener('DOMContentLoaded', function () {
    // 1. Set default view to 'Active'
    filterPartners('active');

    // 2. Unified Search Functionality
    const searchInput = document.getElementById('logisticsSearch');
    if (searchInput) {
        searchInput.addEventListener('keyup', function () {
            const filter = this.value.toLowerCase();
            const cards = document.querySelectorAll('.partner-card');

            cards.forEach(card => {
                // Searches based on the 'data-name' attribute or the visible courier-name class
                const nameAttr = card.getAttribute('data-name')?.toLowerCase();
                const nameText = card.querySelector('.courier-name')?.innerText.toLowerCase();
                const name = nameAttr || nameText;

                if (name && name.includes(filter)) {
                    card.style.display = "";
                    // Adds a subtle entry animation if using animate.css
                    card.classList.add('animate__animated', 'animate__fadeIn');
                } else {
                    card.style.display = "none";
                }
            });
        });
    }
});

/**
 * Filter View Logic (Total / Active / Inactive)
 * @param {string} filter - The status type to display
 */
function filterPartners(filter) {
    const activeSection = document.getElementById('activeSection');
    const inactiveSection = document.getElementById('inactiveSection');
    const filterCards = document.querySelectorAll('.filter-card');

    // 1. Update UI Visuals (Toggle the black border/active state for stat cards)
    filterCards.forEach(card => card.classList.remove('active-filter', 'shadow'));
    const activeCard = document.getElementById(`stat-${filter}`);
    if (activeCard) activeCard.classList.add('active-filter', 'shadow');

    // 2. Toggle Section Visibility
    if (activeSection && inactiveSection) {
        switch (filter) {
            case 'total':
                activeSection.style.display = 'block';
                inactiveSection.style.display = 'block';
                break;
            case 'active':
                activeSection.style.display = 'block';
                inactiveSection.style.display = 'none';
                break;
            case 'inactive':
                activeSection.style.display = 'none';
                inactiveSection.style.display = 'block';
                break;
        }
    }
}

/**
 * Handle Image Preview for New Partner Modal
 */
function previewImage(input) {
    if (input.files && input.files[0]) {
        const reader = new FileReader();
        reader.onload = function (e) {
            const preview = document.getElementById('logoPreview');
            if (preview) preview.src = e.target.result;
        }
        reader.readAsDataURL(input.files[0]);
    }
}

/**
 * Modals & Partner Actions
 */
function openAddPartnerModal() {
    const modalElement = document.getElementById('addPartnerModal');
    if (modalElement) {
        const addModal = new bootstrap.Modal(modalElement);
        // Reset form fields
        const formInput = document.getElementById('newPartnerName');
        const imgPreview = document.getElementById('logoPreview');
        if (formInput) formInput.value = '';
        if (imgPreview) imgPreview.src = '/images/default-logo.png'; // Set to your default path
        addModal.show();
    }
}

function saveNewPartner() {
    const nameInput = document.getElementById('newPartnerName');
    const name = nameInput ? nameInput.value.trim() : "";
    const logo = document.getElementById('logoUpload')?.files[0];

    if (!name) {
        return Swal.fire({
            title: 'Input Required',
            text: 'Please enter a courier name.',
            icon: 'error',
            confirmButtonColor: '#000'
        });
    }

    // Logic for saving (Backend integration point)
    Swal.fire({
        title: 'Partner Added!',
        text: `${name} has been successfully registered.`,
        icon: 'success',
        confirmButtonColor: '#000'
    }).then(() => {
        const modalElement = document.getElementById('addPartnerModal');
        const modalInstance = bootstrap.Modal.getInstance(modalElement);
        if (modalInstance) modalInstance.hide();
        // location.reload(); // Uncomment if you want to refresh the list from DB
    });
}

function viewPerformance(name, rate) {
    const nameDisplay = document.getElementById('perfName');
    const rateText = document.getElementById('perfRateText');
    const progressBar = document.getElementById('perfProgressBar');

    if (nameDisplay) nameDisplay.innerText = name;
    if (rateText) rateText.innerText = rate + '%';
    if (progressBar) {
        progressBar.style.width = '0%';
        setTimeout(() => {
            progressBar.style.width = rate + '%';
        }, 200);
    }

    const perfModal = new bootstrap.Modal(document.getElementById('performanceModal'));
    perfModal.show();
}

function toggleStatus(name, element) {
    const isActivating = element.type === "checkbox" ? element.checked : true;

    Swal.fire({
        title: isActivating ? 'Enable Partner?' : 'Disable Partner?',
        text: `Visibility for ${name} will be updated for all sellers.`,
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: '#000',
        cancelButtonColor: '#d33',
        confirmButtonText: 'Confirm Changes'
    }).then((result) => {
        if (result.isConfirmed) {
            Swal.fire({
                title: 'Success',
                text: 'Status updated successfully.',
                icon: 'success',
                confirmButtonColor: '#000'
            });
            // Update logic here (e.g., AJAX to server)
        } else if (element.type === "checkbox") {
            element.checked = !isActivating; // Revert switch if cancelled
        }
    });
}

function removePartner(name) {
    Swal.fire({
        title: 'Archive Partner?',
        text: `Are you sure you want to remove ${name}? This action can be reversed in the Archives.`,
        icon: 'error',
        showCancelButton: true,
        confirmButtonColor: '#000',
        cancelButtonColor: '#6c757d',
        confirmButtonText: 'Archive Partner'
    }).then((result) => {
        if (result.isConfirmed) {
            Swal.fire({
                title: 'Archived!',
                text: 'The partner has been moved to archives.',
                icon: 'success',
                confirmButtonColor: '#000'
            });
            // AJAX call to set IsDeleted = true
        }
    });
}

/**
* Logistics Partner Module - Admin Logic
*/

document.addEventListener('DOMContentLoaded', function () {
    filterPartners('active'); // Default view
});

function filterPartners(filter) {
    const activeSection = document.getElementById('activeSection');
    const inactiveSection = document.getElementById('inactiveSection');
    const archiveSection = document.getElementById('archiveSection');
    const filterCards = document.querySelectorAll('.filter-card');

    // Update Tab Visuals
    filterCards.forEach(card => card.classList.remove('active-filter', 'shadow'));
    document.getElementById(`stat-${filter}`).classList.add('active-filter', 'shadow');

    // Handle Section Visibility
    // Hide everything first
    activeSection.style.display = 'none';
    inactiveSection.style.display = 'none';
    archiveSection.style.display = 'none';

    if (filter === 'total') {
        activeSection.style.display = 'block';
        inactiveSection.style.display = 'block';
    } else if (filter === 'active') {
        activeSection.style.display = 'block';
    } else if (filter === 'inactive') {
        inactiveSection.style.display = 'block';
    } else if (filter === 'archived') {
        archiveSection.style.display = 'block';
    }
}

function restorePartner(name) {
    Swal.fire({
        title: 'Restore Partner?',
        text: `Move ${name} back to the main list?`,
        icon: 'question',
        showCancelButton: true,
        confirmButtonColor: '#000',
        confirmButtonText: 'Yes, Restore'
    }).then((result) => {
        if (result.isConfirmed) {
            Swal.fire('Restored!', `${name} is now back in Inactive status.`, 'success');
            // Backend AJAX call: IsDeleted = false
        }
    });
}
