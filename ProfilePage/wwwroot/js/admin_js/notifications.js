/**
 * Admin Notifications Logic
 */

function toggleRead(id, isRead) {
    const card = document.getElementById(`notif-${id}`);
    if (!card) return;

    if (isRead) {
        card.classList.remove('unread');
        card.classList.add('read');
    } else {
        card.classList.remove('read');
        card.classList.add('unread');
    }
}

function deleteNotif(id) {
    const card = document.getElementById(`notif-${id}`);
    if (!card) return;

    Swal.fire({
        title: 'Remove notification?',
        text: "This item will be permanently deleted.",
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: '#000',
        cancelButtonColor: '#d33',
        confirmButtonText: 'Delete'
    }).then((result) => {
        if (result.isConfirmed) {
            // Smooth exit
            card.style.opacity = '0';
            card.style.transform = 'scale(0.9) translateX(30px)';

            setTimeout(() => {
                card.remove();
                checkEmptyState();
            }, 300);
        }
    });
}

function markAllAsRead() {
    const unreadCards = document.querySelectorAll('.notif-card.unread');
    if (unreadCards.length === 0) return;

    unreadCards.forEach(card => {
        const id = card.id.split('-')[1];
        toggleRead(id, true);
    });

    Swal.fire({
        toast: true,
        position: 'top-end',
        icon: 'success',
        title: 'All marked as read',
        showConfirmButton: false,
        timer: 2000
    });
}

function checkEmptyState() {
    const container = document.getElementById('notificationContainer');
    const cards = container.querySelectorAll('.notif-card');

    if (cards.length === 0) {
        container.innerHTML = `
            <div class="text-center py-5 bg-white rounded-4 border border-dashed border-2">
                <i class="bi bi-bell-slash text-muted" style="font-size: 3.5rem;"></i>
                <h5 class="mt-3 fw-bold text-dark">No Notifications</h5>
                <p class="text-secondary small">Your moderation queue is currently empty.</p>
            </div>
        `;
    }
}