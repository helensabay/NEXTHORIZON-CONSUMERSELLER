document.addEventListener('DOMContentLoaded', function () {
    const loadingOverlay = document.getElementById('loadingOverlay');
    const minVisibleMs = 1200;
    const maxVisibleMs = 2500;
    const startTime = Date.now();
    let isHidden = false;

    document.documentElement.style.backgroundColor = '#ffffff';
    document.body.style.backgroundColor = '#ffffff';

    function onScroll() {
        const navbar = document.getElementById('navbar');
        if (!navbar) {
            return;
        }

        if (window.scrollY > 50) {
            navbar.classList.add('scrolled');
        } else {
            navbar.classList.remove('scrolled');
        }
    }

    window.addEventListener('scroll', onScroll);
    onScroll();

    if (!loadingOverlay) {
        return;
    }

    function showLoadingScreen() {
        loadingOverlay.style.display = 'flex';
        loadingOverlay.classList.remove('hidden');
        isHidden = false;
    }

    function hideLoadingScreen() {
        if (isHidden) {
            return;
        }

        isHidden = true;
        loadingOverlay.classList.add('hidden');

        setTimeout(function () {
            loadingOverlay.style.display = 'none';
        }, 500);
    }

    showLoadingScreen();

    window.addEventListener('beforeunload', function () {
        showLoadingScreen();
    });

    function hideWhenReady() {
        const elapsed = Date.now() - startTime;
        const remaining = Math.max(0, minVisibleMs - elapsed);

        setTimeout(function () {
            hideLoadingScreen();
        }, remaining);
    }

    setTimeout(function () {
        hideLoadingScreen();
    }, maxVisibleMs);

    if (document.readyState === 'complete') {
        hideWhenReady();
    } else {
        window.addEventListener('load', hideWhenReady, { once: true });
    }
});
