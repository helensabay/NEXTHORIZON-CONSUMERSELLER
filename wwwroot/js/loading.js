document.addEventListener('DOMContentLoaded', function () {
    var loadingOverlay = document.getElementById('loadingOverlay');
    if (!loadingOverlay) {
        return;
    }

    var minVisibleMs = 700;
    var maxVisibleMs = 2200;
    var startTime = Date.now();
    var hidden = false;

    function showLoadingScreen() {
        loadingOverlay.style.display = 'flex';
        loadingOverlay.classList.remove('hidden');
        hidden = false;
    }

    function hideLoadingScreen() {
        if (hidden) {
            return;
        }

        hidden = true;
        loadingOverlay.classList.add('hidden');

        window.setTimeout(function () {
            loadingOverlay.style.display = 'none';
        }, 450);
    }

    showLoadingScreen();

    window.addEventListener('beforeunload', function () {
        showLoadingScreen();
    });

    function hideWhenReady() {
        var elapsed = Date.now() - startTime;
        var remaining = Math.max(0, minVisibleMs - elapsed);

        window.setTimeout(hideLoadingScreen, remaining);
    }

    window.setTimeout(hideLoadingScreen, maxVisibleMs);

    if (document.readyState === 'complete') {
        hideWhenReady();
        return;
    }

    window.addEventListener('load', hideWhenReady, { once: true });
});
