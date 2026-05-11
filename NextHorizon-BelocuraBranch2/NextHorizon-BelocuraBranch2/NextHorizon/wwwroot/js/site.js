// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

function toggleNotifDropdown() {
    const dropdown = document.getElementById('notif-dropdown');
    if (!dropdown) return;
    dropdown.classList.toggle('open');
}

function clearNotifications() {
    const list = document.getElementById('notif-list');
    const dot = document.getElementById('notif-dot');
    if (list) {
        list.innerHTML = '<li class="notif-item"><div class="notif-body"><p class="notif-text">No new notifications</p></div></li>';
    }
    if (dot) {
        dot.classList.remove('active');
    }
}

function openLoginModal() {
    window.alert('Login modal is not implemented yet.');
}

function initOrderStatusTabs() {
    const tabs = document.querySelectorAll('.status-tab');
    const orders = document.querySelectorAll('.order-item');
    const emptyMessage = document.getElementById('empty-orders');

    if (!tabs.length || !orders.length) return;

    tabs.forEach((tab) => {
        tab.addEventListener('click', function () {
            const filter = tab.dataset.filter || 'all';
            let visibleCount = 0;

            tabs.forEach((item) => item.classList.remove('active'));
            tab.classList.add('active');

            orders.forEach((order) => {
                const status = order.dataset.status || 'all';
                const isVisible = filter === 'all' || filter === status;
                order.style.display = isVisible ? '' : 'none';
                if (isVisible) visibleCount += 1;
            });

            if (emptyMessage) {
                emptyMessage.style.display = visibleCount === 0 ? 'block' : 'none';
            }
        });
    });

    const statusFromUrl = new URLSearchParams(window.location.search).get('status');
    if (statusFromUrl) {
        const targetTab = Array.from(tabs).find((tab) => tab.dataset.filter === statusFromUrl);
        if (targetTab) {
            targetTab.click();
        }
    }
}

document.addEventListener('click', function (event) {
    const wrap = document.getElementById('notif-wrap');
    const dropdown = document.getElementById('notif-dropdown');
    if (!wrap || !dropdown) return;

    if (!wrap.contains(event.target)) {
        dropdown.classList.remove('open');
    }
});

document.addEventListener('DOMContentLoaded', function () {
    initOrderStatusTabs();
});
