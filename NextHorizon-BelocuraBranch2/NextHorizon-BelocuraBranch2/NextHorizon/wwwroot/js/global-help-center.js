(function () {
    function initializeGlobalHelpCenter(root) {
        const openButton = root.querySelector('[data-help-center-open]');
        const closeButton = root.querySelector('[data-help-center-close]');
        const overlay = root.querySelector('[data-help-center-overlay]');
        const panel = root.querySelector('[data-help-center-panel]');
        const frame = root.querySelector('[data-help-center-frame]');
        const loading = root.querySelector('[data-help-center-loading]');
        const src = root.getAttribute('data-help-center-src');

        if (!openButton || !closeButton || !overlay || !panel || !frame || !src) {
            return;
        }

        let hasLoadedFrame = false;

        function markLoaded() {
            root.classList.add('is-loaded');
            if (loading) {
                loading.setAttribute('aria-hidden', 'true');
            }
        }

        function ensureFrameLoaded() {
            if (hasLoadedFrame) {
                return;
            }

            hasLoadedFrame = true;
            frame.src = src;
            frame.addEventListener('load', markLoaded, { once: true });
        }

        function openHelpCenter() {
            ensureFrameLoaded();
            overlay.hidden = false;
            panel.hidden = false;
            requestAnimationFrame(() => {
                root.classList.add('is-open');
                document.body.classList.add('global-help-center-open');
                openButton.setAttribute('aria-expanded', 'true');
            });
        }

        function closeHelpCenter() {
            root.classList.remove('is-open');
            document.body.classList.remove('global-help-center-open');
            openButton.setAttribute('aria-expanded', 'false');

            window.setTimeout(() => {
                if (!root.classList.contains('is-open')) {
                    overlay.hidden = true;
                    panel.hidden = true;
                }
            }, 260);
        }

        openButton.addEventListener('click', openHelpCenter);
        closeButton.addEventListener('click', closeHelpCenter);
        overlay.addEventListener('click', closeHelpCenter);

        document.addEventListener('keydown', (event) => {
            if (event.key === 'Escape' && root.classList.contains('is-open')) {
                closeHelpCenter();
            }
        });
    }

    document.addEventListener('DOMContentLoaded', () => {
        document.querySelectorAll('[data-help-center-root]').forEach(initializeGlobalHelpCenter);
    });
})();
