
    function toggleUserDropdown(e) {
            if (e) e.stopPropagation();
    const menu = document.getElementById('user-menu-dropdown');
    if (menu) {
        menu.classList.toggle('show');
    console.log("Toggle clicked. 'show' class is now: " + menu.classList.contains('show'));
            }
        }

    // Close when clicking outside
    window.addEventListener('click', function(e) {
            const menu = document.getElementById('user-menu-dropdown');
    const btn = document.getElementById('user-menu-btn');
    if (menu && menu.classList.contains('show')) {
                if (!menu.contains(e.target) && !btn.contains(e.target)) {
        menu.classList.remove('show');
                }
            }
        });
