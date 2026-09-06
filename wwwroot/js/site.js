// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

(() => {
    'use strict';

    const installButtons = Array.from(document.querySelectorAll('[data-pwa-install]'));
    const isStandalone = window.matchMedia('(display-mode: standalone)').matches || window.navigator.standalone === true;
    const isIos = /iphone|ipad|ipod/i.test(window.navigator.userAgent);
    let deferredInstallPrompt = null;

    const setInstallButtonsVisible = visible => {
        installButtons.forEach(button => {
            button.classList.toggle('d-none', !visible);
            button.disabled = false;
        });
    };

    const showManualInstallHelp = () => {
        const message = isIos
            ? 'To install CTSHIP, tap Share in Safari, then choose Add to Home Screen.'
            : 'Use your browser menu and choose Install app or Add to home screen.';

        if (window.Swal) {
            window.Swal.fire({
                title: 'Install CTSHIP',
                text: message,
                icon: 'info',
                confirmButtonColor: '#3b703b'
            });
            return;
        }

        window.alert(message);
    };

    if (!isStandalone && isIos) {
        setInstallButtonsVisible(true);
    }

    window.addEventListener('beforeinstallprompt', event => {
        event.preventDefault();
        deferredInstallPrompt = event;
        setInstallButtonsVisible(true);
    });

    window.addEventListener('appinstalled', () => {
        deferredInstallPrompt = null;
        setInstallButtonsVisible(false);
    });

    installButtons.forEach(button => {
        button.addEventListener('click', async () => {
            if (!deferredInstallPrompt) {
                showManualInstallHelp();
                return;
            }

            button.disabled = true;
            deferredInstallPrompt.prompt();

            try {
                const choice = await deferredInstallPrompt.userChoice;
                deferredInstallPrompt = null;
                setInstallButtonsVisible(choice.outcome !== 'accepted');
            } catch {
                button.disabled = false;
            }
        });
    });

    if ('serviceWorker' in navigator) {
        window.addEventListener('load', () => {
            navigator.serviceWorker.register('/sw.js').catch(error => {
                if (window.console) {
                    console.warn('Service worker registration failed.', error);
                }
            });
        });
    }
})();
